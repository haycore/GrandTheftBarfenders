using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime;
using Il2CppProtoDivision.AmberApp.Core.LobbyManagement;
using Il2CppProtoDivision.Game;
using Il2CppProtoDivision.Gameplay;
using MelonLoader;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>
/// Zeigt Waffen in den Händen anderer Figuren:
/// - Mitspieler: Jeder Spieler mit Mod schreibt in seine Steam-Lobby-Mitgliedsdaten (wie das Spiel z. B.
///   Avatar und Version), was er hält und wie oft er geschossen/geschlagen hat ("gtb" = "pistol|12|0").
///   Die anderen hängen das Modell an die rechte Hand seiner Figur und spielen Schuss bzw. Schlag ab.
/// - Händler: hält immer die Pistole (im Justier-Modus Schlaeger3P den Schläger).
/// </summary>
internal static class WeaponSync
{
    private const string Key = "gtb";
    private const float PollSeconds = 0.15f;
    private const float ViewScanSeconds = 1f;
    private const float SwingSeconds = 0.3f;

    public enum Kind { None, Pistol, Bat }

    // ---------- eigener Zustand ----------

    private static Kind _localKind;
    private static int _localShots, _localSwings, _localBoosts;
    private static uint _localBoostTarget;
    private static string _published;

    public static void SetLocal(Kind kind) { _localKind = kind; Publish(); }
    public static void LocalShot() { _localShots++; Publish(); }
    public static void LocalSwing() { _localSwings++; Publish(); }

    /// <summary>Mitspieler hat einen Mitarbeiter angetrieben: dem Host melden (Ziel + laufende Nummer).</summary>
    public static void LocalBoost(uint characterRawId) { _localBoostTarget = characterRawId; _localBoosts++; Publish(); }

    private static void Publish()
    {
        var value = $"{_localKind.ToString().ToLowerInvariant()}|{_localShots}|{_localSwings}|{_localBoostTarget}|{_localBoosts}";
        if (value == _published) return;
        try
        {
            LobbyManager.SetLocalMemberData(Key, value);
            _published = value;
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("sync-publish", $"[Sync] Lobby-Daten nicht setzbar: {e.Message}");
        }
    }

    // ---------- fremde Hände ----------

    private sealed class Shown
    {
        public Kind Kind;
        public GameObject Root;
        public WeaponModels.Pistol Pistol;
        public Transform Hand;
        public int Shots, Swings;
        public float SwingStart = -1f;
        public Quaternion SwingRest;
    }

    private static readonly Dictionary<ulong, Shown> _shown = new();
    private static readonly Dictionary<uint, Kind> _kindByCharacter = new();

    /// <summary>Was die Spielfigur gerade in der Hand hat (eigene Figur oder Mitspieler mit Mod).</summary>
    public static Kind KindOf(uint characterRawId)
    {
        var local = GameAccess.LocalPlayer;
        if (local != null && local.CharacterId.CharacterIdRawValue == characterRawId) return _localKind;
        return _kindByCharacter.TryGetValue(characterRawId, out var kind) ? kind : Kind.None;
    }
    private static readonly Dictionary<uint, CharacterView> _views = new();
    private static float _nextPoll, _nextViewScan;
    private const ulong VendorKey = 0;

    public static void Reset()
    {
        foreach (var shown in _shown.Values) if (shown.Root != null) UnityEngine.Object.Destroy(shown.Root);
        _shown.Clear();
        _views.Clear();
        _kindByCharacter.Clear();
        _seenBoosts.Clear();
        _localKind = Kind.None;
        Publish();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Update()
    {
        AnimateSwings();
        if (Time.realtimeSinceStartup < _nextPoll) return;
        _nextPoll = Time.realtimeSinceStartup + PollSeconds;

        try
        {
            var seen = new HashSet<ulong>();
            UpdatePlayers(seen);
            UpdateVendor(seen);
            foreach (var key in _shown.Keys.Where(k => !seen.Contains(k)).ToList()) Remove(key);
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("sync-update", $"[Sync] Fehler: {e}");
        }
    }

    private static void UpdatePlayers(HashSet<ulong> seen)
    {
        var gamers = GameAccess.ViewWorld?.Gamer?.Gamers;
        var local = NetworkSubsystem.Instance?.LocalGamer;
        if (gamers == null || local == null) return;

        for (var i = 0; i < gamers.Count; i++)
        {
            var gamer = gamers[i];
            if (gamer == null || !gamer.IsConnected || gamer.SteamAccountId == 0 || gamer.SteamAccountId == local.SteamAccountId) continue;
            if (!LobbyManager.TryGetMemberData(gamer.SteamAccountId, Key, out var value) || string.IsNullOrEmpty(value)) continue;

            var parts = value.Split('|');
            var kind = parts[0] switch { "pistol" => Kind.Pistol, "bat" => Kind.Bat, _ => Kind.None };
            var shots = parts.Length > 1 && int.TryParse(parts[1], out var s) ? s : 0;
            var swings = parts.Length > 2 && int.TryParse(parts[2], out var w) ? w : 0;
            var boostTarget = parts.Length > 3 && uint.TryParse(parts[3], out var bt) ? bt : 0u;
            var boosts = parts.Length > 4 && int.TryParse(parts[4], out var b) ? b : 0;
            HandleRemoteBoost(gamer.SteamAccountId, boostTarget, boosts);
            if (!gamer.TryGetControlledCharacterId(out var characterId)) continue;
            _kindByCharacter[characterId.CharacterIdRawValue] = kind;
            if (kind == Kind.None) continue;

            var view = FindView(characterId.CharacterIdRawValue);
            if (view == null) continue;
            seen.Add(gamer.SteamAccountId);
            var shown = Show(gamer.SteamAccountId, kind, view);
            if (shown == null) continue;

            if (shots > shown.Shots) Fire(shown);
            if (swings > shown.Swings) StartSwing(shown);
            shown.Shots = shots;
            shown.Swings = swings;
        }
    }

    private static readonly Dictionary<ulong, int> _seenBoosts = new();

    /// <summary>Host: neue Antreib-Meldung eines Mitspielers ausführen (alte beim ersten Lesen nicht nachholen).</summary>
    private static void HandleRemoteBoost(ulong steamId, uint target, int count)
    {
        if (!_seenBoosts.TryGetValue(steamId, out var seen))
        {
            _seenBoosts[steamId] = count;
            return;
        }
        if (count <= seen) return;
        _seenBoosts[steamId] = count;
        if (target != 0) SpeedBoost.Boost(target);
    }

    private static void UpdateVendor(HashSet<ulong> seen)
    {
        var vendor = Vendor.FindVendor(GameAccess.ViewWorld);
        if (vendor == null || vendor.IsRagdoll) return;
        var view = FindView(vendor.CharacterId.CharacterIdRawValue);
        if (view == null) return;
        seen.Add(VendorKey);
        Show(VendorKey, Tuning.Active == Tuning.Target.Schlaeger3P ? Kind.Bat : Kind.Pistol, view);
    }

    /// <summary>Modell an der Hand anzeigen; bei neuer Waffe oder neuer Figur neu bauen.</summary>
    private static Shown Show(ulong key, Kind kind, CharacterView view)
    {
        _shown.TryGetValue(key, out var shown);
        var hand = shown?.Hand;
        if (shown != null && shown.Kind == kind && shown.Root != null && hand != null && hand.IsChildOf(view.transform)) return shown;

        if (shown?.Root != null) UnityEngine.Object.Destroy(shown.Root);
        hand = WeaponModels.FindRightHand(view);
        if (hand == null)
        {
            GameAccess.LogOnce("sync-hand-" + view.name, $"[Sync] Keine rechte Hand gefunden an '{view.name}'.");
            _shown.Remove(key);
            return null;
        }

        var keepCounters = shown;
        shown = new Shown { Kind = kind, Hand = hand, Shots = keepCounters?.Shots ?? 0, Swings = keepCounters?.Swings ?? 0 };
        if (kind == Kind.Pistol)
        {
            shown.Pistol = WeaponModels.CreatePistolThirdPerson(hand);
            shown.Root = shown.Pistol.Root;
        }
        else shown.Root = WeaponModels.CreateBatThirdPerson(hand);
        _shown[key] = shown;
        if (key != VendorKey) MelonLogger.Msg($"[Sync] Mitspieler hält jetzt: {kind}");
        return shown;
    }

    private static void Remove(ulong key)
    {
        if (_shown.TryGetValue(key, out var shown) && shown.Root != null) UnityEngine.Object.Destroy(shown.Root);
        _shown.Remove(key);
    }

    private static void Fire(Shown shown)
    {
        Weapons.FireModel(shown.Pistol?.Gun);
        var clips = shown.Pistol?.FireClips;
        if (clips != null && clips.Length > 0 && shown.Pistol.Audio != null)
            shown.Pistol.Audio.PlayOneShot(clips[UnityEngine.Random.Range(0, clips.Length)]);
    }

    private static void StartSwing(Shown shown)
    {
        if (shown.Root == null || shown.SwingStart >= 0f) return;
        shown.SwingRest = shown.Root.transform.localRotation;
        shown.SwingStart = Time.time;
    }

    private static void AnimateSwings()
    {
        foreach (var shown in _shown.Values)
        {
            if (shown.SwingStart < 0f || shown.Root == null) continue;
            var t = (Time.time - shown.SwingStart) / SwingSeconds;
            if (t >= 1f)
            {
                shown.Root.transform.localRotation = shown.SwingRest;
                shown.SwingStart = -1f;
                continue;
            }
            shown.Root.transform.localRotation = shown.SwingRest * Quaternion.Euler(70f * Mathf.Sin(t * Mathf.PI), 0f, 0f);
        }
    }

    /// <summary>Figuren-Ansicht zu einer Charakter-Id (gecacht, jede Sekunde neu eingesammelt).</summary>
    private static CharacterView FindView(uint rawId)
    {
        if (_views.TryGetValue(rawId, out var cached) && cached != null && cached.CharacterData != null
            && cached.CharacterData.CharacterId.CharacterIdRawValue == rawId) return cached;
        if (Time.realtimeSinceStartup < _nextViewScan) return null;
        _nextViewScan = Time.realtimeSinceStartup + ViewScanSeconds;

        _views.Clear();
        foreach (var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<CharacterView>()))
        {
            var view = obj.TryCast<CharacterView>();
            var data = view?.CharacterData;
            if (data != null) _views[data.CharacterId.CharacterIdRawValue] = view;
        }
        return _views.TryGetValue(rawId, out var found) ? found : null;
    }
}

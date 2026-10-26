using System.Runtime.CompilerServices;
using Il2CppProtoDivision.Gameplay;
using Il2CppProtoDivision.Gameplay.DataTypes;
using MelonLoader;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandTheftBarfenders;

/// <summary>
/// Der Waffenhändler steht fest hinter der Bar. Das Spiel kennt einen (sonst ungenutzten) Figurentyp
/// "Bartender"; der Host erzeugt davon eine Figur am Händler-Platz. Weil sie eine echte Spielfigur ist,
/// sehen sie auch Mitspieler.
///
/// Ansehen + E öffnet das Kaufmenü (bei jedem Spieler mit Mod).
/// </summary>
internal static class Vendor
{
    public const string Name = "Trevor Philips";

    // Hinter der Theke, Blick zu den Gästen (im Spiel mit F7 ausgemessen, 2026-10-24).
    private static readonly float3 Spot = new(-32.05f, 0f, 15.79f);
    private const float SpotYaw = 120f;

    private const float TalkDistance = 2.8f;
    private const float TalkMaxAngle = 30f;
    private const float EnsureIntervalSeconds = 1f;
    private const float MoveBackDistance = 1.0f;

    private static float _nextEnsure;
    private static bool _loggedSpawn;
    private static bool _replacedThisSession;

    public static void Reset()
    {
        _nextEnsure = 0f;
        _loggedSpawn = false;
        _replacedThisSession = false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Update()
    {
        if (Time.realtimeSinceStartup < _nextEnsure) return;
        _nextEnsure = Time.realtimeSinceStartup + EnsureIntervalSeconds;
        try { EnsureSpawned(); }
        catch (Exception e) { GameAccess.LogOnce("vendor-ensure", $"[Händler] Erzeugen fehlgeschlagen: {e}"); }
    }

    /// <summary>Host: Händler erzeugen, falls er im Spielstand noch fehlt, und an seinem Platz halten.</summary>
    private static void EnsureSpawned()
    {
        var logic = GameAccess.LogicWorld;
        if (logic?.Character == null) return;

        var vendor = FindVendor(logic);
        if (vendor != null && vendor.CharacterAppearance != CharacterAppearance.Masculine && !_replacedThisSession)
        {
            // Älterer Händler mit weiblicher Figur: einmal durch einen männlichen ersetzen.
            _replacedThisSession = true;
            RemoveVendor(logic, vendor);
            MelonLogger.Msg("[Händler] Weibliche Figur entfernt, erzeuge männlichen Händler.");
            vendor = null;
        }

        if (vendor == null)
        {
            var created = logic.Character.CreateNewBartenderCharacter(Spot, SpotYaw);
            // Gleich nach dem Erzeugen, bevor die Anzeige die Figur lädt.
            created.CharacterAppearance = CharacterAppearance.Masculine;
            created.AppearanceVariantIndex = 0;
            MelonLogger.Msg($"[Händler] Erzeugt hinter der Bar bei ({Spot.x:0.0}, {Spot.y:0.0}, {Spot.z:0.0}).");
            _loggedSpawn = true;
            return;
        }

        if (!_loggedSpawn)
        {
            MelonLogger.Msg("[Händler] Im Spielstand vorhanden.");
            _loggedSpawn = true;
        }

        // Umgeschubst, weggelaufen oder (aus einer älteren Version) woanders aufgestellt? Zurück an den Platz.
        var p = vendor.Position;
        float dx = p.x - Spot.x, dz = p.z - Spot.z;
        if (!vendor.IsRagdoll && dx * dx + dz * dz > MoveBackDistance * MoveBackDistance)
        {
            // Derselbe Weg wie das Spiel beim Teleportieren von Figuren.
            vendor.PendingTeleportPosition = Spot;
            vendor.TeleportRequestVersion += 1;
        }
        vendor.FaceYaw(SpotYaw);
        Calm(vendor);
    }

    /// <summary>
    /// Trevor hat keine KI: Wird er geschlagen oder angeschossen, bliebe er für immer im Kampfmodus bzw.
    /// "provoziert" – und das Spiel sähe eine laufende Schlägerei (Tag lässt sich nicht beenden).
    /// </summary>
    public static void Calm(CharacterData vendor)
    {
        if (vendor == null) return;
        if (vendor.IsInFightMode) vendor.SetFightMode(false);
        if (vendor.ProvokedByCharacterId.CharacterIdRawValue != 0 || vendor.ProvokedEndTime.GameTimeRawValue != 0)
        {
            vendor.ProvokedByCharacterId = default;
            vendor.ProvokedStartTime = default;
            vendor.ProvokedEndTime = default;
        }
    }

    /// <summary>
    /// Nimmt den Händler aus allen Figurenlisten. Die Anzeige vergleicht die Bartender-Liste und räumt
    /// seine Figur dann selbst weg (bei Mitspielern über den normalen Abgleich).
    /// </summary>
    private static void RemoveVendor(Il2CppProtoDivision.Game.GameWorld logic, CharacterData vendor)
    {
        var characters = logic.Character;
        var bartender = vendor.TryCast<BartenderCharacterData>();
        if (bartender != null) characters.BartenderCharacters.Remove(bartender);
        characters.AllCharacters.Remove(vendor);
        characters.CharactersById?.Remove(vendor.CharacterId);
        characters._characterByIdCache?.Remove(vendor.CharacterId);
        try { bartender?.OnRemovedFromGame(); }
        catch (Exception e) { MelonLogger.Warning($"[Händler] Aufräumen der alten Figur: {e.Message}"); }
    }

    /// <summary>Der Händler ist die (einzige) Bartender-Figur; das Spiel selbst erzeugt keine.</summary>
    public static CharacterData FindVendor(Il2CppProtoDivision.Game.GameWorld world)
    {
        var list = world?.Character?.BartenderCharacters;
        if (list == null || list.Count == 0) return null;
        return list[0];
    }

    /// <summary>Schaut der Spieler den Händler aus der Nähe an?</summary>
    public static bool IsLookingAtVendor()
    {
        var vendor = FindVendor(GameAccess.ViewWorld);
        var camera = Camera.main;
        if (vendor == null || camera == null || vendor.IsRagdoll || vendor.IsFainted) return false;

        var chest = new Vector3(vendor.Position.x, vendor.Position.y + 1.3f, vendor.Position.z);
        var toVendor = chest - camera.transform.position;
        if (toVendor.magnitude > TalkDistance) return false;
        return Vector3.Angle(camera.transform.forward, toVendor) <= TalkMaxAngle;
    }

    public static bool WasTalkPressed()
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked && IsLookingAtVendor();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void DrawHint()
    {
        if (Cursor.lockState != CursorLockMode.Locked || !IsLookingAtVendor()) return;
        const float width = 260f;
        GUI.Box(new Rect((Screen.width - width) / 2f, Screen.height / 2f + 40f, width, 30f), $"[E] {Name}");
    }
}

using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandTheftBarfenders;

/// <summary>
/// Pistole und Schläger in der Hand: Taste 1 / 2 zieht bzw. steckt weg, linke Maustaste schießt bzw.
/// schlägt. Jeder Treffer wirkt wie ein Faustschlag des Spielers (siehe GameAccess.ApplyHit).
/// </summary>
internal static class Weapons
{
    private enum Held { None, Pistol, Bat }

    private const float ShotRange = 40f;
    private const float ShotCooldown = 0.15f;
    private const float BatReach = 2.2f;
    private const float BatMaxAngle = 55f;
    private const float BatCooldown = 0.6f;
    private const float BatSwingSeconds = 0.3f;

    private static Held _held;
    private static WeaponModels.Pistol _pistol;
    private static GameObject _bat;
    private static Quaternion _batRest;
    private static float _nextAction;
    private static float _swingStart = -1f;

    public static bool IsPistolDrawn => _held == Held.Pistol;
    public static bool IsAnyDrawn => _held != Held.None;

    public static bool IsOwnObject(Transform transform)
    {
        for (var t = transform; t != null; t = t.parent)
            if (t.name.StartsWith("GTB_")) return true;
        return false;
    }

    /// <summary>Spielstand gewechselt oder Menü offen: alles wegstecken.</summary>
    public static void Holster()
    {
        if (_pistol?.Root != null) UnityEngine.Object.Destroy(_pistol.Root);
        if (_bat != null) UnityEngine.Object.Destroy(_bat);
        _pistol = null;
        _bat = null;
        _swingStart = -1f;
        if (_held == Held.None) return;
        _held = Held.None;
        WeaponSync.SetLocal(WeaponSync.Kind.None);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Update(bool menuOpen)
    {
        var player = GameAccess.LocalPlayer;
        var camera = Camera.main;
        if (player == null || camera == null)
        {
            if (_held != Held.None) Holster();
            return;
        }

        // Modell weg (z. B. Kamera gewechselt)? Dann gilt die Waffe als weggesteckt.
        if (_held == Held.Pistol && (_pistol?.Root == null)) Holster();
        if (_held == Held.Bat && _bat == null) Holster();

        // Hände voll (Glas, Kiste …) oder ohnmächtig: Waffe weg.
        if (_held != Held.None && (player.IsCarrying || player.IsRagdoll || player.IsFainted)) Holster();

        var keyboard = Keyboard.current;
        if (!menuOpen && keyboard != null && InGameplay())
        {
            if (keyboard[Settings.PistolKey].wasPressedThisFrame) Toggle(Held.Pistol, camera, player);
            else if (keyboard[Settings.BatKey].wasPressedThisFrame) Toggle(Held.Bat, camera, player);
        }

        AnimateSwing();

        var mouse = Mouse.current;
        if (menuOpen || mouse == null || !mouse.leftButton.wasPressedThisFrame || !InGameplay()) return;
        if (Time.time < _nextAction) return;

        if (_held == Held.Pistol) Shoot(camera);
        else if (_held == Held.Bat) Swing(camera);
    }

    /// <summary>Spielt der Spieler gerade (Maus gefangen)? In Menüs, Handy, Dialogen ist sie frei.</summary>
    private static bool InGameplay() => Cursor.lockState == CursorLockMode.Locked;

    private static void Toggle(Held wanted, Camera camera, Il2CppProtoDivision.Gameplay.CharacterData player)
    {
        if (_held == wanted)
        {
            Holster();
            return;
        }
        if (player.IsCarrying)
        {
            Notifier.Show("Erst die Hände frei machen.");
            return;
        }

        if (wanted == Held.Pistol && Settings.Ammo.Value <= 0)
        {
            Notifier.Show($"Keine Pistole – bei {Vendor.Name} hinter der Bar kaufen.");
            return;
        }
        if (wanted == Held.Bat && !Settings.HasBat.Value)
        {
            Notifier.Show($"Kein Schläger – bei {Vendor.Name} hinter der Bar kaufen.");
            return;
        }

        Holster();
        try
        {
            if (wanted == Held.Pistol)
            {
                _pistol = WeaponModels.CreatePistolFirstPerson(camera.transform);
                Play(_pistol.ReloadClip);
            }
            else _bat = WeaponModels.CreateBatFirstPerson(camera.transform);
            _held = wanted;
            WeaponSync.SetLocal(wanted == Held.Pistol ? WeaponSync.Kind.Pistol : WeaponSync.Kind.Bat);
            _nextAction = Time.time + 0.2f;
        }
        catch (Exception e)
        {
            MelonLogger.Error($"[Waffe] Ziehen fehlgeschlagen: {e}");
            Holster();
        }
    }

    private static void Shoot(Camera camera)
    {
        _nextAction = Time.time + ShotCooldown;
        // Das Modell der Schießbude spielt Abzug, Hahn, Trommel und Rückstoß ab. Seine Trommel hat nur 6 Schuss
        // und lädt dann nach (Klicks gingen verloren): deshalb vor jedem Schuss wieder auffüllen.
        FireModel(_pistol.Gun);

        Settings.Ammo.Value = Math.Max(0, Settings.Ammo.Value - 1);
        Settings.Save();
        PlayRandom(_pistol.FireClips);
        WeaponSync.LocalShot();

        var ray = new Ray(camera.transform.position, camera.transform.forward);
        var target = Targeting.FindShotTarget(ray, ShotRange, out var distance);
        if (target != null)
        {
            MelonLogger.Msg($"[Pistole] Treffer in {distance:0.0} m");
            GameAccess.ApplyHit(target, ray.direction);
        }

        if (Settings.Ammo.Value <= 0)
        {
            Notifier.Show($"Magazin leer – die Pistole ist weg. {Vendor.Name} hat neue.");
            Holster();
        }
    }

    /// <summary>Schuss-Animation des Schießbuden-Modells, ohne dass es nachlädt oder einen Klick verschluckt.</summary>
    public static void FireModel(Il2CppProtoDivision.Game.ShootingGalleryGunView gun)
    {
        if (gun == null) return;
        try
        {
            gun.RoundsLeft = Math.Max(1, gun.Capacity);
            gun.SinceShot = 10f; // Abklingzeit des Modells überspringen; das Tempo regelt ShotCooldown
            gun.Fire();
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("fire-model", $"[Pistole] Schuss-Animation fehlgeschlagen: {e.Message}");
        }
    }

    private static void Swing(Camera camera)
    {
        _nextAction = Time.time + BatCooldown;
        _batRest = _bat.transform.localRotation;
        _swingStart = Time.time;
        WeaponSync.LocalSwing();

        var player = GameAccess.LocalPlayer;
        var origin = new Vector3(player.Position.x, player.Position.y, player.Position.z);
        var target = Targeting.FindMeleeTarget(origin, camera.transform.forward, BatReach, BatMaxAngle);
        if (target == null) return;

        // Mitarbeiter: kein Schaden, kein Taumeln – nur antreiben (20 s schneller).
        if (target.TryCast<Il2CppProtoDivision.Gameplay.StaffCharacterData>() != null)
        {
            MelonLogger.Msg("[Schläger] Mitarbeiter angetrieben");
            var rawId = target.CharacterId.CharacterIdRawValue;
            if (GameAccess.LogicWorld != null) SpeedBoost.Boost(rawId);
            else WeaponSync.LocalBoost(rawId); // der Host treibt an
            return;
        }

        var direction = new Vector3(target.Position.x, target.Position.y, target.Position.z) - origin;
        MelonLogger.Msg("[Schläger] Treffer");
        GameAccess.ApplyHit(target, direction);
    }

    /// <summary>Schlag-Bewegung: nach vorn-unten durchziehen und zurück.</summary>
    private static void AnimateSwing()
    {
        if (_bat == null || _swingStart < 0f) return;
        var t = (Time.time - _swingStart) / BatSwingSeconds;
        if (t >= 1f)
        {
            _bat.transform.localRotation = _batRest;
            _swingStart = -1f;
            return;
        }
        var arc = Mathf.Sin(t * Mathf.PI); // 0 → 1 → 0
        _bat.transform.localRotation = _batRest * Quaternion.Euler(75f * arc, -25f * arc, 0f);
    }

    private static void PlayRandom(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<AudioClip> clips)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[UnityEngine.Random.Range(0, clips.Length)]);
    }

    private static void Play(AudioClip clip)
    {
        if (clip == null || _pistol?.Audio == null) return;
        _pistol.Audio.PlayOneShot(clip);
    }

    /// <summary>Munitionsanzeige unten rechts, solange die Pistole gezogen ist.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void DrawHud()
    {
        if (_held != Held.Pistol) return;
        GUI.Box(new Rect(Screen.width - 190f, Screen.height - 70f, 170f, 40f), $"Munition: {Settings.Ammo.Value}");
    }
}

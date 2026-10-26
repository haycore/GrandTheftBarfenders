using System.Runtime.CompilerServices;
using GrandTheftBarfenders;
using MelonLoader;

[assembly: MelonInfo(typeof(GrandTheftBarfendersMod), "Grand Theft Barfenders", "1.4.0", "Tobi")]
[assembly: MelonGame("ProtoDivision", "Barfender")]
[assembly: HarmonyDontPatchAll]

namespace GrandTheftBarfenders;

public class GrandTheftBarfendersMod : MelonMod
{
    private IntPtr _lastWorld;

    public override void OnInitializeMelon()
    {
        Settings.Init();

        // Patches von Hand anwenden (HarmonyDontPatchAll), damit ein Fehler hier geloggt wird
        // statt das Laden des Mods still abzubrechen.
        try
        {
            HarmonyInstance.PatchAll(typeof(SpeedBoost).Assembly);
        }
        catch (Exception e)
        {
            LoggerInstance.Error($"Patches (Speedboost, Eingabe-Sperre) konnten nicht angewendet werden: {e}");
        }

        LoggerInstance.Msg($"Geladen. {Vendor.Name} steht hinter der Bar (E). Pistole: {Settings.PistolKey}, Schläger: {Settings.BatKey}, Justieren: F6.");
    }

    // Die Il2Cpp-Aufrufe stecken in eigenen, nicht inline-fähigen Methoden: So lädt der JIT
    // die zugehörigen Spiel-Typen erst, wenn sie wirklich gebraucht werden.
    public override void OnUpdate()
    {
        if (!TrackWorld()) return;
        Vendor.Update();
        Shop.Update();
        Weapons.Update(Shop.IsOpen);
        Tuning.Update();
        WeaponSync.Update();
    }

    /// <summary>true, solange ein Spielstand läuft; beim Wechsel alles zurücksetzen.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TrackWorld()
    {
        var world = GameAccess.ViewWorld;
        var pointer = world?.Pointer ?? IntPtr.Zero;
        if (pointer != _lastWorld)
        {
            _lastWorld = pointer;
            Shop.Close();
            Weapons.Holster();
            Vendor.Reset();
            WeaponSync.Reset();
            SpeedBoost.Reset();
            if (world != null) LoggerInstance.Msg("Neue Spielwelt erkannt.");
        }
        return world != null;
    }

    public override void OnGUI()
    {
        Notifier.Draw();
        if (_lastWorld == IntPtr.Zero) return;
        DrawGame();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DrawGame()
    {
        Shop.Draw();
        if (Shop.IsOpen) return;
        Vendor.DrawHint();
        Weapons.DrawHud();
        Tuning.Draw();
    }
}

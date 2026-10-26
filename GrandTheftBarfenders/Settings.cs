using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandTheftBarfenders;

/// <summary>Einstellungen in UserData/MelonPreferences.cfg, Abschnitt [GrandTheftBarfenders].</summary>
internal static class Settings
{
    public const int PistolPrice = 150;
    public const int BatPrice = 75;
    public const int PistolRounds = 25;

    public static MelonPreferences_Category Category { get; private set; }

    // Inventar: gehört zum Spieler auf diesem PC, nicht zum Spielstand.
    public static MelonPreferences_Entry<int> Ammo { get; private set; }
    public static MelonPreferences_Entry<bool> HasBat { get; private set; }

    private static MelonPreferences_Entry<string> _pistolKey;
    private static MelonPreferences_Entry<string> _batKey;

    public static Key PistolKey { get; private set; } = Key.Digit1;
    public static Key BatKey { get; private set; } = Key.Digit2;

    /// <summary>Feinjustierung der Waffen-Lage (F6-Modus), je Ziel: x, y, z (m) und Drehung x, y, z (Grad).</summary>
    private static readonly Dictionary<Tuning.Target, MelonPreferences_Entry<string>> _tuning = new();

    public static void Init()
    {
        Category = MelonPreferences.CreateCategory("GrandTheftBarfenders", "Grand Theft Barfenders");
        Ammo = Category.CreateEntry("Munition", 0, description: "Restliche Schuss für die Pistole (0 = keine Pistole).");
        HasBat = Category.CreateEntry("HatSchlaeger", false, description: "true = Schläger gekauft.");
        _pistolKey = Category.CreateEntry("TastePistole", "Digit1", description: "Taste zum Ziehen/Wegstecken der Pistole (Namen wie im Unity Input System, z. B. Digit1, G, F5).");
        _batKey = Category.CreateEntry("TasteSchlaeger", "Digit2", description: "Taste zum Ziehen/Wegstecken des Schlägers.");
        foreach (var target in Tuning.Targets)
            _tuning[target] = Category.CreateEntry("Lage" + target, "0,0,0,0,0,0",
                description: "Feinjustierung (F6): x,y,z in Metern, dann Drehung x,y,z in Grad.");

        PistolKey = ParseKey(_pistolKey.Value, Key.Digit1);
        BatKey = ParseKey(_batKey.Value, Key.Digit2);
    }

    private static Key ParseKey(string text, Key fallback)
    {
        if (Enum.TryParse<Key>(text, true, out var key) && key != Key.None) return key;
        MelonLogger.Warning($"Unbekannte Taste '{text}', nutze {fallback}.");
        return fallback;
    }

    public static (Vector3 Position, Vector3 Euler) GetTuning(Tuning.Target target)
    {
        var parts = _tuning[target].Value.Split(',');
        float At(int i) => i < parts.Length && float.TryParse(parts[i], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
        return (new Vector3(At(0), At(1), At(2)), new Vector3(At(3), At(4), At(5)));
    }

    public static void SetTuning(Tuning.Target target, Vector3 position, Vector3 euler)
    {
        string F(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        _tuning[target].Value = string.Join(",", F(position.x), F(position.y), F(position.z), F(euler.x), F(euler.y), F(euler.z));
    }

    public static void Save() => Category.SaveToFile(false);
}

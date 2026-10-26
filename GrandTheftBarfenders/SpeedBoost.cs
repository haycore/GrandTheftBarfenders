using HarmonyLib;
using Il2CppProtoDivision.Gameplay;
using MelonLoader;
using Unity.Mathematics;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>
/// Wer einen Mitarbeiter mit dem Schläger antreibt, macht ihm Beine: 20 s lang 50 % schneller. Der Schlag
/// macht dabei keinen Schaden und hält ihn nicht auf (siehe Weapons.Swing).
/// Der Boost gilt beim Host (dort läuft die Personal-KI); Mitspieler melden ihre Treffer über die Lobby
/// (WeaponSync).
/// </summary>
internal static class SpeedBoost
{
    private const float Seconds = 20f;
    private const float Factor = 1.5f;

    private static readonly Dictionary<uint, float> _until = new();

    public static void Reset() => _until.Clear();

    /// <summary>Host: Mitarbeiter antreiben (oder Boost erneuern).</summary>
    public static void Boost(uint characterRawId)
    {
        if (GameAccess.LogicWorld == null) return;
        var fresh = !IsBoosted(characterRawId);
        _until[characterRawId] = Time.realtimeSinceStartup + Seconds;
        if (fresh) Notifier.Show($"Mitarbeiter angetrieben: {Seconds:0} s lang +50 % Tempo!");
        MelonLogger.Msg($"[Boost] Mitarbeiter {characterRawId}: +50 % Tempo für {Seconds:0} s");
    }

    public static bool IsBoosted(uint characterRawId) =>
        _until.TryGetValue(characterRawId, out var until) && Time.realtimeSinceStartup < until;
}

/// <summary>
/// Jede Figur läuft Schritt für Schritt über diese Funktion zu ihrem Ziel und bekommt dabei ihr Tempo
/// mitgegeben – angetriebene Mitarbeiter laufen hier 1,5-mal so schnell.
/// </summary>
[HarmonyPatch(typeof(CharacterData), nameof(CharacterData.UpdateCharacterTransformTowards))]
internal static class MoveSpeedPatch
{
    [HarmonyPrefix]
    private static void Boost(CharacterData __instance, ref float moveSpeed)
    {
        var rawId = __instance.CharacterId.CharacterIdRawValue;
        if (!SpeedBoost.IsBoosted(rawId)) return;
        moveSpeed *= 1.5f;
        GameAccess.LogOnce("boost-applied", $"[Boost] Bewegung mit Boost ({moveSpeed:0.00} m/s).");
    }
}

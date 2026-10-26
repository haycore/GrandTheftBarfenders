using HarmonyLib;
using Il2CppProtoDivision.Gameplay;
using MelonLoader;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>
/// "Beende erst die Schlägerei": Das Spiel prüft vor dem Tagesende, ob irgendwer prügelt. Trevor wird
/// direkt davor beruhigt (er hat keine KI, die das sonst täte). Meldet das Spiel trotzdem eine Schlägerei,
/// steht im Log, welche Figuren als prügelnd gelten (höchstens alle 10 s).
/// </summary>
[HarmonyPatch(typeof(CharacterDatabase), nameof(CharacterDatabase.IsAnyCharacterBrawling))]
internal static class BrawlCheckPatch
{
    private static float _nextLog;

    [HarmonyPrefix]
    private static void CalmTrevor(CharacterDatabase __instance)
    {
        try
        {
            var bartenders = __instance.BartenderCharacters;
            if (bartenders == null) return;
            for (var i = 0; i < bartenders.Count; i++) Vendor.Calm(bartenders[i]);
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("brawl-calm", $"[Schlägerei] Trevor beruhigen fehlgeschlagen: {e.Message}");
        }
    }

    [HarmonyPostfix]
    private static void ReportBrawlers(CharacterDatabase __instance, bool ignoreWaiters, bool __result)
    {
        if (!__result || Time.realtimeSinceStartup < _nextLog) return;
        _nextLog = Time.realtimeSinceStartup + 10f;
        try
        {
            var all = __instance.AllCharacters;
            var names = new List<string>();
            for (var i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null || !(c.IsBrawling || c.IsInFightMode)) continue;
                names.Add($"{c.GetIl2CppType().Name} #{c.CharacterId.CharacterIdRawValue}" +
                          $" (Kampfmodus {c.IsInFightMode}, prügelt {c.IsBrawling}, ohnmächtig {c.IsFainted})");
            }
            MelonLogger.Msg($"[Schlägerei] Spiel meldet Schlägerei (ohne Spieler: {ignoreWaiters}): " +
                            (names.Count == 0 ? "keine Figur im Kampfmodus gefunden" : string.Join(", ", names)));
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("brawl-report", $"[Schlägerei] Diagnose fehlgeschlagen: {e.Message}");
        }
    }
}

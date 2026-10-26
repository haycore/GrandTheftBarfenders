using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppProtoDivision.Gameplay;

namespace GrandTheftBarfenders;

/// <summary>
/// Solange Pistole oder Schläger gezogen sind, gehören die Maustasten der Waffe: Das Spiel bekommt kein
/// Interagieren (anklicken, aufheben, benutzen), Fallenlassen, Werfen, Kampf- und Baumodus mehr.
/// Laufen, Springen, Ducken, Handy und Pause bleiben. Die Eingaben werden dort entfernt, wo das Spiel sie
/// einmal pro Bild für die Spielfigur einsammelt (InputManager.State).
/// </summary>
[HarmonyPatch(typeof(InputManager), nameof(InputManager.OnInputUpdate))]
internal static class InputBlockPatch
{
    [HarmonyPostfix]
    private static void BlockWhileArmed(InputManager __instance)
    {
        if (!Weapons.IsAnyDrawn) return;
        try
        {
            var state = __instance._State_k__BackingField;
            // Die Felder sind in der Il2Cpp-Brücke schreibgeschützt; an der lokalen Kopie trotzdem ändern.
            Clear(in state.PrimaryInteraction);
            Clear(in state.SecondaryInteraction);
            Clear(in state.PrimaryInteractionDown);
            Clear(in state.SecondaryInteractionDown);
            Clear(in state.PrimaryInteractionHold);
            Clear(in state.SecondaryInteractionHold);
            Clear(in state.ReboxInteraction);
            Clear(in state.DropItem);
            Clear(in state.ThrowHold);
            Clear(in state.ToggleFightMode);
            Clear(in state.ToggleConstructionMode);
            Clear(in state.CycleCarriedItemNext);
            Clear(in state.CycleCarriedItemPrevious);
            Clear(in state.RotatePlacementClockwise);
            Clear(in state.RotatePlacementCounterClockwise);
            __instance._State_k__BackingField = state;
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("input-block", $"[Eingabe] Sperre fehlgeschlagen: {e.Message}");
        }
    }

    private static void Clear(in bool field) => Unsafe.AsRef(in field) = false;
}

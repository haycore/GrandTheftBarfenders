using HarmonyLib;
using Iced.Intel;
using Il2CppInteropUnity6000Fix;
using MelonLoader;
using NativeLibrary = System.Runtime.InteropServices.NativeLibrary;

[assembly: MelonInfo(typeof(InteropFixPlugin), "Il2CppInterop Unity 6000.3 Fix", "1.0.0", "Tobi")]
[assembly: MelonGame]

namespace Il2CppInteropUnity6000Fix;

/// <summary>
/// Il2CppInterop 1.5.1 sucht Class::GetDefaultFieldValue über eine Sprungkette ab dem Export
/// il2cpp_field_static_get_value. Auf Unity 6000.3 landet die Kette im Konstanten-Decoder
/// (BlobReader) und liefert dessen interne Sprungmarke – eine Adresse mitten in einer Instruktion.
/// Der Hook dort zerstört Maschinencode und das Spiel stürzt Sekunden später ab
/// (LavaGang/MelonLoader#1159, BepInEx/Il2CppInterop#283).
///
/// Field::StaticGetValue ruft bei Konstanten zuerst Class::GetDefaultFieldValue auf und erst
/// danach den Decoder. Liefert Il2CppInterop eine nicht ausgerichtete Adresse, nimmt dieses
/// Plugin stattdessen das erste call-Ziel in Field::StaticGetValue.
/// </summary>
public class InteropFixPlugin : MelonPlugin
{
    private static MelonLogger.Instance _log;

    // Bewusst erst hier (nach der Assembly-Generierung, vor dem Support-Modul): Würde das Plugin früher in
    // Il2CppInterop eingreifen, lädt das die alten Il2Cpp-Assemblies, sperrt sie, und nach einem Spiel-Update
    // kann MelonLoader sie nicht neu erzeugen ("Access denied" auf Il2Cppmscorlib.dll, getestet 2026-10-23).
    public override void OnPreSupportModule()
    {
        _log = LoggerInstance;
        var hookType = Type.GetType("Il2CppInterop.Runtime.Injection.Hooks.Class_GetFieldDefaultValue_Hook, Il2CppInterop.Runtime");
        var target = hookType == null ? null : AccessTools.Method(hookType, "FindTargetMethod");
        if (target == null)
        {
            _log.Warning("Class_GetFieldDefaultValue_Hook.FindTargetMethod nicht gefunden - Il2CppInterop-Version passt nicht, Fix inaktiv.");
            return;
        }

        HarmonyInstance.Patch(target, postfix: new HarmonyMethod(typeof(InteropFixPlugin), nameof(FixTargetAddress)));
        _log.Msg("Bereit: korrigiert Class::GetDefaultFieldValue, falls Il2CppInterop eine falsche Adresse findet.");
    }

    private static void FixTargetAddress(ref IntPtr __result)
    {
        if (__result != IntPtr.Zero && __result.ToInt64() % 16 == 0)
        {
            _log.Msg($"Class::GetDefaultFieldValue 0x{__result.ToInt64():X} sieht korrekt aus, nichts zu tun.");
            return;
        }

        try
        {
            var fixedAddress = FindClassGetDefaultFieldValue();
            _log.Msg($"Class::GetDefaultFieldValue korrigiert: 0x{__result.ToInt64():X} -> 0x{fixedAddress:X}");
            __result = (IntPtr)fixedAddress;
        }
        catch (Exception e)
        {
            _log.Error($"Korrektur fehlgeschlagen, Original bleibt: {e.Message}");
        }
    }

    private static long FindClassGetDefaultFieldValue()
    {
        var gameAssembly = NativeLibrary.Load("GameAssembly");
        var export = NativeLibrary.GetExport(gameAssembly, "il2cpp_field_static_get_value");

        // Wie Il2CppInterop: Der Export springt direkt in Field::StaticGetValue.
        var staticGetValue = JumpTargets(export).Single();

        // Erstes call-Ziel in Field::StaticGetValue = Class::GetDefaultFieldValue (Zweig für Konstanten).
        var candidate = CallTargets(staticGetValue).First();
        if (candidate % 16 != 0)
            throw new InvalidOperationException($"Kandidat 0x{candidate:X} ist nicht ausgerichtet");
        return candidate;
    }

    /// <summary>call- und jmp-Ziele bis zum ersten ret/int3, wie XrefScannerLowLevel.JumpTargets.</summary>
    private static IEnumerable<long> JumpTargets(IntPtr start) => BranchTargets(start, includeJumps: true);

    private static IEnumerable<long> CallTargets(long start) => BranchTargets((IntPtr)start, includeJumps: false);

    private static IEnumerable<long> BranchTargets(IntPtr start, bool includeJumps)
    {
        var decoder = Iced.Intel.Decoder.Create(64, new UnmanagedCodeReader(start));
        decoder.IP = (ulong)start.ToInt64();
        for (var i = 0; i < 2000; i++)
        {
            decoder.Decode(out var instr);
            if (instr.Mnemonic is Mnemonic.Ret or Mnemonic.Int3) yield break;
            if (instr.Op0Kind != OpKind.NearBranch64) continue;

            if (instr.FlowControl == FlowControl.Call)
                yield return (long)instr.NearBranchTarget;
            else if (includeJumps && instr.FlowControl == FlowControl.UnconditionalBranch)
            {
                yield return (long)instr.NearBranchTarget;
                yield break;
            }
        }
    }

    private sealed unsafe class UnmanagedCodeReader : CodeReader
    {
        private byte* _pointer;
        public UnmanagedCodeReader(IntPtr start) => _pointer = (byte*)start;
        public override int ReadByte() => *_pointer++;
    }
}

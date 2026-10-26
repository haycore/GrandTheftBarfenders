using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandTheftBarfenders;

/// <summary>
/// Justier-Modus für die Lage der Waffen in der Hand. F6 schaltet durch die Ziele; Pfeiltasten und
/// Bild auf/ab verschieben, mit gedrückter Umschalttaste drehen, Rücktaste setzt zurück.
/// Die Werte landen in MelonPreferences.cfg (LageSchlaeger1P usw.) und im Log.
///
/// Fremde Hände (3P) sieht man am Händler: Er hält die Pistole, im Modus "SchlaegerHand3P" den Schläger.
/// </summary>
internal static class Tuning
{
    public enum Target { Schlaeger1P, Pistole3P, Schlaeger3P }

    public static readonly Target[] Targets = { Target.Schlaeger1P, Target.Pistole3P, Target.Schlaeger3P };

    private const float MoveStep = 0.01f;
    private const float TurnStep = 5f;

    private static int _mode = -1; // -1 = aus
    public static Target? Active => _mode < 0 ? null : Targets[_mode];

    private sealed class Holder
    {
        public Target Target;
        public Transform Transform;
        public Vector3 BasePosition;
        public Quaternion BaseRotation;
    }

    private static readonly List<Holder> _holders = new();

    /// <summary>Ein Modell meldet seinen Halter an; dessen Lage = Grundlage + Feinjustierung.</summary>
    public static void Register(Target target, Transform holder)
    {
        _holders.Add(new Holder { Target = target, Transform = holder, BasePosition = holder.localPosition, BaseRotation = holder.localRotation });
        Apply(_holders[^1]);
    }

    private static void Apply(Holder holder)
    {
        var (position, euler) = Settings.GetTuning(holder.Target);
        holder.Transform.localPosition = holder.BasePosition + position;
        holder.Transform.localRotation = holder.BaseRotation * Quaternion.Euler(euler);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Update()
    {
        _holders.RemoveAll(h => h.Transform == null);

        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.f6Key.wasPressedThisFrame)
        {
            _mode = _mode + 1 >= Targets.Length ? -1 : _mode + 1;
            MelonLogger.Msg(Active == null ? "[Justieren] aus" : $"[Justieren] {Active}");
        }
        if (Active is not { } target) return;

        var (position, euler) = Settings.GetTuning(target);
        var turn = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        var delta = Vector3.zero;
        if (keyboard.rightArrowKey.wasPressedThisFrame) delta.x += 1;
        if (keyboard.leftArrowKey.wasPressedThisFrame) delta.x -= 1;
        if (keyboard.pageUpKey.wasPressedThisFrame) delta.y += 1;
        if (keyboard.pageDownKey.wasPressedThisFrame) delta.y -= 1;
        if (keyboard.upArrowKey.wasPressedThisFrame) delta.z += 1;
        if (keyboard.downArrowKey.wasPressedThisFrame) delta.z -= 1;
        var reset = keyboard.backspaceKey.wasPressedThisFrame;

        if (delta == Vector3.zero && !reset) return;
        if (reset) { position = Vector3.zero; euler = Vector3.zero; }
        else if (turn) euler += delta * TurnStep;
        else position += delta * MoveStep;

        Settings.SetTuning(target, position, euler);
        Settings.Save();
        foreach (var holder in _holders) if (holder.Target == target) Apply(holder);
        MelonLogger.Msg($"[Justieren] {target}: Position {position.x:0.00} {position.y:0.00} {position.z:0.00}, Drehung {euler.x:0} {euler.y:0} {euler.z:0}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Draw()
    {
        if (Active is not { } target) return;
        var (position, euler) = Settings.GetTuning(target);
        var text = $"Justieren: {target} (F6 weiter)\n" +
                   $"Pfeile/Bild↑↓ = schieben, +Umschalt = drehen, Rücktaste = zurück\n" +
                   $"Pos {position.x:0.00} {position.y:0.00} {position.z:0.00} · Dreh {euler.x:0} {euler.y:0} {euler.z:0}";
        GUI.Box(new Rect(20f, Screen.height - 110f, 470f, 70f), text);
    }
}

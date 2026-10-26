using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>Kurze Hinweise oben im Bild (z. B. "Zu wenig Geld"). Steht zusätzlich im Log.</summary>
internal static class Notifier
{
    private const float ShowSeconds = 4f;
    private const int MaxVisible = 3;

    private static readonly List<(string Text, float Until)> _visible = new();
    private static GUIStyle _style;

    public static void Show(string text)
    {
        var now = Time.realtimeSinceStartup;
        _visible.RemoveAll(v => v.Text == text);
        _visible.Add((text, now + ShowSeconds));
        if (_visible.Count > MaxVisible) _visible.RemoveAt(0);
        MelonLogger.Msg($"[Hinweis] {text}");
    }

    /// <summary>Aus OnGUI aufrufen.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Draw()
    {
        var now = Time.realtimeSinceStartup;
        _visible.RemoveAll(v => v.Until < now);
        if (_visible.Count == 0) return;

        _style ??= new GUIStyle(GUI.skin.box) { fontSize = 16, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        const float width = 520f, height = 34f;
        var x = (Screen.width - width) / 2f;
        var y = 60f;
        foreach (var (text, _) in _visible)
        {
            GUI.Box(new Rect(x, y, width, height), text, _style);
            y += height + 6f;
        }
    }
}

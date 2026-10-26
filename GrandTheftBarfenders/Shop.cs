using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandTheftBarfenders;

/// <summary>Kaufmenü des Händlers: Pistole (25 Schuss) für 150, Schläger für 75.</summary>
internal static class Shop
{
    private const string InputReason = "GrandTheftBarfendersShop";

    public static bool IsOpen { get; private set; }

    private static int _openedFrame = -1;
    private static CursorLockMode _previousLockMode;
    private static bool _previousCursorVisible;
    private static Rect _pistolButton, _batButton, _closeButton;
    private static GUIStyle _title;

    public static void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        _openedFrame = Time.frameCount;
        _previousLockMode = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        Weapons.Holster();
        SetGameMouseBlocked(true);
    }

    public static void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        Cursor.lockState = _previousLockMode;
        Cursor.visible = _previousCursorVisible;
        SetGameMouseBlocked(false);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Update()
    {
        if (!IsOpen)
        {
            if (Vendor.WasTalkPressed()) Open();
            return;
        }

        // Das Spiel fängt den Mauszeiger jedes Frame wieder ein; solange das Menü offen ist, freigeben.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || (keyboard.eKey.wasPressedThisFrame && Time.frameCount != _openedFrame)))
        {
            Close();
            return;
        }

        // Barfender nutzt nur das neue Input System; IMGUI-Knöpfe bekommen keine Klicks, deshalb selbst zuordnen.
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || Time.frameCount == _openedFrame) return;
        var screen = mouse.position.ReadValue();
        var click = new Vector2(screen.x, Screen.height - screen.y);

        if (_closeButton.Contains(click)) Close();
        else if (_pistolButton.Contains(click)) BuyPistol();
        else if (_batButton.Contains(click)) BuyBat();
    }

    private static void BuyPistol()
    {
        if (!GameAccess.TrySpend(Settings.PistolPrice, out var problem))
        {
            Notifier.Show(problem);
            return;
        }
        Settings.Ammo.Value += Settings.PistolRounds;
        Settings.Save();
        Notifier.Show($"Halloween-Pistole gekauft ({Settings.Ammo.Value} Schuss). Ziehen mit {Settings.PistolKey}.");
        MelonLogger.Msg($"[Kauf] Pistole für {Settings.PistolPrice}");
    }

    private static void BuyBat()
    {
        if (Settings.HasBat.Value)
        {
            Notifier.Show("Du hast schon einen Schläger.");
            return;
        }
        if (!GameAccess.TrySpend(Settings.BatPrice, out var problem))
        {
            Notifier.Show(problem);
            return;
        }
        Settings.HasBat.Value = true;
        Settings.Save();
        Notifier.Show($"Schläger gekauft. Ziehen mit {Settings.BatKey}.");
        MelonLogger.Msg($"[Kauf] Schläger für {Settings.BatPrice}");
    }

    /// <summary>
    /// Meldet das Menü beim Eingabe-Manager des Spiels an (wie Bank, Handy, Jukebox):
    /// Mauszeiger frei, Kamera und Mausaktionen bleiben stehen.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SetGameMouseBlocked(bool blocked)
    {
        try
        {
            var input = Il2CppProtoDivision.Subsystems.Core.SubsystemBase<Il2CppProtoDivision.Gameplay.InputManager>.Instance;
            input?.SetCursorUnlockReason(InputReason, blocked, blocked);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"Kamera-Sperre nicht möglich: {e.Message}");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Draw()
    {
        if (!IsOpen) return;
        const float width = 460f, height = 230f, padding = 16f, rowHeight = 56f;
        var x = (Screen.width - width) / 2f;
        var y = (Screen.height - height) / 2f;

        _title ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        GUI.Box(new Rect(x, y, width, height), "");
        GUI.Label(new Rect(x + padding, y + 8f, width - 60f, 30f), $"Grand Theft Barfenders – {Vendor.Name}", _title);
        _closeButton = new Rect(x + width - 34f, y + 8f, 26f, 24f);
        GUI.Button(_closeButton, "X");

        GUI.Label(new Rect(x + padding, y + 42f, width - 2 * padding, 24f), $"Barkasse: {GameAccess.Money:0}");

        var rowY = y + 74f;
        GUI.Label(new Rect(x + padding, rowY, 270f, rowHeight), $"Halloween-Pistole\n{Settings.PistolRounds} Schuss, Treffer = Faustschlag");
        _pistolButton = new Rect(x + width - padding - 140f, rowY + 8f, 140f, 34f);
        GUI.Button(_pistolButton, $"Kaufen – {Settings.PistolPrice}");

        rowY += rowHeight;
        GUI.Label(new Rect(x + padding, rowY, 270f, rowHeight), Settings.HasBat.Value
            ? "Schläger (Mopp-Look)\nschon gekauft"
            : "Schläger (Mopp-Look)\nSchlag = Faustschlag");
        _batButton = new Rect(x + width - padding - 140f, rowY + 8f, 140f, 34f);
        GUI.Button(_batButton, Settings.HasBat.Value ? "Gekauft" : $"Kaufen – {Settings.BatPrice}");

        GUI.Label(new Rect(x + padding, y + height - 30f, width - 2 * padding, 24f),
            $"Ziehen: {Settings.PistolKey} / {Settings.BatKey} · Schießen/Schlagen: linke Maustaste · Esc schließt");
    }
}

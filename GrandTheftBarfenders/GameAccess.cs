using Il2CppProtoDivision.AmberGameLogic;
using Il2CppProtoDivision.AmberGameLogic.DataTypes;
using Il2CppProtoDivision.Barfender.Core;
using Il2CppProtoDivision.Configuration.Core;
using Il2CppProtoDivision.Game;
using Il2CppProtoDivision.Gameplay;
using MelonLoader;
using Unity.Mathematics;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>Zugriff auf Spielwelt, lokalen Spieler, Spielaktionen und Geld.</summary>
internal static class GameAccess
{
    /// <summary>Die Welt, die das Spiel anzeigt (bei jedem Spieler vorhanden).</summary>
    public static GameWorld ViewWorld
    {
        get
        {
            try { return GameWorldBase<GameWorld>.__IsInstanceUsedInViewLoaded ? GameWorldBase<GameWorld>.__InstanceUsedInView : null; }
            catch { return null; }
        }
    }

    /// <summary>
    /// Die Welt, in der die Spiellogik des Hosts läuft; null bei Mitspielern. Änderungen nur an der
    /// Anzeige-Kopie überschreibt das Spiel beim nächsten Abgleich.
    /// </summary>
    public static GameWorld LogicWorld
    {
        get
        {
            try
            {
                if (!GameWorldBase<GameWorld>.__IsInstanceUsedInLogicMonoBehavioursLoaded) return null;
                var world = GameWorldBase<GameWorld>.__InstanceUsedInLogicMonoBehaviours;
                return world != null && world.IsHostCopy ? world : null;
            }
            catch { return null; }
        }
    }

    public static bool IsHost => LogicWorld != null || (ViewWorld?.HasLocalHostCopy ?? false);

    public static CharacterData LocalPlayer
    {
        get
        {
            try { return NetworkSubsystem.Instance?.ControlledCharacter; }
            catch { return null; }
        }
    }

    public static float PunchDamage => GameConfiguration.Instance?.DamageTuning?.PunchDamage ?? 10f;

    /// <summary>Vergleicht zwei Charakter-Ids über die rohe Zahl (Equals über die Il2Cpp-Brücke ist unzuverlässig).</summary>
    public static bool Same(CharacterId a, CharacterId b) => a.CharacterIdRawValue == b.CharacterIdRawValue;

    public static CharacterData FindCharacter(GameWorld world, CharacterId id)
    {
        var all = world?.Character?.AllCharacters;
        if (all == null) return null;
        for (var i = 0; i < all.Count; i++)
        {
            var character = all[i];
            if (character != null && Same(character.CharacterId, id)) return character;
        }
        return null;
    }

    /// <summary>
    /// Stellt eine Spielaktion im Namen des lokalen Spielers an – derselbe Weg wie bei Spieler-Eingaben,
    /// funktioniert also auch für Mitspieler (die Aktion läuft dann beim Host).
    /// </summary>
    public static bool Request(Operation operation, out string reason)
    {
        reason = null;
        try
        {
            var gamer = NetworkSubsystem.Instance?.LocalGamer;
            var scheduler = OperationSchedulerViewSubsystem.Instance;
            if (gamer == null || scheduler == null)
            {
                reason = "kein Spieler/Planer";
                return false;
            }
            var result = scheduler.RequestOperation(gamer, operation, 0f);
            if (result.IsSuccess) return true;
            reason = result.Opcode.ToString();
            return false;
        }
        catch (Exception e)
        {
            reason = e.Message;
            return false;
        }
    }

    private static readonly HashSet<string> _loggedOnce = new();

    public static void LogOnce(string key, string text)
    {
        if (_loggedOnce.Add(key)) MelonLogger.Msg(text);
    }

    /// <summary>
    /// Ein Treffer genau wie ein Faustschlag des Spielers: dieselbe Spielaktion mit dem Schlagschaden aus
    /// der Spielkonfiguration. Lehnt das Spiel die Aktion ab (z. B. weil das Ziel für einen Faustschlag zu
    /// weit weg ist), wendet der Host den Schaden direkt in seiner Spiellogik an.
    /// </summary>
    public static void ApplyHit(CharacterData target, Vector3 direction)
    {
        var player = LocalPlayer;
        if (player == null || target == null) return;

        var flat = new Vector3(direction.x, 0f, direction.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        var damage = PunchDamage;

        if (Request(new ApplyPunchDamageOperation(player.CharacterId, target.CharacterId, damage, new float3(flat.x, flat.y, flat.z)), out var reason))
            return;

        var logic = LogicWorld;
        if (logic == null)
        {
            LogOnce("hit-client-" + reason, $"[Treffer] Spiel lehnt den Treffer ab ({reason}); als Mitspieler kein direkter Weg.");
            return;
        }

        var logicTarget = FindCharacter(logic, target.CharacterId);
        if (logicTarget == null) return;
        LogOnce("hit-direct-" + reason, $"[Treffer] Spiel lehnt die Schlag-Aktion ab ({reason}), Host wendet den Schaden direkt an.");
        logic.Character.ApplyPunchDamage(logicTarget, player.CharacterId, damage, flat);
    }

    public static float Money => ViewWorld?.Money?.TotalMoney ?? 0f;

    /// <summary>Zieht den Preis vom Bar-Konto ab. false, wenn das Geld nicht reicht oder es nicht geht.</summary>
    public static bool TrySpend(float amount, out string problem)
    {
        problem = null;
        var view = ViewWorld;
        if (view == null)
        {
            problem = "Kein Spiel geladen.";
            return false;
        }
        if (!view.Money.CanAfford(amount))
        {
            problem = $"Zu wenig Geld (du hast {view.Money.TotalMoney:0}).";
            return false;
        }

        var logic = LogicWorld;
        if (logic != null)
        {
            // Nur den Kassenstand ändern: RemoveMoney des Spiels zieht auch Erfahrungspunkte ab.
            logic.Money._TotalMoney = Math.Max(0f, logic.Money._TotalMoney - amount);
            return true;
        }

        // Mitspieler: über die Spielaktion "Geld ändern" mit negativem Betrag beim Host.
        var player = LocalPlayer;
        var reason = "kein Spieler";
        if (player != null && Request(new CHEAT_AddMoneyOperation(-amount, player.CharacterId), out reason)) return true;
        problem = "Kaufen geht im Koop nur beim Host.";
        MelonLogger.Msg($"[Kauf] Als Mitspieler abgelehnt ({reason}).");
        return false;
    }
}

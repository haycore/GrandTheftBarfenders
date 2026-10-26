using Il2CppProtoDivision.Gameplay;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>
/// Findet getroffene Personen (Gäste, Personal, Bettler, Mitspieler …). Gerechnet wird mit einer Kapsel
/// um jede Spielfigur statt mit ihren Collidern: Die Figuren-Collider sind je nach Typ verschieden
/// (oder nur Trigger), die Kapsel trifft alle gleich.
/// </summary>
internal static class Targeting
{
    private const float BodyRadius = 0.38f;
    private const float FeetOffset = 0.15f;
    private const float HeadOffset = 1.8f;

    private static Vector3 ToVector(Unity.Mathematics.float3 v) => new(v.x, v.y, v.z);

    private static IEnumerable<CharacterData> Candidates()
    {
        var world = GameAccess.ViewWorld;
        var player = GameAccess.LocalPlayer;
        var all = world?.Character?.AllCharacters;
        if (all == null || player == null) yield break;
        for (var i = 0; i < all.Count; i++)
        {
            var character = all[i];
            if (character == null || GameAccess.Same(character.CharacterId, player.CharacterId)) continue;
            yield return character;
        }
    }

    /// <summary>Schuss: erste Person entlang des Strahls, sofern keine Wand dazwischen ist.</summary>
    public static CharacterData FindShotTarget(Ray ray, float maxDistance, out float distance)
    {
        CharacterData best = null;
        distance = maxDistance;
        foreach (var character in Candidates())
        {
            var feet = ToVector(character.Position);
            var bottom = feet + Vector3.up * FeetOffset;
            var top = feet + Vector3.up * (character.IsRagdoll ? 0.5f : HeadOffset);
            var radius = character.IsRagdoll ? 0.6f : BodyRadius;
            if (!RayHitsCapsule(ray, bottom, top, radius, out var t) || t > distance) continue;
            best = character;
            distance = t;
        }
        if (best == null || IsBlocked(ray, distance)) return null;
        return best;
    }

    /// <summary>Schläger: nächste Person vor dem Spieler in Reichweite.</summary>
    public static CharacterData FindMeleeTarget(Vector3 origin, Vector3 forward, float reach, float maxAngle)
    {
        var flatForward = new Vector3(forward.x, 0f, forward.z).normalized;
        CharacterData best = null;
        var bestDistance = float.MaxValue;
        foreach (var character in Candidates())
        {
            var toTarget = ToVector(character.Position) - origin;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            if (distance > reach || distance >= bestDistance) continue;
            if (distance > 0.3f && Vector3.Angle(flatForward, toTarget) > maxAngle) continue;
            best = character;
            bestDistance = distance;
        }
        return best;
    }

    /// <summary>Wand, Theke o. Ä. vor dem Ziel? Treffer auf Spielfiguren zählen nicht als Hindernis.</summary>
    private static bool IsBlocked(Ray ray, float targetDistance)
    {
        var hits = Physics.RaycastAll(ray, targetDistance, ~0, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.distance > targetDistance - 0.5f) continue;
            if (hit.collider.GetComponentInParent<CharacterView>() != null) continue;
            if (hit.collider.GetComponentInParent<RagdollBoneImpactRelay>() != null) continue;
            if (Weapons.IsOwnObject(hit.collider.transform)) continue;
            return true;
        }
        return false;
    }

    /// <summary>Abstand Strahl – Strecke (Körperachse) kleiner als der Radius? t = Entfernung entlang des Strahls.</summary>
    private static bool RayHitsCapsule(Ray ray, Vector3 a, Vector3 b, float radius, out float t)
    {
        t = 0f;
        var d1 = ray.direction;
        var d2 = b - a;
        var r = ray.origin - a;
        float aa = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);
        float c = Vector3.Dot(d1, r), bb = Vector3.Dot(d1, d2);
        var denom = aa * e - bb * bb;

        float s, u;
        if (denom > 1e-6f)
        {
            s = (bb * f - c * e) / denom;
            if (s < 0f) s = 0f;
        }
        else s = 0f;
        u = e > 1e-6f ? (bb * s + f) / e : 0f;
        if (u < 0f) { u = 0f; s = Math.Max(0f, -c / aa); }
        else if (u > 1f) { u = 1f; s = Math.Max(0f, (bb - c) / aa); }

        var closestRay = ray.origin + d1 * s;
        var closestAxis = a + d2 * u;
        if ((closestRay - closestAxis).sqrMagnitude > radius * radius) return false;
        t = s;
        return true;
    }
}

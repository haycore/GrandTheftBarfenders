using Il2CppInterop.Runtime;
using Il2CppProtoDivision.Game;
using Il2CppProtoDivision.Gameplay;
using MelonLoader;
using UnityEngine;

namespace GrandTheftBarfenders;

/// <summary>
/// Baut die Waffen-Modelle: die Halloween-Pistole aus der Schießbude (das Spiel bringt dafür ein fertiges
/// Modell mit Schuss-Animation mit) und einen Schläger, der aussieht wie der Mopp. Je einmal für die
/// eigene Kamera (1P) und für die Hand einer Spielfigur (3P: Mitspieler, Händler).
/// </summary>
internal static class WeaponModels
{
    // Schläger vor der Kamera: Griff unten rechts, Kopf nach vorn oben (Meter / Grad).
    private static readonly Vector3 BatPosition = new(0.3f, -0.42f, 0.45f);
    private static readonly Vector3 BatEuler = new(40f, 0f, 15f);
    private const float BatLength = 1.0f;
    private const float GripInset = 0.08f; // Griff so weit vom Stielende entfernt (Anteil der Länge)

    public sealed class Pistol
    {
        public GameObject Root;
        public ShootingGalleryGunView Gun; // null beim Ersatzmodell
        public AudioSource Audio;
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<AudioClip> FireClips;
        public AudioClip ReloadClip;
    }

    public static ShootingGalleryView FindGallery()
    {
        try
        {
            var active = ShootingGalleryView.ActiveViews;
            if (active != null)
                for (var i = 0; i < active.Count; i++)
                    if (active[i] != null && active[i].HeldGunPrefab != null) return active[i];

            foreach (var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<ShootingGalleryView>(), true))
            {
                var view = obj.TryCast<ShootingGalleryView>();
                if (view != null && view.HeldGunPrefab != null) return view;
            }
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("gallery-error", $"[Pistole] Schießbude nicht lesbar: {e.Message}");
        }
        return null;
    }

    // ---------- Pistole ----------

    public static Pistol CreatePistolFirstPerson(Transform camera)
    {
        var gallery = FindGallery();
        var pistol = InstantiateGalleryGun(gallery, camera, true);
        if (pistol.Root != null)
        {
            pistol.Root.transform.localPosition = gallery.FirstPersonGripPosition;
            pistol.Root.transform.localRotation = Quaternion.Euler(gallery.FirstPersonEuler);
        }
        else
        {
            GameAccess.LogOnce("pistol-fallback", "[Pistole] Keine Schießbude geladen – nutze ein einfaches Ersatzmodell.");
            pistol.Root = BuildFallbackPistol(camera);
            pistol.Root.transform.localPosition = new Vector3(0.22f, -0.2f, 0.45f);
        }
        AddAudio(pistol, false);
        return pistol;
    }

    /// <summary>Pistole in der rechten Hand einer Spielfigur; der Griff liegt im Handgelenk.</summary>
    public static Pistol CreatePistolThirdPerson(Transform hand)
    {
        var holder = CreateHolder(hand, "GTB_Pistole3P");
        var gallery = FindGallery();
        var pistol = InstantiateGalleryGun(gallery, holder, false);
        if (pistol.Root != null)
        {
            var gun = pistol.Root.transform;
            gun.localRotation = Quaternion.Euler(gallery.ThirdPersonHandEulerOffset);
            gun.localPosition = Vector3.zero;
            if (pistol.Gun?.GripPoint != null) gun.localPosition = -(holder.InverseTransformPoint(pistol.Gun.GripPoint.position));
        }
        else pistol.Root = BuildFallbackPistol(holder);

        pistol.Root = holder.gameObject; // zum Aufräumen: alles hängt am Halter
        Tuning.Register(Tuning.Target.Pistole3P, holder);
        AddAudio(pistol, true);
        return pistol;
    }

    private static Pistol InstantiateGalleryGun(ShootingGalleryView gallery, Transform parent, bool firstPerson)
    {
        var pistol = new Pistol();
        if (gallery == null) return pistol;
        try
        {
            var copy = UnityEngine.Object.Instantiate(gallery.HeldGunPrefab.gameObject, parent, false);
            copy.name = "GTB_Pistole";
            RemovePhysics(copy);
            pistol.Root = copy;
            pistol.Gun = copy.GetComponent<ShootingGalleryGunView>();
            pistol.Gun?.SetFirstPerson(firstPerson);
            pistol.Gun?.ResetForDraw();
            pistol.FireClips = gallery.FireClips;
            pistol.ReloadClip = gallery.ReloadClip;
            GameAccess.LogOnce("pistol-model" + firstPerson, $"[Pistole] Modell aus der Schießbude '{gallery.GalleryId}' ({(firstPerson ? "eigene Hand" : "fremde Hand")}).");
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[Pistole] Modell aus der Schießbude nicht nutzbar: {e.Message}");
            if (pistol.Root != null) UnityEngine.Object.Destroy(pistol.Root);
            pistol = new Pistol();
        }
        return pistol;
    }

    private static void AddAudio(Pistol pistol, bool spatial)
    {
        pistol.Audio = pistol.Root.AddComponent<AudioSource>();
        pistol.Audio.spatialBlend = spatial ? 1f : 0f;
        pistol.Audio.minDistance = 2f;
        pistol.Audio.maxDistance = 40f;
        pistol.Audio.playOnAwake = false;
    }

    // ---------- Schläger ----------

    public static GameObject CreateBatFirstPerson(Transform camera)
    {
        var holder = new GameObject("GTB_Schlaeger").transform;
        holder.SetParent(camera, false);
        holder.localPosition = BatPosition;
        holder.localRotation = Quaternion.Euler(BatEuler);
        BuildBat(holder);
        Tuning.Register(Tuning.Target.Schlaeger1P, holder);
        return holder.gameObject;
    }

    public static GameObject CreateBatThirdPerson(Transform hand)
    {
        var holder = CreateHolder(hand, "GTB_Schlaeger3P");
        BuildBat(holder);
        Tuning.Register(Tuning.Target.Schlaeger3P, holder);
        return holder.gameObject;
    }

    /// <summary>
    /// Mopp-Aussehen als Schläger: Stiel-Ende (Griff) im Ursprung des Halters, Mopp-Kopf entlang +Y,
    /// auf eine handliche Länge skaliert.
    /// </summary>
    private static void BuildBat(Transform holder)
    {
        var body = new GameObject("Mopp").transform;
        body.SetParent(holder, false);

        var mop = FindMop();
        if (mop == null || CopyMeshes(mop, body) == 0)
        {
            GameAccess.LogOnce("bat-fallback", "[Schläger] Kein Mopp gefunden – nutze ein einfaches Ersatzmodell.");
            AddBox(body, new Vector3(0f, 0.45f, 0f), new Vector3(0.035f, 0.9f, 0.035f), new Color(0.55f, 0.4f, 0.25f));
            AddBox(body, new Vector3(0f, 0.92f, 0f), new Vector3(0.18f, 0.12f, 0.18f), new Color(0.85f, 0.85f, 0.8f));
            return;
        }
        AlignAsBat(body, mop.name);
    }

    /// <summary>
    /// Richtet die kopierten Mopp-Teile aus: längste Achse = Stiel. Der Kopf ist dort, wo das meiste Volumen
    /// sitzt (mehrere Teile) bzw. am Ursprung des Mopps (ein Teil – der Mopp steht auf seinem Kopf).
    /// </summary>
    private static void AlignAsBat(Transform body, string sourceName)
    {
        var parts = new List<(Vector3 Min, Vector3 Max)>();
        for (var i = 0; i < body.childCount; i++)
        {
            var part = body.GetChild(i);
            var mesh = part.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null) parts.Add(LocalBounds(part, mesh.bounds));
        }
        var min = parts[0].Min;
        var max = parts[0].Max;
        foreach (var (pMin, pMax) in parts) { min = Vector3.Min(min, pMin); max = Vector3.Max(max, pMax); }

        var size = max - min;
        var axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        var length = size[axis];
        var center = (min + max) / 2f;

        // Seite des Kopfs entlang der Achse: +1 oder -1.
        float headSide;
        if (parts.Count > 1)
        {
            float weighted = 0f, total = 0f;
            foreach (var (pMin, pMax) in parts)
            {
                var s = pMax - pMin;
                var volume = Math.Max(1e-6f, s.x * s.y * s.z);
                weighted += ((pMin[axis] + pMax[axis]) / 2f - center[axis]) * volume;
                total += volume;
            }
            headSide = weighted / total >= 0f ? 1f : -1f;
        }
        else headSide = Math.Abs(0f - min[axis]) <= Math.Abs(max[axis] - 0f) ? -1f : 1f;

        var headDirection = Vector3.zero;
        headDirection[axis] = headSide;
        var grip = center;
        grip[axis] = center[axis] - headSide * length * (0.5f - GripInset);

        var scale = length > 0.01f ? BatLength / length : 1f;
        var rotation = Quaternion.FromToRotation(headDirection, Vector3.up);
        body.localRotation = rotation;
        body.localScale = Vector3.one * scale;
        body.localPosition = -(rotation * (grip * scale));

        GameAccess.LogOnce("bat-model", $"[Schläger] Modell von '{sourceName}': {parts.Count} Teil(e), Länge {length:0.00} m " +
                                        $"(Achse {"XYZ"[axis]}, Kopf bei {(headSide > 0 ? "+" : "-")}), skaliert auf {BatLength} m.");
    }

    /// <summary>Begrenzungsbox eines Teils im Raum seines Elternobjekts.</summary>
    private static (Vector3 Min, Vector3 Max) LocalBounds(Transform part, Bounds bounds)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = -min;
        for (var i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = part.localPosition + part.localRotation * Vector3.Scale(part.localScale, corner);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return (min, max);
    }

    /// <summary>Ein Mopp in der Bar (Spielgegenstand "Mop"), kein Eimer.</summary>
    private static Transform FindMop()
    {
        try
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<ItemView>(), true))
            {
                var view = obj.TryCast<ItemView>();
                if (view == null) continue;
                var name = view.gameObject.name.ToLowerInvariant();
                if (name.Contains("mop") && !name.Contains("bucket")) return view.transform;
            }
        }
        catch (Exception e)
        {
            GameAccess.LogOnce("mop-error", $"[Schläger] Mopp-Suche fehlgeschlagen: {e.Message}");
        }
        return null;
    }

    /// <summary>
    /// Kopiert nur das Aussehen (Meshes + Materialien), keine Spiel-Skripte und keine Physik: So bleibt der
    /// echte Mopp unberührt und die Kopie stört die Spiellogik nicht.
    /// </summary>
    private static int CopyMeshes(Transform source, Transform target)
    {
        var count = 0;
        var inverse = Quaternion.Inverse(source.rotation);
        var sourceScale = source.lossyScale;
        foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            var part = new GameObject("Teil_" + renderer.gameObject.name);
            part.transform.SetParent(target, false);
            part.transform.localPosition = inverse * (renderer.transform.position - source.position);
            part.transform.localRotation = inverse * renderer.transform.rotation;
            var scale = renderer.transform.lossyScale;
            part.transform.localScale = new Vector3(SafeDiv(scale.x, sourceScale.x), SafeDiv(scale.y, sourceScale.y), SafeDiv(scale.z, sourceScale.z));
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var copy = part.AddComponent<MeshRenderer>();
            copy.sharedMaterials = renderer.sharedMaterials;
            copy.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            count++;
        }
        return count;
    }

    private static float SafeDiv(float a, float b) => Math.Abs(b) < 1e-5f ? a : a / b;

    // ---------- Hand einer Spielfigur ----------

    /// <summary>Rechte Hand der Figur (Humanoid-Skelett, sonst über den Knochennamen).</summary>
    public static Transform FindRightHand(CharacterView view)
    {
        if (view == null) return null;
        foreach (var animator in view.GetComponentsInChildren<Animator>(true))
        {
            if (animator == null || !animator.isHuman) continue;
            var bone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (bone != null) return bone;
        }
        foreach (var t in view.GetComponentsInChildren<Transform>(true))
        {
            var name = t.name.ToLowerInvariant().Replace(" ", "");
            if (!name.Contains("hand") || name.Contains("finger") || name.Contains("thumb") || name.Contains("index")) continue;
            if (name.Contains("right") || name.EndsWith("_r") || name.EndsWith(".r") || name.Contains("hand_r") || name.Contains("r_hand")) return t;
        }
        return null;
    }

    /// <summary>Halter an einem Knochen, in Weltgröße 1 (Knochen sind oft skaliert).</summary>
    private static Transform CreateHolder(Transform bone, string name)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(bone, false);
        var s = bone.lossyScale;
        holder.localScale = new Vector3(SafeDiv(1f, s.x), SafeDiv(1f, s.y), SafeDiv(1f, s.z));
        return holder;
    }

    // ---------- Hilfen ----------

    private static void RemovePhysics(GameObject obj)
    {
        foreach (var collider in obj.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(collider);
        foreach (var body in obj.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.Destroy(body);
    }

    private static GameObject BuildFallbackPistol(Transform parent)
    {
        var root = new GameObject("GTB_Pistole");
        root.transform.SetParent(parent, false);
        AddBox(root.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.05f, 0.06f, 0.2f), new Color(0.95f, 0.45f, 0.05f));
        AddBox(root.transform, new Vector3(0f, -0.07f, -0.03f), new Vector3(0.045f, 0.12f, 0.05f), new Color(0.15f, 0.15f, 0.15f));
        return root;
    }

    private static void AddBox(Transform parent, Vector3 position, Vector3 size, Color color)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        UnityEngine.Object.Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = size;
        var renderer = box.GetComponent<MeshRenderer>();
        renderer.material.color = color;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}

// File: Assets/_CyberPickle/Editor/CapeClothSetup.cs
// Namespace: CyberPickle.EditorTools
//
// One-click cape cloth setup for the CyberPickle character. Runs on the
// currently-selected SkinnedMeshRenderer (the Jacket mesh). Adds + configures
// a Cloth component, paints per-vertex constraint coefficients, and creates
// a small body-collider rig parented to the Mixamo bones.
//
// Usage:
//   1. Open CyberPickle.prefab in Prefab Mode.
//   2. Select the SkinnedMeshRenderer GameObject for the Jacket mesh.
//   3. Menu: CyberPickle → Cloth → Setup Cape on Selected SkinnedMeshRenderer.
//   4. Save the prefab.
//
// Re-runnable: running the menu again rebuilds the cloth + colliders from
// scratch (removes any previous body collider GameObjects this script created).
//
// Tuning: edit the constants in CapeClothTuning below and re-run. Cloth
// component values can also be tweaked live in the inspector during play.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CyberPickle.EditorTools
{
    public static class CapeClothSetup
    {
        // ─── Tunables — edit these and re-run ────────────────────────────────

        private const string MENU_ROOT       = "CyberPickle/Cloth/";
        private const string SETUP_MENU_ITEM = MENU_ROOT + "Setup Cape on Selected SkinnedMeshRenderer";
        private const string CLEAR_MENU_ITEM = MENU_ROOT + "Clear Cape Setup on Selected";

        // Marker used to find body colliders WE created so re-runs can clean them up.
        private const string BODY_COLLIDER_NAME_PREFIX = "[CapeColl]";

        private static readonly CapeClothTuning Tuning = new CapeClothTuning
        {
            // Cloth component values — leather-cape feel.
            stretchingStiffness     = 0.85f,  // higher = less stretchy (leather is stiff)
            bendingStiffness        = 0.55f,  // lower = floppier folds (leather has some give)
            damping                 = 0.15f,  // 0..1; higher = settles faster, less wobble
            worldVelocityScale      = 0.5f,   // how much character motion drags the cape
            worldAccelerationScale  = 1.0f,   // how much character acceleration kicks the cape
            friction                = 0.5f,   // cape rubbing on body colliders
            collisionMassScale      = 0f,     // 0 = cape doesn't push the body
            useGravity              = true,
            randomAcceleration      = new Vector3(0.05f, 0.0f, 0.05f), // gentle wind shimmer

            // Constraint painting.
            // Cape (zero-weight) verts: gradient from top→bottom.
            capeMaxDistanceTop      = 0.05f,  // close to skin near collar/shoulders
            capeMaxDistanceBottom   = 0.60f,  // free at the hem
            capePenetrationDepth    = 0.05f,  // how far cape can sink into colliders

            // If no zero-weight verts are detected, fall back to identifying cape
            // by vertex Y position: anything below this normalized height (0..1
            // within mesh bounds) is treated as cape.
            fallbackCapeYThreshold  = 0.45f,

            // Self-collision keeps the cape from passing through itself when
            // it folds. *** DEFAULT OFF *** because it's O(n²) on cloth particles
            // and a 25k-vert mesh can crash the editor. Enable only AFTER the
            // basic cape is working AND you've reduced the Jacket mesh to
            // <5k verts (drop the Subsurf in Blender, or split the cape into
            // its own lower-poly mesh).
            useSelfCollision        = false,
            selfCollisionDistance   = 0.05f,
            selfCollisionStiffness  = 0.2f,

            // Body collider rig.
            colliders = new[]
            {
                new BodyColliderSpec("mixamorig:Hips",         radius: 0.18f, height: 0.30f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.02f, 0)),
                new BodyColliderSpec("mixamorig:Spine",        radius: 0.16f, height: 0.30f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.05f, 0)),
                new BodyColliderSpec("mixamorig:Spine1",       radius: 0.18f, height: 0.30f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.05f, 0)),
                new BodyColliderSpec("mixamorig:Spine2",       radius: 0.18f, height: 0.20f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.03f, 0)),
                new BodyColliderSpec("mixamorig:Head",         radius: 0.14f, height: 0f,     axis: ColliderAxis.Y, offset: new Vector3(0, 0.10f, 0)), // sphere
                new BodyColliderSpec("mixamorig:LeftArm",      radius: 0.08f, height: 0.30f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.15f, 0)),
                new BodyColliderSpec("mixamorig:RightArm",     radius: 0.08f, height: 0.30f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.15f, 0)),
                new BodyColliderSpec("mixamorig:LeftForeArm",  radius: 0.07f, height: 0.25f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.12f, 0)),
                new BodyColliderSpec("mixamorig:RightForeArm", radius: 0.07f, height: 0.25f, axis: ColliderAxis.Y, offset: new Vector3(0, 0.12f, 0)),
            },
        };

        // ─── Menu items ──────────────────────────────────────────────────────

        [MenuItem(SETUP_MENU_ITEM, validate = true)]
        [MenuItem(CLEAR_MENU_ITEM, validate = true)]
        private static bool Validate() => Selection.activeGameObject != null
                                       && Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>() != null;

        [MenuItem(SETUP_MENU_ITEM)]
        private static void SetupSelected()
        {
            var smr = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) { Debug.LogError("[CapeClothSetup] No SkinnedMeshRenderer on selected GameObject."); return; }

            // Safety: warn (and let the user abort) if vertex count is high.
            // Unity Cloth scales poorly past ~5–8k verts; self-collision crashes
            // the editor on 25k+. Recommend dropping the Subsurf in Blender first.
            int verts = smr.sharedMesh != null ? smr.sharedMesh.vertexCount : 0;
            if (verts > 8000)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Cape Cloth Setup — High Vertex Count",
                    $"Mesh '{smr.sharedMesh.name}' has {verts:N0} vertices.\n\n" +
                    $"Unity Cloth performs poorly above ~5,000 verts and self-collision " +
                    $"can crash the editor at this size.\n\n" +
                    $"Recommendation: drop the Subsurf level in Blender first (or split " +
                    $"out the cape into its own lower-poly mesh) before adding cloth.\n\n" +
                    $"Self-collision is already DISABLED in the tuning. Proceed anyway?",
                    ok: "Proceed (no self-collision)",
                    cancel: "Cancel");
                if (!proceed) return;
            }

            Undo.RegisterFullObjectHierarchyUndo(smr.transform.root.gameObject, "Setup Cape Cloth");

            // 1. Build / refresh the body collider rig parented to the Mixamo bones.
            var (capsules, spheres) = BuildBodyColliderRig(smr);

            // 2. Add or reset the Cloth component.
            var cloth = smr.GetComponent<Cloth>();
            if (cloth == null) cloth = Undo.AddComponent<Cloth>(smr.gameObject);

            ApplyClothTuning(cloth, Tuning);
            WireColliders(cloth, capsules, spheres);

            // 3. Paint constraint coefficients (cape verts free, jacket verts locked).
            int capeCount = PaintConstraints(cloth, smr.sharedMesh, Tuning);

            EditorUtility.SetDirty(cloth);
            EditorUtility.SetDirty(smr.gameObject);

            Debug.Log($"[CapeClothSetup] Done. Cape verts identified: {capeCount} / {smr.sharedMesh.vertexCount}. " +
                      $"Body colliders: {capsules.Count} capsules + {spheres.Count} spheres. " +
                      $"Save the prefab to persist.");
        }

        [MenuItem(CLEAR_MENU_ITEM)]
        private static void ClearSelected()
        {
            var smr = Selection.activeGameObject.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) return;

            Undo.RegisterFullObjectHierarchyUndo(smr.transform.root.gameObject, "Clear Cape Cloth");

            var cloth = smr.GetComponent<Cloth>();
            if (cloth != null) Undo.DestroyObjectImmediate(cloth);

            // Find and destroy body collider GameObjects we previously created.
            var root = smr.transform.root;
            var toDestroy = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name.StartsWith(BODY_COLLIDER_NAME_PREFIX))
                    toDestroy.Add(t.gameObject);
            }
            foreach (var go in toDestroy) Undo.DestroyObjectImmediate(go);

            Debug.Log($"[CapeClothSetup] Cleared. Removed Cloth + {toDestroy.Count} body collider GameObjects.");
        }

        // ─── Cloth tuning ────────────────────────────────────────────────────

        private static void ApplyClothTuning(Cloth cloth, CapeClothTuning t)
        {
            cloth.stretchingStiffness    = t.stretchingStiffness;
            cloth.bendingStiffness       = t.bendingStiffness;
            cloth.damping                = t.damping;
            cloth.worldVelocityScale     = t.worldVelocityScale;
            cloth.worldAccelerationScale = t.worldAccelerationScale;
            cloth.friction               = t.friction;
            cloth.collisionMassScale     = t.collisionMassScale;
            cloth.useGravity             = t.useGravity;
            cloth.randomAcceleration     = t.randomAcceleration;
            cloth.useTethers             = true;   // helps prevent runaway stretching

            // NOTE: Cloth.useVirtualParticles and Cloth.enableContinuousCollision
            // are typed inconsistently across Unity versions (float in 6.x rather
            // than bool in older versions), so we leave them at default. Defaults
            // are sensible — no action needed.

            cloth.selfCollisionDistance  = t.useSelfCollision ? t.selfCollisionDistance : 0f;
            cloth.selfCollisionStiffness = t.useSelfCollision ? t.selfCollisionStiffness : 0f;
        }

        // ─── Constraint painting ─────────────────────────────────────────────

        private static int PaintConstraints(Cloth cloth, Mesh mesh, CapeClothTuning t)
        {
            int vertCount = mesh.vertexCount;
            var coefficients = new ClothSkinningCoefficient[vertCount];

            // First pass: build the per-vertex weight totals so we can detect
            // zero-weight verts (cape).
            var bonesPerVertex = mesh.GetBonesPerVertex();
            float[] weightTotals = new float[vertCount];
            if (bonesPerVertex.IsCreated && bonesPerVertex.Length == vertCount)
            {
                var allWeights = mesh.GetAllBoneWeights();
                int wIndex = 0;
                for (int v = 0; v < vertCount; v++)
                {
                    int n = bonesPerVertex[v];
                    float sum = 0f;
                    for (int k = 0; k < n; k++) sum += allWeights[wIndex + k].weight;
                    weightTotals[v] = sum;
                    wIndex += n;
                }
            }

            // Detect cape vs jacket verts.
            var verts = mesh.vertices;
            float yMin = float.PositiveInfinity, yMax = float.NegativeInfinity;
            for (int v = 0; v < vertCount; v++)
            {
                if (verts[v].y < yMin) yMin = verts[v].y;
                if (verts[v].y > yMax) yMax = verts[v].y;
            }
            float yRange = Mathf.Max(0.0001f, yMax - yMin);

            int zeroWeightCount = 0;
            for (int v = 0; v < vertCount; v++)
                if (weightTotals[v] < 0.001f) zeroWeightCount++;

            // Choose detection strategy: prefer zero-weight if any found,
            // otherwise fall back to position-based.
            bool useZeroWeight = zeroWeightCount > 0;

            int capeCount = 0;
            for (int v = 0; v < vertCount; v++)
            {
                bool isCape;
                if (useZeroWeight)
                    isCape = weightTotals[v] < 0.001f;
                else
                {
                    float yNorm = (verts[v].y - yMin) / yRange; // 0=bottom, 1=top
                    isCape = yNorm < t.fallbackCapeYThreshold;
                }

                if (isCape)
                {
                    capeCount++;
                    // Vertical gradient: top of cape tighter, bottom looser.
                    // yNorm in 0..1 across the WHOLE mesh; the cape part typically
                    // sits at yNorm < ~0.6, so we re-normalize within the cape band.
                    float yNorm = (verts[v].y - yMin) / yRange;
                    float capeT = Mathf.Clamp01(1f - yNorm); // 0 = top of mesh, 1 = bottom
                    float maxDist = Mathf.Lerp(t.capeMaxDistanceTop, t.capeMaxDistanceBottom, capeT);

                    coefficients[v].maxDistance = maxDist;
                    coefficients[v].collisionSphereDistance = t.capePenetrationDepth;
                }
                else
                {
                    // Skinned jacket verts: locked to skin.
                    coefficients[v].maxDistance = 0f;
                    coefficients[v].collisionSphereDistance = 0f;
                }
            }

            cloth.coefficients = coefficients;

            Debug.Log($"[CapeClothSetup] Constraint detection: " +
                      $"{(useZeroWeight ? "zero-bone-weight" : "position-based fallback")}. " +
                      $"Cape verts: {capeCount}. Jacket verts: {vertCount - capeCount}.");

            return capeCount;
        }

        // ─── Body collider rig ───────────────────────────────────────────────

        private static (List<CapsuleCollider>, List<SphereCollider>) BuildBodyColliderRig(SkinnedMeshRenderer smr)
        {
            var capsules = new List<CapsuleCollider>();
            var spheres  = new List<SphereCollider>();

            // Clean up any previous colliders we created so re-runs don't pile them up.
            var root = smr.transform.root;
            var toDestroy = new List<GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (t.name.StartsWith(BODY_COLLIDER_NAME_PREFIX))
                    toDestroy.Add(t.gameObject);
            }
            foreach (var go in toDestroy) Undo.DestroyObjectImmediate(go);

            foreach (var spec in Tuning.colliders)
            {
                var bone = FindBoneByName(smr, spec.boneName);
                if (bone == null)
                {
                    Debug.LogWarning($"[CapeClothSetup] Bone '{spec.boneName}' not found — skipping collider.");
                    continue;
                }

                var holder = new GameObject($"{BODY_COLLIDER_NAME_PREFIX} {spec.boneName}");
                Undo.RegisterCreatedObjectUndo(holder, "Create Cape Collider");
                holder.transform.SetParent(bone, worldPositionStays: false);
                holder.transform.localPosition = spec.offset;
                holder.transform.localRotation = Quaternion.identity;

                if (spec.height <= 0.001f)
                {
                    var sphere = Undo.AddComponent<SphereCollider>(holder);
                    sphere.radius   = spec.radius;
                    sphere.isTrigger = true;
                    spheres.Add(sphere);
                }
                else
                {
                    var capsule = Undo.AddComponent<CapsuleCollider>(holder);
                    capsule.radius    = spec.radius;
                    capsule.height    = spec.height + spec.radius * 2f; // total length incl. caps
                    capsule.direction = (int)spec.axis;
                    capsule.isTrigger = true;
                    capsules.Add(capsule);
                }
            }

            return (capsules, spheres);
        }

        private static void WireColliders(Cloth cloth, List<CapsuleCollider> capsules, List<SphereCollider> spheres)
        {
            cloth.capsuleColliders = capsules.ToArray();

            // Cloth's sphereColliders array uses a paired struct (ClothSphereColliderPair)
            // so a "single sphere" entry is just (first=collider, second=null).
            var pairs = new ClothSphereColliderPair[spheres.Count];
            for (int i = 0; i < spheres.Count; i++)
                pairs[i] = new ClothSphereColliderPair(spheres[i]);
            cloth.sphereColliders = pairs;
        }

        private static Transform FindBoneByName(SkinnedMeshRenderer smr, string boneName)
        {
            foreach (var b in smr.bones)
                if (b != null && b.name == boneName) return b;

            // Fallback: deep search the rig hierarchy in case the SMR's bone list
            // doesn't include this specific bone (it shouldn't, but defensive).
            var root = smr.rootBone != null ? smr.rootBone : smr.transform.root;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName) return t;

            return null;
        }

        // ─── Tuning structs ──────────────────────────────────────────────────

        [System.Serializable]
        private struct CapeClothTuning
        {
            public float   stretchingStiffness;
            public float   bendingStiffness;
            public float   damping;
            public float   worldVelocityScale;
            public float   worldAccelerationScale;
            public float   friction;
            public float   collisionMassScale;
            public bool    useGravity;
            public Vector3 randomAcceleration;

            public float capeMaxDistanceTop;
            public float capeMaxDistanceBottom;
            public float capePenetrationDepth;
            public float fallbackCapeYThreshold;

            public bool  useSelfCollision;
            public float selfCollisionDistance;
            public float selfCollisionStiffness;

            public BodyColliderSpec[] colliders;
        }

        private enum ColliderAxis { X = 0, Y = 1, Z = 2 }

        [System.Serializable]
        private struct BodyColliderSpec
        {
            public string       boneName;
            public float        radius;
            public float        height;   // 0 = sphere, > 0 = capsule (height excluding caps)
            public ColliderAxis axis;
            public Vector3      offset;

            public BodyColliderSpec(string boneName, float radius, float height, ColliderAxis axis, Vector3 offset)
            {
                this.boneName = boneName;
                this.radius   = radius;
                this.height   = height;
                this.axis     = axis;
                this.offset   = offset;
            }
        }
    }
}

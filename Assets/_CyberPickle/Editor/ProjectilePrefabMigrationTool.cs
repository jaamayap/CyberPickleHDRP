// File: Assets/_CyberPickle/Editor/ProjectilePrefabMigrationTool.cs
//
// One-click Editor menu helper for migrating Hovl Studio projectile
// prefabs to the CyberPickle hybrid lifecycle.
//
// What it does to every projectile referenced in ElementVfxLibrary:
//   1. Removes the Hovl `HS_ProjectileMover` MonoBehaviour (if present).
//   2. Removes the Rigidbody (no Mono motion; ECS owns motion).
//   3. Removes every Collider on the root (no Mono collision; ECS owns it).
//   4. Adds a CyberPickleProjectileVisual MonoBehaviour (if not already
//      present). Tries to auto-link the Hovl prefab's visual references
//      (projectilePS, hitPS, flash, light, etc.) by inspecting the
//      hierarchy heuristically.
//
// Run via menu: CyberPickle → Tools → Migrate Projectile Prefabs (Hovl → Hybrid)
//
// Idempotent — safe to run multiple times. Logs a per-prefab report so
// you can see what got changed.
//
// IMPORTANT — auto-linking is BEST-EFFORT. The original Hovl prefabs
// have specific GameObjects assigned to `hit`, `hitPS`, `projectilePS`,
// `Detached[]`, etc. The tool tries to recover these from the prefab's
// children by name/structure heuristics, but you should OPEN EACH PREFAB
// after migration and verify the CyberPickleProjectileVisual fields are
// correctly populated. If anything's missing, hand-assign in the
// inspector. The original HS_ProjectileMover field values are visible in
// the prefab's text YAML if you need to reference them.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CyberPickle.Gameplay.Weapons;

namespace CyberPickle.Editor.Tools
{
    public static class ProjectilePrefabMigrationTool
    {
        private const string EnvVfxLibraryPath = "Assets/_CyberPickle/Resources/ElementVfxLibrary.asset";
        private const string MenuPath          = "CyberPickle/Tools/Migrate Projectile Prefabs (Hovl → Hybrid)";
        private const string SyncMenuPath      = "CyberPickle/Tools/Sync Hovl Tuning to Migrated Projectiles";
        private const string HovlPrefabsFolder = "Assets/Hovl Studio/AAA Projectiles Vol 2/Prefabs";

        [MenuItem(MenuPath)]
        public static void Migrate()
        {
            // Load the ElementVfxLibrary to find which prefabs to migrate.
            var library = AssetDatabase.LoadAssetAtPath<ElementVfxLibrary>(EnvVfxLibraryPath);
            if (library == null)
            {
                Debug.LogError($"[ProjectilePrefabMigrationTool] Could not load ElementVfxLibrary at {EnvVfxLibraryPath}. Aborting.");
                return;
            }

            // entries is a public field on ElementVfxLibrary — no reflection
            // needed, just use the typed property directly.
            var entriesArray = library.entries;
            if (entriesArray == null || entriesArray.Length == 0)
            {
                Debug.LogWarning("[ProjectilePrefabMigrationTool] ElementVfxLibrary has no entries.");
                return;
            }

            int migrated = 0;
            int skipped  = 0;
            int errors   = 0;

            for (int i = 0; i < entriesArray.Length; i++)
            {
                var prefab = entriesArray[i].projectilePrefab;
                if (prefab == null)
                {
                    Debug.Log($"[ProjectilePrefabMigrationTool] Element {i}: no projectilePrefab assigned, skipping.");
                    skipped++;
                    continue;
                }

                bool ok = MigrateOnePrefab(prefab, i);
                if (ok) migrated++;
                else errors++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Projectile Prefab Migration",
                $"Migration complete:\n\n  Migrated: {migrated}\n  Skipped:  {skipped}\n  Errors:   {errors}\n\nSee console for per-prefab details. OPEN EACH PREFAB and verify the CyberPickleProjectileVisual field assignments — auto-link is best-effort.",
                "OK");
        }

        /// <summary>
        /// Migrate a single prefab in place. Returns true if the prefab was
        /// successfully written, false if any step errored.
        /// </summary>
        private static bool MigrateOnePrefab(GameObject prefab, int elementIndex)
        {
            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"[ProjectilePrefabMigrationTool] Could not get asset path for prefab '{prefab.name}'.");
                return false;
            }

            string prefabName = Path.GetFileNameWithoutExtension(assetPath);
            Debug.Log($"<color=cyan>[ProjectilePrefabMigrationTool]</color> Element {elementIndex}: migrating '{prefabName}' at {assetPath}");

            // Open the prefab for editing in isolation (modifications
            // happen on a temporary scene-loaded copy that we save back).
            var prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[ProjectilePrefabMigrationTool] LoadPrefabContents failed for {assetPath}");
                return false;
            }

            try
            {
                int removalCount = 0;

                // 1. CAPTURE Hovl field values BEFORE removing the script.
                //    For prefabs that still have HS_ProjectileMover, this
                //    gives us a perfect 1:1 transfer of every field
                //    (including scalar `hitOffset`, bool `UseFirePointRotation`,
                //    `rotationOffset`, and the manually-assigned `Detached[]`
                //    array — none of which the name-based heuristic could
                //    recover). For prefabs already cleaned of Hovl, `captured`
                //    is null and we fall back to the heuristic.
                var captured = TryCaptureHovlValues(prefabRoot);
                if (captured != null)
                    Debug.Log($"    → captured Hovl values: hitOffset={captured.hitOffset}, useFirePointRot={captured.useFirePointRotation}, rotOffset={captured.rotationOffset}, Detached[{captured.detached?.Length ?? 0}]");

                // 2. Remove HS_ProjectileMover (any subclass too).
                foreach (var mb in prefabRoot.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
                {
                    if (mb == null) continue;
                    var typeName = mb.GetType().Name;
                    if (typeName == "HS_ProjectileMover" || mb.GetType().FullName == "HS_ProjectileMover")
                    {
                        Object.DestroyImmediate(mb, allowDestroyingAssets: true);
                        removalCount++;
                        Debug.Log($"    → removed HS_ProjectileMover");
                    }
                }

                // 3. Remove Rigidbody (any).
                foreach (var rb in prefabRoot.GetComponentsInChildren<Rigidbody>(includeInactive: true))
                {
                    if (rb == null) continue;
                    Object.DestroyImmediate(rb, allowDestroyingAssets: true);
                    removalCount++;
                    Debug.Log($"    → removed Rigidbody");
                }

                // 4. Remove all Colliders (Sphere/Box/Capsule/Mesh/etc.).
                foreach (var col in prefabRoot.GetComponentsInChildren<Collider>(includeInactive: true))
                {
                    if (col == null) continue;
                    var t = col.GetType().Name;
                    Object.DestroyImmediate(col, allowDestroyingAssets: true);
                    removalCount++;
                    Debug.Log($"    → removed {t}");
                }

                // 5. Add CyberPickleProjectileVisual on the root if not present.
                var visual = prefabRoot.GetComponent<CyberPickleProjectileVisual>();
                bool newlyAdded = visual == null;
                if (newlyAdded)
                {
                    visual = prefabRoot.AddComponent<CyberPickleProjectileVisual>();
                    Debug.Log($"    → added CyberPickleProjectileVisual");
                }
                else
                {
                    Debug.Log($"    → CyberPickleProjectileVisual already present");
                }

                // 6. Populate field values. Prefer captured Hovl values
                //    (perfect 1:1 transfer); fall back to heuristic
                //    auto-link if no Hovl script was found.
                if (captured != null)
                    ApplyCapturedValues(visual, captured);
                else if (newlyAdded)
                    AutoLinkVisualReferences(prefabRoot, visual);
                else
                    Debug.Log($"    → visual already present and no Hovl source — leaving existing field assignments untouched");

                if (removalCount == 0 && visual != null && captured == null)
                    Debug.Log($"    → prefab was already clean, no removals needed");

                // Save the modified prefab back to its asset path.
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ProjectilePrefabMigrationTool] Migration of '{prefabName}' failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        /// <summary>
        /// Best-effort auto-linking of the CyberPickleProjectileVisual's
        /// inspector references by inspecting the prefab hierarchy.
        ///   - projectilePS: the largest / topmost ParticleSystem (heuristic).
        ///   - hit: a child GO whose name contains "hit" (case-insensitive).
        ///   - hitPS: ParticleSystem on the hit child.
        ///   - flash: a child GO whose name contains "flash".
        ///   - lightSource: the first Light component in children.
        ///   - Detached[]: child GOs whose names contain "trail" or
        ///     "detached" — empty if none found, designer assigns manually.
        ///
        /// Heuristic, NOT guaranteed correct. The designer should open
        /// each prefab post-migration and verify.
        /// </summary>
        private static void AutoLinkVisualReferences(GameObject root, CyberPickleProjectileVisual visual)
        {
            var so = new SerializedObject(visual);

            // Walk the hierarchy once and collect candidates.
            var allTransforms = root.GetComponentsInChildren<Transform>(includeInactive: true);
            var allPS         = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            var allLights     = root.GetComponentsInChildren<Light>(includeInactive: true);

            GameObject hitGO    = null;
            GameObject flashGO  = null;
            ParticleSystem hitPS    = null;
            ParticleSystem mainPS   = null;
            var detachedList = new List<GameObject>();

            foreach (var t in allTransforms)
            {
                if (t == root.transform) continue;
                string nLower = t.name.ToLowerInvariant();
                if (hitGO == null && (nLower.Contains("hit") || nLower.Contains("impact"))) hitGO = t.gameObject;
                if (flashGO == null && nLower.Contains("flash")) flashGO = t.gameObject;
                if (nLower.Contains("trail") || nLower.Contains("detached"))
                    detachedList.Add(t.gameObject);
            }

            if (hitGO != null)
            {
                hitPS = hitGO.GetComponent<ParticleSystem>();
                if (hitPS == null) hitPS = hitGO.GetComponentInChildren<ParticleSystem>(includeInactive: true);
            }

            // projectilePS = largest PS that ISN'T the hit PS — heuristic
            // for the bullet's main head/body emitter.
            int bestMax = -1;
            foreach (var ps in allPS)
            {
                if (ps == hitPS) continue;
                int maxParticles = ps.main.maxParticles;
                if (maxParticles > bestMax) { bestMax = maxParticles; mainPS = ps; }
            }

            // Assign via SerializedObject so the assignments survive prefab save.
            SetObjectRef(so, "hit",          hitGO);
            SetObjectRef(so, "hitPS",        hitPS);
            SetObjectRef(so, "flash",        flashGO);
            SetObjectRef(so, "projectilePS", mainPS);
            SetObjectRef(so, "lightSource",  allLights.Length > 0 ? (Object)allLights[0] : null);

            // Detached[]: assign the list to the SerializedProperty array.
            var detachedProp = so.FindProperty("Detached");
            if (detachedProp != null && detachedProp.isArray)
            {
                detachedProp.arraySize = detachedList.Count;
                for (int i = 0; i < detachedList.Count; i++)
                {
                    detachedProp.GetArrayElementAtIndex(i).objectReferenceValue = detachedList[i];
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"    → auto-linked: hit={(hitGO != null ? hitGO.name : "<null>")}, " +
                      $"hitPS={(hitPS != null ? hitPS.name : "<null>")}, " +
                      $"projectilePS={(mainPS != null ? mainPS.name : "<null>")}, " +
                      $"flash={(flashGO != null ? flashGO.name : "<null>")}, " +
                      $"Detached[{detachedList.Count}]");
        }

        private static void SetObjectRef(SerializedObject so, string propName, Object value)
        {
            var prop = so.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        // ─── Hovl value capture + apply ─────────────────────────────────

        /// <summary>
        /// Captures the HS_ProjectileMover field values from a prefab root
        /// via reflection (HS_ProjectileMover fields are protected). Object
        /// references are recorded as HIERARCHY PATHS relative to the prefab
        /// root so they can be resolved against a different prefab's
        /// hierarchy (used by SyncFromHovlOriginals to copy values from the
        /// Hovl asset-pack originals into the user's migrated copies).
        ///
        /// Returns null if no HS_ProjectileMover is found on the root.
        /// </summary>
        private static HovlCapturedValues TryCaptureHovlValues(GameObject prefabRoot)
        {
            MonoBehaviour hsMover = null;
            foreach (var mb in prefabRoot.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                if (mb.GetType().Name == "HS_ProjectileMover") { hsMover = mb; break; }
            }
            if (hsMover == null) return null;

            var type = hsMover.GetType();
            var bind = System.Reflection.BindingFlags.NonPublic
                     | System.Reflection.BindingFlags.Public
                     | System.Reflection.BindingFlags.Instance;

            var captured = new HovlCapturedValues();

            // Scalars / value types.
            captured.hitOffset            = (float)  (type.GetField("hitOffset", bind)?.GetValue(hsMover)            ?? 0f);
            captured.useFirePointRotation = (bool)   (type.GetField("UseFirePointRotation", bind)?.GetValue(hsMover) ?? false);
            captured.rotationOffset       = (Vector3)(type.GetField("rotationOffset", bind)?.GetValue(hsMover)       ?? Vector3.zero);

            // Object refs → hierarchy paths.
            captured.hitPath          = PathOf(prefabRoot, type.GetField("hit", bind)?.GetValue(hsMover)          as GameObject);
            captured.hitPsPath        = PathOf(prefabRoot, (type.GetField("hitPS", bind)?.GetValue(hsMover)        as ParticleSystem)?.gameObject);
            captured.flashPath        = PathOf(prefabRoot, type.GetField("flash", bind)?.GetValue(hsMover)        as GameObject);
            // Hovl's field name typo: "lightSourse" (sic).
            captured.lightSourcePath  = PathOf(prefabRoot, (type.GetField("lightSourse", bind)?.GetValue(hsMover)  as Light)?.gameObject);
            captured.projectilePsPath = PathOf(prefabRoot, (type.GetField("projectilePS", bind)?.GetValue(hsMover) as ParticleSystem)?.gameObject);

            // Detached[] — array of GameObject refs → array of paths.
            var detachedField = type.GetField("Detached", bind);
            var detachedArray = detachedField?.GetValue(hsMover) as GameObject[];
            captured.detachedPaths = new string[detachedArray?.Length ?? 0];
            for (int i = 0; i < captured.detachedPaths.Length; i++)
                captured.detachedPaths[i] = PathOf(prefabRoot, detachedArray[i]);

            // Convenience flat array for the migration debug log.
            captured.detached = detachedArray ?? new GameObject[0];

            return captured;
        }

        /// <summary>
        /// Applies captured Hovl values to a CyberPickleProjectileVisual.
        /// Scalars are direct-copied; object references are resolved
        /// against the destination prefab's hierarchy by their captured
        /// paths.
        /// </summary>
        private static void ApplyCapturedValues(CyberPickleProjectileVisual visual, HovlCapturedValues captured)
        {
            if (visual == null || captured == null) return;
            var destRoot = visual.gameObject;

            var so = new SerializedObject(visual);

            // Scalars.
            var hitOffsetProp     = so.FindProperty("hitOffset");
            if (hitOffsetProp != null) hitOffsetProp.floatValue = captured.hitOffset;
            var useFirePointProp  = so.FindProperty("UseFirePointRotation");
            if (useFirePointProp != null) useFirePointProp.boolValue = captured.useFirePointRotation;
            var rotOffsetProp     = so.FindProperty("rotationOffset");
            if (rotOffsetProp != null) rotOffsetProp.vector3Value = captured.rotationOffset;

            // Object refs — resolve hierarchy paths to GameObjects/Components
            // in the destination prefab.
            SetObjectRef(so, "hit",          ResolvePath(destRoot, captured.hitPath));
            SetObjectRef(so, "hitPS",        ResolveComponent<ParticleSystem>(destRoot, captured.hitPsPath));
            SetObjectRef(so, "flash",        ResolvePath(destRoot, captured.flashPath));
            SetObjectRef(so, "lightSource",  ResolveComponent<Light>(destRoot, captured.lightSourcePath));
            SetObjectRef(so, "projectilePS", ResolveComponent<ParticleSystem>(destRoot, captured.projectilePsPath));

            // Detached[].
            var detachedProp = so.FindProperty("Detached");
            if (detachedProp != null && detachedProp.isArray)
            {
                detachedProp.arraySize = captured.detachedPaths.Length;
                for (int i = 0; i < captured.detachedPaths.Length; i++)
                {
                    var go = ResolvePath(destRoot, captured.detachedPaths[i]);
                    detachedProp.GetArrayElementAtIndex(i).objectReferenceValue = go;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"    → applied captured Hovl values to CyberPickleProjectileVisual");
        }

        /// <summary>Hierarchy path of <paramref name="child"/> relative to <paramref name="root"/>, or null if the child isn't in the root's hierarchy.</summary>
        private static string PathOf(GameObject root, GameObject child)
        {
            if (root == null || child == null) return null;
            if (child == root) return "";
            // Walk up the hierarchy until we hit root or run off the top.
            var parts = new List<string>();
            var t = child.transform;
            while (t != null && t.gameObject != root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            if (t == null) return null; // child isn't under root
            parts.Reverse();
            return string.Join("/", parts);
        }

        /// <summary>Resolve a hierarchy path under <paramref name="root"/> to the GameObject at that path. Returns null if missing.</summary>
        private static GameObject ResolvePath(GameObject root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
            var t = root.transform.Find(path);
            return t != null ? t.gameObject : null;
        }

        /// <summary>Resolve a hierarchy path and return the requested component on the resolved GameObject.</summary>
        private static T ResolveComponent<T>(GameObject root, string path) where T : Component
        {
            var go = ResolvePath(root, path);
            return go != null ? go.GetComponent<T>() : null;
        }

        // ─── Sync from Hovl originals (for already-migrated prefabs) ────

        /// <summary>
        /// Captured-but-not-yet-applied Hovl values, used by both the
        /// in-place migration path (capture-before-removing) and the
        /// cross-prefab sync path (read from original, apply to migrated copy).
        /// </summary>
        private class HovlCapturedValues
        {
            public float hitOffset;
            public bool useFirePointRotation;
            public Vector3 rotationOffset;
            public string hitPath;
            public string hitPsPath;
            public string flashPath;
            public string lightSourcePath;
            public string projectilePsPath;
            public string[] detachedPaths;
            // Debug-only flat reference (count for log).
            public GameObject[] detached;
        }

        /// <summary>
        /// For each projectile prefab referenced by ElementVfxLibrary, find
        /// the matching ORIGINAL Hovl prefab in
        /// <see cref="HovlPrefabsFolder"/> (matched by file name), read its
        /// HS_ProjectileMover field values, and apply them to the migrated
        /// prefab's CyberPickleProjectileVisual.
        ///
        /// Use this AFTER running the main migration if you find scalar
        /// fields (hitOffset, rotationOffset, UseFirePointRotation) or the
        /// Detached[] array are empty/wrong on your migrated copies — those
        /// are the values the heuristic auto-link can't recover.
        /// </summary>
        [MenuItem(SyncMenuPath)]
        public static void SyncFromHovlOriginals()
        {
            var library = AssetDatabase.LoadAssetAtPath<ElementVfxLibrary>(EnvVfxLibraryPath);
            if (library == null)
            {
                Debug.LogError($"[ProjectilePrefabMigrationTool] Could not load ElementVfxLibrary at {EnvVfxLibraryPath}.");
                return;
            }

            int synced = 0;
            int missing = 0;
            int errors = 0;

            for (int i = 0; i < library.entries.Length; i++)
            {
                var destPrefab = library.entries[i].projectilePrefab;
                if (destPrefab == null) continue;

                string destPath = AssetDatabase.GetAssetPath(destPrefab);
                string baseName = Path.GetFileNameWithoutExtension(destPath);
                string sourcePath = $"{HovlPrefabsFolder}/{baseName}.prefab";

                if (!System.IO.File.Exists(sourcePath))
                {
                    Debug.LogWarning($"[ProjectilePrefabMigrationTool] Element {i}: no Hovl original found at '{sourcePath}' (looking for matching name). Skipping.");
                    missing++;
                    continue;
                }

                Debug.Log($"<color=cyan>[ProjectilePrefabMigrationTool]</color> Element {i}: syncing values from '{sourcePath}' → '{destPath}'");

                // Load both prefabs.
                var sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
                var destRoot   = PrefabUtility.LoadPrefabContents(destPath);
                if (sourceRoot == null || destRoot == null)
                {
                    Debug.LogError($"    → failed to load prefab(s).");
                    if (sourceRoot != null) PrefabUtility.UnloadPrefabContents(sourceRoot);
                    if (destRoot != null) PrefabUtility.UnloadPrefabContents(destRoot);
                    errors++;
                    continue;
                }

                try
                {
                    var captured = TryCaptureHovlValues(sourceRoot);
                    if (captured == null)
                    {
                        Debug.LogWarning($"    → no HS_ProjectileMover found on source. Skipping (already cleaned?).");
                        missing++;
                        continue;
                    }

                    Debug.Log($"    → captured from source: hitOffset={captured.hitOffset}, useFirePointRot={captured.useFirePointRotation}, rotOffset={captured.rotationOffset}, Detached[{captured.detachedPaths?.Length ?? 0}], paths: hit='{captured.hitPath}' projectilePS='{captured.projectilePsPath}'");

                    var visual = destRoot.GetComponent<CyberPickleProjectileVisual>();
                    if (visual == null)
                    {
                        Debug.LogWarning($"    → destination has no CyberPickleProjectileVisual. Run the main migration first.");
                        errors++;
                        continue;
                    }

                    ApplyCapturedValues(visual, captured);
                    PrefabUtility.SaveAsPrefabAsset(destRoot, destPath);
                    synced++;
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"    → sync failed: {ex.Message}\n{ex.StackTrace}");
                    errors++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(sourceRoot);
                    PrefabUtility.UnloadPrefabContents(destRoot);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Sync Hovl Tuning",
                $"Sync complete:\n\n  Synced:   {synced}\n  Missing:  {missing} (no source / no HS_ProjectileMover)\n  Errors:   {errors}\n\nSee console for per-prefab paths and values.",
                "OK");
        }
    }
}

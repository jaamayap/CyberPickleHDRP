// File: Assets/_CyberPickle/Code/DOTS/Bridge/EnemyVisualBridge.cs
// Namespace: CyberPickle.DOTS.Bridge
//
// Scene-scoped singleton MonoBehaviour that owns the Entity -> Transform
// mapping for every active enemy visual. Lives in Game.unity (NOT
// DontDestroyOnLoad) — when the scene unloads, all visuals are torn
// down with the bridge.
//
// EnemyVisualBindingSystem (a SystemBase) talks to this bridge each
// frame to spawn / sync / despawn visuals.
//
// Why not Manager<T>: Manager<T> uses DontDestroyOnLoad and would
// survive scene unload, leaking visuals across scene transitions.
// This bridge is intentionally per-scene.

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace CyberPickle.DOTS.Bridge
{
    [DisallowMultipleComponent]
    public class EnemyVisualBridge : MonoBehaviour
    {
        private static EnemyVisualBridge instance;

        /// <summary>
        /// Lazy singleton accessor. If no bridge GameObject exists in the
        /// active scene, one is created on first access. Only valid in
        /// play mode — returns null in edit mode.
        /// </summary>
        public static EnemyVisualBridge Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    instance = FindFirstObjectByType<EnemyVisualBridge>();
                    if (instance == null)
                    {
                        var go = new GameObject("[EnemyVisualBridge]");
                        instance = go.AddComponent<EnemyVisualBridge>();
                    }
                }
                return instance;
            }
        }

        private readonly Dictionary<Entity, Transform> visuals = new Dictionary<Entity, Transform>(256);

        /// <summary>Live read-only view of the Entity -> visual Transform map. Used by the binding system for the per-frame transform sync.</summary>
        public IReadOnlyDictionary<Entity, Transform> Visuals => visuals;

        public int ActiveVisualCount => visuals.Count;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("[EnemyVisualBridge] Duplicate instance detected, destroying.", this);
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void OnDestroy()
        {
            // Defensive cleanup — destroy any orphaned visuals if the scene
            // unloads while entities are still alive in the world.
            foreach (var kv in visuals)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            }
            visuals.Clear();
            if (instance == this) instance = null;
        }

        public void Register(Entity entity, GameObject visual)
        {
            if (visual == null) return;
            visuals[entity] = visual.transform;
        }

        /// <summary>Removes the entry and returns its Transform via out, so the caller can Destroy it. Returns false if no entry existed.</summary>
        public bool Unregister(Entity entity, out Transform visual)
        {
            if (visuals.TryGetValue(entity, out visual))
            {
                visuals.Remove(entity);
                return true;
            }
            return false;
        }

        public bool TryGet(Entity entity, out Transform visual)
        {
            return visuals.TryGetValue(entity, out visual);
        }
    }
}

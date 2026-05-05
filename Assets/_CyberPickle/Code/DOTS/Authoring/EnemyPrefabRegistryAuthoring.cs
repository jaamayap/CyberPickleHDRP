// File: Assets/_CyberPickle/Code/DOTS/Authoring/EnemyPrefabRegistryAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Multi-entry enemy prefab registry. Place this on the same GameObject
// in EnemisSubScene that holds (or held) EnemyPrefabAuthoring. Each
// entry pairs an EnemyData SO with the entity authoring prefab to bake.
//
// At bake time, the Baker:
//   1. Calls Baker.GetEntity(entry.entityPrefab) to register each prefab
//      as a baked Prefab-tagged entity in the world.
//   2. Hashes entry.data.enemyId via Animator.StringToHash.
//   3. Adds an EnemyPrefabBufferElement for each entry to the singleton
//      entity's DynamicBuffer.
//
// Spawners (EnemySwarmSpawner now, WaveSystem in 5c) look up by
// EnemyData → hash → buffer entry, then Instantiate the prefab.
//
// Coexists with EnemyPrefabAuthoring (the single-prefab MVP) — both
// can be on the same GameObject during migration. EnemySwarmSpawner
// prefers the registry when present, falls back to the singleton.

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Enemies;

namespace CyberPickle.DOTS.Authoring
{
    public class EnemyPrefabRegistryAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("EnemyData SO defining this enemy type's stats and identity. Provides the lookup key (enemyId hash).")]
            public EnemyData data;

            [Tooltip("Entity authoring prefab — lightweight prefab carrying EnemyAuthoring + PhysicsShape + PhysicsBody. NO mesh / animator / visual components.")]
            public GameObject entityPrefab;
        }

        [Tooltip("Each enemy type the game can spawn. Keep these aligned: data.enemyId is the lookup key, entityPrefab is what gets Instantiated.")]
        public List<Entry> entries = new List<Entry>();

        public class Baker : Baker<EnemyPrefabRegistryAuthoring>
        {
            public override void Bake(EnemyPrefabRegistryAuthoring authoring)
            {
                if (authoring.entries == null || authoring.entries.Count == 0)
                {
                    Debug.LogWarning($"[EnemyPrefabRegistryAuthoring] '{authoring.name}' has no entries — no enemy prefabs will be available at runtime.", authoring);
                    return;
                }

                Entity self = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<EnemyPrefabBufferElement>(self);

                foreach (var entry in authoring.entries)
                {
                    if (entry == null) continue;

                    if (entry.data == null)
                    {
                        Debug.LogWarning($"[EnemyPrefabRegistryAuthoring] '{authoring.name}': an entry has no EnemyData assigned — skipped.", authoring);
                        continue;
                    }

                    if (entry.entityPrefab == null)
                    {
                        Debug.LogWarning($"[EnemyPrefabRegistryAuthoring] '{authoring.name}': entry for '{entry.data.enemyId}' has no entityPrefab — skipped.", authoring);
                        continue;
                    }

                    // Re-bake when SOs change so designers see updates without manual rebuild.
                    DependsOn(entry.data);

                    Entity prefabEntity = GetEntity(entry.entityPrefab, TransformUsageFlags.Dynamic);

                    buffer.Add(new EnemyPrefabBufferElement
                    {
                        Hash   = entry.data.GetIdHash(),
                        Prefab = prefabEntity
                    });
                }
            }
        }

        private void OnValidate()
        {
            if (entries == null) return;

            // Detect duplicate enemyIds at edit time — registry lookup uses the
            // first match, so duplicates would silently shadow each other.
            var seenIds = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry?.data == null) continue;
                if (!seenIds.Add(entry.data.enemyId))
                {
                    Debug.LogWarning($"[EnemyPrefabRegistryAuthoring] '{name}': duplicate enemyId '{entry.data.enemyId}' in registry. Lookup will use the first occurrence.", this);
                }
            }
        }
    }
}

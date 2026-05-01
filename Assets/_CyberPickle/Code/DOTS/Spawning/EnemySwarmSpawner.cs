// File: Assets/_CyberPickle/Code/DOTS/Spawning/EnemySwarmSpawner.cs
// Namespace: CyberPickle.DOTS.Spawning
//
// MonoBehaviour test spawner. Lives in Game.unity. After a brief delay
// (so the SubScene + DOTS world finish initializing), it instantiates
// N enemy entities in a ring around the spawner's transform.
//
// Prefab resolution priority:
//   1. If `enemyType` (an EnemyData SO reference) is set, look up the
//      matching baked entity prefab in the EnemyPrefabBufferElement
//      registry by enemyId hash. Multi-prefab path. (Chunk 5b)
//   2. Otherwise, fall back to EnemyPrefabSingleton (the legacy
//      single-prefab path from chunk 5a).
//
// This spawner is intentionally NOT the production wave system — that
// arrives in chunk 5c with pacing, composition, difficulty scaling.
// One spawner = one enemy type, one burst on Start.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Enemies;

namespace CyberPickle.DOTS.Spawning
{
    [DisallowMultipleComponent]
    public class EnemySwarmSpawner : MonoBehaviour
    {
        [Header("Enemy Selection")]
        [Tooltip("Which enemy type to spawn. The spawner looks up the matching baked entity prefab in EnemyPrefabRegistryAuthoring by this SO's enemyId hash. Leave null to fall back to the legacy single-prefab EnemyPrefabSingleton.")]
        public EnemyData enemyType;

        [Header("Spawn Configuration")]
        [Tooltip("How many enemies to spawn at start.")]
        [Min(1)] public int spawnCount = 50;

        [Tooltip("Radius (meters) of the ring around the spawner where enemies appear.")]
        [Min(1f)] public float spawnRadius = 15f;

        [Tooltip("Random radial jitter so enemies don't form a perfect ring.")]
        [Min(0f)] public float radialJitter = 2f;

        [Tooltip("Per-instance MoveSpeed multiplier picked uniformly from [1 - jitter, 1 + jitter]. 0 = all enemies same speed; 0.25 = ±25% speed variation. Decorrelates pack arrival timing.")]
        [Range(0f, 0.5f)] public float speedJitter = 0.25f;

        [Tooltip("Delay before spawning starts. Lets the SubScene + DOTS world fully initialize.")]
        [Min(0f)] public float spawnDelay = 0.5f;

        [Tooltip("If the prefab can't be resolved within this many seconds after enable, give up and log an error.")]
        [Min(0.1f)] public float prefabResolveTimeout = 10f;

        [Tooltip("Log a summary line after spawning completes.")]
        public bool verbose = true;

        private bool spawned;
        private float elapsed;

        private void Update()
        {
            if (spawned) return;

            elapsed += Time.deltaTime;
            if (elapsed < spawnDelay) return;

            if (TrySpawn())
            {
                spawned = true;
                return;
            }

            if (elapsed > spawnDelay + prefabResolveTimeout)
            {
                string identity = enemyType != null ? $"enemyId='{enemyType.enemyId}'" : "EnemyPrefabSingleton";
                Debug.LogError($"[EnemySwarmSpawner] Gave up after {prefabResolveTimeout}s waiting for prefab ({identity}). " +
                               "Verify the SubScene is loaded and the registry entry exists.", this);
                spawned = true;
            }
        }

        private bool TrySpawn()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return false;

            var em = world.EntityManager;

            Entity prefabEntity = ResolvePrefabEntity(em);
            if (prefabEntity == Entity.Null) return false;
            if (!em.Exists(prefabEntity))
            {
                Debug.LogWarning($"[EnemySwarmSpawner] Resolved prefab entity but it no longer exists in the world.", this);
                return false;
            }

            float3 origin = transform.position;
            using var instances = new NativeArray<Entity>(spawnCount, Allocator.Temp);
            em.Instantiate(prefabEntity, instances);

            uint seed = (uint)System.Environment.TickCount | 1u;
            var rng = new Unity.Mathematics.Random(seed);
            float twoPi = 2f * math.PI;

            for (int i = 0; i < spawnCount; i++)
            {
                float angle = (i / (float)spawnCount) * twoPi + rng.NextFloat(-0.05f, 0.05f);
                float r = spawnRadius + rng.NextFloat(-radialJitter, radialJitter);
                float3 pos = origin + new float3(math.cos(angle) * r, 0f, math.sin(angle) * r);

                em.SetComponentData(instances[i], LocalTransform.FromPositionRotationScale(
                    pos,
                    quaternion.identity,
                    1f));

                if (speedJitter > 0f && em.HasComponent<MoveSpeed>(instances[i]))
                {
                    var baseSpeed = em.GetComponentData<MoveSpeed>(instances[i]).Value;
                    float factor = 1f + rng.NextFloat(-speedJitter, speedJitter);
                    em.SetComponentData(instances[i], new MoveSpeed { Value = baseSpeed * factor });
                }
            }

            if (verbose)
            {
                string label = enemyType != null ? enemyType.enemyId : "(legacy singleton)";
                Debug.Log($"[EnemySwarmSpawner] Spawned {spawnCount} '{label}' enemies in a ring (radius {spawnRadius:F1}, jitter {radialJitter:F1}) around {origin}.", this);
            }
            return true;
        }

        // ─── Prefab resolution ───────────────────────────────────────────────

        /// <summary>
        /// Returns the entity prefab to Instantiate. Prefers the multi-entry
        /// registry (EnemyPrefabBufferElement) when `enemyType` is set; falls
        /// back to the legacy EnemyPrefabSingleton otherwise. Returns
        /// Entity.Null if neither path resolves yet (caller retries).
        /// </summary>
        private Entity ResolvePrefabEntity(EntityManager em)
        {
            if (enemyType != null)
            {
                Entity fromRegistry = ResolveFromRegistry(em, enemyType.GetIdHash());
                if (fromRegistry != Entity.Null) return fromRegistry;
                // Registry exists but no matching id, OR registry doesn't exist yet.
                // Fall through to singleton fallback below — useful during migration.
            }

            return ResolveFromSingleton(em);
        }

        private Entity ResolveFromRegistry(EntityManager em, int enemyIdHash)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyPrefabBufferElement>());
            if (query.CalculateEntityCount() == 0) return Entity.Null;

            using var registries = query.ToEntityArray(Allocator.Temp);
            for (int r = 0; r < registries.Length; r++)
            {
                var buffer = em.GetBuffer<EnemyPrefabBufferElement>(registries[r], isReadOnly: true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Hash == enemyIdHash)
                        return buffer[i].Prefab;
                }
            }
            return Entity.Null;
        }

        private Entity ResolveFromSingleton(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<EnemyPrefabSingleton>());
            if (query.CalculateEntityCount() == 0) return Entity.Null;
            return query.GetSingleton<EnemyPrefabSingleton>().Value;
        }
    }
}

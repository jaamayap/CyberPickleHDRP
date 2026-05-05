// File: Assets/_CyberPickle/Code/DOTS/Spawning/WaveSpawner.cs
// Namespace: CyberPickle.DOTS.Spawning
//
// Runtime executor of a WavePlan. Lives in Game.unity. Each frame:
//
//   1. Advances run time.
//   2. For each active spawn directive (active = startTime <= now < endTime):
//      - Continuous: accumulate spawnsPerSecond * dt. While the
//        accumulator is >= 1, spawn one enemy and decrement.
//      - One-shot burst: spawn rate × duration enemies at startTime,
//        once. Tracked via a per-directive "fired" flag.
//   3. Each spawn places the enemy at a random angle around the player
//      at the directive's spawnRadius (slightly off-camera so the player
//      sees them walk in), with per-instance speed jitter.
//
// Replaces (or coexists with) EnemySwarmSpawner for production gameplay.
// EnemySwarmSpawner is fine for one-shot perf tests; this is for actual
// gameplay sessions.
//
// Looks up entity prefabs in EnemyPrefabRegistryAuthoring (chunk 5b).
// Uses the player position from PlayerPositionData (the same singleton
// that drives EnemyMovementSystem) for placement origin.

using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Enemies;
using CyberPickle.Gameplay.Waves;

namespace CyberPickle.DOTS.Spawning
{
    [DisallowMultipleComponent]
    public class WaveSpawner : MonoBehaviour
    {
        [Header("Plan")]
        [Tooltip("WavePlan SO defining the spawn schedule for the run.")]
        public WavePlan plan;

        [Header("Lifecycle")]
        [Tooltip("Delay before the run timer starts ticking. Lets the SubScene + DOTS world fully initialize.")]
        [Min(0f)] public float startDelay = 0.5f;

        [Tooltip("Pause spawning while true. Useful for menus, level-up screens, cutscenes.")]
        public bool paused = false;

        [Header("Diagnostics")]
        [Tooltip("Log a line each time a directive becomes active or deactivates.")]
        public bool logDirectiveTransitions = true;

        [Tooltip("Log every spawn (very noisy with high spawn rates — only useful for debugging).")]
        public bool logEverySpawn = false;

        // ─── Runtime state ──────────────────────────────────────────────────

        /// <summary>Seconds elapsed since the wave timer started.</summary>
        public float RunTime => runTime;

        private float startCountdown;
        private float runTime;
        private float[] accumulators;       // continuous-spawn fractional accumulators per directive
        private bool[] burstFired;          // one-shot directives: have we fired this iteration?
        private bool[] wasActive;           // for transition logging

        private World world;
        private EntityManager entityManager;
        private bool initialized;
        private Unity.Mathematics.Random rng;

        // Resolved prefab cache, keyed by EnemyData.GetIdHash() — avoids
        // re-querying the registry buffer for every spawn.
        private readonly Dictionary<int, Entity> resolvedPrefabs = new Dictionary<int, Entity>();

        private void OnEnable()
        {
            startCountdown = startDelay;
            runTime = 0f;
            initialized = false;
            resolvedPrefabs.Clear();

            if (plan != null && plan.directives != null)
            {
                accumulators = new float[plan.directives.Count];
                burstFired   = new bool[plan.directives.Count];
                wasActive    = new bool[plan.directives.Count];
            }

            uint seed = (uint)System.Environment.TickCount | 1u;
            rng = new Unity.Mathematics.Random(seed);
        }

        private void Update()
        {
            if (plan == null || plan.directives == null || plan.directives.Count == 0) return;
            if (paused) return;

            // Lazy-initialize the EntityManager reference on first Update so we
            // run after DOTS world bootstrap completes.
            if (!initialized)
            {
                world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return;
                entityManager = world.EntityManager;
                initialized = true;
            }

            float dt = Time.deltaTime;

            if (startCountdown > 0f)
            {
                startCountdown -= dt;
                return;
            }

            runTime += dt;

            // Loop the run timer if the plan loops.
            if (plan.loop && runTime >= plan.planDuration)
            {
                runTime -= plan.planDuration;
                System.Array.Clear(burstFired, 0, burstFired.Length);
                if (logDirectiveTransitions)
                    Debug.Log($"[WaveSpawner] Plan looped at t={runTime:F1}s.", this);
            }

            ProcessDirectives(dt);
        }

        // ─── Core loop ──────────────────────────────────────────────────────

        private void ProcessDirectives(float dt)
        {
            if (!TryGetPlayerPosition(out float3 playerPos)) return;

            for (int i = 0; i < plan.directives.Count; i++)
            {
                var d = plan.directives[i];
                if (d == null || d.enemyType == null) continue;

                bool active = runTime >= d.startTime && runTime < math.min(d.endTime, plan.planDuration);

                if (active && !wasActive[i] && logDirectiveTransitions)
                    Debug.Log($"[WaveSpawner] Directive '{d.label}' ({d.enemyType.enemyId}) ACTIVE at t={runTime:F1}s.", this);
                if (!active && wasActive[i] && logDirectiveTransitions)
                    Debug.Log($"[WaveSpawner] Directive '{d.label}' ({d.enemyType.enemyId}) DEACTIVATED at t={runTime:F1}s.", this);
                wasActive[i] = active;

                if (!active) continue;

                if (d.oneShotBurst)
                {
                    if (burstFired[i]) continue;

                    float windowDuration = math.max(0.1f, d.endTime - d.startTime);
                    int burstCount = math.max(1, (int)math.round(d.spawnsPerSecond * windowDuration));
                    SpawnMany(d, burstCount, playerPos);
                    burstFired[i] = true;
                }
                else
                {
                    accumulators[i] += d.spawnsPerSecond * dt;
                    while (accumulators[i] >= 1f)
                    {
                        SpawnOne(d, playerPos);
                        accumulators[i] -= 1f;
                    }
                }
            }
        }

        private void SpawnMany(SpawnDirective directive, int count, float3 playerPos)
        {
            for (int i = 0; i < count; i++) SpawnOne(directive, playerPos);
        }

        private void SpawnOne(SpawnDirective directive, float3 playerPos)
        {
            Entity prefabEntity = ResolvePrefab(directive.enemyType);
            if (prefabEntity == Entity.Null || !entityManager.Exists(prefabEntity))
            {
                if (logDirectiveTransitions)
                    Debug.LogWarning($"[WaveSpawner] Could not resolve prefab for '{directive.enemyType.enemyId}' — directive '{directive.label}' will not spawn.", this);
                return;
            }

            // Random angle around player + jittered radius. Y stays at player's
            // ground level — enemies spawn at floor height regardless of player Y.
            float angle = rng.NextFloat(0f, 2f * math.PI);
            float r = directive.spawnRadius + rng.NextFloat(-directive.radialJitter, directive.radialJitter);
            float3 spawnPos = new float3(
                playerPos.x + math.cos(angle) * r,
                playerPos.y,
                playerPos.z + math.sin(angle) * r);

            Entity instance = entityManager.Instantiate(prefabEntity);

            entityManager.SetComponentData(instance, LocalTransform.FromPositionRotationScale(
                spawnPos, quaternion.identity, 1f));

            if (directive.speedJitter > 0f && entityManager.HasComponent<MoveSpeed>(instance))
            {
                var baseSpeed = entityManager.GetComponentData<MoveSpeed>(instance).Value;
                float factor = 1f + rng.NextFloat(-directive.speedJitter, directive.speedJitter);
                entityManager.SetComponentData(instance, new MoveSpeed { Value = baseSpeed * factor });
            }

            if (logEverySpawn)
                Debug.Log($"[WaveSpawner] Spawned '{directive.enemyType.enemyId}' at {spawnPos}.", this);
        }

        // ─── Prefab resolution + player position ────────────────────────────

        private Entity ResolvePrefab(EnemyData data)
        {
            int hash = data.GetIdHash();
            if (resolvedPrefabs.TryGetValue(hash, out var cached)) return cached;

            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyPrefabBufferElement>());
            if (query.CalculateEntityCount() == 0) return Entity.Null;

            using var registries = query.ToEntityArray(Allocator.Temp);
            for (int r = 0; r < registries.Length; r++)
            {
                var buffer = entityManager.GetBuffer<EnemyPrefabBufferElement>(registries[r], isReadOnly: true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Hash == hash)
                    {
                        resolvedPrefabs[hash] = buffer[i].Prefab;
                        return buffer[i].Prefab;
                    }
                }
            }
            return Entity.Null;
        }

        private bool TryGetPlayerPosition(out float3 pos)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerPositionData>());
            if (query.CalculateEntityCount() == 0)
            {
                pos = float3.zero;
                return false;
            }
            pos = query.GetSingleton<PlayerPositionData>().Position;
            return true;
        }
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyVisualBindingSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Hybrid GameObject <-> Entity bridge driver. Each frame:
//
//   1) For every entity with VisualPrefabRef but NO HasVisualTag,
//      Instantiate the visual prefab (Animator-bearing GameObject),
//      Register it with EnemyVisualBridge, and add HasVisualTag.
//
//   2) For every entry currently in the bridge, copy the entity's
//      LocalTransform onto the visual's Transform (position + rotation).
//
//   3) For every bridge entry whose entity no longer exists in the
//      world (or no longer carries EnemyTag), Destroy the visual and
//      Unregister.
//
// SystemBase (managed) — GameObject.Instantiate / Transform.SetPositionAndRotation
// are not Burst-friendly. The cost is acceptable: ~0.1 ms for 100 visuals,
// ~1 ms for 1000. If transform sync becomes a bottleneck, that hot path
// can move to TransformAccessArray + IJobParallelForTransform — same
// dictionary, different scheduling.
//
// Update group: PresentationSystemGroup, so the position read here is
// the post-movement (post-SimulationSystemGroup) value of the frame.

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Bridge;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EnemyVisualBindingSystem : SystemBase
    {
        // Cached Animator parameter hash. Set once on the new visual at spawn
        // so the controller's transition conditions (Walk/Run/Death branches)
        // know which character archetype this is.
        private static readonly int EnemyTypeHash = UnityEngine.Animator.StringToHash("EnemyType");

        private EntityQuery newEnemiesQuery;

        protected override void OnCreate()
        {
            newEnemiesQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, VisualPrefabRef, LocalTransform>()
                .WithNone<HasVisualTag>()
                .Build();
        }

        protected override void OnUpdate()
        {
            var bridge = EnemyVisualBridge.Instance;
            if (bridge == null) return;

            var em = EntityManager;

            // ─── 1. Spawn visuals for new enemies ───
            if (!newEnemiesQuery.IsEmpty)
            {
                using var entities = newEnemiesQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var entity = entities[i];
                    var prefabRef = em.GetComponentData<VisualPrefabRef>(entity);
                    var prefab = prefabRef.Value.Value; // UnityObjectRef -> GameObject
                    if (prefab == null)
                    {
                        // Mark as visual-bound anyway so we don't retry every frame.
                        em.AddComponent<HasVisualTag>(entity);
                        continue;
                    }

                    var transform = em.GetComponentData<LocalTransform>(entity);
                    GameObject visual = Object.Instantiate(prefab, transform.Position, transform.Rotation);
                    visual.name = $"{prefab.name}_Visual_{entity.Index}";

                    // Configure the visual's Animator EnemyType parameter from
                    // the entity's baked visual classification, so the shared
                    // controller's transitions (Mutant Walk/Run vs Zombie Walk/Run)
                    // route to the right animation states.
                    if (em.HasComponent<EnemyVisualTypeId>(entity))
                    {
                        var animator = visual.GetComponent<UnityEngine.Animator>();
                        if (animator != null)
                        {
                            int visualType = em.GetComponentData<EnemyVisualTypeId>(entity).Value;
                            animator.SetInteger(EnemyTypeHash, visualType);
                        }
                    }

                    bridge.Register(entity, visual);
                    em.AddComponent<HasVisualTag>(entity);
                }
            }

            // ─── 2. Sync transforms + ─── 3. Detect stale entries ───
            // We iterate the bridge dictionary so we sync and find dead entries
            // in a single pass. Two-phase: collect stale keys first, mutate after.
            var stale = new NativeList<Entity>(64, Allocator.Temp);
            foreach (var kv in bridge.Visuals)
            {
                Entity entity = kv.Key;
                Transform visual = kv.Value;

                // Visual was destroyed externally, or entity was destroyed,
                // or entity lost its EnemyTag (e.g., re-archetyped).
                if (visual == null || !em.Exists(entity) || !em.HasComponent<EnemyTag>(entity))
                {
                    stale.Add(entity);
                    continue;
                }

                var transform = em.GetComponentData<LocalTransform>(entity);
                visual.SetPositionAndRotation(transform.Position, transform.Rotation);
            }

            for (int i = 0; i < stale.Length; i++)
            {
                if (bridge.Unregister(stale[i], out var orphan) && orphan != null)
                {
                    Object.Destroy(orphan.gameObject);
                }
            }
            stale.Dispose();
        }
    }
}

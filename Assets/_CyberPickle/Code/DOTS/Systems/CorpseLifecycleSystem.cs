// File: Assets/_CyberPickle/Code/DOTS/Systems/CorpseLifecycleSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Drives the post-death timeline for every entity carrying CorpseLifecycle.
// Two transitions happen per corpse:
//
//   1. At t = DeathTime + DelayBeforeDissolve:
//      - Look up the bound visual via EnemyVisualBridge.
//      - Call CorpseDissolveDriver.StartDissolve(DissolveDuration) on it.
//      - Mark DissolveSignaled=true so we don't re-fire.
//      - Neutralize physics so the corpse stops being simulated by Unity
//        Physics during the dissolve (zero velocity, zero gravity factor,
//        zero inverse mass — body stays put).
//
//   2. At t = DeathTime + DelayBeforeDissolve + DissolveDuration:
//      - Destroy the entity. The bridge's stale-entry cleanup
//        (in EnemyVisualBindingSystem) detects the missing entity next
//        frame and destroys the visual GameObject.
//
// SystemBase (managed) — needs Animator / GameObject / dictionary access
// for the visual signal step. The work is bounded to corpses transitioning
// THIS frame, which is small.

using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using CyberPickle.DOTS.Bridge;
using CyberPickle.DOTS.Components;
using CyberPickle.DOTS.Visual;

namespace CyberPickle.DOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class CorpseLifecycleSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<CorpseLifecycle>();
        }

        protected override void OnUpdate()
        {
            var bridge = EnemyVisualBridge.Instance;
            var em = EntityManager;
            double now = SystemAPI.Time.ElapsedTime;

            using var toSignal = new NativeList<Entity>(32, Allocator.Temp);
            using var toDestroy = new NativeList<Entity>(32, Allocator.Temp);

            foreach (var (lifecycle, entity) in
                     SystemAPI.Query<RefRW<CorpseLifecycle>>().WithEntityAccess())
            {
                float elapsed = (float)(now - lifecycle.ValueRO.DeathTime);

                // Phase 1: signal dissolve once when delay expires.
                if (!lifecycle.ValueRO.DissolveSignaled
                    && elapsed >= lifecycle.ValueRO.DelayBeforeDissolve)
                {
                    toSignal.Add(entity);
                    lifecycle.ValueRW.DissolveSignaled = true;
                }

                // Phase 2: destroy entity at end of dissolve.
                float endTime = lifecycle.ValueRO.DelayBeforeDissolve
                              + lifecycle.ValueRO.DissolveDuration;
                if (elapsed >= endTime)
                {
                    toDestroy.Add(entity);
                }
            }

            // ─── Phase 1 — signal visuals ──────────────────────────────────
            for (int i = 0; i < toSignal.Length; i++)
            {
                var entity = toSignal[i];

                // Tell the visual to start its dissolve animation.
                if (bridge != null
                    && bridge.TryGet(entity, out var visualTransform)
                    && visualTransform != null)
                {
                    var driver = visualTransform.GetComponent<CorpseDissolveDriver>();
                    if (driver != null)
                    {
                        var lifecycle = em.GetComponentData<CorpseLifecycle>(entity);
                        driver.StartDissolve(lifecycle.DissolveDuration);
                    }
                }

                // Neutralize physics for the corpse so it stops simulating.
                // Cheaper than a full broadphase for hundreds of corpses.
                if (em.HasComponent<PhysicsVelocity>(entity))
                {
                    em.SetComponentData(entity, new PhysicsVelocity
                    {
                        Linear = Unity.Mathematics.float3.zero,
                        Angular = Unity.Mathematics.float3.zero,
                    });
                }
                if (em.HasComponent<PhysicsGravityFactor>(entity))
                {
                    em.SetComponentData(entity, new PhysicsGravityFactor { Value = 0f });
                }
            }

            // ─── Phase 2 — destroy entity ──────────────────────────────────
            // Direct destruction (not deferred via ECB) so the bridge's stale-entry
            // cleanup picks them up on the very next frame.
            for (int i = 0; i < toDestroy.Length; i++)
            {
                em.DestroyEntity(toDestroy[i]);
            }
        }
    }
}

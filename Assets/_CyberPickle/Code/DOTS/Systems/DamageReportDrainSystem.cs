// File: Assets/_CyberPickle/Code/DOTS/Systems/DamageReportDrainSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Managed-side consumer of the Burst-side damage report queue. Owns the
// queue's lifecycle (creation / disposal) and drains it once per frame,
// dispatching each report to PerWeaponStatsTracker.
//
// Why a managed SystemBase: we need to call into managed PerWeaponStatsTracker
// (a Manager<T> singleton) which can't be touched from Burst. SystemBase
// runs on the main thread, so it's the right side to do this dispatch.
//
// Update order: should run AFTER ProjectileCollisionSystem so the queue
// is fully populated before we drain. Enforced via [UpdateAfter].

using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Combat;
using CyberPickle.Gameplay.Weapons;

namespace CyberPickle.DOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileCollisionSystem))]
    public partial class DamageReportDrainSystem : SystemBase
    {
        protected override void OnCreate()
        {
            // Allocate the persistent queue + register the singleton entity.
            // Allocator.Persistent because the queue lives for the World's
            // lifetime, not just one frame.
            var queue = new NativeQueue<DamageHitReport>(Allocator.Persistent);
            var entity = EntityManager.CreateEntity(typeof(DamageReportQueueSingleton));
            EntityManager.SetName(entity, "DamageReportQueueSingleton");
            EntityManager.SetComponentData(entity, new DamageReportQueueSingleton { Queue = queue });
        }

        protected override void OnDestroy()
        {
            // Dispose the queue. Without this we leak the native allocation
            // every domain reload — Unity will yell about it in the editor.
            if (SystemAPI.HasSingleton<DamageReportQueueSingleton>())
            {
                var qs = SystemAPI.GetSingleton<DamageReportQueueSingleton>();
                if (qs.Queue.IsCreated)
                    qs.Queue.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            // Drain regardless of run state — even if we're paused, late
            // hits queued just before pause should still flush. Otherwise
            // the queue would back up across the level-up screen and
            // eventually appear in mass when the run resumes.
            if (!SystemAPI.HasSingleton<DamageReportQueueSingleton>()) return;

            var qs = SystemAPI.GetSingleton<DamageReportQueueSingleton>();
            var queue = qs.Queue;
            if (!queue.IsCreated) return;

            var tracker = PerWeaponStatsTracker.Instance;
            while (queue.TryDequeue(out var report))
            {
                // Tracker may not exist yet on the very first frame (Manager<T>
                // Awake order), in which case we drop the report rather than
                // backing up the queue. Subsequent reports will land normally
                // once the tracker is alive.
                //
                // Skip 0-damage reports for stats — those are visual-only
                // markers (e.g., the central explosion VFX trigger that
                // ProjectileExplosionSystem emits at the AoE epicenter, with
                // DamageDealt=0 so it doesn't double-count actual per-enemy
                // damage). Without this skip, every grenade explosion would
                // inflate the hit count by +1 against the actual damaged
                // enemies.
                if (report.DamageDealt > 0f)
                    tracker?.RecordHit(report);

                // M9 PR F: spawn the Mono-side hit VFX. ElementVfxLibrary
                // picks the right per-element prefab; HitVfxApplier scales
                // particles by damage / crit / weapon scale / AoE; tints by
                // element + crit; orients the burst along the projectile's
                // travel direction. Silent if the library or prefab is missing.
                //
                // SKIP this path when the projectile carried
                // ProjectileHasHybridVisual — Hovl's authored hit GO is
                // already spawning via CyberPickleProjectileVisual.OnHit on
                // the LateSimulation tick. Running both produces two hit
                // visuals at slightly offset positions ("weird behavior"
                // pre-fix). The hybrid path takes precedence.
                if (!report.SuppressDefaultHitVfx)
                {
                    ElementId element = (ElementId)report.Element;
                    HitVfxApplier.Play(
                        weaponId:     report.WeaponId.ToString(),
                        element:      element,
                        hitPosition:  new Vector3(report.HitPosition.x, report.HitPosition.y, report.HitPosition.z),
                        hitDirection: new Vector3(report.HitDirection.x, report.HitDirection.y, report.HitDirection.z),
                        damageDealt:  report.DamageDealt,
                        isCrit:       report.IsCrit);
                }
            }
        }
    }
}

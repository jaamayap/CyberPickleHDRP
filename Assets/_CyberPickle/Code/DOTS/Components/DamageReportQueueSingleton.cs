// File: Assets/_CyberPickle/Code/DOTS/Components/DamageReportQueueSingleton.cs
// Namespace: CyberPickle.DOTS.Components
//
// Singleton component holding the per-frame queue of damage hits emitted
// by Burst-side combat systems and consumed by the managed-side stats
// tracker. The queue lives across frames; producers (Burst) Enqueue,
// consumer (DamageReportDrainSystem, managed SystemBase) drains every
// frame so the queue size stays bounded.
//
// Why this pattern: ProjectileCollisionSystem is BurstCompiled and
// cannot call into managed code (PerWeaponStatsTracker / MusicEventBus).
// A NativeQueue is Burst-safe to write to. A managed system reads from
// the same queue and dispatches to managed listeners. This is the
// canonical DOTS bridge.
//
// Lifecycle: created by DamageReportDrainSystem.OnCreate, disposed in
// OnDestroy. The NativeQueue is allocated with Allocator.Persistent
// because it lives for the lifetime of the World.

using Unity.Collections;
using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct DamageReportQueueSingleton : IComponentData
    {
        /// <summary>
        /// Persistent queue. Burst producers enqueue hits; the drain system
        /// dequeues each frame. Created and disposed by DamageReportDrainSystem.
        /// </summary>
        public NativeQueue<DamageHitReport> Queue;
    }
}

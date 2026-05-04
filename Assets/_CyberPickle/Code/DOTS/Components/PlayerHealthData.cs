// File: Assets/_CyberPickle/Code/DOTS/Components/PlayerHealthData.cs
// Namespace: CyberPickle.DOTS.Components
//
// Singleton mirroring the player's health state for ECS-side reads, plus
// a damage accumulator that ECS systems write to and PlayerHealthBridge
// drains each frame.
//
// Read-side (Burst-friendly):
//   - CurrentHealth, MaxHealth, IsAlive — read by enemy AI (avoid dead
//     player), HUD systems, projectile systems for hit feedback, etc.
//
// Write-side (single accumulator, no atomic concerns):
//   - PendingDamage — ECS damage sources (EnemyContactDamageSystem,
//     future enemy-projectile system) ADD to this each frame. The bridge
//     drains + zeros it each frame and forwards to PlayerHealth.TakeDamage.
//   - Concurrent ECS writes to a singleton field are SAFE because all
//     ECS systems run on the main thread by default; only Job code with
//     parallel writes would need atomics, and we don't do that here.
//
// PlayerHealthBridge writes CurrentHealth / MaxHealth / IsAlive only
// when they change (event-driven dirty bit), avoiding per-frame
// SetComponentData calls when the player is idle.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct PlayerHealthData : IComponentData
    {
        /// <summary>Current health value. Mirrored from PlayerHealth.CurrentHealth.</summary>
        public float CurrentHealth;

        /// <summary>Max health value. Mirrored from PlayerHealth.MaxHealth (which reads PlayerStats.Get(MaxHealth)).</summary>
        public float MaxHealth;

        /// <summary>True while the player is alive. False once health reaches 0 — gameplay systems should stop targeting / attacking the player.</summary>
        public bool IsAlive;

        /// <summary>
        /// Damage queued by ECS systems this frame, drained + applied by
        /// PlayerHealthBridge. Multiple ECS systems can += into this field
        /// (main-thread, single-frame) without coordination.
        /// </summary>
        public float PendingDamage;
    }
}

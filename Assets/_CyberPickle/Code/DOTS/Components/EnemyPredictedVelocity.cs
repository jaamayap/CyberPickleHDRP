// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyPredictedVelocity.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-enemy "where will I be heading next" vector. Written by whichever
// AI / motion system is currently driving the enemy:
//
//   • SeekPlayer (current default) — EnemyMovementSystem writes the
//     toward-player direction × move speed each frame.
//   • EnemyAntiStuckSystem — when it rotates a stuck enemy's velocity
//     out of a wedge, it updates this prediction so the new direction
//     is reflected.
//   • Future pathfinding / ranged / charging AIs — each writes the
//     direction it intends to send the enemy next.
//
// Consumed by:
//   • WeaponFiring (parabolic launches) — leads the target by
//     EnemyPredictedVelocity × flightTime so grenades land where
//     the enemy WILL be when they arrive, not where the enemy WAS
//     when the launch was computed.
//   • Future homing / predictive HUD / leading bullets — same contract.
//
// Why a single component instead of per-AI prediction methods:
//   • Burst-friendly (single component lookup, no virtual dispatch).
//   • Decouples weapons from AI types — adding a new AI type doesn't
//     require touching weapons code, just writing this component.
//   • Single source of truth — anti-stuck and movement both update
//     the same field, so weapons always read the most recent intent.
//
// The vector is the FULL velocity (m/s), NOT a normalized direction.
// Weapons multiply by their own flight time to compute the lead offset.
// Y is typically zero (XZ pursuit) but the contract doesn't forbid
// non-zero Y — future jumping enemies could write a vertical component
// for vertical lead.

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyPredictedVelocity : IComponentData
    {
        /// <summary>Predicted velocity (world-space, m/s). Multiply by flight time to get lead offset.</summary>
        public float3 Value;
    }
}

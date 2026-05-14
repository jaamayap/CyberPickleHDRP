// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyStuckTracker.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-enemy state for EnemyAntiStuckSystem. Tracks the previous frame's
// position so the system can detect "no actual translation" frames —
// the symptom of a Unity Physics body that has entered sleep state and
// is no longer responding to our PhysicsVelocity writes.
//
// Added lazily by EnemyAntiStuckSystem on first encounter (any enemy
// without this component). No bake-time authoring needed — keeps the
// EnemyAuthoring SO free of system-implementation noise.

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyStuckTracker : IComponentData
    {
        /// <summary>The entity's LocalTransform.Position last frame. Compared to current to detect "didn't move" frames.</summary>
        public float3 LastPosition;

        /// <summary>Accumulated seconds the entity has been "stuck" (not translating despite the movement system writing velocity). When this exceeds the system's threshold, the unstick kick fires.</summary>
        public float StuckSeconds;

        /// <summary>Toggle bit so the unstick kick alternates direction (CW / CCW), preventing the same enemy from oscillating into the same wedge twice in a row.</summary>
        public byte KickDirection;
    }
}

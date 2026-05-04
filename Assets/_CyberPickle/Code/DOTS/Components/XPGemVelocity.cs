// File: Assets/_CyberPickle/Code/DOTS/Components/XPGemVelocity.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-gem accumulated motion vector. The magnet system smoothly lerps this
// toward (direction-to-player × maxPullSpeed) when the gem is in range,
// then integrates position from it. Gives the satisfying "magnet ramp-up"
// feel — gems accelerate toward the player rather than instantly snapping.

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct XPGemVelocity : IComponentData
    {
        public float3 Value;
    }
}

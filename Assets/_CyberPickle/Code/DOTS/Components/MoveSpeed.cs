// File: Assets/_CyberPickle/Code/DOTS/Components/MoveSpeed.cs
// Namespace: CyberPickle.DOTS.Components
//
// Movement speed in world units per second. Used by EnemyMovementSystem
// (and any other locomotion system) to translate the entity each tick.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }
}

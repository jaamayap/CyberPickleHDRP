// File: Assets/_CyberPickle/Code/DOTS/Components/Health.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-entity health. Read by damage systems (later); decremented when
// entities take damage; entities are despawned when Current <= 0.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct Health : IComponentData
    {
        public float Current;
        public float Max;
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Components/Lifetime.cs
// Namespace: CyberPickle.DOTS.Components
//
// Generic "self-destruct after N seconds" component. Used by projectiles,
// hit VFX, muzzle flashes, and any other transient entity. The
// LifetimeSystem decrements Remaining each tick and destroys the entity
// when it reaches zero.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct Lifetime : IComponentData
    {
        public float Remaining;
    }
}

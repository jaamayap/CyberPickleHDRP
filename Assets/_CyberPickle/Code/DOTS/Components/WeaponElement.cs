// File: Assets/_CyberPickle/Code/DOTS/Components/WeaponElement.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-projectile element tag stamped at fire time by WeaponFiring (mirrors
// the weapon's currently-coupled ElementId at the moment the projectile
// spawned). Burst-readable; used by ProjectileCollisionSystem to forward
// the element through the hit report so the Mono-side HitVfxApplier can
// pick the right element-tinted hit prefab from ElementVfxLibrary.
//
// Why stamp at fire time (not look up at hit time): the weapon's element
// can change mid-flight if a power-up is added/removed/levelled during
// the ~1s projectile lifetime. The projectile should detonate with the
// element it was FIRED with, not the element the weapon currently has.
//
// Underlying byte mirrors CyberPickle.Core.ElementId (None=0, Fire=1, ...,
// Dark=7) for compactness — same encoding used everywhere else (ECS
// components, network payloads, save data).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct WeaponElement : IComponentData
    {
        /// <summary>The element this projectile carries. Maps to <c>CyberPickle.Core.ElementId</c>.</summary>
        public byte Value;
    }
}

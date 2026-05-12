// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileDying.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker + timer for a projectile that has hit its final target and is
// now playing out its death animation (trail-linger). Added by
// ProjectileCollisionSystem at kill-time; consumed by ProjectileFadeOutSystem
// which stops particle emission on the Companion GameObject and destroys
// the entity when the timer expires.
//
// Lifecycle:
//   - frame 0 (collision frame): ProjectileTag REMOVED, ProjectilePierce
//                                 cleared, ProjectileVelocity zeroed,
//                                 ProjectileDying ADDED with TimeRemaining
//                                 = trailLingerSeconds (from WeaponData).
//   - frame 0+: ProjectileFadeOutSystem detects ProjectileDying + new flag,
//                stops emission on every ParticleSystem in the visual
//                hierarchy (existing particles complete their lifetime
//                naturally — the trail fades instead of pop-vanishing).
//   - each tick: TimeRemaining -= dt.
//   - TimeRemaining <= 0: DestroyEntity. The Companion GameObject (and
//                          its now-faded particles) goes with it.
//
// Without this state, projectile destruction was instant and looked
// "broken" (Hovl-style trails got cut mid-flight). The fade gives the
// hit-VFX time to read AND the bullet's own trail time to dissipate.

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileDying : IComponentData
    {
        /// <summary>Seconds left before the entity is destroyed. Decremented by ProjectileFadeOutSystem each tick.</summary>
        public float TimeRemaining;

        /// <summary>Set true by ProjectileFadeOutSystem on its first encounter with this entity so the "stop emission" pass only runs once. ECS-managed, do not set externally.</summary>
        public byte EmissionStoppedFlag;

        /// <summary>World position of the killing hit — where the projectile's <c>OnHit</c> visual should be positioned. Set by ProjectileCollisionSystem at kill time, read once by ProjectileFadeOutSystem.</summary>
        public float3 ContactPosition;

        /// <summary>"Out of the surface" direction used to orient the hit VFX. We don't have a real surface normal (proximity collision, not Mono contacts), so we pass the bullet's reversed velocity as a stand-in — points back at the shooter, which is what most impact-spark / splash patterns want to face.</summary>
        public float3 ContactNormal;
    }
}

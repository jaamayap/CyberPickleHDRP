// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileHasHybridVisual.cs
// Namespace: CyberPickle.DOTS.Components
//
// Tag component stamped at fire time by WeaponFiring when the spawned
// projectile entity's Companion GameObject carries a
// CyberPickleProjectileVisual MonoBehaviour. Read by
// ProjectileCollisionSystem to set DamageHitReport.SuppressDefaultHitVfx
// — DamageReportDrainSystem then skips the parallel HitVfxApplier.Play
// path so the Hovl-authored hit GO is the ONLY hit visual, no double-up.
//
// When absent: HitVfxApplier.Play runs as before (legacy / non-hybrid
// projectiles still get the ElementVfxLibrary.hitPrefab visual).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileHasHybridVisual : IComponentData
    {
        // Tag-only — no data. Presence of the component is the signal.
    }
}

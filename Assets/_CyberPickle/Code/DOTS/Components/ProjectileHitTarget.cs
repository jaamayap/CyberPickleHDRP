// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileHitTarget.cs
// Namespace: CyberPickle.DOTS.Components
//
// Dynamic buffer storing enemy entities already hit by this projectile.
// Used by pierce projectiles to dedup repeat hits — without this, a
// projectile passing through an enemy's hit radius could re-register hits
// across multiple frames (or while moving slowly through the radius),
// burning pierce counts on the same enemy.
//
// Only present on pierce-capable projectiles (pierce > 0 at fire time).
// Non-pierce projectiles destroy on first hit, so they never accumulate
// hit history — no buffer needed.
//
// Burst-friendly; ProjectileCollisionSystem reads via BufferLookup and
// appends new hits via EntityCommandBuffer.AppendToBuffer (deferred to
// frame-end playback, but within-frame dedup is automatic because the
// inner loop visits each enemy exactly once per projectile per frame).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileHitTarget : IBufferElementData
    {
        /// <summary>Enemy entity already hit by this projectile. Used by ProjectileCollisionSystem to skip re-hits.</summary>
        public Entity Value;
    }
}

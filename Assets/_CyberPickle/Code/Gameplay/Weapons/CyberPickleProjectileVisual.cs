// File: Assets/_CyberPickle/Code/Gameplay/Weapons/CyberPickleProjectileVisual.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Visual-only Hovl-equivalent. Replaces HS_ProjectileMover on each
// projectile prefab. Field names MATCH HS_ProjectileMover's so a
// component swap preserves all per-prefab inspector references (the
// `hit` GO, `hitPS`, `flash`, `projectilePS`, `Detached[]`, `lightSource`,
// etc. — all of Hovl's hand-tuned authoring carries through).
//
// What this script does NOT do (and HS_ProjectileMover did):
//   - Move the projectile (no Rigidbody, no FixedUpdate velocity drive).
//     ECS owns motion via ProjectileMovementSystem.
//   - Detect collisions (no Mono collider, no OnCollisionEnter).
//     ECS owns collision via ProjectileCollisionSystem.
//   - Self-destroy (no Destroy(gameObject, ...)).
//     ECS owns destruction via ProjectileDying + ProjectileFadeOutSystem.
//
// What this script DOES do (mirrors HS_ProjectileMover's visual-cleanup
// half of OnCollisionEnter):
//   - Awake: detach the flash GO from this prefab so the muzzle flash
//     stays at the muzzle while the bullet flies forward.
//   - OnHit(contactPoint, contactNormal): called by the ECS bridge
//     (ProjectileFadeOutSystem) at the killing-hit moment. Stops + clears
//     the main projectile particles, disables the light, positions the
//     authored hit GO at the contact point with the authored rotation
//     mode, plays its hitPS, and stops emission on every Detached[]
//     trail PS so trails fade naturally.
//
// Idempotent — safe to call OnHit multiple times (defensive against any
// ECS double-fire). The destroy timer runs ECS-side so the entity stays
// alive long enough for hitPS to play out + trails to fade.

using UnityEngine;

namespace CyberPickle.Gameplay.Weapons
{
    [DisallowMultipleComponent]
    public class CyberPickleProjectileVisual : MonoBehaviour
    {
        // Field names match HS_ProjectileMover so swapping the script
        // component preserves inspector assignments via name-based
        // serialization. DO NOT rename without coordinating a migration.

        [Header("Hit Effect")]
        [Tooltip("Hit GameObject (typically a child of this prefab). On hit, its transform is moved to the contact point + rotated per the authored mode, and its hitPS is .Play()ed.")]
        [SerializeField] private GameObject hit;

        [Tooltip("Particle system on the hit GO that fires the impact visuals. .Play() is called on hit.")]
        [SerializeField] private ParticleSystem hitPS;

        [Tooltip("Offset (along the contact normal) applied to the hit GO's position so the impact reads PROUD of the surface, not embedded. Matches Hovl's hitOffset.")]
        [SerializeField] private float hitOffset = 0f;

        [Tooltip("If true, the hit VFX rotation uses this prefab's transform rotation (rotated 180° on Y) — directional impact (e.g., sparks shooting back at the shooter).")]
        [SerializeField] private bool UseFirePointRotation;

        [Tooltip("Constant Euler rotation override for the hit VFX. Only applied when non-zero AND UseFirePointRotation is false. Fallback when neither is set: LookAt(contact + normal).")]
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Muzzle Flash (optional)")]
        [Tooltip("Flash GO. Auto-detached from this prefab's hierarchy on Awake so the flash stays at the muzzle position while the bullet flies forward. Leave null if your weapon's muzzle flash is spawned separately (via ElementVfxLibrary).")]
        [SerializeField] private GameObject flash;

        [Header("Light + Bullet Particles")]
        [Tooltip("Optional Light component on the projectile. Disabled instantly on hit so the bullet stops illuminating during fade-out. Matches Hovl's lightSource.enabled = false.")]
        [SerializeField] private Light lightSource;

        [Tooltip("Main projectile particle system (head / glow). Stop+Clear on hit — the bullet's head visual vanishes instantly (no static cloud at the freeze position).")]
        [SerializeField] private ParticleSystem projectilePS;

        [Header("Trail particles")]
        [Tooltip("Trail / detached particle systems. Each GO should carry a ParticleSystem. On hit, each is Stop()ed (no Clear) so existing trail particles complete their startLifetime — the trail fades smoothly. Matches Hovl's Detached[] iteration.")]
        [SerializeField] private GameObject[] Detached;

        // ─── Runtime state ───────────────────────────────────────────────

        private bool _hitFired;

        private void Awake()
        {
            // Detach the flash so it stays at the muzzle position even as
            // the bullet flies away. Hovl does this in Start(); we do it in
            // Awake() for symmetric timing with ECS Instantiate.
            if (flash != null)
            {
                flash.transform.parent = null;
            }
        }

        /// <summary>
        /// Called by ECS bridge (ProjectileFadeOutSystem) at the killing-hit
        /// moment. Mirrors HS_ProjectileMover.OnCollisionEnter's visual
        /// cleanup logic without the motion/rigidbody/destroy parts.
        ///
        /// <paramref name="contactPoint"/> is the world position to spawn
        /// the hit VFX at (typically the enemy's position).
        /// <paramref name="contactNormal"/> is the "out of the surface"
        /// direction used to orient the hit VFX — ECS passes the bullet's
        /// reversed velocity as a stand-in for a real surface normal in our
        /// proximity-based collision model (no real Mono contact data).
        ///
        /// Idempotent — repeated calls are safely ignored. ECS shouldn't
        /// call this more than once per kill, but defensive.
        ///
        /// SELF-SUFFICIENT vs. Hovl: Hovl-authored prefabs often leave
        /// `lightSource` null and have ParticleSystems that aren't wired
        /// into `Detached[]` or `projectilePS`. Hovl masked this by
        /// destroying the bullet ~1s after hit — the unwired components
        /// died with the GO. We keep the entity alive for the fade-out
        /// window, so we have to explicitly shut down EVERY Light and PS
        /// in the prefab on hit (except those under `hit`, which provide
        /// the impact visuals). This makes the script robust against
        /// inconsistent prefab wiring.
        /// </summary>
        public void OnHit(Vector3 contactPoint, Vector3 contactNormal)
        {
            if (_hitFired) return;
            _hitFired = true;

            // ─── Light cleanup ──────────────────────────────────────────
            // Explicit reference first (cheap, well-defined).
            if (lightSource != null) lightSource.enabled = false;

            // Then defensively disable ANY other Light components in
            // children — Hovl prefabs often have an HDR Light at intensity
            // 40+ that's not wired to lightSource. Without this, a frozen
            // bullet keeps blasting bright light at the contact point for
            // the entire fade-out duration. Skip any Light parented to the
            // `hit` GO so the impact burst can still illuminate the scene.
            var allLights = GetComponentsInChildren<Light>(includeInactive: false);
            for (int i = 0; i < allLights.Length; i++)
            {
                var lt = allLights[i];
                if (lt == null) continue;
                if (hit != null && lt.transform.IsChildOf(hit.transform)) continue;
                lt.enabled = false;
            }

            // ─── Particle cleanup ───────────────────────────────────────
            // Main projectile particles → CLEAR (head vanishes instantly,
            // no static cloud at the freeze position).
            if (projectilePS != null)
                projectilePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Every other PS in the prefab (except those under `hit` and
            // the explicit projectilePS already handled above) gets a soft
            // Stop — emission stops but existing particles complete their
            // startLifetime, giving trails / glow halos a natural fade.
            // This catches loop-emitters not wired into Detached[].
            var allPS = GetComponentsInChildren<ParticleSystem>(includeInactive: false);
            for (int i = 0; i < allPS.Length; i++)
            {
                var ps = allPS[i];
                if (ps == null) continue;
                if (ps == hitPS) continue;                                         // impact burst — needs to play
                if (ps == projectilePS) continue;                                  // already cleared above
                if (hit != null && ps.transform.IsChildOf(hit.transform)) continue; // anything under hit GO
                ps.Stop(); // StopEmitting (default) — existing particles complete
            }

            // Detached trails → same soft Stop pattern (no-op if already
            // stopped by the loop above, but kept for explicit clarity and
            // safety on prefabs where Detached entries are parents of
            // ParticleSystem children rather than carrying one themselves).
            if (Detached != null)
            {
                for (int i = 0; i < Detached.Length; i++)
                {
                    if (Detached[i] == null) continue;
                    var ps = Detached[i].GetComponent<ParticleSystem>();
                    if (ps != null) ps.Stop();
                }
            }

            // ─── Position + play the hit VFX (unchanged) ────────────────
            // Per the authored rotation mode — this is what makes the
            // impact feel connected to the bullet at the exact point the
            // designer tuned for this projectile.
            if (hit != null)
            {
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, contactNormal);
                Vector3 pos = contactPoint + contactNormal * hitOffset;
                hit.transform.rotation = rot;
                hit.transform.position = pos;

                if (UseFirePointRotation)
                {
                    hit.transform.rotation = transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                }
                else if (rotationOffset != Vector3.zero)
                {
                    hit.transform.rotation = Quaternion.Euler(rotationOffset);
                }
                else
                {
                    hit.transform.LookAt(contactPoint + contactNormal);
                }

                if (hitPS != null) hitPS.Play();
            }
        }

        /// <summary>
        /// Total time the entity should stay alive after a kill so that:
        ///   • The Hovl-authored hit VFX (hitPS) gets to play out fully.
        ///   • The Detached[] trail particles complete their startLifetime
        ///     (since we Stop()'d them with no Clear, existing particles
        ///     fade naturally over that duration).
        ///
        /// Read by ProjectileFadeOutSystem on the first frame of the
        /// projectile's dying state — it sizes the destroy timer so the
        /// Companion GO survives long enough for all this to complete.
        ///
        /// Each PREFAB knows its own correct timing because Hovl's
        /// authors tuned the particle systems. WeaponData no longer
        /// carries a per-weapon `trailLingerSeconds` — that was the wrong
        /// layer (a weapon can fire many element-coupled projectiles,
        /// each with different particle timings).
        ///
        /// Falls back to 1.0s if no particle data is available (defensive).
        /// </summary>
        public float GetTotalFadeDuration()
        {
            float maxDuration = 0f;

            // Hit VFX duration: hitPS plays out at OnHit. Live at least
            // through hitPS.duration + the longest particle's startLifetime
            // so the impact burst isn't cut off.
            if (hitPS != null)
            {
                var main = hitPS.main;
                maxDuration = Mathf.Max(maxDuration, main.duration + main.startLifetime.constantMax);
            }
            // Plus any sub-PS nested under the hit GO.
            if (hit != null)
            {
                var nested = hit.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
                for (int i = 0; i < nested.Length; i++)
                {
                    var m = nested[i].main;
                    float d = m.duration + m.startLifetime.constantMax;
                    if (d > maxDuration) maxDuration = d;
                }
            }

            // EVERY non-hit child PS — after the soft Stop in OnHit, their
            // in-flight particles complete startLifetime. The entity has
            // to live at least that long for trails / glow halos to fade
            // smoothly. Catches loop-emitters not wired into Detached[].
            var allPS = GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < allPS.Length; i++)
            {
                var ps = allPS[i];
                if (ps == null) continue;
                if (ps == hitPS) continue;
                if (hit != null && ps.transform.IsChildOf(hit.transform)) continue;
                var m = ps.main;
                float d = m.startLifetime.constantMax;
                if (d > maxDuration) maxDuration = d;
            }

            // Floor at 0.3s so we never destroy IMMEDIATELY even if the
            // prefab is misconfigured (gives the hit visual a frame or
            // two to register before the entity goes away). Ceiling at
            // 4s so a misconfigured looping-particle (startLifetime = ∞)
            // doesn't keep the bullet alive forever.
            return Mathf.Clamp(maxDuration, 0.05f, 4f);
        }
    }
}

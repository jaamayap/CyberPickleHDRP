// File: Assets/_CyberPickle/Code/DOTS/Systems/ProjectileFadeOutSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Managed (NOT Burst) system that runs the trail-fade-out for projectiles
// transitioned to ProjectileDying by ProjectileCollisionSystem.
//
// Two visual-cleanup paths, picked per-entity:
//
//   HYBRID (preferred): If the Companion GameObject carries a
//     CyberPickleProjectileVisual component (our adapted Hovl-style script
//     with no motion/collision), call its OnHit(contactPos, contactNormal)
//     once. The script applies the prefab's authored hit positioning, hit
//     rotation mode, projectilePS clear, Detached[] fade, light disable,
//     and flash detach — exactly mirroring Hovl's per-prefab tuning. This
//     gives us the asset authors' intent without re-engineering it.
//
//   HEURISTIC FALLBACK: If no CyberPickleProjectileVisual is on the
//     entity (e.g., prefab not yet migrated, or a procedurally-built
//     projectile from a future system), walk all child ParticleSystems
//     and pick treatment by simulationSpace — local-space PS get
//     StopEmittingAndClear, world-space get StopEmitting. Plus disable
//     MeshRenderer, SkinnedMeshRenderer, TrailRenderer, Light. This is
//     a reasonable approximation but lacks the per-prefab tuning of the
//     hybrid path.
//
// Either path: decrement TimeRemaining each tick; destroy the entity
// when zero. The Companion GO dies with the entity.
//
// Why managed: ParticleSystem.Stop and friends are Mono calls, not
// Burst-compatible. The dying state is rare (only at kill moments) so
// the managed path's cost is negligible — typically 0 work per frame
// except during the brief fade windows after a hit.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Weapons;

namespace CyberPickle.DOTS.Systems
{
    /// <summary>
    /// Managed SystemBase that runs the fade-out for dying projectiles.
    /// Updates AFTER simulation so the dying state has propagated through
    /// the ECB playback from ProjectileCollisionSystem.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class ProjectileFadeOutSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<ProjectileDying>();
        }

        protected override void OnUpdate()
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                               .CreateCommandBuffer(World.Unmanaged);

            // Cannot use Burst here — we're touching Mono ParticleSystems via
            // GetComponentObject. The query is small (only dying projectiles,
            // usually 0-10 at peak combat) so the managed iteration cost is
            // negligible vs. the Burst-compiled simulation path.
            foreach (var (dyingRef, entity) in
                     SystemAPI.Query<RefRW<ProjectileDying>>().WithEntityAccess())
            {
                ref var dying = ref dyingRef.ValueRW;

                // First-time setup: trigger the prefab-authored OnHit
                // (hybrid path) or the heuristic cleanup (fallback). Also
                // read the prefab's correct fade-duration and write it
                // into TimeRemaining (ProjectileCollisionSystem set it to
                // 0 as a placeholder — we resolve the real value here
                // because it depends on per-prefab particle timings).
                // Subsequent ticks skip this branch via the flag.
                if (dying.EmissionStoppedFlag == 0)
                {
                    var contactPos    = new Vector3(dying.ContactPosition.x, dying.ContactPosition.y, dying.ContactPosition.z);
                    var contactNormal = new Vector3(dying.ContactNormal.x,   dying.ContactNormal.y,   dying.ContactNormal.z);
                    dying.TimeRemaining = RunVisualCleanup(entity, contactPos, contactNormal);
                    dying.EmissionStoppedFlag = 1;
                }

                // Decrement timer. When it hits zero, the entity is destroyed
                // by the ECB — the Companion GameObject + its (mostly-faded)
                // particles + light + mesh all die with it.
                dying.TimeRemaining -= dt;
                if (dying.TimeRemaining <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }

        /// <summary>
        /// Dispatches to the hybrid path (CyberPickleProjectileVisual.OnHit)
        /// when the Companion GO carries the script — that's where Hovl's
        /// per-prefab tuning lives (hit GO positioning, rotation mode,
        /// projectilePS clear, Detached[] fade, light disable, etc.).
        /// Falls back to the heuristic if the script isn't present.
        ///
        /// Returns the time-to-destroy that the caller should write back
        /// into ProjectileDying.TimeRemaining — read from the prefab's own
        /// timing data (hybrid) or computed from the Companion's particle
        /// systems (fallback). Each PREFAB owns its correct fade duration
        /// because Hovl's authors tuned the particle systems individually.
        ///
        /// CRITICAL: don't use EntityManager.HasComponent&lt;TMonoBehaviour&gt;
        /// for Companion-GO-attached scripts. Unity's hybrid bake may or
        /// may not register a custom MonoBehaviour as an ECS managed
        /// component, so the check is unreliable. The robust pattern is
        /// to grab the Companion Transform (always linked to hybrid
        /// entities) and call GetComponent on its GameObject.
        /// </summary>
        private float RunVisualCleanup(Entity entity, Vector3 contactPos, Vector3 contactNormal)
        {
            // Get the Companion Transform. Bail with default if there's no
            // Companion (pure ECS entity, no visuals to clean up).
            if (!EntityManager.HasComponent<UnityEngine.Transform>(entity)) return 0.3f;
            var companionTransform = EntityManager.GetComponentObject<UnityEngine.Transform>(entity);
            if (companionTransform == null) return 0.3f;

            // Hybrid path: find our script on the Companion GO. This
            // GetComponent IS reliable — it's a standard Mono call against
            // a real, live GameObject.
            var visual = companionTransform.GetComponent<CyberPickleProjectileVisual>();
            if (visual != null)
            {
                visual.OnHit(contactPos, contactNormal);
                return visual.GetTotalFadeDuration();
            }

            // Fallback: heuristic cleanup for prefabs without the script
            // (legacy, future spawn paths, scene-test setups).
            StopEmissionsOnCompanionTransform(companionTransform);
            return ComputeFadeDurationFromCompanion(companionTransform);
        }

        /// <summary>
        /// Fallback duration: longest particle lifetime in the Companion's
        /// visual hierarchy. Used when no CyberPickleProjectileVisual is
        /// present (legacy prefabs). Floor at 0.3s so we never destroy
        /// instantly even on misconfigured prefabs.
        /// </summary>
        private float ComputeFadeDurationFromCompanion(UnityEngine.Transform companionTransform)
        {
            float maxDuration = 0f;
            var particles = companionTransform.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                float d = main.duration + main.startLifetime.constantMax;
                if (d > maxDuration) maxDuration = d;
            }
            return Mathf.Max(0.3f, maxDuration);
        }

        /// <summary>
        /// Reach into the Companion GameObject for this entity and trigger
        /// Hovl-equivalent visual cleanup. Mirrors what HS_ProjectileMover
        /// did in its OnCollisionEnter — split into ECS-friendly pieces:
        ///
        ///   LOCAL-SPACE particle systems   → Stop(true, StopEmittingAndClear)
        ///     Hovl's <c>projectilePS.Stop(true, StopEmittingAndClear)</c>.
        ///     These emit relative to the bullet's transform (head glow,
        ///     attached spark). When the bullet freezes, local-space
        ///     particles would also freeze, leaving a static cloud at the
        ///     death spot. Clearing them avoids the "ghost cloud" look.
        ///
        ///   WORLD-SPACE particle systems   → Stop(true, StopEmitting)
        ///     Hovl's <c>Detached[].forEach Stop()</c>. These emit into
        ///     world space and drift independently of the bullet. After
        ///     StopEmitting, existing particles complete their
        ///     startLifetime naturally — the trail dissipates.
        ///
        ///   TrailRenderer components       → emitting = false
        ///     Stops new trail segments. Existing trail mesh fades over
        ///     the renderer's <c>time</c> field.
        ///
        ///   MeshRenderer / SkinnedMeshRenderer → enabled = false
        ///     The bullet's solid mesh head vanishes immediately. Hovl
        ///     achieved this implicitly by clearing the head's particle
        ///     system (whose "single-shot" emission rendered the head);
        ///     for prefabs where the head is an actual Mesh, this is the
        ///     explicit equivalent.
        ///
        ///   Light components               → enabled = false
        ///     Hovl's <c>lightSource.enabled = false</c>.
        ///
        /// Caller-supplied Companion Transform (already looked up by
        /// RunVisualCleanup) — saves a redundant GetComponentObject call.
        /// </summary>
        private void StopEmissionsOnCompanionTransform(UnityEngine.Transform companionTransform)
        {
            if (companionTransform == null) return;

            // ─── Particle systems: split by simulationSpace ──────────────
            var particles = companionTransform.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particles.Length; i++)
            {
                var ps = particles[i];
                var main = ps.main;
                if (main.simulationSpace == ParticleSystemSimulationSpace.World)
                {
                    // Trail. Let existing world-space particles fade naturally.
                    ps.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmitting);
                }
                else
                {
                    // Local / Custom space — these would freeze attached to
                    // the bullet's frozen transform, looking like a static
                    // particle cloud. Clear them out (Hovl's main projectile-
                    // particles treatment).
                    ps.Stop(withChildren: false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            // ─── Trail renderers ────────────────────────────────────────
            var trails = companionTransform.GetComponentsInChildren<TrailRenderer>(includeInactive: true);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].emitting = false;
            }

            // ─── Solid mesh head → instant hide ─────────────────────────
            var meshes = companionTransform.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i].enabled = false;
            }

            var skinned = companionTransform.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true);
            for (int i = 0; i < skinned.Length; i++)
            {
                skinned[i].enabled = false;
            }

            // ─── Lights off ─────────────────────────────────────────────
            var lights = companionTransform.GetComponentsInChildren<Light>(includeInactive: true);
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = false;
            }
        }
    }
}

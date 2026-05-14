// File: Assets/_CyberPickle/Code/Gameplay/Weapons/HitVfxApplier.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Mono-side hit VFX spawner. Called by DamageReportDrainSystem once per
// drained DamageHitReport. Picks the right element-tinted hit prefab
// from ElementVfxLibrary, instantiates it at the hit position, modulates
// its particle systems based on weapon scale + damage + crit, and lets
// Unity's ParticleSystem update render it out before auto-cleanup.
//
// Why Mono (not ECS): hit VFX are short-lived, position-fixed, pure
// visuals — no continuous update or gameplay state to justify the bake +
// ECS system overhead. Mono Instantiate + Destroy is the natural fit
// (see the chat 2026-05-11 design note).
//
// Size scale composition (per the M9 design):
//   sizeScale = hitVfxScale            (weapon-specific multiplier)
//             × damageMultiplier       (sqrt-scaled by damage, clamped)
//             × critMultiplier         (1.6× on crit)
//             × aoeMultiplier          (weapon's AoE radius if flagged)
//
// Tint composition:
//   tint = element.DisplayColor()  blended toward white on crit (40%)
//
// The applier is silent when:
//   - ElementVfxLibrary is missing (warned once via library)
//   - The element has no hitPrefab authored
//   - The weapon's hitVfxScale is zero (designer explicitly disabled)

using Unity.Entities;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.DOTS.Components;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    public static class HitVfxApplier
    {
        /// <summary>
        /// Read the player's AreaOfEffect stat from the ECS singleton.
        /// Returns 0 (neutral, no bonus) when the singleton isn't ready
        /// or the default world doesn't exist (early frames / scene-test
        /// setups). Used here so hit-VFX scales by the same scaled AoE
        /// formula that WeaponFiring uses to stamp ProjectileAoE.Radius
        /// and drive the telegraph — keeps visuals consistent with the
        /// actual blast.
        /// </summary>
        private static float ReadAreaStatStatic()
        {
            var w = World.DefaultGameObjectInjectionWorld;
            if (w == null) return 0f;
            using var query = w.EntityManager.CreateEntityQuery(typeof(PlayerStatsData));
            if (query.CalculateEntityCount() == 0) return 0f;
            return query.GetSingleton<PlayerStatsData>().AreaOfEffect;
        }


        /// <summary>
        /// Spawn an element-tinted hit VFX at the given world position,
        /// scaled and tinted per the damage + crit + weapon parameters,
        /// and oriented along the projectile's travel direction so the
        /// burst's emission cone reads as a continuation of the bullet's
        /// path (rather than the default world-up identity rotation,
        /// which looks detached from the trajectory).
        /// </summary>
        /// <param name="weaponId">The source weapon's equipmentId (for WeaponData lookup). May be empty.</param>
        /// <param name="element">The element this hit carries (drives prefab pick + base tint).</param>
        /// <param name="hitPosition">World position where the hit occurred.</param>
        /// <param name="hitDirection">Normalized direction the projectile was traveling when it hit. <c>Vector3.zero</c> if not available — VFX falls back to identity rotation.</param>
        /// <param name="damageDealt">Final damage applied (post-Power × crit). Drives the size scale.</param>
        /// <param name="isCrit">Whether the hit was a crit. Boosts size + brightens tint.</param>
        public static void Play(string weaponId, ElementId element, Vector3 hitPosition, Vector3 hitDirection, float damageDealt, bool isCrit)
        {
            var lib = ElementVfxLibrary.Instance;
            if (lib == null) return;

            var entry = lib.Get(element);
            if (entry.hitPrefab == null) return;

            // Look up the weapon's data for per-weapon scale + AoE flags.
            // Tolerates missing loadout (early run / direct-play) — falls
            // back to a neutral 1× scale and skips AoE.
            WeaponData weaponData = null;
            float aoeRadius = 1f;
            if (!string.IsNullOrEmpty(weaponId))
            {
                var loadout = WeaponLoadoutRuntime.Instance;
                var instance = loadout != null ? loadout.FindByWeaponId(weaponId) : null;
                if (instance != null && instance.IsValid && instance.weaponData != null)
                {
                    weaponData = instance.weaponData;
                    // Use the scaled AoE (level + area stat) so hit-VFX
                    // size matches the actual blast radius this shot
                    // produced. WeaponFiring computed exactly this for the
                    // ProjectileAoE.Radius stamp + the telegraph preview.
                    float areaStat = ReadAreaStatStatic();
                    aoeRadius = Mathf.Max(1f, weaponData.GetAreaOfEffectForLevel(instance.level, areaStat));
                }
            }

            float weaponScale = weaponData != null ? weaponData.hitVfxScale : 1f;
            if (weaponScale <= 0f) return;

            float damageMul = ComputeDamageMultiplier(damageDealt);
            float critMul   = isCrit ? 1.6f : 1f;
            float aoeMul    = (weaponData != null && weaponData.hitVfxScalesWithAreaOfEffect) ? aoeRadius : 1f;
            float sizeScale = weaponScale * damageMul * critMul * aoeMul;

            Color elementColor = element.DisplayColor();
            // On crit, lerp 40% toward white for a "pop" of brightness.
            Color tint = isCrit ? Color.Lerp(elementColor, Color.white, 0.4f) : elementColor;

            // Orient the hit VFX so its forward axis faces back along the
            // projectile's direction of travel — splash + spark patterns
            // emit "back at the shooter" which reads as a proper impact.
            // Fall back to identity if no direction was provided (legacy
            // call sites, or zero-velocity projectiles).
            Quaternion hitRot = Quaternion.identity;
            if (hitDirection.sqrMagnitude > 0.0001f)
                hitRot = Quaternion.LookRotation(-hitDirection, Vector3.up);

            GameObject hit = Object.Instantiate(entry.hitPrefab, hitPosition, hitRot);
            ApplyParticleModulation(hit, sizeScale, tint);

            // Auto-cleanup. Use the longest particle system duration + a
            // small grace period so trailing particles finish.
            float maxDuration = 1.0f;
            foreach (var ps in hit.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                float dur = main.duration + main.startLifetime.constantMax;
                if (dur > maxDuration) maxDuration = dur;
            }
            Object.Destroy(hit, maxDuration + 0.5f);
        }

        /// <summary>
        /// Map damage to a size multiplier with a sqrt curve so big-damage
        /// bursts don't dwarf the screen. damage=10 → 1.0× (the design
        /// baseline). damage=50 → ~2.2×. damage=160 → ~4.0× (clamp cap).
        /// </summary>
        private static float ComputeDamageMultiplier(float damageDealt)
        {
            if (damageDealt <= 0f) return 0.5f;
            float raw = Mathf.Sqrt(damageDealt / 10f);
            return Mathf.Clamp(raw, 0.5f, 4f);
        }

        /// <summary>
        /// Mutate every ParticleSystem on the hit GameObject — scale
        /// startSize by <paramref name="sizeScale"/> and tint startColor
        /// toward <paramref name="tint"/>. Preserves alpha and original
        /// luminance balance (60% original × 40% tint) so designer-
        /// authored gradients stay readable.
        /// </summary>
        private static void ApplyParticleModulation(GameObject hit, float sizeScale, Color tint)
        {
            var systems = hit.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in systems)
            {
                var main = ps.main;

                // Scale startSize — handles both Constant and
                // RandomBetweenTwoConstants modes (sets both endpoints).
                var s = main.startSize;
                s.constantMin *= sizeScale;
                s.constantMax *= sizeScale;
                main.startSize = s;

                // Tint startColor — lerp the constant components 60/40
                // toward the requested tint. For TwoColors mode this
                // tints both endpoints. For Gradient/RandomColor modes
                // we leave the gradient itself alone and just nudge the
                // alpha + base — designer's gradient choice still reads.
                var c = main.startColor;
                Color blended = Color.Lerp(c.color, tint, 0.4f);
                blended.a = c.color.a; // preserve authored alpha
                c.color = blended;
                main.startColor = c;
            }
        }
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Components/WeaponLevel.cs
// Namespace: CyberPickle.DOTS.Components
//
// Burst-side mirror of an equipped weapon's Level axis (1..5 + Evolved).
// Per weapon_rarity_v1.md the Level axis controls fire-rate AND musical
// pattern complexity (the same phenomenon viewed mechanically vs musically).
//
// Component placement:
//   - On per-equipped-weapon entities (one per active loadout slot, 0..3)
//     when the loadout system lands. Until then, set at projectile spawn
//     time so per-shot consumers (collision, music) can read the level
//     value without going back through the WeaponId → Mono lookup.
//
// Sync source of truth:
//   - MonoBehaviour-side WeaponInstanceData.level / .evolved (run-state
//     authority — see Gameplay/Weapons/WeaponInstanceData.cs).
//   - WeaponLoadoutRuntime mirrors changes into this component when the
//     weapon levels up.
//
// Why a separate Evolved flag instead of treating L6 as Evolved:
//   Level 1..5 maps to fire-rate scalars and pattern grain. Evolved is a
//   *separate axis* on top of L5 — it changes the projectile's mechanic
//   entirely (cone burst, chained shots, etc.) and triggers a unique
//   musical pattern. Encoding it as L6 would overload Value's meaning.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    /// <summary>
    /// Burst-readable weapon level. <see cref="Value"/> is 1..5; <see cref="EvolvedFlag"/>
    /// is 1 once the weapon has unlocked its evolved form (post-L5 + evolution
    /// trigger conditions). Both axes contribute independently to fire rate
    /// and to the musical pattern bound to this slot.
    /// </summary>
    public struct WeaponLevel : IComponentData
    {
        /// <summary>1..5. Out-of-range values are treated as clamped at the consumer.</summary>
        public byte Value;

        /// <summary>0 = base form, 1 = evolved (unique pattern + mechanic).</summary>
        public byte EvolvedFlag;
    }
}

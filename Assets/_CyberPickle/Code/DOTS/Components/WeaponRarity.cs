// File: Assets/_CyberPickle/Code/DOTS/Components/WeaponRarity.cs
// Namespace: CyberPickle.DOTS.Components
//
// Burst-side mirror of an equipped weapon's Rarity axis (Common..Legendary).
// Per weapon_rarity_v1.md §2, Rarity drives the damage scalar (×1.0..×4.0)
// and a per-tier bonus perk. Visually it drives the weapon's frame color
// and audio distortion intensity.
//
// Component placement:
//   - On per-equipped-weapon entities (one per active loadout slot, 0..3)
//     when the loadout system lands. Or on projectile entities at spawn
//     time so collision-side damage formulas can read the multiplier
//     without going back through the WeaponId → Mono lookup.
//
// Sync source of truth:
//   - MonoBehaviour-side WeaponInstanceData.rarity (run-state authority).
//   - WeaponLoadoutRuntime mirrors changes into this component when the
//     weapon's rarity changes (Augment Console, Rarity-up card, Black Market).
//
// Byte values match CyberPickle.Core.Rarity exactly (Common=0..Legendary=4).
// Cast in either direction is safe: `(Rarity)weaponRarity.Value` and
// `(byte)rarity`. See Core/Rarity.cs for the centralization rule.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    /// <summary>
    /// Burst-readable weapon rarity. <see cref="Value"/> matches the byte
    /// values of <see cref="CyberPickle.Core.Rarity"/> (0=Common..4=Legendary).
    /// Burst code can cast directly: <c>(Rarity)weaponRarity.Value</c>.
    /// </summary>
    public struct WeaponRarity : IComponentData
    {
        /// <summary>0..4 matching CyberPickle.Core.Rarity. See RarityExtensions.DamageMultiplier() for the scalar table.</summary>
        public byte Value;
    }
}

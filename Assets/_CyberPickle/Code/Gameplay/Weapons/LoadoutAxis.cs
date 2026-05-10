// File: Assets/_CyberPickle/Code/Gameplay/Weapons/LoadoutAxis.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// One axis of the player's cross-shaped loadout. An axis is a paired
// (weapon, power-up) tuple that shares an element identity. With the
// default 4 axes (N/E/S/W), there are 4 weapon slots + 4 power-up slots
// = 8 cells in the cross HUD.
//
// Slotting rules (per chat 2026-05-11):
//   - Either field can be null (axis can hold a power-up with no weapon,
//     a weapon with no power-up, or both).
//   - The axis's element comes from the slotted power-up. If a weapon
//     is present, it inherits that element (which drives its musical mode
//     per procedural_music_reference.md §22.4).
//   - When the power-up is removed, the weapon's element falls back to
//     ElementId.None (neutral) — production weapons roll neutral by
//     default; element only ever comes from power-up coupling.
//   - No replacement: once an axis cell is full, it stays full until the
//     run ends. The card pool filters out cards that would land on a full
//     cell — see UpgradePoolSO loadout-aware filter (M8 step 2).
//
// Class (not struct) so call sites can do `axis.weapon = X` without
// having to fetch + reassign back into the array. The 4-class allocation
// per run is negligible.

using System;

namespace CyberPickle.Gameplay.Weapons
{
    /// <summary>
    /// One axis of the cross loadout. Pair of (weapon, power-up) sharing
    /// an element identity.
    /// </summary>
    [Serializable]
    public class LoadoutAxis
    {
        /// <summary>
        /// Stable axis index (0..N-1). Set by <see cref="WeaponLoadoutRuntime"/>
        /// when the axis is allocated; never mutated thereafter.
        /// </summary>
        public int axisIndex;

        /// <summary>
        /// Weapon slotted on this axis, or null if empty. Inherits its
        /// element from <see cref="powerUp"/> when both are present.
        /// </summary>
        public WeaponInstanceData weapon;

        /// <summary>
        /// Power-up slotted on this axis, or null if empty. Its stat
        /// bonus applies globally (registered as a StatModifier on
        /// PlayerStats); its element confers locally to <see cref="weapon"/>.
        /// </summary>
        public PowerUpInstanceData powerUp;

        /// <summary>True iff a weapon is slotted on this axis.</summary>
        public bool HasWeapon => weapon != null && weapon.IsValid;

        /// <summary>True iff a power-up is slotted on this axis.</summary>
        public bool HasPowerUp => powerUp != null && powerUp.IsValid;

        /// <summary>True iff both slots are empty (axis is fully unused).</summary>
        public bool IsEmpty => !HasWeapon && !HasPowerUp;

        /// <summary>True iff both slots are filled (axis can't accept more cards).</summary>
        public bool IsFull => HasWeapon && HasPowerUp;
    }
}

// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponInstanceData.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Runtime per-equipped-weapon record (MonoBehaviour-side). Holds the
// current Level + Rarity + Element state of one weapon in the player's
// loadout.
//
// Owned by: WeaponLoadoutRuntime (Manager<T>, scene-bound). The loadout
// owns up to 4 of these — 1 starting weapon + up to 3 drafted in-run,
// per economy_design_v1.md §7.
//
// 2026-05-10 refactor: replaced opaque <c>string weaponId</c> with a
// direct <see cref="WeaponData"/> reference. Reasons:
//   - Run-scoped state isn't serialized, so the SO ref is safe (no
//     Unity asset migration concerns).
//   - Downstream consumers (WeaponFiring damage formula, music conductor,
//     hover tooltips) read base damage / fire-rate / patterns from
//     WeaponData every frame; doing that via a string lookup would
//     require a registry indirection on a hot path.
//   - The string id is still exposed via <see cref="WeaponId"/> for
//     analytics, tracker attribution, and debug logs.
//
// Why a plain serializable class (not a ScriptableObject and not an ECS
// component):
//   - Run-scoped state, not persistent design data — SO is wrong fit.
//   - Mutated frequently from main thread (level-up cards, rarity
//     upgrades, Augment Console interactions) — Burst is wrong fit.
//   - Designers don't author these — they're constructed at runtime
//     from a WeaponData reference + initial rarity roll.
//
// Mirroring rules:
//   - On level/rarity/element change, WeaponLoadoutRuntime fires
//     OnWeaponLevelChanged / OnWeaponRarityChanged (TODO: WeaponElementChanged
//     when M9 element-coupling lands — see procedural_music_reference.md §22.7).
//   - HUD, music conductor, and any future per-slot ECS entities update
//     their state from those events.

using System;
using CyberPickle.Core;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    /// <summary>
    /// Per-equipped-weapon runtime record. Six fields, no behaviour —
    /// the data shape that the loadout system, UI, music conductor, and
    /// damage formula all read from.
    /// </summary>
    [Serializable]
    public class WeaponInstanceData
    {
        /// <summary>
        /// Direct reference to the <see cref="WeaponData"/> ScriptableObject
        /// that defines this weapon's design (base damage, fire rate curve
        /// per level, rarity-tier perks, evolved-form mechanics, projectile
        /// prefab, level patterns, default element, etc.).
        ///
        /// Required — a WeaponInstanceData with a null weaponData is
        /// invalid and should be discarded by consumers.
        /// </summary>
        public WeaponData weaponData;

        /// <summary>
        /// Stable string id for this weapon. Equivalent to
        /// <c>weaponData.equipmentId</c>. Cached on the instance for
        /// places that historically used a string id (analytics,
        /// per-weapon tracker attribution via <c>ProjectileSource.WeaponId</c>,
        /// debug logs) — avoids null-checking weaponData every call site.
        /// </summary>
        public string WeaponId => weaponData != null ? weaponData.equipmentId : string.Empty;

        /// <summary>
        /// Level axis: 1..5. Drives fire-rate scaling and musical pattern
        /// complexity (per <c>weapon_rarity_v1.md</c> §1). Independent of
        /// <see cref="rarity"/>.
        /// </summary>
        public int level = 1;

        /// <summary>
        /// True once the weapon has unlocked its evolved form (typically
        /// after L5 + evolution trigger such as a paired power-up). Adds
        /// a unique projectile mechanic and a unique musical pattern on
        /// top of the level scaling.
        /// </summary>
        public bool evolved = false;

        /// <summary>
        /// Rarity axis: drives damage scalar (×1.0..×4.0) and the bonus
        /// perk awarded per tier. Rolled at first appearance, modulated
        /// by Luck, upgradeable mid-run via Rarity-up cards or in-level
        /// Augment Console / Black Market interactables.
        /// </summary>
        public Rarity rarity = Rarity.Common;

        /// <summary>
        /// Active element. Drives the musical mode for this weapon's
        /// pattern playback (Fire = Phrygian Dominant, etc. — see
        /// <c>procedural_music_reference.md</c> §8).
        ///
        /// 2026-05-11 (M8): element is now SOURCED FROM THE AXIS'S POWER-UP
        /// (not from <c>weaponData.defaultElement</c>). When a weapon enters
        /// the loadout, it inherits the element of the power-up already on
        /// the same axis (or <see cref="ElementId.None"/> if no power-up).
        /// When a power-up is added/removed/replaced, the axis's weapon's
        /// element is updated by <c>WeaponLoadoutRuntime</c> and
        /// <c>MusicEvent.WeaponElementChanged</c> fires.
        /// <c>WeaponData.defaultElement</c> remains for editor-test-only
        /// scenarios — production weapons start neutral.
        /// </summary>
        public ElementId element = ElementId.None;

        /// <summary>
        /// Slot the weapon occupies in the player's run loadout. 0 = the
        /// pre-run starting weapon; 1..3 = drafted weapons. Used by the
        /// HUD to map weapon icons to slots, and by the music system to
        /// pick which RTPCs (<c>Music_WeaponLevel_Slot{N}</c>, etc.) this
        /// weapon drives.
        /// </summary>
        public int slotIndex;

        /// <summary>
        /// Convenience: 6 if Evolved, else <see cref="level"/>. The
        /// "effective level" the music system maps to a pattern grain
        /// (8th → 16th → 32nd → unique-pattern).
        /// </summary>
        public int EffectiveLevel => evolved ? 6 : level;

        /// <summary>True iff this instance has a valid <see cref="weaponData"/> reference.</summary>
        public bool IsValid => weaponData != null;
    }
}

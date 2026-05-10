// File: Assets/_CyberPickle/Code/Gameplay/Weapons/PowerUpInstanceData.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Runtime per-axis power-up record. Mirrors the shape of WeaponInstanceData
// for symmetry. Created when a power-up card is committed to a loadout
// axis; destroyed when the run ends or a future replacement card lands on
// the same axis (replacement is currently disallowed by design — see
// CLAUDE.md M8 design notes — but the lifecycle hook exists for the day
// the rule changes).
//
// Two characteristics, mirroring PowerUpData's design:
//   - GLOBAL: stat boost (affectedStat × magnitude) — applies via a
//     StatModifier added to PlayerStats. Sourced as
//     "powerup_<id>_axis<N>" so removal is one bulk call.
//   - LOCAL: element conferred to the WEAPON on the same axis. When
//     this power-up is added/removed/changed, the axis's weapon's
//     ElementId is updated by WeaponLoadoutRuntime.
//
// Why "Level" exists if the M8 model only scales magnitude by Rarity:
// per the user's design (chat 2026-05-11), upgrade cards are dual-axis
// (Level + Rarity) for power-ups too. Level scaling reserved for M8
// step 2 — currently unused but stored so step 2 can wire it without
// migrating runtime data.

using System;
using CyberPickle.Core;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    /// <summary>
    /// Per-equipped-power-up runtime record.
    /// </summary>
    [Serializable]
    public class PowerUpInstanceData
    {
        /// <summary>
        /// Direct reference to the <see cref="PowerUpData"/> asset that
        /// defines this power-up's stat target + per-rarity magnitude curve.
        /// </summary>
        public PowerUpData powerUpData;

        /// <summary>
        /// Stable string id (mirrors <c>powerUpData.equipmentId</c>) for
        /// analytics + tracker attribution.
        /// </summary>
        public string PowerUpId => powerUpData != null ? powerUpData.equipmentId : string.Empty;

        /// <summary>
        /// Level axis: 1..5. Reserved for M8 step 2 (level upgrade cards
        /// for power-ups). Currently unused by the magnitude formula but
        /// stored for forward compat.
        /// </summary>
        public int level = 1;

        /// <summary>
        /// Rarity axis: drives the magnitude lookup in
        /// <c>PowerUpData.GetMagnitudeForRarity</c>. Rolled at draft time
        /// (Luck-modulated via <c>RarityRollService</c>); upgradeable
        /// in-run via Rarity-up cards.
        /// </summary>
        public Rarity rarity = Rarity.Common;

        /// <summary>
        /// Element this power-up confers to its axis's weapon (if any).
        /// Rolled at draft time — same template asset can show up as
        /// Fire / Lightning / Ice / etc. variants.
        /// </summary>
        public ElementId element = ElementId.None;

        /// <summary>
        /// Axis this power-up occupies in the player's loadout. Mirrors
        /// the slotIndex on <see cref="WeaponInstanceData"/> for symmetry.
        /// </summary>
        public int axisIndex;

        /// <summary>True iff this instance has a valid <see cref="powerUpData"/> reference.</summary>
        public bool IsValid => powerUpData != null;

        /// <summary>
        /// Current magnitude (decimal fraction, 0.10 = +10%) given this
        /// instance's rarity. Used by <c>WeaponLoadoutRuntime</c> when
        /// applying / refreshing the StatModifier on PlayerStats.
        /// </summary>
        public float CurrentMagnitude
            => powerUpData != null ? powerUpData.GetMagnitudeForRarity(rarity) : 0f;

        /// <summary>
        /// SourceId used when adding the StatModifier to PlayerStats.
        /// Includes the axis index so two power-ups of the same template
        /// on different axes don't collide.
        /// </summary>
        public string ModifierSourceId
            => $"powerup_{PowerUpId}_axis{axisIndex}";
    }
}

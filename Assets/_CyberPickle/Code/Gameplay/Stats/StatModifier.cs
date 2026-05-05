// File: Assets/_CyberPickle/Code/Gameplay/Stats/StatModifier.cs
// Namespace: CyberPickle.Gameplay.Stats
//
// One stat modifier — the unit of stat change in the system. Every
// source of stat modification (skill, equipment, implant, run upgrade,
// status effect) emits StatModifier instances; PlayerStats accumulates
// them and recomputes effective values from the base.
//
// SourceId is the identifier used for selective removal (e.g., when an
// item is unequipped, all of its modifiers are removed in one call to
// PlayerStats.RemoveModifiersFromSource("equip_amulet_fortune")).

using System;

namespace CyberPickle.Gameplay.Stats
{
    /// <summary>
    /// How a modifier combines with the base value. Application order
    /// (per stat, in PlayerStats.Recompute):
    ///   1. AddBase     — added to base before any percent multipliers
    ///   2. AddPercent  — sums all percent modifiers, applies as +X%
    ///   3. MultFinal   — multiplicative, stacks (×1.5 × ×1.5 = ×2.25)
    ///   4. Override    — replaces the value entirely (last one wins)
    /// </summary>
    public enum ModifierKind : byte
    {
        /// <summary>Adds a flat value to base BEFORE percent multipliers. e.g., +50 MaxHealth from an armor.</summary>
        AddBase    = 0,

        /// <summary>Adds a percent (0.10 = +10%) that stacks ADDITIVELY with other AddPercent modifiers. e.g., +10% Power from a skill.</summary>
        AddPercent = 1,

        /// <summary>Multiplies the final value (1.5 = ×1.5). Stacks MULTIPLICATIVELY. e.g., ×2 damage power-up.</summary>
        MultFinal  = 2,

        /// <summary>Replaces the value entirely. Last applied wins. Use sparingly — typically for hard caps or debuff overrides.</summary>
        Override   = 3,
    }

    /// <summary>
    /// One stat modifier targeting one stat. Carries a sourceId for
    /// selective removal — when a source (e.g., an unequipped item)
    /// is removed, all its modifiers are unwound in one O(n) pass.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        public PlayerStatType type;
        public ModifierKind   kind;
        public float          value;

        /// <summary>
        /// Tag identifying the source of this modifier. Convention:
        ///   "skill_<id>"     skill tree allocation
        ///   "equip_<id>"     equipped item (weapon / armor / amulet / power-up)
        ///   "implant_<id>"   cybernetic implant
        ///   "run_<id>"       in-run upgrade card
        ///   "temp_<id>"      temporary status effect
        ///   "identity_<id>"  active synergy identity
        ///   "breakpoint_<id>" stat breakpoint effect
        /// Pass to PlayerStats.RemoveModifiersFromSource(sourceId) to remove.
        /// </summary>
        public string sourceId;

        public StatModifier(PlayerStatType type, ModifierKind kind, float value, string sourceId)
        {
            this.type     = type;
            this.kind     = kind;
            this.value    = value;
            this.sourceId = sourceId;
        }
    }
}

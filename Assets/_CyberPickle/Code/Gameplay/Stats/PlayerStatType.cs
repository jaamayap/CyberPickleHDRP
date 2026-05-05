// File: Assets/_CyberPickle/Code/Gameplay/Stats/PlayerStatType.cs
// Namespace: CyberPickle.Gameplay.Stats
//
// Canonical player stat enum. Used as the single key across the entire
// stats pipeline:
//   - CharacterData base values (per character, in SO)
//   - CharacterProgressionData saved values (per profile, persistent)
//   - StatModifier targeting (one modifier targets one stat)
//   - BaseStats Get/Set (strongly-typed accessor)
//   - PlayerStats cache (effective values at runtime)
//   - PlayerStatsData ECS singleton (mirrored for Burst-side reads)
//
// Numeric values are stable for serialization — DO NOT renumber existing
// entries. Add new stats at the end of the enum.

namespace CyberPickle.Gameplay.Stats
{
    public enum PlayerStatType : byte
    {
        // ─── Defense / Survivability ─────────────────────────────────────
        MaxHealth         = 0,
        HealthRegen       = 1,
        Defense           = 2,

        // ─── Combat Output ────────────────────────────────────────────────
        Power             = 3,
        CritChance        = 4,   // 0..1 (probability)
        Lifesteal         = 5,   // 0..1 (fraction of damage healed)

        // ─── Movement / Pickup ────────────────────────────────────────────
        Speed             = 6,
        MagneticField     = 7,
        AreaOfEffect      = 8,
        Dexterity         = 9,

        // ─── Replay Variance / Identity ───────────────────────────────────
        Luck              = 10,
        Hack              = 11,  // 0..1 (probability on hit)
        CooldownReduction = 12,  // 0..1 (fraction reduction)
        NeuralAdaptation  = 13,  // 1.0 = 100% baseline, > 1 = XP gain bonus
    }

    /// <summary>
    /// Constants and helpers for PlayerStatType. Centralized so other code
    /// (BaseStats, PlayerStats, PlayerStatsData) can use Count for
    /// fixed-size buffers without duplicating the literal.
    /// </summary>
    public static class PlayerStatTypeMeta
    {
        /// <summary>Total number of stat types in the canonical enum (= 14).</summary>
        public const int Count = 14;
    }
}

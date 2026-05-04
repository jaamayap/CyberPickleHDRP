// File: Assets/_CyberPickle/Code/Gameplay/Stats/BaseStats.cs
// Namespace: CyberPickle.Gameplay.Stats
//
// Strongly-typed value carrier for the canonical 14 player stats.
// Replaces:
//   - CharacterData's loose float fields (maxHealth, power, etc.)
//   - CharacterProgressionData.stats Dictionary<string,float>
//
// Strongly-typed (vs Dictionary) for:
//   - Compile-time safety (no typo bugs on stat names)
//   - Inspector editability per character SO
//   - Burst / blittable compatibility (pure-data struct)
//   - Performance: no string lookups, no boxing
//
// The Get/Set switch statements compile to a jump table on byte enum
// inputs — sub-nanosecond per call. No measurable cost vs direct field
// access.

using System;
using UnityEngine;

namespace CyberPickle.Gameplay.Stats
{
    [Serializable]
    public struct BaseStats
    {
        // Field order matches PlayerStatType enum order. DO NOT reorder
        // without coordinating with the enum — serialized assets depend
        // on field name preservation, not order, but matching helps when
        // reading the file by hand.

        [Header("Defense / Survivability")]
        [Tooltip("Maximum health pool.")]
        [Min(1f)] public float maxHealth;

        [Tooltip("Health recovered per second (passive trickle).")]
        [Min(0f)] public float healthRegen;

        [Tooltip("Damage reduction. Formula: taken × 100 / (100 + defense).")]
        [Min(0f)] public float defense;

        [Header("Combat Output")]
        [Tooltip("Multiplier into the damage formula. 10 = baseline character power.")]
        [Min(0f)] public float power;

        [Tooltip("Probability per hit (0..1) to deal a critical hit (2× damage; 4× at Mega Crit breakpoint).")]
        [Range(0f, 1f)] public float critChance;

        [Tooltip("Fraction (0..1) of damage dealt healed back to the player.")]
        [Range(0f, 1f)] public float lifesteal;

        [Header("Movement / Pickup")]
        [Tooltip("Free-space movement speed in world units per second.")]
        [Min(0.1f)] public float speed;

        [Tooltip("Multiplier on item magnet radius. 1.0 = baseline (~4m).")]
        [Min(0.1f)] public float magneticField;

        [Tooltip("Multiplier on AOE radius for explosions, auras, etc.")]
        [Min(0.1f)] public float areaOfEffect;

        [Tooltip("Multiplier on weapon rate-of-fire (cooldown reduction on weapons).")]
        [Min(0f)] public float dexterity;

        [Header("Replay Variance / Identity")]
        [Tooltip("Affects drop tier rarity rolls and crit-chance scaling. 1.0 = baseline.")]
        [Min(0f)] public float luck;

        [Tooltip("Probability per hit (0..1) to hack the target. Mechanical enemies become temporary allies; organic enemies are disabled briefly.")]
        [Range(0f, 1f)] public float hack;

        [Tooltip("Fraction (0..1) reduction on special-ability + power-up cooldowns.")]
        [Range(0f, 1f)] public float cooldownReduction;

        [Tooltip("XP gain multiplier. 1.0 = 100% baseline. > 1 = scales faster.")]
        [Min(0f)] public float neuralAdaptation;

        // ─── Get / Set by enum key ───────────────────────────────────────

        /// <summary>Read a stat by enum key.</summary>
        public float Get(PlayerStatType s)
        {
            switch (s)
            {
                case PlayerStatType.MaxHealth:         return maxHealth;
                case PlayerStatType.HealthRegen:       return healthRegen;
                case PlayerStatType.Defense:           return defense;
                case PlayerStatType.Power:             return power;
                case PlayerStatType.CritChance:        return critChance;
                case PlayerStatType.Lifesteal:         return lifesteal;
                case PlayerStatType.Speed:             return speed;
                case PlayerStatType.MagneticField:     return magneticField;
                case PlayerStatType.AreaOfEffect:      return areaOfEffect;
                case PlayerStatType.Dexterity:         return dexterity;
                case PlayerStatType.Luck:              return luck;
                case PlayerStatType.Hack:              return hack;
                case PlayerStatType.CooldownReduction: return cooldownReduction;
                case PlayerStatType.NeuralAdaptation:  return neuralAdaptation;
                default: return 0f;
            }
        }

        /// <summary>Write a stat by enum key.</summary>
        public void Set(PlayerStatType s, float value)
        {
            switch (s)
            {
                case PlayerStatType.MaxHealth:         maxHealth = value;         break;
                case PlayerStatType.HealthRegen:       healthRegen = value;       break;
                case PlayerStatType.Defense:           defense = value;           break;
                case PlayerStatType.Power:             power = value;             break;
                case PlayerStatType.CritChance:        critChance = value;        break;
                case PlayerStatType.Lifesteal:         lifesteal = value;         break;
                case PlayerStatType.Speed:             speed = value;             break;
                case PlayerStatType.MagneticField:     magneticField = value;     break;
                case PlayerStatType.AreaOfEffect:      areaOfEffect = value;      break;
                case PlayerStatType.Dexterity:         dexterity = value;         break;
                case PlayerStatType.Luck:              luck = value;              break;
                case PlayerStatType.Hack:              hack = value;              break;
                case PlayerStatType.CooldownReduction: cooldownReduction = value; break;
                case PlayerStatType.NeuralAdaptation:  neuralAdaptation = value;  break;
            }
        }

        // ─── Defaults ────────────────────────────────────────────────────

        /// <summary>Sensible default values for a freshly-created character.</summary>
        public static BaseStats Defaults => new BaseStats
        {
            maxHealth         = 100f,
            healthRegen       = 1f,
            defense           = 10f,
            power             = 10f,
            critChance        = 0.05f,  // 5% baseline crit
            lifesteal         = 0f,
            speed             = 6f,
            magneticField     = 1f,
            areaOfEffect      = 1f,
            dexterity         = 10f,
            luck              = 1f,
            hack              = 0f,
            cooldownReduction = 0f,
            neuralAdaptation  = 1f,     // 1.0 = 100% (no XP scaling bonus)
        };
    }
}

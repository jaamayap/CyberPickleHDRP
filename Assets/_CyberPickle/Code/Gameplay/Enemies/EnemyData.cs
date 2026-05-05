// File: Assets/_CyberPickle/Code/Gameplay/Enemies/EnemyData.cs
// Namespace: CyberPickle.Gameplay.Enemies
//
// Designer-facing ScriptableObject defining a single enemy type
// (Robot Insect, Tank, Cyborg, etc.). The full schema is defined upfront
// so designers can fill out one form per enemy and never need to revisit
// when new systems ship — fields are documented with the milestone whose
// system will consume them. Fields default to sensible "no-op" values
// so unimplemented features don't produce wrong behavior.
//
// Pattern: this SO is the single source of truth for an enemy's design
// data. The visual prefab carries an EnemyAuthoring component that
// references its EnemyData here. At bake time, EnemyAuthoring.Baker
// reads ONLY the fields whose runtime systems exist, and copies values
// into IComponentData on the entity. As future milestones land, their
// Bakers add new components reading the additional fields.
//
// Adding a field later costs ~1 line on the SO + ~1 line in the Baker.
// Existing .asset files retain whatever values they had + get the
// default for the new field. No migration script ever required.

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Enemies
{
    [CreateAssetMenu(fileName = "NewEnemyData", menuName = "CyberPickle/Enemies/Enemy Data", order = 1)]
    public class EnemyData : ScriptableObject
    {
        // =====================================================================
        // IDENTITY (M5 — used now)
        // =====================================================================

        [Header("Identity")]
        [Tooltip("Unique string ID. Used as a hash key for the enemy prefab registry. Convention: lowercase_with_underscores, e.g. \"robot_insect\".")]
        public string enemyId = "new_enemy";

        [Tooltip("Display name shown in UI / death notifications / bestiary.")]
        public string displayName = "New Enemy";

        [Tooltip("If true, this enemy is treated as a boss — different HUD, music swell, larger contact-damage scaling, custom death sequence.")]
        public bool isBoss = false;


        // =====================================================================
        // VISUALS (M5 — used now)
        // =====================================================================

        [Header("Visuals")]
        [Tooltip("GameObject prefab for the enemy. Carries the visual mesh, animator, EnemyAuthoring component, and any other authoring components. The Baker uses this prefab to populate the EnemyPrefabRegistry singleton.")]
        public GameObject visualPrefab;

        [Tooltip("Visual type / size category. Drives Animator branching (which Walk/Run/Death states play) and any future per-size logic (drop scaling, knockback resistance defaults, etc.). Stored as an int on the entity so the Animator's EnemyType parameter can reference it directly.")]
        public EnemyVisualType visualType = EnemyVisualType.StandardHumanoid;


        // =====================================================================
        // CORE STATS (M5 — used now)
        // =====================================================================

        [Header("Core Stats")]
        [Tooltip("Total health pool. Damage from projectiles depletes this; entity despawns when Current ≤ 0.")]
        [Min(0.01f)]
        public float maxHealth = 10f;

        [Tooltip("Movement speed in world units per second toward the player.")]
        [Min(0f)]
        public float moveSpeed = 2f;

        [Tooltip("Damage applied to player on collision contact (per-second tick or per-touch — implemented in M6).")]
        [Min(0f)]
        public float contactDamage = 5f;

        [Tooltip("Physical size of the enemy's collision footprint (used by collision proximity checks and visual scale hint).")]
        [Min(0.1f)]
        public float bodyRadius = 0.5f;


        // =====================================================================
        // DROPS (M6 — currency / XP system)
        // =====================================================================

        [Header("Corpse Lifecycle")]
        [Tooltip("Seconds the corpse remains as a ragdolling body before the dissolve effect starts. Lower = quicker cleanup, higher = more ragdoll time. Designer-tunable per enemy.")]
        [Range(0.1f, 30f)] public float corpseDelayBeforeDissolve = 3f;

        [Tooltip("Length of the dissolve effect itself (the visual shrink + emissive flare). After this, entity + visual are destroyed.")]
        [Range(0.1f, 5f)] public float corpseDissolveDuration = 1.5f;

        [Header("Drops on Death")]
        [Tooltip("Neural Credits awarded to the player when this enemy dies. (Currency — M? when environment-mining ships)")]
        [Min(0)]
        public int neuralCreditsOnDeath = 1;

        [Tooltip("[LEGACY — replaced by xpDropTable] Flat XP awarded on death. Kept for back-compat; new enemies should use the tier-based xpDropTable instead.")]
        [Min(0)]
        public int xpOnDeath = 1;

        [Tooltip("Tiered XP drop probabilities — cascade roll picks one tier per kill. Bosses spawn a multi-drop burst on top.")]
        public XPDropTable xpDropTable = XPDropTable.DefaultTrash;

        [Tooltip("Chance (0–100%) that a rare item drop fires on death. 0 = never. (M7+)")]
        [Range(0f, 100f)]
        public float rareDropChance = 0f;

        [Tooltip("Loot table — items that may drop, with per-item probability. Evaluated only if rareDropChance fires. (M7+)")]
        public List<LootDrop> rareDropTable = new List<LootDrop>();


        // =====================================================================
        // DEFENSES (M7 — combat modifiers)
        // =====================================================================

        [Header("Defenses")]
        [Tooltip("Flat damage reduction (subtracted before % resistances). 0 = none. (M7)")]
        [Min(0f)]
        public float armor = 0f;

        [Tooltip("0 = takes full knockback, 1 = immune. (M7)")]
        [Range(0f, 1f)]
        public float knockbackResistance = 0f;

        [Tooltip("0 = stuns last full duration, 1 = immune. (M7)")]
        [Range(0f, 1f)]
        public float stunResistance = 0f;

        [Tooltip("Per-element damage multipliers (0 = immune, 1 = normal damage, > 1 = vulnerable). (M7)")]
        public ElementResistances elementResistances = ElementResistances.Default;


        // =====================================================================
        // BEHAVIOR / AI (M8 — AI variants beyond simple SeekPlayer)
        // =====================================================================

        [Header("Behavior / AI")]
        [Tooltip("Movement / engagement pattern. Defines which AI system handles this enemy. (M8)")]
        public EnemyAIPattern aiPattern = EnemyAIPattern.SeekPlayer;

        [Tooltip("Distance at which the AI activates / starts pursuing. Outside this, enemy may idle / patrol. (M8)")]
        [Min(0f)]
        public float aggroRange = 30f;

        [Tooltip("Range at which AI attempts a special attack (ranged shot, charge, etc). 0 = melee/contact only. (M8)")]
        [Min(0f)]
        public float attackRange = 1.5f;

        [Tooltip("Cooldown (seconds) between special attacks. (M8)")]
        [Min(0f)]
        public float attackCooldown = 1f;

        [Tooltip("Damage per ranged/special attack (separate from contact damage). (M8)")]
        [Min(0f)]
        public float specialAttackDamage = 5f;

        [Tooltip("If aiPattern requires a projectile (Ranged), this is the projectile prefab to fire. (M8)")]
        public GameObject specialAttackProjectile;


        // =====================================================================
        // VFX (M9 — visual polish)
        // =====================================================================

        [Header("VFX")]
        [Tooltip("Particle effect spawned when this enemy dies. (M9)")]
        public GameObject deathVfxPrefab;

        [Tooltip("Particle effect spawned each time this enemy takes damage (small flash / hit reaction). (M9)")]
        public GameObject hitReactionVfxPrefab;

        [Tooltip("Particle effect played continuously on the enemy (idle aura, e.g. boss aura). (M9)")]
        public GameObject persistentAuraVfxPrefab;


        // =====================================================================
        // AUDIO (M10 — Wwise integration)
        // =====================================================================

        [Header("Audio (Wwise event names)")]
        [Tooltip("Wwise event posted on enemy spawn. (M10)")]
        public string spawnSoundEvent = "";

        [Tooltip("Wwise event posted on death. (M10)")]
        public string deathSoundEvent = "";

        [Tooltip("Wwise event posted each time damage is taken. (M10)")]
        public string hitSoundEvent = "";

        [Tooltip("Wwise event posted when this enemy executes its special attack. (M10)")]
        public string attackSoundEvent = "";

        [Tooltip("Wwise event posted on a footstep (driven by animation event). Empty = silent footsteps. (M10)")]
        public string footstepSoundEvent = "";


        // =====================================================================
        // UI / BESTIARY METADATA (M?, low priority)
        // =====================================================================

        [Header("UI / Metadata")]
        [Tooltip("Sprite icon for HUD threat indicators, bestiary screen, post-game stats. (M?)")]
        public Sprite icon;

        [Tooltip("Designer-facing description for the bestiary / lore reference.")]
        [TextArea(2, 5)]
        public string description = "";

        [Tooltip("Difficulty tier — used by spawn system to scale credit/XP rewards and music intensity. 1=trivial, 5=elite. (M?)")]
        [Range(1, 5)]
        public int threatLevel = 1;


        // =====================================================================
        // BOSS-SPECIFIC (only relevant when isBoss = true)
        // =====================================================================

        [Header("Boss (only when isBoss = true)")]
        [Tooltip("Title shown in the boss intro card / health bar.")]
        public string bossTitle = "";

        [Tooltip("Wwise event for the boss music track. Posted on boss spawn, stopped on death.")]
        public string bossMusicEvent = "";

        [Tooltip("Health-fraction thresholds at which the boss enters new phases. e.g. [0.66, 0.33] = 3-phase boss. Empty = single phase.")]
        public float[] phaseHealthThresholds = new float[0];

        [Tooltip("Per-phase ability identifiers. Length should match phaseHealthThresholds.Length + 1 (includes phase 0). (M? boss system)")]
        public string[] phaseAbilityIds = new string[0];


        // =====================================================================
        // RUNTIME HELPERS
        // =====================================================================

        /// <summary>
        /// Stable integer hash of <see cref="enemyId"/> — used as the key in the
        /// <c>EnemyPrefabRegistry</c> singleton. Burst-friendly (just an int).
        /// </summary>
        public int GetIdHash()
        {
            return Animator.StringToHash(enemyId);
        }

        private void OnValidate()
        {
            // Light schema sanity checking — designers see these as console
            // warnings while editing the SO.
            if (string.IsNullOrWhiteSpace(enemyId))
                Debug.LogWarning($"[EnemyData] '{name}' has empty enemyId — registry key will collide with other empty-id enemies.", this);

            if (visualPrefab == null)
                Debug.LogWarning($"[EnemyData] '{name}' has no visualPrefab assigned — entity will have no visible representation.", this);

            if (isBoss && phaseHealthThresholds != null && phaseAbilityIds != null
                && phaseAbilityIds.Length != phaseHealthThresholds.Length + 1)
            {
                Debug.LogWarning($"[EnemyData] Boss '{name}': phaseAbilityIds.Length should be phaseHealthThresholds.Length + 1 (one ability set per phase, including phase 0).", this);
            }
        }
    }


    // =========================================================================
    // SUPPORTING TYPES
    // =========================================================================

    /// <summary>
    /// Per-element damage multipliers. 1.0 = normal damage; 0.0 = immune;
    /// > 1.0 = vulnerable (extra damage taken). Designer-tunable per enemy.
    /// </summary>
    [Serializable]
    public struct ElementResistances
    {
        [Range(0f, 2f)] public float physical;
        [Range(0f, 2f)] public float fire;
        [Range(0f, 2f)] public float ice;
        [Range(0f, 2f)] public float electric;
        [Range(0f, 2f)] public float energy;
        [Range(0f, 2f)] public float poison;

        /// <summary>Default = 1.0 multiplier for all elements (no resistance, no vulnerability).</summary>
        public static ElementResistances Default => new ElementResistances
        {
            physical = 1f,
            fire = 1f,
            ice = 1f,
            electric = 1f,
            energy = 1f,
            poison = 1f,
        };
    }

    /// <summary>
    /// Visual classification — drives Animator branching (which walk / run
    /// / death state plays for this enemy). Stored as an int on the entity
    /// so the Animator's "EnemyType" parameter can match the value directly.
    /// Extend with new entries as new visual archetypes ship.
    /// </summary>
    public enum EnemyVisualType
    {
        /// <summary>Default-sized humanoid (e.g. zombie, skeleton, cyborg trooper). Death variants 0–1.</summary>
        StandardHumanoid = 0,
        /// <summary>Larger humanoid (e.g. mutant, brute, ogre). Death variant 2.</summary>
        BigHumanoid = 1,
        // Future: Drone = 2, Quadruped = 3, Flyer = 4, etc.
    }

    /// <summary>
    /// AI behavior pattern for an enemy. The AI system reads this to dispatch
    /// to the right movement / attack logic. (M8 expansion)
    /// </summary>
    public enum EnemyAIPattern
    {
        /// <summary>Walk in a straight line toward the player. (M5 default — only one currently implemented)</summary>
        SeekPlayer,
        /// <summary>Pursue at distance, stop at attack range, fire projectile, repeat.</summary>
        Ranged,
        /// <summary>Long-range telegraph then fast charge in straight line through player position.</summary>
        Charge,
        /// <summary>Doesn't move; fires projectiles or has aura. Turret-like.</summary>
        Stationary,
        /// <summary>Moves in a fixed pattern (figure-8, circle around point) ignoring player position.</summary>
        Patrol,
        /// <summary>Groups with nearby allies; movement is influenced by neighbors (boids-like).</summary>
        Swarm,
        /// <summary>Avoids the player when within aggroRange — breaks line-of-sight. (e.g., glass-cannon ranged enemy)</summary>
        Flee,
        /// <summary>Custom AI handled by a dedicated system — used for unique bosses.</summary>
        Special,
    }

    /// <summary>
    /// Per-enemy XP drop probabilities. The kill rolls a single random
    /// number 0..1 and walks the cascade from highest tier down — first
    /// threshold crossed wins. T0 (Data Fragment) is the implicit fallback
    /// when no higher tier triggers, so something always drops.
    ///
    /// Bosses ignore the cascade and spawn `BossMultiDropCount` Tier 4 gems
    /// in a circle around the body for spectacle.
    /// </summary>
    [Serializable]
    public struct XPDropTable
    {
        [Tooltip("Chance (0–1) of dropping Tier 1 (Code Crystal, 3 XP).")]
        [Range(0f, 1f)] public float tier1Chance;

        [Tooltip("Chance (0–1) of dropping Tier 2 (Neural Shard, 10 XP).")]
        [Range(0f, 1f)] public float tier2Chance;

        [Tooltip("Chance (0–1) of dropping Tier 3 (Synth Spark, 30 XP).")]
        [Range(0f, 1f)] public float tier3Chance;

        [Tooltip("Chance (0–1) of dropping Tier 4 (Sentinel Core, 100 XP).")]
        [Range(0f, 1f)] public float tier4Chance;

        [Tooltip("Bosses spawn this many Tier 4 gems in a burst around their body, on top of the normal cascade. 0 = no burst (non-boss enemies).")]
        [Min(0)] public int bossMultiDropCount;

        // ─── Convenient defaults for designers to start from ───

        /// <summary>Trash-mob baseline: 0 / 0 / 2% / 10% / 88%.</summary>
        public static XPDropTable DefaultTrash => new XPDropTable
        {
            tier4Chance = 0f,
            tier3Chance = 0.005f,
            tier2Chance = 0.02f,
            tier1Chance = 0.10f,
            bossMultiDropCount = 0,
        };

        /// <summary>Mid-tier humanoid (skeleton / mutant): more upward bias.</summary>
        public static XPDropTable DefaultMidTier => new XPDropTable
        {
            tier4Chance = 0f,
            tier3Chance = 0.03f,
            tier2Chance = 0.08f,
            tier1Chance = 0.25f,
            bossMultiDropCount = 0,
        };

        /// <summary>Big mutant / elite: occasional T4, lots of T2-T3.</summary>
        public static XPDropTable DefaultElite => new XPDropTable
        {
            tier4Chance = 0.03f,
            tier3Chance = 0.10f,
            tier2Chance = 0.25f,
            tier1Chance = 0.40f,
            bossMultiDropCount = 0,
        };

        /// <summary>Boss: 100% T4 + multi-drop burst.</summary>
        public static XPDropTable DefaultBoss => new XPDropTable
        {
            tier4Chance = 1f,
            tier3Chance = 0f,
            tier2Chance = 0f,
            tier1Chance = 0f,
            bossMultiDropCount = 8,
        };
    }

    /// <summary>
    /// One entry in an enemy's loot table. Per-roll independent probability.
    /// </summary>
    [Serializable]
    public class LootDrop
    {
        [Tooltip("The equipment / item that may drop.")]
        public EquipmentData item;

        [Tooltip("Independent probability (0–100%) that this drop fires when the loot table is rolled.")]
        [Range(0f, 100f)]
        public float chance = 10f;

        [Tooltip("Minimum quantity if the drop fires.")]
        [Min(1)]
        public int minQuantity = 1;

        [Tooltip("Maximum quantity if the drop fires (inclusive).")]
        [Min(1)]
        public int maxQuantity = 1;
    }
}

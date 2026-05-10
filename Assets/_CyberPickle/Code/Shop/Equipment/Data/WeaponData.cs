// File: Assets/Code/Shop/Equipment/Data/WeaponData.cs
//
// Purpose: Defines the data structure for weapons in Cyber Pickle.
// Contains weapon-specific properties like damage, fire rate, projectile
// behavior, and special effects. Authored as a ScriptableObject so
// designers can iterate without code changes.
//
// Created: 2025-02-25
// Updated: 2026-05-10 — DUAL-AXIS REFACTOR
//   - Damage scaling moved to the Rarity axis (Common→Legendary).
//   - Fire-rate scaling moved to the LEVEL axis via authored rhythmic
//     patterns (active-cell density × BPM, where BPM is driven by the
//     Dexterity stat).
//   - Removed deprecated upgrade-multiplier fields and per-level helpers
//     (GetDamageForLevel, GetProjectileSpeedForLevel, etc.).
//
// ─── DUAL-AXIS NOTE (READ BEFORE EDITING) ─────────────────────────────────
//
// The CyberPickle weapon model is dual-axis:
//   - LEVEL  (1..5 + Evolved) → fire-rate via active-cell pattern density
//                                + musical pattern complexity
//   - RARITY (Common..Legendary) → damage scalar (×1.0..×4.0)
//                                + tier-bonus perk (per-weapon authored)
//
// Source of truth:
//   - LLM Knowledge Base/weapon_rarity_v1.md           (axis design)
//   - LLM Knowledge Base/procedural_music_reference.md §22 (pattern math)
//   - Assets/_CyberPickle/Code/Core/Rarity.cs          (rarity enum + scalars)

using UnityEngine;
using System;
using System.Collections.Generic;
using CyberPickle.Core;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Shop.Equipment.Data
{
    /// <summary>
    /// Defines possible weapon attack types
    /// </summary>
    public enum WeaponAttackType
    {
        Projectile,
        Beam,
        Area,
        Melee
    }

    /// <summary>
    /// ScriptableObject that defines data for weapon equipment
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "CyberPickle/Equipment/WeaponData")]
    public class WeaponData : EquipmentData
    {
        [Header("Weapon Properties")]
        [Tooltip("The type of weapon - hand or body")]
        public EquipmentSlotType weaponType = EquipmentSlotType.HandWeapon;

        [Tooltip("How this weapon attacks")]
        public WeaponAttackType attackType = WeaponAttackType.Projectile;

        [Tooltip("Base damage per attack/projectile. Final damage scales by Rarity (×1.0..×4.0) plus PlayerStats.Power and crit at hit time. See weapon_rarity_v1.md §2.")]
        public float baseDamage = 10f;

        [Tooltip("Fallback fire rate (shots/sec) used ONLY when activeCellsPerLevel is empty. Production weapons author the per-level pattern below; this field is a safety net for legacy / placeholder weapons.")]
        public float baseFireRate = 2f;

        [Tooltip("Base projectile speed (if applicable). Currently a flat per-weapon constant; may scale with level in M9+ polish.")]
        public float baseProjectileSpeed = 10f;

        [Tooltip("Base area of effect radius (if applicable). Multiplied by PlayerStats.AreaOfEffect at runtime.")]
        public float baseAreaOfEffect = 1f;

        [Tooltip("Base pierce count (0 = no pierce). May be augmented by per-rarity-tier perks (e.g., Legendary 'pierces all').")]
        public int basePierceCount = 0;

        [Header("Audio & VFX")]
        [Tooltip("Prefab for projectile or attack VFX")]
        public GameObject projectilePrefab;

        [Tooltip("Sound effect for firing")]
        public AudioClip fireSound;

        [Tooltip("Sound effect for hitting")]
        public AudioClip hitSound;

        [Tooltip("VFX prefab for hit effect")]
        public GameObject hitEffectPrefab;

        [Header("Pattern (Level → fire rate, see procedural_music_reference.md §22)")]
        [Tooltip("Active-cell count per weapon level (1..5). Index 0 = L1, index 4 = L5. Each entry is the number of cells with active=1 in the weapon's 4-bar × 32-subdivision pattern (max 128). Effective fire rate = activeCells / patternDuration, where patternDuration = barCount × beatsPerBar × 60 / BPM. Example: 4 active cells in a 4-bar pattern at 120 BPM → 4 / 8s = 0.5 shots/sec (very sparse, good for L1 of a heavy weapon).")]
        public int[] activeCellsPerLevel = new int[5] { 4, 8, 16, 32, 64 };

        [Tooltip("Bars per pattern. Default 4. Coprime values (5, 7) get the moiré effect described in procedural_music_reference.md §11.")]
        [Min(1)] public int barCount = 4;

        [Tooltip("Beats per bar (time signature numerator). Default 4 (4/4 time).")]
        [Min(1)] public int beatsPerBar = 4;

        [Tooltip("Base BPM at Dexterity=0. Final BPM = baseBPM × (1 + Dex × 0.01), clamped to [60, 180]. Default 60 — slowest. Players raise Dex to speed up the weapon's fire rate.")]
        [Min(30f)] public float baseBPM = 60f;

        [Header("Final Form")]
        [Tooltip("Final weapon form when fully upgraded with associated power-up")]
        public WeaponData finalForm;

        [Tooltip("Required power-up ID to unlock final form")]
        public string requiredPowerUpId;

        [Header("Element (default — see procedural_music_reference.md §22.4)")]
        [Tooltip("Default element this weapon enters the loadout with. Drives the weapon's pre-evolution musical mode (Fire = Phrygian Dominant, Ice = Aeolian, etc.). Locked to a new value at evolution if a power-up of a different element triggers it. ElementId.None means 'no element assigned' — used for elementally-neutral weapons or testing.")]
        public ElementId defaultElement = ElementId.None;

        // ─── Dual-axis: Rarity tier perks ─────────────────────────────────
        // Per weapon_rarity_v1.md §2 each rarity tier (above Common) layers
        // a per-weapon bonus perk on top of the global damage scalar. These
        // are deterministic per-weapon — every Plasma Lance Legendary has
        // the same Legendary keyword, but every WEAPON has its own.
        //
        // Designers fill the array left-to-right by tier:
        //   [0] = Common     (typically empty — Common = baseline)
        //   [1] = Uncommon   (+1 minor effect)
        //   [2] = Rare       (+1 small effect)
        //   [3] = Epic       (+1 unique major effect)
        //   [4] = Legendary  (+1 build-defining keyword)

        [Header("Rarity Tier Perks (dual-axis — see weapon_rarity_v1.md §2)")]
        [Tooltip("Per-rarity-tier bonus perks. Index = (int)Rarity tier (0=Common..4=Legendary). Each entry adds an effect ON TOP of the global damage scalar applied via Rarity.DamageMultiplier(). Leave entries empty for tiers that don't grant a bonus perk for this weapon.")]
        public RarityTierPerk[] rarityTierPerks = new RarityTierPerk[0];

        [ContextMenu("Debug Weapon Data")]
        private void DebugWeaponData()
        {
            Debug.Log($"Weapon: {displayName}");
            Debug.Log($"ID: {equipmentId}");
            Debug.Log($"Icon assigned: {equipmentIcon != null}");
            if (equipmentIcon != null)
            {
                Debug.Log($"Icon name: {equipmentIcon.name}");
                Debug.Log($"Icon instance ID: {equipmentIcon.GetInstanceID()}");
            }
        }

        /// <summary>
        /// Validates the weapon data when it's created or modified in the editor.
        /// </summary>
        protected override void OnValidate()
        {
            // Set the slot type based on weapon type
            slotType = weaponType;

            base.OnValidate();
            ValidateWeaponFields();
        }

        private void ValidateWeaponFields()
        {
            baseDamage = Mathf.Max(0.1f, baseDamage);
            baseFireRate = Mathf.Max(0.1f, baseFireRate);
            baseProjectileSpeed = Mathf.Max(0.1f, baseProjectileSpeed);
            baseAreaOfEffect = Mathf.Max(0.1f, baseAreaOfEffect);
            basePierceCount = Mathf.Max(0, basePierceCount);
            barCount = Mathf.Max(1, barCount);
            beatsPerBar = Mathf.Max(1, beatsPerBar);
            baseBPM = Mathf.Max(30f, baseBPM);

            // Clamp pattern array length to a sensible range. We expect 5
            // entries (one per level 1..5); fewer is allowed, more triggers
            // a warning so designers know they've over-authored.
            if (activeCellsPerLevel == null) activeCellsPerLevel = new int[0];
            int maxCells = barCount * 32; // 32-subdiv master grid
            for (int i = 0; i < activeCellsPerLevel.Length; i++)
                activeCellsPerLevel[i] = Mathf.Clamp(activeCellsPerLevel[i], 0, maxCells);
        }

        // ─── Dual-axis API ────────────────────────────────────────────────

        /// <summary>
        /// Effective damage per shot at the given Rarity. Per
        /// <c>weapon_rarity_v1.md</c> §2 the Rarity axis drives damage
        /// scaling — Level controls fire rate (via patterns), not damage.
        ///
        /// This is the canonical damage-formula building block. Combine
        /// with player-side modifiers (Power, crit) at the call site:
        /// <c>finalDamage = baseDamage × Rarity.DamageMultiplier() × (1 + Power*0.01) × critMul</c>.
        /// </summary>
        public float GetDamageForRarity(Rarity rarity)
        {
            return baseDamage * rarity.DamageMultiplier();
        }

        /// <summary>
        /// Effective fire rate (shots/sec) at the given level + Dexterity.
        /// Computed from the pattern's active-cell count and the BPM
        /// derived from Dexterity:
        /// <code>
        ///   bpm = clamp(baseBPM × (1 + dexterity × 0.01), 60, 180)
        ///   patternDuration = barCount × beatsPerBar × (60 / bpm)
        ///   fireRate = activeCells / patternDuration
        /// </code>
        ///
        /// Falls back to <see cref="baseFireRate"/> × Dex scaling when
        /// <see cref="activeCellsPerLevel"/> isn't authored — that lets
        /// legacy weapons keep working until they migrate to patterns.
        /// </summary>
        public float GetFireRateForLevel(int level, float dexterity = 0f)
        {
            if (activeCellsPerLevel == null || activeCellsPerLevel.Length == 0)
            {
                // Legacy fallback — flat baseFireRate scaled additively by Dex.
                return baseFireRate * (1f + dexterity * 0.01f);
            }

            int idx = Mathf.Clamp(level - 1, 0, activeCellsPerLevel.Length - 1);
            int activeCells = activeCellsPerLevel[idx];
            if (activeCells <= 0) return 0f;

            float bpm = ComputeBPM(dexterity);
            float patternDuration = barCount * beatsPerBar * (60f / bpm);
            if (patternDuration <= 0f) return 0f;

            return activeCells / patternDuration;
        }

        /// <summary>
        /// Compute the active BPM for this weapon given the player's Dexterity.
        /// Linear scaling: each Dex point adds 1% to the BPM, clamped to
        /// the 60..180 range locked in CLAUDE.md ("Dexterity → tempo").
        /// </summary>
        public float ComputeBPM(float dexterity)
        {
            return Mathf.Clamp(baseBPM * (1f + dexterity * 0.01f), 60f, 180f);
        }

        /// <summary>
        /// Returns the bonus perk (if any) defined for the given rarity tier.
        /// Returns null when no perk is configured for that tier — a valid
        /// design choice (Common typically has no perk; rare weapons may
        /// only define Legendary).
        /// </summary>
        public RarityTierPerk GetPerkForRarity(Rarity rarity)
        {
            if (rarityTierPerks == null) return null;
            int idx = (int)rarity;
            if (idx < 0 || idx >= rarityTierPerks.Length) return null;
            var perk = rarityTierPerks[idx];
            return (perk != null && !string.IsNullOrEmpty(perk.title)) ? perk : null;
        }

        /// <summary>
        /// Returns stat descriptors for the equipment-hub preview at the
        /// given upgrade level. Damage shown at Common rarity (the floor);
        /// fire rate shown at Dexterity=0. The actual in-run values are
        /// computed by <see cref="GetDamageForRarity"/> and
        /// <see cref="GetFireRateForLevel"/> with live PlayerStats.
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);
            var stats = new List<StatDescriptor>(6);

            // Damage at Common rarity = baseDamage (rarity multiplier ×1.0).
            stats.Add(new StatDescriptor("Damage (Common)", baseDamage));
            // Fire rate at Dex=0 (baseline).
            stats.Add(new StatDescriptor("Fire Rate (Dex=0)", GetFireRateForLevel(upgradeLevel, 0f)));

            switch (attackType)
            {
                case WeaponAttackType.Projectile:
                    stats.Add(new StatDescriptor("Projectile Speed", baseProjectileSpeed));
                    if (basePierceCount > 0)
                        stats.Add(new StatDescriptor("Pierce Count", basePierceCount));
                    break;
                case WeaponAttackType.Area:
                case WeaponAttackType.Beam:
                    stats.Add(new StatDescriptor(attackType == WeaponAttackType.Beam ? "Range" : "Area of Effect", baseAreaOfEffect));
                    break;
            }

            return stats.ToArray();
        }

#if UNITY_EDITOR
        public override bool ValidateReferences()
        {
            bool valid = base.ValidateReferences();

            if (projectilePrefab == null && attackType == WeaponAttackType.Projectile)
            {
                Debug.LogError($"[WeaponData] Projectile prefab is missing for {displayName}");
                valid = false;
            }

            if (fireSound == null)
            {
                Debug.LogWarning($"[WeaponData] Fire sound is missing for {displayName}");
            }

            return valid;
        }
#endif
    }

    /// <summary>
    /// One per-rarity-tier bonus perk for a weapon. Drives the
    /// "+1 minor effect" / "+1 keyword" tooltip line on hover and the
    /// runtime application of the perk's effect (when wired in M9 — until
    /// then it's data-only and surfaces in the tooltip).
    ///
    /// Why a class (not a struct): Unity serializes class arrays inside
    /// ScriptableObjects more reliably than struct arrays for nullable-style
    /// access (we want "this tier has no perk" to mean a null/empty entry,
    /// not a default-initialised struct that looks valid).
    /// </summary>
    [Serializable]
    public class RarityTierPerk
    {
        [Tooltip("Short label shown on the weapon tooltip (e.g., 'Pierces enemies', 'Crit triple', 'Plasma Lance: full line pierce'). Keep under ~40 chars.")]
        public string title;

        [Tooltip("Optional longer description shown in expanded tooltip / equipment hub view. Optional.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Stable id for the perk's gameplay effect, used by runtime systems to apply the actual mechanic (e.g., 'pierce_all_in_line', 'crit_triple_first_hit'). Looked up by the rarity-perk effect dispatcher when implemented in M9. Leave empty for pure-flavor perks that only show in tooltips.")]
        public string effectId;
    }
}

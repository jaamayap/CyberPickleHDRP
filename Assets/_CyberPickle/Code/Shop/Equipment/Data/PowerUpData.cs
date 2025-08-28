// File: Assets/Code/Shop/Equipment/Data/PowerUpData.cs
//
// Purpose: Defines the data structure for power-ups in Cyber Pickle.
// Contains power-up effects and their scaling with upgrade levels.
// Supports synergies with weapons to unlock final weapon forms.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using System;
using System.Collections.Generic;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Shop.Equipment.Data
{
    /// <summary>
    /// Defines the type of effect a power-up provides
    /// </summary>
    public enum PowerUpEffectType
    {
        StatBoost,      // Directly boosts a character stat
        WeaponBoost,    // Enhances weapon performance
        SpecialEffect,  // Provides a special gameplay effect
        DefensiveAbility, // Provides a defensive ability
        PassiveAbility  // Provides a passive ability
    }

    /// <summary>
    /// Scriptable Object that defines data for power-up equipment
    /// </summary>
    [CreateAssetMenu(fileName = "PowerUp", menuName = "CyberPickle/Equipment/PowerUpData")]
    public class PowerUpData : EquipmentData
    {
        [Header("Power-Up Properties")]
        [Tooltip("The type of effect this power-up provides")]
        public PowerUpEffectType effectType = PowerUpEffectType.StatBoost;

        [Tooltip("Duration of the effect in seconds (0 = permanent)")]
        public float baseDuration = 0f;

        [Tooltip("Cooldown before the power-up can be activated again (0 = passive effect)")]
        public float baseCooldown = 0f;

        [Tooltip("Is this power-up automatically activated or manually triggered?")]
        public bool isPassive = true;

        [Header("Stat Boosts")]
        [Tooltip("The stat this power-up affects (if applicable)")]
        public string affectedStat = "Power";

        [Tooltip("Base amount to boost the stat by")]
        public float baseStatBoost = 10f;

        [Tooltip("Is the stat boost a flat value (false) or percentage (true)?")]
        public bool isPercentageBased = true;

        [Header("Weapon Enhancement")]
        [Tooltip("Compatible weapon types for enhanced effects")]
        public EquipmentSlotType[] compatibleWeaponTypes;

        [Tooltip("Weapon IDs that have special synergy with this power-up")]
        public string[] synergisticWeaponIds;

        [Tooltip("Weapon properties affected by this power-up")]
        public string[] affectedWeaponProperties;

        [Tooltip("Base multiplier for weapon enhancement")]
        public float baseWeaponEnhancementMultiplier = 1.2f;

        [Header("Special Effects")]
        [Tooltip("Description of special effects for level 1")]
        [TextArea(2, 5)]
        public string baseEffectDescription;

        [Tooltip("Description of special effects for max level")]
        [TextArea(2, 5)]
        public string maxLevelEffectDescription;

        [Header("Visual & Audio")]
        [Tooltip("VFX prefab for when the power-up is active")]
        public GameObject activeEffectPrefab;

        [Tooltip("Sound effect for activation")]
        public AudioClip activationSound;

        [Header("Upgrade Scaling")]
        [Tooltip("Effect strength increase per level (multiplier)")]
        [Range(1f, 2f)]
        public float effectStrengthUpgradeMultiplier = 1.2f;

        [Tooltip("Duration increase per level (multiplier)")]
        [Range(1f, 1.5f)]
        public float durationUpgradeMultiplier = 1.15f;

        [Tooltip("Cooldown reduction per level (multiplier)")]
        [Range(0.5f, 1f)]
        public float cooldownReductionMultiplier = 0.9f;

        /// <summary>
        /// Validates the power-up data when it's created or modified in the editor.
        /// </summary>
        protected override void OnValidate()
        {
            // Ensure correct slot type
            slotType = EquipmentSlotType.PowerUp;

            base.OnValidate();

            // Additional power-up specific validation
            ValidatePowerUpFields();
        }

        /// <summary>
        /// Validates power-up specific fields
        /// </summary>
        private void ValidatePowerUpFields()
        {
            // Ensure base values are valid
            baseDuration = Mathf.Max(0f, baseDuration);
            baseCooldown = Mathf.Max(0f, baseCooldown);
            baseStatBoost = Mathf.Max(0f, baseStatBoost);
            baseWeaponEnhancementMultiplier = Mathf.Max(1f, baseWeaponEnhancementMultiplier);

            // Ensure scaling multipliers are in valid ranges
            effectStrengthUpgradeMultiplier = Mathf.Max(1f, effectStrengthUpgradeMultiplier);

            if (baseDuration > 0)
                durationUpgradeMultiplier = Mathf.Max(1f, durationUpgradeMultiplier);

            if (baseCooldown > 0)
                cooldownReductionMultiplier = Mathf.Clamp(cooldownReductionMultiplier, 0.5f, 1f);
        }

        /// <summary>
        /// Gets the power-up's effect strength for a specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the power-up</param>
        /// <returns>The effect strength value at that level</returns>
        public float GetEffectStrengthForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);

            switch (effectType)
            {
                case PowerUpEffectType.StatBoost:
                    return baseStatBoost * Mathf.Pow(effectStrengthUpgradeMultiplier, level - 1);

                case PowerUpEffectType.WeaponBoost:
                    return baseWeaponEnhancementMultiplier + (0.1f * (level - 1));

                default:
                    return Mathf.Pow(effectStrengthUpgradeMultiplier, level - 1);
            }
        }

        /// <summary>
        /// Gets the power-up's duration for a specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the power-up</param>
        /// <returns>The duration in seconds at that level</returns>
        public float GetDurationForLevel(int level)
        {
            if (baseDuration <= 0) return 0f; // Permanent effect

            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseDuration * Mathf.Pow(durationUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the power-up's cooldown for a specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level of the power-up</param>
        /// <returns>The cooldown in seconds at that level</returns>
        public float GetCooldownForLevel(int level)
        {
            if (baseCooldown <= 0) return 0f; // No cooldown

            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseCooldown * Mathf.Pow(cooldownReductionMultiplier, level - 1);
        }

        /// <summary>
        /// Gets a formatted effect description for the specified level
        /// </summary>
        /// <param name="level">Upgrade level of the power-up</param>
        /// <returns>A formatted description of the effect at that level</returns>
        public string GetEffectDescriptionForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);

            // For level 1, return the base description
            if (level == 1)
                return baseEffectDescription;

            // For max level, return the max level description
            if (level == maxUpgradeLevel && !string.IsNullOrEmpty(maxLevelEffectDescription))
                return maxLevelEffectDescription;

            // For intermediate levels, generate a description based on effect type
            string description = baseEffectDescription;

            // Replace placeholders with actual values
            description = description.Replace("{strength}", GetEffectStrengthForLevel(level).ToString("F1"));
            description = description.Replace("{duration}", GetDurationForLevel(level).ToString("F1"));
            description = description.Replace("{cooldown}", GetCooldownForLevel(level).ToString("F1"));

            return description;
        }

        /// <summary>
        /// Gets the stats for the specified upgrade level
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);

            List<StatDescriptor> stats = new List<StatDescriptor>();

            // Add stats based on effect type
            switch (effectType)
            {
                case PowerUpEffectType.StatBoost:
                    stats.Add(new StatDescriptor(
                        affectedStat,
                        GetEffectStrengthForLevel(upgradeLevel),
                        isPercentageBased,
                        true
                    ));
                    break;

                case PowerUpEffectType.WeaponBoost:
                    stats.Add(new StatDescriptor(
                        "Weapon Boost",
                        (GetEffectStrengthForLevel(upgradeLevel) - 1) * 100f,
                        true,
                        true
                    ));
                    break;

                case PowerUpEffectType.SpecialEffect:
                case PowerUpEffectType.DefensiveAbility:
                case PowerUpEffectType.PassiveAbility:
                    stats.Add(new StatDescriptor(
                        "Effect Strength",
                        GetEffectStrengthForLevel(upgradeLevel) * 100f,
                        true,
                        true
                    ));
                    break;
            }

            // Add duration if applicable
            if (baseDuration > 0)
            {
                stats.Add(new StatDescriptor(
                    "Duration",
                    GetDurationForLevel(upgradeLevel),
                    false,
                    true
                ));
            }

            // Add cooldown if applicable
            if (baseCooldown > 0)
            {
                stats.Add(new StatDescriptor(
                    "Cooldown",
                    GetCooldownForLevel(upgradeLevel),
                    false,
                    false  // Lower is better for cooldown
                ));
            }

            return stats.ToArray();
        }

        /// <summary>
        /// Determines if this power-up can enhance the specified weapon
        /// </summary>
        /// <param name="weaponId">The ID of the weapon to check</param>
        /// <returns>True if compatible, false otherwise</returns>
        public bool IsCompatibleWithWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId))
                return false;

            // Check if weapon is in synergistic list
            for (int i = 0; i < synergisticWeaponIds.Length; i++)
            {
                if (synergisticWeaponIds[i] == weaponId)
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validates required references are assigned in the editor
        /// </summary>
        public override bool ValidateReferences()
        {
            bool valid = base.ValidateReferences();

            if (activationSound == null && !isPassive)
            {
                Debug.LogWarning($"[PowerUpData] Activation sound is missing for non-passive power-up {displayName}");
            }

            return valid;
        }
#endif
    }
}
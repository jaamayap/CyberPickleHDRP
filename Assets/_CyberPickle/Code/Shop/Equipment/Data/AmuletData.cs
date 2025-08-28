// File: Assets/Code/Shop/Equipment/Data/AmuletData.cs
//
// Purpose: Defines the data structure for amulet equipment in Cyber Pickle.
// Contains amulet-specific properties including luck bonuses, drop rate
// modifications, and special rare effects.
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
    /// Defines the specialty focus of an amulet
    /// </summary>
    public enum AmuletSpecialty
    {
        Fortune,      // Focuses on luck and drop rate
        Magnetism,    // Focuses on item attraction
        Recovery,     // Focuses on health recovery
        Currency,     // Focuses on currency gains
        Experience,   // Focuses on experience gains
        Resilience,   // Focuses on damage resistance
        Mining        // Enhances neural mining efficiency
    }

    /// <summary>
    /// ScriptableObject that defines data for amulet equipment
    /// </summary>
    [CreateAssetMenu(fileName = "Amulet", menuName = "CyberPickle/Equipment/AmuletData")]
    public class AmuletData : EquipmentData
    {
        [Header("Amulet Properties")]
        [Tooltip("The specialty focus of this amulet")]
        public AmuletSpecialty specialty = AmuletSpecialty.Fortune;

        [Tooltip("Base luck bonus")]
        public float baseLuckBonus = 0.1f;

        [Tooltip("Base drop rate bonus (percentage)")]
        [Range(0f, 100f)]
        public float baseDropRateBonus = 10f;

        [Tooltip("Base currency bonus (percentage)")]
        [Range(0f, 100f)]
        public float baseCurrencyBonus = 0f;

        [Tooltip("Base experience bonus (percentage)")]
        [Range(0f, 100f)]
        public float baseExperienceBonus = 0f;

        [Tooltip("Neural mining efficiency bonus (percentage)")]
        [Range(0f, 100f)]
        public float baseMiningBonus = 0f;

        [Header("Special Ability")]
        [Tooltip("Does this amulet have a special ability?")]
        public bool hasSpecialAbility = false;

        [Tooltip("Description of special ability")]
        [TextArea(2, 4)]
        public string specialAbilityDescription;

        [Tooltip("Probability of rare item drops (percentage)")]
        [Range(0f, 100f)]
        public float rareItemDropChance = 0f;

        [Header("Visual & Audio")]
        [Tooltip("VFX prefab for when the special ability activates")]
        public GameObject specialAbilityVFX;

        [Tooltip("Color of the amulet glow")]
        public Color glowColor = Color.yellow;

        [Header("Upgrade Scaling")]
        [Tooltip("Luck bonus increase per level (multiplier)")]
        [Range(1f, 2f)]
        public float luckUpgradeMultiplier = 1.2f;

        [Tooltip("Other bonuses increase per level (multiplier)")]
        [Range(1f, 1.5f)]
        public float otherBonusesUpgradeMultiplier = 1.15f;

        /// <summary>
        /// Validates the amulet data when it's created or modified in the editor.
        /// </summary>
        protected override void OnValidate()
        {
            // Ensure correct slot type
            slotType = EquipmentSlotType.Amulet;

            base.OnValidate();

            // Additional amulet-specific validation
            ValidateAmuletFields();
        }

        /// <summary>
        /// Validates amulet-specific fields
        /// </summary>
        private void ValidateAmuletFields()
        {
            baseLuckBonus = Mathf.Max(0f, baseLuckBonus);
            baseDropRateBonus = Mathf.Max(0f, baseDropRateBonus);
            baseCurrencyBonus = Mathf.Max(0f, baseCurrencyBonus);
            baseExperienceBonus = Mathf.Max(0f, baseExperienceBonus);
            baseMiningBonus = Mathf.Max(0f, baseMiningBonus);
            rareItemDropChance = Mathf.Clamp(rareItemDropChance, 0f, 100f);

            if (hasSpecialAbility && string.IsNullOrEmpty(specialAbilityDescription))
            {
                specialAbilityDescription = "Activates a special ability.";
                Debug.LogWarning($"[AmuletData] Special ability description is empty for {displayName}");
            }

            // Set default values based on specialty
            if (specialty == AmuletSpecialty.Fortune && baseLuckBonus <= 0)
            {
                baseLuckBonus = 0.1f;
                baseDropRateBonus = 10f;
            }
            else if (specialty == AmuletSpecialty.Currency && baseCurrencyBonus <= 0)
            {
                baseCurrencyBonus = 15f;
            }
            else if (specialty == AmuletSpecialty.Experience && baseExperienceBonus <= 0)
            {
                baseExperienceBonus = 15f;
            }
            else if (specialty == AmuletSpecialty.Mining && baseMiningBonus <= 0)
            {
                baseMiningBonus = 20f;
            }
        }

        /// <summary>
        /// Gets the luck bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Luck bonus at that level</returns>
        public float GetLuckBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseLuckBonus * Mathf.Pow(luckUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the drop rate bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Drop rate bonus percentage at that level</returns>
        public float GetDropRateBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseDropRateBonus * Mathf.Pow(otherBonusesUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the currency bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Currency bonus percentage at that level</returns>
        public float GetCurrencyBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseCurrencyBonus * Mathf.Pow(otherBonusesUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the experience bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Experience bonus percentage at that level</returns>
        public float GetExperienceBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseExperienceBonus * Mathf.Pow(otherBonusesUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the mining bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Mining bonus percentage at that level</returns>
        public float GetMiningBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseMiningBonus * Mathf.Pow(otherBonusesUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the rare item drop chance for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Rare item drop chance percentage at that level</returns>
        public float GetRareItemDropChanceForLevel(int level)
        {
            if (rareItemDropChance <= 0)
                return 0f;

            level = Mathf.Clamp(level, 1, maxUpgradeLevel);

            // Rare drop chance increases linearly with level
            return rareItemDropChance + (rareItemDropChance * 0.2f * (level - 1));
        }

        /// <summary>
        /// Gets the stats for the specified upgrade level
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);

            List<StatDescriptor> stats = new List<StatDescriptor>();

            // Add stats based on specialty
            switch (specialty)
            {
                case AmuletSpecialty.Fortune:
                    stats.Add(new StatDescriptor("Luck", GetLuckBonusForLevel(upgradeLevel)));
                    stats.Add(new StatDescriptor("Drop Rate", GetDropRateBonusForLevel(upgradeLevel), true));
                    if (rareItemDropChance > 0)
                    {
                        stats.Add(new StatDescriptor("Rare Drop Chance", GetRareItemDropChanceForLevel(upgradeLevel), true));
                    }
                    break;

                case AmuletSpecialty.Magnetism:
                    stats.Add(new StatDescriptor("Magnetic Field", 0.5f * upgradeLevel));
                    stats.Add(new StatDescriptor("Item Attraction", 15f * upgradeLevel, true));
                    break;

                case AmuletSpecialty.Recovery:
                    stats.Add(new StatDescriptor("Health Regen", 0.5f * upgradeLevel));
                    stats.Add(new StatDescriptor("Health Pickup Effect", 20f * upgradeLevel, true));
                    break;

                case AmuletSpecialty.Currency:
                    stats.Add(new StatDescriptor("Neural Credits Gain", GetCurrencyBonusForLevel(upgradeLevel), true));
                    stats.Add(new StatDescriptor("CyberCoins Chance", 2f * upgradeLevel, true));
                    break;

                case AmuletSpecialty.Experience:
                    stats.Add(new StatDescriptor("Experience Gain", GetExperienceBonusForLevel(upgradeLevel), true));
                    break;

                case AmuletSpecialty.Resilience:
                    stats.Add(new StatDescriptor("Damage Reduction", 3f * upgradeLevel, true));
                    stats.Add(new StatDescriptor("Recovery Speed", 5f * upgradeLevel, true));
                    break;

                case AmuletSpecialty.Mining:
                    stats.Add(new StatDescriptor("Mining Efficiency", GetMiningBonusForLevel(upgradeLevel), true));
                    stats.Add(new StatDescriptor("Node Upgrade Discount", 5f * upgradeLevel, true));
                    break;
            }

            // Always add luck if not already added and it's greater than 0
            if (specialty != AmuletSpecialty.Fortune && baseLuckBonus > 0)
            {
                stats.Add(new StatDescriptor("Luck", GetLuckBonusForLevel(upgradeLevel)));
            }

            // Add special ability info if applicable
            if (hasSpecialAbility)
            {
                stats.Add(new StatDescriptor("Special Ability", 100f, false));
            }

            return stats.ToArray();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validates required references are assigned in the editor
        /// </summary>
        public override bool ValidateReferences()
        {
            bool valid = base.ValidateReferences();

            if (hasSpecialAbility && specialAbilityVFX == null)
            {
                Debug.LogWarning($"[AmuletData] Special ability VFX is missing for {displayName}");
            }

            return valid;
        }
#endif
    }
}

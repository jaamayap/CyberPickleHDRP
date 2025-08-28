// File: Assets/Code/Shop/Equipment/Data/ArmorData.cs
//
// Purpose: Defines the data structure for armor equipment in Cyber Pickle.
// Contains armor-specific properties like defense bonuses, special effects,
// and weight/speed impact values.
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
    /// Defines the armor's primary attribute focus
    /// </summary>
    public enum ArmorFocusType
    {
        Balanced,     // Equal focus on all attributes
        Defensive,    // Focus on defense and health
        Offensive,    // Focus on damage and attack speed
        Mobility,     // Focus on speed and magnetic field
        Utility       // Focus on luck and area of effect
    }

    /// <summary>
    /// ScriptableObject that defines data for armor equipment
    /// </summary>
    [CreateAssetMenu(fileName = "Armor", menuName = "CyberPickle/Equipment/ArmorData")]
    public class ArmorData : EquipmentData
    {
        [Header("Armor Properties")]
        [Tooltip("The focus type of this armor piece")]
        public ArmorFocusType focusType = ArmorFocusType.Balanced;

        [Tooltip("Base defense bonus")]
        public float baseDefenseBonus = 10f;

        [Tooltip("Base health bonus")]
        public float baseHealthBonus = 20f;

        [Tooltip("Speed modifier (negative values slow down the player)")]
        public float speedModifier = 0f;

        [Tooltip("Health regeneration bonus")]
        public float healthRegenBonus = 0f;

        [Header("Special Effects")]
        [Tooltip("Does this armor have a special effect?")]
        public bool hasSpecialEffect = false;

        [Tooltip("Description of special effect")]
        [TextArea(2, 4)]
        public string specialEffectDescription;

        [Tooltip("Chance to trigger the special effect (if applicable)")]
        [Range(0f, 1f)]
        public float specialEffectChance = 0.1f;

        [Tooltip("Cooldown for special effect (if applicable)")]
        public float specialEffectCooldown = 10f;

        [Header("Visual & Audio")]
        [Tooltip("Visual effect prefab when special effect activates")]
        public GameObject specialEffectVFX;

        [Tooltip("Sound effect when special effect activates")]
        public AudioClip specialEffectSound;

        [Tooltip("Override character material with this (if not null)")]
        public Material armorMaterial;

        [Header("Upgrade Scaling")]
        [Tooltip("Defense increase per level (multiplier)")]
        [Range(1f, 2f)]
        public float defenseUpgradeMultiplier = 1.2f;

        [Tooltip("Health increase per level (multiplier)")]
        [Range(1f, 2f)]
        public float healthUpgradeMultiplier = 1.15f;

        [Tooltip("Special effect chance increase per level (flat addition)")]
        [Range(0f, 0.1f)]
        public float specialEffectChanceIncrease = 0.02f;

        /// <summary>
        /// Validates the armor data when it's created or modified in the editor.
        /// </summary>
        protected override void OnValidate()
        {
            // Ensure correct slot type
            slotType = EquipmentSlotType.Armor;

            base.OnValidate();

            // Additional armor-specific validation
            ValidateArmorFields();
        }

        /// <summary>
        /// Validates armor-specific fields
        /// </summary>
        private void ValidateArmorFields()
        {
            baseDefenseBonus = Mathf.Max(0f, baseDefenseBonus);
            baseHealthBonus = Mathf.Max(0f, baseHealthBonus);

            // Speed modifier can be negative (slows down), but not too extreme
            speedModifier = Mathf.Clamp(speedModifier, -0.3f, 0.3f);

            healthRegenBonus = Mathf.Max(0f, healthRegenBonus);

            if (hasSpecialEffect)
            {
                specialEffectChance = Mathf.Clamp01(specialEffectChance);
                specialEffectCooldown = Mathf.Max(1f, specialEffectCooldown);

                if (string.IsNullOrEmpty(specialEffectDescription))
                {
                    specialEffectDescription = "Activates a special effect.";
                    Debug.LogWarning($"[ArmorData] Special effect description is empty for {displayName}");
                }
            }
        }

        /// <summary>
        /// Gets the defense bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Defense bonus at that level</returns>
        public float GetDefenseBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseDefenseBonus * Mathf.Pow(defenseUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the health bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Health bonus at that level</returns>
        public float GetHealthBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return baseHealthBonus * Mathf.Pow(healthUpgradeMultiplier, level - 1);
        }

        /// <summary>
        /// Gets the health regeneration bonus for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Health regen bonus at that level</returns>
        public float GetHealthRegenBonusForLevel(int level)
        {
            level = Mathf.Clamp(level, 1, maxUpgradeLevel);

            if (healthRegenBonus <= 0f)
                return 0f;

            // Health regen scales linearly with level
            return healthRegenBonus * (1f + (0.2f * (level - 1)));
        }

        /// <summary>
        /// Gets the special effect chance for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Special effect chance at that level</returns>
        public float GetSpecialEffectChanceForLevel(int level)
        {
            if (!hasSpecialEffect)
                return 0f;

            level = Mathf.Clamp(level, 1, maxUpgradeLevel);
            return Mathf.Clamp01(specialEffectChance + (specialEffectChanceIncrease * (level - 1)));
        }

        /// <summary>
        /// Gets the special effect cooldown for the specified upgrade level
        /// </summary>
        /// <param name="level">Upgrade level</param>
        /// <returns>Special effect cooldown at that level</returns>
        public float GetSpecialEffectCooldownForLevel(int level)
        {
            if (!hasSpecialEffect)
                return 0f;

            level = Mathf.Clamp(level, 1, maxUpgradeLevel);

            // Cooldown reduces by 10% per level (multiplicative)
            return specialEffectCooldown * Mathf.Pow(0.9f, level - 1);
        }

        /// <summary>
        /// Gets the stats for the specified upgrade level
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);

            List<StatDescriptor> stats = new List<StatDescriptor>();

            // Always add the main armor stats
            stats.Add(new StatDescriptor("Defense", GetDefenseBonusForLevel(upgradeLevel)));
            stats.Add(new StatDescriptor("Health", GetHealthBonusForLevel(upgradeLevel)));

            // Add speed modifier if not zero
            if (speedModifier != 0f)
            {
                stats.Add(new StatDescriptor("Speed", speedModifier * 100f, true, speedModifier > 0f));
            }

            // Add health regen if applicable
            if (healthRegenBonus > 0f)
            {
                stats.Add(new StatDescriptor("Health Regen", GetHealthRegenBonusForLevel(upgradeLevel)));
            }

            // Add special effect stats if applicable
            if (hasSpecialEffect)
            {
                stats.Add(new StatDescriptor("Effect Chance", GetSpecialEffectChanceForLevel(upgradeLevel) * 100f, true));
                stats.Add(new StatDescriptor("Effect Cooldown", GetSpecialEffectCooldownForLevel(upgradeLevel), false, false));
            }

            // Add additional stats based on focus type
            switch (focusType)
            {
                case ArmorFocusType.Offensive:
                    stats.Add(new StatDescriptor("Power", 5f * upgradeLevel, false, true));
                    break;

                case ArmorFocusType.Mobility:
                    if (speedModifier <= 0) // Only add if not already added above
                    {
                        stats.Add(new StatDescriptor("Speed", 3f * upgradeLevel, false, true));
                    }
                    stats.Add(new StatDescriptor("Magnetic Field", 0.2f * upgradeLevel, false, true));
                    break;

                case ArmorFocusType.Utility:
                    stats.Add(new StatDescriptor("Luck", 0.05f * upgradeLevel, false, true));
                    stats.Add(new StatDescriptor("Area of Effect", 0.1f * upgradeLevel, false, true));
                    break;
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

            if (hasSpecialEffect)
            {
                if (specialEffectVFX == null)
                {
                    Debug.LogWarning($"[ArmorData] Special effect VFX is missing for {displayName}");
                }

                if (specialEffectSound == null)
                {
                    Debug.LogWarning($"[ArmorData] Special effect sound is missing for {displayName}");
                }
            }

            return valid;
        }
#endif
    }
}

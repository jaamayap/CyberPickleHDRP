// File: Assets/Code/Shop/Equipment/Data/EquipmentData.cs
//
// Purpose: Defines the base data structure for all equipment types in Cyber Pickle.
// This ScriptableObject is the parent class for weapons, power-ups, armor, and amulets.
// Contains shared properties and functionality across all equipment types.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using System;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Shop.Equipment.Data
{
    /// <summary>
    /// Base ScriptableObject class for all equipment types in the game.
    /// Defines common properties and functions shared across different equipment.
    /// </summary>
    public abstract class EquipmentData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for the equipment")]
        public string equipmentId;

        [Tooltip("Display name shown in the UI")]
        public string displayName;

        [Tooltip("Equipment description shown in shop and inventory")]
        [TextArea(3, 5)]
        public string description;

        [Header("Equipment Type")]
        [Tooltip("Type of equipment slot this item uses")]
        public EquipmentSlotType slotType;

        [Header("Visual References")]
        [Tooltip("2D icon for UI elements")]
        public Sprite equipmentIcon;

        [Tooltip("3D model prefab for this equipment")]
        public GameObject equipmentPrefab;

        [Header("Economy")]
        [Tooltip("Base cost in Neural Credits")]
        public int neuralCreditCost;

        [Tooltip("Cost in CyberCoins (if purchasable with premium currency)")]
        public int cyberCoinCost;

        [Header("Unlock Requirements")]
        [Tooltip("If true, equipment is available from the start")]
        public bool unlockedByDefault;

        [Tooltip("Minimum player level required to unlock")]
        public int requiredPlayerLevel;

        [Tooltip("Achievement IDs required to unlock this equipment")]
        public string[] requiredAchievements;

        [Header("Upgrade Path")]
        [Tooltip("Maximum upgrade level")]
        [Range(1, 5)]
        public int maxUpgradeLevel = 5;

        /// <summary>
        /// Validates the equipment data when it's created or modified in the editor.
        /// Automatically generates an equipmentId if none is provided.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                equipmentId = $"{slotType.ToString().ToLower()}_{displayName?.ToLower().Replace(" ", "_") ?? "undefined"}";
                Debug.Log($"[EquipmentData] Auto-generated equipmentId: {equipmentId}");
            }

            ValidateFields();
        }

        /// <summary>
        /// Validates equipment fields have appropriate values
        /// </summary>
        protected virtual void ValidateFields()
        {
            neuralCreditCost = Mathf.Max(0, neuralCreditCost);
            cyberCoinCost = Mathf.Max(0, cyberCoinCost);
            requiredPlayerLevel = Mathf.Max(1, requiredPlayerLevel);
        }

        /// <summary>
        /// Gets the equipment's stats for the specified upgrade level
        /// </summary>
        /// <param name="upgradeLevel">The upgrade level to get stats for (1-5)</param>
        /// <returns>An array of stat descriptors for display</returns>
        public abstract StatDescriptor[] GetStatsForLevel(int upgradeLevel);

        /// <summary>
        /// Gets the cost to upgrade this equipment to the specified level
        /// </summary>
        /// <param name="currentLevel">Current level of the equipment</param>
        /// <param name="targetLevel">Target level to upgrade to</param>
        /// <returns>The cost in Neural Credits to upgrade</returns>
        public virtual int GetUpgradeCost(int currentLevel, int targetLevel)
        {
            if (currentLevel >= targetLevel || currentLevel < 1 || targetLevel > maxUpgradeLevel)
                return 0;

            // Base upgrade cost calculation
            int baseCost = neuralCreditCost / 2;
            float multiplier = 1.5f;

            int totalCost = 0;
            for (int level = currentLevel; level < targetLevel; level++)
            {
                totalCost += Mathf.RoundToInt(baseCost * Mathf.Pow(multiplier, level - 1));
            }

            return totalCost;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Validates required references are assigned in the editor
        /// </summary>
        public virtual bool ValidateReferences()
        {
            if (equipmentIcon == null)
            {
                Debug.LogError($"[EquipmentData] Equipment icon is missing for {displayName}");
                return false;
            }

            return true;
        }
#endif
    }

    /// <summary>
    /// Struct to describe a single stat for display purposes
    /// </summary>
    [Serializable]
    public struct StatDescriptor
    {
        /// <summary>
        /// The name of the stat
        /// </summary>
        public string statName;

        /// <summary>
        /// The value of the stat (can be raw number or percentage)
        /// </summary>
        public float value;

        /// <summary>
        /// Whether this stat should be displayed as a percentage
        /// </summary>
        public bool isPercentage;

        /// <summary>
        /// Whether higher values are better (green) or worse (red)
        /// </summary>
        public bool higherIsBetter;

        /// <summary>
        /// Creates a new stat descriptor
        /// </summary>
        public StatDescriptor(string name, float value, bool isPercentage = false, bool higherIsBetter = true)
        {
            this.statName = name;
            this.value = value;
            this.isPercentage = isPercentage;
            this.higherIsBetter = higherIsBetter;
        }

        /// <summary>
        /// Gets the formatted display value as a string
        /// </summary>
        public string GetDisplayValue()
        {
            return isPercentage ? $"{value:F1}%" : $"{value:F1}";
        }
    }
}

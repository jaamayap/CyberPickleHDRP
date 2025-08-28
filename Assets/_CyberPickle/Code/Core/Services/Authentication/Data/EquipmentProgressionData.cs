// File: Assets/Code/Core/Services/Authentication/Data/EquipmentProgressionData.cs
//
// Purpose: Defines the data structure for equipment progression tracking.
// Stores the upgrade level and other persistent stats for equipment items.
// Used by ProfileData to track permanent equipment upgrades between sessions.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using System;
using Newtonsoft.Json;

namespace CyberPickle.Core.Services.Authentication.Data
{
    /// <summary>
    /// Stores progression data for a single equipment item
    /// </summary>
    [Serializable]
    public class EquipmentProgressionData
    {
        [JsonProperty("equipmentId")]
        private string equipmentId;

        [JsonProperty("currentLevel")]
        private int currentLevel;

        [JsonProperty("purchaseTimeTicks")]
        private long purchaseTimeTicks;

        [JsonProperty("lastUpgradeTimeTicks")]
        private long lastUpgradeTimeTicks;

        [JsonProperty("timesUsed")]
        private int timesUsed;

        [JsonProperty("totalKills")]
        private int totalKills;

        [JsonProperty("totalDamage")]
        private float totalDamage;

        /// <summary>
        /// The equipment ID this progression data is for
        /// </summary>
        [JsonIgnore]
        public string EquipmentId => equipmentId;

        /// <summary>
        /// The current upgrade level of the equipment
        /// </summary>
        [JsonIgnore]
        public int CurrentLevel => currentLevel;

        /// <summary>
        /// When the equipment was first purchased
        /// </summary>
        [JsonIgnore]
        public DateTime PurchaseTime => new DateTime(purchaseTimeTicks);

        /// <summary>
        /// When the equipment was last upgraded
        /// </summary>
        [JsonIgnore]
        public DateTime LastUpgradeTime => new DateTime(lastUpgradeTimeTicks);

        /// <summary>
        /// How many times the equipment has been used
        /// </summary>
        [JsonIgnore]
        public int TimesUsed => timesUsed;

        /// <summary>
        /// How many kills the equipment has gotten
        /// </summary>
        [JsonIgnore]
        public int TotalKills => totalKills;

        /// <summary>
        /// Total damage dealt with this equipment
        /// </summary>
        [JsonIgnore]
        public float TotalDamage => totalDamage;

        /// <summary>
        /// Creates new equipment progression data
        /// </summary>
        /// <param name="equipmentId">The ID of the equipment</param>
        public EquipmentProgressionData(string equipmentId)
        {
            this.equipmentId = equipmentId;
            this.currentLevel = 1;
            this.purchaseTimeTicks = DateTime.UtcNow.Ticks;
            this.lastUpgradeTimeTicks = this.purchaseTimeTicks;
            this.timesUsed = 0;
            this.totalKills = 0;
            this.totalDamage = 0;
        }

        /// <summary>
        /// Upgrade the equipment to the specified level
        /// </summary>
        /// <param name="newLevel">The new level to upgrade to</param>
        /// <returns>True if upgraded successfully</returns>
        public bool Upgrade(int newLevel)
        {
            if (newLevel <= currentLevel)
                return false;

            currentLevel = newLevel;
            lastUpgradeTimeTicks = DateTime.UtcNow.Ticks;
            return true;
        }

        /// <summary>
        /// Record usage of this equipment
        /// </summary>
        public void RecordUse()
        {
            timesUsed++;
        }

        /// <summary>
        /// Record kills made with this equipment
        /// </summary>
        /// <param name="kills">Number of kills to add</param>
        public void RecordKills(int kills)
        {
            if (kills > 0)
                totalKills += kills;
        }

        /// <summary>
        /// Record damage dealt with this equipment
        /// </summary>
        /// <param name="damage">Amount of damage to add</param>
        public void RecordDamage(float damage)
        {
            if (damage > 0)
                totalDamage += damage;
        }
    }
}

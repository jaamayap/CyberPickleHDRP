// File: Core/Services/Authentication/Data/ProfileData.cs
//
// Purpose: Defines the data structures for player profiles, character progression,
// and mining nodes. Enhanced to support shop items, global currencies, and expanded
// equipment systems.
//
// Created: 2024-02-11
// Updated: 2024-02-24

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    /// <summary>
    /// Defines the available equipment slot types
    /// </summary>
    [Serializable]
    public enum EquipmentSlotType
    {
        /// <summary>Hand weapon slot (max 2)</summary>
        HandWeapon,
        /// <summary>Body weapon slot (max 1)</summary>
        BodyWeapon,
        /// <summary>Power-up slot (max 3)</summary>
        PowerUp,
        /// <summary>Armor slot (max 1)</summary>
        Armor,
        /// <summary>Amulet slot (max 1)</summary>
        Amulet
    }

    /// <summary>
    /// Defines the types of currency available in the game
    /// </summary>
    [Serializable]
    public enum CurrencyType
    {
        /// <summary>
        /// Primary currency earned through gameplay, destroying objects and defeating enemies
        /// </summary>
        NeuralCredits,

        /// <summary>
        /// Special cryptocurrency used for character unlocks, earned through Neural Mining
        /// </summary>
        CyberCoins
    }

    /// <summary>
    /// Represents character-specific progression data including stats, experience, and equipment
    /// </summary>
    [Serializable]
    public class CharacterProgressionData
    {
        [JsonProperty("characterId")]
        private string characterId;

        [JsonProperty("characterLevel")]
        private int characterLevel;

        [JsonProperty("experience")]
        private float experience;

        [JsonProperty("stats")]
        private Dictionary<string, float> stats;

        [JsonProperty("unlockedSkills")]
        private List<string> unlockedSkills;

        // Keep existing properties
        [JsonProperty("equippedWeaponId")]
        private string equippedWeaponId;

        [JsonProperty("equippedPowerupIds")]
        private List<string> equippedPowerupIds;

        // Add new equipment slots
        [JsonProperty("equippedHandWeapons")]
        private List<string> equippedHandWeapons;

        [JsonProperty("equippedBodyWeapon")]
        private string equippedBodyWeapon;

        [JsonProperty("equippedArmor")]
        private string equippedArmor;

        [JsonProperty("equippedAmulet")]
        private string equippedAmulet;

        // Constants defining equipment slot limits
        private const int MAX_HAND_WEAPONS = 2;
        private const int MAX_BODY_WEAPONS = 1;
        private const int MAX_POWER_UPS = 3;

        // Public Properties - keep existing ones
        [JsonIgnore] public string CharacterId => characterId;
        [JsonIgnore] public int CharacterLevel => characterLevel;
        [JsonIgnore] public float Experience => experience;
        [JsonIgnore] public IReadOnlyDictionary<string, float> Stats => stats;
        [JsonIgnore] public IReadOnlyList<string> UnlockedSkills => unlockedSkills;
        [JsonIgnore] public string EquippedWeaponId => equippedWeaponId;
        [JsonIgnore] public IReadOnlyList<string> EquippedPowerupIds => equippedPowerupIds;

        // Add new properties
        [JsonIgnore] public IReadOnlyList<string> EquippedHandWeapons => equippedHandWeapons;
        [JsonIgnore] public string EquippedBodyWeapon => equippedBodyWeapon;
        [JsonIgnore] public string EquippedArmor => equippedArmor;
        [JsonIgnore] public string EquippedAmulet => equippedAmulet;

        public CharacterProgressionData(string characterId)
        {
            this.characterId = characterId;
            InitializeCollections();
        }

        private void InitializeCollections()
        {
            characterLevel = 1;
            experience = 0f;
            stats = new Dictionary<string, float>();
            unlockedSkills = new List<string>();
            equippedPowerupIds = new List<string>();

            // Initialize new collections
            equippedHandWeapons = new List<string>(MAX_HAND_WEAPONS);
            equippedBodyWeapon = string.Empty;
            equippedArmor = string.Empty;
            equippedAmulet = string.Empty;

            // Set default stats
            InitializeDefaultStats();
        }

        /// <summary>
        /// Sets up default stats for a new character
        /// </summary>
        private void InitializeDefaultStats()
        {
            // Initialize with basic stats if stats dictionary is empty
            if (stats.Count == 0)
            {
                stats["Health"] = 100f;
                stats["Defense"] = 10f;
                stats["Power"] = 10f;
                stats["Speed"] = 5f;
                stats["MagneticField"] = 1f;
                stats["Dexterity"] = 10f;
                stats["Luck"] = 1f;
                stats["AreaOfEffect"] = 1f;
            }
        }

        #region Experience and Leveling

        /// <summary>
        /// Adds experience points to the character
        /// </summary>
        /// <param name="amount">Amount of experience to add</param>
        /// <returns>True if the character leveled up, false otherwise</returns>
        public bool AddExperience(float amount)
        {
            if (amount <= 0) return false;

            int oldLevel = characterLevel;
            experience += amount;

            // Calculate new level based on experience
            // XP needed for next level = level * 1000
            while (experience >= characterLevel * 1000)
            {
                experience -= characterLevel * 1000;
                characterLevel++;

                // When leveling up, increase stats automatically
                IncreaseLevelStats();
            }

            Debug.Log($"[CharacterProgressionData] Added {amount} XP to {characterId}. Level: {characterLevel}, XP: {experience}");
            return characterLevel > oldLevel;
        }

        /// <summary>
        /// Increases character stats when leveling up
        /// </summary>
        private void IncreaseLevelStats()
        {
            // Apply stat increases on level up
            if (stats.ContainsKey("Health")) stats["Health"] += 10f;
            if (stats.ContainsKey("Defense")) stats["Defense"] += 1f;
            if (stats.ContainsKey("Power")) stats["Power"] += 1f;

            Debug.Log($"[CharacterProgressionData] {characterId} leveled up to level {characterLevel}");
        }

        #endregion

        #region Equipment Management

        /// <summary>
        /// Equips an item in the specified slot
        /// </summary>
        /// <param name="equipmentId">The unique identifier of the equipment</param>
        /// <param name="slotType">The slot type to equip in</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool EquipItem(string equipmentId, EquipmentSlotType slotType)
        {
            if (string.IsNullOrEmpty(equipmentId))
            {
                Debug.LogError("[CharacterProgressionData] Cannot equip null or empty equipment ID");
                return false;
            }

            try
            {
                switch (slotType)
                {
                    case EquipmentSlotType.HandWeapon:
                        return EquipHandWeapon(equipmentId);

                    case EquipmentSlotType.BodyWeapon:
                        equippedBodyWeapon = equipmentId;
                        Debug.Log($"[CharacterProgressionData] Equipped body weapon: {equipmentId}");
                        return true;

                    case EquipmentSlotType.PowerUp:
                        return EquipPowerUp(equipmentId);

                    case EquipmentSlotType.Armor:
                        equippedArmor = equipmentId;
                        Debug.Log($"[CharacterProgressionData] Equipped armor: {equipmentId}");
                        return true;

                    case EquipmentSlotType.Amulet:
                        equippedAmulet = equipmentId;
                        Debug.Log($"[CharacterProgressionData] Equipped amulet: {equipmentId}");
                        return true;

                    default:
                        Debug.LogError($"[CharacterProgressionData] Unknown equipment slot type: {slotType}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterProgressionData] Error equipping item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Equips a hand weapon while respecting the maximum limit
        /// </summary>
        /// <param name="weaponId">Hand weapon ID to equip</param>
        /// <returns>True if equipped successfully</returns>
        private bool EquipHandWeapon(string weaponId)
        {
            // Check if already equipped
            if (equippedHandWeapons.Contains(weaponId))
            {
                Debug.LogWarning($"[CharacterProgressionData] Hand weapon {weaponId} is already equipped");
                return false;
            }

            // Check if at max capacity
            if (equippedHandWeapons.Count >= MAX_HAND_WEAPONS)
            {
                Debug.LogWarning($"[CharacterProgressionData] Cannot equip more than {MAX_HAND_WEAPONS} hand weapons");
                return false;
            }

            equippedHandWeapons.Add(weaponId);
            Debug.Log($"[CharacterProgressionData] Equipped hand weapon: {weaponId}");
            return true;
        }

        /// <summary>
        /// Equips a power-up while respecting the maximum limit
        /// </summary>
        /// <param name="powerUpId">Power-up ID to equip</param>
        /// <returns>True if equipped successfully</returns>
        private bool EquipPowerUp(string powerUpId)
        {
            // Check if already equipped
            if (equippedPowerupIds.Contains(powerUpId))
            {
                Debug.LogWarning($"[CharacterProgressionData] Power-up {powerUpId} is already equipped");
                return false;
            }

            // Check if at max capacity
            if (equippedPowerupIds.Count >= MAX_POWER_UPS)
            {
                Debug.LogWarning($"[CharacterProgressionData] Cannot equip more than {MAX_POWER_UPS} power-ups");
                return false;
            }

            equippedPowerupIds.Add(powerUpId);
            Debug.Log($"[CharacterProgressionData] Equipped power-up: {powerUpId}");
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Represents a single Neural Mining Node that passively generates CyberCoins
    /// </summary>
    [Serializable]
    public class MiningNodeData
    {
        [JsonProperty("nodeId")]
        private string nodeId;

        [JsonProperty("level")]
        private int level;

        [JsonProperty("baseRate")]
        private float baseRate;

        [JsonProperty("multiplier")]
        private float multiplier;

        [JsonProperty("lastMiningTimeTicks")]
        private long lastMiningTimeTicks;

        // Maximum level a node can be upgraded to
        private const int MAX_NODE_LEVEL = 10;

        // Maximum hours that mining resources can accumulate while offline
        private const float MAX_OFFLINE_HOURS = 24f;

        // Public Properties
        [JsonIgnore] public string NodeId => nodeId;
        [JsonIgnore] public int Level => level;
        [JsonIgnore] public float BaseRate => baseRate;
        [JsonIgnore] public float Multiplier => multiplier;
        [JsonIgnore] public DateTime LastMiningTime => new DateTime(lastMiningTimeTicks);

        /// <summary>
        /// Calculate the current mining rate including level bonuses and multipliers
        /// </summary>
        [JsonIgnore] public float CurrentMiningRate => baseRate * (1 + (level - 1) * 0.2f) * multiplier;

        /// <summary>
        /// How many CyberCoins have accumulated since last collection
        /// </summary>
        [JsonIgnore]
        public float PendingCoins
        {
            get
            {
                DateTime now = DateTime.UtcNow;
                TimeSpan timeSinceLastMining = now - LastMiningTime;

                // Calculate how many hours have passed (up to a maximum of MAX_OFFLINE_HOURS)
                float hoursElapsed = Mathf.Min((float)timeSinceLastMining.TotalHours, MAX_OFFLINE_HOURS);

                // Calculate earnings
                return hoursElapsed * CurrentMiningRate;
            }
        }

        public MiningNodeData(string nodeId)
        {
            this.nodeId = nodeId;
            this.level = 1;
            this.baseRate = 10f;
            this.multiplier = 1f;
            this.lastMiningTimeTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// Upgrades the mining node to the next level if possible
        /// </summary>
        /// <returns>True if the upgrade was successful, false if already at max level</returns>
        public bool Upgrade()
        {
            if (level >= MAX_NODE_LEVEL)
            {
                Debug.Log($"[MiningNodeData] Cannot upgrade node {nodeId}: already at maximum level {MAX_NODE_LEVEL}");
                return false;
            }

            level++;
            Debug.Log($"[MiningNodeData] Node {nodeId} upgraded to level {level}");
            return true;
        }

        /// <summary>
        /// Collects accumulated CyberCoins from this mining node
        /// </summary>
        /// <returns>The amount of CyberCoins collected</returns>
        public float CollectMining()
        {
            float earnings = PendingCoins;

            // Update last mining time to now
            lastMiningTimeTicks = DateTime.UtcNow.Ticks;

            Debug.Log($"[MiningNodeData] Collected {earnings} CyberCoins from node {nodeId}");
            return earnings;
        }
    }

    /// <summary>
    /// Main profile data class containing global and character-specific information
    /// </summary>
    [Serializable]
    public class ProfileData
    {
        #region Private Fields
        [JsonProperty("equipmentProgressionData")]
        private Dictionary<string, EquipmentProgressionData> equipmentProgressionData = new Dictionary<string, EquipmentProgressionData>();

        [JsonProperty("ProfileId")]
        private string profileId;

        [JsonProperty("PlayerId")]
        private string playerId;

        [JsonProperty("DisplayName")]
        private string displayName;

        [JsonProperty("IsActive")]
        private bool isActive;

        [JsonProperty("CreatedAtTicks")]
        private long createdAtTicks;

        [JsonProperty("LastLoginAtTicks")]
        private long lastLoginAtTicks;

        [JsonProperty("TotalPlayTime")]
        private float totalPlayTime;

        [JsonProperty("HighestScore")]
        private int highestScore;

        [JsonProperty("FurthestDistance")]
        private float furthestDistance;

        [JsonProperty("Level")]
        private int level;

        [JsonProperty("HasSaveData")]
        private bool hasSaveData;

        [JsonProperty("CyberCoins")]
        private float cyberCoins;

        [JsonProperty("NeuralCredits")]
        private float neuralCredits;

        [JsonProperty("CharacterProgress")]
        private Dictionary<string, CharacterProgressionData> characterProgress;

        [JsonProperty("MiningNodes")]
        private Dictionary<string, MiningNodeData> miningNodes;

        [JsonProperty("UnlockedEquipment")]
        private HashSet<string> unlockedEquipment;

        [JsonProperty("Achievements")]
        private HashSet<string> achievements;

        [JsonProperty("GlobalMultipliers")]
        private Dictionary<string, float> globalMultipliers;

        [JsonProperty("UnlockedLevels")]
        private HashSet<string> unlockedLevels;

        #endregion

        #region Public Properties

        [JsonIgnore] public string ProfileId => profileId;
        [JsonIgnore] public string PlayerId => playerId;
        [JsonIgnore] public string DisplayName => displayName;
        [JsonIgnore] public bool HasSaveData => hasSaveData;
        [JsonIgnore] public float TotalPlayTime => totalPlayTime;
        [JsonIgnore] public int HighestScore => highestScore;
        [JsonIgnore] public float FurthestDistance => furthestDistance;
        [JsonIgnore] public int Level => level;
        [JsonIgnore] public bool IsActive => isActive;
        [JsonIgnore] public DateTime CreatedAt => new DateTime(createdAtTicks);
        [JsonIgnore] public DateTime LastLoginTime => new DateTime(lastLoginAtTicks);
        [JsonIgnore] public float CyberCoins => cyberCoins;
        [JsonIgnore] public float NeuralCredits => neuralCredits;

        [JsonIgnore] public IReadOnlyDictionary<string, CharacterProgressionData> CharacterProgress => characterProgress;
        [JsonIgnore] public IReadOnlyDictionary<string, MiningNodeData> MiningNodes => miningNodes;
        [JsonIgnore] public IReadOnlyCollection<string> UnlockedEquipment => unlockedEquipment;
        [JsonIgnore] public IReadOnlyCollection<string> Achievements => achievements;
        [JsonIgnore] public IReadOnlyDictionary<string, float> GlobalMultipliers => globalMultipliers;
        [JsonIgnore] public IReadOnlyCollection<string> UnlockedLevels => unlockedLevels;

        #endregion

        #region Constructor and Initialization

        public ProfileData(string profileId, string playerId, string displayName)
        {
            this.profileId = profileId;
            this.playerId = playerId;
            this.displayName = displayName;
            this.createdAtTicks = DateTime.UtcNow.Ticks;
            this.lastLoginAtTicks = DateTime.UtcNow.Ticks;

            InitializeCollectionsIfNeeded();
            SetDefaultValues();
        }

        /// <summary>
        /// Ensures all collections are properly initialized
        /// Called after deserialization to prevent null collections
        /// </summary>
        public void InitializeCollectionsIfNeeded()
        {
            Debug.Log($"[ProfileData] Initializing collections for profile: {profileId}");

            characterProgress ??= new Dictionary<string, CharacterProgressionData>();
            miningNodes ??= new Dictionary<string, MiningNodeData>();
            unlockedEquipment ??= new HashSet<string>();
            achievements ??= new HashSet<string>();
            globalMultipliers ??= new Dictionary<string, float>();
            unlockedLevels ??= new HashSet<string>();
        }

        private void SetDefaultValues()
        {
            isActive = false;
            hasSaveData = false;
            totalPlayTime = 0f;
            highestScore = 0;
            furthestDistance = 0f;
            level = 1;
            cyberCoins = 0f;
            neuralCredits = 0f;
        }

        #endregion

        #region Public Methods

        // Keep existing methods
        public void UpdateLoginTime()
        {
            lastLoginAtTicks = DateTime.UtcNow.Ticks;
        }

        public void UpdateDisplayName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                displayName = name;
            }
        }

        public void UpdateProgress(float playTime, int score, float distance, int level)
        {
            totalPlayTime = playTime;
            if (score > highestScore) highestScore = score;
            if (distance > furthestDistance) furthestDistance = distance;
            this.level = level;
            hasSaveData = true;
        }

        public void UpdateCharacterProgress(string characterId, CharacterProgressionData progression)
        {
            InitializeCollectionsIfNeeded();
            characterProgress[characterId] = progression;
            Debug.Log($"[ProfileData] Updated character progress for {characterId} in profile {profileId}");
        }

        public void UpdateMiningNode(string nodeId, MiningNodeData nodeData)
        {
            InitializeCollectionsIfNeeded();
            miningNodes[nodeId] = nodeData;
        }

        public void UpdateCurrency(float cyberCoins, float neuralCredits)
        {
            this.cyberCoins = cyberCoins;
            this.neuralCredits = neuralCredits;
        }

        public void ClearProgress()
        {
            totalPlayTime = 0f;
            highestScore = 0;
            furthestDistance = 0f;
            level = 1;
            cyberCoins = 0f;
            neuralCredits = 0f;

            InitializeCollectionsIfNeeded();
            characterProgress.Clear();
            miningNodes.Clear();
            unlockedEquipment.Clear();
            achievements.Clear();
            globalMultipliers.Clear();
            unlockedLevels.Clear();

            hasSaveData = false;
            Debug.Log($"[ProfileData] Cleared progress for profile: {profileId}");
        }

        public void SetActive(bool active)
        {
            isActive = active;
            Debug.Log($"[ProfileData] Profile {profileId} active state set to: {active}");
        }

        // Add new methods

        #region Currency Methods

        /// <summary>
        /// Adds currency to the player's balance
        /// </summary>
        /// <param name="amount">Amount to add (must be positive)</param>
        /// <param name="type">Type of currency to add</param>
        /// <returns>True if the currency was added successfully</returns>
        public bool AddCurrency(float amount, CurrencyType type)
        {
            if (amount <= 0) return false;

            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    neuralCredits += amount;
                    Debug.Log($"[ProfileData] Added {amount} Neural Credits. New balance: {neuralCredits}");
                    return true;

                case CurrencyType.CyberCoins:
                    cyberCoins += amount;
                    Debug.Log($"[ProfileData] Added {amount} CyberCoins. New balance: {cyberCoins}");
                    return true;

                default:
                    Debug.LogError($"[ProfileData] Unknown currency type: {type}");
                    return false;
            }
        }

        /// <summary>
        /// Attempts to spend currency from the player's balance
        /// </summary>
        /// <param name="amount">Amount to spend</param>
        /// <param name="type">Type of currency to spend</param>
        /// <returns>True if sufficient funds were available and spent, false otherwise</returns>
        public bool SpendCurrency(float amount, CurrencyType type)
        {
            if (amount <= 0) return false;

            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    if (neuralCredits >= amount)
                    {
                        neuralCredits -= amount;
                        Debug.Log($"[ProfileData] Spent {amount} Neural Credits. Remaining balance: {neuralCredits}");
                        return true;
                    }
                    Debug.Log($"[ProfileData] Insufficient Neural Credits. Required: {amount}, Available: {neuralCredits}");
                    return false;

                case CurrencyType.CyberCoins:
                    if (cyberCoins >= amount)
                    {
                        cyberCoins -= amount;
                        Debug.Log($"[ProfileData] Spent {amount} CyberCoins. Remaining balance: {cyberCoins}");
                        return true;
                    }
                    Debug.Log($"[ProfileData] Insufficient CyberCoins. Required: {amount}, Available: {cyberCoins}");
                    return false;

                default:
                    Debug.LogError($"[ProfileData] Unknown currency type: {type}");
                    return false;
            }
        }

        #endregion

        #region Equipment Methods

        /// <summary>
        /// Unlocks a piece of equipment for use by any character
        /// </summary>
        /// <param name="equipmentId">The unique identifier of the equipment</param>
        /// <returns>True if newly unlocked, false if already unlocked</returns>
        public bool UnlockEquipment(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return false;

            InitializeCollectionsIfNeeded();

            if (unlockedEquipment.Contains(equipmentId))
            {
                Debug.LogWarning($"[ProfileData] Equipment {equipmentId} is already unlocked");
                return false;
            }

            unlockedEquipment.Add(equipmentId);
            Debug.Log($"[ProfileData] Unlocked equipment: {equipmentId}");
            return true;
        }

        /// <summary>
        /// Checks if a piece of equipment is unlocked
        /// </summary>
        /// <param name="equipmentId">The equipment ID to check</param>
        /// <returns>True if unlocked, false otherwise</returns>
        public bool IsEquipmentUnlocked(string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId)) return false;

            InitializeCollectionsIfNeeded();
            return unlockedEquipment.Contains(equipmentId);
        }

        #endregion

        #region Level Methods

        /// <summary>
        /// Unlocks a level for play
        /// </summary>
        /// <param name="levelId">The unique identifier of the level</param>
        /// <returns>True if newly unlocked, false if already unlocked</returns>
        public bool UnlockLevel(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return false;

            InitializeCollectionsIfNeeded();

            if (unlockedLevels.Contains(levelId))
            {
                Debug.LogWarning($"[ProfileData] Level {levelId} is already unlocked");
                return false;
            }

            unlockedLevels.Add(levelId);
            Debug.Log($"[ProfileData] Unlocked level: {levelId}");
            return true;
        }

        /// <summary>
        /// Checks if a level is unlocked
        /// </summary>
        /// <param name="levelId">The level ID to check</param>
        /// <returns>True if unlocked, false otherwise</returns>
        public bool IsLevelUnlocked(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return false;

            InitializeCollectionsIfNeeded();
            return unlockedLevels.Contains(levelId);
        }

        #endregion

        #region Mining Methods

        /// <summary>
        /// Adds a new mining node to the player's collection
        /// </summary>
        /// <param name="nodeId">The unique identifier for the node</param>
        /// <param name="level">Starting level for the node (default: 1)</param>
        public void AddMiningNode(string nodeId, int level = 1)
        {
            if (string.IsNullOrEmpty(nodeId)) return;

            InitializeCollectionsIfNeeded();

            if (!miningNodes.ContainsKey(nodeId))
            {
                miningNodes[nodeId] = new MiningNodeData(nodeId)
                {
                    // If needed, set additional properties here
                };
                Debug.Log($"[ProfileData] Added mining node: {nodeId} at level {level}");
            }
        }

        /// <summary>
        /// Collects all pending CyberCoins from all mining nodes
        /// </summary>
        /// <returns>The total amount of CyberCoins collected</returns>
        public float CollectAllMining()
        {
            InitializeCollectionsIfNeeded();

            float totalCollected = 0f;
            foreach (var node in miningNodes.Values)
            {
                totalCollected += node.CollectMining();
            }

            // Add the collected coins to the player's balance
            cyberCoins += totalCollected;

            Debug.Log($"[ProfileData] Collected a total of {totalCollected} CyberCoins from all mining nodes");
            return totalCollected;
        }

        #endregion

        #endregion
    }
}
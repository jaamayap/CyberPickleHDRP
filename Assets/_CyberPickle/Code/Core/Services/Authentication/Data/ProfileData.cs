using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
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

        [JsonProperty("equippedWeaponId")]
        private string equippedWeaponId;

        [JsonProperty("equippedPowerupIds")]
        private List<string> equippedPowerupIds;

        // Public Properties
        [JsonIgnore] public string CharacterId => characterId;
        [JsonIgnore] public int CharacterLevel => characterLevel;
        [JsonIgnore] public float Experience => experience;
        [JsonIgnore] public IReadOnlyDictionary<string, float> Stats => stats;
        [JsonIgnore] public IReadOnlyList<string> UnlockedSkills => unlockedSkills;
        [JsonIgnore] public string EquippedWeaponId => equippedWeaponId;
        [JsonIgnore] public IReadOnlyList<string> EquippedPowerupIds => equippedPowerupIds;

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
        }
    }

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

        // Public Properties
        [JsonIgnore] public string NodeId => nodeId;
        [JsonIgnore] public int Level => level;
        [JsonIgnore] public float BaseRate => baseRate;
        [JsonIgnore] public float Multiplier => multiplier;

        public MiningNodeData(string nodeId)
        {
            this.nodeId = nodeId;
            this.level = 1;
            this.baseRate = 10f;
            this.multiplier = 1f;
        }
    }

    [Serializable]
    public class ProfileData
    {
        #region Private Fields

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

            hasSaveData = false;
            Debug.Log($"[ProfileData] Cleared progress for profile: {profileId}");
        }

        public void SetActive(bool active)
        {
            isActive = active;
            Debug.Log($"[ProfileData] Profile {profileId} active state set to: {active}");
        }

        #endregion
    }
}
// File: Assets/Code/Core/Services/Authentication/Data/ProfileDataEquipmentExtension.cs
//
// Purpose: Extends the ProfileData class with equipment progression tracking.
// Adds methods to get, set, and update equipment progression data.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json;

namespace CyberPickle.Core.Services.Authentication.Data
{
    /// <summary>
    /// Extension class for ProfileData to add equipment progression tracking
    /// </summary>
    public static class ProfileDataEquipmentExtension
    {
        // Private field name in ProfileData that will store our equipment progression data
        private const string EquipmentProgressionFieldName = "equipmentProgressionData";

        /// <summary>
        /// Initializes equipment progression tracking for a profile if not already initialized
        /// </summary>
        /// <param name="profile">The profile to initialize</param>
        public static void InitializeEquipmentProgression(this ProfileData profile)
        {
            // Get or create the equipment progression dictionary using reflection
            var equipmentProgressionField = GetEquipmentProgressionField(profile);
            if (equipmentProgressionField == null)
                return;

            var equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;

            if (equipmentProgression == null)
            {
                equipmentProgression = new Dictionary<string, EquipmentProgressionData>();
                equipmentProgressionField.SetValue(profile, equipmentProgression);
                Debug.Log($"[ProfileData] Initialized equipment progression tracking for profile {profile.ProfileId}");
            }
        }

        /// <summary>
        /// Gets equipment progression data for a specific equipment item
        /// </summary>
        /// <param name="profile">The profile to get data from</param>
        /// <param name="equipmentId">The ID of the equipment</param>
        /// <returns>Equipment progression data or null if not found</returns>
        public static EquipmentProgressionData GetEquipmentProgression(this ProfileData profile, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return null;

            var equipmentProgressionField = GetEquipmentProgressionField(profile);
            if (equipmentProgressionField == null)
                return null;

            var equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;
            if (equipmentProgression == null)
            {
                profile.InitializeEquipmentProgression();
                equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;

                if (equipmentProgression == null)
                    return null;
            }

            // Try to get existing progression data
            if (equipmentProgression.TryGetValue(equipmentId, out var progressionData))
                return progressionData;

            return null;
        }

        /// <summary>
        /// Gets or creates equipment progression data for a specific equipment item
        /// </summary>
        /// <param name="profile">The profile to get data from</param>
        /// <param name="equipmentId">The ID of the equipment</param>
        /// <returns>Equipment progression data</returns>
        public static EquipmentProgressionData GetOrCreateEquipmentProgression(this ProfileData profile, string equipmentId)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return null;

            // Try to get existing progression data
            var progressionData = profile.GetEquipmentProgression(equipmentId);
            if (progressionData != null)
                return progressionData;

            // Create new progression data
            var equipmentProgressionField = GetEquipmentProgressionField(profile);
            if (equipmentProgressionField == null)
                return null;

            var equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;
            if (equipmentProgression == null)
            {
                profile.InitializeEquipmentProgression();
                equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;

                if (equipmentProgression == null)
                    return null;
            }

            // Create new progression data
            progressionData = new EquipmentProgressionData(equipmentId);
            equipmentProgression[equipmentId] = progressionData;

            Debug.Log($"[ProfileData] Created equipment progression data for {equipmentId} in profile {profile.ProfileId}");

            return progressionData;
        }

        /// <summary>
        /// Gets the current level of a specific equipment item
        /// </summary>
        /// <param name="profile">The profile to get data from</param>
        /// <param name="equipmentId">The ID of the equipment</param>
        /// <returns>The current level or 0 if not found</returns>
        public static int GetEquipmentLevel(this ProfileData profile, string equipmentId)
        {
            var progressionData = profile.GetEquipmentProgression(equipmentId);
            return progressionData?.CurrentLevel ?? 0;
        }

        /// <summary>
        /// Upgrades a specific equipment item to a new level
        /// </summary>
        /// <param name="profile">The profile to upgrade in</param>
        /// <param name="equipmentId">The ID of the equipment</param>
        /// <param name="newLevel">The new level</param>
        /// <returns>True if upgraded successfully</returns>
        public static bool UpgradeEquipment(this ProfileData profile, string equipmentId, int newLevel)
        {
            if (string.IsNullOrEmpty(equipmentId) || newLevel <= 1)
                return false;

            var progressionData = profile.GetOrCreateEquipmentProgression(equipmentId);
            if (progressionData == null)
                return false;

            return progressionData.Upgrade(newLevel);
        }

        /// <summary>
        /// Records usage statistics for a specific equipment item
        /// </summary>
        /// <param name="profile">The profile to record in</param>
        /// <param name="equipmentId">The ID of the equipment</param>
        /// <param name="kills">Number of kills to record</param>
        /// <param name="damage">Amount of damage to record</param>
        public static void RecordEquipmentUsage(this ProfileData profile, string equipmentId, int kills = 0, float damage = 0)
        {
            if (string.IsNullOrEmpty(equipmentId))
                return;

            var progressionData = profile.GetOrCreateEquipmentProgression(equipmentId);
            if (progressionData == null)
                return;

            progressionData.RecordUse();

            if (kills > 0)
                progressionData.RecordKills(kills);

            if (damage > 0)
                progressionData.RecordDamage(damage);
        }

        /// <summary>
        /// Gets all equipment progression data for a profile
        /// </summary>
        /// <param name="profile">The profile to get data from</param>
        /// <returns>Dictionary of equipment progression data</returns>
        public static Dictionary<string, EquipmentProgressionData> GetAllEquipmentProgression(this ProfileData profile)
        {
            var equipmentProgressionField = GetEquipmentProgressionField(profile);
            if (equipmentProgressionField == null)
                return new Dictionary<string, EquipmentProgressionData>();

            var equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;
            if (equipmentProgression == null)
            {
                profile.InitializeEquipmentProgression();
                equipmentProgression = equipmentProgressionField.GetValue(profile) as Dictionary<string, EquipmentProgressionData>;

                if (equipmentProgression == null)
                    return new Dictionary<string, EquipmentProgressionData>();
            }

            return equipmentProgression;
        }

        /// <summary>
        /// Gets the field info for the equipment progression field in ProfileData
        /// </summary>
        /// <param name="profile">The profile to get field info for</param>
        /// <returns>Field info or null if not found</returns>
        private static FieldInfo GetEquipmentProgressionField(ProfileData profile)
        {
            // First check if the field already exists
            var field = typeof(ProfileData).GetField(EquipmentProgressionFieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
                return field;

            // If not, use reflection to get a private field that might hold a dictionary (fallback)
            var fields = typeof(ProfileData).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.FieldType == typeof(Dictionary<string, EquipmentProgressionData>))
                    return f;
            }

            // If we still haven't found it, we'll need to add the field using reflection
            // However, adding fields at runtime is not possible in C#
            // So we'll log a warning and return null
            Debug.LogWarning($"[ProfileData] Equipment progression field not found for profile {profile.ProfileId}. Make sure to add a private field named {EquipmentProgressionFieldName} to ProfileData.");

            return null;
        }
    }
}
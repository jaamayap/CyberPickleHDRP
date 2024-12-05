using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    /// <summary>
    /// Container class for managing player profiles with Json.NET serialization support.
    /// Handles profile storage, retrieval, and persistence.
    /// </summary>
    [Serializable]
    public class ProfileContainer
    {
        [JsonProperty("Profiles")]
        private List<ProfileData> profiles = new List<ProfileData>();

        private static string profilesFilePath;

        /// <summary>
        /// Lazy-initialized file path for profile storage
        /// </summary>
        private static string ProfilesFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(profilesFilePath))
                {
                    profilesFilePath = Path.Combine(Application.persistentDataPath, "profiles.json");
                }
                return profilesFilePath;
            }
        }

        /// <summary>
        /// Read-only access to all profiles
        /// </summary>
        [JsonIgnore]
        public IReadOnlyList<ProfileData> Profiles => profiles.AsReadOnly();

        /// <summary>
        /// Currently active profile
        /// </summary>
        [JsonIgnore]
        public ProfileData ActiveProfile => profiles.FirstOrDefault(p => p.IsActive);

        /// <summary>
        /// Adds a new profile if it doesn't exist
        /// </summary>
        public void AddProfile(ProfileData profile)
        {
            if (!profiles.Any(p => p.ProfileId == profile.ProfileId))
            {
                Debug.Log($"[ProfileContainer] Adding new profile: {profile.ProfileId}");
                profiles.Add(profile);
                SaveProfiles();
            }
        }

        /// <summary>
        /// Retrieves a profile by ID
        /// </summary>
        public ProfileData GetProfile(string profileId)
        {
            return profiles.FirstOrDefault(p => p.ProfileId == profileId);
        }

        /// <summary>
        /// Sets the active profile and deactivates all others
        /// </summary>
        public void SetActiveProfile(string profileId)
        {
            Debug.Log($"[ProfileContainer] Setting active profile to {profileId}. Before change - Active profile: {ActiveProfile?.ProfileId}");

            foreach (var profile in profiles)
            {
                bool shouldBeActive = profile.ProfileId == profileId;
                profile.SetActive(shouldBeActive);
                Debug.Log($"[ProfileContainer] Profile {profile.ProfileId} - Setting active to: {shouldBeActive}");
            }

            SaveProfiles();

            // Verify the change
            var activeProfile = ActiveProfile;
            if (activeProfile != null)
            {
                Debug.Log($"[ProfileContainer] Active profile is now: {activeProfile.ProfileId}");
            }
            else
            {
                Debug.LogError("[ProfileContainer] No active profile found after setting active profile.");
            }
        }

        /// <summary>
        /// Updates an existing profile
        /// </summary>
        public void UpdateProfile(ProfileData updatedProfile)
        {
            var index = profiles.FindIndex(p => p.ProfileId == updatedProfile.ProfileId);
            if (index != -1)
            {
                Debug.Log($"[ProfileContainer] Updating profile: {updatedProfile.ProfileId}");
                profiles[index] = updatedProfile;
                SaveProfiles();
            }
        }

        /// <summary>
        /// Removes a profile by ID
        /// </summary>
        public void RemoveProfile(string profileId)
        {
            profiles.RemoveAll(p => p.ProfileId == profileId);
            SaveProfiles();
        }

        /// <summary>
        /// Saves all profiles to disk using Json.NET
        /// </summary>
        public void SaveProfiles()
        {
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Include,
                    ObjectCreationHandling = ObjectCreationHandling.Replace
                };

                var json = JsonConvert.SerializeObject(this, settings);
                File.WriteAllText(ProfilesFilePath, json);

                Debug.Log($"[ProfileContainer] Profiles saved successfully to {ProfilesFilePath}");
                Debug.Log($"[ProfileContainer] Saved {profiles.Count} profiles, Active profile: {ActiveProfile?.ProfileId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileContainer] Failed to save profiles: {e.Message}\nStack trace: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Loads profiles from disk using Json.NET
        /// </summary>
        public static ProfileContainer Load()
        {
            try
            {
                Debug.Log($"[ProfileContainer] Attempting to load from: {ProfilesFilePath}");

                if (!File.Exists(ProfilesFilePath))
                {
                    Debug.Log($"[ProfileContainer] No profile file found at: {ProfilesFilePath}");
                    return new ProfileContainer();
                }

                var json = File.ReadAllText(ProfilesFilePath);
                Debug.Log($"[ProfileContainer] Loaded JSON content: {json}");

                var settings = new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Populate
                };

                var container = JsonConvert.DeserializeObject<ProfileContainer>(json, settings);

                if (container == null)
                {
                    Debug.LogError("[ProfileContainer] Deserialization returned null container");
                    return new ProfileContainer();
                }

                Debug.Log($"[ProfileContainer] Successfully loaded {container.profiles.Count} profiles");
                foreach (var profile in container.profiles)
                {
                    // Initialize collections if they're null
                    profile.InitializeCollectionsIfNeeded();
                    Debug.Log($"[ProfileContainer] Loaded profile: {profile.ProfileId}, Active: {profile.IsActive}");
                }

                return container;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileContainer] Error loading profiles: {e.Message}\nStack trace: {e.StackTrace}");
                return new ProfileContainer();
            }
        }

        /// <summary>
        /// Clears all profiles and deletes the save file
        /// </summary>
        public void ClearAll()
        {
            Debug.Log("[ProfileContainer] Clearing all profiles");
            profiles.Clear();
            if (File.Exists(ProfilesFilePath))
            {
                File.Delete(ProfilesFilePath);
            }
        }

        /// <summary>
        /// Verifies the integrity of the profile container and repairs if needed
        /// </summary>
        public void VerifyIntegrity()
        {
            Debug.Log("[ProfileContainer] Verifying profile container integrity");

            // Ensure no duplicate active profiles
            var activeProfiles = profiles.Count(p => p.IsActive);
            if (activeProfiles > 1)
            {
                Debug.LogWarning("[ProfileContainer] Found multiple active profiles, fixing...");
                var firstActive = profiles.First(p => p.IsActive);
                foreach (var profile in profiles.Where(p => p.IsActive && p.ProfileId != firstActive.ProfileId))
                {
                    profile.SetActive(false);
                }
            }

            // Initialize collections for all profiles
            foreach (var profile in profiles)
            {
                profile.InitializeCollectionsIfNeeded();
            }

            SaveProfiles();
        }
    }
}
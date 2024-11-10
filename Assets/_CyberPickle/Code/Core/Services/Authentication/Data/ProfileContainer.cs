using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    [Serializable]
    public class ProfileContainer
    {
        [SerializeField]
        private List<ProfileData> profiles = new List<ProfileData>();

       

        // Use a private static field without initialization
        private static string profilesFilePath;

        // Lazy-initialized static property to get the file path
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

        public IReadOnlyList<ProfileData> Profiles => profiles.AsReadOnly();

        public ProfileData ActiveProfile => profiles.FirstOrDefault(p => p.IsActive);

        public void AddProfile(ProfileData profile)
        {
            if (!profiles.Any(p => p.ProfileId == profile.ProfileId))
            {
                profiles.Add(profile);
                SaveProfiles();
            }
        }

        public ProfileData GetProfile(string profileId)
        {
            return profiles.FirstOrDefault(p => p.ProfileId == profileId);
        }

        public void SetActiveProfile(string profileId)
        {
            foreach (var profile in profiles)
            {
                profile.SetActive(profile.ProfileId == profileId);
            }
            SaveProfiles();
        }

        public void UpdateProfile(ProfileData updatedProfile)
        {
            var index = profiles.FindIndex(p => p.ProfileId == updatedProfile.ProfileId);
            if (index != -1)
            {
                profiles[index] = updatedProfile;
                SaveProfiles();
            }
        }

        public void RemoveProfile(string profileId)
        {
            profiles.RemoveAll(p => p.ProfileId == profileId);
            SaveProfiles();
        }

        public void SaveProfiles()
        {
            try
            {
                var json = JsonUtility.ToJson(this, prettyPrint: true);
                File.WriteAllText(ProfilesFilePath, json);
                Debug.Log($"Profiles saved successfully to {ProfilesFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save profiles: {e.Message}");
            }
        }

        public static ProfileContainer Load()
        {
            try
            {
                if (!File.Exists(ProfilesFilePath))
                {
                    Debug.Log($"[ProfileContainer] No profile file found at: {ProfilesFilePath}");
                    return new ProfileContainer();
                }

                var json = File.ReadAllText(ProfilesFilePath);
                Debug.Log($"[ProfileContainer] Loaded JSON: {json}");
                return JsonUtility.FromJson<ProfileContainer>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileContainer] Error loading profiles: {e}");
                return new ProfileContainer();
            }
        }

        public void ClearAll()
        {
            profiles.Clear();
            if (File.Exists(ProfilesFilePath))
            {
                File.Delete(ProfilesFilePath);
            }
        }
    }
}


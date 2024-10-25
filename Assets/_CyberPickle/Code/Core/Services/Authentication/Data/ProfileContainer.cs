using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    [Serializable]
    public class ProfileContainer
    {
        private List<ProfileData> profiles = new List<ProfileData>();
        private const string PROFILES_PREFS_KEY = "CyberPickle_Profiles";

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
            var json = JsonUtility.ToJson(this);
            PlayerPrefs.SetString(PROFILES_PREFS_KEY, json);
            PlayerPrefs.Save();
        }

        public static ProfileContainer Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(PROFILES_PREFS_KEY, "");
                if (string.IsNullOrEmpty(json))
                {
                    return new ProfileContainer();
                }
                return JsonUtility.FromJson<ProfileContainer>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load profiles: {e.Message}");
                return new ProfileContainer();
            }
        }

        public void ClearAll()
        {
            profiles.Clear();
            PlayerPrefs.DeleteKey(PROFILES_PREFS_KEY);
            PlayerPrefs.Save();
        }
    }
}

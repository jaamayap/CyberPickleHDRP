using System;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    [Serializable]
    public class ProfileData
    {
        // Authentication Data
        public string ProfileId { get; private set; }
        public string PlayerId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime LastLoginAt { get; private set; }

        // Profile Metadata
        public string DisplayName { get; private set; }
        public bool HasSaveData { get; private set; }
        public float TotalPlayTime { get; private set; }
        public int HighestScore { get; private set; }
        public float FurthestDistance { get; private set; }

        // Session Data
        public string SessionToken { get; private set; }
        public bool IsActive { get; private set; }

        public ProfileData(string profileId, string playerId)
        {
            ProfileId = profileId;
            PlayerId = playerId;
            CreatedAt = DateTime.UtcNow;
            LastLoginAt = DateTime.UtcNow;
            DisplayName = $"Player_{profileId}";
            HasSaveData = false;
            IsActive = true;
        }

        public void UpdateLoginTime()
        {
            LastLoginAt = DateTime.UtcNow;
        }

        public void UpdateSessionToken(string token)
        {
            SessionToken = token;
            LastLoginAt = DateTime.UtcNow;
        }

        public void UpdateDisplayName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                DisplayName = name;
            }
        }

        public void UpdateProgress(float playTime, int score, float distance)
        {
            TotalPlayTime = playTime;
            if (score > HighestScore) HighestScore = score;
            if (distance > FurthestDistance) FurthestDistance = distance;
            HasSaveData = true;
        }

        public void ClearProgress()
        {
            TotalPlayTime = 0;
            HighestScore = 0;
            FurthestDistance = 0;
            HasSaveData = false;
        }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static ProfileData FromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<ProfileData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse ProfileData from JSON: {e.Message}");
                return null;
            }
        }
    }
}

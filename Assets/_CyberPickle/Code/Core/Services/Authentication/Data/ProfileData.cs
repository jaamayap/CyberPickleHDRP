using System;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Data
{
    [Serializable]
    public class ProfileData
    {
        // Authentication Data
        [SerializeField]
        private string profileId;
        [SerializeField]
        private string playerId;

        // Profile Metadata
        [SerializeField]
        private string displayName;
        [SerializeField]
        private bool hasSaveData;
        [SerializeField]
        private float totalPlayTime;
        [SerializeField]
        private int highestScore;
        [SerializeField]
        private float furthestDistance;

        // Session Data
        [SerializeField]
        private bool isActive;

        // Serialized DateTime as ticks
        [SerializeField]
        private long createdAtTicks;
        [SerializeField]
        private long lastLoginAtTicks;

        // Public Properties for Access
        public string ProfileId => profileId;
        public string PlayerId => playerId;
        public string DisplayName => displayName;
        public bool HasSaveData => hasSaveData;
        public float TotalPlayTime => totalPlayTime;
        public int HighestScore => highestScore;
        public float FurthestDistance => furthestDistance;
        public bool IsActive => isActive;
        public DateTime CreatedAt => new DateTime(createdAtTicks);
        public DateTime LastLoginAt => new DateTime(lastLoginAtTicks);

        // Constructors
        public ProfileData(string profileId, string playerId, string displayName)
        {
            this.profileId = profileId;
            this.playerId = playerId;
            this.displayName = displayName;
            this.createdAtTicks = DateTime.UtcNow.Ticks;
            this.lastLoginAtTicks = DateTime.UtcNow.Ticks;
            this.isActive = false;
            this.hasSaveData = false;
            this.totalPlayTime = 0f;
            this.highestScore = 0;
            this.furthestDistance = 0f;
        }

        // Methods to Update Profile Data
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

        public void UpdateProgress(float playTime, int score, float distance)
        {
            totalPlayTime = playTime;
            if (score > highestScore) highestScore = score;
            if (distance > furthestDistance) furthestDistance = distance;
            hasSaveData = true;
        }

        public void ClearProgress()
        {
            totalPlayTime = 0f;
            highestScore = 0;
            furthestDistance = 0f;
            hasSaveData = false;
        }

        public void SetActive(bool active)
        {
            isActive = active;
        }
    }
}

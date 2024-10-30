// File: Assets/Code/Core/Services/Authentication/ProfileManager.cs

using System;
using System.Collections.Generic;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using UnityEngine;


namespace CyberPickle.Core.Services.Authentication
{
    public class ProfileManager : Manager<ProfileManager>, IInitializable
    {
        private ProfileContainer profileContainer;
        private ProfileManagementEvents profileEvents;

        public void Initialize()
        {
            
            
        }

        protected override void OnManagerAwake()
        {
            Debug.Log("[ProfileManager] OnManagerAwake called.");
            profileContainer = ProfileContainer.Load();
            profileEvents = new ProfileManagementEvents();
            Debug.Log("[ProfileManager] profileEvents and profileContainer initialized.");
            Debug.Log($"[ProfileManager] Loaded {profileContainer.Profiles.Count} profiles.");

        }

        public ProfileData ActiveProfile => profileContainer.ActiveProfile;

        public void CreateProfile(string profileId, string playerId, string displayName)
        {
            var profile = new ProfileData(profileId, playerId, displayName);
            profileContainer.AddProfile(profile);
            profileContainer.SetActiveProfile(profileId);
            profileContainer.SaveProfiles();
            profileEvents.InvokeNewProfileCreated(profileId);
        }

        public void SwitchActiveProfile(string profileId)
        {
            profileContainer.SetActiveProfile(profileId);
            profileContainer.SaveProfiles();
            profileEvents.InvokeProfileSwitched(profileId);
        }

        public IReadOnlyList<ProfileData> GetAllProfiles()
        {
            return profileContainer.Profiles;
        }

        public void UpdateActiveProfileProgress(float playTime, int score, float distance)
        {
            if (ActiveProfile != null)
            {
                ActiveProfile.UpdateProgress(playTime, score, distance);
                profileContainer.UpdateProfile(ActiveProfile);
            }
        }

        public void DeleteProfile(string profileId)
        {
            profileContainer.RemoveProfile(profileId);
            profileEvents.InvokeProfileDeleted(profileId);
        }

        public void ClearAllProfiles()
        {
            profileContainer.ClearAll();
        }

        // Event Subscription Methods
        public void SubscribeToProfileLoaded(Action<string> callback)
            => profileEvents.OnProfileLoaded += callback;

        public void UnsubscribeFromProfileLoaded(Action<string> callback)
            => profileEvents.OnProfileLoaded -= callback;
        public void SubscribeToProfileSwitched(Action<string> callback)
        {
            if (profileEvents == null)
            {
                Debug.LogError("[ProfileManager] profileEvents is null in SubscribeToProfileSwitched.");
            }
            else
            {
                profileEvents.OnProfileSwitched += callback;
            }
        }

        public void UnsubscribeFromProfileSwitched(Action<string> callback)
            => profileEvents.OnProfileSwitched -= callback;

        public void SubscribeToNewProfileCreated(Action<string> callback)
            => profileEvents.OnNewProfileCreated += callback;

        public void UnsubscribeFromNewProfileCreated(Action<string> callback)
            => profileEvents.OnNewProfileCreated -= callback;

        public void SubscribeToProfileDeleted(Action<string> callback)
            => profileEvents.OnProfileDeleted += callback;

        public void UnsubscribeFromProfileDeleted(Action<string> callback)
            => profileEvents.OnProfileDeleted -= callback;
    }
}



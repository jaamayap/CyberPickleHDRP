// File: Assets/Code/Core/Services/Authentication/ProfileManager.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.Linq;
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Services.Authentication.Data;
using System.Threading;

namespace CyberPickle.Core.Services.Authentication
{
    public class ProfileOperationResult
    {
        public bool Success { get; }
        public string Message { get; }
        public Exception Error { get; }

        private ProfileOperationResult(bool success, string message, Exception error = null)
        {
            Success = success;
            Message = message;
            Error = error;
        }

        public static ProfileOperationResult Succeeded(string message = null)
            => new ProfileOperationResult(true, message);

        public static ProfileOperationResult Failed(string message, Exception error = null)
            => new ProfileOperationResult(false, message, error);
    }

    public class ProfileManager : Manager<ProfileManager>, IInitializable
    {
        private ProfileContainer profileContainer;
        private ProfileManagementEvents profileEvents;
        private bool isInitialized;
        private bool isAutosaveEnabled = true;
        private readonly object saveLock = new object();

        public bool IsInitialized => isInitialized;
        public ProfileData ActiveProfile => profileContainer?.ActiveProfile;

        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[ProfileManager] Starting initialization");

            try
            {
                profileContainer = ProfileContainer.Load();
                Debug.Log($"[ProfileManager] Loaded profile container. Has profiles: {profileContainer?.Profiles?.Count > 0}");

                if (profileContainer == null)
                {
                    profileContainer = new ProfileContainer();
                    Debug.Log("[ProfileManager] Created new profile container");
                }

                profileEvents = new ProfileManagementEvents();
                isInitialized = true;
                Debug.Log("[ProfileManager] Initialization complete");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProfileManager] Failed to initialize: {e.Message}");
                throw;
            }
        }

        protected override void OnManagerAwake()
        {
            Debug.Log("[ProfileManager] OnManagerAwake called");
            Initialize();
        }

        private void ValidateState()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("[ProfileManager] Manager not initialized");
            }

            if (profileContainer == null)
            {
                throw new InvalidOperationException("[ProfileManager] Profile container is null");
            }
        }

        public async Task<ProfileOperationResult> CreateProfileAsync(string profileId, string playerId, string displayName)
        {
            try
            {
                ValidateState();

                Debug.Log($"[ProfileManager] Creating profile: {displayName} (ID: {profileId})");
                var profile = new ProfileData(profileId, playerId, displayName);
                profileContainer.AddProfile(profile);
                profileContainer.SetActiveProfile(profileId);

                await SaveProfilesAsync();
                profileEvents.InvokeNewProfileCreated(profileId);

                return ProfileOperationResult.Succeeded($"Profile created successfully: {profileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to create profile: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to create profile", ex);
            }
        }

        public ProfileData GetProfile(string profileId)
        {
            try
            {
                ValidateState();
                return profileContainer.GetProfile(profileId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to get profile {profileId}: {ex.Message}");
                return null;
            }
        }

        public async Task<ProfileOperationResult> AddProfileAsync(ProfileData profile)
        {
            try
            {
                ValidateState();

                Debug.Log($"[ProfileManager] Adding profile: {profile.DisplayName}");
                profileContainer.AddProfile(profile);
                await SaveProfilesAsync();
                profileEvents?.InvokeNewProfileCreated(profile.ProfileId);

                return ProfileOperationResult.Succeeded($"Profile added successfully: {profile.DisplayName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to add profile: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to add profile", ex);
            }
        }

        public async Task<ProfileOperationResult> DeleteProfileAsync(string profileId)
        {
            try
            {
                ValidateState();

                Debug.Log($"[ProfileManager] Deleting profile: {profileId}");

                // Check if profile exists
                var profile = GetProfile(profileId);
                if (profile == null)
                {
                    return ProfileOperationResult.Failed($"Profile not found: {profileId}");
                }

                // If deleting active profile, handle special case
                if (ActiveProfile?.ProfileId == profileId)
                {
                    var nextProfile = profileContainer.Profiles
                        .FirstOrDefault(p => p.ProfileId != profileId);

                    if (nextProfile != null)
                    {
                        profileContainer.SetActiveProfile(nextProfile.ProfileId);
                    }
                }

                profileContainer.RemoveProfile(profileId);
                await SaveProfilesAsync();
                profileEvents?.InvokeProfileDeleted(profileId);

                return ProfileOperationResult.Succeeded($"Profile deleted successfully: {profileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to delete profile: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to delete profile", ex);
            }
        }

        public async Task<ProfileOperationResult> UpdateProfileAsync(ProfileData profile)
        {
            try
            {
                ValidateState();

                Debug.Log($"[ProfileManager] Updating profile: {profile.ProfileId}");
                profileContainer.UpdateProfile(profile);
                await SaveProfilesAsync();

                return ProfileOperationResult.Succeeded($"Profile updated successfully: {profile.ProfileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to update profile: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to update profile", ex);
            }
        }

        public async Task<ProfileOperationResult> SwitchActiveProfileAsync(string profileId)
        {
            try
            {
                ValidateState();

                var profile = GetProfile(profileId);
                if (profile == null)
                {
                    return ProfileOperationResult.Failed($"Profile not found: {profileId}");
                }

                profileContainer.SetActiveProfile(profileId);
                Debug.Log($"[ProfileManager] ActiveProfile set to {ActiveProfile?.ProfileId} at {Time.time}"); ;
                await SaveProfilesAsync();

                profileEvents.InvokeProfileSwitched(profileId);

                return ProfileOperationResult.Succeeded($"Profile switched successfully: {profileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to switch profile: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to switch profile", ex);
            }
        }

        public IReadOnlyList<ProfileData> GetAllProfiles()
        {
            try
            {
                ValidateState();
                return profileContainer.Profiles;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to get profiles: {ex.Message}");
                return new List<ProfileData>();
            }
        }

        public async Task<ProfileOperationResult> UpdateActiveProfileProgressAsync(float playTime, int score, float distance, int level)
        {
            try
            {
                ValidateState();

                if (ActiveProfile == null)
                {
                    return ProfileOperationResult.Failed("No active profile");
                }

                ActiveProfile.UpdateProgress(playTime, score, distance, level);
                profileContainer.UpdateProfile(ActiveProfile);
                await SaveProfilesAsync();

                return ProfileOperationResult.Succeeded("Profile progress updated successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to update profile progress: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to update profile progress", ex);
            }
        }

        private async Task SaveProfilesAsync()
        {
            if (!isAutosaveEnabled || profileContainer == null) return;

            try
            {
                var token = cancellationTokenSource?.Token ?? CancellationToken.None;
                if (token.IsCancellationRequested)
                {
                    Debug.Log("[ProfileManager] Save operation cancelled due to application quit");
                    return;
                }

                // Check IsQuitting on the main thread before starting the save operation
                bool isQuitting = IsQuitting;
                if (isQuitting)
                {
                    Debug.Log("[ProfileManager] Skipping save due to application quit");
                    return;
                }

                // Run the save operation on a background thread
                await Task.Run(() =>
                {
                    lock (saveLock)
                    {
                        profileContainer.SaveProfiles();
                    }
                }, token);

                Debug.Log("[ProfileManager] Profile save completed successfully");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[ProfileManager] Save operation was cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to save profiles: {ex.Message}");
            }
        }

        public async Task<ProfileOperationResult> ClearAllProfilesAsync()
        {
            try
            {
                ValidateState();

                profileContainer.ClearAll();
                await SaveProfilesAsync();

                return ProfileOperationResult.Succeeded("All profiles cleared successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Failed to clear profiles: {ex.Message}");
                return ProfileOperationResult.Failed("Failed to clear profiles", ex);
            }
        }

        #region Event Subscription Methods
        public void SubscribeToProfileLoaded(Action<string> callback)
            => profileEvents.OnProfileLoaded += callback;

        public void UnsubscribeFromProfileLoaded(Action<string> callback)
            => profileEvents.OnProfileLoaded -= callback;

        public void SubscribeToProfileSwitched(Action<string> callback)
        {
            if (profileEvents == null)
            {
                Debug.LogError("[ProfileManager] profileEvents is null in SubscribeToProfileSwitched");
                return;
            }
            profileEvents.OnProfileSwitched += callback;
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
        #endregion

        protected override void OnDestroy()
        {
            if (isInitialized)
            {
                try
                {
                    // Don't wait for async operations during cleanup
                    if (!IsQuitting)
                    {
                        SaveProfilesAsync().GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ProfileManager] Error during cleanup: {ex.Message}");
                }
                finally
                {
                    isInitialized = false;
                    profileEvents = null;
                }
            }

            base.OnDestroy();
        }
    }
}



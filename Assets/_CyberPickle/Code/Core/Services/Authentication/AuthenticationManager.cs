using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.Interfaces;
using System.Collections.Generic;

namespace CyberPickle.Core.Services.Authentication
{
    public class AuthenticationManager : Manager<AuthenticationManager>, IInitializable
    {
        private AuthenticationState currentState = AuthenticationState.NotInitialized;
        private ProfileContainer profileContainer;
        private AuthenticationEvents events;
        private bool isInitialized;

        public AuthenticationState CurrentState => currentState;
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string CurrentPlayerId => AuthenticationService.Instance.PlayerId;
        public ProfileData CurrentProfile => profileContainer?.ActiveProfile;

        protected override void OnManagerAwake()
        {
            events = new AuthenticationEvents();
            profileContainer = ProfileContainer.Load();
        }

        public async void Initialize()
        {
            if (isInitialized) return;

            try
            {
                SetState(AuthenticationState.NotInitialized);

                // Initialize Unity Services
                await UnityServices.InitializeAsync();

                // Subscribe to Authentication events
                SubscribeToAuthEvents();

                // Check for cached session
                if (AuthenticationService.Instance.SessionTokenExists)
                {
                    events.InvokeSessionTokenFound(AuthenticationService.Instance.Profile);
                }

                SetState(AuthenticationState.NotAuthenticated);
                isInitialized = true;

                Debug.Log("Authentication Manager initialized successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize Authentication Manager: {e.Message}");
                SetState(AuthenticationState.AuthenticationFailed);
            }
        }

        private void SubscribeToAuthEvents()
        {
            AuthenticationService.Instance.SignedIn += OnSignedIn;
            AuthenticationService.Instance.SignedOut += OnSignedOut;
            AuthenticationService.Instance.Expired += OnSessionExpired;
        }

        private void UnsubscribeFromAuthEvents()
        {
            if (AuthenticationService.Instance != null)
            {
                AuthenticationService.Instance.SignedIn -= OnSignedIn;
                AuthenticationService.Instance.SignedOut -= OnSignedOut;
                AuthenticationService.Instance.Expired -= OnSessionExpired;
            }
        }

        #region Authentication Methods

        public async Task<bool> SignInAnonymouslyAsync()
        {
            if (currentState == AuthenticationState.AuthenticationInProgress)
            {
                Debug.LogWarning("Authentication already in progress");
                return false;
            }

            try
            {
                SetState(AuthenticationState.AuthenticationInProgress);

                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // If we have a cached profile, load it
                var profile = profileContainer.GetProfile(AuthenticationService.Instance.Profile);
                if (profile == null)
                {
                    // Create new profile
                    profile = CreateNewProfile(AuthenticationService.Instance.Profile,
                                            AuthenticationService.Instance.PlayerId);
                }

                profileContainer.SetActiveProfile(profile.ProfileId);
                return true;
            }
            catch (AuthenticationException e)
            {
                HandleAuthenticationError(e);
                return false;
            }
            catch (RequestFailedException e)
            {
                HandleRequestError(e);
                return false;
            }
        }

        public async Task<bool> SwitchProfileAsync(string profileId)
        {
            try
            {
                SetState(AuthenticationState.ProfileSwitchInProgress);

                // Switch profile in Unity's Authentication service
                AuthenticationService.Instance.SwitchProfile(profileId);

                // Try to sign in with the new profile
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

                // Update our profile container
                profileContainer.SetActiveProfile(profileId);

                events.InvokeProfileSwitched(profileId);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to switch profile: {e.Message}");
                events.InvokeAuthenticationFailed($"Profile switch failed: {e.Message}");
                return false;
            }
        }

        public void SignOut()
        {
            try
            {
                AuthenticationService.Instance.SignOut();
                SetState(AuthenticationState.NotAuthenticated);
                events.InvokeSignedOut();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during sign out: {e.Message}");
            }
        }

        #endregion

        #region Profile Management

        private ProfileData CreateNewProfile(string profileId, string playerId)
        {
            var profile = new ProfileData(profileId, playerId);
            profileContainer.AddProfile(profile);
            events.InvokeNewProfileCreated(profileId);
            return profile;
        }

        public void UpdateProfileProgress(float playTime, int score, float distance)
        {
            if (CurrentProfile != null)
            {
                CurrentProfile.UpdateProgress(playTime, score, distance);
                profileContainer.UpdateProfile(CurrentProfile);
            }
        }

        public void ClearProfileProgress(string profileId)
        {
            var profile = profileContainer.GetProfile(profileId);
            if (profile != null)
            {
                profile.ClearProgress();
                profileContainer.UpdateProfile(profile);
            }
        }

        public IReadOnlyList<ProfileData> GetAllProfiles()
        {
            return profileContainer.Profiles;
        }

        #endregion

        #region Event Handlers

        private void OnSignedIn()
        {
            SetState(AuthenticationState.Authenticated);
            events.InvokeAuthenticationCompleted(AuthenticationService.Instance.PlayerId);
            Debug.Log($"Signed in successfully. Player ID: {AuthenticationService.Instance.PlayerId}");
        }

        private void OnSignedOut()
        {
            SetState(AuthenticationState.NotAuthenticated);
            events.InvokeSignedOut();
            Debug.Log("Signed out successfully");
        }

        private void OnSessionExpired()
        {
            SetState(AuthenticationState.SessionExpired);
            events.InvokeSessionExpired();
            Debug.Log("Session expired");
        }

        #endregion

        #region Error Handling

        private void HandleAuthenticationError(AuthenticationException e)
        {
            string errorMessage = $"Authentication error: {e.Message}";
            Debug.LogError(errorMessage);
            SetState(AuthenticationState.AuthenticationFailed);
            events.InvokeAuthenticationFailed(errorMessage);
        }

        private void HandleRequestError(RequestFailedException e)
        {
            string errorMessage = $"Request failed: {e.Message} (Error Code: {e.ErrorCode})";
            Debug.LogError(errorMessage);
            SetState(AuthenticationState.AuthenticationFailed);
            events.InvokeAuthenticationFailed(errorMessage);
        }

        #endregion

        private void SetState(AuthenticationState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                events.InvokeAuthenticationStateChanged(newState);
            }
        }

        #region Event Subscription Methods

        public void SubscribeToAuthenticationCompleted(Action<string> callback)
            => events.OnAuthenticationCompleted += callback;
        public void UnsubscribeFromAuthenticationCompleted(Action<string> callback)
            => events.OnAuthenticationCompleted -= callback;

        public void SubscribeToAuthenticationFailed(Action<string> callback)
            => events.OnAuthenticationFailed += callback;
        public void UnsubscribeFromAuthenticationFailed(Action<string> callback)
            => events.OnAuthenticationFailed -= callback;

        public void SubscribeToProfileSwitched(Action<string> callback)
            => events.OnProfileSwitched += callback;
        public void UnsubscribeFromProfileSwitched(Action<string> callback)
            => events.OnProfileSwitched -= callback;

        // Add more subscription methods as needed...

        #endregion

        private void OnDestroy()
        {
            UnsubscribeFromAuthEvents();
            profileContainer?.SaveProfiles();
        }
    }
}

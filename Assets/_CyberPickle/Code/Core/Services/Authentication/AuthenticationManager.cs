// File: Assets/Code/Core/Services/Authentication/AuthenticationManager.cs
//
// Purpose: Manages authentication state in the game.
// Provides centralized control over user authentication.
//
// Dependencies:
// - Unity.Services.Authentication for base authentication
// - CyberPickle.Core.Events for game-wide event system
// - CyberPickle.Core.Management for base manager pattern
//
// Created: 2024-01-13
// Updated: 2024-01-14

using UnityEngine;
using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Services.Authentication
{
    public class AuthenticationManager : Manager<AuthenticationManager>, IInitializable
    {
        private AuthenticationState currentState = AuthenticationState.NotInitialized;
        private AuthenticationEvents authEvents;
        private bool isInitialized;

        public AuthenticationState CurrentState => currentState;
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string CurrentPlayerId => AuthenticationService.Instance.PlayerId;

        protected override void OnManagerAwake()
        {
            authEvents = new AuthenticationEvents();
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
                    authEvents.InvokeSessionTokenFound(AuthenticationService.Instance.PlayerId);
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

                // Sign-in successful, events are handled in OnSignedIn

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

        public void SignOut()
        {
            try
            {
                AuthenticationService.Instance.SignOut();
                SetState(AuthenticationState.NotAuthenticated);
                authEvents.InvokeSignedOut();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error during sign out: {e.Message}");
            }
        }

        #endregion

        #region Event Handlers

        private void OnSignedIn()
        {
            SetState(AuthenticationState.Authenticated);
            authEvents.InvokeAuthenticationCompleted(AuthenticationService.Instance.PlayerId);
            Debug.Log($"Signed in successfully. Player ID: {AuthenticationService.Instance.PlayerId}");
        }

        private void OnSignedOut()
        {
            SetState(AuthenticationState.NotAuthenticated);
            authEvents.InvokeSignedOut();
            Debug.Log("Signed out successfully");
        }

        private void OnSessionExpired()
        {
            SetState(AuthenticationState.SessionExpired);
            authEvents.InvokeSessionExpired();
            Debug.Log("Session expired");
        }

        #endregion

        #region Error Handling

        private void HandleAuthenticationError(AuthenticationException e)
        {
            string errorMessage = $"Authentication error: {e.Message}";
            Debug.LogError(errorMessage);
            SetState(AuthenticationState.AuthenticationFailed);
            authEvents.InvokeAuthenticationFailed(errorMessage);
        }

        private void HandleRequestError(RequestFailedException e)
        {
            string errorMessage = $"Request failed: {e.Message} (Error Code: {e.ErrorCode})";
            Debug.LogError(errorMessage);
            SetState(AuthenticationState.AuthenticationFailed);
            authEvents.InvokeAuthenticationFailed(errorMessage);
        }

        #endregion

        private void SetState(AuthenticationState newState)
        {
            if (currentState != newState)
            {
                currentState = newState;
                authEvents.InvokeAuthenticationStateChanged(newState);
            }
        }

        #region Event Subscription Methods

        public void SubscribeToAuthenticationStateChanged(Action<AuthenticationState> callback)
            => authEvents.OnAuthenticationStateChanged += callback;

        public void UnsubscribeFromAuthenticationStateChanged(Action<AuthenticationState> callback)
            => authEvents.OnAuthenticationStateChanged -= callback;

        public void SubscribeToAuthenticationCompleted(Action<string> callback)
            => authEvents.OnAuthenticationCompleted += callback;

        public void UnsubscribeFromAuthenticationCompleted(Action<string> callback)
            => authEvents.OnAuthenticationCompleted -= callback;

        public void SubscribeToAuthenticationFailed(Action<string> callback)
            => authEvents.OnAuthenticationFailed += callback;

        public void UnsubscribeFromAuthenticationFailed(Action<string> callback)
            => authEvents.OnAuthenticationFailed -= callback;

        // Session event subscriptions
        public void SubscribeToSessionTokenFound(Action<string> callback)
            => authEvents.OnSessionTokenFound += callback;

        public void UnsubscribeFromSessionTokenFound(Action<string> callback)
            => authEvents.OnSessionTokenFound -= callback;

        public void SubscribeToSessionExpired(Action callback)
            => authEvents.OnSessionExpired += callback;

        public void UnsubscribeFromSessionExpired(Action callback)
            => authEvents.OnSessionExpired -= callback;

        public void SubscribeToSignedOut(Action callback)
            => authEvents.OnSignedOut += callback;

        public void UnsubscribeFromSignedOut(Action callback)
            => authEvents.OnSignedOut -= callback;

        #endregion

        private void OnDestroy()
        {
            UnsubscribeFromAuthEvents();
            // No profile container to save
        }
    }
}

using System;

namespace CyberPickle.Core.Services.Authentication
{
    public class AuthenticationEvents
    {
        // Profile events
        public event Action<string> OnProfileLoaded;
        public event Action<string> OnProfileSwitched;
        public event Action<string> OnNewProfileCreated;

        // Authentication state events
        public event Action<AuthenticationState> OnAuthenticationStateChanged;
        public event Action<string> OnAuthenticationCompleted;
        public event Action<string> OnAuthenticationFailed;

        // Session events
        public event Action<string> OnSessionTokenFound;
        public event Action OnSessionExpired;
        public event Action OnSignedOut;

        internal void InvokeProfileLoaded(string profileId) => OnProfileLoaded?.Invoke(profileId);
        internal void InvokeProfileSwitched(string profileId) => OnProfileSwitched?.Invoke(profileId);
        internal void InvokeNewProfileCreated(string profileId) => OnNewProfileCreated?.Invoke(profileId);
        internal void InvokeAuthenticationStateChanged(AuthenticationState state) => OnAuthenticationStateChanged?.Invoke(state);
        internal void InvokeAuthenticationCompleted(string playerId) => OnAuthenticationCompleted?.Invoke(playerId);
        internal void InvokeAuthenticationFailed(string error) => OnAuthenticationFailed?.Invoke(error);
        internal void InvokeSessionTokenFound(string token) => OnSessionTokenFound?.Invoke(token);
        internal void InvokeSessionExpired() => OnSessionExpired?.Invoke();
        internal void InvokeSignedOut() => OnSignedOut?.Invoke();
    }
}

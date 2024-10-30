using System;

namespace CyberPickle.Core.Services.Authentication
{
    public class AuthenticationEvents
    {
        // Authentication state events
        public event Action<AuthenticationState> OnAuthenticationStateChanged;
        public event Action<string> OnAuthenticationCompleted;
        public event Action<string> OnAuthenticationFailed;

        // Session events
        public event Action<string> OnSessionTokenFound;
        public event Action OnSessionExpired;
        public event Action OnSignedOut;

        // Internal methods to invoke events
        internal void InvokeAuthenticationStateChanged(AuthenticationState state) => OnAuthenticationStateChanged?.Invoke(state);
        internal void InvokeAuthenticationCompleted(string playerId) => OnAuthenticationCompleted?.Invoke(playerId);
        internal void InvokeAuthenticationFailed(string error) => OnAuthenticationFailed?.Invoke(error);
        internal void InvokeSessionTokenFound(string token) => OnSessionTokenFound?.Invoke(token);
        internal void InvokeSessionExpired() => OnSessionExpired?.Invoke();
        internal void InvokeSignedOut() => OnSignedOut?.Invoke();
    }
}


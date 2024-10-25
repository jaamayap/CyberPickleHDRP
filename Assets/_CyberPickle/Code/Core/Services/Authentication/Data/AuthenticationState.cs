using UnityEngine;

namespace CyberPickle.Core.Services.Authentication
{
    public enum AuthenticationState
    {
        NotInitialized,
        NotAuthenticated,
        AuthenticationInProgress,
        Authenticated,
        AuthenticationFailed,
        ProfileSwitchInProgress,
        SessionExpired
    }
}

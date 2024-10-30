using System;

namespace CyberPickle.Core.Services.Authentication
{
    public class ProfileManagementEvents
    {
        // Profile events
        public event Action<string> OnProfileLoaded;
        public event Action<string> OnProfileSwitched;
        public event Action<string> OnNewProfileCreated;
        public event Action<string> OnProfileDeleted;

        // Internal methods to invoke events
        internal void InvokeProfileLoaded(string profileId) => OnProfileLoaded?.Invoke(profileId);
        internal void InvokeProfileSwitched(string profileId) => OnProfileSwitched?.Invoke(profileId);
        internal void InvokeNewProfileCreated(string profileId) => OnNewProfileCreated?.Invoke(profileId);
        internal void InvokeProfileDeleted(string profileId) => OnProfileDeleted?.Invoke(profileId);
    }
}

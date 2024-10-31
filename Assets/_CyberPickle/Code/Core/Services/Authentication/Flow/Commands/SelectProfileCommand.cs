// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/SelectProfileCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class SelectProfileCommand : IAuthCommand
    {
        private readonly ProfileManager profileManager;
        private readonly string profileId;
        private string previousProfileId;

        public SelectProfileCommand(ProfileManager profileManager, string profileId)
        {
            this.profileManager = profileManager;
            this.profileId = profileId;
        }

        public async Task Execute()
        {
            Debug.Log($"[SelectProfileCommand] Selecting profile: {profileId}");

            try
            {
                // Store the previously active profile ID for undo
                previousProfileId = profileManager.ActiveProfile?.ProfileId;

                // Switch to the new profile
                var result = await profileManager.SwitchActiveProfileAsync(profileId);

                if (!result.Success)
                {
                    throw new Exception($"Failed to switch profile: {result.Message}");
                }

                Debug.Log($"[SelectProfileCommand] Profile selected successfully: {profileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SelectProfileCommand] Failed to select profile: {ex.Message}");
                throw;
            }
        }

        public void Undo()
        {
            if (!string.IsNullOrEmpty(previousProfileId))
            {
                try
                {
                    Debug.Log($"[SelectProfileCommand] Reverting to previous profile: {previousProfileId}");

                    // Execute synchronously since we're in Undo
                    var switchTask = profileManager.SwitchActiveProfileAsync(previousProfileId);
                    switchTask.Wait(); // Wait for the async operation to complete

                    Debug.Log($"[SelectProfileCommand] Successfully reverted to previous profile: {previousProfileId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SelectProfileCommand] Failed to revert to previous profile: {ex.Message}");
                }
            }
        }
    }
}

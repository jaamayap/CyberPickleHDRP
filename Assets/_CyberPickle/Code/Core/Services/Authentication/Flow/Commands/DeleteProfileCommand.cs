// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/DeleteProfileCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Linq;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class DeleteProfileCommand : IAuthCommand
    {
        private readonly ProfileManager profileManager;
        private readonly string profileId;
        private ProfileData deletedProfileBackup;
        private bool wasActiveProfile;

        public DeleteProfileCommand(ProfileManager profileManager, string profileId)
        {
            this.profileManager = profileManager;
            this.profileId = profileId;
        }

        public async Task Execute()
        {
            Debug.Log($"[DeleteProfileCommand] Preparing to delete profile: {profileId}");

            try
            {
                // Store a backup of the profile before deletion
                var profileToDelete = profileManager.GetProfile(profileId);
                if (profileToDelete == null)
                {
                    throw new Exception($"Profile not found: {profileId}");
                }

                // Create backup
                deletedProfileBackup = profileToDelete;

                // Execute the delete operation
                var result = await profileManager.DeleteProfileAsync(profileId);
                if (!result.Success)
                {
                    throw new Exception(result.Message);
                }

                // Notify that the profile was deleted
                GameEvents.OnProfileDeleted.Invoke(profileId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DeleteProfileCommand] Failed to delete profile: {ex.Message}");
                throw;
            }
        }


        public void Undo()
        {
            if (deletedProfileBackup != null)
            {
                try
                {
                    Debug.Log($"[DeleteProfileCommand] Restoring deleted profile: {profileId}");

                    // Restore the profile
                    RestoreProfileCommand restoreCommand = new RestoreProfileCommand(
                        profileManager,
                        deletedProfileBackup);

                    // Execute synchronously since we're in Undo
                    restoreCommand.Execute().GetAwaiter().GetResult();

                    // If this was the active profile, switch back to it
                    if (wasActiveProfile)
                    {
                        SelectProfileCommand selectCommand = new SelectProfileCommand(
                            profileManager,
                            profileId);
                        selectCommand.Execute().GetAwaiter().GetResult();
                    }

                    Debug.Log($"[DeleteProfileCommand] Profile restored successfully: {profileId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DeleteProfileCommand] Failed to restore profile: {ex.Message}");
                }
            }
        }
    }
}
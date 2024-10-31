// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/RestoreProfileCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class RestoreProfileCommand : IAuthCommand
    {
        private readonly ProfileManager profileManager;
        private readonly ProfileData profileToRestore;
        private ProfileData existingProfileBackup;

        public RestoreProfileCommand(ProfileManager profileManager, ProfileData profileToRestore)
        {
            this.profileManager = profileManager;
            this.profileToRestore = profileToRestore;
        }

        public async Task Execute()
        {
            Debug.Log($"[RestoreProfileCommand] Restoring profile: {profileToRestore.ProfileId}");

            try
            {
                // Check if a profile with this ID already exists
                var existingProfile = profileManager.GetProfile(profileToRestore.ProfileId);
                if (existingProfile != null)
                {
                    // Backup the existing profile before overwriting
                    existingProfileBackup = existingProfile;

                    // Delete existing profile
                    var deleteResult = await profileManager.DeleteProfileAsync(existingProfile.ProfileId);
                    if (!deleteResult.Success)
                    {
                        throw new Exception($"Failed to delete existing profile: {deleteResult.Message}");
                    }
                }

                // Restore the profile
                var addResult = await profileManager.AddProfileAsync(profileToRestore);
                if (!addResult.Success)
                {
                    throw new Exception($"Failed to restore profile: {addResult.Message}");
                }

                // Notify that the profile was restored
                GameEvents.OnProfileRestored?.Invoke(profileToRestore.ProfileId);

                Debug.Log($"[RestoreProfileCommand] Profile restored successfully: {profileToRestore.ProfileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RestoreProfileCommand] Failed to restore profile: {ex.Message}");
                throw;
            }
        }

        public void Undo()
        {
            try
            {
                Debug.Log($"[RestoreProfileCommand] Undoing profile restoration: {profileToRestore.ProfileId}");

                // Remove the restored profile
                var deleteTask = profileManager.DeleteProfileAsync(profileToRestore.ProfileId);
                deleteTask.Wait(); // Wait for the async operation to complete

                // If we backed up an existing profile, restore it
                if (existingProfileBackup != null)
                {
                    var restoreTask = profileManager.AddProfileAsync(existingProfileBackup);
                    restoreTask.Wait(); // Wait for the async operation to complete
                }

                Debug.Log($"[RestoreProfileCommand] Restoration undone for profile: {profileToRestore.ProfileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RestoreProfileCommand] Failed to undo profile restoration: {ex.Message}");
            }
        }
    }
}


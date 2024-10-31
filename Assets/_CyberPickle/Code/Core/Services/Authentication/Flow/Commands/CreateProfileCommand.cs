// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/CreateProfileCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class CreateProfileCommand : IAuthCommand
    {
        private readonly ProfileManager profileManager;
        private readonly string displayName;
        private string createdProfileId;
        private bool wasSuccessful;

        public CreateProfileCommand(ProfileManager profileManager, string displayName)
        {
            this.profileManager = profileManager;
            this.displayName = displayName;
        }

        public async Task Execute()
        {
            Debug.Log($"[CreateProfileCommand] Creating profile for: {displayName}");

            try
            {
                // Generate a unique profile ID
                createdProfileId = $"{displayName.ToLower()}_{DateTime.UtcNow.Ticks}";

                // Get the current authenticated player ID
                string playerId = AuthenticationManager.Instance.CurrentPlayerId;

                // Create the profile
                var result = await profileManager.CreateProfileAsync(createdProfileId, playerId, displayName);

                if (!result.Success)
                {
                    throw new Exception(result.Message);
                }

                wasSuccessful = true;
                Debug.Log($"[CreateProfileCommand] Profile created successfully: {createdProfileId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CreateProfileCommand] Failed to create profile: {ex.Message}");
                wasSuccessful = false;
                throw;
            }
        }

        public void Undo()
        {
            if (wasSuccessful && !string.IsNullOrEmpty(createdProfileId))
            {
                try
                {
                    Debug.Log($"[CreateProfileCommand] Undoing profile creation: {createdProfileId}");

                    // Execute synchronously since we're in Undo
                    var deleteTask = profileManager.DeleteProfileAsync(createdProfileId);
                    deleteTask.Wait(); // Wait for the async operation to complete

                    Debug.Log($"[CreateProfileCommand] Successfully undid profile creation: {createdProfileId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[CreateProfileCommand] Failed to undo profile creation: {ex.Message}");
                }
            }
        }
    }
}

// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/LoadProfilesCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class LoadProfilesCommand : IAuthCommand
    {
        private readonly ProfileManager profileManager;
        private List<ProfileData> loadedProfiles;

        public LoadProfilesCommand(ProfileManager profileManager)
        {
            this.profileManager = profileManager;
        }

        public async Task Execute()
        {
            Debug.Log("[LoadProfilesCommand] Loading profiles");

            try
            {
                // Wait for profile container to load
                await Task.Yield(); // Ensure we're not blocking the main thread

                loadedProfiles = profileManager.GetAllProfiles().ToList();
                Debug.Log($"[LoadProfilesCommand] Successfully loaded {loadedProfiles.Count} profiles");

                // Notify UI that profiles are loaded
                GameEvents.OnProfilesLoaded?.Invoke(loadedProfiles);

                // Give time for any UI updates to complete
                await Task.Yield();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LoadProfilesCommand] Failed to load profiles: {ex.Message}");
                throw;
            }
        }

        public void Undo()
        {
            Debug.Log("[LoadProfilesCommand] Undoing profile load");
            GameEvents.OnProfilesCleared?.Invoke();
        }
    }
}
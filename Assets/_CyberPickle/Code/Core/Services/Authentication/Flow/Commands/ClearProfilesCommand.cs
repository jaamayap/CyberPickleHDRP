// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/ClearProfilesCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class ClearProfilesCommand : IAuthCommand
    {
        private readonly ProfileManager profileManager;
        private List<ProfileData> backupProfiles;

        public ClearProfilesCommand(ProfileManager profileManager)
        {
            this.profileManager = profileManager;
        }

        public async Task Execute()
        {
            Debug.Log("[ClearProfilesCommand] Executing profile clear");
            // Backup current profiles before clearing
            backupProfiles = profileManager.GetAllProfiles().ToList();
            await profileManager.ClearAllProfilesAsync();
            Debug.Log("[ClearProfilesCommand] Profiles cleared successfully");
        }

        public void Undo()
        {
            if (backupProfiles != null)
            {
                Debug.Log("[ClearProfilesCommand] Undoing profile clear");
                foreach (var profile in backupProfiles)
                {
                    try
                    {
                        var addTask = profileManager.AddProfileAsync(profile);
                        addTask.Wait();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ClearProfilesCommand] Failed to restore profile during undo: {ex.Message}");
                    }
                }
                Debug.Log($"[ClearProfilesCommand] Restored {backupProfiles.Count} profiles");
            }
        }
    }
}
// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/Commands/StartAuthenticationCommand.cs
using UnityEngine;
using System;
using System.Threading.Tasks;

namespace CyberPickle.Core.Services.Authentication.Flow.Commands
{
    public class StartAuthenticationCommand : IAuthCommand
    {
        private readonly AuthenticationManager authManager;
        private bool isAuthenticated;

        public StartAuthenticationCommand(AuthenticationManager authManager)
        {
            this.authManager = authManager;
        }

        public async Task Execute()
        {
            Debug.Log("[StartAuthCommand] Starting authentication");
            isAuthenticated = await authManager.SignInAnonymouslyAsync();

            if (isAuthenticated)
            {
                Debug.Log("[StartAuthCommand] Authentication successful");
            }
            else
            {
                Debug.LogError("[StartAuthCommand] Authentication failed");
                throw new Exception("Authentication failed");
            }
        }

        public void Undo()
        {
            if (isAuthenticated)
            {
                authManager.SignOut();
            }
        }
    }
}

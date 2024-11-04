// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/States/AuthenticatingState.cs
using UnityEngine;
using System.Threading.Tasks;
using CyberPickle.Core.Services.Authentication.Flow.Commands;

namespace CyberPickle.Core.Services.Authentication.Flow.States
{
    public class AuthenticatingState : IAuthenticationState
    {
        private readonly AuthenticationFlowManager flowManager;
        private readonly AuthenticationManager authManager;

        public AuthenticatingState(AuthenticationFlowManager flowManager)
        {
            this.flowManager = flowManager;
            this.authManager = AuthenticationManager.Instance;
        }

        public async void Enter()
        {
            Debug.Log("[AuthenticatingState] Starting authentication");
            var command = new StartAuthenticationCommand(authManager);
            await flowManager.ExecuteCommand(command);

            // Only transition to ProfileSelectionState after successful authentication
            if (authManager.IsSignedIn)
            {
                flowManager.TransitionTo<ProfileSelectionState>();
            }
        }

        public void Exit()
        {
            Debug.Log("[AuthenticatingState] Authentication completed");
        }

        public void Update() { }

        public bool CanTransitionTo(IAuthenticationState nextState)
        {
            // Can only transition to ProfileSelectionState if authenticated
            return nextState is ProfileSelectionState && authManager.IsSignedIn;
        }
    }
}

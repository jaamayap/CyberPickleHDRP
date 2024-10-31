// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/States/ProfileSelectionState.cs
using UnityEngine;
using CyberPickle.Core.Services.Authentication.Flow.Commands;

namespace CyberPickle.Core.Services.Authentication.Flow.States
{
    public class ProfileSelectionState : IAuthenticationState
    {
        private readonly AuthenticationFlowManager flowManager;
        private readonly ProfileManager profileManager;

        public ProfileSelectionState(AuthenticationFlowManager flowManager)
        {
            this.flowManager = flowManager;
            this.profileManager = ProfileManager.Instance;
        }

        public async void Enter()
        {
            Debug.Log("[ProfileSelectionState] Entering profile selection");
            var command = new LoadProfilesCommand(profileManager);
            await flowManager.ExecuteCommand(command);
        }

        public void Exit()
        {
            Debug.Log("[ProfileSelectionState] Exiting profile selection");
        }

        public void Update() { }

        public bool CanTransitionTo(IAuthenticationState nextState)
        {
            return nextState is MainMenuState || nextState is AuthenticatingState;
        }
    }
}


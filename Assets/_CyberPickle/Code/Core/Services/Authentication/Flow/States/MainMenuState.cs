// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/States/MainMenuState.cs
using CyberPickle.Core.Events;
using CyberPickle.Core.States;
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Flow.States
{
    public class MainMenuState : IAuthenticationState
    {
        private readonly AuthenticationFlowManager flowManager;

        public MainMenuState(AuthenticationFlowManager flowManager)
        {
            this.flowManager = flowManager;
        }

        public void Enter()
        {
            Debug.Log("[MainMenuState] Entering main menu");
            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        public void Exit()
        {
            Debug.Log("[MainMenuState] Exiting main menu");
        }

        public void Update() { }

        public bool CanTransitionTo(IAuthenticationState nextState)
        {
            return nextState is ProfileSelectionState;
        }
    }
}

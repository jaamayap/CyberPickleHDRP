// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/States/InitialState.cs
using UnityEngine;

namespace CyberPickle.Core.Services.Authentication.Flow.States
{
    public class InitialState : IAuthenticationState
    {
        private readonly AuthenticationFlowManager flowManager;

        public InitialState(AuthenticationFlowManager flowManager)
        {
            this.flowManager = flowManager;
        }

        public void Enter()
        {
            Debug.Log("[InitialState] Entered");
        }

        public void Exit()
        {
            Debug.Log("[InitialState] Exited");
        }

        public void Update() { }

        public bool CanTransitionTo(IAuthenticationState nextState)
        {
            return nextState is AuthenticatingState;
        }
    }
}

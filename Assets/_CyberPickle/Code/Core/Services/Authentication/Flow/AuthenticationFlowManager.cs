// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/AuthenticationFlowManager.cs
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CyberPickle.Core.Management;
using CyberPickle.Core.Events;
using CyberPickle.Core.Interfaces;
using CyberPickle.Core.Services.Authentication.Flow.States;
using CyberPickle.Core.Services.Authentication.Flow.Commands;



namespace CyberPickle.Core.Services.Authentication.Flow
{
    public class AuthenticationFlowManager : Manager<AuthenticationFlowManager>, IInitializable
    {
        private IAuthenticationState currentState;
        private Stack<IAuthCommand> executedCommands = new Stack<IAuthCommand>();
        private Dictionary<Type, IAuthenticationState> states;
        private bool isInitialized;

        public IAuthenticationState CurrentState => currentState;
        public event Action<IAuthenticationState> OnStateChanged;

        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("[AuthFlowManager] Initializing");
            InitializeStates();
            isInitialized = true;
            TransitionTo<InitialState>();
        }

        private void InitializeStates()
        {
            states = new Dictionary<Type, IAuthenticationState>
            {
                { typeof(InitialState), new InitialState(this) },
                { typeof(AuthenticatingState), new AuthenticatingState(this) },
                { typeof(ProfileSelectionState), new ProfileSelectionState(this) },
                { typeof(MainMenuState), new MainMenuState(this) }
            };
        }

        public async Task ExecuteCommand(IAuthCommand command)
        {
            try
            {
                Debug.Log($"[AuthFlowManager] Executing command: {command.GetType().Name}");
                await command.Execute();
                executedCommands.Push(command);
                Debug.Log($"[AuthFlowManager] Command executed successfully: {command.GetType().Name}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthFlowManager] Command execution failed: {ex.Message}");
                throw;
            }
        }

        public void TransitionTo<T>() where T : IAuthenticationState
        {
            var nextState = states[typeof(T)];

            Debug.Log($"[AuthFlowManager] Attempting transition from {currentState?.GetType().Name ?? "null"} to {typeof(T).Name}");

            if (currentState?.CanTransitionTo(nextState) ?? true)
            {
                currentState?.Exit();
                currentState = nextState;
                OnStateChanged?.Invoke(currentState);
                currentState.Enter();

                Debug.Log($"[AuthFlowManager] Transitioned to {typeof(T).Name}");
            }
            else
            {
                Debug.LogWarning($"[AuthFlowManager] Invalid state transition from {currentState.GetType().Name} to {typeof(T).Name}");
            }
        }

        public bool IsInState<T>() where T : IAuthenticationState
        {
            return currentState?.GetType() == typeof(T);
        }
    }
}
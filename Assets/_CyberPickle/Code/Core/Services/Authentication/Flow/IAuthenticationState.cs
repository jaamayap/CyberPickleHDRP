// File: Assets/_CyberPickle/Code/Core/Services/Authentication/Flow/IAuthenticationState.cs
namespace CyberPickle.Core.Services.Authentication.Flow
{
    public interface IAuthenticationState
    {
        void Enter();
        void Exit();
        void Update();
        bool CanTransitionTo(IAuthenticationState nextState);
    }
}

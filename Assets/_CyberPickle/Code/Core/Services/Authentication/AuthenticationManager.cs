using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Services.Authentication
{
    public class AuthenticationManager : Manager<AuthenticationManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("AuthenticationManager initialized");
        }
    }
}

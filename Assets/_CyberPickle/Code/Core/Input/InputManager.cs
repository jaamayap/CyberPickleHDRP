using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Input
{
    public class InputManager : Manager<InputManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("InputManager initialized");
        }
    }
}
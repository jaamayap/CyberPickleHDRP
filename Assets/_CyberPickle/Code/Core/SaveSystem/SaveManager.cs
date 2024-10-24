using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.SaveSystem
{
    public class SaveManager : Manager<SaveManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("SaveManager initialized");
        }
    }
}

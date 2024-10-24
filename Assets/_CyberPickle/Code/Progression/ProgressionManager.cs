using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Progression
{
    public class ProgressionManager : Manager<ProgressionManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("ProgressionManager initialized");
        }
    }
}

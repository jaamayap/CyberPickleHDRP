using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Audio
{
    public class AudioManager : Manager<AudioManager>, IInitializable
    {
        public void Initialize()
        {
            // Basic initialization
            UnityEngine.Debug.Log("AudioManager initialized");
        }
    }
}

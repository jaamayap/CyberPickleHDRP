using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Characters
{
    public class CharacterManager : Manager<CharacterManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("CharacterManager initialized");
        }
    }
}

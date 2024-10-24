using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;
using UnityEngine;

namespace CyberPickle.Achievements
{
    public class AchievementManager : Manager<AchievementManager>, IInitializable
    {
        public void Initialize()
        {
            Debug.Log("Initializing Achievement Manager");
        }
    }
}

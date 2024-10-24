using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Services.Leaderboard
{
    public class LeaderboardManager : Manager<LeaderboardManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("LeaderboardManager initialized");
        }
    }
}

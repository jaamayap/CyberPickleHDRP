using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Analytics
{
    public class AnalyticsManager : Manager<AnalyticsManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("AnalyticsManager initialized");
        }
    }
}

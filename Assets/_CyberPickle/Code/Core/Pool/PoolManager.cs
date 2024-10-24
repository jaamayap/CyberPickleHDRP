using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Pool
{
    public class PoolManager : Manager<PoolManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("PoolManager initialized");
        }
    }
}

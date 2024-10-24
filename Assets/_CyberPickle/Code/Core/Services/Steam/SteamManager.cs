// Core/Services/Steam/SteamManager.cs
using CyberPickle.Core.Management;
using CyberPickle.Core.Interfaces;

namespace CyberPickle.Core.Services.Steam
{
    public class SteamManager : Manager<SteamManager>, IInitializable
    {
        public void Initialize()
        {
            UnityEngine.Debug.Log("SteamManager initialized");
        }
    }
}

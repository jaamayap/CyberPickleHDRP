using UnityEngine;

namespace CyberPickle.Core.Config
{
    [CreateAssetMenu(fileName = "BootConfig", menuName = "CyberPickle/Config/Boot")]
    public class BootConfig : ScriptableObject
    {
        [Header("Scene Settings")]
        public string mainMenuSceneName = "MainMenu";

        [Header("Timing Settings")]
        public float fadeInDuration = 1.5f;
        public float displayDuration = 2.0f;
        public float fadeOutDuration = 1.5f;
        public float minimumLoadingTime = 3.0f;

        [Header("Loading Settings")]
        public bool waitForInput = true;
        public string[] loadingTips;
    }
}

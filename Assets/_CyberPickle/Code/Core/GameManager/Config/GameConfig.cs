using UnityEngine;

namespace CyberPickle.Core.Config
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "CyberPickle/Config/Game")]
    public class GameConfig : ScriptableObject
    {
        [Header("Scene Names")]
        public string mainMenuSceneName = "MainMenu";
        //public string characterSelectSceneName = "CharacterSelect";
        public string equipmentSelectSceneName = "EquipmentHub";
        public string levelSelectSceneName = "LevelSelect";
        public string gameSceneName = "Game";
        public string postGameSceneName = "PostGame";

        [Header("Game Settings")]
        public float gameStartDelay = 3f;
        public float gameOverDelay = 2f;
        public bool enableTutorial = true;

        [Header("Debug Settings")]
        public bool enableDebugMode = false;
        public bool skipBootSequence = false;
        public bool unlockAllCharacters = false;
    }
}

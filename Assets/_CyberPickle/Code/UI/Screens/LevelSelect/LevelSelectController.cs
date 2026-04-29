// File: Assets/_CyberPickle/Code/UI/Screens/LevelSelect/LevelSelectController.cs
// Namespace: CyberPickle.UI.Screens.LevelSelect
//
// Purpose: Placeholder LevelSelect screen controller.
// Exposes a single button hook (OnStartLevelClicked) that asks the
// GameManager to start a fixed test level. When real level data lands
// this will be replaced by a level-list UI driven by data assets.

using UnityEngine;
using CyberPickle.Core;

namespace CyberPickle.UI.Screens.LevelSelect
{
    public class LevelSelectController : MonoBehaviour
    {
        [Header("Test Level")]
        [SerializeField] private string testLevelId = "Level_01";

        /// <summary>
        /// Hooked to a UI Button's OnClick event. Asks GameManager to
        /// load the gameplay scene and transition to the Playing state.
        /// </summary>
        public void OnStartLevelClicked()
        {
            if (GameManager.Instance == null)
            {
                Debug.LogError("[LevelSelectController] GameManager.Instance is null. " +
                               "Are you running this scene without booting through Boot.unity? " +
                               "Press Play from Boot scene to initialize managers.");
                return;
            }

            Debug.Log($"[LevelSelectController] Start clicked. Requesting level: {testLevelId}");
            GameManager.Instance.StartLevel(testLevelId);
        }
    }
}

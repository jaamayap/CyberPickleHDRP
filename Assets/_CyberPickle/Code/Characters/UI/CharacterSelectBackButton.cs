// File: Assets/_CyberPickle/Code/Characters/UI/CharacterSelectBackButton.cs
// Namespace: CyberPickle.Characters.UI
//
// Drop on the Back button GameObject inside CanvasCharacterSelectionScreen.
// Single responsibility: navigate from the character-select view back to
// the main menu (Start / Options / Quit).
//
// Distinct from OnCharacterSelectionCancelled — that's the per-character
// "cancel my pick" button on the confirmation panel and only zooms the camera
// out from a focused character to the wide selection view (keeping the user
// in character select). THIS button takes the user one step further back
// to the main menu.
//
// Why this is a separate small component:
//   - The button lives on a UI Canvas; the rest of the navigation logic lives
//     in CharacterSelectionManager / CameraManager / MainMenuController. A
//     single-purpose adapter is the cleanest seam.
//   - All it does is fire GameEvents.OnGameStateChanged(GameState.MainMenu);
//     the existing handlers do the actual work (camera animates back, hub
//     panels fade in, character pre-spawn cache is preserved by hide-not-
//     destroy logic in CharacterSelectionManager).

using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Core.Events;
using CyberPickle.Core.States;

namespace CyberPickle.Characters.UI
{
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public class CharacterSelectBackButton : MonoBehaviour
    {
        private Button backButton;
        private bool isTransitioning;

        private void Awake()
        {
            backButton = GetComponent<Button>();
        }

        private void OnEnable()
        {
            backButton.onClick.AddListener(HandleClick);
            // We listen for state changes so we can re-arm whenever
            // CharacterSelect is entered. The OnEnable reset alone is
            // insufficient because CanvasCharacterSelectionScreen stays
            // active even when the user is "in" the main menu — there's
            // no SetActive toggle to trigger OnEnable on re-entry.
            GameEvents.OnGameStateChanged.AddListener(HandleGameStateChanged);
            ArmButton();
        }

        private void OnDisable()
        {
            backButton.onClick.RemoveListener(HandleClick);
            GameEvents.OnGameStateChanged.RemoveListener(HandleGameStateChanged);
        }

        private void HandleClick()
        {
            // Guard against multi-click during the 1.5s camera transition —
            // double-firing OnGameStateChanged(MainMenu) would re-trigger the
            // panel fades and animate the title again, looking glitchy.
            if (isTransitioning) return;
            isTransitioning = true;
            backButton.interactable = false;

            Debug.Log("[CharacterSelectBackButton] Returning to main menu.");
            GameEvents.OnGameStateChanged.Invoke(GameState.MainMenu);
        }

        private void HandleGameStateChanged(GameState newState)
        {
            // Re-arm whenever CharacterSelect is (re-)entered: the user just
            // arrived (or returned from EquipmentHub via post-load broadcast).
            // The button is once again the right action.
            //
            // We deliberately do NOT re-arm on other state transitions (e.g.,
            // immediately on MainMenu) because the back button might still be
            // visible behind the camera mid-transition; making it interactable
            // there would invite double-clicks that fire MainMenu twice.
            if (newState == GameState.CharacterSelect)
            {
                ArmButton();
            }
        }

        private void ArmButton()
        {
            isTransitioning = false;
            if (backButton != null) backButton.interactable = true;
        }
    }
}

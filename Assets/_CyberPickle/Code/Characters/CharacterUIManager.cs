// File: Assets/_CyberPickle/Code/Characters/CharacterUIManager.cs
//
// Purpose: Manages the UI elements for character selection, including hover state,
// details panel, and stat displays. Works with CharacterSelectionManager to provide
// visual feedback and interaction options.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using CyberPickle.Core.Management;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Characters.Data;
using DG.Tweening;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Core.Events;
using System.Threading.Tasks;

namespace CyberPickle.Characters
{
    public class CharacterUIManager : Manager<CharacterUIManager>
    {
        [Header("Details Panel")]
        [SerializeField] private CanvasGroup detailsPanel;
        [SerializeField] private TextMeshProUGUI detailsCharacterNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Transform statsContainer;
        [SerializeField] private TextMeshProUGUI loreText;
        [SerializeField] private GameObject statRowPrefab;

        [Header("Hover Panel")]
        [SerializeField] private CanvasGroup hoverPanel;
        [SerializeField] private TextMeshProUGUI hoverCharacterNameText;
        [SerializeField] private GameObject leftClickPrompt;
        [SerializeField] private GameObject rightClickPrompt;

        [Header("Unlock Panel")]
        [SerializeField] private CanvasGroup unlockPanel;
        [SerializeField] private TextMeshProUGUI unlockRequirementsText;

        [Header("Confirm Selection Panel")]
        [SerializeField] private CanvasGroup confirmationPanelPrefab; // Prefab reference
        private CanvasGroup confirmationPanel; // Runtime reference to instantiated panel


        [Header("Animation Settings")]
        [SerializeField] private float panelFadeDuration = 0.3f;
        [SerializeField] private float statUpdateDuration = 0.5f;
        [SerializeField] private float panelTransitionDuration = 0.8f;

        [Header("Positioning")]
        [SerializeField] private Vector3 hoverPanelOffset = new Vector3(0, 2, 0);
        [SerializeField] private Vector3 detailsPanelOffsetGeneral = new Vector3(2, 1, 0); // Offset in general view
        [SerializeField] private Vector3 detailsPanelOffsetFocused = new Vector3(-1, 0, 0); // Offset in focused view
        [SerializeField] private Vector3 confirmationPanelOffset = new Vector3(0, 2, 0);

        // Dependencies
        private CharacterSelectionManager characterSelectionManager;
        private ProfileManager profileManager;

        // State tracking
        private string currentCharacterId;
        private Dictionary<string, TextMeshProUGUI> statTextCache;
        private Dictionary<string, CharacterProgressionData> progressionCache;

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            InitializeManagers();
            ValidateReferences();
            InitializeCache();
        }

        public void UpdatePanelPositions(Transform characterPosition)
        {
            if (characterPosition == null) return;

            // Animate panel position if transitioning from preview to selected
            if (detailsPanel != null && characterSelectionManager.IsCharacterSelected(currentCharacterId))
            {
                Vector3 targetPosition = characterPosition.position + detailsPanelOffsetFocused;
                detailsPanel.transform.DOLocalMove(targetPosition, panelTransitionDuration)
                    .SetEase(Ease.InOutQuad) // Ease-in-out for smooth sliding
                    .SetUpdate(true); // Ensure update in late update for smooth movement
                detailsPanel.transform.DOScale(new Vector3(0.5f, 0.5f, 0.5f), panelTransitionDuration)
                    .SetEase(Ease.InOutQuad);
            }
            else if (detailsPanel != null)
            {
                // Preview state: position above the head (no animation needed here, handled by ShowDetails)
                Vector3 targetPosition = characterPosition.position + detailsPanelOffsetGeneral;
                detailsPanel.transform.position = targetPosition;
                detailsPanel.transform.localScale = Vector3.one;
            }

            // Position other panels as before (hover, unlock, confirmation)
            if (hoverPanel != null)
            {
                hoverPanel.transform.position = characterPosition.position + hoverPanelOffset;
            }

            if (unlockPanel != null)
            {
                unlockPanel.transform.position = characterPosition.position + hoverPanelOffset;
            }

            if (confirmationPanel != null)
            {
                confirmationPanel.transform.position = characterPosition.position + confirmationPanelOffset;
            }
        }
        private void InitializeManagers()
        {
            characterSelectionManager = CharacterSelectionManager.Instance;
            profileManager = ProfileManager.Instance;

            if (!ValidateManagerReferences())
            {
                Debug.LogError("[CharacterUIManager] Failed to initialize required managers!");
            }
        }

        private bool ValidateManagerReferences()
        {
            return characterSelectionManager != null && profileManager != null;
        }

        private void ValidateReferences()
        {
            if (detailsPanel == null) Debug.LogError("[CharacterUIManager] Details panel is missing!");
            if (hoverPanel == null) Debug.LogError("[CharacterUIManager] Hover panel is missing!");
            if (unlockPanel == null) Debug.LogError("[CharacterUIManager] Unlock panel is missing!");
            if (statsContainer == null) Debug.LogError("[CharacterUIManager] Stats container is missing!");
            if (statRowPrefab == null) Debug.LogError("[CharacterUIManager] Stat row prefab is missing!");
        }

        private void InitializeCache()
        {
            statTextCache = new Dictionary<string, TextMeshProUGUI>();
            progressionCache = new Dictionary<string, CharacterProgressionData>();
            HideAllPanels();
        }

        public void Initialize(CharacterData[] characters)
        {
            ClearUI();
            foreach (var character in characters)
            {
                if (character != null)
                {
                    progressionCache[character.characterId] = GetCharacterProgression(character.characterId);
                }
            }
            Debug.Log("[CharacterUIManager] Initialized with " + characters.Length + " characters");
        }

        public void ShowCharacterPreview(CharacterData character)
        {
            if (character == null) return;

            currentCharacterId = character.characterId;
            bool isUnlocked = characterSelectionManager.IsCharacterUnlocked(character.characterId);

            // Always reset panels before updating
            HideAllPanels();

            // Update hover panel content
            if (hoverCharacterNameText != null)
            {
                hoverCharacterNameText.text = character.displayName;
            }

            // Hide click prompts for locked characters
            if (leftClickPrompt != null) leftClickPrompt.SetActive(isUnlocked);
            if (rightClickPrompt != null) rightClickPrompt.SetActive(isUnlocked);

            // Show the appropriate panel based on unlock state
            if (isUnlocked)
            {
                SetPanelState(hoverPanel, true);
            }
            else
            {
                ShowUnlockRequirements(character);
            }
        }

        public void ShowDetails(string characterId)
        {
            var character = characterSelectionManager.GetCharacterData(characterId);
            if (character == null) return;

            currentCharacterId = characterId;

            // Update details panel content
            if (detailsCharacterNameText != null)
            {
                detailsCharacterNameText.text = character.displayName;
            }

            if (loreText != null)
            {
                loreText.text = character.lore;
            }

            // Get progression data
            var progression = progressionCache.TryGetValue(characterId, out var cachedProgression)
                ? cachedProgression
                : GetCharacterProgression(characterId);

            if (levelText != null && progression != null)
            {
                levelText.text = $"Level {progression.CharacterLevel}";
            }

            // Update and show stats
            UpdateCharacterStats(character, progression);

            // Position the panel based on selection state
            var characterPosition = characterSelectionManager.GetCharacterGameObject(characterId)?.transform;
            if (characterPosition != null && detailsPanel != null)
            {
                // Ensure the panel is active and visible
                detailsPanel.gameObject.SetActive(true); // Reactivate if inactive
                SetPanelState(detailsPanel, true); // Set alpha to 1, enable interaction/raycasts

                if (characterSelectionManager.IsCharacterSelected(characterId))
                {
                    // Selected state: position instantly on the left
                    detailsPanel.transform.position = characterPosition.position + detailsPanelOffsetFocused;
                    detailsPanel.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                }
                else
                {
                    // Preview state: position above the head
                    detailsPanel.transform.position = characterPosition.position + detailsPanelOffsetGeneral;
                    detailsPanel.transform.localScale = Vector3.one;
                }
            }

            // Ensure panel is visible

            SetPanelState(hoverPanel, false);
            SetPanelState(unlockPanel, false);

        }

        public void ShowConfirmationPanel(bool show)
        {
            Debug.Log($"[CharacterUIManager] ShowConfirmationPanel called with show={show}");

            if (!show)
            {
                if (confirmationPanel != null)
                {
                    // Clean up existing listeners before destroying
                    var buttons = confirmationPanel.GetComponentsInChildren<Button>();
                    foreach (var button in buttons)
                    {
                        button.onClick.RemoveAllListeners();
                    }
                    Destroy(confirmationPanel.gameObject);
                    confirmationPanel = null;
                }
                return;
            }

            if (confirmationPanel == null)
            {
                if (confirmationPanelPrefab == null)
                {
                    Debug.LogError("[CharacterUIManager] Confirmation panel prefab is missing!");
                    return;
                }

                var instance = Instantiate(confirmationPanelPrefab, hoverPanel.transform.parent);
                confirmationPanel = instance.GetComponent<CanvasGroup>();

                if (confirmationPanel != null)
                {
                    var characterPosition = characterSelectionManager.GetCharacterGameObject(currentCharacterId)?.transform;
                    if (characterPosition != null)
                    {
                        confirmationPanel.transform.position = characterPosition.position + confirmationPanelOffset;
                    }
                }

                var buttons = instance.GetComponentsInChildren<Button>();
                foreach (var button in buttons)
                {
                    button.onClick.RemoveAllListeners(); // Clear any existing listeners
                    if (button.gameObject.name.Contains("Confirm"))
                    {
                        button.onClick.AddListener(() => {
                            Debug.Log("[CharacterUIManager] Confirm button clicked");
                            GameEvents.OnCharacterConfirmed?.Invoke();
                        });
                    }
                    else if (button.gameObject.name.Contains("Cancel"))
                    {
                        button.onClick.AddListener(() => {
                            Debug.Log("[CharacterUIManager] Cancel button clicked");
                            GameEvents.OnCharacterSelectionCancelled?.Invoke();
                        });
                    }
                }
            }

            if (confirmationPanel != null)
            {
                confirmationPanel.gameObject.SetActive(true);
                SetPanelState(confirmationPanel, true);
            }
        }

        public void UpdateCharacterProgress(string characterId, CharacterProgressionData progression)
        {
            if (string.IsNullOrEmpty(characterId) || progression == null) return;

            progressionCache[characterId] = progression;

            if (characterId == currentCharacterId)
            {
                var character = characterSelectionManager.GetCharacterData(characterId);
                if (character != null)
                {
                    UpdateCharacterStats(character, progression);
                }
            }
        }

        private void UpdateCharacterStats(CharacterData baseData, CharacterProgressionData progression)
        {
            ClearStatsContainer();

            CreateStatRow("Health", baseData.maxHealth, progression);
            CreateStatRow("Defense", baseData.defense, progression);
            CreateStatRow("Power", baseData.power, progression);
            CreateStatRow("Speed", baseData.speed, progression);
            CreateStatRow("Dexterity", baseData.dexterity, progression);
            CreateStatRow("Luck", baseData.luck, progression);
        }

        private void CreateStatRow(string statName, float baseValue, CharacterProgressionData progression)
        {
            if (statRowPrefab == null || statsContainer == null) return;

            GameObject statRow = Instantiate(statRowPrefab, statsContainer);
            var texts = statRow.GetComponentsInChildren<TextMeshProUGUI>();

            if (texts.Length >= 2)
            {
                texts[0].text = statName;
                texts[1].text = baseValue.ToString("F1");

                string statKey = $"{currentCharacterId}_{statName}";
                statTextCache[statKey] = texts[1];

                // Animate if progressed value exists
                if (progression?.Stats != null && progression.Stats.TryGetValue(statName, out float progressedValue))
                {
                    DOTween.To(
                        () => baseValue,
                        (float value) => texts[1].text = value.ToString("F1"),
                        progressedValue,
                        statUpdateDuration
                    );
                }
            }
        }

        public void ShowUnlockRequirements(CharacterData character)
        {
            if (unlockRequirementsText == null) return;

            string requirements = "Requirements to unlock:\n";

            if (character.requiredPlayerLevel > 1)
            {
                requirements += $"• Level {character.requiredPlayerLevel}\n";
            }

            if (character.requiredAchievements != null && character.requiredAchievements.Length > 0)
            {
                foreach (var achievement in character.requiredAchievements)
                {
                    requirements += $"• {achievement}\n";
                }
            }

            unlockRequirementsText.text = requirements;
            SetPanelState(unlockPanel, true);
        }

        public void HideUnlockPanel()
        {
            if (unlockPanel != null)
            {
                SetPanelState(unlockPanel, false); // Use the existing method to hide the panel
            }

            Debug.Log("[CharacterUIManager] Unlock panel hidden");
        }
        private CharacterProgressionData GetCharacterProgression(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || profileManager == null) return null;

            var profile = profileManager.ActiveProfile;
            if (profile?.CharacterProgress == null) return null;

            profile.CharacterProgress.TryGetValue(characterId, out var progression);
            return progression;
        }

        private void SetPanelState(CanvasGroup panel, bool visible)
        {
            if (panel == null) return;

            panel.DOFade(visible ? 1f : 0f, panelFadeDuration);
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }

        public void Cleanup()
        {
            ClearUI();
            if (confirmationPanel != null)
            {
                var buttons = confirmationPanel.GetComponentsInChildren<Button>();
                foreach (var button in buttons)
                {
                    button.onClick.RemoveAllListeners();
                }
                Destroy(confirmationPanel.gameObject);
                confirmationPanel = null;
            }
            progressionCache.Clear();
        }

        private void ClearUI()
        {
            HideAllPanels();
            ClearStatsContainer();
            currentCharacterId = null;
        }

        public async void HideAllPanels()
        {
            SetPanelState(hoverPanel, false);
            SetPanelState(unlockPanel, false);
            await AnimatePanelExit(panelTransitionDuration); // Await the details panel exit animation
        }

        public async Task AnimatePanelToFocusedPosition(Transform characterTransform, float duration)
        {
            if (detailsPanel == null || characterTransform == null) return;

            // Ensure the panel is active and visible before animating
            detailsPanel.gameObject.SetActive(true); // Reactivate if inactive
            SetPanelState(detailsPanel, true); // Set alpha to 1, enable interaction/raycasts

            Vector3 startPosition = detailsPanel.transform.position;
            Vector3 targetPosition = characterTransform.position + detailsPanelOffsetFocused;

            // Animate position and scale
            detailsPanel.transform.DOLocalMove(targetPosition, duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);
            detailsPanel.transform.DOScale(new Vector3(0.5f, 0.5f, 0.5f), duration)
                .SetEase(Ease.InOutQuad);

            // Wait for the animation to complete
            await detailsPanel.transform.DOLocalMove(targetPosition, duration).AsyncWaitForCompletion();
        }

        public async Task AnimatePanelExit(float duration)
        {
            if (detailsPanel == null) return;

            // Fade out and slide off (or just fade out for simplicity)
            Sequence sequence = DOTween.Sequence();
            sequence.Append(detailsPanel.DOFade(0f, duration).SetEase(Ease.InOutQuad)); // Fade out
            sequence.Join(detailsPanel.transform.DOLocalMoveY(detailsPanel.transform.position.y - 200f, duration)
                .SetEase(Ease.InOutQuad)); // Slide upward or off-screen

            // Wait for the animation to complete
            await sequence.AsyncWaitForCompletion();

            // Ensure panel is hidden after animation
            detailsPanel.gameObject.SetActive(false);
        }


        private void ClearStatsContainer()
        {
            if (statsContainer == null) return;

            foreach (Transform child in statsContainer)
            {
                Destroy(child.gameObject);
            }

            statTextCache.Clear();
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            Cleanup();
        }
    }
}
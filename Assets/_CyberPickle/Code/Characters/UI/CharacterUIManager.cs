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

namespace CyberPickle.Characters.UI
{
    /// <summary>
    /// Manages UI elements for character selection: hover panel, details panel,
    /// unlock requirements, and confirmation prompts.
    /// Only responsible for UI feedback/animations—logic or selection rules
    /// live in other managers.
    /// </summary>
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
        private CanvasGroup confirmationPanel; // Runtime reference

        [Header("Animation Settings")]
        [SerializeField] private float panelFadeDuration = 0.3f;
        [SerializeField] private float statUpdateDuration = 0.5f;
        [SerializeField] private float panelTransitionDuration = 0.8f;
        
        [Header("Positioning")]
        [SerializeField] private Vector3 hoverPanelOffset = new Vector3(0, 2, 0);
        [SerializeField] private Vector3 detailsPanelOffsetGeneral = new Vector3(2, 1, 0);
        [SerializeField] private Vector3 detailsPanelOffsetFocused = new Vector3(-1, 0, 0);
        [SerializeField] private Vector3 confirmationPanelOffset = new Vector3(0, 2, 0);

        // Dependencies
        private ProfileManager profileManager;

        // State
        private string currentCharacterId;
        private Dictionary<string, TextMeshProUGUI> statTextCache;
        private Dictionary<string, CharacterProgressionData> progressionCache;

        #region Manager Overrides

        protected override void OnManagerAwake()
        {
            base.OnManagerAwake();
            profileManager = ProfileManager.Instance;  // if needed for progression
            ValidateReferences();
            InitializeCache();
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            Cleanup();
        }

        #endregion

        #region Initialization

        private void ValidateReferences()
        {
            if (!detailsPanel) Debug.LogError("[CharacterUIManager] Details panel is missing!");
            if (!hoverPanel) Debug.LogError("[CharacterUIManager] Hover panel is missing!");
            if (!unlockPanel) Debug.LogError("[CharacterUIManager] Unlock panel is missing!");
            if (!statsContainer) Debug.LogError("[CharacterUIManager] Stats container is missing!");
            if (!statRowPrefab) Debug.LogError("[CharacterUIManager] Stat row prefab is missing!");
        }

        private void InitializeCache()
        {
            statTextCache = new Dictionary<string, TextMeshProUGUI>();
            progressionCache = new Dictionary<string, CharacterProgressionData>();
            HideAllPanelsImmediate();
        }

        /// <summary>
        /// Called externally once. Prepares progression data if needed, etc.
        /// </summary>
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
            Debug.Log($"[CharacterUIManager] Initialized with {characters.Length} characters");
        }

        #endregion

        #region Public UI Handling

        /// <summary>
        /// Updates the panel positions relative to the given character's transform.
        /// Should be called each frame or on hover/selection changes.
        /// </summary>
        public void UpdatePanelPositions(Transform characterPosition)
        {
            if (!characterPosition) return;

            // Keep the hover/unlock/confirmation panels updated if you like
            if (hoverPanel)
            {
                hoverPanel.transform.position = characterPosition.position + hoverPanelOffset;
            }

            if (unlockPanel)
            {
                unlockPanel.transform.position = characterPosition.position + hoverPanelOffset;
            }

            if (confirmationPanel)
            {
                confirmationPanel.transform.position = characterPosition.position + confirmationPanelOffset;
            }

            // SKIP MOVING THE DETAILS PANEL HERE, to avoid overriding the tween
        }

        /// <summary>
        /// Shows the hover panel or unlock panel depending on unlock status.
        /// </summary>
        public void ShowCharacterPreview(CharacterData character, bool isUnlocked)
        {
            if (!character) return;
            currentCharacterId = character.characterId;

            // Reset panels first
            HideAllPanelsImmediate();

            if (hoverCharacterNameText) hoverCharacterNameText.text = character.displayName;
            if (leftClickPrompt) leftClickPrompt.SetActive(isUnlocked);
            if (rightClickPrompt) rightClickPrompt.SetActive(isUnlocked);

            // Show hover panel if unlocked, else show unlock requirements
            if (isUnlocked)
            {
                SetPanelState(hoverPanel, true);
            }
            else
            {
                ShowUnlockRequirements(character);
            }
        }

        /// <summary>
        /// Fills and shows the character details panel: name, lore, stats, etc.
        /// </summary>
        public void ShowDetails(CharacterData character)
        {
            if (!character) return;
            currentCharacterId = character.characterId;

            if (detailsCharacterNameText) detailsCharacterNameText.text = character.displayName;
            if (loreText) loreText.text = character.lore;

            var progression = progressionCache.TryGetValue(character.characterId, out var cachedProg)
                ? cachedProg
                : GetCharacterProgression(character.characterId);

            if (levelText && progression != null)
            {
                levelText.text = $"Level {progression.CharacterLevel}";
            }

            UpdateCharacterStats(character, progression);
            SetPanelState(detailsPanel, true);

            // Hide other panels
            SetPanelState(hoverPanel, false);
            SetPanelState(unlockPanel, false);
        }

        /// <summary>
        /// Displays the confirmation panel prefab for final selection,
        /// or hides it if show=false.
        /// </summary>
        public void ShowConfirmationPanel(bool show)
        {
            if (!show)
            {
                if (confirmationPanel)
                {
                    var buttons = confirmationPanel.GetComponentsInChildren<Button>();
                    foreach (var b in buttons) b.onClick.RemoveAllListeners();
                    Destroy(confirmationPanel.gameObject);
                    confirmationPanel = null;
                }
                return;
            }

            if (!confirmationPanel && confirmationPanelPrefab)
            {
                var instance = Instantiate(confirmationPanelPrefab, hoverPanel.transform.parent);
                confirmationPanel = instance.GetComponent<CanvasGroup>();

                // Positioning
                if (confirmationPanel)
                {
                    SetPanelState(confirmationPanel, true);
                }

                // Hook up confirm/cancel buttons
                var buttons = instance.GetComponentsInChildren<Button>();
                foreach (var button in buttons)
                {
                    button.onClick.RemoveAllListeners(); // clear existing
                    if (button.gameObject.name.Contains("Confirm"))
                    {
                        button.onClick.AddListener(() => GameEvents.OnCharacterConfirmed?.Invoke());
                    }
                    else if (button.gameObject.name.Contains("Cancel"))
                    {
                        button.onClick.AddListener(() => GameEvents.OnCharacterSelectionCancelled?.Invoke());
                    }
                }
            }
            else if (confirmationPanel)
            {
                confirmationPanel.gameObject.SetActive(true);
                SetPanelState(confirmationPanel, true);
            }
        }

        /// <summary>
        /// Displays requirements for locked characters.
        /// </summary>
        public void ShowUnlockRequirements(CharacterData character)
        {
            if (!unlockRequirementsText) return;
            string requirements = "Requirements to unlock:\n";

            if (character.requiredPlayerLevel > 1)
            {
                requirements += $"• Level {character.requiredPlayerLevel}\n";
            }
            if (character.requiredAchievements != null && character.requiredAchievements.Length > 0)
            {
                foreach (var ach in character.requiredAchievements)
                {
                    requirements += $"• {ach}\n";
                }
            }

            unlockRequirementsText.text = requirements;
            SetPanelState(unlockPanel, true);
        }

        /// <summary>
        /// Hides the Unlock Panel specifically.
        /// </summary>
        public void HideUnlockPanel()
        {
            SetPanelState(unlockPanel, false);
        }

        /// <summary>
        /// Hides all panels gracefully with a fade out (async).
        /// </summary>
        public async void HideAllPanels()
        {
            SetPanelState(hoverPanel, false);
            SetPanelState(unlockPanel, false);
            await AnimatePanelExit(panelTransitionDuration);  // fade out details
        }

        /// <summary>
        /// Immediately hides all panels (no fade).
        /// </summary>
        public void HideAllPanelsImmediate()
        {
            if (hoverPanel) hoverPanel.alpha = 0f;
            if (unlockPanel) unlockPanel.alpha = 0f;
            if (detailsPanel) detailsPanel.alpha = 0f;

            if (hoverPanel) hoverPanel.gameObject.SetActive(false);
            if (unlockPanel) unlockPanel.gameObject.SetActive(false);
            if (detailsPanel) detailsPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Updates progression data for the given character.
        /// </summary>
        public void UpdateCharacterProgress(string characterId, CharacterProgressionData progression)
        {
            if (string.IsNullOrEmpty(characterId) || progression == null) return;
            progressionCache[characterId] = progression;

            // If currently showing that character, update stats
            if (characterId == currentCharacterId)
            {
                // We need the base data, typically from outside
                // e.g. CharacterSelectionManager or somewhere else
                // For now, we do a debug check
                Debug.Log($"[CharacterUIManager] UpdateCharacterProgress called for {characterId}, but base data not re-fetched here.");
            }
        }

        public async Task AnimatePanelToFocusedPosition(Transform characterTransform, float duration)
        {
            if (!detailsPanel || !characterTransform) return;

            // Ensure it's visible
            detailsPanel.gameObject.SetActive(true);
            SetPanelState(detailsPanel, true);

            Vector3 targetPos = characterTransform.position + detailsPanelOffsetFocused;

            // Tween in world space
            detailsPanel.transform.DOMove(targetPos, duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);

            // Scale to half
            detailsPanel.transform.DOScale(Vector3.one * 0.5f, duration)
                .SetEase(Ease.InOutQuad);

            await detailsPanel.transform
                .DOMove(targetPos, duration)
                .AsyncWaitForCompletion();
        }

        public async Task AnimatePanelReturnToOverhead(Transform characterTransform, float duration)
        {
            // Exactly the same logic as AnimatePanelOverheadPosition, 
            // but some devs prefer a separate name for clarity.
            await AnimatePanelOverheadPosition(characterTransform, duration);
        }

        /// <summary>
        /// Animates the details panel off-screen, then hides it.
        /// </summary>
        public async Task AnimatePanelExit(float duration)
        {
            if (!detailsPanel) return;

            Sequence seq = DOTween.Sequence();
            seq.Append(detailsPanel.DOFade(0f, duration).SetEase(Ease.InOutQuad));
            seq.Join(detailsPanel.transform.DOLocalMoveY(detailsPanel.transform.position.y - 200f, duration)
                .SetEase(Ease.InOutQuad));
            await seq.AsyncWaitForCompletion();

            detailsPanel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Clears all UI and data references.
        /// </summary>
        public void Cleanup()
        {
            ClearUI();
            if (confirmationPanel)
            {
                var buttons = confirmationPanel.GetComponentsInChildren<Button>();
                foreach (var b in buttons) b.onClick.RemoveAllListeners();
                Destroy(confirmationPanel.gameObject);
                confirmationPanel = null;
            }
            progressionCache.Clear();
        }

        #endregion

        #region Private Helpers

        private CharacterProgressionData GetCharacterProgression(string characterId)
        {
            if (string.IsNullOrEmpty(characterId) || profileManager == null) return null;
            var profile = profileManager.ActiveProfile;
            if (profile?.CharacterProgress == null) return null;

            profile.CharacterProgress.TryGetValue(characterId, out var prog);
            return prog;
        }

        /// <summary>
        /// Animates the details panel above the character's head (general/preview),
        /// at full scale (Vector3.one).
        /// </summary>
        public async Task AnimatePanelOverheadPosition(Transform characterTransform, float duration)
        {
            if (!detailsPanel || !characterTransform) return;

            // Make sure it's active and visible
            detailsPanel.gameObject.SetActive(true);
            SetPanelState(detailsPanel, true);

            // Overhead position in *world* coordinates
            Vector3 targetPos = characterTransform.position + detailsPanelOffsetGeneral;

            // Move in world space:
            detailsPanel.transform.DOMove(targetPos, duration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);

            // Scale to normal size:
            detailsPanel.transform.DOScale(Vector3.one, duration)
                .SetEase(Ease.InOutQuad);

            // Wait for the movement to complete
            await detailsPanel.transform
                .DOMove(targetPos, duration)
                .AsyncWaitForCompletion();
        }

        


        private void SetPanelState(CanvasGroup panel, bool visible)
        {
            if (!panel) return;
            panel.gameObject.SetActive(true);

            panel.DOFade(visible ? 1f : 0f, panelFadeDuration)
                .OnComplete(() =>
                {
                    // After fade out, disable object if needed
                    if (!visible) panel.gameObject.SetActive(false);
                });
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }

        private void UpdateCharacterStats(CharacterData baseData, CharacterProgressionData progression)
        {
            ClearStatsContainer();

            // Example stats we show:
            CreateStatRow("Health", baseData.maxHealth, progression);
            CreateStatRow("Defense", baseData.defense, progression);
            CreateStatRow("Power", baseData.power, progression);
            CreateStatRow("Speed", baseData.speed, progression);
            CreateStatRow("Dexterity", baseData.dexterity, progression);
            CreateStatRow("Luck", baseData.luck, progression);
        }

        private void CreateStatRow(string statName, float baseValue, CharacterProgressionData progression)
        {
            if (!statRowPrefab || !statsContainer) return;

            var row = Instantiate(statRowPrefab, statsContainer);
            var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length < 2) return;

            texts[0].text = statName;
            texts[1].text = baseValue.ToString("F1");

            string statKey = $"{currentCharacterId}_{statName}";
            statTextCache[statKey] = texts[1];

            if (progression?.Stats != null && progression.Stats.TryGetValue(statName, out float progressedValue))
            {
                // Animate from baseValue to progressedValue
                DOTween.To(
                    () => baseValue,
                    v => texts[1].text = v.ToString("F1"),
                    progressedValue,
                    statUpdateDuration
                );
            }
        }

        private void ClearStatsContainer()
        {
            if (!statsContainer) return;
            foreach (Transform child in statsContainer)
            {
                Destroy(child.gameObject);
            }
            statTextCache.Clear();
        }

        private void ClearUI()
        {
            HideAllPanelsImmediate();
            ClearStatsContainer();
            currentCharacterId = null;
        }

        #endregion
    }
}

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

        [Header("Animation Settings")]
        [SerializeField] private float panelFadeDuration = 0.3f;
        [SerializeField] private float statUpdateDuration = 0.5f;

        [Header("Positioning")]
        [SerializeField] private Vector3 hoverPanelOffset = new Vector3(0, 2, 0);
        [SerializeField] private Vector3 detailsPanelOffset = new Vector3(2, 1, 0);

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

            // Position hover panel above character
            if (hoverPanel != null)
            {
                hoverPanel.transform.position = characterPosition.position + hoverPanelOffset;
            }

            // Position details panel to the side
            if (detailsPanel != null)
            {
                detailsPanel.transform.position = characterPosition.position + detailsPanelOffset;
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

            // Update hover panel
            if (hoverCharacterNameText != null)
            {
                hoverCharacterNameText.text = character.displayName;
            }

            // Show appropriate prompts
            if (leftClickPrompt != null) leftClickPrompt.SetActive(isUnlocked);
            if (rightClickPrompt != null) rightClickPrompt.SetActive(isUnlocked);

            // Show appropriate panels
            SetPanelState(hoverPanel, true);
            SetPanelState(detailsPanel, false);
            SetPanelState(unlockPanel, !isUnlocked);

            if (!isUnlocked)
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

            // Show details panel
            SetPanelState(hoverPanel, false);
            SetPanelState(detailsPanel, true);
            SetPanelState(unlockPanel, false);
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

        private void ShowUnlockRequirements(CharacterData character)
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
            progressionCache.Clear();
        }

        private void ClearUI()
        {
            HideAllPanels();
            ClearStatsContainer();
            currentCharacterId = null;
        }

        private void HideAllPanels()
        {
            SetPanelState(hoverPanel, false);
            SetPanelState(detailsPanel, false);
            SetPanelState(unlockPanel, false);
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
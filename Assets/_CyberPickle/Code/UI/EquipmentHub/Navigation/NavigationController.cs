using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using System;

namespace CyberPickle.UI.EquipmentHub
{
    public enum HubSection
    {
        Loadout,
        Shop,
        Skills,
        Mining
    }

    public class NavigationController : MonoBehaviour
    {
        [Header("Navigation Buttons")]
        [SerializeField] private Button loadoutButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button skillsButton;
        [SerializeField] private Button miningButton;

        [Header("Visual Settings")]
        [SerializeField] private Color activeButtonColor = new Color(0.2f, 1f, 0.8f, 1f);
        [SerializeField] private Color inactiveButtonColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private float buttonTransitionDuration = 0.3f;
        [SerializeField] private float buttonHoverScale = 1.05f;

        [Header("Button Icons (Optional)")]
        [SerializeField] private Image loadoutIcon;
        [SerializeField] private Image shopIcon;
        [SerializeField] private Image skillsIcon;
        [SerializeField] private Image miningIcon;

        [Header("Button Labels")]
        [SerializeField] private TextMeshProUGUI loadoutLabel;
        [SerializeField] private TextMeshProUGUI shopLabel;
        [SerializeField] private TextMeshProUGUI skillsLabel;
        [SerializeField] private TextMeshProUGUI miningLabel;

        [Header("Effects")]
        [SerializeField] private GameObject selectionIndicator;
        [SerializeField] private float indicatorMoveSpeed = 0.2f;

        private EquipmentHubManager hubManager;
        private Dictionary<HubSection, Button> sectionButtons;
        private Dictionary<HubSection, Image> sectionIcons;
        private Dictionary<HubSection, TextMeshProUGUI> sectionLabels;
        private HubSection currentSection = HubSection.Loadout;
        private bool isInitialized = false;

        public event Action<HubSection> OnSectionChanged;

        #region Initialization

        public void Initialize(EquipmentHubManager hubManager)
        {
            if (isInitialized) return;

            this.hubManager = hubManager;
            SetupButtonMappings();
            SetupButtonListeners();
            SetInitialButtonStates();

            isInitialized = true;
        }

        private void SetupButtonMappings()
        {
            // Map sections to buttons
            sectionButtons = new Dictionary<HubSection, Button>
            {
                { HubSection.Loadout, loadoutButton },
                { HubSection.Shop, shopButton },
                { HubSection.Skills, skillsButton },
                { HubSection.Mining, miningButton }
            };

            // Map sections to icons (if assigned)
            sectionIcons = new Dictionary<HubSection, Image>();
            if (loadoutIcon != null) sectionIcons[HubSection.Loadout] = loadoutIcon;
            if (shopIcon != null) sectionIcons[HubSection.Shop] = shopIcon;
            if (skillsIcon != null) sectionIcons[HubSection.Skills] = skillsIcon;
            if (miningIcon != null) sectionIcons[HubSection.Mining] = miningIcon;

            // Map sections to labels
            sectionLabels = new Dictionary<HubSection, TextMeshProUGUI>();
            if (loadoutLabel != null) sectionLabels[HubSection.Loadout] = loadoutLabel;
            if (shopLabel != null) sectionLabels[HubSection.Shop] = shopLabel;
            if (skillsLabel != null) sectionLabels[HubSection.Skills] = skillsLabel;
            if (miningLabel != null) sectionLabels[HubSection.Mining] = miningLabel;
        }

        private void SetupButtonListeners()
        {
            if (loadoutButton != null)
                loadoutButton.onClick.AddListener(() => OnLoadoutClicked());

            if (shopButton != null)
                shopButton.onClick.AddListener(() => OnShopClicked());

            if (skillsButton != null)
                skillsButton.onClick.AddListener(() => OnSkillsClicked());

            if (miningButton != null)
                miningButton.onClick.AddListener(() => OnMiningClicked());

            // Setup hover effects
            foreach (var kvp in sectionButtons)
            {
                if (kvp.Value != null)
                {
                    AddHoverEffects(kvp.Value);
                }
            }
        }

        private void SetInitialButtonStates()
        {
            // Set all buttons to inactive state initially
            foreach (var section in sectionButtons.Keys)
            {
                UpdateButtonVisualState(section, false, true);
            }

            // Set the default section as active
            SetActiveButton(HubSection.Loadout);
        }

        #endregion

        #region Button Click Handlers

        public void OnLoadoutClicked()
        {
            if (currentSection == HubSection.Loadout) return;
            NavigateToSection(HubSection.Loadout);
        }

        public void OnShopClicked()
        {
            if (currentSection == HubSection.Shop) return;
            NavigateToSection(HubSection.Shop);
        }

        public void OnSkillsClicked()
        {
            if (currentSection == HubSection.Skills) return;
            NavigateToSection(HubSection.Skills);
        }

        public void OnMiningClicked()
        {
            if (currentSection == HubSection.Mining) return;
            NavigateToSection(HubSection.Mining);
        }

        #endregion

        #region Navigation Logic

        private void NavigateToSection(HubSection newSection)
        {
            if (hubManager == null)
            {
                Debug.LogError("[NavigationController] HubManager is not set!");
                return;
            }

            // Animate button transition
            AnimateButtonTransition(sectionButtons[currentSection], sectionButtons[newSection]);

            // Update visual states
            UpdateButtonVisualState(currentSection, false);
            UpdateButtonVisualState(newSection, true);

            // Move selection indicator if available
            if (selectionIndicator != null)
            {
                MoveSelectionIndicator(sectionButtons[newSection]);
            }

            // Update current section
            var previousSection = currentSection;
            currentSection = newSection;

            // Notify hub manager to switch sections
            hubManager.ShowSection(newSection);

            // Fire event
            OnSectionChanged?.Invoke(newSection);
        }

        public void SetActiveButton(HubSection section)
        {
            if (!sectionButtons.ContainsKey(section)) return;

            // Update all button states
            foreach (var kvp in sectionButtons)
            {
                UpdateButtonVisualState(kvp.Key, kvp.Key == section);
            }

            // Update selection indicator
            if (selectionIndicator != null && sectionButtons[section] != null)
            {
                selectionIndicator.transform.position = sectionButtons[section].transform.position;
            }

            currentSection = section;
        }

        #endregion

        #region Visual Effects

        private void UpdateButtonVisualState(HubSection section, bool isActive, bool instant = false)
        {
            if (!sectionButtons.TryGetValue(section, out Button button) || button == null) return;

            float duration = instant ? 0f : buttonTransitionDuration;
            Color targetColor = isActive ? activeButtonColor : inactiveButtonColor;

            // Update button background color
            var buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.DOColor(targetColor, duration);
            }

            // Update icon color if available
            if (sectionIcons.TryGetValue(section, out Image icon) && icon != null)
            {
                icon.DOColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f), duration);
            }

            // Update label color if available
            if (sectionLabels.TryGetValue(section, out TextMeshProUGUI label) && label != null)
            {
                label.DOColor(isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f), duration);
            }

            // Scale effect for active button
            if (isActive)
            {
                button.transform.DOScale(1.05f, duration * 0.5f)
                    .OnComplete(() => button.transform.DOScale(1f, duration * 0.5f));
            }
        }

        public void AnimateButtonTransition(Button fromButton, Button toButton)
        {
            if (fromButton == null || toButton == null) return;

            // Add pulse effect to the new active button
            toButton.transform.DOPunchScale(Vector3.one * 0.1f, buttonTransitionDuration, 1, 0.5f);

            // Optional: Add a glow or highlight effect
            var toButtonImage = toButton.GetComponent<Image>();
            if (toButtonImage != null)
            {
                // Flash effect
                toButtonImage.DOColor(Color.white, buttonTransitionDuration * 0.3f)
                    .OnComplete(() => toButtonImage.DOColor(activeButtonColor, buttonTransitionDuration * 0.7f));
            }
        }

        private void MoveSelectionIndicator(Button targetButton)
        {
            if (selectionIndicator == null || targetButton == null) return;

            selectionIndicator.transform.DOMove(targetButton.transform.position, indicatorMoveSpeed)
                .SetEase(Ease.OutQuad);
        }

        private void AddHoverEffects(Button button)
        {
            if (button == null) return;

            var eventTriggers = button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTriggers == null)
            {
                eventTriggers = button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            // Add hover enter event
            var hoverEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            hoverEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            hoverEnter.callback.AddListener((data) => OnButtonHoverEnter(button));
            eventTriggers.triggers.Add(hoverEnter);

            // Add hover exit event
            var hoverExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            hoverExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            hoverExit.callback.AddListener((data) => OnButtonHoverExit(button));
            eventTriggers.triggers.Add(hoverExit);
        }

        private void OnButtonHoverEnter(Button button)
        {
            // Don't scale if it's the active button
            HubSection? hoveredSection = GetSectionForButton(button);
            if (hoveredSection.HasValue && hoveredSection.Value != currentSection)
            {
                button.transform.DOScale(buttonHoverScale, 0.2f).SetEase(Ease.OutQuad);
            }

            // Play hover sound if available
            //var audioController = GetComponentInParent<AudioFeedbackController>();
            //audioController?.PlayHoverSound();
        }

        private void OnButtonHoverExit(Button button)
        {
            button.transform.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
        }

        private HubSection? GetSectionForButton(Button button)
        {
            foreach (var kvp in sectionButtons)
            {
                if (kvp.Value == button)
                    return kvp.Key;
            }
            return null;
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // Remove all listeners
            if (loadoutButton != null) loadoutButton.onClick.RemoveAllListeners();
            if (shopButton != null) shopButton.onClick.RemoveAllListeners();
            if (skillsButton != null) skillsButton.onClick.RemoveAllListeners();
            if (miningButton != null) miningButton.onClick.RemoveAllListeners();

            // Kill any active tweens
            DOTween.Kill(this);
        }

        #endregion
    }
}
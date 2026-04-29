using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.UI.EquipmentHub
{
    public class InventoryTabController : MonoBehaviour
    {
        [System.Serializable]
        public class TabButton
        {
            public Button button;
            public Image backgroundImage;
            public Image iconImage;
            public TextMeshProUGUI labelText;
            public TextMeshProUGUI countText;
            public GameObject countBadge;
            public EquipmentSlotType? slotType; // null for "All" tab
        }

        [Header("Tab References")]
        [SerializeField] private TabButton allTab;
        [SerializeField] private TabButton weaponsTab;
        [SerializeField] private TabButton powerUpsTab;
        [SerializeField] private TabButton armorTab;
        [SerializeField] private TabButton amuletsTab;

        [Header("Visual Settings")]
        [SerializeField] private Color activeTabColor = new Color(0.2f, 1f, 0.8f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color hoverTabColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        [SerializeField] private float tabTransitionDuration = 0.2f;

        [Header("Tab Indicator")]
        [SerializeField] private GameObject tabIndicator;
        [SerializeField] private float indicatorMoveSpeed = 0.3f;

        private InventoryUIController inventoryController;
        private Dictionary<TabButton, EquipmentSlotType?> tabMapping;
        private TabButton currentActiveTab;
        private bool isInitialized = false;

        #region Initialization

        public void Initialize(InventoryUIController controller)
        {
            if (isInitialized) return;

            inventoryController = controller;
            SetupTabs();
            SetupTabListeners();

            // Set "All" as default active tab
            OnTabSelected(allTab);

            isInitialized = true;
        }

        private void SetupTabs()
        {
            // Configure tab types
            allTab.slotType = null; // null means show all
            weaponsTab.slotType = EquipmentSlotType.HandWeapon;
            powerUpsTab.slotType = EquipmentSlotType.PowerUp;
            armorTab.slotType = EquipmentSlotType.Armor;
            amuletsTab.slotType = EquipmentSlotType.Amulet;

            // Set tab labels
            SetTabLabel(allTab, "All");
            SetTabLabel(weaponsTab, "Weapons");
            SetTabLabel(powerUpsTab, "Power-Ups");
            SetTabLabel(armorTab, "Armor");
            SetTabLabel(amuletsTab, "Amulets");

            // Create tab mapping
            tabMapping = new Dictionary<TabButton, EquipmentSlotType?>
            {
                { allTab, null },
                { weaponsTab, EquipmentSlotType.HandWeapon },
                { powerUpsTab, EquipmentSlotType.PowerUp },
                { armorTab, EquipmentSlotType.Armor },
                { amuletsTab, EquipmentSlotType.Amulet }
            };

            // Hide count badges initially
            foreach (var tab in tabMapping.Keys)
            {
                if (tab.countBadge != null)
                {
                    tab.countBadge.SetActive(false);
                }
            }
        }

        private void SetupTabListeners()
        {
            foreach (var tab in tabMapping.Keys)
            {
                if (tab.button != null)
                {
                    var capturedTab = tab; // Capture for closure
                    tab.button.onClick.AddListener(() => OnTabSelected(capturedTab));

                    // Add hover effects
                    AddHoverEffects(tab);
                }
            }
        }

        private void SetTabLabel(TabButton tab, string label)
        {
            if (tab.labelText != null)
            {
                tab.labelText.text = label;
            }
        }

        #endregion

        #region Tab Selection

        private void OnTabSelected(TabButton selectedTab)
        {
            if (selectedTab == currentActiveTab) return;

            // Update visual states
            UpdateTabVisuals(selectedTab);

            // Move indicator
            if (tabIndicator != null && selectedTab.button != null)
            {
                MoveIndicatorToTab(selectedTab);
            }

            // Update inventory filter
            if (inventoryController != null && tabMapping.TryGetValue(selectedTab, out var slotType))
            {
                inventoryController.SetActiveTab(slotType);
            }

            // Play tab switch sound
            //var audioController = GetComponentInParent<AudioFeedbackController>();
            //audioController?.PlayHoverSound();

            currentActiveTab = selectedTab;
        }

        private void UpdateTabVisuals(TabButton activeTab = null)
        {
            foreach (var tab in tabMapping.Keys)
            {
                bool isActive = (tab == activeTab) || (activeTab == null && tab == currentActiveTab);
                UpdateTabVisualState(tab, isActive);
            }
        }

        private void UpdateTabVisualState(TabButton tab, bool isActive)
        {
            if (tab.backgroundImage != null)
            {
                Color targetColor = isActive ? activeTabColor : inactiveTabColor;
                tab.backgroundImage.DOColor(targetColor, tabTransitionDuration);
            }

            if (tab.iconImage != null)
            {
                Color iconColor = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                tab.iconImage.DOColor(iconColor, tabTransitionDuration);
            }

            if (tab.labelText != null)
            {
                Color textColor = isActive ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
                tab.labelText.DOColor(textColor, tabTransitionDuration);
            }

            // Scale effect for active tab
            if (isActive && tab.button != null)
            {
                tab.button.transform.DOScale(1.05f, tabTransitionDuration * 0.5f)
                    .OnComplete(() => tab.button.transform.DOScale(1f, tabTransitionDuration * 0.5f));
            }
        }

        private void MoveIndicatorToTab(TabButton tab)
        {
            if (tabIndicator == null || tab.button == null) return;

            tabIndicator.transform.DOMove(tab.button.transform.position, indicatorMoveSpeed)
                .SetEase(Ease.OutQuad);
        }

        #endregion

        #region Item Counting

        public void UpdateItemCounts(Dictionary<EquipmentSlotType?, int> counts)
        {
            foreach (var kvp in tabMapping)
            {
                var tab = kvp.Key;
                var slotType = kvp.Value;

                if (counts.TryGetValue(slotType, out int count))
                {
                    ShowItemCount(tab, count);
                }
            }
        }

        private void ShowItemCount(TabButton tab, int count)
        {
            if (tab.countText != null && tab.countBadge != null)
            {
                tab.countText.text = count.ToString();

                // Only show badge if count > 0
                bool showBadge = count > 0;
                tab.countBadge.SetActive(showBadge);

                if (showBadge)
                {
                    // Animate badge appearance
                    tab.countBadge.transform.DOScale(1.2f, 0.2f)
                        .OnComplete(() => tab.countBadge.transform.DOScale(1f, 0.1f));
                }
            }
        }

        #endregion

        #region Hover Effects

        private void AddHoverEffects(TabButton tab)
        {
            if (tab.button == null) return;

            var eventTriggers = tab.button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (eventTriggers == null)
            {
                eventTriggers = tab.button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            }

            // Add hover enter event
            var hoverEnter = new UnityEngine.EventSystems.EventTrigger.Entry();
            hoverEnter.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
            hoverEnter.callback.AddListener((data) => OnTabHoverEnter(tab));
            eventTriggers.triggers.Add(hoverEnter);

            // Add hover exit event
            var hoverExit = new UnityEngine.EventSystems.EventTrigger.Entry();
            hoverExit.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            hoverExit.callback.AddListener((data) => OnTabHoverExit(tab));
            eventTriggers.triggers.Add(hoverExit);
        }

        private void OnTabHoverEnter(TabButton tab)
        {
            if (tab == currentActiveTab) return;

            if (tab.backgroundImage != null)
            {
                tab.backgroundImage.DOColor(hoverTabColor, 0.2f);
            }

            if (tab.button != null)
            {
                tab.button.transform.DOScale(1.05f, 0.2f);
            }
        }

        private void OnTabHoverExit(TabButton tab)
        {
            if (tab == currentActiveTab) return;

            if (tab.backgroundImage != null)
            {
                tab.backgroundImage.DOColor(inactiveTabColor, 0.2f);
            }

            if (tab.button != null)
            {
                tab.button.transform.DOScale(1f, 0.2f);
            }
        }

        #endregion

        #region Public Methods

        public void SelectTab(EquipmentSlotType? slotType)
        {
            foreach (var kvp in tabMapping)
            {
                if (kvp.Value == slotType)
                {
                    OnTabSelected(kvp.Key);
                    break;
                }
            }
        }

        public void RefreshTabCounts()
        {
            // This would be called by InventoryUIController to update counts
            // Implementation depends on how you want to count items
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            // Remove all listeners
            foreach (var tab in tabMapping.Keys)
            {
                if (tab.button != null)
                {
                    tab.button.onClick.RemoveAllListeners();
                }
            }

            // Kill any active tweens
            DOTween.Kill(this);
        }

        #endregion
    }
}
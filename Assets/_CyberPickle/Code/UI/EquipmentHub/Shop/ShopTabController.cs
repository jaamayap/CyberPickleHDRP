using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using DG.Tweening;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.EquipmentHub.Shop
{
    public class ShopTabController : MonoBehaviour
    {
        [System.Serializable]
        public class ShopTab
        {
            public string tabName;
            public Button button;
            public EquipmentSlotType? slotType;
            public Image icon;
            public TextMeshProUGUI label;
            public GameObject selectedIndicator;
            public Image backgroundImage;
        }

        [Header("Tab Configuration")]
        [SerializeField] private List<ShopTab> tabs = new List<ShopTab>();

        [Header("Visual Settings")]
        [SerializeField] private Color activeTabColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color inactiveTabColor = new Color(0.7f, 0.7f, 0.7f, 0.8f);
        [SerializeField] private Color hoverTabColor = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        [SerializeField] private float tabTransitionDuration = 0.2f;
        [SerializeField] private float selectedIndicatorWidth = 4f;

        [Header("Icons")]
        [SerializeField] private Sprite allItemsIcon;
        [SerializeField] private Sprite weaponsIcon;
        [SerializeField] private Sprite powerUpsIcon;
        [SerializeField] private Sprite armorIcon;
        [SerializeField] private Sprite amuletsIcon;

        private ShopUIController shopController;
        private ShopTab activeTab;
        private Dictionary<Button, ShopTab> buttonToTabMap = new Dictionary<Button, ShopTab>();

        #region Initialization

        public void Initialize(ShopUIController controller)
        {
            shopController = controller;
            SetupTabs();

            // Select "All" tab by default
            if (tabs.Count > 0)
            {
                OnTabSelected(tabs[0]);
            }
        }

        private void SetupTabs()
        {
            buttonToTabMap.Clear();

            // Setup default tabs if not configured
            if (tabs.Count == 0)
            {
                CreateDefaultTabs();
            }

            // Configure each tab
            foreach (var tab in tabs)
            {
                if (tab.button != null)
                {
                    // Store reference
                    buttonToTabMap[tab.button] = tab;

                    // Add click listener
                    tab.button.onClick.RemoveAllListeners();
                    tab.button.onClick.AddListener(() => OnTabSelected(tab));

                    // Setup hover events
                    var eventTrigger = tab.button.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>();
                    if (eventTrigger == null)
                    {
                        eventTrigger = tab.button.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                    }

                    // Add hover enter
                    var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                    {
                        eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
                    };
                    enterEntry.callback.AddListener((data) => OnTabHoverEnter(tab));
                    eventTrigger.triggers.Add(enterEntry);

                    // Add hover exit
                    var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                    {
                        eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
                    };
                    exitEntry.callback.AddListener((data) => OnTabHoverExit(tab));
                    eventTrigger.triggers.Add(exitEntry);

                    // Set initial state
                    SetTabVisualState(tab, false);
                }
            }
        }

        private void CreateDefaultTabs()
        {
            // This would be called if tabs aren't pre-configured in the inspector
            tabs = new List<ShopTab>
            {
                new ShopTab { tabName = "All", slotType = null },
                new ShopTab { tabName = "Weapons", slotType = EquipmentSlotType.HandWeapon },
                new ShopTab { tabName = "Power-Ups", slotType = EquipmentSlotType.PowerUp },
                new ShopTab { tabName = "Armor", slotType = EquipmentSlotType.Armor },
                new ShopTab { tabName = "Amulets", slotType = EquipmentSlotType.Amulet }
            };
        }

        #endregion

        #region Tab Selection

        public void OnTabSelected(ShopTab tab)
        {
            if (tab == null || tab == activeTab) return;

            // Update active tab
            var previousTab = activeTab;
            activeTab = tab;

            // Update visuals
            UpdateTabVisuals(previousTab, tab);

            // Notify shop controller
            if (shopController != null)
            {
                shopController.FilterByType(tab.slotType);
            }

            // Play selection sound
            PlayTabSelectionSound();
        }

        public void SelectTabByType(EquipmentSlotType? slotType)
        {
            foreach (var tab in tabs)
            {
                if (tab.slotType == slotType)
                {
                    OnTabSelected(tab);
                    break;
                }
            }
        }

        #endregion

        #region Visual Updates

        private void UpdateTabVisuals(ShopTab previousTab, ShopTab newTab)
        {
            // Deactivate previous tab
            if (previousTab != null)
            {
                SetTabVisualState(previousTab, false);
            }

            // Activate new tab
            if (newTab != null)
            {
                SetTabVisualState(newTab, true);
            }
        }

        private void SetTabVisualState(ShopTab tab, bool isActive)
        {
            if (tab == null) return;

            // Background color
            if (tab.backgroundImage != null)
            {
                tab.backgroundImage.DOColor(isActive ? activeTabColor : inactiveTabColor, tabTransitionDuration);
            }

            // Text color
            if (tab.label != null)
            {
                tab.label.DOColor(isActive ? activeTabColor : inactiveTabColor, tabTransitionDuration);
            }

            // Icon color
            if (tab.icon != null)
            {
                tab.icon.DOColor(isActive ? activeTabColor : inactiveTabColor, tabTransitionDuration);
            }

            // Selected indicator
            if (tab.selectedIndicator != null)
            {
                if (isActive)
                {
                    tab.selectedIndicator.SetActive(true);
                    tab.selectedIndicator.transform.localScale = new Vector3(1f, 0f, 1f);
                    tab.selectedIndicator.transform.DOScaleY(1f, tabTransitionDuration)
                        .SetEase(Ease.OutCubic);
                }
                else
                {
                    tab.selectedIndicator.transform.DOScaleY(0f, tabTransitionDuration)
                        .SetEase(Ease.OutCubic)
                        .OnComplete(() => tab.selectedIndicator.SetActive(false));
                }
            }

            // Button scale
            if (tab.button != null)
            {
                tab.button.transform.DOScale(isActive ? 1.05f : 1f, tabTransitionDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        #endregion

        #region Hover Effects

        private void OnTabHoverEnter(ShopTab tab)
        {
            if (tab == null || tab == activeTab) return;

            // Hover color
            if (tab.backgroundImage != null)
            {
                tab.backgroundImage.DOColor(hoverTabColor, tabTransitionDuration * 0.5f);
            }

            // Slight scale
            if (tab.button != null)
            {
                tab.button.transform.DOScale(1.02f, tabTransitionDuration * 0.5f)
                    .SetEase(Ease.OutCubic);
            }
        }

        private void OnTabHoverExit(ShopTab tab)
        {
            if (tab == null || tab == activeTab) return;

            // Reset to inactive state
            SetTabVisualState(tab, false);
        }

        #endregion

        #region Helper Methods

        private Sprite GetIconForSlotType(EquipmentSlotType? slotType)
        {
            if (!slotType.HasValue)
                return allItemsIcon;

            switch (slotType.Value)
            {
                case EquipmentSlotType.HandWeapon:
                case EquipmentSlotType.BodyWeapon:
                    return weaponsIcon;
                case EquipmentSlotType.PowerUp:
                    return powerUpsIcon;
                case EquipmentSlotType.Armor:
                    return armorIcon;
                case EquipmentSlotType.Amulet:
                    return amuletsIcon;
                default:
                    return allItemsIcon;
            }
        }

        private void PlayTabSelectionSound()
        {
            // Implement audio feedback
            // AudioManager.Instance?.PlaySound("TabSelect");
        }

        #endregion

        #region Public Methods

        public ShopTab GetActiveTab()
        {
            return activeTab;
        }

        public void RefreshTabs()
        {
            // Refresh tab states if needed
            foreach (var tab in tabs)
            {
                SetTabVisualState(tab, tab == activeTab);
            }
        }

        #endregion

        private void OnDestroy()
        {
            // Clean up
            foreach (var tab in tabs)
            {
                if (tab.button != null)
                {
                    tab.button.onClick.RemoveAllListeners();
                }
            }

            buttonToTabMap.Clear();
            DOTween.Kill(this);
        }
    }
}
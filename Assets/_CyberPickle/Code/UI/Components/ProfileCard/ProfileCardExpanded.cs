// File: Assets/Code/UI/Components/ProfileCard/ProfileCardExpanded.cs
//
// Purpose: Handles the expanded view of the profile card showing detailed information
// and providing minimize and profile switch options.
//
// Created: 2024-02-11

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.UI.Components.ProfileCard
{
    public class ProfileCardExpanded : MonoBehaviour
    {
        [Header("Card Elements")]
        [SerializeField] private Image backgroundPanel;
        [SerializeField] private Image neonBorder;

        [Header("Header Section")]
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private TextMeshProUGUI lastLoginText;
        [SerializeField] private Image avatarImage;

        [Header("Stats Grid")]
        [SerializeField] private GridLayoutGroup statsGrid;
        [SerializeField] private TextMeshProUGUI levelStatValue;
        [SerializeField] private TextMeshProUGUI timeStatValue;
        [SerializeField] private TextMeshProUGUI scoreStatValue;

        [Header("Buttons")]
        [SerializeField] private Button minimizeButton;
        [SerializeField] private TextMeshProUGUI minimizeButtonText;
        [SerializeField] private Button switchProfileButton;
        [SerializeField] private TextMeshProUGUI switchProfileButtonText;

        private ProfileData currentProfile;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            ValidateReferences();
            SetupButtons();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void ValidateReferences()
        {
            if (profileNameText == null)
                Debug.LogError("[ProfileCardExpanded] Profile name text is missing!");
            if (lastLoginText == null)
                Debug.LogError("[ProfileCardExpanded] Last login text is missing!");
            if (minimizeButton == null)
                Debug.LogError("[ProfileCardExpanded] Minimize button is missing!");
            if (switchProfileButton == null)
                Debug.LogError("[ProfileCardExpanded] Switch Profile button is missing!");
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void SetupButtons()
        {
            if (minimizeButton != null)
            {
                minimizeButton.onClick.AddListener(() => GameEvents.OnProfileCardClicked.Invoke());
                if (minimizeButtonText != null)
                    minimizeButtonText.text = "Minimize";
            }

            if (switchProfileButton != null)
            {
                switchProfileButton.onClick.AddListener(HandleSwitchProfileClick);
                if (switchProfileButtonText != null)
                    switchProfileButtonText.text = "Change Profile";
            }
        }

        private void SubscribeToEvents()
        {
            GameEvents.OnProfileCardInteractionEnabled.AddListener(SetInteractable);
        }

        private void UnsubscribeFromEvents()
        {
            GameEvents.OnProfileCardInteractionEnabled.RemoveListener(SetInteractable);
        }

        public void UpdateDisplay(ProfileData profileData)
        {
            if (profileData == null) return;

            currentProfile = profileData;

            // Update header information
            if (profileNameText != null)
                profileNameText.text = profileData.DisplayName;

            if (lastLoginText != null)
                lastLoginText.text = FormatLastLoginTime(profileData.LastLoginTime);

            // Update stats
            if (levelStatValue != null)
                levelStatValue.text = profileData.Level.ToString();

            if (timeStatValue != null)
                timeStatValue.text = FormatPlayTime(profileData.TotalPlayTime);

            if (scoreStatValue != null)
                scoreStatValue.text = profileData.HighestScore.ToString();

            // Avatar implementation can be added later
            if (avatarImage != null)
                avatarImage.gameObject.SetActive(false);
        }

        private void HandleSwitchProfileClick()
        {
            // Trigger profile selection screen
            GameEvents.OnProfileLoadRequested.Invoke();

        }

        public void SetInteractable(bool interactable)
        {
            if (minimizeButton != null)
                minimizeButton.interactable = interactable;

            if (switchProfileButton != null)
                switchProfileButton.interactable = interactable;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }
        }

        private string FormatPlayTime(float playTimeInSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(playTimeInSeconds);
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            else if (time.Minutes >= 1)
                return $"{time.Minutes}m {time.Seconds}s";
            else
                return $"{time.Seconds}s";
        }

        private string FormatLastLoginTime(DateTime lastLoginTime)
        {
            TimeSpan timeSince = DateTime.Now - lastLoginTime;

            if (timeSince.TotalDays >= 1)
                return $"{(int)timeSince.TotalDays} days ago";
            else if (timeSince.TotalHours >= 1)
                return $"{(int)timeSince.TotalHours} hours ago";
            else if (timeSince.TotalMinutes >= 1)
                return $"{(int)timeSince.TotalMinutes} minutes ago";
            else
                return "Just now";
        }

        private void OnDestroy()
        {
            if (minimizeButton != null)
                minimizeButton.onClick.RemoveAllListeners();

            if (switchProfileButton != null)
                switchProfileButton.onClick.RemoveAllListeners();

            UnsubscribeFromEvents();
        }
    }
}

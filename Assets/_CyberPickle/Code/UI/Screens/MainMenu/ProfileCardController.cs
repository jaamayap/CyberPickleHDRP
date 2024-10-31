// File: Assets/Code/UI/Screens/MainMenu/ProfileCardController.cs
//
// Purpose: Controls the profile card UI, displays profile information, and handles user interactions such as selection and deletion.
// This controller focuses solely on UI handling and delegates profile actions to the ProfileSelectionController.
//
// Created: 2024-01-15
// Updated: 2024-01-15
//
// Dependencies:
// - TMPro for TextMeshProUGUI components
// - UnityEngine.UI for UI elements like Button
// - CyberPickle.Core.Services.Authentication.Data for ProfileData
// - System for event handling

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.UI.Screens.MainMenu
{
    public class ProfileCardController : MonoBehaviour
    {
        // Header Section
        [Header("Header Section")]
        [SerializeField] private TextMeshProUGUI profileNameText;
        [SerializeField] private TextMeshProUGUI lastLoginText;

        // Stats Grid
        [Header("Stats Grid")]
        [SerializeField] private TextMeshProUGUI levelValueText;
        [SerializeField] private TextMeshProUGUI timeValueText;
        [SerializeField] private TextMeshProUGUI scoreValueText;

        // Buttons
        [Header("Buttons")]
        [SerializeField] private Button selectButton;
        [SerializeField] private Button deleteButton;

        // Events
        public event Action<ProfileData> OnProfileSelected;
        public event Action<ProfileData> OnProfileDeleted;

        // Private variables
        private ProfileData profileData;

        // Public property to expose profileData
        public ProfileData ProfileData => profileData;

        /// <summary>
        /// Initializes the profile card with profile data.
        /// </summary>
        /// <param name="profile">The profile data to display.</param>
        public void Initialize(ProfileData profile)
        {
            profileData = profile;

            // Set header information
            profileNameText.text = profile.DisplayName;
            lastLoginText.text = FormatLastLoginTime(profile.LastLoginTime);

            // Set stats
            levelValueText.text = profile.Level.ToString();
            timeValueText.text = FormatPlayTime(profile.TotalPlayTime);
            scoreValueText.text = profile.HighestScore.ToString();

            // Set up button listeners
            selectButton.onClick.AddListener(OnSelectButtonClicked);
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }

        private void OnSelectButtonClicked()
        {
            OnProfileSelected?.Invoke(profileData);
        }

        private void OnDeleteButtonClicked()
        {
            OnProfileDeleted?.Invoke(profileData);
        }

        private string FormatLastLoginTime(DateTime lastLoginTime)
        {
            TimeSpan timeSinceLastLogin = DateTime.Now - lastLoginTime;
            if (timeSinceLastLogin.TotalDays >= 1)
                return $"{(int)timeSinceLastLogin.TotalDays} days ago";
            else if (timeSinceLastLogin.TotalHours >= 1)
                return $"{(int)timeSinceLastLogin.TotalHours} hours ago";
            else if (timeSinceLastLogin.TotalMinutes >= 1)
                return $"{(int)timeSinceLastLogin.TotalMinutes} minutes ago";
            else
                return "Just now";
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

        private void OnDestroy()
        {
            selectButton.onClick.RemoveListener(OnSelectButtonClicked);
            deleteButton.onClick.RemoveListener(OnDeleteButtonClicked);
        }
    }
}


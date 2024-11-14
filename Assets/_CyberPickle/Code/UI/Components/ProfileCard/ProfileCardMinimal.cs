// File: Assets/Code/UI/Components/ProfileCard/ProfileCardMinimal.cs
//
// Purpose: Handles the minimized corner view of the profile card showing essential information
// and handling basic interactions.
//
// Created: 2024-02-11

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;

namespace CyberPickle.UI.Components.ProfileCard
{
    public class ProfileCardMinimal : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private Button cardButton;

        [Header("Animation")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float hoverScaleMultiplier = 1.05f;

        private Vector3 originalScale;
        private ProfileData currentProfile;

        private void Awake()
        {
            ValidateReferences();
            SetupInteractions();
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
            if (playerNameText == null)
                Debug.LogError("[ProfileCardMinimal] Player name text is missing!");
            if (levelText == null)
                Debug.LogError("[ProfileCardMinimal] Level text is missing!");
            if (cardButton == null)
                cardButton = GetComponent<Button>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            originalScale = transform.localScale;
        }

        private void SetupInteractions()
        {
            if (cardButton != null)
            {
                cardButton.onClick.AddListener(() => GameEvents.OnProfileCardClicked.Invoke());
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

            if (playerNameText != null)
                playerNameText.text = profileData.DisplayName;

            if (levelText != null)
                levelText.text = $"Level {profileData.Level}";
        }

        public void SetInteractable(bool interactable)
        {
            if (cardButton != null)
                cardButton.interactable = interactable;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }
        }

        private void OnDestroy()
        {
            if (cardButton != null)
                cardButton.onClick.RemoveAllListeners();

            UnsubscribeFromEvents();
        }

        #region Mouse Interaction Handlers

        public void OnPointerEnter()
        {
            transform.localScale = originalScale * hoverScaleMultiplier;
        }

        public void OnPointerExit()
        {
            transform.localScale = originalScale;
        }

        #endregion
    }
}
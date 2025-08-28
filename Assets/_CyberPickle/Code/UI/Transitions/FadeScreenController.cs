// File: Assets/Code/UI/Transitions/FadeScreenController.cs
//
// Purpose: Controls screen fading transitions between scenes or major state changes.
// Provides fade-to-black and fade-from-black functionality with configurable duration and easing.
// Supports displaying a loading indicator during transitions.
//
// Created: 2025-02-25
// Updated: 2025-02-25

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using TMPro;

namespace CyberPickle.UI.Transitions
{
    /// <summary>
    /// Controls screen fading transitions between scenes or major state changes
    /// </summary>
    public class FadeScreenController : MonoBehaviour
    {
        [Header("Fade Settings")]
        [SerializeField] private Image fadeImage;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private Ease fadeEase = Ease.InOutQuad;

        [Header("Loading Indicator")]
        [SerializeField] private GameObject loadingIndicator;
        [SerializeField] private TextMeshProUGUI loadingText;

        private Sequence currentFadeSequence;

        /// <summary>
        /// Duration of the fade transition
        /// </summary>
        public float FadeDuration => fadeDuration;

        private void Awake()
        {
            ValidateReferences();

            // Make sure we start with a transparent fade image
            if (fadeImage != null)
            {
                fadeImage.color = new Color(fadeImage.color.r, fadeImage.color.g, fadeImage.color.b, 0f);
                fadeImage.gameObject.SetActive(false);
            }

            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);
        }

        private void ValidateReferences()
        {
            // Ensure fade image exists
            if (fadeImage == null)
            {
                Debug.LogError("[FadeScreenController] Fade image not assigned! Creating a new one...");

                // Create a canvas if needed
                Canvas canvas = GetComponent<Canvas>();
                if (canvas == null)
                {
                    canvas = gameObject.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.sortingOrder = 999; // Ensure it's on top

                    // Add a canvas scaler
                    CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1920, 1080);

                    // Add a graphic raycaster
                    gameObject.AddComponent<GraphicRaycaster>();
                }

                // Create fade image
                GameObject imageObj = new GameObject("FadeImage");
                imageObj.transform.SetParent(transform, false);

                RectTransform rectTransform = imageObj.AddComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.sizeDelta = Vector2.zero;
                rectTransform.anchoredPosition = Vector2.zero;

                fadeImage = imageObj.AddComponent<Image>();
                fadeImage.color = Color.black;
                fadeImage.raycastTarget = false;

                Debug.Log("[FadeScreenController] Created fade image");
            }
        }

        /// <summary>
        /// Fades the screen to black
        /// </summary>
        public void FadeToBlack()
        {
            if (fadeImage == null) return;

            fadeImage.gameObject.SetActive(true);

            // Kill any existing fade
            if (currentFadeSequence != null && currentFadeSequence.IsActive())
                currentFadeSequence.Kill();

            // Create fade sequence
            currentFadeSequence = DOTween.Sequence();
            currentFadeSequence.Append(fadeImage.DOFade(1f, fadeDuration).SetEase(fadeEase));

            if (loadingIndicator != null)
            {
                currentFadeSequence.AppendCallback(() => {
                    loadingIndicator.SetActive(true);
                });
            }

            Debug.Log("[FadeScreenController] Fading to black");
        }

        /// <summary>
        /// Fades the screen from black back to transparent
        /// </summary>
        public void FadeFromBlack()
        {
            if (fadeImage == null) return;

            // Kill any existing fade
            if (currentFadeSequence != null && currentFadeSequence.IsActive())
                currentFadeSequence.Kill();

            if (loadingIndicator != null)
                loadingIndicator.SetActive(false);

            // Create fade sequence
            currentFadeSequence = DOTween.Sequence();
            currentFadeSequence.Append(fadeImage.DOFade(0f, fadeDuration).SetEase(fadeEase));
            currentFadeSequence.AppendCallback(() => {
                fadeImage.gameObject.SetActive(false);
            });

            Debug.Log("[FadeScreenController] Fading from black");
        }

        /// <summary>
        /// Updates the loading text if it exists
        /// </summary>
        public void SetLoadingText(string text)
        {
            if (loadingText != null)
                loadingText.text = text;
        }

        /// <summary>
        /// Performs a full fade cycle (transparent to black and back)
        /// </summary>
        public IEnumerator FadeCycle(float holdDuration = 0.2f)
        {
            FadeToBlack();
            yield return new WaitForSeconds(fadeDuration + holdDuration);
            FadeFromBlack();
        }

        /// <summary>
        /// Fades to black, executes an action, then fades back
        /// </summary>
        public IEnumerator FadeAction(System.Action action, float holdDuration = 0.2f)
        {
            FadeToBlack();
            yield return new WaitForSeconds(fadeDuration);

            if (action != null)
                action();

            yield return new WaitForSeconds(holdDuration);
            FadeFromBlack();
        }

        private void OnDestroy()
        {
            if (currentFadeSequence != null && currentFadeSequence.IsActive())
                currentFadeSequence.Kill();
        }
    }
}

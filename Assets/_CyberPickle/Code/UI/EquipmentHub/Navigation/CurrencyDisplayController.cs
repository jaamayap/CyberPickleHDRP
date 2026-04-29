using CyberPickle.Core.Events;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Currency;
using DG.Tweening;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CyberPickle.UI.EquipmentHub
{
    /// <summary>
    /// Controls the currency display UI in the Equipment Hub scene.
    /// Manages visual updates, animations, and formatting for both Neural Credits and CyberCoins.
    /// </summary>
    public class CurrencyDisplayController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Currency Display")]
        [SerializeField] private GameObject currencyPanel;
        [SerializeField] private CanvasGroup currencyCanvasGroup;

        [Header("Neural Credits")]
        [SerializeField] private TextMeshProUGUI neuralCreditsValueText;
        [SerializeField] private TextMeshProUGUI neuralCreditsLabelText;
        [SerializeField] private Image neuralCreditsIcon;
        [SerializeField] private GameObject neuralCreditsGlowEffect;

        [Header("CyberCoins")]
        [SerializeField] private TextMeshProUGUI cyberCoinsValueText;
        [SerializeField] private TextMeshProUGUI cyberCoinsLabelText;
        [SerializeField] private Image cyberCoinsIcon;
        [SerializeField] private GameObject cyberCoinsGlowEffect;

        [Header("Animation Settings")]
        [SerializeField] private float valueChangeAnimationDuration = 0.5f;
        [SerializeField] private float glowEffectDuration = 0.3f;
        [SerializeField] private AnimationCurve valueChangeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float pulseScale = 1.1f;

        [Header("Formatting")]
        [SerializeField] private string currencyFormat = "N0"; // Format with thousand separators
        [SerializeField] private Color positiveChangeColor = new Color(0.2f, 1f, 0.2f, 1f);
        [SerializeField] private Color negativeChangeColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("Change Indicators")]
        [SerializeField] private GameObject neuralCreditsChangeIndicator;
        [SerializeField] private GameObject cyberCoinsChangeIndicator;
        [SerializeField] private TextMeshProUGUI neuralCreditsChangeText;
        [SerializeField] private TextMeshProUGUI cyberCoinsChangeText;

        #endregion

        #region Private Fields

        private CurrencyManager currencyManager;
        private float displayedNeuralCredits;
        private float displayedCyberCoins;
        private Coroutine neuralCreditsAnimationCoroutine;
        private Coroutine cyberCoinsAnimationCoroutine;
        private Vector3 originalNeuralCreditsIconScale;
        private Vector3 originalCyberCoinsIconScale;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            currencyManager = CurrencyManager.Instance;
            ValidateReferences();
            CacheOriginalScales();
            InitializeDisplay();
        }

        private void OnEnable()
        {
            SubscribeToEvents();
            RefreshDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
            StopAllAnimations();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Validates all required UI references are assigned
        /// </summary>
        private void ValidateReferences()
        {
            if (currencyCanvasGroup == null && currencyPanel != null)
            {
                currencyCanvasGroup = currencyPanel.GetComponent<CanvasGroup>();
                if (currencyCanvasGroup == null)
                {
                    currencyCanvasGroup = currencyPanel.AddComponent<CanvasGroup>();
                }
            }

            if (neuralCreditsValueText == null)
                Debug.LogError("[CurrencyDisplayController] Neural Credits value text is not assigned!");

            if (cyberCoinsValueText == null)
                Debug.LogError("[CurrencyDisplayController] CyberCoins value text is not assigned!");
        }

        /// <summary>
        /// Caches the original scales of icons for animation purposes
        /// </summary>
        private void CacheOriginalScales()
        {
            if (neuralCreditsIcon != null)
                originalNeuralCreditsIconScale = neuralCreditsIcon.transform.localScale;

            if (cyberCoinsIcon != null)
                originalCyberCoinsIconScale = cyberCoinsIcon.transform.localScale;
        }

        /// <summary>
        /// Sets up the initial display state
        /// </summary>
        private void InitializeDisplay()
        {
            if (neuralCreditsLabelText != null)
                neuralCreditsLabelText.text = "Neural Credits";

            if (cyberCoinsLabelText != null)
                cyberCoinsLabelText.text = "CyberCoins";

            HideChangeIndicators();
            DisableGlowEffects();
        }

        #endregion

        #region Event Management

        /// <summary>
        /// Subscribes to currency change events
        /// </summary>
        private void SubscribeToEvents()
        {
            if (currencyManager != null)
            {
                currencyManager.OnCurrencyChanged += HandleCurrencyChanged;
                currencyManager.OnCurrencyAdded += HandleCurrencyAdded;
                currencyManager.OnCurrencySpent += HandleCurrencySpent;
                currencyManager.OnInsufficientFunds += HandleInsufficientFunds;
            }
        }

        /// <summary>
        /// Unsubscribes from currency change events
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (currencyManager != null)
            {
                currencyManager.OnCurrencyChanged -= HandleCurrencyChanged;
                currencyManager.OnCurrencyAdded -= HandleCurrencyAdded;
                currencyManager.OnCurrencySpent -= HandleCurrencySpent;
                currencyManager.OnInsufficientFunds -= HandleInsufficientFunds;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles currency value changes with animated transitions
        /// </summary>
        private void HandleCurrencyChanged(CurrencyType type, float oldValue, float newValue)
        {
            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    AnimateNeuralCreditsChange(oldValue, newValue);
                    break;
                case CurrencyType.CyberCoins:
                    AnimateCyberCoinsChange(oldValue, newValue);
                    break;
            }
        }

        /// <summary>
        /// Handles currency addition with positive feedback
        /// </summary>
        private void HandleCurrencyAdded(CurrencyType type, float amount)
        {
            ShowChangeIndicator(type, amount, true);
            PulseIcon(type);
            FlashGlowEffect(type);
        }

        /// <summary>
        /// Handles currency spending with visual feedback
        /// </summary>
        private void HandleCurrencySpent(CurrencyType type, float amount)
        {
            ShowChangeIndicator(type, -amount, false);
        }

        /// <summary>
        /// Handles insufficient funds feedback
        /// </summary>
        private void HandleInsufficientFunds(CurrencyType type)
        {
            StartCoroutine(ShakeAnimation(type));
        }

        #endregion

        #region Display Updates

        /// <summary>
        /// Refreshes the display with current currency values
        /// </summary>
        public void RefreshDisplay()
        {
            if (currencyManager == null) return;

            displayedNeuralCredits = currencyManager.NeuralCredits;
            displayedCyberCoins = currencyManager.CyberCoins;

            UpdateNeuralCreditsDisplay(displayedNeuralCredits);
            UpdateCyberCoinsDisplay(displayedCyberCoins);
        }

        /// <summary>
        /// Updates the Neural Credits display text
        /// </summary>
        private void UpdateNeuralCreditsDisplay(float value)
        {
            if (neuralCreditsValueText != null)
            {
                neuralCreditsValueText.text = value.ToString(currencyFormat);
            }
        }

        /// <summary>
        /// Updates the CyberCoins display text
        /// </summary>
        private void UpdateCyberCoinsDisplay(float value)
        {
            if (cyberCoinsValueText != null)
            {
                cyberCoinsValueText.text = value.ToString(currencyFormat);
            }
        }

        #endregion

        #region Animations

        /// <summary>
        /// Animates Neural Credits value change
        /// </summary>
        private void AnimateNeuralCreditsChange(float fromValue, float toValue)
        {
            if (neuralCreditsAnimationCoroutine != null)
                StopCoroutine(neuralCreditsAnimationCoroutine);

            neuralCreditsAnimationCoroutine = StartCoroutine(AnimateValueChange(
                fromValue,
                toValue,
                UpdateNeuralCreditsDisplay,
                () => displayedNeuralCredits = toValue
            ));
        }

        /// <summary>
        /// Animates CyberCoins value change
        /// </summary>
        private void AnimateCyberCoinsChange(float fromValue, float toValue)
        {
            if (cyberCoinsAnimationCoroutine != null)
                StopCoroutine(cyberCoinsAnimationCoroutine);

            cyberCoinsAnimationCoroutine = StartCoroutine(AnimateValueChange(
                fromValue,
                toValue,
                UpdateCyberCoinsDisplay,
                () => displayedCyberCoins = toValue
            ));
        }

        /// <summary>
        /// Generic value animation coroutine
        /// </summary>
        private IEnumerator AnimateValueChange(float fromValue, float toValue, System.Action<float> updateAction, System.Action onComplete)
        {
            float elapsedTime = 0f;

            while (elapsedTime < valueChangeAnimationDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / valueChangeAnimationDuration;
                float curveValue = valueChangeCurve.Evaluate(progress);
                float currentValue = Mathf.Lerp(fromValue, toValue, curveValue);

                updateAction?.Invoke(currentValue);

                yield return null;
            }

            updateAction?.Invoke(toValue);
            onComplete?.Invoke();
        }

        /// <summary>
        /// Shows change indicator with amount
        /// </summary>
        private void ShowChangeIndicator(CurrencyType type, float amount, bool isPositive)
        {
            GameObject indicator = null;
            TextMeshProUGUI changeText = null;

            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    indicator = neuralCreditsChangeIndicator;
                    changeText = neuralCreditsChangeText;
                    break;
                case CurrencyType.CyberCoins:
                    indicator = cyberCoinsChangeIndicator;
                    changeText = cyberCoinsChangeText;
                    break;
            }

            if (indicator != null && changeText != null)
            {
                changeText.text = $"{(amount >= 0 ? "+" : "")}{amount:N0}";
                changeText.color = isPositive ? positiveChangeColor : negativeChangeColor;

                indicator.SetActive(true);

                // Animate the indicator
                indicator.transform.localScale = Vector3.zero;
                indicator.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                indicator.transform.DOLocalMoveY(indicator.transform.localPosition.y + 30f, 1f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        indicator.SetActive(false);
                        indicator.transform.localPosition = Vector3.zero;
                    });

                // Fade out
                var canvasGroup = indicator.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = indicator.AddComponent<CanvasGroup>();

                canvasGroup.alpha = 1f;
                canvasGroup.DOFade(0f, 1f).SetEase(Ease.InQuad);
            }
        }

        /// <summary>
        /// Pulses the currency icon
        /// </summary>
        private void PulseIcon(CurrencyType type)
        {
            Transform iconTransform = null;
            Vector3 originalScale = Vector3.one;

            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    if (neuralCreditsIcon != null)
                    {
                        iconTransform = neuralCreditsIcon.transform;
                        originalScale = originalNeuralCreditsIconScale;
                    }
                    break;
                case CurrencyType.CyberCoins:
                    if (cyberCoinsIcon != null)
                    {
                        iconTransform = cyberCoinsIcon.transform;
                        originalScale = originalCyberCoinsIconScale;
                    }
                    break;
            }

            if (iconTransform != null)
            {
                iconTransform.DOScale(originalScale * pulseScale, 0.1f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        iconTransform.DOScale(originalScale, 0.1f).SetEase(Ease.InQuad);
                    });
            }
        }

        /// <summary>
        /// Flashes the glow effect
        /// </summary>
        private void FlashGlowEffect(CurrencyType type)
        {
            GameObject glowEffect = null;

            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    glowEffect = neuralCreditsGlowEffect;
                    break;
                case CurrencyType.CyberCoins:
                    glowEffect = cyberCoinsGlowEffect;
                    break;
            }

            if (glowEffect != null)
            {
                glowEffect.SetActive(true);

                var canvasGroup = glowEffect.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = glowEffect.AddComponent<CanvasGroup>();

                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, glowEffectDuration * 0.5f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        canvasGroup.DOFade(0f, glowEffectDuration * 0.5f)
                            .SetEase(Ease.InQuad)
                            .OnComplete(() => glowEffect.SetActive(false));
                    });
            }
        }

        /// <summary>
        /// Shakes the currency display on insufficient funds
        /// </summary>
        private IEnumerator ShakeAnimation(CurrencyType type)
        {
            Transform targetTransform = null;

            switch (type)
            {
                case CurrencyType.NeuralCredits:
                    targetTransform = neuralCreditsValueText?.transform.parent;
                    break;
                case CurrencyType.CyberCoins:
                    targetTransform = cyberCoinsValueText?.transform.parent;
                    break;
            }

            if (targetTransform != null)
            {
                Vector3 originalPosition = targetTransform.localPosition;
                float shakeDuration = 0.5f;
                float shakeStrength = 10f;

                targetTransform.DOShakePosition(shakeDuration, shakeStrength, 10, 90, false, true)
                    .OnComplete(() => targetTransform.localPosition = originalPosition);
            }

            yield return null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Hides all change indicators
        /// </summary>
        private void HideChangeIndicators()
        {
            if (neuralCreditsChangeIndicator != null)
                neuralCreditsChangeIndicator.SetActive(false);

            if (cyberCoinsChangeIndicator != null)
                cyberCoinsChangeIndicator.SetActive(false);
        }

        /// <summary>
        /// Disables all glow effects
        /// </summary>
        private void DisableGlowEffects()
        {
            if (neuralCreditsGlowEffect != null)
                neuralCreditsGlowEffect.SetActive(false);

            if (cyberCoinsGlowEffect != null)
                cyberCoinsGlowEffect.SetActive(false);
        }

        /// <summary>
        /// Stops all running animations
        /// </summary>
        private void StopAllAnimations()
        {
            if (neuralCreditsAnimationCoroutine != null)
            {
                StopCoroutine(neuralCreditsAnimationCoroutine);
                neuralCreditsAnimationCoroutine = null;
            }

            if (cyberCoinsAnimationCoroutine != null)
            {
                StopCoroutine(cyberCoinsAnimationCoroutine);
                cyberCoinsAnimationCoroutine = null;
            }

            DOTween.Kill(this);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows or hides the currency display with animation
        /// </summary>
        public void SetVisible(bool visible, float duration = 0.3f)
        {
            if (currencyCanvasGroup == null) return;

            currencyCanvasGroup.DOFade(visible ? 1f : 0f, duration)
                .OnComplete(() => {
                    currencyCanvasGroup.interactable = visible;
                    currencyCanvasGroup.blocksRaycasts = visible;

                    if (!visible)
                    {
                        currencyPanel.SetActive(false);
                    }
                });

            if (visible && !currencyPanel.activeSelf)
            {
                currencyPanel.SetActive(true);
            }
        }

        /// <summary>
        /// Highlights a specific currency type temporarily
        /// </summary>
        public void HighlightCurrency(CurrencyType type, float duration = 2f)
        {
            StartCoroutine(HighlightCurrencyRoutine(type, duration));
        }

        private IEnumerator HighlightCurrencyRoutine(CurrencyType type, float duration)
        {
            // Flash the glow effect
            FlashGlowEffect(type);

            // Scale up the value text
            TextMeshProUGUI targetText = type == CurrencyType.NeuralCredits ?
                neuralCreditsValueText : cyberCoinsValueText;

            if (targetText != null)
            {
                Vector3 originalScale = targetText.transform.localScale;
                targetText.transform.DOScale(originalScale * 1.2f, 0.3f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => {
                        targetText.transform.DOScale(originalScale, 0.3f)
                            .SetEase(Ease.InQuad)
                            .SetDelay(duration - 0.6f);
                    });
            }

            yield return new WaitForSeconds(duration);
        }

        #endregion

        #region Cleanup

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
            StopAllAnimations();
        }

        #endregion
    }
}
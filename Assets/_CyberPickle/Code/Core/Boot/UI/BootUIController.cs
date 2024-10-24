using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CyberPickle.Core.Boot.UI
{
    public class BootUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private CanvasGroup logoCanvasGroup;
        [SerializeField] private TextMeshProUGUI companyNameText;
        [SerializeField] private Slider loadingBarSlider;
        [SerializeField] private TextMeshProUGUI loadingText;

        private void Awake()
        {
            ValidateReferences();
        }

        private void ValidateReferences()
        {
            if (logoCanvasGroup == null)
                Debug.LogError("[BootUIController] Logo CanvasGroup is not assigned!");
            if (companyNameText == null)
                Debug.LogError("[BootUIController] Company Name Text is not assigned!");
            if (loadingBarSlider == null)
                Debug.LogError("[BootUIController] Loading Bar Slider is not assigned!");
            if (loadingText == null)
                Debug.LogError("[BootUIController] Loading Text is not assigned!");
        }

        public void UpdateProgress(float progress)
        {
            if (loadingBarSlider != null)
            {
                loadingBarSlider.value = progress;
                Debug.Log($"[BootUIController] Progress updated: {progress:P0}");
            }
        }

        public void UpdateLoadingText(string text)
        {
            if (loadingText != null)
            {
                loadingText.text = text;
                Debug.Log($"[BootUIController] Loading text updated: {text}");
            }
        }

        public void SetLogoAlpha(float alpha)
        {
            if (logoCanvasGroup != null)
            {
                logoCanvasGroup.alpha = alpha;
                Debug.Log($"[BootUIController] Logo alpha updated: {alpha:F2}");
            }
        }
    }
}
// File: Assets/_CyberPickle/Code/UI/HUD/XpBarUI.cs
// Namespace: CyberPickle.UI.HUD
//
// XP bar in the in-run HUD. Subscribes to PlayerXPBridge.OnXPChanged and
// updates a Slider showing progress to next level + a TMP showing the
// current level. Auto-discovers PlayerXPBridge on RunStart (same pattern
// as HealthBarUI).
//
// Layout convention: thin horizontal bar (often near the bottom of the
// screen, full-width) with a small "Lv N" label. Survivor-genre standard.

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Player;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class XpBarUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Slider showing XP progress to next level (0..1). Required.")]
        [SerializeField] private Slider fillSlider;

        [Tooltip("TMP showing 'Lv N' or just the number. Optional — leave empty for bar-only.")]
        [SerializeField] private TextMeshProUGUI levelLabel;

        [Tooltip("TMP showing 'currentXP / xpToNextLevel'. Optional. Most VS-style HUDs omit this.")]
        [SerializeField] private TextMeshProUGUI xpValueLabel;

        [Header("Display")]
        [Tooltip("Format for level label. {0}=level. e.g. 'Lv {0}' → 'Lv 5'.")]
        [SerializeField] private string levelFormat = "Lv {0}";

        [Tooltip("Format for xp value label. {0}=current, {1}=threshold. e.g. '{0}/{1}'.")]
        [SerializeField] private string xpValueFormat = "{0}/{1}";

        [Header("Diagnostics")]
        [SerializeField] private bool verbose;

        private PlayerXPBridge _xpBridge;
        private bool _subscribed;

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
            UnsubscribeFromBridge();
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            if (type == MusicEvent.RunStart) BindToBridge();
        }

        private void BindToBridge()
        {
            if (_xpBridge == null)
                _xpBridge = FindFirstObjectByType<PlayerXPBridge>();

            if (_xpBridge == null)
            {
                Debug.LogError("[XpBarUI] No PlayerXPBridge found at RunStart.");
                return;
            }

            if (!_subscribed)
            {
                _xpBridge.OnXPChanged += HandleXpChanged;
                _subscribed = true;
            }

            // Push initial values.
            HandleXpChanged(_xpBridge.CurrentXP, _xpBridge.XPToNextLevel, _xpBridge.CurrentLevel);

            if (verbose)
                Debug.Log($"[XpBarUI] Bound to PlayerXPBridge on '{_xpBridge.name}'.");
        }

        private void UnsubscribeFromBridge()
        {
            if (_subscribed && _xpBridge != null)
            {
                _xpBridge.OnXPChanged -= HandleXpChanged;
                _subscribed = false;
            }
        }

        private void HandleXpChanged(int currentXp, int xpToNext, int currentLevel)
        {
            if (fillSlider != null)
                fillSlider.value = xpToNext > 0 ? (float)currentXp / xpToNext : 0f;

            if (levelLabel != null)
                levelLabel.text = string.Format(levelFormat, currentLevel);

            if (xpValueLabel != null)
                xpValueLabel.text = string.Format(xpValueFormat, currentXp, xpToNext);
        }
    }
}

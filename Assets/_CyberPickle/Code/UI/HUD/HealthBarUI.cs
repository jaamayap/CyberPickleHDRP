// File: Assets/_CyberPickle/Code/UI/HUD/HealthBarUI.cs
// Namespace: CyberPickle.UI.HUD
//
// Health bar in the in-run HUD. Subscribes to PlayerHealth.OnHealthChanged
// and updates a Slider + numeric TMP label. Auto-discovers PlayerHealth
// on RunStart (the player isn't spawned at scene-load, so we can't bind
// in OnEnable — same pattern as LevelUpCoordinator).
//
// Why expose the numeric value alongside the bar: VS-style invisible-numbers
// excludes color-blind / vision-impaired players. Per GDD §3.12.6
// player-side feedback should be readable. Toggle the numeric label off
// in the inspector if a particular HUD layout doesn't want it.

using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Player;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class HealthBarUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Slider showing HP fill (0..1). Required.")]
        [SerializeField] private Slider fillSlider;

        [Tooltip("TMP showing 'current / max' or 'current%'. Optional — leave empty for bar-only.")]
        [SerializeField] private TextMeshProUGUI valueLabel;

        [Header("Display")]
        [Tooltip("Format: '{0:F0}/{1:F0}' shows '47/100'. '{0:F0}%' shows '47%'. Uses {0}=current, {1}=max.")]
        [SerializeField] private string valueFormat = "{0:F0}/{1:F0}";

        [Header("Diagnostics")]
        [SerializeField] private bool verbose;

        private PlayerHealth _health;
        private bool _subscribed;

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
            UnsubscribeFromHealth();
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            if (type == MusicEvent.RunStart)
            {
                BindToPlayerHealth();
            }
        }

        private void BindToPlayerHealth()
        {
            // Defensive: if the user pre-bound a reference we'll respect it,
            // otherwise find at run-start time when the player exists.
            if (_health == null)
                _health = FindFirstObjectByType<PlayerHealth>();

            if (_health == null)
            {
                Debug.LogError("[HealthBarUI] No PlayerHealth found at RunStart. Player did not spawn?");
                return;
            }

            if (!_subscribed)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _subscribed = true;
            }

            // Push initial values immediately — OnHealthChanged won't fire
            // until the next damage/heal, but the bar should reflect full HP
            // from frame zero of the run.
            HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);

            if (verbose)
                Debug.Log($"[HealthBarUI] Bound to PlayerHealth on '{_health.name}'.");
        }

        private void UnsubscribeFromHealth()
        {
            if (_subscribed && _health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _subscribed = false;
            }
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (fillSlider != null)
                fillSlider.value = max > 0f ? current / max : 0f;

            if (valueLabel != null)
                valueLabel.text = string.Format(valueFormat, current, max);
        }
    }
}

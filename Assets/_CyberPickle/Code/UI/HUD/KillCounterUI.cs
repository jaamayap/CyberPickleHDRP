// File: Assets/_CyberPickle/Code/UI/HUD/KillCounterUI.cs
// Namespace: CyberPickle.UI.HUD
//
// Displays the active run's enemy-kill count. Reads from
// RunStatsTracker.EnemiesKilled each frame, but only updates the TMP when
// the value changes — most frames there's no kill, so this is essentially
// idle.
//
// Could alternately subscribe to a "kill happened" event, but RunStatsTracker
// doesn't expose one and the polling cost is negligible (one int comparison
// per frame). Keeping it simple.

using TMPro;
using UnityEngine;
using CyberPickle.Gameplay.RunState;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class KillCounterUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("TMP showing the kill count. Required.")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("Display")]
        [Tooltip("Format string. {0}=kill count. Default just the number.")]
        [SerializeField] private string format = "{0}";

        [Tooltip("Optional icon (skull / target / whatever) is purely visual; this component doesn't manage it. Authors place an Image next to the label in the layout.")]
        [SerializeField] private bool showZeroAtStart = true;

        private int _lastDisplayedKills = -1;

        private void Awake()
        {
            if (showZeroAtStart && label != null)
                label.text = string.Format(format, 0);
        }

        private void Update()
        {
            var tracker = RunStatsTracker.Instance;
            if (tracker == null) return;

            int kills = tracker.EnemiesKilled;
            if (kills == _lastDisplayedKills) return;
            _lastDisplayedKills = kills;

            if (label != null)
                label.text = string.Format(format, kills);
        }
    }
}

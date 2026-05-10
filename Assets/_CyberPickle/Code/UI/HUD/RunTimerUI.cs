// File: Assets/_CyberPickle/Code/UI/HUD/RunTimerUI.cs
// Namespace: CyberPickle.UI.HUD
//
// Displays the active run's elapsed time as MM:SS. Reads from
// RunStateManager.RunTime each frame — RunTime is just a float that
// only ticks during Running, so polling is safe and bounded. No event
// subscription needed.
//
// Update is gated to "only update when the displayed text would change"
// (every full second) so we're not throwing TMP geometry rebuilds at the
// renderer 60+ times per second for a clock that only ticks once per
// second visually.

using TMPro;
using UnityEngine;
using CyberPickle.Gameplay.RunState;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class RunTimerUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("TMP showing the timer. Required.")]
        [SerializeField] private TextMeshProUGUI label;

        [Header("Display")]
        [Tooltip("Format string. {0}=minutes, {1}=seconds (zero-padded). Default 'MM:SS'.")]
        [SerializeField] private string format = "{0:00}:{1:00}";

        private int _lastDisplayedSeconds = -1;

        private void Update()
        {
            var rsm = RunStateManager.Instance;
            if (rsm == null) return;

            int totalSeconds = Mathf.FloorToInt(rsm.RunTime);
            if (totalSeconds == _lastDisplayedSeconds) return;
            _lastDisplayedSeconds = totalSeconds;

            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            if (label != null)
                label.text = string.Format(format, minutes, seconds);
        }
    }
}

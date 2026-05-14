// File: Assets/_CyberPickle/Code/UI/HUD/HoloRadarHUD.cs
// Namespace: CyberPickle.UI.HUD
//
// Cyberpunk-themed BPM + beat-grid readout. Drop this on a HUD GameObject
// in your canvas, wire up the 6 references in the Inspector, and play —
// the conductor's tempo + 4-beat bar grid become visible peripheral UI.
//
// Visual model (the "Holo Radar Ring"):
//
//   • A sweep arm (RectTransform) rotates clockwise around a centre point.
//     Rotation is PHASE-LOCKED to the conductor's bar — one full revolution
//     = one bar = 4 beats. Speed is therefore implicitly the BPM, so the
//     player sees tempo as the rate of the sweep.
//
//   • Four "beat ticks" (Image components) sit on the perimeter. When a
//     beat fires, that beat's tick flashes lit (designer-tunable colour),
//     then fades back to the unlit colour over tickFadeSeconds.
//
//   • A TMP label in the centre shows "128 BPM". Flashes briefly when BPM
//     changes (Dex-up dopamine).
//
// Phase-lock instead of free rotation: we read CurrentSubdivision from the
// conductor + interpolate sub-subdivision progress via TimeUntilNextSubdivision
// so the sweep is always EXACTLY aligned with the music — never drifts
// even after pauses, level-up screens, or BPM changes.
//
// Unscaled time everywhere: the HUD ticks during LevelUpPaused, Paused,
// and GameOver phases too (mirrors how DOTween's SetUpdate(true) handles
// pause-resilient UI per CLAUDE.md).
//
// Lazy subscription: if MusicConductor.Instance isn't alive when this
// enables (rare — boot order should have it), we re-attempt subscription
// each frame in Update. No spam, just one if-check.
//
// This is M9 music-HUD slice 1. Slices 2 (per-slot fill rings on the cross)
// and 3 (bar-start vignette pulse) build on the same conductor reads.

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Gameplay.Audio;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class HoloRadarHUD : MonoBehaviour
    {
        // ─── Inspector wiring ────────────────────────────────────────────

        [Header("Sweep Arm")]
        [Tooltip("RectTransform that rotates to indicate position-within-bar. Anchor its PIVOT at the centre of the ring; the script writes localRotation.z each frame so it sweeps around. One full revolution = one musical bar.")]
        [SerializeField] private RectTransform sweepArm;

        [Tooltip("If true, the sweep rotates clockwise (Z decreases). False = counter-clockwise. Designer preference — clockwise reads as 'time forward' in most cultures.")]
        [SerializeField] private bool sweepClockwise = true;

        [Header("Beat Ticks (4 entries — one per beat in a bar)")]
        [Tooltip("Four Image components on the ring's perimeter, one per beat. Index 0 = beat 1, index 3 = beat 4. The script tints them between tickLitColor (just fired) and tickUnlitColor (idle).")]
        [SerializeField] private Image[] beatTicks = new Image[4];

        [Tooltip("Colour of a tick at the instant its beat fires. Cyan / hot-pink / etc. for the cyberpunk look — high saturation reads at HDRP brightness.")]
        [SerializeField] private Color tickLitColor = new Color(0.4f, 1f, 1f, 1f);

        [Tooltip("Colour of a tick when no recent beat is associated with it. Dim version of tickLitColor for a 'standby' look.")]
        [SerializeField] private Color tickUnlitColor = new Color(0.2f, 0.6f, 0.6f, 0.35f);

        [Tooltip("Seconds for a tick to fade from lit → unlit after its beat fires. Short (0.2-0.4s) keeps the visual snappy; longer feels laggy.")]
        [Min(0.05f)] [SerializeField] private float tickFadeSeconds = 0.35f;

        [Header("BPM Label")]
        [Tooltip("TMP showing the current BPM. Updated each frame when BPM changes. Required.")]
        [SerializeField] private TextMeshProUGUI bpmLabel;

        [Tooltip("Format string for the BPM text. {0} = bpm number. Default 'F0' shows '128 BPM' — F1 would show '128.5 BPM' if you want decimal precision.")]
        [SerializeField] private string bpmFormat = "{0:F0} BPM";

        [Tooltip("If true, the BPM label flashes a different colour briefly when BPM changes (e.g. Dex-up). Free dopamine — the player SEES their stat investment pay off.")]
        [SerializeField] private bool flashBpmOnChange = true;

        [Tooltip("Colour the BPM label flashes TO on a change. Returns to the label's base colour over bpmFlashSeconds.")]
        [SerializeField] private Color bpmFlashColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("Seconds for the BPM flash to fade back to base. Short (0.4-0.6s) for a punchy 'just-changed' feel.")]
        [Min(0.05f)] [SerializeField] private float bpmFlashSeconds = 0.5f;

        [Header("Visibility")]
        [Tooltip("If true, hides the radar entirely when no MusicConductor is running (e.g. menu scenes). False = shows last-known values frozen.")]
        [SerializeField] private bool hideWhenNoConductor = true;

        [Tooltip("Optional CanvasGroup on a parent — gets alpha=0 when hidden. Cheaper + cleaner than SetActive for fade transitions. Optional.")]
        [SerializeField] private CanvasGroup containerGroup;

        // ─── Runtime state ───────────────────────────────────────────────

        // Per-tick brightness in [0..1], 1 = just fired. Decays toward 0 each
        // frame at 1/tickFadeSeconds per second. Driven by HandleBeat below.
        private float[] _tickIntensities;

        // Last BPM displayed — used to detect changes and trigger the flash.
        private float _lastDisplayedBpm = -1f;

        // BPM flash state.
        private float _bpmFlashRemaining;
        private Color _bpmBaseColor = Color.white;

        // Subscription state — we attempt lazy subscription each frame if
        // the conductor wasn't alive on enable.
        private bool _subscribed;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _tickIntensities = new float[(beatTicks != null) ? beatTicks.Length : 0];
            if (bpmLabel != null) _bpmBaseColor = bpmLabel.color;
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var c = MusicConductor.Instance;
            if (c == null) return; // try again next frame
            c.OnBeat += HandleBeat;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            var c = MusicConductor.Instance;
            if (c != null) c.OnBeat -= HandleBeat;
            _subscribed = false;
        }

        private void HandleBeat()
        {
            var c = MusicConductor.Instance;
            if (c == null || _tickIntensities == null || _tickIntensities.Length == 0) return;

            // CurrentBeat is a monotonic counter from RunStart. Beat-in-bar
            // = beat % 4. The conductor's bars are 4 beats each by design
            // (per `procedural_music_reference.md`); if you ever go to 3/4
            // or 5/4, change the divisor here and the beatTicks array length.
            int beatInBar = c.CurrentBeat % beatTicks.Length;
            if (beatInBar >= 0 && beatInBar < _tickIntensities.Length)
            {
                _tickIntensities[beatInBar] = 1f;
            }
        }

        // ─── Per-frame update ────────────────────────────────────────────

        private void Update()
        {
            TrySubscribe(); // lazy if conductor wasn't ready earlier
            var c = MusicConductor.Instance;

            // Hide / dim when no conductor (menus, boot scenes, etc.).
            if (c == null)
            {
                if (hideWhenNoConductor && containerGroup != null)
                    containerGroup.alpha = 0f;
                return;
            }
            if (containerGroup != null) containerGroup.alpha = 1f;

            UpdateSweep(c);
            UpdateTicks();
            UpdateBpmLabel(c);
        }

        /// <summary>
        /// Set the sweep arm's rotation to match the current bar phase.
        /// Phase = subdivisions-elapsed-this-bar / subdivisions-per-bar,
        /// interpolated with sub-subdivision progress for smoothness.
        /// </summary>
        private void UpdateSweep(MusicConductor c)
        {
            if (sweepArm == null) return;

            int beatsPerBar = (beatTicks != null && beatTicks.Length > 0) ? beatTicks.Length : 4;
            float subsPerBar = beatsPerBar * c.SubdivisionsPerBeat;
            if (subsPerBar <= 0f) return;

            // CurrentSubdivision is monotonic since RunStart. Modulo the bar
            // gives the integer subdivisions elapsed within the current bar.
            float subsThisBar = c.CurrentSubdivision % subsPerBar;

            // Sub-subdivision interpolation — the sweep glides smoothly
            // between integer subdivisions instead of stepping. Without this,
            // the sweep would tick discretely on every subdivision (jagged
            // motion at low subdivisions-per-beat counts).
            float secondsToNext   = c.TimeUntilNextSubdivision();
            float secondsPerSub   = c.SecondsPerSubdivision;
            float subProgress     = (secondsPerSub > 0.0001f)
                                  ? Mathf.Clamp01(1f - secondsToNext / secondsPerSub)
                                  : 0f;

            float phase = (subsThisBar + subProgress) / subsPerBar; // 0..1 across the bar
            float angle = phase * 360f * (sweepClockwise ? -1f : 1f);
            sweepArm.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>
        /// Decay each tick's intensity toward 0, then tint the corresponding
        /// Image between unlit and lit colours.
        /// </summary>
        private void UpdateTicks()
        {
            if (beatTicks == null || _tickIntensities == null) return;
            float dt = Time.unscaledDeltaTime;
            float fadeRate = 1f / Mathf.Max(0.01f, tickFadeSeconds);

            int n = Mathf.Min(beatTicks.Length, _tickIntensities.Length);
            for (int i = 0; i < n; i++)
            {
                _tickIntensities[i] = Mathf.Max(0f, _tickIntensities[i] - dt * fadeRate);
                if (beatTicks[i] == null) continue;
                beatTicks[i].color = Color.Lerp(tickUnlitColor, tickLitColor, _tickIntensities[i]);
            }
        }

        /// <summary>
        /// Refresh the BPM label. Detect changes; on change, trigger the
        /// flash (if enabled). Flash is a colour lerp back to base over
        /// bpmFlashSeconds, using unscaled time so it still plays during
        /// pause.
        /// </summary>
        private void UpdateBpmLabel(MusicConductor c)
        {
            if (bpmLabel == null) return;

            float currentBpm = c.BPM;
            if (Mathf.Abs(currentBpm - _lastDisplayedBpm) > 0.05f)
            {
                bool wasInitialized = _lastDisplayedBpm > 0f;
                _lastDisplayedBpm = currentBpm;
                bpmLabel.text = string.Format(bpmFormat, currentBpm);

                if (flashBpmOnChange && wasInitialized)
                {
                    _bpmFlashRemaining = bpmFlashSeconds;
                }
            }

            // Flash fade — interpolates from flashColor (at t=1) back to
            // baseColor (at t=0). Linear is fine for short durations.
            if (_bpmFlashRemaining > 0f)
            {
                _bpmFlashRemaining -= Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_bpmFlashRemaining / bpmFlashSeconds);
                bpmLabel.color = Color.Lerp(_bpmBaseColor, bpmFlashColor, t);
            }
            else if (bpmLabel.color != _bpmBaseColor)
            {
                // Settle exactly on the base colour once the flash ends so
                // we don't accumulate floating-point drift over many flashes.
                bpmLabel.color = _bpmBaseColor;
            }
        }
    }
}

// File: Assets/_CyberPickle/Code/UI/HUD/WeaponSlotBeatPulse.cs
// Namespace: CyberPickle.UI.HUD
//
// Per-slot animator that combines TWO layers of music-driven feedback:
//
//   1. ANTICIPATION — a fill ring encircling the slot creeps from 0 → 1
//      between shots, like the burning fuse on a TNT bomb. A bright "fuse
//      spark" image tracks the tip of the fill, optionally trailing UI
//      particles. The player SEES when the next shot is coming.
//
//   2. REACTION — at the moment of fire, the slot DANCES: the icon scale-
//      punches up + springs back, rotates slightly (alternating each shot
//      so it wobbles rather than drifts), and an element-tinted burst
//      ring explodes outward and fades.
//
// Why combine both: anticipation reads tempo across the whole loadout
// (sniper fuse fills 4× slower than pistol fuse — polyrhythm becomes
// VISIBLE), and reaction punctuates each shot with a satisfying pulse.
// Together they make the equipment cross feel ALIVE with the music
// instead of being a static readout.
//
// Listens for MusicEvent.WeaponFire on the bus and filters by SlotIndex so
// this component only reacts to ITS weapon's shots. Slot index is set by
// the parent WeaponSlotsPanel via SetSlotIndex (same pattern as WeaponSlotUI).
//
// Pause-safe: DOTween tweens use SetUpdate(true) (unscaled time), and the
// fill update polls Time.unscaledTime — animations keep playing during
// LevelUpPaused / Paused / GameOver per CLAUDE.md.

using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Core;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.RunState;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.UI.HUD
{
    /// <summary>
    /// Path along which the fuse spark walks as the fill ring fills.
    /// Drives ONLY the spark's position; the radial fill itself is always
    /// angular (Unity Image.Type.Filled handles that).
    /// </summary>
    public enum FuseTrackShape
    {
        /// <summary>Spark moves on a circle inscribed inside the fillRing's bounds. Correct for circular slot designs.</summary>
        Circle,
        /// <summary>Spark moves along the rectangular perimeter of the fillRing's bounds. Correct for square / rectangular slot designs — hugs the corners instead of cutting across diagonals.</summary>
        Rectangle,
    }

    [DisallowMultipleComponent]
    public class WeaponSlotBeatPulse : MonoBehaviour
    {
        // ─── References ──────────────────────────────────────────────────

        [Header("Slot Dance (reaction on fire)")]
        [Tooltip("RectTransform that gets the scale-punch + rotation flick on fire. Typically the slot icon (or a parent that wraps the icon + frame). Required for the dance effect.")]
        [SerializeField] private RectTransform pulseTarget;

        [Header("Fuse Ring (anticipation)")]
        [Tooltip("Image with Image Type = Filled, Fill Method = Radial 360. Encircles the slot. Fill goes 0 → 1 between shots and snaps back to 0 on fire. Optional — leave null to skip the anticipation layer entirely.")]
        [SerializeField] private Image fillRing;

        [Tooltip("Small bright Image positioned at the tip of the fill — the burning end of the fuse. Optional child can have a UI ParticleSystem for trailing sparks. Optional — leave null to skip the spark.")]
        [SerializeField] private RectTransform fuseSpark;

        [Tooltip("Shape of the path the fuse spark walks. Circle = inscribed circle inside the fillRing (correct for circular slots). Rectangle = the fillRing's outer rectangle perimeter (correct for square / rectangular slots — spark hugs corners instead of cutting across diagonals).")]
        [SerializeField] private FuseTrackShape trackShape = FuseTrackShape.Circle;

        [Tooltip("If true, the fuse spark's local rotation tracks the fill angle so it 'leans into' the direction it's burning. Pure visual polish.")]
        [SerializeField] private bool rotateFuseSpark = true;

        [Tooltip("Optional ParticleSystem under the fuse spark — gets played/stopped along with the spark's visibility. Drop in a small UI particle that emits trailing sparks for the TNT-fuse vibe.")]
        [SerializeField] private ParticleSystem fuseSparkParticles;

        [Header("Burst Ring (reaction on fire)")]
        [Tooltip("Image that expands outward from the slot at the moment of fire, fading out. Element-tinted via tintBurstByElement. Optional — leave null to skip the burst layer.")]
        [SerializeField] private Image burstRing;

        // ─── Dance pulse tunables ────────────────────────────────────────

        [Header("Scale Punch")]
        [Tooltip("How much the pulseTarget grows on fire (relative — 0.15 = 15% bigger at peak). DOTween's DOPunchScale overshoots slightly so 0.15 looks like a noticeable bounce.")]
        [Min(0f)] [SerializeField] private float scalePunch = 0.15f;

        [Tooltip("Seconds for the scale punch to play out (peak + return).")]
        [Min(0.05f)] [SerializeField] private float scaleDuration = 0.18f;

        [Tooltip("How elastic the spring-back is. 0 = no overshoot (clean), 1 = lots of bounce. 0.5 reads as a confident pop.")]
        [Range(0f, 1f)] [SerializeField] private float scaleElasticity = 0.5f;

        [Header("Rotation Flick")]
        [Tooltip("Magnitude of rotation in degrees. ±5° is a subtle wobble; ±10° is energetic.")]
        [Range(0f, 30f)] [SerializeField] private float rotationDegrees = 5f;

        [Tooltip("If true, rotation direction alternates each shot (CW / CCW / CW / ...) so the slot wobbles rather than slowly drifting in one direction.")]
        [SerializeField] private bool alternateRotation = true;

        [Tooltip("Seconds for the rotation flick to play out.")]
        [Min(0.05f)] [SerializeField] private float rotationDuration = 0.18f;

        // ─── Burst ring tunables ─────────────────────────────────────────

        [Header("Burst Ring Animation")]
        [Tooltip("Starting scale of the burst ring (relative to the slot). Typically 0.8-1.0 so it starts close to the slot's edge before expanding.")]
        [Min(0f)] [SerializeField] private float burstStartScale = 0.9f;

        [Tooltip("Final scale the burst ring expands to.")]
        [Min(0f)] [SerializeField] private float burstEndScale = 1.8f;

        [Tooltip("Seconds for the burst ring's expand+fade animation. Short (0.25-0.4s) reads as a snap; longer feels lingering.")]
        [Min(0.05f)] [SerializeField] private float burstDuration = 0.3f;

        [Tooltip("Color of the burst ring at the start of its animation (fully bright).")]
        [SerializeField] private Color burstStartColor = new Color(1f, 1f, 1f, 1f);

        [Tooltip("Color of the burst ring at the end (typically alpha 0 for fade-out).")]
        [SerializeField] private Color burstEndColor = new Color(1f, 1f, 1f, 0f);

        [Tooltip("If true, multiplies the burst's RGB by the weapon's element colour at fire time, so Ice grenades pop cyan, Fire grenades pop orange, etc.")]
        [SerializeField] private bool tintBurstByElement = true;

        // ─── Fill ring tunables ──────────────────────────────────────────

        [Header("Fuse Behaviour")]
        [Tooltip("Default fire interval in seconds when no weapon data is available yet (first frame, scene-test setups). The fill ring fills over this duration until the first real fire event arrives.")]
        [Min(0.1f)] [SerializeField] private float defaultFireInterval = 0.5f;

        // ─── Diagnostics ─────────────────────────────────────────────────

        [Header("Diagnostics")]
        [Tooltip("Log each fire-event reception for this slot. Off by default.")]
        [SerializeField] private bool verbose = false;

        // ─── Runtime state ───────────────────────────────────────────────

        // Default -1 = uninitialized. Auto-resolved in Start() from a
        // sibling WeaponSlotUI if not explicitly set via SetSlotIndex.
        // Without this auto-discovery, BeatPulses scattered across slot
        // prefabs would all default to 0 and react only to slot 0's fire
        // events — all four slots would animate in lockstep with the pistol.
        private int   _slotIndex = -1;
        private bool  _slotIndexResolved;

        private float _lastFireTime;          // SCALED Time.time when current cycle started
        private float _fireInterval;          // seconds between fires (1/fireRate) at the current rate
        private float _currentCycleDuration;  // duration the fuse should fill over for the CURRENT cycle
                                              //   = timeUntilFirst on first cycle of any anticipation
                                              //   = _fireInterval after the first real fire arrives
                                              //   "Hybrid" mode keeps fill 0→100% smooth regardless of where the cycle starts on the grid
        private bool  _hasFiredOnce;
        private bool  _rotateFlip;            // toggle for alternating rotation direction
        private bool  _subscribed;

        // Targeting state, broadcast by WeaponFiring via MusicEvent.WeaponAimChanged.
        // When false, hide all anticipation visuals — the weapon isn't going to
        // fire on any of the upcoming grid cells because HandleSubdivision skips
        // them. When the target is acquired we reset _hasFiredOnce to re-start
        // the anticipation cleanly from 0% for the upcoming first shot.
        private bool _hasTarget;

        // Cached references for fast access.
        private Tween _scaleTween;
        private Tween _rotationTween;
        private Tween _burstScaleTween;
        private Tween _burstColorTween;

        public void SetSlotIndex(int idx)
        {
            _slotIndex = idx;
            _slotIndexResolved = true;
        }

        /// <summary>
        /// Resolve the slot index (call before any consumer reads it).
        /// Explicit setter wins; otherwise inherit from a sibling
        /// WeaponSlotUI (which WeaponSlotsPanel always indexes correctly
        /// via its slots[] array). Last resort: 0, with a warning.
        ///
        /// Lazy so we don't depend on Awake/Start ordering between the
        /// panel and the per-slot components.
        /// </summary>
        private void EnsureSlotIndexResolved()
        {
            if (_slotIndexResolved) return;

            // Did an explicit caller already set us (negative-fence test)?
            if (_slotIndex >= 0)
            {
                _slotIndexResolved = true;
                return;
            }

            // Inherit from sibling WeaponSlotUI — same GameObject's slot UI
            // already knows its index thanks to WeaponSlotsPanel.Awake.
            var siblingSlot = GetComponent<WeaponSlotUI>();
            if (siblingSlot != null && siblingSlot.SlotIndex >= 0)
            {
                _slotIndex = siblingSlot.SlotIndex;
                _slotIndexResolved = true;
                if (verbose) Debug.Log($"[WeaponSlotBeatPulse] '{name}' inherited slot index {_slotIndex} from sibling WeaponSlotUI.");
                return;
            }

            // Last-resort default with a warning — designer should fix.
            _slotIndex = 0;
            _slotIndexResolved = true;
            Debug.LogWarning($"[WeaponSlotBeatPulse] '{name}' could not resolve slot index — defaulting to 0. Either drop this component into WeaponSlotsPanel.beatPulses[] in the right order OR put it on the same GameObject as a WeaponSlotUI so it can inherit the index.", this);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _fireInterval = defaultFireInterval;

            // Hide all anticipation + reaction visuals until the first
            // anticipation window opens. Update() will reveal them when
            // it's actually time to show progress toward a shot.
            if (burstRing != null) burstRing.gameObject.SetActive(false);
            if (fuseSpark != null) fuseSpark.gameObject.SetActive(false);
            if (fillRing  != null) fillRing.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed) MusicEventBus.OnEvent -= HandleMusicEvent;
            _subscribed = false;

            // Kill any in-flight tweens so we don't leak callbacks.
            _scaleTween?.Kill();
            _rotationTween?.Kill();
            _burstScaleTween?.Kill();
            _burstColorTween?.Kill();
        }

        // ─── Event handling ──────────────────────────────────────────────

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            // RunStart — reset anticipation state. Conductor's subdivision
            // counter resets at RunStart (when it transitions back to
            // Running from Loading or GameOver), so any anticipation we
            // computed earlier (during Loading, or from a previous run)
            // is stale. Forcing _hasFiredOnce = false makes the next
            // Update re-run TryAnticipateFirstFire with fresh grid state,
            // giving the STARTING weapon (the one equipped from the
            // equipment hub) a properly-synced first-shot anticipation.
            if (type == MusicEvent.RunStart)
            {
                _hasFiredOnce = false;
                _lastFireTime = 0f;
                _currentCycleDuration = _fireInterval;
                _hasTarget = false; // wait for explicit WeaponAimChanged from WeaponFiring
                HideFuseVisuals();
                if (verbose) Debug.Log($"[WeaponSlotBeatPulse] Slot {_slotIndex} reset on RunStart.");
                return;
            }

            // Targeting state — drives whether anticipation visuals are
            // even valid. No target → no fires → hide. Target acquired →
            // re-anticipate from 0% over the actual time-until-next-cell.
            if (type == MusicEvent.WeaponAimChanged)
            {
                if (payload is not WeaponAimPayload aim) return;
                EnsureSlotIndexResolved();
                if (aim.SlotIndex != _slotIndex) return;

                bool wasTargeting = _hasTarget;
                _hasTarget = aim.HasTarget;

                if (!_hasTarget)
                {
                    // Lost target — hide visuals. The next Update will
                    // keep them hidden via the !_hasTarget check.
                    HideFuseVisuals();
                    if (verbose) Debug.Log($"[WeaponSlotBeatPulse] Slot {_slotIndex} target lost — hiding.");
                }
                else if (!wasTargeting)
                {
                    // Just acquired a target. Reset anticipation so the
                    // next Update re-runs TryAnticipateFirstFire and the
                    // fuse appears at 0%, filling smoothly toward the
                    // first grid-locked fire. THIS is the moment the
                    // player should see anticipation begin.
                    _hasFiredOnce = false;
                    if (verbose) Debug.Log($"[WeaponSlotBeatPulse] Slot {_slotIndex} target acquired — anticipating.");
                }
                return;
            }

            if (type != MusicEvent.WeaponFire) return;
            if (payload is not WeaponFirePayload data) return;
            EnsureSlotIndexResolved();
            if (data.SlotIndex != _slotIndex) return; // not our slot

            if (verbose) Debug.Log($"[WeaponSlotBeatPulse] Slot {_slotIndex} fired '{data.WeaponId}' — playing dance + burst.");

            // Refresh the fire interval from the weapon's effective fire rate.
            // The fuse fills over this interval until the next fire event
            // resets the timer. NOTE: GetFireRateForLevel reads the current
            // BPM internally, so this naturally reflects Dex changes too.
            UpdateFireInterval();

            // Snapshot time so the fill ring re-starts from 0 next frame.
            // Scaled Time.time keeps us in lockstep with the music conductor
            // (which uses Time.time and pauses during non-Running phases).
            _lastFireTime = Time.time;
            _currentCycleDuration = _fireInterval; // back to NORMAL cycle after first real fire
            _hasFiredOnce = true;

            // Trigger the dance + burst animations. The fuse spark + fill
            // visibility is handled by the next Update frame's check
            // (elapsed >= 0 right after a fire reset, so they show
            // automatically).
            TriggerDance();
            TriggerBurst();
        }

        /// <summary>
        /// Read the effective fire rate for the current weapon in our slot
        /// and convert to a per-shot interval (seconds). Used to drive the
        /// fuse-ring fill speed. Falls back to defaultFireInterval if the
        /// loadout isn't ready or the weapon doesn't expose a rate.
        /// </summary>
        private void UpdateFireInterval()
        {
            EnsureSlotIndexResolved();

            var loadout = WeaponLoadoutRuntime.Instance;
            if (loadout == null) { _fireInterval = defaultFireInterval; return; }

            var instance = loadout.GetSlot(_slotIndex);
            if (instance == null || !instance.IsValid || instance.weaponData == null)
            {
                _fireInterval = defaultFireInterval;
                return;
            }

            float rate = instance.weaponData.GetFireRateForLevel(instance.level);
            if (rate <= 0.001f) { _fireInterval = defaultFireInterval; return; }
            _fireInterval = 1f / rate;
        }

        // ─── Dance + burst animations ────────────────────────────────────

        private void TriggerDance()
        {
            if (pulseTarget == null) return;

            // Kill any in-flight tweens so a fast-firing weapon doesn't queue
            // overlapping animations (would compound the scale/rotation).
            _scaleTween?.Kill(true);
            _rotationTween?.Kill(true);

            _scaleTween = pulseTarget
                .DOPunchScale(Vector3.one * scalePunch, scaleDuration, vibrato: 1, elasticity: scaleElasticity)
                .SetUpdate(true);

            if (rotationDegrees > 0.01f)
            {
                float sign = (alternateRotation && _rotateFlip) ? -1f : 1f;
                _rotateFlip = !_rotateFlip;
                _rotationTween = pulseTarget
                    .DOPunchRotation(new Vector3(0f, 0f, rotationDegrees * sign), rotationDuration, vibrato: 1, elasticity: 0.5f)
                    .SetUpdate(true);
            }
        }

        private void TriggerBurst()
        {
            if (burstRing == null) return;

            _burstScaleTween?.Kill();
            _burstColorTween?.Kill();

            // Resolve burst tint with optional element multiplication.
            Color startColor = burstStartColor;
            Color endColor   = burstEndColor;
            if (tintBurstByElement)
            {
                EnsureSlotIndexResolved();
                var loadout = WeaponLoadoutRuntime.Instance;
                var instance = loadout != null ? loadout.GetSlot(_slotIndex) : null;
                if (instance != null && instance.IsValid && instance.element != ElementId.None)
                {
                    Color elementCol = instance.element.DisplayColor();
                    startColor = new Color(
                        burstStartColor.r * elementCol.r,
                        burstStartColor.g * elementCol.g,
                        burstStartColor.b * elementCol.b,
                        burstStartColor.a);
                    endColor = new Color(
                        burstEndColor.r * elementCol.r,
                        burstEndColor.g * elementCol.g,
                        burstEndColor.b * elementCol.b,
                        burstEndColor.a);
                }
            }

            burstRing.gameObject.SetActive(true);
            burstRing.transform.localScale = Vector3.one * burstStartScale;
            burstRing.color = startColor;

            _burstScaleTween = burstRing.transform
                .DOScale(burstEndScale, burstDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            _burstColorTween = burstRing
                .DOColor(endColor, burstDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() => { if (burstRing != null) burstRing.gameObject.SetActive(false); });
        }

        // ─── Fuse ring + spark (per-frame update) ────────────────────────

        private void Update()
        {
            if (fillRing == null && fuseSpark == null) return;

            // Hide everything when the run isn't actively running. The
            // conductor freezes during Loading / LevelUpPaused / Paused /
            // GameOver (it short-circuits its own Update on !IsRunning),
            // so WeaponFire events don't fire and any animation we'd play
            // here would be desync'd from the music. Hiding keeps the HUD
            // visually consistent with the paused game state.
            var runState = RunStateManager.Instance;
            bool isRunning = runState != null && runState.IsRunning;
            if (!isRunning)
            {
                HideFuseVisuals();
                return;
            }

            // Hide when the weapon has no target. WeaponFiring's
            // HandleSubdivision skips firing on no-target frames, so no
            // WeaponFire events arrive and any animation we'd run here
            // would be anticipating fires that aren't going to happen.
            // Updated via MusicEvent.WeaponAimChanged broadcasts.
            if (!_hasTarget)
            {
                HideFuseVisuals();
                return;
            }

            // Detect interval changes from any source (level-up, BPM/Dex,
            // weapon swap, evolution, rarity change). GetFireRateForLevel
            // reads the current BPM internally so polling each frame
            // catches all four. If the interval changed, force a clean
            // re-anticipation with the new rate.
            float oldInterval = _fireInterval;
            UpdateFireInterval();
            bool intervalChanged = _hasFiredOnce && Mathf.Abs(_fireInterval - oldInterval) > 0.001f;
            if (intervalChanged)
            {
                _hasFiredOnce = false;
                if (verbose)
                    Debug.Log($"[WeaponSlotBeatPulse] Slot {_slotIndex} interval changed {oldInterval:F2}s → {_fireInterval:F2}s — re-anticipating.");
            }

            // Pre-first-fire (or post-reset): start a clean anticipation cycle.
            // The fuse will fill smoothly from 0 → 100% over the time until
            // the next grid-locked fire (HYBRID mode), then snap to NORMAL
            // cycle after the first real WeaponFire arrives.
            if (!_hasFiredOnce)
            {
                if (!TryAnticipateFirstFire())
                {
                    // Couldn't resolve a weapon yet — hide and try again
                    // next frame. Common during the first frame or two
                    // after Running entry while the loadout populates.
                    HideFuseVisuals();
                    return;
                }
                // Fall through — _lastFireTime is now `Time.time` so
                // elapsed = 0 on this frame and phase = 0. The fuse
                // appears at 0% and starts filling smoothly.
            }

            float elapsed = Time.time - _lastFireTime;
            if (elapsed < 0f || _currentCycleDuration < 0.001f)
            {
                // Defensive — should not happen with the scaled-time math,
                // but if a system somehow back-dates _lastFireTime into the
                // future, hide rather than show a broken value.
                HideFuseVisuals();
                return;
            }

            // Anticipation window is open: show + animate.
            ShowFuseVisuals();

            float phase = Mathf.Clamp01(elapsed / _currentCycleDuration);
            if (fillRing != null) fillRing.fillAmount = phase;
            if (fuseSpark != null) UpdateFuseSparkPosition(phase);
        }

        private void ShowFuseVisuals()
        {
            if (fillRing != null && !fillRing.gameObject.activeSelf)
                fillRing.gameObject.SetActive(true);
            ShowFuseSpark(true);
        }

        private void HideFuseVisuals()
        {
            if (fillRing != null && fillRing.gameObject.activeSelf)
                fillRing.gameObject.SetActive(false);
            ShowFuseSpark(false);
        }

        /// <summary>
        /// Start a HYBRID anticipation cycle: the fuse will fill smoothly
        /// from 0% → 100% over <see cref="_currentCycleDuration"/>, which
        /// is set to the actual time until the next grid-locked fire
        /// (NOT the normal fire interval). After the first real WeaponFire
        /// event arrives, the cycle switches to NORMAL (duration = fire
        /// interval, perfectly aligned with the grid).
        ///
        /// Hybrid mode for the first cycle solves two visual problems:
        ///   • A simple "phase = elapsed / fireInterval" with lastFireTime
        ///     = now would let the first shot fire mid-fill (snap-fire
        ///     looks early).
        ///   • Back-dating lastFireTime so phase starts partway feels
        ///     like the fuse "appeared half-burnt out of nowhere" — the
        ///     symptom you reported for the starting weapon.
        /// Hybrid: 0% → 100% in exactly the right duration → clean.
        ///
        /// Returns false if we can't read the loadout yet (typically only
        /// during the first frame or two after scene start / Running entry).
        /// </summary>
        private bool TryAnticipateFirstFire()
        {
            EnsureSlotIndexResolved();

            var loadout = WeaponLoadoutRuntime.Instance;
            var instance = loadout != null ? loadout.GetSlot(_slotIndex) : null;
            if (instance == null || !instance.IsValid || instance.weaponData == null)
                return false;

            // Refresh the interval (in case it changed). Also caches it
            // for the "next interval after first fire" use.
            UpdateFireInterval();

            // Find the actual time until the next grid-locked fire. Falls
            // back to fireInterval if the conductor isn't ready.
            float timeUntilFirst;
            var conductor = MusicConductor.Instance;
            if (conductor != null)
            {
                timeUntilFirst = PredictTimeUntilNextFire(conductor, instance);
            }
            else
            {
                timeUntilFirst = _fireInterval;
            }

            _lastFireTime         = Time.time;
            _currentCycleDuration = Mathf.Max(0.05f, timeUntilFirst); // floor avoids div-by-zero / instant fill
            _hasFiredOnce         = true;

            if (verbose)
                Debug.Log($"[WeaponSlotBeatPulse] Slot {_slotIndex} hybrid anticipation: fill over {_currentCycleDuration:F2}s (normal interval={_fireInterval:F2}s).");
            return true;
        }

        /// <summary>
        /// Inspect the weapon's grid-locked fire pattern + the conductor's
        /// current subdivision, return seconds until the NEXT fire cell.
        /// Includes the partial seconds remaining in the current subdivision
        /// for sub-frame accuracy.
        /// </summary>
        private float PredictTimeUntilNextFire(MusicConductor conductor, WeaponInstanceData instance)
        {
            int subsPerBeat = conductor.SubdivisionsPerBeat;
            int totalSubs   = instance.weaponData.GetTotalSubdivisions(subsPerBeat);
            if (totalSubs <= 0) return _fireInterval;

            int[] fireCells = instance.weaponData.GetFireCellsForLevel(instance.level, totalSubs);
            if (fireCells == null || fireCells.Length == 0) return _fireInterval;

            int currentSubInBar = ((conductor.CurrentSubdivision % totalSubs) + totalSubs) % totalSubs;

            // Walk the fire cells to find the smallest positive delta from
            // currentSubInBar. Wrap around the bar if no cell lies after us.
            int subsUntilFire = totalSubs; // worst case = exactly one bar
            for (int i = 0; i < fireCells.Length; i++)
            {
                int delta = fireCells[i] - currentSubInBar;
                if (delta <= 0) delta += totalSubs;
                if (delta < subsUntilFire) subsUntilFire = delta;
            }

            // Account for the partial sub we're already through.
            // TimeUntilNextSubdivision = time until the END of the current
            // subdivision; after that we wait (subsUntilFire - 1) more whole
            // subdivisions until the fire cell.
            float secondsUntilNextSub = conductor.TimeUntilNextSubdivision();
            float result = secondsUntilNextSub + (subsUntilFire - 1) * conductor.SecondsPerSubdivision;
            return Mathf.Max(0.01f, result);
        }

        /// <summary>
        /// Place the fuse spark on the perimeter of the fill ring at the
        /// angle corresponding to the current fill phase. Reads the fill
        /// ring's RectTransform size to find the bounds, and its
        /// fillOrigin + fillClockwise to find the start angle + direction.
        ///
        /// Path shape is controlled by trackShape:
        ///   • Circle:    spark walks on the inscribed circle (radius =
        ///                min(width, height)/2). Correct for circular slots.
        ///   • Rectangle: spark walks along the fillRing's RECT perimeter
        ///                (handles non-square dimensions too). Correct for
        ///                square/rectangular slots — hugs corners instead
        ///                of cutting inward at diagonals.
        /// </summary>
        private void UpdateFuseSparkPosition(float phase)
        {
            if (fillRing == null) return;

            var fillRect = fillRing.rectTransform;
            float ax = fillRect.rect.width  * 0.5f; // half-width
            float ay = fillRect.rect.height * 0.5f; // half-height

            // Start-angle interpretation: Unity's Image.Fill360 origin is an
            // enum (Bottom/Right/Top/Left). The visible filled wedge sweeps
            // from that origin in either CW or CCW direction by fillAmount.
            // We mirror that math here so the spark sits at the END of the
            // filled wedge (the burning tip of the fuse).
            float startDeg = FillOriginToStartDegrees((Image.Origin360)fillRing.fillOrigin);
            float sweep = phase * 360f * (fillRing.fillClockwise ? -1f : 1f);
            float angleDeg = startDeg + sweep;
            float angleRad = angleDeg * Mathf.Deg2Rad;

            float c = Mathf.Cos(angleRad);
            float s = Mathf.Sin(angleRad);

            Vector2 pos;
            if (trackShape == FuseTrackShape.Rectangle)
            {
                // Project the unit-direction ray onto the rectangle's
                // perimeter. We need the smallest positive t such that
                // the ray (t·cos, t·sin) hits an edge:
                //   x = ±ax → t = ax / |cos|
                //   y = ±ay → t = ay / |sin|
                // The min of those two is the perimeter hit.
                float absC = Mathf.Abs(c);
                float absS = Mathf.Abs(s);
                float tx = (absC > 0.0001f) ? ax / absC : float.MaxValue;
                float ty = (absS > 0.0001f) ? ay / absS : float.MaxValue;
                float t  = Mathf.Min(tx, ty);
                pos = new Vector2(c * t, s * t);
            }
            else
            {
                float radius = Mathf.Min(ax, ay);
                pos = new Vector2(c, s) * radius;
            }

            fuseSpark.anchoredPosition = pos;

            if (rotateFuseSpark)
            {
                // Rotate the spark to point along the direction of travel.
                // For the circle path this IS the tangent (perpendicular to
                // the radius). For the rectangle path it's an approximation
                // (the actual tangent jumps 90° at each corner), but the
                // angular form reads smoothly across the perimeter without
                // snap-rotations at corners. If you want exact per-side
                // rotation, set rotateFuseSpark = false and rotate the
                // spark sprite manually in art.
                float tangentDeg = angleDeg + (fillRing.fillClockwise ? -90f : 90f);
                fuseSpark.localRotation = Quaternion.Euler(0f, 0f, tangentDeg);
            }
        }

        private static float FillOriginToStartDegrees(Image.Origin360 origin)
        {
            // Unity screen-space convention: 0° = right, 90° = up.
            // Image.Origin360: Bottom = -90°, Right = 0°, Top = 90°, Left = 180°.
            switch (origin)
            {
                case Image.Origin360.Bottom: return -90f;
                case Image.Origin360.Right:  return 0f;
                case Image.Origin360.Top:    return 90f;
                case Image.Origin360.Left:   return 180f;
                default:                     return 90f; // Top fallback
            }
        }

        private void ShowFuseSpark(bool show)
        {
            if (fuseSpark == null) return;
            if (fuseSpark.gameObject.activeSelf != show)
            {
                fuseSpark.gameObject.SetActive(show);
            }
            if (fuseSparkParticles != null)
            {
                if (show && !fuseSparkParticles.isPlaying) fuseSparkParticles.Play();
                else if (!show && fuseSparkParticles.isPlaying) fuseSparkParticles.Stop();
            }
        }
    }
}

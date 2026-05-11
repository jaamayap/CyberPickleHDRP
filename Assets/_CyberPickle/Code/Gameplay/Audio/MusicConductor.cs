// File: Assets/_CyberPickle/Code/Gameplay/Audio/MusicConductor.cs
// Namespace: CyberPickle.Gameplay.Audio
//
// Single source of musical timing for the run. Owns the master tempo and
// emits OnBeat / OnBar / OnSubdivision events that gameplay systems
// subscribe to for beat-quantized firing, UI pulsing, particle sync, etc.
//
// Stage 0 (today): naive Time.time-based clock. Sufficient for visual
// pulsing, weapon-firing intent buffering, and validating the architecture.
// Frame-rate dips can drop subdivisions if a frame >1 grid unit; fine for
// stub work, not for shipping.
//
// Stage 2 (M9 Wwise): replace the Update loop with Wwise music callbacks
// (AK_MusicSyncBeat / AK_MusicSyncGrid). Sample-accurate to the audio
// thread. Public API stays identical — subscribers don't need to know
// which clock is driving the events.
//
// Scene-bound: belongs in Game.unity. Other scenes don't need it; menus
// don't have a tempo concept yet (might add later for menu-music sync).

using System;
using UnityEngine;
using CyberPickle.Core.Management;
using CyberPickle.Gameplay.RunState;
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public class MusicConductor : Manager<MusicConductor>
    {
        // Scene-bound: dies with the Game scene. Same lifecycle as
        // RunStateManager / RunStatsTracker.
        protected override bool PersistAcrossScenes => false;

        [Header("Tempo")]
        [Tooltip("Current master tempo in beats per minute. Default 128 — French-electro / cyberpunk standard. See GDD §3.5.2. At runtime this value is OVERWRITTEN by RecomputeBpmFromDex() each time PlayerStats.Dexterity changes (unless enableDexterityToBpm is off).")]
        [SerializeField, Range(60f, 200f)] private float bpm = 128f;

        [Tooltip("Subdivisions per beat. 4 = 16th-note grid (fast weapons), 2 = 8th-note grid (medium), 1 = quarter (slow). Most weapons will quantize to 16ths.")]
        [SerializeField, Range(1, 8)] private int subdivisionsPerBeat = 4;

        [Header("Dexterity → BPM (anchor-based mapping)")]
        [Tooltip("When ON, BPM is recomputed live from PlayerStats.Dexterity each time stats change. The serialized bpm field above becomes the boot-time default — it's overwritten on the first stats event (typically RunStart). Turn OFF for manual tuning / scene testing without a player.")]
        [SerializeField] private bool enableDexterityToBpm = true;

        [Tooltip("BPM when Dexterity equals minDexterityAnchor. This is the SLOW end of the song-build arc — what the player hears at the start of a run with no Dex investments.")]
        [SerializeField, Range(30f, 120f)] private float bpmAtMinDex = 60f;

        [Tooltip("Dexterity value treated as the 'minimum expected'. Below this, BPM stays clamped at bpmAtMinDex. Default 10 matches BaseStats.Defaults.dexterity — the unmodified starting value.")]
        [SerializeField, Min(0f)] private float minDexterityAnchor = 10f;

        [Tooltip("BPM when Dexterity equals maxDexterityAnchor. This is the FAST end of the song-build arc — what a fully Dex-maxed build hears at end-of-run climax.")]
        [SerializeField, Range(120f, 240f)] private float bpmAtMaxDex = 180f;

        [Tooltip("Dexterity value treated as the 'maximum expected'. Above this, BPM stays clamped at bpmAtMaxDex. Tune to what you want a Dex-stacked build to reach — e.g. 30 if a player picking ~5 of the +10% Dex cards should hit max tempo. Higher values = harder to reach top BPM = more progression headroom.")]
        [SerializeField, Min(0.1f)] private float maxDexterityAnchor = 30f;

        [Header("Diagnostics")]
        [Tooltip("Log each beat / bar to the console. Off by default — chatty.")]
        [SerializeField] private bool verbose;

        [Tooltip("Mirror MusicEventBus.VerboseLogging — when ON, every Fire() call logs (RunStart, WeaponFire, EnemyDeath, PlayerHit, etc.). Useful for confirming producers are wired. WARNING: WeaponFire alone can spam 30+/sec in combat. Toggle off when not actively diagnosing.")]
        [SerializeField] private bool verboseEventBus;

        // ─── Public state ─────────────────────────────────────────────────

        public float BPM => bpm;
        public int   SubdivisionsPerBeat => subdivisionsPerBeat;

        /// <summary>Seconds per beat at the current BPM. Useful for tweens / wait times that need to match the grid.</summary>
        public float SecondsPerBeat => 60f / bpm;

        /// <summary>Seconds per subdivision. shortest grid unit.</summary>
        public float SecondsPerSubdivision => 60f / (bpm * subdivisionsPerBeat);

        /// <summary>Number of beats fired since the most recent RunStart. 0 before the first beat fires.</summary>
        public int CurrentBeat { get; private set; }

        /// <summary>Number of subdivisions fired since the most recent RunStart.</summary>
        public int CurrentSubdivision { get; private set; }

        /// <summary>Bar number (4 beats per bar in 4/4). 0-indexed.</summary>
        public int CurrentBar => CurrentBeat / 4;

        // ─── Public events ────────────────────────────────────────────────

        /// <summary>Fires every beat. Subscribe for quarter-note locked behavior (kicks, slow weapons).</summary>
        public event Action OnBeat;

        /// <summary>Fires every bar (every 4 beats in 4/4). Subscribe for phrase-level events (transitions, big swells).</summary>
        public event Action OnBar;

        /// <summary>Fires every subdivision (16th note by default). Highest-resolution grid for fast weapons + UI sync.</summary>
        public event Action OnSubdivision;

        // ─── Internal clock state ─────────────────────────────────────────

        private float _runStartTime;
        private int _lastSubdivisionFired = -1;
        private int _lastBeatFired = -1;
        private int _lastBarFired = -1;

        // Dex→BPM wiring. PlayerStats is a scene MonoBehaviour (not a
        // Manager<T> singleton), so we resolve it lazily on RunStart and
        // subscribe to its change event.
        private PlayerStats _playerStats;
        private bool _subscribedToPlayerStats;

        // ─── Manager lifecycle ────────────────────────────────────────────

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            // Apply inspector toggle to the static bus flag. Editor-friendly:
            // the user can flip the bool in the inspector mid-Play and changes
            // take effect immediately via OnValidate (below).
            MusicEventBus.VerboseLogging = verboseEventBus;
            // Subscribe to the bus for RunStart so we can reset our clock to
            // align beat 0 with the moment combat begins.
            MusicEventBus.OnEvent += HandleMusicEvent;

            // Also align with RunStateManager directly: TransitionTo(Running)
            // is the run's actual start, regardless of who fires it. Belt and
            // suspenders — the bus handles producer-driven RunStart while the
            // direct subscription handles RunStateManager initialization.
            if (RunStateManager.Instance != null)
            {
                RunStateManager.Instance.OnPhaseChanged += HandleRunPhaseChanged;
                if (RunStateManager.Instance.IsRunning) ResetClock();
            }
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            MusicEventBus.OnEvent -= HandleMusicEvent;
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.OnPhaseChanged -= HandleRunPhaseChanged;
            UnsubscribePlayerStats();
        }

        // ─── Event handlers ───────────────────────────────────────────────

        private void HandleMusicEvent(MusicEvent type, object _)
        {
            if (type == MusicEvent.RunStart)
            {
                ResetClock();
                // Player exists by RunStart (the bootstrap spawned it before
                // firing the event). Lazily wire PlayerStats here so the
                // conductor doesn't need any direct reference to the player.
                ResolveAndSubscribePlayerStats();
            }
        }

        private void HandleRunPhaseChanged(RunStatePhase phase)
        {
            // Reset on the run's first entry to Running; PRESERVE the clock
            // through LevelUpPaused / Paused so the music keeps phase across
            // the level-up screen (a card pick that lands "on the beat" must
            // pick the same beat that was about to play).
            if (phase == RunStatePhase.Running && CurrentSubdivision == 0)
            {
                ResetClock();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Apply inspector tweaks live during Play so the user can toggle
            // verbose logging mid-session without restarting.
            MusicEventBus.VerboseLogging = verboseEventBus;
        }
#endif

        private void ResetClock()
        {
            _runStartTime = Time.time;
            _lastSubdivisionFired = -1;
            _lastBeatFired = -1;
            _lastBarFired = -1;
            CurrentSubdivision = 0;
            CurrentBeat = 0;
            if (verbose) Debug.Log($"[MusicConductor] Clock reset. BPM={bpm}, subdivisions/beat={subdivisionsPerBeat}.");
        }

        // ─── Frame loop ───────────────────────────────────────────────────

        private void Update()
        {
            // Don't tick during paused phases — Time.time still advances but
            // the run is effectively frozen and music should pause with it.
            // (Stage 2 with Wwise will tick from the audio callback regardless,
            // since Wwise has its own clock; revisit then.)
            var rsm = RunStateManager.Instance;
            if (rsm != null && !rsm.IsRunning) return;

            float elapsed = Time.time - _runStartTime;
            int subdivision = Mathf.FloorToInt(elapsed / SecondsPerSubdivision);

            // Catch up across multiple subdivisions if a frame was long
            // enough to skip one. We fire each missed event once so weapon
            // patterns don't desync from the grid on hitches.
            while (_lastSubdivisionFired < subdivision)
            {
                _lastSubdivisionFired++;
                CurrentSubdivision = _lastSubdivisionFired;
                OnSubdivision?.Invoke();

                int beat = CurrentSubdivision / subdivisionsPerBeat;
                if (beat != _lastBeatFired)
                {
                    _lastBeatFired = beat;
                    CurrentBeat = beat;
                    if (verbose) Debug.Log($"[MusicConductor] Beat {beat}");
                    OnBeat?.Invoke();

                    int bar = beat / 4;
                    if (bar != _lastBarFired)
                    {
                        _lastBarFired = bar;
                        if (verbose) Debug.Log($"[MusicConductor] Bar {bar}");
                        OnBar?.Invoke();
                    }
                }
            }
        }

        // ─── Dexterity → BPM (PR G3) ──────────────────────────────────────

        /// <summary>
        /// Resolve <see cref="PlayerStats"/> via scene search and subscribe
        /// to its <see cref="PlayerStats.OnStatsChanged"/> event. Called on
        /// RunStart (when the player is guaranteed to exist). No-op if
        /// already subscribed or PlayerStats can't be found (direct-play in
        /// Game.unity without a character — fine, BPM stays at the inspector
        /// default).
        /// </summary>
        private void ResolveAndSubscribePlayerStats()
        {
            if (_subscribedToPlayerStats) return;
            _playerStats = FindFirstObjectByType<PlayerStats>();
            if (_playerStats == null) return;

            _playerStats.OnStatsChanged += HandlePlayerStatsChanged;
            _subscribedToPlayerStats = true;

            // Initial sync — the current Dex value sets the initial BPM.
            // Without this we'd miss the Initialize() event that fired
            // before we subscribed (RunStart sequence: player init → bus fire).
            RecomputeBpmFromDex();
        }

        private void UnsubscribePlayerStats()
        {
            if (_playerStats != null && _subscribedToPlayerStats)
                _playerStats.OnStatsChanged -= HandlePlayerStatsChanged;
            _playerStats = null;
            _subscribedToPlayerStats = false;
        }

        private void HandlePlayerStatsChanged(PlayerStatType type)
        {
            // Respond to Dexterity changes AND to the bulk-change sentinel
            // (PlayerStats fires OnStatsChanged(default) on Initialize and
            // bulk RemoveModifiersFromSource — default == MaxHealth == 0).
            // Both could mean Dex changed. The recompute is cheap, just
            // re-runs unnecessarily for non-Dex stat changes.
            if (type == PlayerStatType.Dexterity || type == default)
                RecomputeBpmFromDex();
        }

        /// <summary>
        /// Read current Dexterity from <see cref="PlayerStats"/>, map it to
        /// a BPM via linear interpolation between two designer-set anchors,
        /// and forward to <see cref="SetBpm"/> (which handles the clock rebase).
        ///
        /// Mapping:
        ///   • Dex ≤ minDexterityAnchor → bpmAtMinDex (slow / song-floor)
        ///   • Dex ≥ maxDexterityAnchor → bpmAtMaxDex (fast / song-ceiling)
        ///   • In between: linear interpolation
        ///
        /// <see cref="Mathf.InverseLerp"/> auto-clamps the [0..1] interpolant,
        /// so out-of-range Dex values are safe (and ride the clamp).
        /// </summary>
        private void RecomputeBpmFromDex()
        {
            if (!enableDexterityToBpm) return;
            if (_playerStats == null) return;

            float dex = _playerStats.Get(PlayerStatType.Dexterity);
            float t   = Mathf.InverseLerp(minDexterityAnchor, maxDexterityAnchor, dex);
            float newBpm = Mathf.Lerp(bpmAtMinDex, bpmAtMaxDex, t);
            SetBpm(newBpm);
        }

        /// <summary>
        /// Set the BPM at runtime and REBASE the run-start time so the
        /// current subdivision index is preserved under the new tempo.
        /// Without this rebase, a BPM jump causes Update()'s catch-up loop
        /// to fire (or skip) many subdivisions in one frame — weapons
        /// burst-fire or pause for a beat.
        ///
        /// Math: at the moment of change we're at
        /// <c>currentPosInSubdivs = elapsed / oldSecondsPerSubdiv</c>.
        /// Under new tempo we want the same position, so
        /// <c>newRunStartTime = Time.time - currentPosInSubdivs × newSecondsPerSubdiv</c>.
        /// Result: the next subdivision fires after exactly one full
        /// <c>newSecondsPerSubdiv</c>, with the fractional progress through
        /// the current subdivision preserved.
        ///
        /// Public so designers / debug tools can force a tempo for testing
        /// (the dex-driven path also goes through here).
        /// </summary>
        public void SetBpm(float newBpm)
        {
            if (newBpm <= 0f) return;
            if (Mathf.Approximately(newBpm, bpm)) return;

            // Preserve fractional progress through the current subdivision.
            float oldElapsed = Time.time - _runStartTime;
            float oldSecondsPerSubdiv = 60f / (bpm * Mathf.Max(1, subdivisionsPerBeat));
            float currentPosInSubdivs = oldSecondsPerSubdiv > 0f
                ? oldElapsed / oldSecondsPerSubdiv
                : 0f;

            float newSecondsPerSubdiv = 60f / (newBpm * Mathf.Max(1, subdivisionsPerBeat));
            _runStartTime = Time.time - currentPosInSubdivs * newSecondsPerSubdiv;

            float oldBpm = bpm;
            bpm = newBpm;

            if (verbose)
                Debug.Log($"[MusicConductor] BPM {oldBpm:F1} → {newBpm:F1} (clock rebased).");
        }

        // ─── Quantization helper (Day 1 baseline; extended in M7.3) ─────

        /// <summary>
        /// Returns the seconds-from-now of the next subdivision boundary.
        /// Caller can use this to delay firing until the grid lands. Stage 0
        /// implementation; Stage 2 routes through Wwise's tempo timeline.
        /// </summary>
        public float TimeUntilNextSubdivision()
        {
            float elapsed = Time.time - _runStartTime;
            float secs = SecondsPerSubdivision;
            float intoCurrent = elapsed % secs;
            return secs - intoCurrent;
        }
    }
}

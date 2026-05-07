// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerHealth.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Source of truth for the player's health state during a run. Lives on
// the player prefab alongside PlayerStats. Owns:
//   - CurrentHealth (mutable runtime value)
//   - MaxHealth (read-through to PlayerStats.Get(MaxHealth))
//   - IsAlive flag
//   - Health regen tick
//   - Invulnerability window after taking damage (i-frames)
//
// Public API:
//   TakeDamage(amount, attacker)  — applies Defense reduction + i-frame check
//   Heal(amount)
//   ResetToFull()                  — called by GameSceneBootstrap on run start
//                                    after PlayerStats is initialized
//   OnHealthChanged(current, max)  — fired on any HP change (HUD subscribes)
//   OnDamageTaken                  — fired when damage actually lands (hit
//                                    feedback: flash, sound, screen shake)
//   OnPlayerDied                   — fired once when CurrentHealth hits 0
//                                    (consumed by run-state / results screen)
//
// Damage formula (per GDD §2.4):
//   reduced = amount × 100 / (100 + Defense)
//
// Defense=0 → no reduction. Defense=100 → 50% reduction. Diminishing
// returns built into the formula — Defense=400 → 80% reduction (4× HP
// effective vs raw damage, not 4× damage taken). Standard ARPG curve.

using System;
using UnityEngine;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.Gameplay.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Damage Tuning")]
        [Tooltip("Seconds of invulnerability after taking damage. Prevents N overlapping enemies all dealing damage on the same frame. 0 = no i-frames (continuous DPS).")]
        [Min(0f)] public float invulnerabilityWindow = 0.4f;

        [Header("Diagnostics")]
        [Tooltip("Log each damage event + death to the console.")]
        public bool verbose = false;

        // ─── Runtime state ────────────────────────────────────────────────

        private PlayerStats _stats;
        private float _currentHealth;
        private float _invulnTimer;
        private bool _isAlive = true;
        private bool _hasInitialized;

        /// <summary>Current HP value. Always between 0 and MaxHealth.</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Maximum HP value. Read-through to PlayerStats.Get(MaxHealth) so it reflects all active modifiers.</summary>
        public float MaxHealth => _stats != null ? _stats.Get(PlayerStatType.MaxHealth) : 100f;

        /// <summary>True until CurrentHealth first reaches 0. Stays false after death — heals are ignored.</summary>
        public bool IsAlive => _isAlive;

        /// <summary>Currently in i-frames (recently took damage)? Damage during this window is ignored.</summary>
        public bool IsInvulnerable => _invulnTimer > 0f;

        // ─── Events ───────────────────────────────────────────────────────

        /// <summary>Fired on any HP change — damage, heal, regen, max change. (current, max).</summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>Fired specifically when damage is taken (passes i-frame check). Use for hit feedback (flash, screen shake, audio).</summary>
        public event Action OnDamageTaken;

        /// <summary>Fired exactly once when CurrentHealth first reaches 0. Run-state and results-screen subscribe here.</summary>
        public event Action OnPlayerDied;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            _stats = GetComponent<PlayerStats>();
        }

        private void OnEnable()
        {
            // PlayerStats may not be initialized yet (GameSceneBootstrap
            // calls Initialize after spawn). Don't snapshot HP here; wait
            // for ResetToFull() to be called explicitly.
            if (_stats != null)
                _stats.OnStatsChanged += HandleStatsChanged;
        }

        private void OnDisable()
        {
            if (_stats != null)
                _stats.OnStatsChanged -= HandleStatsChanged;
        }

        private void Update()
        {
            if (!_isAlive) return;

            float dt = Time.deltaTime;

            // Tick i-frames.
            if (_invulnTimer > 0f) _invulnTimer = Mathf.Max(0f, _invulnTimer - dt);

            // Apply HealthRegen stat — seconds-per-second healing.
            if (_stats != null)
            {
                float regenPerSec = _stats.Get(PlayerStatType.HealthRegen);
                float max = MaxHealth;
                if (regenPerSec > 0f && _currentHealth < max)
                {
                    float prev = _currentHealth;
                    _currentHealth = Mathf.Min(max, _currentHealth + regenPerSec * dt);
                    if (!Mathf.Approximately(prev, _currentHealth))
                        OnHealthChanged?.Invoke(_currentHealth, max);
                }
            }
        }

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Apply damage to the player. Reduced by Defense per GDD §2.4. Ignored
        /// if dead, in i-frames, or if amount is non-positive. Triggers
        /// OnPlayerDied if HP reaches 0 as a result of this hit.
        /// </summary>
        public void TakeDamage(float amount, GameObject attacker = null)
        {
            if (!_isAlive) return;
            if (amount <= 0f) return;
            if (_invulnTimer > 0f) return;

            // Defense reduction: reduced = amount × 100 / (100 + defense)
            float defense = _stats != null ? _stats.Get(PlayerStatType.Defense) : 0f;
            float reduced = amount * 100f / (100f + defense);

            _currentHealth = Mathf.Max(0f, _currentHealth - reduced);
            _invulnTimer = invulnerabilityWindow;

            if (verbose)
                Debug.Log($"[PlayerHealth] -{reduced:F1} HP (raw {amount:F1}, def {defense:F0}) | {_currentHealth:F1}/{MaxHealth:F1}{(attacker != null ? $" from {attacker.name}" : "")}");

            OnDamageTaken?.Invoke();
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);

            // Broadcast to the audio bus. Damage feedback systems and the
            // music conductor (low-HP heartbeat, music ducking) listen here.
            MusicEventBus.Fire(MusicEvent.PlayerHit, reduced);

            if (_currentHealth <= 0f)
            {
                _isAlive = false;
                if (verbose) Debug.Log("[PlayerHealth] Player died.");
                OnPlayerDied?.Invoke();
                // RunEnd is also fired by RunStateManager when phase becomes
                // GameOver; firing it here too means death-music can react
                // immediately, before the run-state coroutine cycles.
            }
        }

        /// <summary>Heal the player. Capped at MaxHealth. Ignored if dead.</summary>
        public void Heal(float amount)
        {
            if (!_isAlive || amount <= 0f) return;
            float prev = _currentHealth;
            _currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
            if (!Mathf.Approximately(prev, _currentHealth))
            {
                OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
                // Heal events drive subtle audio cues (lifesteal sub-bass,
                // regen high-frequency shimmer per GDD §3.12.6).
                MusicEventBus.Fire(MusicEvent.PlayerHealed, _currentHealth - prev);
            }
        }

        /// <summary>
        /// Reset the player to full HP and alive state. Called by
        /// GameSceneBootstrap after PlayerStats.Initialize so MaxHealth
        /// reflects the spawned character's BaseStats. Also used by
        /// retry / respawn flows.
        /// </summary>
        public void ResetToFull()
        {
            _currentHealth = MaxHealth;
            _invulnTimer = 0f;
            _isAlive = true;
            _hasInitialized = true;
            OnHealthChanged?.Invoke(_currentHealth, MaxHealth);
        }

        // ─── Internal ─────────────────────────────────────────────────────

        private void HandleStatsChanged(PlayerStatType _)
        {
            // If MaxHealth changed (e.g., armor equipped, skill applied),
            // clamp current to the new max but don't refill. Players don't
            // get free heals from gaining MaxHealth mid-run.
            float max = MaxHealth;
            bool changed = _currentHealth > max;
            if (changed) _currentHealth = max;

            // First-ever stats change is the run-start initialize. Set HP
            // to full IF ResetToFull hasn't already been called.
            if (!_hasInitialized)
            {
                _currentHealth = max;
                _hasInitialized = true;
                changed = true;
            }

            if (changed) OnHealthChanged?.Invoke(_currentHealth, max);
        }
    }
}

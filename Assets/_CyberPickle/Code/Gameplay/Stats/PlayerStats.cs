// File: Assets/_CyberPickle/Code/Gameplay/Stats/PlayerStats.cs
// Namespace: CyberPickle.Gameplay.Stats
//
// SINGLE SOURCE OF TRUTH for the player's effective stats during a run.
//
// Owns:
//   - The player's BaseStats (initialized from CharacterData on run start)
//   - A list of StatModifiers (skills, equipment, implants, run upgrades, temp effects)
//   - A cache of effective stat values (recomputed lazily on next Get when dirty)
//
// Public API:
//   Get(type)                           — read effective value (cheap, cached)
//   Initialize(baseStats)               — set base + clear modifiers (run start)
//   AddModifier(modifier)               — apply a new modifier (equip / level-up / etc.)
//   RemoveModifiersFromSource(sourceId) — remove all from one source (unequip / etc.)
//   OnStatsChanged event                — subscribe to be notified of changes (HUD)
//
// Performance: Get is a single array index (sub-nanosecond). Recompute
// is O(modifiers + stats) — at most ~64 + 14 ops, runs only when a
// modifier is added/removed and Get is subsequently called.
//
// PlayerStatsBridge mirrors the cached values to a PlayerStatsData ECS
// singleton each frame for Burst-side reads. Bridge writes are skipped
// when nothing changed.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CyberPickle.Gameplay.Stats
{
    [DisallowMultipleComponent]
    public class PlayerStats : MonoBehaviour
    {
        [Header("Base Stats")]
        [Tooltip("Starting base values. Initialize(baseStats) overwrites this from CharacterData on run start. Inspector values are useful for testing in isolation (e.g., a standalone player prefab).")]
        [SerializeField] private BaseStats _base = BaseStats.Defaults;

        // ─── Modifier storage + cache ─────────────────────────────────────
        private readonly List<StatModifier> _modifiers = new List<StatModifier>(64);
        private readonly float[] _cached = new float[PlayerStatTypeMeta.Count];
        private bool _dirty = true;

        // Pre-allocated scratch buffers for Recompute. Avoids per-call GC.
        private readonly float[] _scratchAddBase    = new float[PlayerStatTypeMeta.Count];
        private readonly float[] _scratchAddPercent = new float[PlayerStatTypeMeta.Count];
        private readonly float[] _scratchMultFinal  = new float[PlayerStatTypeMeta.Count];
        private readonly float[] _scratchOverride   = new float[PlayerStatTypeMeta.Count];
        private readonly bool[]  _scratchHasOverride = new bool[PlayerStatTypeMeta.Count];

        // ─── Events ───────────────────────────────────────────────────────

        /// <summary>
        /// Fires when one or more stats may have changed. Argument is the
        /// affected stat for single-stat changes; for bulk changes (Initialize,
        /// RemoveModifiersFromSource), argument is `default` (== MaxHealth).
        /// Subscribers should not rely on the argument being precise — they
        /// should refresh whatever stats they depend on.
        /// </summary>
        public event Action<PlayerStatType> OnStatsChanged;

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>Read the effective value of a stat. Recomputes if dirty.</summary>
        public float Get(PlayerStatType type)
        {
            if (_dirty) Recompute();
            return _cached[(int)type];
        }

        /// <summary>
        /// Replace base stats and clear all modifiers. Called at run start
        /// from CharacterData.baseStats by the player setup code.
        /// </summary>
        public void Initialize(BaseStats baseStats)
        {
            _base = baseStats;
            _modifiers.Clear();
            _dirty = true;
            OnStatsChanged?.Invoke(default);
        }

        /// <summary>Apply a new modifier. Marks cache dirty for next Get.</summary>
        public void AddModifier(StatModifier mod)
        {
            _modifiers.Add(mod);
            _dirty = true;
            OnStatsChanged?.Invoke(mod.type);
        }

        /// <summary>
        /// Remove all modifiers from a source. Returns count removed.
        /// O(n) over the modifier list. Idempotent — safe to call with
        /// a sourceId that has no active modifiers.
        /// </summary>
        public int RemoveModifiersFromSource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return 0;

            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].sourceId == sourceId)
                {
                    _modifiers.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0)
            {
                _dirty = true;
                OnStatsChanged?.Invoke(default);
            }
            return removed;
        }

        /// <summary>Number of active modifiers. For HUD / debug display.</summary>
        public int ModifierCount => _modifiers.Count;

        /// <summary>Read-only view of base (un-modified) stat values. For HUD comparison.</summary>
        public BaseStats Base => _base;

        /// <summary>Force-mark cache dirty. Call after externally mutating _base values.</summary>
        public void MarkDirty()
        {
            _dirty = true;
            OnStatsChanged?.Invoke(default);
        }

        // ─── Recompute pipeline ───────────────────────────────────────────

        private void Recompute()
        {
            int n = PlayerStatTypeMeta.Count;

            // Reset scratch buffers.
            for (int i = 0; i < n; i++)
            {
                _scratchAddBase[i]      = 0f;
                _scratchAddPercent[i]   = 0f;
                _scratchMultFinal[i]    = 1f;
                _scratchOverride[i]     = 0f;
                _scratchHasOverride[i]  = false;
            }

            // Single pass through modifiers — bucket each by stat type + kind.
            int modCount = _modifiers.Count;
            for (int i = 0; i < modCount; i++)
            {
                var m = _modifiers[i];
                int idx = (int)m.type;
                switch (m.kind)
                {
                    case ModifierKind.AddBase:
                        _scratchAddBase[idx] += m.value;
                        break;
                    case ModifierKind.AddPercent:
                        _scratchAddPercent[idx] += m.value;
                        break;
                    case ModifierKind.MultFinal:
                        _scratchMultFinal[idx] *= m.value;
                        break;
                    case ModifierKind.Override:
                        _scratchOverride[idx]    = m.value;
                        _scratchHasOverride[idx] = true;
                        break;
                }
            }

            // Resolve each stat's effective value.
            for (int i = 0; i < n; i++)
            {
                if (_scratchHasOverride[i])
                {
                    _cached[i] = _scratchOverride[i];
                }
                else
                {
                    float baseVal = _base.Get((PlayerStatType)i);
                    float withAddBase = baseVal + _scratchAddBase[i];
                    float withPercent = withAddBase * (1f + _scratchAddPercent[i]);
                    float final      = withPercent * _scratchMultFinal[i];
                    _cached[i] = final;
                }
            }

            _dirty = false;
        }
    }
}

// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponLoadoutRuntime.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Scene-bound Manager<T> that owns the player's per-run loadout. The
// loadout is a fixed-size array of LoadoutAxis records — each axis pairs
// one weapon slot + one power-up slot, sharing an element identity.
//
// Default config: 4 axes (N / E / S / W). Configurable up to 8 (adds
// diagonals NE / SE / SW / NW). The axis count is decided once at run
// start and stays fixed for the duration of the run.
//
// Source of truth for:
//   - Which weapons + power-ups are equipped right now (per axis)
//   - Each weapon's Level (1..5 + Evolved) and Rarity (Common..Legendary)
//   - Each power-up's Rarity (Common..Legendary) and rolled Element
//   - Element coupling: an axis's weapon inherits the element of the
//     power-up on the SAME axis (or ElementId.None if no power-up)
//   - Power-up stat boosts wired into PlayerStats as StatModifiers,
//     keyed on "powerup_<id>_axis<N>" sourceIds (clean removal)
//
// NOT the source of truth for:
//   - WeaponData / PowerUpData design (those are SOs)
//   - Damage numbers (those flow through PlayerStats × WeaponData × Rarity)
//   - PlayerStats themselves — we only feed it modifiers; it owns the cache
//
// Lifecycle:
//   - Scene-bound (PersistAcrossScenes => false). Created when the Game
//     scene loads, destroyed when it unloads.
//   - Subscribes to MusicEventBus.OnEvent (process-global static, no
//     spawn-timing concerns). Currently nothing acted-on; the RunStart
//     auto-clear was removed in M7.4 (see HandleMusicEvent comment below).
//
// Event semantics:
//   - C# events fire on every state change. Argument is the axis index.
//   - MusicEventBus events fire alongside for cross-cutting consumers
//     (analytics, future Wwise integration).
//   - Element coupling: when a power-up is added/removed, the axis's
//     weapon's element is updated and MusicEvent.WeaponElementChanged
//     fires for the same axis index.
//
// Threading: main-thread only. Never call mutation methods from Burst.
//
// Cross-references:
//   - LoadoutAxis (per-axis pair record)
//   - WeaponInstanceData / PowerUpInstanceData (the inner shapes)
//   - RarityRollService (rolls initial rarity on add)
//   - PlayerStats (stat-modifier sink for power-up bonuses)
//   - economy_design_v1.md §7 (slot-count rationale)
//   - weapon_rarity_v1.md §7 (RTPC binding spec)

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.Core.Management;
using CyberPickle.Core.Services;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Stats;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    [DisallowMultipleComponent]
    public class WeaponLoadoutRuntime : Manager<WeaponLoadoutRuntime>
    {
        /// <summary>
        /// Default number of axes (N / E / S / W). Configurable per-run via
        /// <see cref="SetAxisCount"/> for an 8-axis variant (diagonals).
        /// </summary>
        public const int DefaultAxisCount = 4;

        /// <summary>Hard upper bound on axes — UI layouts assume ≤ 8.</summary>
        public const int MaxAxisCount = 8;

        /// <summary>
        /// Back-compat alias for the legacy <c>MaxSlots</c> constant. Some
        /// HUD code still references this — kept as DefaultAxisCount so
        /// existing inspector-array sizes match.
        /// </summary>
        public const int MaxSlots = DefaultAxisCount;

        [Header("Diagnostics")]
        [Tooltip("Log loadout changes to the console.")]
        public bool verbose = false;

        // Scene-bound. Loadout is run-scoped; persisting would carry stale
        // data into the next run.
        protected override bool PersistAcrossScenes => false;

        // ─── State ────────────────────────────────────────────────────────

        private LoadoutAxis[] _axes;
        private int _axisCount = DefaultAxisCount;

        /// <summary>Read-only view of all axes (always full length, may contain empty axes).</summary>
        public IReadOnlyList<LoadoutAxis> Axes
        {
            get { EnsureInitialized(); return _axes; }
        }

        /// <summary>Number of axes (4 default, configurable up to 8).</summary>
        public int AxisCount => _axisCount;

        /// <summary>Number of weapons currently equipped across all axes.</summary>
        public int WeaponCount
        {
            get
            {
                EnsureInitialized();
                int n = 0;
                for (int i = 0; i < _axisCount; i++) if (_axes[i].HasWeapon) n++;
                return n;
            }
        }

        /// <summary>Number of power-ups currently equipped across all axes.</summary>
        public int PowerUpCount
        {
            get
            {
                EnsureInitialized();
                int n = 0;
                for (int i = 0; i < _axisCount; i++) if (_axes[i].HasPowerUp) n++;
                return n;
            }
        }

        /// <summary>True when every axis already has a weapon (no more new-weapon cards).</summary>
        public bool AreWeaponSlotsFull => WeaponCount >= _axisCount;

        /// <summary>True when every axis already has a power-up (no more new-power-up cards).</summary>
        public bool ArePowerUpSlotsFull => PowerUpCount >= _axisCount;

        /// <summary>Back-compat alias for legacy callers — true when weapon slots are full.</summary>
        public bool IsFull => AreWeaponSlotsFull;

        /// <summary>Back-compat alias for legacy callers — number of weapons equipped.</summary>
        public int Count => WeaponCount;

        /// <summary>
        /// Read-only view of equipped weapons (drops empty axes). Order
        /// matches axis index. Used by HUD components that iterate weapons
        /// in display order. Allocates on each call — fine for HUD refresh
        /// rates, avoid in hot per-frame paths.
        /// </summary>
        public IReadOnlyList<WeaponInstanceData> Slots
        {
            get
            {
                EnsureInitialized();
                var list = new List<WeaponInstanceData>(_axisCount);
                for (int i = 0; i < _axisCount; i++)
                    if (_axes[i].HasWeapon) list.Add(_axes[i].weapon);
                return list;
            }
        }

        // ─── Events (C#-side, granular) ───────────────────────────────────

        /// <summary>Fires after a new weapon enters an axis. Argument is the new instance.</summary>
        public event Action<WeaponInstanceData> OnWeaponAdded;

        /// <summary>Fires after a weapon's Level changes. Argument is the axis index.</summary>
        public event Action<int> OnWeaponLevelChanged;

        /// <summary>Fires after a weapon's Rarity changes. Argument is the axis index.</summary>
        public event Action<int> OnWeaponRarityChanged;

        /// <summary>Fires when a weapon transitions to its evolved form. Argument is the axis index.</summary>
        public event Action<int> OnWeaponEvolved;

        /// <summary>Fires when a weapon's Element changes (typically from power-up coupling). Argument is the axis index.</summary>
        public event Action<int> OnWeaponElementChanged;

        /// <summary>Fires after a new power-up enters an axis. Argument is the new instance.</summary>
        public event Action<PowerUpInstanceData> OnPowerUpAdded;

        /// <summary>Fires after a power-up is removed from an axis. Argument is the axis index.</summary>
        public event Action<int> OnPowerUpRemoved;

        /// <summary>Fires after a power-up's Level changes. Argument is the axis index.</summary>
        public event Action<int> OnPowerUpLevelChanged;

        /// <summary>Fires after a power-up's Rarity changes. Argument is the axis index.</summary>
        public event Action<int> OnPowerUpRarityChanged;

        /// <summary>Fires after the entire loadout is cleared (e.g., on scene unload).</summary>
        public event Action OnLoadoutCleared;

        // ─── Cached references ────────────────────────────────────────────

        // PlayerStats lives in the scene as a sibling of the Player root.
        // Cached lazily on first access — avoids ordering issues with
        // OnManagerEnabled vs player spawn.
        private PlayerStats _cachedPlayerStats;

        private PlayerStats GetPlayerStats()
        {
            if (_cachedPlayerStats != null) return _cachedPlayerStats;
            _cachedPlayerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            return _cachedPlayerStats;
        }

        // ─── Public API: axis access ─────────────────────────────────────

        /// <summary>Returns the axis at the given index, or null if out of range.</summary>
        public LoadoutAxis GetAxis(int axisIndex)
        {
            EnsureInitialized();
            if (axisIndex < 0 || axisIndex >= _axisCount) return null;
            return _axes[axisIndex];
        }

        /// <summary>
        /// Returns the WEAPON at the given axis, or null if the axis is out
        /// of range or has no weapon. Back-compat with legacy <c>GetSlot</c>
        /// callers (WeaponFiring, WeaponSlotsPanel, etc.).
        /// </summary>
        public WeaponInstanceData GetSlot(int axisIndex)
        {
            var axis = GetAxis(axisIndex);
            return axis != null ? axis.weapon : null;
        }

        /// <summary>Returns the POWER-UP at the given axis, or null if empty/out-of-range.</summary>
        public PowerUpInstanceData GetPowerUp(int axisIndex)
        {
            var axis = GetAxis(axisIndex);
            return axis != null ? axis.powerUp : null;
        }

        /// <summary>First axis whose weapon matches the given equipmentId, or null.</summary>
        public WeaponInstanceData FindByWeaponId(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            EnsureInitialized();
            for (int i = 0; i < _axisCount; i++)
            {
                var w = _axes[i].weapon;
                if (w != null && w.IsValid && w.WeaponId == weaponId) return w;
            }
            return null;
        }

        /// <summary>First axis whose weapon matches the given <see cref="WeaponData"/>, or null.</summary>
        public WeaponInstanceData FindByWeaponData(WeaponData weapon)
        {
            if (weapon == null) return null;
            EnsureInitialized();
            for (int i = 0; i < _axisCount; i++)
            {
                var w = _axes[i].weapon;
                if (w != null && w.weaponData == weapon) return w;
            }
            return null;
        }

        /// <summary>First axis whose power-up matches the given <see cref="PowerUpData"/>, or null.</summary>
        public PowerUpInstanceData FindByPowerUpData(PowerUpData powerUp)
        {
            if (powerUp == null) return null;
            EnsureInitialized();
            for (int i = 0; i < _axisCount; i++)
            {
                var p = _axes[i].powerUp;
                if (p != null && p.powerUpData == powerUp) return p;
            }
            return null;
        }

        /// <summary>Index of the first axis whose weapon slot is empty, or -1 if all full.</summary>
        public int FirstEmptyWeaponAxis()
        {
            EnsureInitialized();
            for (int i = 0; i < _axisCount; i++)
                if (!_axes[i].HasWeapon) return i;
            return -1;
        }

        /// <summary>Index of the first axis whose power-up slot is empty, or -1 if all full.</summary>
        public int FirstEmptyPowerUpAxis()
        {
            EnsureInitialized();
            for (int i = 0; i < _axisCount; i++)
                if (!_axes[i].HasPowerUp) return i;
            return -1;
        }

        // ─── Public API: weapon mutation ─────────────────────────────────

        /// <summary>
        /// Add a weapon to the FIRST EMPTY weapon axis. Convenience for
        /// "give me whatever axis you've got" callers (PlayerLoadoutLoader,
        /// generic add-this-card flow). For explicit-axis-pick UI flows,
        /// use <see cref="TryAddWeaponAt"/>.
        ///
        /// Returns false if all weapon slots are full or <paramref name="weapon"/>
        /// is null. On success, fires OnWeaponAdded + MusicEvent.WeaponAdded.
        /// </summary>
        public bool TryAddWeapon(WeaponData weapon, Rarity initialRarity, out WeaponInstanceData added)
        {
            int axis = FirstEmptyWeaponAxis();
            if (axis < 0)
            {
                added = null;
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddWeapon ignored — all {_axisCount} weapon slots full.");
                return false;
            }
            return TryAddWeaponAt(axis, weapon, initialRarity, out added);
        }

        /// <summary>
        /// Add a weapon to a specific axis. Used by the cross-UI slot-picker
        /// flow where the player chose which axis to fill.
        ///
        /// Returns false if the axis is out of range, already has a weapon,
        /// or <paramref name="weapon"/> is null. On success, fires
        /// OnWeaponAdded + MusicEvent.WeaponAdded. The new weapon's element
        /// inherits from any power-up already on this axis (or
        /// ElementId.None if the axis has no power-up).
        /// </summary>
        public bool TryAddWeaponAt(int axisIndex, WeaponData weapon, Rarity initialRarity, out WeaponInstanceData added)
        {
            added = null;
            EnsureInitialized();
            if (axisIndex < 0 || axisIndex >= _axisCount)
            {
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddWeaponAt({axisIndex}) — out of range (0..{_axisCount - 1}).");
                return false;
            }
            if (weapon == null)
            {
                if (verbose) Debug.LogWarning("[WeaponLoadoutRuntime] TryAddWeaponAt — weaponData is null.");
                return false;
            }
            var axis = _axes[axisIndex];
            if (axis.HasWeapon)
            {
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddWeaponAt({axisIndex}) — axis already has weapon '{axis.weapon.WeaponId}'.");
                return false;
            }

            // Element comes from the axis's power-up (if any). Production
            // weapons roll Neutral by default — defaultElement on WeaponData
            // is test-only per chat 2026-05-11.
            var initialElement = axis.HasPowerUp ? axis.powerUp.element : ElementId.None;

            added = new WeaponInstanceData
            {
                weaponData = weapon,
                level      = 1,
                evolved    = false,
                rarity     = initialRarity,
                element    = initialElement,
                slotIndex  = axisIndex,
            };
            axis.weapon = added;

            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Added '{added.WeaponId}' to axis {axisIndex} at {initialRarity}, element {initialElement}.");

            OnWeaponAdded?.Invoke(added);
            MusicEventBus.Fire(MusicEvent.WeaponAdded, axisIndex);
            return true;
        }

        /// <summary>
        /// Convenience: add a weapon at the first empty axis with a
        /// Luck-modulated first-roll (per RarityRollService).
        /// </summary>
        public bool TryAddWeaponWithRoll(WeaponData weapon, float luck, out WeaponInstanceData added)
        {
            var rolled = RarityRollService.RollFirstAppearance(luck);
            return TryAddWeapon(weapon, rolled, out added);
        }

        /// <summary>
        /// Bump a weapon's Level by 1 (capped at 5). Pass the AXIS index.
        /// Returns false if the axis has no weapon, or the weapon is at L5
        /// (use <see cref="EvolveWeapon"/> for L5 → Evolved).
        /// </summary>
        public bool LevelUpWeapon(int axisIndex)
        {
            var w = GetSlot(axisIndex);
            if (w == null) return false;
            if (w.level >= 5) return false;

            w.level++;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Axis {axisIndex} weapon '{w.WeaponId}' L{w.level - 1} → L{w.level}.");

            OnWeaponLevelChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.WeaponLevelChanged, axisIndex);
            return true;
        }

        /// <summary>
        /// Bump a weapon's Rarity by N tiers (clamped at Legendary).
        /// Returns false if the axis has no weapon. Already-Legendary
        /// returns true with no event (no-op success).
        /// </summary>
        public bool UpgradeRarity(int axisIndex, int tiers = 1)
        {
            var w = GetSlot(axisIndex);
            if (w == null) return false;

            var oldRarity = w.rarity;
            var newRarity = RarityRollService.UpgradeBy(oldRarity, tiers);
            if (newRarity == oldRarity) return true;

            w.rarity = newRarity;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Axis {axisIndex} weapon '{w.WeaponId}' rarity {oldRarity} → {newRarity}.");

            OnWeaponRarityChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.WeaponRarityChanged, axisIndex);
            return true;
        }

        /// <summary>
        /// Bump weapon rarity DOWN by N tiers (clamped at Common). Used by
        /// <see cref="GambleRarity"/> on failure.
        /// </summary>
        public bool DowngradeRarity(int axisIndex, int tiers = 1)
        {
            var w = GetSlot(axisIndex);
            if (w == null) return false;

            var oldRarity = w.rarity;
            var newRarity = RarityRollService.DowngradeBy(oldRarity, tiers);
            if (newRarity == oldRarity) return true;

            w.rarity = newRarity;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Axis {axisIndex} weapon '{w.WeaponId}' rarity DOWN {oldRarity} → {newRarity}.");

            OnWeaponRarityChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.WeaponRarityChanged, axisIndex);
            return true;
        }

        /// <summary>Black Market gamble on a weapon's rarity.</summary>
        public Rarity GambleRarity(int axisIndex, float successChance = 0.6f, bool hasInsurance = false)
        {
            var w = GetSlot(axisIndex);
            if (w == null) return Rarity.Common;

            var oldRarity = w.rarity;
            var newRarity = RarityRollService.AttemptGamble(oldRarity, successChance, hasInsurance);
            if (newRarity == oldRarity) return newRarity;

            w.rarity = newRarity;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Axis {axisIndex} weapon GAMBLE {oldRarity} → {newRarity}.");

            OnWeaponRarityChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.WeaponRarityChanged, axisIndex);
            return newRarity;
        }

        /// <summary>
        /// Transition a weapon to its evolved form. Requires L5; earlier
        /// evolutions are not permitted by design.
        /// </summary>
        public bool EvolveWeapon(int axisIndex)
        {
            var w = GetSlot(axisIndex);
            if (w == null) return false;
            if (w.level < 5) return false;
            if (w.evolved) return false;

            w.evolved = true;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Axis {axisIndex} weapon '{w.WeaponId}' EVOLVED.");

            OnWeaponEvolved?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.WeaponEvolved, axisIndex);
            return true;
        }

        // ─── Public API: power-up mutation ───────────────────────────────

        /// <summary>
        /// Add a power-up to the FIRST EMPTY power-up axis. Convenience
        /// helper. For axis-explicit flows use <see cref="TryAddPowerUpAt"/>.
        /// </summary>
        public bool TryAddPowerUp(PowerUpData powerUp, ElementId rolledElement, Rarity initialRarity, out PowerUpInstanceData added)
        {
            int axis = FirstEmptyPowerUpAxis();
            if (axis < 0)
            {
                added = null;
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddPowerUp ignored — all {_axisCount} power-up slots full.");
                return false;
            }
            return TryAddPowerUpAt(axis, powerUp, rolledElement, initialRarity, out added);
        }

        /// <summary>
        /// Add a power-up to a specific axis. The power-up's stat bonus is
        /// applied GLOBALLY to PlayerStats; its element couples LOCALLY to
        /// the axis's weapon (if any), which fires WeaponElementChanged.
        ///
        /// Returns false if the axis is out of range, already has a power-up,
        /// or <paramref name="powerUp"/> is null.
        /// </summary>
        public bool TryAddPowerUpAt(int axisIndex, PowerUpData powerUp, ElementId rolledElement, Rarity initialRarity, out PowerUpInstanceData added)
        {
            added = null;
            EnsureInitialized();
            if (axisIndex < 0 || axisIndex >= _axisCount)
            {
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddPowerUpAt({axisIndex}) — out of range.");
                return false;
            }
            if (powerUp == null)
            {
                if (verbose) Debug.LogWarning("[WeaponLoadoutRuntime] TryAddPowerUpAt — powerUpData is null.");
                return false;
            }
            var axis = _axes[axisIndex];
            if (axis.HasPowerUp)
            {
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddPowerUpAt({axisIndex}) — axis already has power-up '{axis.powerUp.PowerUpId}'.");
                return false;
            }

            added = new PowerUpInstanceData
            {
                powerUpData = powerUp,
                level       = 1,
                rarity      = initialRarity,
                element     = rolledElement,
                axisIndex   = axisIndex,
            };
            axis.powerUp = added;

            // Apply global stat boost.
            ApplyPowerUpModifier(added);

            // Couple element onto the axis's weapon (if any).
            UpdateWeaponElementFromAxis(axisIndex);

            if (verbose) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Added power-up '{added.PowerUpId}' to axis {axisIndex} at {initialRarity}, element {rolledElement}.");

            OnPowerUpAdded?.Invoke(added);
            MusicEventBus.Fire(MusicEvent.PowerUpAdded, axisIndex);
            return true;
        }

        /// <summary>
        /// Remove the power-up from the given axis. Removes the global stat
        /// modifier and decouples the element from the axis's weapon.
        /// Returns false if the axis has no power-up.
        ///
        /// (Per current design, replacement is disallowed — see CLAUDE.md
        /// M8 design notes — but this method exists for future use and
        /// for clean teardown on run end.)
        /// </summary>
        public bool RemovePowerUp(int axisIndex)
        {
            var axis = GetAxis(axisIndex);
            if (axis == null || !axis.HasPowerUp) return false;

            var p = axis.powerUp;
            RemovePowerUpModifier(p);
            axis.powerUp = null;

            // Decouple element — axis's weapon falls back to Neutral.
            UpdateWeaponElementFromAxis(axisIndex);

            if (verbose) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Removed power-up '{p.PowerUpId}' from axis {axisIndex}.");

            OnPowerUpRemoved?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.PowerUpRemoved, axisIndex);
            return true;
        }

        /// <summary>Bump a power-up's Level by 1 (capped at 5).</summary>
        public bool LevelUpPowerUp(int axisIndex)
        {
            var p = GetPowerUp(axisIndex);
            if (p == null) return false;
            if (p.level >= 5) return false;

            p.level++;
            if (verbose) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Axis {axisIndex} power-up '{p.PowerUpId}' L{p.level - 1} → L{p.level}.");

            // Magnitude unchanged in M8 step 1 (level scaling not yet wired).
            // When step 2 lands and level adjusts magnitude, refresh the
            // modifier here.

            OnPowerUpLevelChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.PowerUpLevelChanged, axisIndex);
            return true;
        }

        /// <summary>
        /// Bump a power-up's Rarity by N tiers (clamped at Legendary).
        /// Refreshes the global stat modifier with the new magnitude.
        /// </summary>
        public bool UpgradePowerUpRarity(int axisIndex, int tiers = 1)
        {
            var p = GetPowerUp(axisIndex);
            if (p == null) return false;

            var oldRarity = p.rarity;
            var newRarity = RarityRollService.UpgradeBy(oldRarity, tiers);
            if (newRarity == oldRarity) return true;

            p.rarity = newRarity;

            // Refresh modifier — old removed, new applied.
            RemovePowerUpModifier(p);
            ApplyPowerUpModifier(p);

            if (verbose) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Axis {axisIndex} power-up '{p.PowerUpId}' rarity {oldRarity} → {newRarity}.");

            OnPowerUpRarityChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.PowerUpRarityChanged, axisIndex);
            return true;
        }

        // ─── Element coupling ────────────────────────────────────────────

        private void UpdateWeaponElementFromAxis(int axisIndex)
        {
            var axis = _axes[axisIndex];
            if (!axis.HasWeapon) return;

            var newElement = axis.HasPowerUp ? axis.powerUp.element : ElementId.None;
            if (axis.weapon.element == newElement) return;

            var oldElement = axis.weapon.element;
            axis.weapon.element = newElement;

            if (verbose) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Axis {axisIndex} weapon element {oldElement} → {newElement}.");

            OnWeaponElementChanged?.Invoke(axisIndex);
            MusicEventBus.Fire(MusicEvent.WeaponElementChanged, axisIndex);
        }

        // ─── Stat modifier wiring ────────────────────────────────────────

        private void ApplyPowerUpModifier(PowerUpInstanceData p)
        {
            var stats = GetPlayerStats();
            if (stats == null)
            {
                if (verbose) Debug.LogWarning("[WeaponLoadoutRuntime] No PlayerStats found in scene — power-up stat boost not applied.");
                return;
            }
            var mod = new StatModifier(
                type:     p.powerUpData.affectedStat,
                kind:     ModifierKind.AddPercent,
                value:    p.CurrentMagnitude, // decimal fraction
                sourceId: p.ModifierSourceId);
            stats.AddModifier(mod);

            if (verbose) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Applied modifier: {p.powerUpData.affectedStat} +{p.CurrentMagnitude * 100f:F0}% from {p.ModifierSourceId}.");
        }

        private void RemovePowerUpModifier(PowerUpInstanceData p)
        {
            var stats = GetPlayerStats();
            if (stats == null) return;
            int removed = stats.RemoveModifiersFromSource(p.ModifierSourceId);
            if (verbose && removed > 0) Debug.Log($"<color=magenta>[WeaponLoadoutRuntime]</color> Removed {removed} modifier(s) from {p.ModifierSourceId}.");
        }

        // ─── Run lifecycle ───────────────────────────────────────────────

        /// <summary>
        /// Set the axis count for this run. Must be called before any axis
        /// operations (typically by the run bootstrap). Clamped to
        /// [1, MaxAxisCount]. Re-allocates the internal array; any existing
        /// state is wiped (emits OnLoadoutCleared).
        /// </summary>
        public void SetAxisCount(int count)
        {
            count = Mathf.Clamp(count, 1, MaxAxisCount);
            if (_axes != null && _axisCount == count) return;

            ClearLoadout(); // tear down any existing modifiers first
            _axisCount = count;
            _axes = new LoadoutAxis[count];
            for (int i = 0; i < count; i++)
            {
                _axes[i] = new LoadoutAxis { axisIndex = i };
            }
            if (verbose) Debug.Log($"[WeaponLoadoutRuntime] Axis count set to {count}.");
        }

        /// <summary>
        /// Empty the loadout (all weapons, all power-ups, all stat modifiers).
        /// Safe to call at any point — idempotent.
        /// </summary>
        public void ClearLoadout()
        {
            EnsureInitialized();

            bool hadAnything = false;
            for (int i = 0; i < _axisCount; i++)
            {
                var axis = _axes[i];
                if (axis.HasPowerUp)
                {
                    RemovePowerUpModifier(axis.powerUp);
                    axis.powerUp = null;
                    hadAnything = true;
                }
                if (axis.HasWeapon)
                {
                    axis.weapon = null;
                    hadAnything = true;
                }
            }

            if (!hadAnything) return;

            if (verbose) Debug.Log("<color=cyan>[WeaponLoadoutRuntime]</color> Loadout cleared.");
            OnLoadoutCleared?.Invoke();
            MusicEventBus.Fire(MusicEvent.LoadoutCleared);
        }

        // ─── Lifecycle ───────────────────────────────────────────────────

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            EnsureInitialized();
            // Process-global static — no scene-spawn timing concerns.
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        protected override void OnManagerDisabled()
        {
            base.OnManagerDisabled();
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        protected override void OnManagerDestroyed()
        {
            base.OnManagerDestroyed();
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void EnsureInitialized()
        {
            if (_axes != null) return;
            _axes = new LoadoutAxis[_axisCount];
            for (int i = 0; i < _axisCount; i++)
            {
                _axes[i] = new LoadoutAxis { axisIndex = i };
            }
        }

        private void HandleMusicEvent(MusicEvent ev, object payload)
        {
            // 2026-05-10: still no auto-clear on RunStart — see M7.4 Day 3
            // notes (PlayerLoadoutLoader.LoadAndSpawn calls ClearLoadout()
            // explicitly at the top to handle retry-without-scene-reload).
            // The Manager<T> being scene-bound (PersistAcrossScenes=>false)
            // also ensures fresh state on scene load.
        }
    }
}

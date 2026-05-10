// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponLoadoutRuntime.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Scene-bound Manager<T> that owns the player's per-run weapon loadout.
// Holds up to 4 WeaponInstanceData records (1 starting weapon + up to 3
// drafted in-run, per economy_design_v1.md §7).
//
// Source of truth for:
//   - Which weapons are equipped right now (slot 0..3)
//   - Each equipped weapon's Level (1..5 + Evolved) and Rarity (Common..Legendary)
//
// NOT the source of truth for:
//   - WeaponData design (that's the ScriptableObject — each instance carries
//     a weaponId pointing back to it)
//   - Damage numbers (those flow through PlayerStats × WeaponData × Rarity
//     at projectile spawn time; see WeaponFiring + ProjectileCollisionSystem)
//
// Lifecycle:
//   - Scene-bound (PersistAcrossScenes => false). Created when the Game
//     scene loads, destroyed when it unloads.
//   - Subscribes to MusicEventBus.OnEvent at OnManagerEnabled. On
//     MusicEvent.RunStart, clears the loadout (fresh runs start empty —
//     pre-run starting-weapon population is the responsibility of the
//     run bootstrap, e.g., GameSceneBootstrap, calling TryAddWeapon
//     after RunStart fires).
//
// Event semantics (subscribers can wire either C# events or MusicEventBus):
//   - C# events for tight in-process consumers (HUD, music conductor).
//     Provide slot-level granularity.
//   - MusicEventBus events for cross-cutting consumers (analytics, future
//     Wwise integration). Same slot index passed as the payload.
//
// Threading: main-thread only. All mutation methods call into _slots List
// (not thread-safe). Burst-side weapon entities mirror state via the
// WeaponLevel / WeaponRarity ECS components — sync points are TBD until
// Phase 4 wiring lands.
//
// Cross-references:
//   - WeaponInstanceData (the data shape being managed)
//   - RarityRollService (rolls initial rarity on weapon add)
//   - economy_design_v1.md §7 (slot count rationale)
//   - weapon_rarity_v1.md §7 (RTPC binding spec)

using System;
using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.Core.Management;
using CyberPickle.Core.Services;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    [DisallowMultipleComponent]
    public class WeaponLoadoutRuntime : Manager<WeaponLoadoutRuntime>
    {
        /// <summary>
        /// Hard cap on equipped weapons in a single run: 1 starting + 3 drafted = 4.
        /// Per economy_design_v1.md §7 — kept small so every weapon matters.
        /// </summary>
        public const int MaxSlots = 4;

        [Header("Diagnostics")]
        [Tooltip("Log loadout changes to the console.")]
        public bool verbose = false;

        // Scene-bound — see RunStateManager / PerWeaponStatsTracker for the
        // same pattern. Loadout is run-scoped; persisting across scenes
        // would carry stale data into the next run.
        protected override bool PersistAcrossScenes => false;

        // ─── State ────────────────────────────────────────────────────────

        private readonly List<WeaponInstanceData> _slots = new List<WeaponInstanceData>(MaxSlots);

        /// <summary>Read-only view of equipped weapons. Order = slotIndex.</summary>
        public IReadOnlyList<WeaponInstanceData> Slots => _slots;

        /// <summary>Number of currently equipped weapons (0..MaxSlots).</summary>
        public int Count => _slots.Count;

        /// <summary>True when no more weapons can be added.</summary>
        public bool IsFull => _slots.Count >= MaxSlots;

        // ─── Events (C#-side, granular) ───────────────────────────────────

        /// <summary>Fires after a new weapon enters the loadout. Argument is the new instance.</summary>
        public event Action<WeaponInstanceData> OnWeaponAdded;

        /// <summary>Fires after a weapon's Level changes. Argument is the slot index.</summary>
        public event Action<int> OnWeaponLevelChanged;

        /// <summary>Fires after a weapon's Rarity changes. Argument is the slot index.</summary>
        public event Action<int> OnWeaponRarityChanged;

        /// <summary>Fires when a weapon transitions to its evolved form. Argument is the slot index.</summary>
        public event Action<int> OnWeaponEvolved;

        /// <summary>Fires after the loadout is cleared (e.g., on RunStart).</summary>
        public event Action OnLoadoutCleared;

        // ─── Public API ───────────────────────────────────────────────────

        /// <summary>
        /// Returns the WeaponInstanceData at the given slot, or null if the
        /// slot is empty / out of range.
        /// </summary>
        public WeaponInstanceData GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
            return _slots[slotIndex];
        }

        /// <summary>
        /// First slot whose weaponData has the given equipmentId, or null
        /// if not equipped. O(MaxSlots) — fine, list is tiny.
        /// </summary>
        public WeaponInstanceData FindByWeaponId(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].WeaponId == weaponId) return _slots[i];
            }
            return null;
        }

        /// <summary>
        /// First slot referencing the given <see cref="WeaponData"/>, or
        /// null if not equipped. Convenience for SO-driven callers.
        /// </summary>
        public WeaponInstanceData FindByWeaponData(WeaponData weapon)
        {
            if (weapon == null) return null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].weaponData == weapon) return _slots[i];
            }
            return null;
        }

        /// <summary>
        /// Add a weapon at the given initial Rarity. Used when:
        ///   - The starting weapon enters the loadout at RunStart (rarity
        ///     typically Common, or character-specific).
        ///   - An in-run weapon draft locks in a specific roll (caller
        ///     already rolled the rarity via RarityRollService).
        ///   - A boss-drop or fixed-rarity scripted reward is applied.
        ///
        /// Returns false if the loadout is full or <paramref name="weapon"/>
        /// is null. On success, fires OnWeaponAdded + MusicEvent.WeaponAdded.
        /// The instance's element is initialized from <c>weapon.defaultElement</c>
        /// (per procedural_music_reference.md §22.4).
        /// </summary>
        public bool TryAddWeapon(WeaponData weapon, Rarity initialRarity, out WeaponInstanceData added)
        {
            added = null;
            if (IsFull)
            {
                if (verbose) Debug.LogWarning($"[WeaponLoadoutRuntime] TryAddWeapon ignored — loadout full ({MaxSlots}/{MaxSlots}).");
                return false;
            }
            if (weapon == null)
            {
                if (verbose) Debug.LogWarning("[WeaponLoadoutRuntime] TryAddWeapon ignored — weaponData is null.");
                return false;
            }

            int newSlot = _slots.Count;
            added = new WeaponInstanceData
            {
                weaponData = weapon,
                level      = 1,
                evolved    = false,
                rarity     = initialRarity,
                element    = weapon.defaultElement,
                slotIndex  = newSlot,
            };
            _slots.Add(added);

            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Added '{added.WeaponId}' to slot {newSlot} at rarity {initialRarity}, element {added.element}.");

            OnWeaponAdded?.Invoke(added);
            MusicEventBus.Fire(MusicEvent.WeaponAdded, newSlot);
            return true;
        }

        /// <summary>
        /// Convenience: add a weapon with a Luck-modulated first-roll
        /// (per RarityRollService). Used for in-run drafts where the
        /// caller wants the standard roll rather than a fixed rarity.
        /// </summary>
        public bool TryAddWeaponWithRoll(WeaponData weapon, float luck, out WeaponInstanceData added)
        {
            var rolled = RarityRollService.RollFirstAppearance(luck);
            return TryAddWeapon(weapon, rolled, out added);
        }

        /// <summary>
        /// Bump a weapon's Level by 1 (capped at 5). Returns false if the
        /// slot is empty, out of range, or already at L5 unevolved (use
        /// <see cref="EvolveWeapon"/> for the L5 → Evolved transition).
        ///
        /// Fires OnWeaponLevelChanged + MusicEvent.WeaponLevelChanged.
        /// </summary>
        public bool LevelUpWeapon(int slotIndex)
        {
            var w = GetSlot(slotIndex);
            if (w == null) return false;
            if (w.level >= 5) return false;

            w.level++;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Slot {slotIndex} '{w.WeaponId}' L{w.level - 1} → L{w.level}.");

            OnWeaponLevelChanged?.Invoke(slotIndex);
            MusicEventBus.Fire(MusicEvent.WeaponLevelChanged, slotIndex);
            return true;
        }

        /// <summary>
        /// Bump a weapon's Rarity by N tiers (default 1, clamped at Legendary).
        /// Returns false if the slot is empty / out of range, or true if a
        /// change actually happened (already-Legendary returns true with no
        /// state change but no event — caller can re-check rarity to detect).
        ///
        /// Fires OnWeaponRarityChanged + MusicEvent.WeaponRarityChanged on
        /// successful change. Used by Rarity-up cards, Augment Console,
        /// Resonator pickups, Echo Compiler keystone.
        /// </summary>
        public bool UpgradeRarity(int slotIndex, int tiers = 1)
        {
            var w = GetSlot(slotIndex);
            if (w == null) return false;

            var oldRarity = w.rarity;
            var newRarity = RarityRollService.UpgradeBy(oldRarity, tiers);
            if (newRarity == oldRarity) return true; // already at cap; no-op success

            w.rarity = newRarity;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Slot {slotIndex} '{w.WeaponId}' rarity {oldRarity} → {newRarity}.");

            OnWeaponRarityChanged?.Invoke(slotIndex);
            MusicEventBus.Fire(MusicEvent.WeaponRarityChanged, slotIndex);
            return true;
        }

        /// <summary>
        /// Bump rarity DOWN by N tiers (clamped at Common). Used internally
        /// by <see cref="GambleRarity"/> on failure; rarely needed elsewhere
        /// since the design avoids visible negative numbers (CLAUDE.md
        /// design pillar 3). Public so Black Market UI can call directly
        /// when displaying gamble-failed flow.
        /// </summary>
        public bool DowngradeRarity(int slotIndex, int tiers = 1)
        {
            var w = GetSlot(slotIndex);
            if (w == null) return false;

            var oldRarity = w.rarity;
            var newRarity = RarityRollService.DowngradeBy(oldRarity, tiers);
            if (newRarity == oldRarity) return true;

            w.rarity = newRarity;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Slot {slotIndex} '{w.WeaponId}' rarity DOWN {oldRarity} → {newRarity}.");

            OnWeaponRarityChanged?.Invoke(slotIndex);
            MusicEventBus.Fire(MusicEvent.WeaponRarityChanged, slotIndex);
            return true;
        }

        /// <summary>
        /// Black Market gamble on a slot's rarity. Convenience wrapper
        /// around <see cref="RarityRollService.AttemptGamble"/> that
        /// applies the result to the loadout in one call.
        /// </summary>
        /// <returns>The rarity AFTER the gamble (so the UI can flash up/down).</returns>
        public Rarity GambleRarity(int slotIndex, float successChance = 0.6f, bool hasInsurance = false)
        {
            var w = GetSlot(slotIndex);
            if (w == null) return Rarity.Common;

            var oldRarity = w.rarity;
            var newRarity = RarityRollService.AttemptGamble(oldRarity, successChance, hasInsurance);
            if (newRarity == oldRarity) return newRarity;

            w.rarity = newRarity;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Slot {slotIndex} '{w.WeaponId}' GAMBLE {oldRarity} → {newRarity} (success={(newRarity > oldRarity)}, insurance={hasInsurance}).");

            OnWeaponRarityChanged?.Invoke(slotIndex);
            MusicEventBus.Fire(MusicEvent.WeaponRarityChanged, slotIndex);
            return newRarity;
        }

        /// <summary>
        /// Transition a weapon to its evolved form. Requires the weapon to
        /// be at L5 — earlier evolutions are not permitted by design (they
        /// would skip the pattern-complexity progression).
        ///
        /// Returns false if the slot is empty, the weapon isn't at L5, or
        /// it's already evolved. Fires OnWeaponEvolved + MusicEvent.WeaponEvolved.
        ///
        /// Note: trigger conditions (e.g., paired power-up acquired) are
        /// checked OUTSIDE this method by the caller. This method only
        /// applies the transition; it doesn't gate on prerequisites.
        /// </summary>
        public bool EvolveWeapon(int slotIndex)
        {
            var w = GetSlot(slotIndex);
            if (w == null) return false;
            if (w.level < 5) return false;
            if (w.evolved) return false;

            w.evolved = true;
            if (verbose) Debug.Log($"<color=cyan>[WeaponLoadoutRuntime]</color> Slot {slotIndex} '{w.WeaponId}' EVOLVED.");

            OnWeaponEvolved?.Invoke(slotIndex);
            MusicEventBus.Fire(MusicEvent.WeaponEvolved, slotIndex);
            return true;
        }

        /// <summary>
        /// Empty the loadout. Called automatically on MusicEvent.RunStart;
        /// can be called manually for retry / restart flows. Fires
        /// OnLoadoutCleared + MusicEvent.LoadoutCleared.
        /// </summary>
        public void ClearLoadout()
        {
            if (_slots.Count == 0) return;
            _slots.Clear();
            if (verbose) Debug.Log("<color=cyan>[WeaponLoadoutRuntime]</color> Loadout cleared.");

            OnLoadoutCleared?.Invoke();
            MusicEventBus.Fire(MusicEvent.LoadoutCleared);
        }

        // ─── Lifecycle ────────────────────────────────────────────────────

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            // Process-global static — no scene-spawn timing concerns. Same
            // pattern as LevelUpCoordinator (per CLAUDE.md foot-guns).
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
            // Defensive — Disable should have unsubscribed already, but a
            // belt-and-braces detach prevents leaks if Disable was skipped.
            MusicEventBus.OnEvent -= HandleMusicEvent;
        }

        private void HandleMusicEvent(MusicEvent ev, object payload)
        {
            // 2026-05-10: REMOVED auto-clear on RunStart.
            //
            // Why: PlayerLoadoutLoader.OnEnable populates the loadout when
            // GameSceneBootstrap activates the player components. Then the
            // bootstrap calls RunStateManager.TransitionTo(Running), which
            // fires MusicEvent.RunStart. If we cleared on RunStart, we'd
            // wipe the weapons that PlayerLoadoutLoader just added — the
            // HUD would then show an empty loadout despite the player
            // visually having weapons in their hand. (Real bug, debugged
            // via MCP introspection in M7.4 Day 3.)
            //
            // Caller responsibility: PlayerLoadoutLoader.LoadAndSpawn calls
            // ClearLoadout() at the top to handle retry-without-scene-reload.
            // The Manager<T> being scene-bound (PersistAcrossScenes=>false)
            // also ensures fresh state on scene load.
        }
    }
}

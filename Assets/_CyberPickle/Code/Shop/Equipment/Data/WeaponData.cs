// File: Assets/_CyberPickle/Code/Shop/Equipment/Data/WeaponData.cs
//
// Purpose: ScriptableObject design data for one weapon. Authored by
// designers; consumed at runtime by WeaponFiring (damage/fire-rate/
// muzzle-VFX), HitVfxApplier (hit-VFX scale), HUD tooltips, and the
// equipment-hub preview.
//
// Created: 2025-02-25
// Updated:
//   2026-05-10 — DUAL-AXIS REFACTOR
//     - Damage scaling moved to the Rarity axis (Common→Legendary).
//     - Fire-rate scaling moved to the LEVEL axis via authored rhythmic
//       patterns (active-cell density × BPM, where BPM is driven globally
//       by MusicConductor + Dexterity).
//     - Removed deprecated upgrade-multiplier fields and per-level helpers.
//   2026-05-11 — M9 PR G cleanup
//     - Removed legacy fields: weaponType, baseFireRate, baseBPM,
//       projectilePrefab, hitEffectPrefab, fireSound, hitSound.
//     - BPM moved out of WeaponData entirely — sourced from
//       MusicConductor.Instance.BPM (global musical tempo). Per-weapon
//       BPM was a design violation: two weapons share a song.
//     - ComputeBPM(dex) removed.
//     - GetFireRateForLevel(level) — dex parameter dropped (BPM is the
//       only downstream value, and BPM is now global).
//     - Visual prefabs moved out (M9 ElementVfxLibrary):
//       projectilePrefab → ProjectilePrefabSetupAuthoring buffer
//       hitEffectPrefab + flashes → ElementVfxLibrary entries
//
// ─── DUAL-AXIS NOTE (READ BEFORE EDITING) ─────────────────────────────────
//
// The CyberPickle weapon model is dual-axis:
//   - LEVEL  (1..5 + Evolved) → fire-rate via active-cell pattern density
//                                + musical pattern complexity
//   - RARITY (Common..Legendary) → damage scalar (×1.0..×4.0)
//                                + tier-bonus perk (per-weapon authored)
//
// Source of truth:
//   - LLM Knowledge Base/weapon_rarity_v1.md           (axis design)
//   - LLM Knowledge Base/procedural_music_reference.md §22 (pattern math)
//   - Assets/_CyberPickle/Code/Core/Rarity.cs          (rarity enum + scalars)
//   - Assets/_CyberPickle/Code/Gameplay/Audio/MusicConductor.cs (BPM source)

using UnityEngine;
using System;
using System.Collections.Generic;
using CyberPickle.Core;
using CyberPickle.Gameplay.Audio;

namespace CyberPickle.Shop.Equipment.Data
{
    /// <summary>
    /// Categorical attack-type tag, used for equipment-hub UI labeling
    /// (Projectile → "Projectile Speed" row; Beam → "Range" row; etc.).
    /// Not consumed at gameplay runtime.
    /// </summary>
    public enum WeaponAttackType
    {
        Projectile,
        Beam,
        Area,
        Melee
    }

    /// <summary>
    /// How a projectile moves between muzzle and impact.
    /// </summary>
    public enum ProjectileTrajectory : byte
    {
        /// <summary>Linear flight along the muzzle's forward vector at <c>baseProjectileSpeed</c>. Default for pistol / shotgun / sniper.</summary>
        Straight = 0,

        /// <summary>Ballistic parabolic arc that lands at the target after <c>flightBeats × 60 / BPM</c> seconds. Real gravity drives the arc — launch angle is steep at close range, shallow at far range, apex height depends only on flight time. Grenade launcher.</summary>
        Parabolic = 1,
    }

    /// <summary>
    /// How a weapon picks which enemy to aim at each re-target tick. Read by
    /// <c>WeaponTargeting.FindBestTarget</c>. Byte-typed for stable ordering
    /// in serialized assets — do not renumber.
    /// </summary>
    public enum TargetingStrategy : byte
    {
        /// <summary>Nearest enemy in range. Pistol / shotgun default — fastest to compute, snappiest in feel.</summary>
        Closest = 0,

        /// <summary>Lowest current HP. Useful for executing wounded enemies before they reach you.</summary>
        Weakest = 1,

        /// <summary>Highest current HP. Useful for prioritizing tanks / bosses over chaff.</summary>
        Strongest = 2,

        /// <summary>Enemy with the most OTHER enemies lined up behind them in a narrow cone (per <c>targetingConeHalfAngleDeg</c>). Sniper default — pierce shots chain naturally through the column. O(N²) — beat-throttled by WeaponTargeting.</summary>
        MostInLine = 3,

        /// <summary>Enemy with the most OTHER enemies within <c>baseAreaOfEffect</c> radius of them. Grenade default — the lob lands in a kill zone instead of singling out one target. O(N²) — beat-throttled by WeaponTargeting.</summary>
        DensestCluster = 4,
    }

    /// <summary>
    /// ScriptableObject that defines data for weapon equipment.
    /// </summary>
    [CreateAssetMenu(fileName = "Weapon", menuName = "CyberPickle/Equipment/WeaponData")]
    public class WeaponData : EquipmentData
    {
        // ─── Core combat stats ────────────────────────────────────────────

        [Header("Combat")]
        [Tooltip("Categorical tag used for equipment-hub stat-row labels (Projectile / Beam / Area / Melee). Not consumed at gameplay runtime.")]
        public WeaponAttackType attackType = WeaponAttackType.Projectile;

        [Tooltip("Base damage per shot. Final damage = baseDamage × Rarity.DamageMultiplier() × (1 + Power × 0.01) × critMul (per weapon_rarity_v1.md §2). Rarity scales damage; Level does not.")]
        [Min(0.1f)] public float baseDamage = 10f;

        [Tooltip("Projectile travel speed (world units/sec). Per-weapon constant. Heavier weapons (sniper) tend higher; mortar-style lobs (grenade) lower.")]
        [Min(0.1f)] public float baseProjectileSpeed = 10f;

        [Tooltip("Area-of-effect radius (world units). Used by HitVfxApplier for hit-VFX size scaling (when hitVfxScalesWithAreaOfEffect is true) and by area-damage queries (PR E grenade).")]
        [Min(0.1f)] public float baseAreaOfEffect = 1f;

        [Tooltip("Pierce count — how many enemies a projectile can pass through. 0 = stop on first hit. Sniper uses 1+ scaled by level/rarity (PR D).")]
        [Min(0)] public int basePierceCount = 0;

        [Tooltip("Maximum targeting range (world units). Used by WeaponTargeting to ignore enemies outside this radius. Pistols ~12, shotgun ~8, sniper ~25, grenade ~18.")]
        [Min(1f)] public float baseRange = 15f;

        [Header("Targeting")]
        [Tooltip("How the weapon picks which enemy to aim at each re-target tick. Closest is the default. MostInLine is the sniper's bread and butter (chains pierce through a column). DensestCluster lands grenades on tightly packed groups.")]
        public TargetingStrategy targetingStrategy = TargetingStrategy.Closest;

        [Tooltip("HALF-angle (degrees) of the 'in line' cone used by the MostInLine strategy. 8° = a ±8° wedge ≈ 16° total. Wider = easier to find lined targets but less sniper-like precision. Narrower = harder to find lineups but more dramatic when one happens. Ignored by non-MostInLine strategies.")]
        [Range(1f, 45f)] public float targetingConeHalfAngleDeg = 8f;

        // ─── VFX scaling (visuals come from ElementVfxLibrary) ────────────

        [Header("VFX Scaling")]
        [Tooltip("Scale multiplier on the muzzle flash spawned at fire time. Library default = 1.0. Heavy weapons (grenade) flash bigger, light weapons (pistol) subtler.")]
        [Min(0f)] public float muzzleFlashScale = 1f;

        [Tooltip("Scale multiplier on the projectile visual. Library default = 1.0.")]
        [Min(0f)] public float projectileScale = 1f;

        [Tooltip("Scale multiplier on the hit VFX spawned on collision. Composed at hit time with damage / crit / AoE multipliers (see HitVfxApplier).")]
        [Min(0f)] public float hitVfxScale = 1f;

        [Tooltip("If true, hit VFX is ADDITIONALLY scaled by baseAreaOfEffect — the burst visually fills the damage radius. Set true for grenade launcher; leave false for point-impact weapons.")]
        public bool hitVfxScalesWithAreaOfEffect = false;

        // ─── Trajectory ───────────────────────────────────────────────────

        [Header("Trajectory")]
        [Tooltip("How the projectile moves. Straight = linear along muzzle.forward at baseProjectileSpeed (pistol / shotgun / sniper). Parabolic = ballistic arc landing at flightBeats × 60 / BPM seconds (grenade).")]
        public ProjectileTrajectory trajectory = ProjectileTrajectory.Straight;

        [Tooltip(
            "PARABOLIC ONLY — number of musical beats from launch to impact. " +
            "Ignored when trajectory = Straight. Default 1 (fire on beat N, " +
            "explode on beat N+1). Grenade launcher uses 2 (kick-kick tick-" +
            "tock on beats 1 and 3 of 4/4). Set higher (3, 4) for slow lobs.")]
        [Min(1)] public int flightBeats = 1;

        // ─── Fire-rate pattern (Level → shots/sec via active-cell density) ─

        [Header("Pattern (Level → fire rate)")]
        [Tooltip(
            "Active-cell count per weapon level (1..5). Index 0 = L1, index 4 = L5. " +
            "Effective fire rate = activeCells / patternDuration, where " +
            "patternDuration = barCount × beatsPerBar × 60 / BPM, and BPM is " +
            "read globally from MusicConductor.Instance.BPM (driven by " +
            "Dexterity). Example: 4 active cells, 4-bar pattern, 120 BPM → " +
            "4 / 8s = 0.5 shots/sec. Required — weapons with no pattern fire " +
            "at 0 shots/sec.")]
        public int[] activeCellsPerLevel = new int[5] { 4, 8, 16, 32, 64 };

        [Tooltip("Bars per pattern. Default 4. Coprime values (5, 7) get the moiré effect described in procedural_music_reference.md §11.")]
        [Min(1)] public int barCount = 4;

        [Tooltip("Beats per bar (time signature numerator). Default 4 (4/4 time).")]
        [Min(1)] public int beatsPerBar = 4;

        // ─── Evolution (M10 placeholder) ─────────────────────────────────

        [Header("Evolution (M10 placeholder — inert)")]
        [Tooltip("M10 PLACEHOLDER — the WeaponData this weapon evolves into when its L5-evolution trigger fires. Not wired yet.")]
        public WeaponData finalForm;

        [Tooltip("M10 PLACEHOLDER — power-up id that triggers evolution. Not wired yet.")]
        public string requiredPowerUpId;

        // ─── Element (test-only) ─────────────────────────────────────────

        [Header("Element — TEST-ONLY")]
        [Tooltip(
            "TEST-ONLY default element. Production weapons start NEUTRAL " +
            "(ElementId.None) and only acquire an element when a power-up " +
            "is slotted on the same loadout axis (M8 element coupling). " +
            "This field exists for editor / preview / unit-test scenarios " +
            "where you want a weapon to fire with a specific element without " +
            "going through the full power-up coupling flow. Leave as None " +
            "for any weapon shipping in a real run.")]
        public ElementId defaultElement = ElementId.None;

        // ─── Rarity tier perks (dual-axis bonus per rarity) ──────────────

        [Header("Rarity Tier Perks")]
        [Tooltip(
            "Per-rarity-tier bonus perks. Index = (int)Rarity (0=Common..4=Legendary). " +
            "Each entry layers an effect ON TOP of the global damage scalar " +
            "from Rarity.DamageMultiplier(). Leave entries empty for tiers " +
            "that don't grant a bonus perk for this weapon.")]
        public RarityTierPerk[] rarityTierPerks = new RarityTierPerk[0];

        // ─── Tunables — fallback BPM when MusicConductor isn't running ────

        /// <summary>
        /// Fallback BPM used by <see cref="GetFireRateForLevel"/> when no
        /// <see cref="MusicConductor"/> singleton is present (editor preview,
        /// pre-game-bootstrap, unit tests). Production play-through always
        /// has the conductor available. 120 BPM is the neutral baseline
        /// matching most rock / electronic 4/4 backbones.
        /// </summary>
        private const float FallbackBPM = 120f;

        // ─── Debug helpers ────────────────────────────────────────────────

        [ContextMenu("Debug Weapon Data")]
        private void DebugWeaponData()
        {
            Debug.Log($"Weapon: {displayName}");
            Debug.Log($"ID: {equipmentId}");
            Debug.Log($"Icon assigned: {equipmentIcon != null}");
            if (equipmentIcon != null)
                Debug.Log($"Icon name: {equipmentIcon.name}");
        }

        // ─── Editor validation ───────────────────────────────────────────

        protected override void OnValidate()
        {
            base.OnValidate();
            ValidateWeaponFields();
        }

        private void ValidateWeaponFields()
        {
            baseDamage = Mathf.Max(0.1f, baseDamage);
            baseProjectileSpeed = Mathf.Max(0.1f, baseProjectileSpeed);
            baseAreaOfEffect = Mathf.Max(0.1f, baseAreaOfEffect);
            basePierceCount = Mathf.Max(0, basePierceCount);
            barCount = Mathf.Max(1, barCount);
            beatsPerBar = Mathf.Max(1, beatsPerBar);

            // Clamp pattern array entries to a sensible range. Max cell
            // count = bars × 32 (the 32-subdiv master grid).
            if (activeCellsPerLevel == null) activeCellsPerLevel = new int[0];
            int maxCells = barCount * 32;
            for (int i = 0; i < activeCellsPerLevel.Length; i++)
                activeCellsPerLevel[i] = Mathf.Clamp(activeCellsPerLevel[i], 0, maxCells);
        }

        // ─── Dual-axis API ────────────────────────────────────────────────

        /// <summary>
        /// Effective damage per shot at the given Rarity. Rarity scales
        /// damage; Level controls fire rate (via patterns), not damage.
        /// Combine with player-side modifiers (Power, crit) at the call
        /// site: <c>finalDamage = baseDamage × Rarity.DamageMultiplier() × (1 + Power*0.01) × critMul</c>.
        /// </summary>
        public float GetDamageForRarity(Rarity rarity)
        {
            return baseDamage * rarity.DamageMultiplier();
        }

        /// <summary>
        /// Effective fire rate (shots/sec) at the given level. Reads the
        /// global BPM from <see cref="MusicConductor"/> — there's no
        /// per-weapon BPM anymore (BPM is a song-level property; all
        /// weapons in a run share it, driven by Dexterity → conductor).
        ///
        /// <code>
        ///   bpm = MusicConductor.Instance.BPM  (or 120 fallback)
        ///   patternDuration = barCount × beatsPerBar × (60 / bpm)
        ///   fireRate = activeCellsPerLevel[level - 1] / patternDuration
        /// </code>
        ///
        /// Returns 0 if no pattern is authored — that signals the weapon
        /// isn't fully configured and shouldn't fire. WeaponFiring logs a
        /// warning in that case.
        /// </summary>
        public float GetFireRateForLevel(int level)
        {
            if (activeCellsPerLevel == null || activeCellsPerLevel.Length == 0)
                return 0f;

            int idx = Mathf.Clamp(level - 1, 0, activeCellsPerLevel.Length - 1);
            int activeCells = activeCellsPerLevel[idx];
            if (activeCells <= 0) return 0f;

            float bpm = CurrentBPM();
            float patternDuration = barCount * beatsPerBar * (60f / bpm);
            if (patternDuration <= 0f) return 0f;

            return activeCells / patternDuration;
        }

        /// <summary>
        /// Read the current global BPM. Prefer <see cref="MusicConductor.Instance.BPM"/>;
        /// fall back to <see cref="FallbackBPM"/> when no conductor exists
        /// (editor preview, pre-bootstrap, unit tests).
        /// </summary>
        public static float CurrentBPM()
        {
            var conductor = MusicConductor.Instance;
            return conductor != null ? conductor.BPM : FallbackBPM;
        }

        // ─── Grid-locked firing (phase-locked to MusicConductor) ─────────

        /// <summary>
        /// Total subdivisions in one pattern cycle at the conductor's grid
        /// resolution. = <c>barCount × beatsPerBar × subdivisionsPerBeat</c>.
        ///
        /// Example: 1 bar × 4 beats × 4 subdivs/beat = 16 subdivisions
        /// (the conductor's default 16th-note grid). 4-bar pattern at the
        /// same grid = 64. WeaponFiring uses this together with
        /// <see cref="GetFireCellsForLevel"/> to phase-lock every shot to
        /// the master beat clock, eliminating float-cooldown drift between
        /// weapons.
        /// </summary>
        public int GetTotalSubdivisions(int subdivisionsPerBeat)
        {
            return barCount * beatsPerBar * Mathf.Max(1, subdivisionsPerBeat);
        }

        /// <summary>
        /// Returns the subdivision indices at which this weapon fires
        /// within one pattern cycle, in the range [0, totalSubdivs - 1].
        /// Cells are evenly distributed across the pattern via integer
        /// Bresenham-style spacing: <c>cell[i] = i × totalSubdivs / activeCells</c>.
        ///
        /// Properties of this distribution:
        ///   • Always includes subdiv 0 (first cell of pattern).
        ///   • Sorted ascending (cells[i+1] >= cells[i]).
        ///   • For activeCells that divides totalSubdivs evenly (e.g., 8 in
        ///     16), gives clean integer intervals (every 2 subdivs). For
        ///     non-clean cases (e.g., 5 in 16), distributes as evenly as
        ///     integer math allows: {0, 3, 6, 9, 12}.
        ///   • Two weapons sampling the same grid coincide at every
        ///     common multiple of their intervals → mathematically cannot
        ///     drift apart.
        ///
        /// When real composer-authored patterns land (procedural_music_
        /// reference §22 — explicit cell array per level/element), this
        /// method's body becomes a lookup into that authored array. The
        /// API stays the same so WeaponFiring doesn't need to change.
        ///
        /// Returns empty array when no pattern is authored, totalSubdivs is
        /// invalid, or activeCells == 0.
        /// </summary>
        public int[] GetFireCellsForLevel(int level, int totalSubdivs)
        {
            if (activeCellsPerLevel == null || activeCellsPerLevel.Length == 0)
                return System.Array.Empty<int>();
            if (totalSubdivs <= 0)
                return System.Array.Empty<int>();

            int idx = Mathf.Clamp(level - 1, 0, activeCellsPerLevel.Length - 1);
            int activeCells = activeCellsPerLevel[idx];
            if (activeCells <= 0)
                return System.Array.Empty<int>();

            // Saturate: a pattern can't have more fire-cells than grid
            // subdivisions. Every subdiv fires.
            if (activeCells >= totalSubdivs)
            {
                var all = new int[totalSubdivs];
                for (int i = 0; i < totalSubdivs; i++) all[i] = i;
                return all;
            }

            var cells = new int[activeCells];
            for (int i = 0; i < activeCells; i++)
                cells[i] = i * totalSubdivs / activeCells;
            return cells;
        }

        /// <summary>
        /// Returns the bonus perk (if any) defined for the given rarity tier.
        /// Returns null when no perk is configured for that tier.
        /// </summary>
        public RarityTierPerk GetPerkForRarity(Rarity rarity)
        {
            if (rarityTierPerks == null) return null;
            int idx = (int)rarity;
            if (idx < 0 || idx >= rarityTierPerks.Length) return null;
            var perk = rarityTierPerks[idx];
            return (perk != null && !string.IsNullOrEmpty(perk.title)) ? perk : null;
        }

        /// <summary>
        /// Returns stat descriptors for the equipment-hub preview at the
        /// given upgrade level. Damage shown at Common rarity (the floor);
        /// fire rate shown at the current global BPM (whatever Dex drives).
        /// </summary>
        public override StatDescriptor[] GetStatsForLevel(int upgradeLevel)
        {
            upgradeLevel = Mathf.Clamp(upgradeLevel, 1, maxUpgradeLevel);
            var stats = new List<StatDescriptor>(6);

            stats.Add(new StatDescriptor("Damage (Common)", baseDamage));
            stats.Add(new StatDescriptor("Fire Rate", GetFireRateForLevel(upgradeLevel)));

            switch (attackType)
            {
                case WeaponAttackType.Projectile:
                    stats.Add(new StatDescriptor("Projectile Speed", baseProjectileSpeed));
                    if (basePierceCount > 0)
                        stats.Add(new StatDescriptor("Pierce Count", basePierceCount));
                    break;
                case WeaponAttackType.Area:
                case WeaponAttackType.Beam:
                    stats.Add(new StatDescriptor(attackType == WeaponAttackType.Beam ? "Range" : "Area of Effect", baseAreaOfEffect));
                    break;
            }

            return stats.ToArray();
        }
    }

    /// <summary>
    /// One per-rarity-tier bonus perk for a weapon. Drives the
    /// "+1 minor effect" / "+1 keyword" tooltip line and the runtime
    /// application of the perk's effect (when wired in a later milestone —
    /// until then it's data-only and surfaces in the tooltip).
    ///
    /// Class (not struct) so "this tier has no perk" reads as null/empty
    /// rather than a default-initialised struct.
    /// </summary>
    [Serializable]
    public class RarityTierPerk
    {
        [Tooltip("Short label shown on the weapon tooltip (e.g., 'Pierces enemies', 'Crit triple'). Keep under ~40 chars.")]
        public string title;

        [Tooltip("Optional longer description shown in expanded tooltip / equipment hub view.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Stable id for the perk's gameplay effect, used by the rarity-perk effect dispatcher when wired. Leave empty for pure-flavor perks that only show in tooltips.")]
        public string effectId;
    }
}

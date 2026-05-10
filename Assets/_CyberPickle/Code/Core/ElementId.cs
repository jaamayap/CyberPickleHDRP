// File: Assets/_CyberPickle/Code/Core/ElementId.cs
// Namespace: CyberPickle.Core
//
// SINGLE SOURCE OF TRUTH for the project's element identity enum.
//
// Used by:
//   - Weapons (WeaponInstanceData.element — runtime; WeaponData.defaultElement — design)
//   - Power-ups (PowerUpData.element — M9 work; one of the keys for the
//     type×element card draft pool, see procedural_music_reference.md §22.4)
//   - VFX (projectile palette tints toward the element's canonical color)
//   - Music system (drives the active musical mode for a weapon's pattern
//     playback — see procedural_music_reference.md §22.6)
//   - Achievements / mastery challenges (element-themed conditions)
//
// CENTRALIZATION RULE (mirrors Rarity):
//   - All element-bearing systems use this enum.
//   - DO NOT define new element enums (WeaponElement, PowerUpElement, etc.).
//   - DO NOT shadow this enum in another namespace.
//
// History:
//   - 2026-05-10: Created. Element layer added to support per-weapon
//     mode mapping and power-up type×element coupling per the music
//     design refinement.
//
// Cross-reference:
//   - procedural_music_reference.md §8 — element-to-mode mapping
//   - procedural_music_reference.md §22 — power-up coupling + pattern playback

using UnityEngine;

namespace CyberPickle.Core
{
    /// <summary>
    /// Element identity. Drives:
    /// <list type="bullet">
    /// <item>Music: which musical mode the weapon's rhythmic pattern plays in
    ///       (Fire = Phrygian Dominant, Lightning = Phrygian, etc. — see
    ///       <c>procedural_music_reference.md</c> §8).</item>
    /// <item>VFX: projectile + hit-effect color palette tints.</item>
    /// <item>Power-up cards: the same mechanical effect can appear in up to 7
    ///       elemental flavors, giving build-identity variety in the draft.</item>
    /// </list>
    ///
    /// Byte-typed for ECS IComponentData compatibility. Values are stable
    /// contracts (persisted in save data, .asset files, ECS chunks) — DO NOT
    /// renumber. Insert new elements at the end with new byte values.
    /// </summary>
    public enum ElementId : byte
    {
        /// <summary>Sentinel — used when no element has been assigned yet (e.g., a generic / non-elemental weapon, or a weapon whose default element hasn't been authored). NOT a playable element.</summary>
        None      = 0,
        Fire      = 1,
        Lightning = 2,
        Ice       = 3,
        Earth     = 4,
        Plasma    = 5,
        Light     = 6,
        Dark      = 7,
    }

    /// <summary>
    /// Extension methods for ElementId. Per the centralization rule, these
    /// are the canonical source of element-related visuals and tags. Read
    /// from here — never re-derive in consumer code.
    /// </summary>
    public static class ElementIdExtensions
    {
        /// <summary>
        /// User-facing display name. "Fire", "Lightning", etc. <see cref="ElementId.None"/>
        /// returns "—" so it reads as "no element" in tooltips.
        /// </summary>
        public static string DisplayName(this ElementId e) => e switch
        {
            ElementId.None      => "—",
            ElementId.Fire      => "Fire",
            ElementId.Lightning => "Lightning",
            ElementId.Ice       => "Ice",
            ElementId.Earth     => "Earth",
            ElementId.Plasma    => "Plasma",
            ElementId.Light     => "Light",
            ElementId.Dark      => "Dark",
            _                   => "?",
        };

        /// <summary>
        /// Canonical UI / VFX accent color. Card frames, projectile palettes,
        /// hit-impact bursts, weapon-icon tints all read from here.
        ///
        ///   Fire      — orange-red    (combat aggression)
        ///   Lightning — yellow        (chaotic, fast)
        ///   Ice       — cyan          (cold, calm)
        ///   Earth     — moss-brown    (grounded, steady)
        ///   Plasma    — magenta       (exotic, otherworldly)
        ///   Light     — pale-gold     (protective, holy-tech)
        ///   Dark      — deep-purple   (sinister, abyssal)
        ///   None      — neutral grey  (no-element fallback)
        /// </summary>
        public static Color DisplayColor(this ElementId e) => e switch
        {
            ElementId.None      => new Color(0.55f, 0.55f, 0.60f, 1f),  // grey
            ElementId.Fire      => new Color(1.00f, 0.36f, 0.10f, 1f),  // orange-red
            ElementId.Lightning => new Color(1.00f, 0.92f, 0.20f, 1f),  // yellow
            ElementId.Ice       => new Color(0.40f, 0.85f, 1.00f, 1f),  // cyan
            ElementId.Earth     => new Color(0.55f, 0.45f, 0.25f, 1f),  // moss-brown
            ElementId.Plasma    => new Color(0.95f, 0.30f, 0.85f, 1f),  // magenta
            ElementId.Light     => new Color(1.00f, 0.95f, 0.70f, 1f),  // pale-gold
            ElementId.Dark      => new Color(0.40f, 0.10f, 0.55f, 1f),  // deep-purple
            _                   => Color.white,
        };

        /// <summary>
        /// True for the seven playable elements; false for None.
        /// Convenience for filtering UI lists (don't show None as a draft option).
        /// </summary>
        public static bool IsPlayable(this ElementId e) => e != ElementId.None;
    }
}

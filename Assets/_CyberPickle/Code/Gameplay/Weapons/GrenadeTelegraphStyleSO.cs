// File: Assets/_CyberPickle/Code/Gameplay/Weapons/GrenadeTelegraphStyleSO.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Designer-facing style sheet for the grenade-launcher aim preview
// (parabolic arc + AoE ring + optional ground-sprite disc). One SO
// per visual flavour — typically a single project-wide default for all
// grenade launchers, with optional per-weapon overrides for legendaries
// or evolved variants.
//
// Wiring:
//   1. Create the asset:  Assets → Create → CyberPickle → VFX → Grenade Telegraph Style.
//   2. Either drop it on the project-wide default reference (if you add one)
//      OR assign per-weapon on WeaponData.telegraphStyle.
//   3. WeaponFiring drives a GrenadeTelegraph child on each parabolic weapon;
//      the telegraph reads from this SO to configure its LineRenderers + disc.
//
// Element tinting: the telegraph receives the weapon's current element
// colour from WeaponFiring each frame (so swapping the equipped power-up
// from Ice → Fire recolours the preview live). The SO's baseColor is
// either used straight (when tintByElement=false) or mixed with the
// element colour by elementTintStrength.

using UnityEngine;

namespace CyberPickle.Gameplay.Weapons
{
    [CreateAssetMenu(fileName = "GrenadeTelegraphStyle", menuName = "CyberPickle/VFX/Grenade Telegraph Style", order = 1)]
    public class GrenadeTelegraphStyleSO : ScriptableObject
    {
        // ─── Arc line (parabolic trajectory) ─────────────────────────────

        [Header("Arc Line")]
        [Tooltip("Material applied to the arc LineRenderer. Leave null to fall back to Sprites/Default (always renders, no setup needed) — assign a custom HDRP/Unlit material for designer-controlled emissive look.")]
        public Material arcMaterial;

        [Tooltip("Width of the arc line along its length (0 = muzzle, 1 = landing point). Designer can taper the line — e.g. thicker near muzzle, thinner near impact, or vice versa.")]
        public AnimationCurve arcWidth = AnimationCurve.Linear(0f, 0.08f, 1f, 0.04f);

        [Tooltip("Number of segments sampled along the parabolic arc. 24 is smooth for typical arcs; bump to 48 if grenades fly long distances and the arc looks polygonal.")]
        [Range(4, 128)] public int arcSegments = 24;

        // ─── AoE ring (outline of the blast circle) ──────────────────────

        [Header("AoE Ring (outline)")]
        [Tooltip("Material applied to the ring LineRenderer. Leave null to fall back to Sprites/Default. The ring is drawn as a loop, so the line connects back to itself smoothly.")]
        public Material ringMaterial;

        [Tooltip("Width of the ring outline. Constant 1.0 → uniform; designer can vary for stylistic effect.")]
        public AnimationCurve ringWidth = AnimationCurve.Constant(0f, 1f, 0.08f);

        [Tooltip("Number of segments around the circle. 48 reads as smooth at typical AoE radii (~2-3m).")]
        [Range(8, 128)] public int ringSegments = 48;

        [Tooltip("Vertical lift above the ground plane — small positive (e.g. 0.05m) prevents Z-fighting with floor meshes.")]
        public float ringHeightOffset = 0.05f;

        // ─── Ground disc (optional filled circle / sprite under the ring) ──

        [Header("Ground Disc (optional fill)")]
        [Tooltip("Material applied to a flat Quad rendered on the ground at the AoE landing point. Use an HDRP/Unlit material with a circle-mask texture (alpha-tested or transparent) for a 'damage zone' look. Leave null to skip the disc entirely (only the ring outline shows).")]
        public Material groundDiscMaterial;

        [Tooltip("Multiplier on the disc's scale relative to the AoE radius. 1.0 = disc edge exactly at ring; >1 = disc extends past ring (soft edge); <1 = disc smaller than ring (inner highlight).")]
        [Min(0.01f)] public float groundDiscScale = 1.0f;

        // ─── Color + alpha ───────────────────────────────────────────────

        [Header("Color")]
        [Tooltip("Base colour when not tinting by element, OR mixed with the element colour when tintByElement is true. Designer-tunable so the telegraph reads at HDRP brightness without saturating.")]
        public Color baseColor = new Color(1f, 0.85f, 0.4f, 1f);

        [Tooltip("If true, the telegraph is tinted by the weapon's current element colour (Fire → orange, Ice → cyan, etc.). If false, baseColor is used as-is.")]
        public bool tintByElement = true;

        [Tooltip("How strongly element tint overrides baseColor. 0 = pure baseColor; 1 = pure element colour; mix in between. Only applies when tintByElement is true.")]
        [Range(0f, 1f)] public float elementTintStrength = 1f;

        [Tooltip("Overall alpha multiplier applied to the final colour. Use this to make the telegraph more subtle (0.5) or solid (1.0).")]
        [Range(0f, 1f)] public float overallAlpha = 0.9f;

        // ─── Defaults / helpers ──────────────────────────────────────────

        /// <summary>
        /// Compose the final colour for arc/ring/disc by mixing baseColor
        /// with elementColor (if tinting is enabled) and applying the
        /// overall alpha multiplier.
        /// </summary>
        public Color ResolveColor(Color elementColor)
        {
            Color c = baseColor;
            if (tintByElement && elementColor.a > 0.001f)
            {
                c = Color.Lerp(baseColor, elementColor, elementTintStrength);
            }
            c.a *= overallAlpha;
            return c;
        }
    }
}

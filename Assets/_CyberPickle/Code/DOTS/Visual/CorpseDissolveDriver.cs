// File: Assets/_CyberPickle/Code/DOTS/Visual/CorpseDissolveDriver.cs
// Namespace: CyberPickle.DOTS.Visual
//
// Lives on the hybrid GameObject visual prefab alongside ZombieAnimDriver.
// Driven by CorpseLifecycleSystem via the EnemyVisualBridge — when the
// entity's CorpseLifecycle hits its DelayBeforeDissolve threshold, the
// system calls `StartDissolve(duration)` on this component.
//
// During dissolve:
//   - Animator gets disabled (frees per-frame Animator.Update cost)
//   - Each frame, transform.localScale lerps from initial → zero
//   - Each frame, every material's emission intensity ramps UP sharply
//     (gives a "body absorbing into its own energy" flare via HDRP bloom)
//   - Optionally spawn a particle burst at start for spectacle
//   - Color tint can shift toward a "death color" (e.g. cyan/magenta) to
//     sell the cyber dissolve aesthetic
//
// When dissolve completes, this component does nothing further — the
// entity destruction (handled by the system) auto-destroys the visual
// via the bridge's stale-entry cleanup path.
//
// Material handling: we instance the materials so each corpse gets its
// own material copy (otherwise tweaking one would tweak all spawned
// instances). This is unavoidable for per-corpse emission ramping.
// The new instances are tracked and destroyed when we are.

using System.Collections.Generic;
using UnityEngine;

namespace CyberPickle.DOTS.Visual
{
    [DisallowMultipleComponent]
    public class CorpseDissolveDriver : MonoBehaviour
    {
        [Header("Visual Effect")]
        [Tooltip("Optional particle burst spawned at this transform's position when the dissolve starts. Cyber sparkles, energy burst, etc.")]
        public GameObject onDissolveStartVfx;

        [Tooltip("Maximum emission intensity multiplier reached at the end of the dissolve. Final value = original × this.")]
        [Range(1f, 30f)] public float emissionFlareMultiplier = 8f;

        [Tooltip("If non-zero, the emissive color shifts toward this hue during dissolve. Set HDR to drive bloom hard. Leave default (0,0,0,0) to keep original color.")]
        [ColorUsage(showAlpha: false, hdr: true)] public Color targetEmissionTint = Color.black;

        [Tooltip("Curve mapping dissolve progress (0..1) to scale multiplier (1..0). Default = ease-in shrink.")]
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Tooltip("Curve mapping dissolve progress (0..1) to emission flare multiplier (1..emissionFlareMultiplier). Default = sharp ramp at the end.")]
        public AnimationCurve emissionCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.6f, 0.2f, 0f, 0f),
            new Keyframe(1f, 1f, 8f, 0f));

        // ─── Internal ───────────────────────────────────────────────────────
        private bool dissolving;
        private float elapsed;
        private float duration;
        private Vector3 initialScale;
        private Animator animator;

        private struct MaterialState
        {
            public Material instance;
            public Color originalEmission;
            public bool hadEmissionEnabled;
        }
        private List<MaterialState> trackedMaterials;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor"); // HDRP/Lit uses this
        private const string EmissionKeyword = "_EMISSION";

        private void Awake()
        {
            initialScale = transform.localScale;
            animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Begin the dissolve effect. Called once by CorpseLifecycleSystem
        /// when the corpse's delay timer expires. Idempotent — re-calling
        /// while a dissolve is in progress does nothing.
        /// </summary>
        public void StartDissolve(float dissolveDuration)
        {
            if (dissolving) return;
            dissolving = true;
            elapsed = 0f;
            duration = Mathf.Max(0.05f, dissolveDuration);

            // Stop animator ticking — frees per-frame Animator.Update cost
            // for the duration of the dissolve. Mesh stays in last pose.
            if (animator != null) animator.enabled = false;

            // Spawn the visual burst, if assigned.
            if (onDissolveStartVfx != null)
            {
                Instantiate(onDissolveStartVfx, transform.position, transform.rotation);
            }

            CacheAndInstanceMaterials();
        }

        private void CacheAndInstanceMaterials()
        {
            trackedMaterials = new List<MaterialState>();
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: false);

            foreach (var rend in renderers)
            {
                // Touching .materials creates instances — that's what we want.
                var mats = rend.materials;
                rend.materials = mats;

                foreach (var mat in mats)
                {
                    if (mat == null) continue;

                    // HDRP/Lit uses _EmissiveColor; Built-in/URP uses _EmissionColor.
                    int prop = mat.HasProperty(EmissiveColorId) ? EmissiveColorId
                               : mat.HasProperty(EmissionColorId) ? EmissionColorId
                               : -1;
                    if (prop == -1) continue;

                    Color original = mat.GetColor(prop);
                    bool hadEmission = mat.IsKeywordEnabled(EmissionKeyword) || prop == EmissiveColorId;

                    // Make sure emission is on so our ramped value actually shows up.
                    if (!hadEmission) mat.EnableKeyword(EmissionKeyword);

                    trackedMaterials.Add(new MaterialState
                    {
                        instance = mat,
                        originalEmission = original,
                        hadEmissionEnabled = hadEmission,
                    });
                }
            }
        }

        private void Update()
        {
            if (!dissolving) return;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Scale shrink — curve-driven so designers can tweak the easing.
            float scaleT = scaleCurve.Evaluate(t);
            transform.localScale = initialScale * Mathf.Max(0f, scaleT);

            // Emission ramp — drives HDRP bloom hard at the climax.
            float emissionT = emissionCurve.Evaluate(t);
            float intensityFactor = Mathf.Lerp(1f, emissionFlareMultiplier, emissionT);

            if (trackedMaterials != null)
            {
                for (int i = 0; i < trackedMaterials.Count; i++)
                {
                    var s = trackedMaterials[i];
                    if (s.instance == null) continue;

                    int prop = s.instance.HasProperty(EmissiveColorId) ? EmissiveColorId : EmissionColorId;
                    Color blended = Color.Lerp(s.originalEmission, targetEmissionTint, emissionT);
                    s.instance.SetColor(prop, blended * intensityFactor);
                }
            }

            // We don't destroy ourselves — CorpseLifecycleSystem destroys the
            // ECS entity at the end of the duration, which causes the bridge to
            // unregister + Destroy(this.gameObject). Keeps lifecycle authoritative
            // on the entity side.
        }
    }
}

// File: Assets/_CyberPickle/Code/Gameplay/Weapons/GrenadeTelegraph.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Visual aim preview for a parabolic / AoE projectile (grenade launcher).
// Persistent component — typically lives as a child GameObject of each
// parabolic weapon. WeaponFiring drives it each frame:
//   • Has a target?  → ShowAim(...) updates the arc + ring + disc.
//   • No target?     → Hide() makes everything invisible.
//
// Designer style data comes from a GrenadeTelegraphStyleSO (materials,
// widths, colours, alpha, optional ground-disc material). Element tint
// is applied live each frame, so swapping a power-up that changes the
// weapon's element recolours the preview immediately.
//
// Visual pieces (all auto-created if not assigned):
//   • Arc LineRenderer  — sampled parabola from muzzle to landing point.
//   • Ring LineRenderer — closed loop on the ground at AoE radius.
//   • Ground disc Quad  — optional filled circle / sprite UNDER the ring,
//                         scaled to AoE diameter × style.groundDiscScale.
//
// One-shot vs persistent: the previous version of this script destroyed
// itself after flightTime — that was wrong for an "aim preview" UX, which
// must persist as long as the weapon has a target. Drop a fire-and-forget
// instance per shot only if you want a separate in-flight visual; this
// component is now the aim preview only.

using UnityEngine;

namespace CyberPickle.Gameplay.Weapons
{
    [DisallowMultipleComponent]
    public class GrenadeTelegraph : MonoBehaviour
    {
        [Header("Style (designer-facing)")]
        [Tooltip("ScriptableObject controlling materials, widths, colours, alpha, and optional ground-disc material. Per-weapon overrides come from WeaponData.telegraphStyle; this Inspector reference is the fallback used when WeaponFiring doesn't push a style.")]
        [SerializeField] private GrenadeTelegraphStyleSO style;

        [Header("LineRenderers (auto-created if null)")]
        [Tooltip("Draws the parabolic arc. Auto-created on a child GameObject in Awake if not assigned, so the script works out of the box.")]
        [SerializeField] private LineRenderer arcLine;

        [Tooltip("Draws the AoE landing circle as a closed loop. Auto-created in Awake if not assigned.")]
        [SerializeField] private LineRenderer ringLine;

        [Header("Ground Disc (auto-created if null)")]
        [Tooltip("Optional flat Quad rendered on the ground for a filled damage-zone visual. Auto-created in Awake if not assigned. Hidden when style.groundDiscMaterial is null.")]
        [SerializeField] private Transform groundDisc;

        // ─── Runtime state ────────────────────────────────────────────────

        private bool _initialized;
        private MeshRenderer _discRenderer;

        // Shared circle mesh used by all ground discs (cheap — one mesh
        // allocation regardless of how many telegraphs exist). Built lazily
        // the first time a disc child is created. Unit radius (= 1m) in the
        // XZ plane with normal facing +Y — ShowAim scales by aoeRadius
        // directly and never has to rotate.
        private static Mesh s_sharedCircleMesh;

        // Color property names vary per shader. We write ALL the common
        // ones to each renderer's INSTANCED material; setting a property
        // a shader doesn't declare is a silent no-op, so this is cheap
        // and covers every material a designer might assign:
        //
        //   _BaseColor      — Shader Graph (lit + unlit), HDRP/Lit
        //   _UnlitColor     — HDRP/Unlit (the default HDRP unlit shader)
        //   _Color          — legacy / Sprites/Default / URP/Unlit
        //   _EmissiveColor  — HDRP emission (lit + unlit) — designer can
        //                     bump emissive intensity on the material so
        //                     the line glows when this is tinted
        //
        // Why instanced materials (not MaterialPropertyBlock):
        // MaterialPropertyBlock writes are documented to override per-
        // renderer properties, but in practice HDRP + LineRenderer
        // combinations can swallow them (SRP Batcher path quirks).
        // Instancing the material on first tint guarantees the writes
        // reach the shader, at the cost of one Material allocation per
        // renderer per weapon. We destroy them in OnDestroy to avoid
        // a per-run leak. With at most ~16 renderers across a 4-weapon
        // loadout this is negligible.
        private static readonly int s_baseColorId     = Shader.PropertyToID("_BaseColor");
        private static readonly int s_unlitColorId    = Shader.PropertyToID("_UnlitColor");
        private static readonly int s_colorId         = Shader.PropertyToID("_Color");
        private static readonly int s_emissiveColorId = Shader.PropertyToID("_EmissiveColor");

        // Per-renderer material instances + the shared-material source they
        // were cloned from. Tracking the source lets us re-instance if the
        // designer swaps the style SO at runtime (rare, but cheap to handle).
        private Material _arcMatInstance,  _arcMatSource;
        private Material _ringMatInstance, _ringMatSource;
        private Material _discMatInstance, _discMatSource;

        // ─── Public API (called by WeaponFiring each frame) ──────────────

        /// <summary>
        /// Make this telegraph match the supplied launch parameters and
        /// element colour. Called each frame the weapon has a target.
        /// </summary>
        /// <param name="spawnPos">Muzzle world position — start of the arc.</param>
        /// <param name="v0">Initial launch velocity (m/s) — direction + magnitude. WeaponFiring computes this from spawn → target with the same helper the projectile uses, so the arc traces the actual flight path.</param>
        /// <param name="gravityMagnitude">Magnitude of the downward gravity vector (m/s²).</param>
        /// <param name="flightTime">Flight time used to sample the arc — must match the projectile's lifetime so the line ends exactly at the landing point.</param>
        /// <param name="aoeRadius">Blast radius — drives the ring and disc size.</param>
        /// <param name="elementColor">Weapon's current element colour. Mixed with style.baseColor per style.elementTintStrength.</param>
        public void ShowAim(Vector3 spawnPos, Vector3 v0, float gravityMagnitude, float flightTime, float aoeRadius, Color elementColor)
        {
            EnsureChildren();
            if (style == null)
            {
                // Without a style we can still render — sensible fallback
                // values keep the telegraph visible. Designer should assign
                // a style to actually customize.
                ApplyFallbackLineRendererConfig();
            }
            else
            {
                ApplyStyleToLineRenderers();
            }

            // Resolve colour (element tint + alpha multiplier).
            Color color = style != null ? style.ResolveColor(elementColor) : new Color(1f, 0.85f, 0.4f, 0.9f);

            // ─── Arc points ──
            int arcSegments = style != null ? style.arcSegments : 24;
            float g = Mathf.Max(0.01f, gravityMagnitude);
            float t = Mathf.Max(0.05f, flightTime);

            if (arcLine != null)
            {
                arcLine.positionCount = arcSegments + 1;
                for (int i = 0; i <= arcSegments; i++)
                {
                    float ti = (i / (float)arcSegments) * t;
                    arcLine.SetPosition(i, ParabolaPos(spawnPos, v0, g, ti));
                }
                // Vertex colors (for shaders that read them — Sprites/Default).
                arcLine.startColor = arcLine.endColor = color;
                // AND instanced-material tint (for HDRP/Unlit + Shader Graph
                // unlit, which ignore vertex colors).
                ApplyTint(arcLine, ref _arcMatInstance, ref _arcMatSource, color);
                arcLine.enabled = true;
            }

            // ─── Ring points ──
            Vector3 landingPos = ParabolaPos(spawnPos, v0, g, t);
            int ringSegments = style != null ? style.ringSegments : 48;
            float ringLift   = style != null ? style.ringHeightOffset : 0.05f;

            if (ringLine != null)
            {
                ringLine.loop = true;
                ringLine.positionCount = ringSegments;
                Vector3 center = landingPos + Vector3.up * ringLift;
                float step = (2f * Mathf.PI) / ringSegments;
                for (int i = 0; i < ringSegments; i++)
                {
                    float a = i * step;
                    ringLine.SetPosition(i, new Vector3(
                        center.x + Mathf.Cos(a) * aoeRadius,
                        center.y,
                        center.z + Mathf.Sin(a) * aoeRadius));
                }
                ringLine.startColor = ringLine.endColor = color;
                ApplyTint(ringLine, ref _ringMatInstance, ref _ringMatSource, color);
                ringLine.enabled = true;
            }

            // ─── Ground disc ──
            // The disc is a procedural circle mesh (built in EnsureChildren),
            // unit radius, flat in XZ with normal +Y. We pin its WORLD
            // rotation to identity each frame so the disc lies flat on the
            // ground regardless of how the weapon (its parent) is rotated.
            // localScale.xz = aoeRadius × style scale → the disc fits inside
            // the AoE ring (with optional growth/shrink via groundDiscScale).
            if (groundDisc != null)
            {
                bool discVisible = style != null && style.groundDiscMaterial != null;
                groundDisc.gameObject.SetActive(discVisible);

                if (discVisible)
                {
                    float radiusScale = style.groundDiscScale * aoeRadius;
                    groundDisc.position = landingPos + Vector3.up * (ringLift * 0.5f); // slightly below ring for layering
                    groundDisc.rotation = Quaternion.identity; // WORLD identity — decouples from weapon rotation
                    groundDisc.localScale = new Vector3(radiusScale, 1f, radiusScale);
                    if (_discRenderer != null)
                    {
                        _discRenderer.sharedMaterial = style.groundDiscMaterial;
                        ApplyTint(_discRenderer, ref _discMatInstance, ref _discMatSource, color);
                    }
                }
            }

            _initialized = true;
        }

        /// <summary>Hide the telegraph (no target / aim lost). Cheap — just disables renderers.</summary>
        public void Hide()
        {
            if (arcLine != null)  arcLine.enabled = false;
            if (ringLine != null) ringLine.enabled = false;
            if (groundDisc != null) groundDisc.gameObject.SetActive(false);
        }

        /// <summary>Set the style at runtime (e.g. when WeaponFiring resolves WeaponData.telegraphStyle). Updates apply on the next ShowAim call.</summary>
        public void SetStyle(GrenadeTelegraphStyleSO newStyle)
        {
            style = newStyle;
        }

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureChildren();
            Hide(); // start hidden — WeaponFiring will call ShowAim when there's a target.
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private static Vector3 ParabolaPos(Vector3 spawn, Vector3 v0, float g, float t)
        {
            return spawn + v0 * t + new Vector3(0f, -0.5f * g * t * t, 0f);
        }

        /// <summary>
        /// Set the renderer's per-instance colour by instancing its shared
        /// material on first call and writing colour directly to the
        /// instance. If the shared material later changes (designer swaps
        /// the style SO), the old instance is destroyed and a fresh one
        /// is created from the new source — so style edits propagate
        /// without leaking the previous instance.
        ///
        /// Writes ALL the common colour property names so the tint applies
        /// regardless of which shader the designer assigned. Setting a
        /// non-existent property is a silent no-op.
        /// </summary>
        private void ApplyTint(Renderer r, ref Material instance, ref Material lastSource, Color c)
        {
            if (r == null) return;

            // If the source material changed (style swap), drop the stale
            // instance so we re-clone from the new source.
            if (r.sharedMaterial != lastSource)
            {
                if (instance != null)
                {
                    Destroy(instance);
                    instance = null;
                }
                lastSource = r.sharedMaterial;
            }

            // First tint OR post-swap: instance the material. .material is
            // the auto-instancing accessor — first read clones the shared
            // material; subsequent reads return the cached clone.
            if (instance == null)
            {
                instance = r.material; // auto-instances
            }
            else if (r.sharedMaterial != instance)
            {
                // Renderer's sharedMaterial got changed externally somehow;
                // re-assign our instance so subsequent writes land where
                // we expect.
                r.material = instance;
            }

            // Base / Unlit / legacy Color — write the raw RGBA. The shader's
            // alpha-blend pass uses the alpha channel directly, so overallAlpha
            // dims transparency naturally.
            instance.SetColor(s_baseColorId,  c);
            instance.SetColor(s_unlitColorId, c);
            instance.SetColor(s_colorId,      c);

            // Emissive — HDRP shaders add emission on TOP of the alpha blend
            // (emission is unaffected by alpha). To make overallAlpha actually
            // dim the glow, we pre-multiply the emissive RGB by alpha here.
            // Alpha 1.0 → full glow, alpha 0.2 → 20% glow intensity. The
            // alpha channel itself stays 1 (irrelevant for emission).
            Color emiss = new Color(c.r * c.a, c.g * c.a, c.b * c.a, 1f);
            instance.SetColor(s_emissiveColorId, emiss);
        }

        private void OnDestroy()
        {
            // Clean up instanced materials so we don't leak them across
            // run resets / scene reloads. Each one was created via
            // r.material auto-instancing, so we own them.
            if (_arcMatInstance  != null) Destroy(_arcMatInstance);
            if (_ringMatInstance != null) Destroy(_ringMatInstance);
            if (_discMatInstance != null) Destroy(_discMatInstance);
        }

        private void EnsureChildren()
        {
            if (arcLine == null)
            {
                var arcGo = new GameObject("ArcLine");
                arcGo.transform.SetParent(transform, worldPositionStays: false);
                arcLine = arcGo.AddComponent<LineRenderer>();
                ConfigureLineRendererCommon(arcLine);
            }
            if (ringLine == null)
            {
                var ringGo = new GameObject("RingLine");
                ringGo.transform.SetParent(transform, worldPositionStays: false);
                ringLine = ringGo.AddComponent<LineRenderer>();
                ConfigureLineRendererCommon(ringLine);
            }
            if (groundDisc == null)
            {
                // Build a procedural circle mesh — unit radius in the XZ
                // plane, normal facing +Y. The disc is ALREADY circular
                // by geometry (no alpha-mask texture required) AND already
                // flat (no rotation needed). Designer just needs to assign
                // a flat unlit material; the shape comes for free.
                EnsureSharedCircleMesh();

                var discGo = new GameObject("GroundDisc");
                discGo.transform.SetParent(transform, worldPositionStays: false);
                var mf = discGo.AddComponent<MeshFilter>();
                mf.sharedMesh = s_sharedCircleMesh;
                _discRenderer = discGo.AddComponent<MeshRenderer>();
                _discRenderer.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
                _discRenderer.receiveShadows       = false;
                _discRenderer.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
                _discRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                groundDisc = discGo.transform;
                groundDisc.gameObject.SetActive(false); // hidden until style supplies a material
            }
            else
            {
                _discRenderer = groundDisc.GetComponent<MeshRenderer>();
            }
        }

        /// <summary>
        /// Builds (once) a shared circle mesh in the XZ plane with normal +Y.
        /// Unit radius (1m) so callers can scale by aoeRadius directly.
        /// Triangle fan from a centre vertex out to N rim vertices —
        /// 48 segments at radius 1 reads as a smooth circle and is
        /// negligible in render cost.
        /// </summary>
        private static void EnsureSharedCircleMesh()
        {
            if (s_sharedCircleMesh != null) return;

            const int segments = 48;
            var mesh = new Mesh { name = "GrenadeTelegraph_Circle" };

            var verts = new Vector3[segments + 1];
            var uvs   = new Vector2[segments + 1];
            var tris  = new int[segments * 3];

            // Centre vertex.
            verts[0] = Vector3.zero;
            uvs[0]   = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a);
                float s = Mathf.Sin(a);
                verts[i + 1] = new Vector3(c, 0f, s);          // unit-radius rim point in XZ
                uvs[i + 1]   = new Vector2(c * 0.5f + 0.5f,    // map [-1,1] → [0,1] for texture sampling
                                            s * 0.5f + 0.5f);
            }

            // Triangle fan — each rim point connects to the next + centre.
            // Winding chosen so the mesh's normal faces +Y (visible from above).
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = next + 1;
                tris[i * 3 + 2] = i + 1;
            }

            mesh.vertices  = verts;
            mesh.uv        = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            s_sharedCircleMesh = mesh;
        }

        private void ApplyStyleToLineRenderers()
        {
            if (style == null) return;
            if (arcLine != null)
            {
                if (style.arcMaterial != null) arcLine.sharedMaterial = style.arcMaterial;
                else if (arcLine.sharedMaterial == null) arcLine.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                arcLine.widthCurve = style.arcWidth;
            }
            if (ringLine != null)
            {
                if (style.ringMaterial != null) ringLine.sharedMaterial = style.ringMaterial;
                else if (ringLine.sharedMaterial == null) ringLine.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                ringLine.widthCurve = style.ringWidth;
            }
        }

        private void ApplyFallbackLineRendererConfig()
        {
            // No style assigned — give the LineRenderers sane defaults so
            // the telegraph is visible without designer setup.
            if (arcLine != null && arcLine.sharedMaterial == null)
            {
                arcLine.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                arcLine.widthCurve = AnimationCurve.Linear(0f, 0.08f, 1f, 0.04f);
            }
            if (ringLine != null && ringLine.sharedMaterial == null)
            {
                ringLine.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                ringLine.widthCurve = AnimationCurve.Constant(0f, 1f, 0.08f);
            }
        }

        private static void ConfigureLineRendererCommon(LineRenderer lr)
        {
            lr.useWorldSpace        = true;
            lr.alignment            = LineAlignment.View;
            lr.textureMode          = LineTextureMode.Stretch;
            lr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows       = false;
            lr.lightProbeUsage      = UnityEngine.Rendering.LightProbeUsage.Off;
            lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            lr.numCornerVertices    = 0;
            lr.numCapVertices       = 4;
            lr.widthMultiplier      = 1f;
        }
    }
}

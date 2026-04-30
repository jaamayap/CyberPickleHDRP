// File: Assets/_CyberPickle/Code/DOTS/Authoring/HitVFXAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Authors a hit-VFX prefab into a baked entity prefab with:
//   - Prefab marker (so it stays inactive in the world until Instantiate)
//   - Lifetime (so it self-destructs after the particle burst plays out)
//
// Visuals are provided by the GameObject hierarchy itself (Hovl Studio's
// Hit-X particles), kept alive at runtime via Entities Graphics' Companion
// GameObject mechanism — Lifetime governs when the entity (and its
// companion) are destroyed.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class HitVFXAuthoring : MonoBehaviour
    {
        [Tooltip("How long the hit VFX entity lives before self-destructing. Should match the duration of the particle burst.")]
        public float lifetime = 1.0f;

        public class Baker : Baker<HitVFXAuthoring>
        {
            public override void Bake(HitVFXAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                // NOTE: do NOT manually AddComponent<Prefab>. ProjectileAuthoring's
                // baker calls GetEntity(hitVFXPrefab, flags) which handles Prefab
                // marker + LinkedEntityGroup automatically.
                AddComponent(entity, new Lifetime { Remaining = authoring.lifetime });
            }
        }
    }
}

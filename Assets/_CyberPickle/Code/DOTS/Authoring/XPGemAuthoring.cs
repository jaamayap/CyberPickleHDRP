// File: Assets/_CyberPickle/Code/DOTS/Authoring/XPGemAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Add this MonoBehaviour to every XP gem prefab (one per tier). At bake
// time it stamps the entity with the gameplay components needed by the
// magnet/collection system:
//
//   XPGemTag       — marker the systems query on
//   XPGemValue     — XP awarded on collection (default 1; the registry
//                    overwrites it per-tier when spawning, so any value
//                    here just acts as a fallback)
//   XPGemVelocity  — runtime motion vector (zeroed)
//
// The prefab's own MeshRenderer / MeshFilter / material are baked by the
// standard entities-graphics path — no explicit authoring needed for them.

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class XPGemAuthoring : MonoBehaviour
    {
        [Tooltip("Default XP value if the spawner doesn't override it. The registry stamps the tier's actual value at spawn time, so this is just a sane fallback for solo prefab testing.")]
        [Min(1)] public int defaultXPValue = 1;

        public class Baker : Baker<XPGemAuthoring>
        {
            public override void Bake(XPGemAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<XPGemTag>(entity);
                AddComponent(entity, new XPGemValue { Value = authoring.defaultXPValue });
                AddComponent(entity, new XPGemVelocity { Value = float3.zero });
            }
        }
    }
}

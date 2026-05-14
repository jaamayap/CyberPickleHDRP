// File: Assets/_CyberPickle/Code/DOTS/Authoring/XPGemAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Add this MonoBehaviour to every XP gem prefab (one per tier). At bake
// time it stamps the entity with the gameplay components needed by the
// magnet/collection system:
//
//   XPGemTag       — marker the systems query on
//   XPGemValue     — XP awarded on collection (sentinel value 1; the
//                    registry overwrites it with the tier table's value
//                    at spawn time, so the inline value is effectively
//                    unused in normal gameplay)
//   XPGemVelocity  — runtime motion vector (zeroed)
//
// 2026-05-12: removed `defaultXPValue` Inspector field. The XPGemTierTableSO
// (referenced by XPGemRegistryAuthoring) is now the only source of truth
// for tier XP values. The bake here writes a sentinel of 1 so a gem dragged
// into a scene for solo testing isn't worth 0; in the normal kill→drop
// flow the registry stamps the real value over this on instantiation.

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class XPGemAuthoring : MonoBehaviour
    {
        // Sentinel value. Overwritten at runtime by EnemyDeathSystem.SpawnGem
        // using the tier value from XPGemTierTableSO. Don't add a designer
        // field here — keeping data on the SO is the whole point of the
        // refactor.
        private const int SentinelXPValue = 1;

        public class Baker : Baker<XPGemAuthoring>
        {
            public override void Bake(XPGemAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<XPGemTag>(entity);
                AddComponent(entity, new XPGemValue { Value = SentinelXPValue });
                AddComponent(entity, new XPGemVelocity { Value = float3.zero });
            }
        }
    }
}

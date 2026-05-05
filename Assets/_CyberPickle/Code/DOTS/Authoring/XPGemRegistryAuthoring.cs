// File: Assets/_CyberPickle/Code/DOTS/Authoring/XPGemRegistryAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Multi-tier XP gem prefab registry. Place this on a GameObject in
// EnemisSubScene (alongside EnemyPrefabRegistryAuthoring) and assign
// the 5 tier configurations. The Baker registers each gemPrefab as a
// baked Prefab-tagged entity and adds an XPGemPrefabBufferElement entry
// to the registry singleton.
//
// EnemyDeathSystem uses this buffer to look up which prefab to
// Instantiate when a kill rolls a particular tier.
//
// Tier order matters: index 0 = Data Fragment (trash green), index 4 =
// Sentinel Core (orange jackpot). Death roll cascade walks 4 -> 0.

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class XPGemRegistryAuthoring : MonoBehaviour
    {
        [Tooltip("5 entries — one per tier (index 0..4). Tier 0 is the trash-drop fallback (always something); Tier 4 is the rare jackpot. Each entry pairs a prefab with its XP value.")]
        public TierConfig[] tiers = new TierConfig[5];

        [System.Serializable]
        public class TierConfig
        {
            [Tooltip("Designer label, shown in the Inspector. No runtime effect.")]
            public string displayName = "Tier";

            [Tooltip("Visible color for this tier — used by editor previews and as a quick reference for the prefab's emissive color. Doesn't directly tint the runtime material; that's set on the prefab itself.")]
            public Color tierColor = Color.white;

            [Tooltip("XP awarded when a gem of this tier is collected.")]
            [Min(1)] public int xpValue = 1;

            [Tooltip("Entity authoring prefab — small mesh + emissive material baked into entities-graphics rendering. No GameObject visual / SkinnedMeshRenderer / Animator needed; gems are pure ECS entities for performance.")]
            public GameObject gemPrefab;
        }

        public class Baker : Baker<XPGemRegistryAuthoring>
        {
            public override void Bake(XPGemRegistryAuthoring authoring)
            {
                if (authoring.tiers == null || authoring.tiers.Length == 0)
                {
                    Debug.LogWarning($"[XPGemRegistryAuthoring] '{authoring.name}' has no tier entries — XP gems will not spawn.", authoring);
                    return;
                }

                Entity self = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<XPGemPrefabBufferElement>(self);

                for (int tier = 0; tier < authoring.tiers.Length; tier++)
                {
                    var entry = authoring.tiers[tier];
                    if (entry == null)
                    {
                        Debug.LogWarning($"[XPGemRegistryAuthoring] '{authoring.name}': tier {tier} entry is null.", authoring);
                        continue;
                    }
                    if (entry.gemPrefab == null)
                    {
                        Debug.LogWarning($"[XPGemRegistryAuthoring] '{authoring.name}': tier {tier} '{entry.displayName}' has no gemPrefab — skipped.", authoring);
                        continue;
                    }

                    Entity prefabEntity = GetEntity(entry.gemPrefab, TransformUsageFlags.Dynamic);
                    buffer.Add(new XPGemPrefabBufferElement
                    {
                        Tier     = tier,
                        Prefab   = prefabEntity,
                        XPValue  = entry.xpValue,
                    });
                }
            }
        }
    }
}

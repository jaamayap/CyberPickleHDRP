// File: Assets/_CyberPickle/Code/DOTS/Authoring/XPGemRegistryAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Multi-tier XP gem prefab registry. Place this on a GameObject in
// EnemisSubScene (alongside EnemyPrefabRegistryAuthoring) and assign a
// single XPGemTierTableSO. That SO is the entire configuration — XP
// values, colors, display names, AND per-tier prefab refs all live there.
//
// The Baker iterates the SO's tier entries, registers each prefab as a
// baked Prefab-tagged entity, and writes the XPGemPrefabBufferElement
// buffer on this singleton. EnemyDeathSystem reads that buffer at runtime
// to look up which prefab to Instantiate when a kill rolls a particular
// tier.
//
// Why we can put prefab refs on an SO (and why this Baker is the only
// place that has to care): Bakers can resolve GameObject references no
// matter how they reach them — direct field, SO field, doesn't matter.
// `GetEntity(prefab)` takes any UnityEngine.GameObject. The only thing we
// must remember is DependsOn(prefab) for each prefab in the SO; that
// makes the SubScene re-bake when a prefab is edited (DependsOn(so) only
// catches changes to the SO itself, not to assets it references).
//
// 2026-05-12: refactored — prefabs moved from this authoring's inline
// array onto XPGemTierTableSO. The authoring is now a thin SO ref.
// Variant tables (BossStage, Halloween, etc.) can swap entire visual sets
// + values with a single drag.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Progression;

namespace CyberPickle.DOTS.Authoring
{
    public class XPGemRegistryAuthoring : MonoBehaviour
    {
        [Tooltip("The tier table SO — single source of truth for XP values, colors, display names, AND per-tier prefab references. REQUIRED. Create via Assets → Create → CyberPickle → XP → Tier Table.")]
        public XPGemTierTableSO tierTable;

        public class Baker : Baker<XPGemRegistryAuthoring>
        {
            public override void Bake(XPGemRegistryAuthoring authoring)
            {
                if (authoring.tierTable == null)
                {
                    Debug.LogError($"[XPGemRegistryAuthoring] '{authoring.name}' has no XPGemTierTableSO assigned — XP gems will not spawn. Assign a tier table SO and try again.", authoring);
                    return;
                }

                // Re-bake when the SO itself changes (xpValue edits, color, etc.).
                // Prefab-content changes are covered by DependsOn(prefab) below.
                DependsOn(authoring.tierTable);

                var tiers = authoring.tierTable.tiers;
                if (tiers == null || tiers.Length == 0)
                {
                    Debug.LogWarning($"[XPGemRegistryAuthoring] '{authoring.name}': tier table has no entries — XP gems will not spawn.", authoring);
                    return;
                }

                Entity self = GetEntity(TransformUsageFlags.None);
                var buffer = AddBuffer<XPGemPrefabBufferElement>(self);

                for (int tier = 0; tier < tiers.Length; tier++)
                {
                    var entry = tiers[tier];
                    if (entry == null)
                    {
                        Debug.LogWarning($"[XPGemRegistryAuthoring] '{authoring.name}': tier {tier} entry is null in the SO — skipped.", authoring);
                        continue;
                    }
                    if (entry.gemPrefab == null)
                    {
                        Debug.LogWarning($"[XPGemRegistryAuthoring] '{authoring.name}': tier {tier} '{entry.displayName}' has no gemPrefab — skipped. Drops rolling this tier will silently produce nothing.", authoring);
                        continue;
                    }

                    // Re-bake this SubScene when this specific prefab is edited.
                    // Critical: without this, designers editing T3's gem mesh
                    // wouldn't see the change in entities until a manual bake.
                    DependsOn(entry.gemPrefab);

                    Entity prefabEntity = GetEntity(entry.gemPrefab, TransformUsageFlags.Dynamic);
                    buffer.Add(new XPGemPrefabBufferElement
                    {
                        Tier    = tier,
                        Prefab  = prefabEntity,
                        XPValue = entry.xpValue,
                    });
                }
            }
        }
    }
}

// File: Assets/_CyberPickle/Code/Gameplay/Progression/XPGemTierTableSO.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Single source of truth for an XP gem tier — display name, color, XP value,
// AND visual prefab. Referenced by XPGemRegistryAuthoring, whose authoring
// component becomes a thin wrapper holding just this SO reference.
//
// Why everything (including prefab refs) lives here:
//   1. ONE file per tier configuration. Designer balances xpValue, picks
//      color, and assigns the visual all in one Inspector view — no
//      bouncing between SO + scene authoring component.
//   2. Variant tables are now free. A "BossStage" or "HalloweenEvent"
//      table can point at different prefabs with the same XP economics
//      (or vice-versa). The registry just swaps which SO it loads.
//   3. Diffable / versionable — git shows "T3 xpValue 30 → 50" or
//      "T4 prefab → new gold variant" as one-line changes on a .asset.
//
// DOTS baking consideration: when the Baker references a prefab via this
// SO, it must call DependsOn(prefab) on each tier's gemPrefab so the
// SubScene re-bakes when a prefab is edited (DependsOn(so) alone catches
// SO changes but NOT changes inside the prefabs the SO references).
// XPGemRegistryAuthoring's Baker handles this.
//
// Tier count is whatever the inspector array length is (default 6: T0
// trash → T5 Sentinel Prime jackpot). Indexes line up 1:1 with the tier
// numbers consumed by EnemyXPDropChances cascade and the registry buffer.

using System;
using UnityEngine;

namespace CyberPickle.Gameplay.Progression
{
    [CreateAssetMenu(fileName = "XPGemTierTable", menuName = "CyberPickle/XP/Tier Table", order = 1)]
    public class XPGemTierTableSO : ScriptableObject
    {
        [Tooltip("One entry per tier, index 0..N. Index 0 = trash fallback (always something drops), highest index = ultra-rare jackpot. Default count = 6 (T0..T5).")]
        public TierConfig[] tiers = new TierConfig[6];

        [Serializable]
        public class TierConfig
        {
            [Tooltip("Designer label, shown in the Inspector + used as a fallback when generating display names in pickup banners. No serialized runtime effect beyond UI.")]
            public string displayName = "Tier";

            [Tooltip("Reference color for this tier — drives in-world emissive tint, pickup banner accent, and any UI element that needs the tier's signature color.")]
            public Color tierColor = Color.white;

            [Tooltip("XP awarded when a gem of this tier is collected. Read by the registry baker and stamped onto the spawned gem's XPGemValue at runtime.")]
            [Min(1)] public int xpValue = 1;

            [Tooltip("The prefab spawned when a kill rolls this tier. Authored as a GameObject prefab with XPGemAuthoring + visual mesh + emissive material. The registry Baker bakes this into an Entity prefab and DependsOn() it so SubScene re-bakes on prefab edits.")]
            public GameObject gemPrefab;
        }

        /// <summary>
        /// Safe accessor — returns a fallback config when the index is out of
        /// range or the entry is null. Keeps callers from having to null-check
        /// individual fields in hot paths. The fallback has no prefab, so
        /// drops rolling out-of-range tiers produce nothing (intentional —
        /// failing silently is better than spawning a broken gem).
        /// </summary>
        public TierConfig GetTier(int index)
        {
            if (tiers == null || index < 0 || index >= tiers.Length || tiers[index] == null)
            {
                return new TierConfig { displayName = $"Tier {index}", tierColor = Color.white, xpValue = 1, gemPrefab = null };
            }
            return tiers[index];
        }

        /// <summary>Total tier count (array length). Useful for cascade roll sanity-checks.</summary>
        public int TierCount => tiers != null ? tiers.Length : 0;

        private void OnValidate()
        {
            if (tiers == null) return;
            for (int i = 0; i < tiers.Length; i++)
            {
                if (tiers[i] == null) continue;
                if (tiers[i].xpValue < 1) tiers[i].xpValue = 1;
            }
        }
    }
}

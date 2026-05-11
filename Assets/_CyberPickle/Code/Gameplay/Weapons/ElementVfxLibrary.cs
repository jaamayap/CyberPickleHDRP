// File: Assets/_CyberPickle/Code/Gameplay/Weapons/ElementVfxLibrary.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Single global SO mapping ElementId → (flashPrefab, projectilePrefab,
// hitPrefab). At fire time, WeaponFiring picks the trio matching the
// weapon's currently-coupled element (driven by the power-up on the
// same loadout axis, per M8 element coupling).
//
// Why centralize: 4 weapons × 8 elements = 32 visual variants. Authoring
// 32 separate WeaponData.projectilePrefab fields per weapon is a
// maintenance nightmare — and would break the "same projectile, different
// scale per weapon" intent from the chat 2026-05-11 design. The library
// holds the 8 element variants once; per-weapon scaling lives on
// WeaponData (muzzleFlashScale / projectileScale / hitVfxScale).
//
// Loading: the asset MUST live under a Resources folder so the static
// Instance property can find it at runtime without manual wiring on every
// WeaponFiring. Convention: Assets/_CyberPickle/Resources/ElementVfxLibrary.asset.
// Falls back to null gracefully if the asset is missing — WeaponFiring
// treats null library as "spawn nothing" (silent — no errors).

using System;
using UnityEngine;
using CyberPickle.Core;

namespace CyberPickle.Gameplay.Weapons
{
    [CreateAssetMenu(menuName = "CyberPickle/Gameplay/Element VFX Library", fileName = "ElementVfxLibrary")]
    public class ElementVfxLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("Which element this trio covers. The library has one entry per ElementId; lookups by element find the entry with the matching id.")]
            public ElementId element;

            [Tooltip("Spawned at the weapon's muzzle on fire. One-shot — auto-destroys after its particle systems finish.")]
            public GameObject flashPrefab;

            [Tooltip("The visual projectile spawned per shot. Hovl AAA projectile prefab (with embedded particle systems).")]
            public GameObject projectilePrefab;

            [Tooltip("Spawned at the projectile's impact point on collision. Scaled by hitVfxScale × damage / crit / AoE (M9 PR F).")]
            public GameObject hitPrefab;
        }

        [Tooltip("One entry per ElementId. Should contain 8 entries (None + 7 elements). Missing entries fall through to the first entry (typically None / neutral chrome).")]
        public Entry[] entries = new Entry[8];

        // ─── Static lookup ────────────────────────────────────────────────

        private const string ResourcesPath = "ElementVfxLibrary";

        private static ElementVfxLibrary _cached;
        private static bool _lookedUp;

        /// <summary>
        /// Auto-loaded singleton. The asset lives under a Resources folder
        /// at <c>Resources/ElementVfxLibrary.asset</c>. Returns null if the
        /// asset is missing — callers should null-check and treat as "no
        /// library configured" (spawn no flash / use weaponData fallback).
        /// </summary>
        public static ElementVfxLibrary Instance
        {
            get
            {
                if (!_lookedUp)
                {
                    _cached = Resources.Load<ElementVfxLibrary>(ResourcesPath);
                    _lookedUp = true;
                    if (_cached == null)
                    {
                        Debug.LogWarning(
                            "[ElementVfxLibrary] No ElementVfxLibrary asset found at " +
                            "Resources/ElementVfxLibrary. Per-element VFX won't spawn. " +
                            "Create one via Assets → Create → CyberPickle → Gameplay → Element VFX Library, " +
                            "place it under a Resources folder, and name it 'ElementVfxLibrary'.");
                    }
                }
                return _cached;
            }
        }

        /// <summary>
        /// Editor-only: clear the cached singleton so subsequent <see cref="Instance"/>
        /// calls re-load from Resources. Useful when authoring the asset
        /// during play mode.
        /// </summary>
        public static void ClearCache()
        {
            _cached = null;
            _lookedUp = false;
        }

        // ─── Lookup ────────────────────────────────────────────────────────

        /// <summary>
        /// Get the VFX trio for an element. Falls back to the first entry
        /// (typically <see cref="ElementId.None"/>) when no match exists.
        /// O(8) linear search — fine, the array is tiny.
        /// </summary>
        public Entry Get(ElementId element)
        {
            if (entries == null || entries.Length == 0) return default;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].element == element) return entries[i];
            }
            // Fallback to first entry (the "Neutral" slot).
            return entries[0];
        }

        // ─── Editor validation ────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Warn on duplicate element entries — designers should have one
            // entry per ElementId, not two Fire entries.
            if (entries == null) return;
            var seen = new System.Collections.Generic.HashSet<ElementId>();
            for (int i = 0; i < entries.Length; i++)
            {
                if (!seen.Add(entries[i].element))
                {
                    Debug.LogWarning(
                        $"[ElementVfxLibrary] Duplicate entry for element '{entries[i].element}' at index {i}. " +
                        $"Only the first occurrence will be returned by Get().",
                        this);
                }
            }
        }
#endif
    }
}

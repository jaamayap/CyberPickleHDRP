// File: Assets/_CyberPickle/Code/DOTS/Authoring/ProjectilePrefabSetupAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Place this on a single empty GameObject inside a SubScene attached to
// the Game scene. Drag a projectile prefab into the entry for each
// element. At bake time, GetEntity recursively bakes each prefab + its
// children (LinkedEntityGroup populated) and adds them to a
// DynamicBuffer<ProjectilePrefabEntry> on the setup entity.
//
// 2026-05-11 (M9 PR B follow-up): migrated from single-prefab to per-
// element buffer. The old projectilePrefab field is kept (deprecated) so
// existing SubScenes that still reference it bake to the buffer as the
// None entry. New SubScenes should use the entries array.
//
// WeaponFiring reads the buffer at runtime and picks the entry matching
// the weapon's currently-coupled ElementId, falling back to None.

using Unity.Entities;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class ProjectilePrefabSetupAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct ElementEntry
        {
            [Tooltip("Which element this prefab represents. Mirrors ElementVfxLibrary.")]
            public ElementId element;

            [Tooltip("The projectile prefab to bake for this element (a regular project asset — NOT placed in this SubScene).")]
            public GameObject prefab;
        }

        [Header("Per-element projectiles (M9 PR B+)")]
        [Tooltip(
            "One entry per ElementId. Ideally 8 (None + 7 elements). Each prefab " +
            "is baked at SubScene-bake time into the DynamicBuffer<ProjectilePrefabEntry> " +
            "the WeaponFiring code reads at fire time. Mirror the entries in " +
            "ElementVfxLibrary so the projectile and the muzzle flash/hit visuals " +
            "are consistent per element.")]
        public ElementEntry[] entries;

        [Header("Legacy (pre-PR B follow-up)")]
        [Tooltip(
            "LEGACY single-prefab field. Used as the None / fallback entry when " +
            "the entries array above is empty. New SubScenes should populate " +
            "entries and leave this empty.")]
        public GameObject projectilePrefab;

        public class Baker : Baker<ProjectilePrefabSetupAuthoring>
        {
            public override void Bake(ProjectilePrefabSetupAuthoring authoring)
            {
                // The setup GameObject doesn't need transform sync — it's just a holder.
                Entity setupEntity = GetEntity(TransformUsageFlags.None);

                // ─── Per-element buffer ──────────────────────────────────
                var buffer = AddBuffer<ProjectilePrefabEntry>(setupEntity);

                if (authoring.entries != null)
                {
                    foreach (var entry in authoring.entries)
                    {
                        if (entry.prefab == null) continue;
                        var prefabEntity = GetEntity(entry.prefab, TransformUsageFlags.Dynamic);
                        buffer.Add(new ProjectilePrefabEntry
                        {
                            Element = (byte)entry.element,
                            Prefab  = prefabEntity,
                        });
                    }
                }

                // ─── Legacy single-prefab fallback ───────────────────────
                // If the legacy field is set, also register it as the None
                // entry (so old SubScenes bake to the new buffer model
                // without re-authoring). Also kept on the legacy
                // ProjectilePrefabHolder singleton for back-compat.
                Entity legacyEntity = (authoring.projectilePrefab != null)
                    ? GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(setupEntity, new ProjectilePrefabHolder { Value = legacyEntity });

                // If the buffer doesn't already have a None entry and the
                // legacy field is set, register the legacy prefab as None.
                if (legacyEntity != Entity.Null)
                {
                    bool hasNone = false;
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        if (buffer[i].Element == (byte)ElementId.None) { hasNone = true; break; }
                    }
                    if (!hasNone)
                    {
                        buffer.Add(new ProjectilePrefabEntry
                        {
                            Element = (byte)ElementId.None,
                            Prefab  = legacyEntity,
                        });
                    }
                }
            }
        }
    }
}

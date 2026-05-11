// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectilePrefabEntry.cs
// Namespace: CyberPickle.DOTS.Components
//
// Per-element baked projectile prefab. One DynamicBuffer<ProjectilePrefabEntry>
// lives on the setup entity that ProjectilePrefabSetupAuthoring's Baker
// creates — one element per buffer entry, ideally all 8 (None + Fire +
// Lightning + Ice + Earth + Plasma + Light + Dark).
//
// Looked up at WeaponFiring.Fire time: the weapon's currently-coupled
// ElementId (from the loadout axis's power-up) picks which baked entity
// to Instantiate. Falls back to ElementId.None / first entry when the
// element has no entry authored.
//
// Why a buffer (not 8 singletons or a struct-with-8-fields): bakers can
// only emit one component per type per entity, and we want this to scale
// with future element additions without changing the type signature.
// DynamicBuffer is the ECS-idiomatic answer.
//
// Element field stored as byte for compactness — it's the underlying
// representation of ElementId anyway.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    /// <summary>
    /// One (element, prefab-entity) pair in the per-element projectile
    /// registry. Stored as a DynamicBuffer on the setup entity created by
    /// <c>ProjectilePrefabSetupAuthoring</c>'s Baker.
    /// </summary>
    public struct ProjectilePrefabEntry : IBufferElementData
    {
        /// <summary>The element this prefab represents. Underlying byte of <c>CyberPickle.Core.ElementId</c>.</summary>
        public byte Element;

        /// <summary>The baked entity prefab to instantiate at fire time.</summary>
        public Entity Prefab;
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Components/VisualPrefabRef.cs
// Namespace: CyberPickle.DOTS.Components
//
// Holds a reference to the GameObject prefab that should be instantiated
// as the visible "visual" representation of this entity. Used by the
// hybrid bridge: the entity owns gameplay state (position, health, AI),
// while a separate GameObject owns the SkinnedMeshRenderer + Animator
// and follows the entity's LocalTransform each frame.
//
// UnityObjectRef<T> stores a stable lookup handle that survives
// serialization. It must be dereferenced from managed code only —
// EnemyVisualBindingSystem (a SystemBase) does this; never call .Value
// from inside a Burst job.

using Unity.Entities;
using UnityEngine;

namespace CyberPickle.DOTS.Components
{
    public struct VisualPrefabRef : IComponentData
    {
        public UnityObjectRef<GameObject> Value;
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Components/HasVisualTag.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker added to an entity once its hybrid GameObject visual has been
// instantiated and registered with the EnemyVisualBridge. The visual
// binding system uses absence of this tag to identify entities that
// still need a visual spawned this frame.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct HasVisualTag : IComponentData { }
}

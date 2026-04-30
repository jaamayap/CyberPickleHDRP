// File: Assets/_CyberPickle/Code/DOTS/Components/EnemyTag.cs
// Namespace: CyberPickle.DOTS.Components
//
// Empty marker component — flags an entity as an enemy. Used by systems
// that should iterate only enemy entities (movement, damage, AI, etc.).

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct EnemyTag : IComponentData { }
}

// File: Assets/_CyberPickle/Code/DOTS/Components/ProjectileTag.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker component flagging an entity as a projectile. Movement /
// lifetime / collision systems iterate entities with this tag.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct ProjectileTag : IComponentData { }
}

// File: Assets/_CyberPickle/Code/DOTS/Components/Dead.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker for an entity that has died. Switches the entity from
// "kinematic-driven by EnemyMovementSystem" to "free physics body":
// movement / AI systems exclude entities with this tag, while the
// physics body (now with unlocked rotation and full gravity) tumbles
// to the ground and rests there.
//
// The visual stays bound via EnemyVisualBridge — the corpse falls and
// tumbles, the death animation plays during the fall, and the visual
// follows the entity transform through the whole sequence.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct Dead : IComponentData { }
}

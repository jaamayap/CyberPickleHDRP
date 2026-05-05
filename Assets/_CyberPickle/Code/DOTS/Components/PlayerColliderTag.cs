// File: Assets/_CyberPickle/Code/DOTS/Components/PlayerColliderTag.cs
// Namespace: CyberPickle.DOTS.Components
//
// Marker for the kinematic "player collider proxy" entity. The proxy
// is a body authored in EnemisSubScene that gets teleported to the
// player's MonoBehaviour position every frame by PlayerColliderSyncSystem.
// Its sole purpose is to act as a physics obstacle so swarm zombies stop
// at the player's footprint instead of all converging to the player's
// exact XYZ point and stacking up.

using Unity.Entities;

namespace CyberPickle.DOTS.Components
{
    public struct PlayerColliderTag : IComponentData { }
}

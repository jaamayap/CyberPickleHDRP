// File: Assets/_CyberPickle/Code/DOTS/Authoring/PlayerColliderProxyAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Tags a baked entity as the player collider proxy. Place this on a
// GameObject in EnemisSubScene that ALSO carries a PhysicsShapeAuthoring
// (capsule for the player's footprint) and a PhysicsBodyAuthoring with
// Motion Type = Kinematic.
//
// At bake time this just adds PlayerColliderTag — the physics components
// are baked by Unity Physics's authoring path on the same GameObject.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class PlayerColliderProxyAuthoring : MonoBehaviour
    {
        public class Baker : Baker<PlayerColliderProxyAuthoring>
        {
            public override void Bake(PlayerColliderProxyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerColliderTag>(entity);
            }
        }
    }
}

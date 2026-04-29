// File: Assets/_CyberPickle/Code/DOTS/Components/PlayerPositionData.cs
// Namespace: CyberPickle.DOTS.Components
//
// Singleton component holding the player's world position. Updated each
// frame by PlayerPositionBridge (a MonoBehaviour on the player).
// ECS systems read this to make decisions relative to the player —
// enemy AI, projectile homing, magnetic-pickup attraction, etc.
//
// Why a singleton instead of querying the player each frame from ECS:
//   - The player is a MonoBehaviour (hybrid architecture), not an entity
//   - ECS systems can't directly query MonoBehaviours from a Burst job
//   - Singleton = one entity holds the data, all systems read from it.
//     Cheap O(1) access, Burst-compatible.

using Unity.Entities;
using Unity.Mathematics;

namespace CyberPickle.DOTS.Components
{
    public struct PlayerPositionData : IComponentData
    {
        public float3 Position;
    }
}

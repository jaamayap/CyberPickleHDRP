// File: Assets/_CyberPickle/Code/DOTS/Systems/PlayerColliderSyncSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Drives the kinematic player collider proxy. Each frame:
//   1. Reads the player's world position from PlayerPositionData (written
//      by PlayerPositionBridge on the MonoBehaviour player).
//   2. Snaps the proxy entity's LocalTransform.Position to that value.
//   3. Computes the implied linear velocity from the position delta and
//      writes it into PhysicsVelocity. This gives the kinematic body a
//      "real" velocity so dynamic zombies it intersects get pushed when
//      the player walks through them — not just blocked.
//
// Run order: BEFORE PhysicsSystemGroup so the position + velocity we
// write are what the simulation steps with this frame.

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial struct PlayerColliderSyncSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerPositionData>();
            state.RequireForUpdate<PlayerColliderTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float3 playerPos = SystemAPI.GetSingleton<PlayerPositionData>().Position;
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, velocity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>>()
                              .WithAll<PlayerColliderTag>())
            {
                float3 oldPos = transform.ValueRO.Position;

                // Implied velocity from position delta — kinematic bodies need
                // a non-zero velocity field for the contact constraint to push
                // dynamic bodies in their path. Without this, zombies just
                // stop dead at the proxy without being shoved aside.
                float3 inferred = deltaTime > 1e-5f
                    ? (playerPos - oldPos) / deltaTime
                    : float3.zero;

                transform.ValueRW.Position = playerPos;
                velocity.ValueRW.Linear = inferred;
                velocity.ValueRW.Angular = float3.zero;
            }
        }
    }
}

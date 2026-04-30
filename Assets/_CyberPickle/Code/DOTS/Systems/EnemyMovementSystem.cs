// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyMovementSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that moves all entities tagged as EnemyTag toward
// the player's current position each frame. Reads PlayerPositionData
// singleton (written by PlayerPositionBridge from the MonoBehaviour side).
//
// First-iteration implementation: sequential foreach. When enemy counts
// climb into the hundreds, refactor to IJobEntity for parallel execution
// — same logic, different scheduling.

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Don't tick the system unless prerequisites exist:
            // - the player position singleton
            // - at least one enemy
            state.RequireForUpdate<PlayerPositionData>();
            state.RequireForUpdate<EnemyTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float3 playerPos = SystemAPI.GetSingleton<PlayerPositionData>().Position;

            foreach (var (transform, speed) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>().WithAll<EnemyTag>())
            {
                float3 toPlayer = playerPos - transform.ValueRO.Position;

                // Project onto XZ plane — enemies don't fly toward the player vertically.
                toPlayer.y = 0f;

                float distSq = math.lengthsq(toPlayer);
                if (distSq < 0.0001f) continue; // already on top of the player

                float3 dir = math.normalize(toPlayer);
                transform.ValueRW.Position += dir * speed.ValueRO.Value * deltaTime;

                // Face the player so future visual upgrades (animation, weapons) read forward correctly.
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(dir, math.up());
            }
        }
    }
}

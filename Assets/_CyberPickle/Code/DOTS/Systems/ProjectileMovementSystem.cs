// File: Assets/_CyberPickle/Code/DOTS/Systems/ProjectileMovementSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that advances all active projectile entities
// along their velocity each tick. Prefab entities are excluded
// automatically (they have the Prefab component, which the default
// query excludes).

using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    public partial struct ProjectileMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, velocity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<ProjectileVelocity>>().WithAll<ProjectileTag>())
            {
                transform.ValueRW.Position += velocity.ValueRO.Value * deltaTime;
            }
        }
    }
}

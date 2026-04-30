// File: Assets/_CyberPickle/Code/DOTS/Systems/LifetimeSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that decrements Lifetime.Remaining for every
// entity that has it, and destroys entities whose lifetime has expired.
// Generic — works for projectiles, hit VFX, muzzle flashes, currency
// drops, etc. Anything transient with a fixed display duration.

using Unity.Burst;
using Unity.Entities;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    public partial struct LifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Lifetime>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (lifetime, entity) in
                     SystemAPI.Query<RefRW<Lifetime>>().WithEntityAccess())
            {
                lifetime.ValueRW.Remaining -= deltaTime;
                if (lifetime.ValueRW.Remaining <= 0f)
                {
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyDeathSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that destroys enemy entities whose Health has
// dropped to zero or below. Separated from ProjectileCollisionSystem so
// future damage sources (DOT, AOE, environmental hazards, contact damage,
// boss attacks, etc.) all funnel through the same death path.
//
// When we add drops (currency, XP), they'll be spawned here on death.

using Unity.Burst;
using Unity.Entities;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    public partial struct EnemyDeathSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (health, entity) in
                     SystemAPI.Query<RefRO<Health>>().WithAll<EnemyTag>().WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0f)
                {
                    // TODO (later milestone): spawn currency / XP drop entities here
                    // before destroying.
                    ecb.DestroyEntity(entity);
                }
            }
        }
    }
}

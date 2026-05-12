// File: Assets/_CyberPickle/Code/DOTS/Systems/LifetimeSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that decrements Lifetime.Remaining for every
// entity that has it, and destroys entities whose lifetime has expired.
// Generic — works for hit VFX, muzzle flashes, currency drops, straight
// projectiles, etc. Anything transient with a fixed display duration.
//
// EXCLUDES entities with ProjectileAoE — those are owned by
// ProjectileExplosionSystem, which handles their lifetime decrement and
// triggers an AoE damage pass + dying transition on expiry (instead of
// just destroying them silently).

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

            // WithNone<ProjectileAoE>: hand off AoE projectile lifecycle to
            // ProjectileExplosionSystem (which decrements + detonates +
            // transitions to dying). Without this, both systems decrement
            // the same Lifetime each frame → grenade explodes in half its
            // intended time AND/OR LifetimeSystem destroys the entity
            // before ExplosionSystem can detonate it.
            foreach (var (lifetime, entity) in
                     SystemAPI.Query<RefRW<Lifetime>>()
                              .WithNone<ProjectileAoE>()
                              .WithEntityAccess())
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

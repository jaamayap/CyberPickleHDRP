// File: Assets/_CyberPickle/Code/DOTS/Systems/ProjectileCollisionSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that checks each active projectile against all
// active enemies for proximity-based hits (single-target — first enemy in
// range is damaged, projectile is consumed).
//
// On hit:
//   - applies projectile damage to the enemy's Health (deferred via ECB,
//     so multiple hits in the same frame accumulate cleanly)
//   - spawns a HitVFX entity at the projectile's position (instantiates
//     the entity prefab carried in HitVFXPrefabRef on the projectile)
//   - destroys the projectile entity
//
// Enemy death is the responsibility of EnemyDeathSystem — this system
// only applies damage. Lifetime expiry of projectiles is the
// responsibility of LifetimeSystem.
//
// First-iteration: O(N*M) sequential check. When projectile/enemy counts
// climb (>500 each), refactor to spatial hashing or Unity Physics overlap
// queries. Same logic, different scheduling.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    public partial struct ProjectileCollisionSystem : ISystem
    {
        // Squared hit radius — projectile is considered to hit an enemy
        // when their LocalTransform positions are within this distance.
        private const float HitRadiusSq = 0.6f * 0.6f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Snapshot enemies into temp arrays for inner-loop access.
            // Exclude Dead so projectiles don't waste hits on corpses.
            EntityQuery enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, Health, LocalTransform>()
                .WithNone<Dead>()
                .Build();

            NativeArray<Entity> enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            if (enemyEntities.Length == 0)
            {
                enemyEntities.Dispose();
                enemyTransforms.Dispose();
                return;
            }

            foreach (var (projTransform, projDamage, hitVfxRef, projEntity) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ProjectileDamage>, RefRO<HitVFXPrefabRef>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                float3 projPos = projTransform.ValueRO.Position;

                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    // XZ-plane distance only — top-down survivors-like means
                    // Y differences (muzzle height vs enemy capsule center)
                    // shouldn't prevent hits. Hit detection is conceptually 2D.
                    float3 enemyPos = enemyTransforms[i].Position;
                    float dx = projPos.x - enemyPos.x;
                    float dz = projPos.z - enemyPos.z;
                    if (dx * dx + dz * dz > HitRadiusSq) continue;

                    Entity enemyEntity = enemyEntities[i];
                    Health health = SystemAPI.GetComponent<Health>(enemyEntity);
                    health.Current -= projDamage.ValueRO.Value;
                    ecb.SetComponent(enemyEntity, health);

                    // Spawn hit VFX entity at the projectile's position. The VFX prefab
                    // carries its own Lifetime + visuals (Hovl particle hierarchy via
                    // Companion GameObject) — LifetimeSystem destroys it when the burst
                    // plays out.
                    Entity vfxRef = hitVfxRef.ValueRO.Value;
                    if (vfxRef != Entity.Null)
                    {
                        Entity vfxInstance = ecb.Instantiate(vfxRef);
                        ecb.SetComponent(vfxInstance, LocalTransform.FromPositionRotation(
                            projPos, projTransform.ValueRO.Rotation));
                    }

                    // Projectile is consumed.
                    ecb.DestroyEntity(projEntity);
                    break;
                }
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }
    }
}

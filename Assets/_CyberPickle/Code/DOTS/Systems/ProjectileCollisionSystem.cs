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

        // RNG for crit rolls. Initialized in OnCreate; advanced each hit.
        private Unity.Mathematics.Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            // Burst-safe seed. System.Environment.TickCount is a managed BCL
            // property; calling it from a [BurstCompile] context works in the
            // Editor (Burst falls back to managed execution in some modes)
            // but CRASHES the player build with an access violation in
            // ProjectileCollisionSystem.__codegen__OnCreate before the first
            // frame renders. Random.CreateFromIndex hashes its input so any
            // uint is safe (including 0). state.GlobalSystemVersion is
            // available in Burst, non-zero, and varies enough across runs
            // to give different crit sequences without touching managed APIs.
            _random = Unity.Mathematics.Random.CreateFromIndex(state.GlobalSystemVersion);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Read player damage modifiers from PlayerStatsData singleton.
            // Power is treated as a percent bonus per GDD §2.4 — Power=10 → +10% damage,
            // Power=100 → +100% (×2), Power=300 → +300% (×4).
            // CritChance is a 0..1 probability; crits double damage (Mega Crit
            // breakpoint at >= 100% would 4×, implemented in M8 with the
            // breakpoint system).
            // Falls back to neutral multipliers when the stats singleton hasn't
            // been initialized yet.
            float power = 0f;
            float critChance = 0f;
            if (SystemAPI.HasSingleton<PlayerStatsData>())
            {
                var s = SystemAPI.GetSingleton<PlayerStatsData>();
                power = s.Power;
                critChance = s.CritChance;
            }
            float powerMultiplier = 1f + power * 0.01f;

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

                    // Apply damage formula: base × (1 + Power%) × critMultiplier.
                    // Element / weapon-upgrade / equipment / skill multipliers
                    // slot in here as those systems land (M8 / M9).
                    float critMultiplier = (_random.NextFloat() < critChance) ? 2f : 1f;
                    float finalDamage = projDamage.ValueRO.Value * powerMultiplier * critMultiplier;

                    health.Current -= finalDamage;
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

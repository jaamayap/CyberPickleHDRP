// File: Assets/_CyberPickle/Code/DOTS/Systems/ProjectileExplosionSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that detonates AoE projectiles (grenades) EXACTLY
// when their Lifetime expires — NOT on proximity collision with enemies.
// This is the "rhythm-locked grenade" design: the projectile is a timed
// bomb. Fire on a kick beat, lifetime = flightBeats × 60/BPM seconds,
// explode on the snare beat. Whether the grenade visually passes through
// enemies mid-flight is irrelevant — it commits to its scheduled
// detonation regardless.
//
// Why a separate system from ProjectileCollisionSystem:
//   - AoE projectiles don't use proximity collision at all (would cause
//     mid-flight detonations at apex when XZ aligns with an enemy below).
//   - ProjectileCollisionSystem explicitly SKIPS projectiles that have
//     ProjectileAoE (see the early-out at the top of its inner foreach).
//   - This system owns lifetime tracking + explosion for AoE alone.
//
// Why run BEFORE LifetimeSystem:
//   - LifetimeSystem destroys entities whose Lifetime.Remaining <= 0.
//     We need to INTERCEPT that destruction for AoE projectiles so we
//     can run the damage pass + transition to ProjectileDying (for the
//     fade visual) instead of just deleting the entity.
//   - On detonation, we REMOVE the Lifetime component so LifetimeSystem
//     doesn't double-destroy on the next tick.

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(LifetimeSystem))]
    public partial struct ProjectileExplosionSystem : ISystem
    {
        private Unity.Mathematics.Random _random;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileAoE>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _random = Unity.Mathematics.Random.CreateFromIndex(state.GlobalSystemVersion);
            UnityEngine.Debug.Log("<color=lime>[ProjectileExplosionSystem]</color> OnCreate — system registered. Will run when ProjectileAoE entities exist.");
        }

        // [BurstCompile] removed temporarily so we can log diagnostics
        // until the grenade timer-detonation is confirmed working.
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Player stats for damage formula.
            float power = 0f, critChance = 0f;
            if (SystemAPI.HasSingleton<PlayerStatsData>())
            {
                var s = SystemAPI.GetSingleton<PlayerStatsData>();
                power = s.Power;
                critChance = s.CritChance;
            }
            float powerMultiplier = 1f + power * 0.01f;

            // Hit-report queue for the Mono-side stats tracker + HitVfxApplier.
            bool queueExists = SystemAPI.HasSingleton<DamageReportQueueSingleton>();
            NativeQueue<DamageHitReport> reportQueue = default;
            if (queueExists)
                reportQueue = SystemAPI.GetSingleton<DamageReportQueueSingleton>().Queue;

            // Component lookups for the per-projectile attribution data.
            var sourceLookup   = SystemAPI.GetComponentLookup<ProjectileSource>(isReadOnly: true);
            var elementLookup  = SystemAPI.GetComponentLookup<WeaponElement>(isReadOnly: true);
            var velocityLookup = SystemAPI.GetComponentLookup<ProjectileVelocity>(isReadOnly: true);
            var hybridLookup   = SystemAPI.GetComponentLookup<ProjectileHasHybridVisual>(isReadOnly: true);

            // Enemy snapshot — same query shape as ProjectileCollisionSystem.
            EntityQuery enemyQuery = SystemAPI.QueryBuilder()
                .WithAll<EnemyTag, Health, LocalTransform>()
                .WithNone<Dead>()
                .Build();

            NativeArray<Entity> enemies = enemyQuery.ToEntityArray(Allocator.Temp);
            NativeArray<LocalTransform> enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            // Main loop: tick down each AoE projectile's lifetime; detonate
            // when zero.
            int aliveCount = 0;
            foreach (var (lifetime, transform, damage, aoe, projEntity) in
                     SystemAPI.Query<RefRW<Lifetime>, RefRO<LocalTransform>, RefRO<ProjectileDamage>, RefRO<ProjectileAoE>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                aliveCount++;
                lifetime.ValueRW.Remaining -= deltaTime;
                if (lifetime.ValueRO.Remaining > 0f) continue;

                UnityEngine.Debug.Log($"<color=lime>[ProjectileExplosionSystem]</color> DETONATING entity {projEntity.Index} at pos ({transform.ValueRO.Position.x:F1}, {transform.ValueRO.Position.y:F1}, {transform.ValueRO.Position.z:F1}) — Lifetime expired. AoE radius={aoe.ValueRO.Radius:F1}m.");

                // ── Detonation moment ──────────────────────────────────────
                float3 projPos = transform.ValueRO.Position;
                float radius = aoe.ValueRO.Radius;
                float radiusSq = radius * radius;

                // Gather per-projectile attribution data once per blast.
                FixedString64Bytes weaponId = default;
                if (sourceLookup.HasComponent(projEntity))
                    weaponId = sourceLookup[projEntity].WeaponId;

                byte element = 0;
                if (elementLookup.HasComponent(projEntity))
                    element = elementLookup[projEntity].Value;

                float3 hitDir = float3.zero;
                if (velocityLookup.HasComponent(projEntity))
                {
                    float3 v = velocityLookup[projEntity].Value;
                    float lenSq = math.lengthsq(v);
                    if (lenSq > 0.0001f) hitDir = v / math.sqrt(lenSq);
                }

                // AoE projectiles ALWAYS suppress per-enemy hit visuals.
                // The explosion visual is a SINGLE shot at the impact
                // point (via CyberPickleProjectileVisual.OnHit + the
                // prefab's authored `hit` GO). Each damaged enemy still
                // gets a DamageHitReport for stats attribution, but the
                // per-enemy HitVfxApplier path is silenced — without this
                // the user sees N tiny hit bursts (one per enemy) instead
                // of ONE big explosion at the epicenter.
                bool suppressHitVfx = true;

                // Radial damage pass — every enemy within Radius of the
                // grenade's current position takes damage. Per-target crit
                // roll (each enemy gets its own chance) for that chaotic
                // explosion feel.
                for (int j = 0; j < enemies.Length; j++)
                {
                    float3 enemyPos = enemyTransforms[j].Position;
                    float dx = projPos.x - enemyPos.x;
                    float dz = projPos.z - enemyPos.z;
                    if (dx * dx + dz * dz > radiusSq) continue;

                    Entity enemyEntity = enemies[j];
                    Health health = SystemAPI.GetComponent<Health>(enemyEntity);

                    bool isCrit = _random.NextFloat() < critChance;
                    float critMul = isCrit ? 2f : 1f;
                    float aoeDamage = damage.ValueRO.Value * powerMultiplier * critMul;

                    health.Current -= aoeDamage;
                    ecb.SetComponent(enemyEntity, health);

                    if (queueExists)
                    {
                        // Report at enemy XZ + grenade Y so the per-enemy
                        // hit-VFX (HitVfxApplier fallback) lands at the
                        // grenade's altitude. The hybrid visual uses its
                        // own positioning via OnHit(contactPosition).
                        float3 reportPos = new float3(enemyPos.x, projPos.y, enemyPos.z);

                        reportQueue.Enqueue(new DamageHitReport
                        {
                            WeaponId              = weaponId,
                            DamageDealt           = aoeDamage,
                            IsCrit                = isCrit,
                            KilledTarget          = health.Current <= 0f,
                            HitPosition           = reportPos,
                            Element               = element,
                            HitDirection          = hitDir,
                            SuppressDefaultHitVfx = suppressHitVfx,
                        });
                    }
                }

                // One CENTRAL explosion visual at the epicenter, regardless
                // of enemy count. SuppressDefaultHitVfx=false → HitVfxApplier
                // spawns the ElementVfxLibrary hit prefab here, scaled by
                // weaponData.hitVfxScale × hitVfxScalesWithAreaOfEffect.
                // damageDealt=0 → PerWeaponStatsTracker ignores it for kill/
                // damage attribution (no double-counting). This is what fires
                // when the grenade lands in EMPTY ground — the user sees the
                // explosion regardless of whether any enemies were hit.
                if (queueExists)
                {
                    reportQueue.Enqueue(new DamageHitReport
                    {
                        WeaponId              = weaponId,
                        DamageDealt           = 0f,             // visual-only marker
                        IsCrit                = false,
                        KilledTarget          = false,
                        HitPosition           = projPos,        // explosion epicenter
                        Element               = element,
                        HitDirection          = hitDir,
                        SuppressDefaultHitVfx = false,          // ← THIS one fires HitVfxApplier
                    });
                }

                // Transition to dying state for visual fade. The Hovl
                // explosion VFX (via CyberPickleProjectileVisual.OnHit)
                // fires from this transition — ProjectileFadeOutSystem
                // dispatches to the Companion script.
                //
                // Remove ProjectileTag so the movement / collision systems
                // ignore the entity from now on.
                // Remove Lifetime so LifetimeSystem doesn't compete for
                // its destruction (we now own the lifecycle).
                // Zero ProjectileVelocity so the entity freezes at the
                // detonation point.
                ecb.RemoveComponent<ProjectileTag>(projEntity);
                ecb.RemoveComponent<Lifetime>(projEntity);
                ecb.SetComponent(projEntity, new ProjectileVelocity { Value = float3.zero });
                ecb.AddComponent(projEntity, new ProjectileDying
                {
                    TimeRemaining       = 0f, // ProjectileFadeOutSystem computes from prefab
                    EmissionStoppedFlag = 0,
                    ContactPosition     = projPos,
                    ContactNormal       = math.up(), // explosion plume points UP — air burst
                });
            }

            enemies.Dispose();
            enemyTransforms.Dispose();
        }
    }
}

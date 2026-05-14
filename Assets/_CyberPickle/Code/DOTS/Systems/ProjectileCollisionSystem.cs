// File: Assets/_CyberPickle/Code/DOTS/Systems/ProjectileCollisionSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that checks each active projectile against all
// active enemies for proximity-based hits. Single-target by default;
// pierce-capable when ProjectilePierce.Remaining > 0.
//
// On hit:
//   - applies projectile damage to the enemy's Health (deferred via ECB,
//     so multiple hits in the same frame accumulate cleanly)
//   - emits a DamageHitReport for the Mono-side stats/VFX pipeline
//   - spawns a HitVFX entity at the projectile's position (legacy path
//     for projectiles authored with HitVFXPrefabRef populated)
//
// 2026-05-11 PR D — PIERCE:
//   When ProjectilePierce.Remaining > 0, the projectile decrements its
//   counter and CONTINUES (doesn't destroy + doesn't break) — meaning it
//   can hit additional enemies in the same frame AND in subsequent frames
//   until Remaining reaches 0. The ProjectileHitTarget dynamic buffer
//   stores already-hit entity IDs to prevent re-hitting the same enemy
//   while the projectile sits in their hit radius across multiple frames.
//   The Mono-side hit-VFX path fires per hit (each pierce gets its own
//   element-tinted burst), so the visual feel is "chain of detonations
//   through the column."
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

            // Component lookup for the per-projectile weapon attribution. Optional —
            // if a projectile lacks ProjectileSource (e.g., spawned by future systems
            // that don't tag), we report a default weapon id. WeaponFiring always
            // tags its spawns so this is the common path.
            var sourceLookup = SystemAPI.GetComponentLookup<ProjectileSource>(isReadOnly: true);
            // Health is RW via ComponentLookup so successive hits in the
            // same frame see each other's writes. Previously we used
            // GetComponent + ecb.SetComponent — that meant two projectiles
            // hitting the same enemy in the same frame each read the SAME
            // stale health value, both decided whether they "killed" it
            // based on that stale read, and the last ECB write won
            // (silently overwriting earlier writes). Net effect: damage
            // got lost AND multiple weapons could each claim the kill,
            // inflating PerWeaponStatsTracker.TotalKills above
            // RunStatsTracker.EnemiesKilled. Direct ComponentLookup writes
            // are immediate — each subsequent hit reads the post-previous-
            // hit value, KilledTarget is true for AT MOST one hit per kill,
            // and damage accumulates correctly.
            var healthLookup = SystemAPI.GetComponentLookup<Health>(isReadOnly: false);
            // M9 PR F: per-projectile element tag (optional — defaults to
            // None for projectiles without it, e.g., future spawn paths).
            var elementLookup = SystemAPI.GetComponentLookup<WeaponElement>(isReadOnly: true);
            // M9 PR D: pierce counter (RW — we decrement on each hit) and
            // already-hit buffer (RW — we read existing hits for dedup,
            // append new hits via the ECB to defer the buffer mutation
            // until end-of-frame playback).
            var pierceLookup = SystemAPI.GetComponentLookup<ProjectilePierce>(isReadOnly: false);
            var hitTargetsLookup = SystemAPI.GetBufferLookup<ProjectileHitTarget>(isReadOnly: true);
            // PR D follow-up: velocity lookup for HitDirection in the
            // damage report — so HitVfxApplier can orient hit VFX along
            // the projectile's travel direction instead of identity rotation.
            var velocityLookup = SystemAPI.GetComponentLookup<ProjectileVelocity>(isReadOnly: true);
            // Hybrid-visual tag lookup. When present, the projectile uses
            // CyberPickleProjectileVisual.OnHit for its hit visual; we
            // suppress the parallel HitVfxApplier path to avoid double-up.
            var hybridLookup = SystemAPI.GetComponentLookup<ProjectileHasHybridVisual>(isReadOnly: true);
            // AoE lookup (M9 PR E). When present, the projectile damages
            // ALL enemies within Radius on first impact — single shot,
            // radial. Pierce is ignored for AoE projectiles.
            var aoeLookup = SystemAPI.GetComponentLookup<ProjectileAoE>(isReadOnly: true);

            // Hit-report queue for per-weapon stats. Drained by DamageReportDrainSystem
            // each frame on the managed side. May not exist on frame 0 (system creation
            // order) — defensive HasSingleton check below.
            bool queueExists = SystemAPI.HasSingleton<DamageReportQueueSingleton>();
            NativeQueue<DamageHitReport> reportQueue = default;
            if (queueExists)
                reportQueue = SystemAPI.GetSingleton<DamageReportQueueSingleton>().Queue;

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
                // AoE projectiles (grenade) are rhythm-locked — they
                // explode on Lifetime expiry, not on proximity. Skip them
                // entirely here; ProjectileExplosionSystem owns their
                // lifecycle. Without this skip, the grenade would explode
                // mid-flight the moment its XZ aligned with any enemy.
                if (aoeLookup.HasComponent(projEntity)) continue;

                float3 projPos = projTransform.ValueRO.Position;

                // Snapshot the pierce counter at start-of-projectile. We
                // decrement locally as we hit enemies, write back to the
                // component once when we destroy or exhaust the projectile
                // (cheaper than a write per hit). Default 0 (no pierce
                // component → single-target legacy behavior).
                bool hasPierce = pierceLookup.HasComponent(projEntity);
                byte pierceRemaining = hasPierce ? pierceLookup[projEntity].Remaining : (byte)0;
                bool destroyProjectile = false;

                // Captured at the killing-hit moment so we can snap the
                // projectile's LocalTransform.Position to it in the destroy
                // block below (outside the inner loop, where `enemyPos` is
                // no longer in scope). Without this snap, the bullet
                // visually freezes 0.6m short of the enemy (HitRadius)
                // while the hit VFX pops at the enemy itself → detachment.
                float3 killingHitEnemyPos = projTransform.ValueRO.Position;
                // Reverse-velocity contact normal stand-in for the hybrid
                // visual bridge. Hovl's OnCollisionEnter received a real
                // surface normal from physics; we don't have one
                // (proximity-based ECS collision). The reversed travel
                // direction points "back at the shooter" — which is what
                // most impact patterns (sparks, splash, shockwave) want
                // to face for the right visual read.
                float3 killingHitContactNormal = math.up();

                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    if (destroyProjectile) break;

                    Entity enemyEntity = enemyEntities[i];

                    // Cross-frame dedup: skip enemies this projectile has
                    // already hit on a previous frame. Within-frame dedup
                    // is automatic (the enemy array contains each enemy
                    // exactly once per OnUpdate). Only pierce projectiles
                    // carry the buffer; single-target projectiles never
                    // hit twice anyway (they destroy on first hit).
                    if (hasPierce && hitTargetsLookup.HasBuffer(projEntity))
                    {
                        var hitBuffer = hitTargetsLookup[projEntity];
                        bool alreadyHit = false;
                        for (int j = 0; j < hitBuffer.Length; j++)
                        {
                            if (hitBuffer[j].Value == enemyEntity) { alreadyHit = true; break; }
                        }
                        if (alreadyHit) continue;
                    }

                    // XZ-plane distance only — top-down survivors-like means
                    // Y differences (muzzle height vs enemy capsule center)
                    // shouldn't prevent hits. Hit detection is conceptually
                    // 2D for STRAIGHT projectiles. (AoE / parabolic projectiles
                    // were skipped at the top of the foreach — they detonate
                    // on lifetime via ProjectileExplosionSystem.)
                    float3 enemyPos = enemyTransforms[i].Position;
                    float dx = projPos.x - enemyPos.x;
                    float dz = projPos.z - enemyPos.z;
                    if (dx * dx + dz * dz > HitRadiusSq) continue;

                    // Read CURRENT (post-previous-hit) health via the RW
                    // lookup — see the lookup declaration above for the
                    // reasoning. If two projectiles hit the same enemy this
                    // frame, the second one sees the first one's damage
                    // already applied, so its KilledTarget evaluation is
                    // accurate and the total damage accumulates correctly.
                    Health health = healthLookup[enemyEntity];

                    // CAPTURE pre-hit health so we can detect "this hit
                    // crossed zero" rather than "this hit ended up at <= 0."
                    // Without this, a hit that strikes an ALREADY-dead enemy
                    // (e.g., killed by a previous projectile this same frame,
                    // not yet Dead-tagged) would still report KilledTarget=true,
                    // and a second weapon would get a free kill credit. The
                    // RW lookup correctly applies damage to the corpse but
                    // we DO NOT want the kill counter to credit this hit.
                    float preHitCurrent = health.Current;

                    // Apply damage formula: base × (1 + Power%) × critMultiplier.
                    // Element / weapon-upgrade / equipment / skill multipliers
                    // slot in here as those systems land (M8 / M9).
                    bool isCrit = _random.NextFloat() < critChance;
                    float critMultiplier = isCrit ? 2f : 1f;
                    float finalDamage = projDamage.ValueRO.Value * powerMultiplier * critMultiplier;

                    health.Current -= finalDamage;
                    healthLookup[enemyEntity] = health;

                    // Enqueue a per-hit report for PerWeaponStatsTracker. Burst
                    // can't call into managed code; the queue is the bridge.
                    // DamageReportDrainSystem dequeues these on the managed side
                    // each frame and dispatches to the tracker. Each pierce hit
                    // gets its own report → its own element-tinted hit VFX
                    // burst via HitVfxApplier → "chain of detonations" feel.
                    if (queueExists)
                    {
                        FixedString64Bytes weaponId = default;
                        if (sourceLookup.HasComponent(projEntity))
                            weaponId = sourceLookup[projEntity].WeaponId;

                        byte element = 0; // ElementId.None
                        if (elementLookup.HasComponent(projEntity))
                            element = elementLookup[projEntity].Value;

                        // Normalized travel direction for hit-VFX orientation.
                        // Zero vector if velocity is unavailable or near-zero —
                        // HitVfxApplier falls back to identity rotation.
                        float3 hitDir = float3.zero;
                        if (velocityLookup.HasComponent(projEntity))
                        {
                            float3 v = velocityLookup[projEntity].Value;
                            float lenSq = math.lengthsq(v);
                            if (lenSq > 0.0001f) hitDir = v / math.sqrt(lenSq);
                        }

                        // If the projectile is a hybrid-visual one, Hovl's
                        // authored hit GO fires via CyberPickleProjectileVisual
                        // .OnHit (LateSimulation tick). Suppress the parallel
                        // HitVfxApplier.Play here to avoid double hit visuals.
                        bool suppressHitVfx = hybridLookup.HasComponent(projEntity);

                        // Hit-VFX position: enemy's XZ + bullet's Y. Same
                        // rationale as the snap above — enemy.LocalTransform
                        // is at FEET; bullet is at chest height. Reporting
                        // enemyPos directly would put the fallback hit VFX
                        // on the floor.
                        float3 reportHitPos = new float3(enemyPos.x, projPos.y, enemyPos.z);

                        reportQueue.Enqueue(new DamageHitReport
                        {
                            WeaponId             = weaponId,
                            DamageDealt          = finalDamage,
                            IsCrit               = isCrit,
                            // KILL ATTRIBUTION: only the hit that CROSSED
                            // zero gets kill credit. A late hit on an
                            // already-dead corpse (same frame, before
                            // Dead-tagging) reports KilledTarget=false.
                            // Without this, two projectiles hitting an
                            // enemy with low HP would both claim the kill
                            // (one brings it from 10→-50, the next finds
                            // it at -50 and writes -100; both saw their
                            // POST-state as <=0). The crossing-zero check
                            // makes kill credit unique per enemy death.
                            KilledTarget         = preHitCurrent > 0f && health.Current <= 0f,
                            HitPosition          = reportHitPos,
                            Element              = element,
                            HitDirection         = hitDir,
                            SuppressDefaultHitVfx = suppressHitVfx,
                        });
                    }

                    // Spawn hit VFX entity at the impact altitude (XZ at
                    // enemy, Y at bullet). Legacy path for projectile
                    // prefabs authored with HitVFXPrefabRef populated —
                    // most M9-era weapons use the Mono-side HitVfxApplier
                    // driven by the report queue above instead.
                    Entity vfxRef = hitVfxRef.ValueRO.Value;
                    if (vfxRef != Entity.Null)
                    {
                        Entity vfxInstance = ecb.Instantiate(vfxRef);
                        ecb.SetComponent(vfxInstance, LocalTransform.FromPositionRotation(
                            new float3(enemyPos.x, projPos.y, enemyPos.z), projTransform.ValueRO.Rotation));
                    }

                    // Pierce bookkeeping: only track the hit + decrement
                    // when we're actually piercing (Remaining > 0). For
                    // single-target projectiles, skip straight to destroy.
                    //
                    // CRITICAL: the hit-targets buffer only exists on
                    // projectiles spawned with pierceCount > 0 (see
                    // WeaponFiring.FireOneProjectile). Trying to
                    // AppendToBuffer on an entity without the buffer
                    // throws at ECB playback, aborting the destroy call
                    // — that's exactly the "pistol never disappears,
                    // pierces all enemies" symptom seen in pre-fix builds.
                    if (pierceRemaining > 0)
                    {
                        // Pierce mode: record this enemy, consume one
                        // pierce, keep flying.
                        if (hitTargetsLookup.HasBuffer(projEntity))
                            ecb.AppendToBuffer(projEntity, new ProjectileHitTarget { Value = enemyEntity });
                        pierceRemaining--;
                        // Component write deferred until projectile end —
                        // saves N writes during a chain-pierce shot.
                    }
                    else
                    {
                        // No pierces remaining (or never had any) → this
                        // hit destroys the projectile. Capture the impact
                        // position for the post-loop snap step.
                        //
                        // CRITICAL: snap XZ ONLY, preserve the bullet's
                        // current Y. Enemy LocalTransform pivots are
                        // typically at the FEET (Y=0), but the bullet
                        // flies at chest height (~1.2m). A full-3-axis
                        // snap drags the bullet DOWN to the floor on
                        // impact — visually broken (bullet trail at chest
                        // height + sudden teleport to feet + hit VFX
                        // bursting on the ground past the enemy's body).
                        // Keeping the bullet's Y means the freeze + hit
                        // VFX both appear at the altitude the bullet was
                        // flying — visually right at the enemy's torso.
                        destroyProjectile = true;
                        killingHitEnemyPos = new float3(enemyPos.x, projPos.y, enemyPos.z);

                        // Contact normal stand-in: reverse projectile velocity.
                        if (velocityLookup.HasComponent(projEntity))
                        {
                            float3 v = velocityLookup[projEntity].Value;
                            float lenSq = math.lengthsq(v);
                            killingHitContactNormal = (lenSq > 0.0001f) ? (-v / math.sqrt(lenSq)) : math.up();
                        }
                    }
                }

                // Write back the decremented pierce counter once per projectile
                // per frame (instead of once per hit) — single component write
                // even if the projectile pierced 5 enemies this frame.
                if (hasPierce && !destroyProjectile)
                {
                    pierceLookup[projEntity] = new ProjectilePierce { Remaining = pierceRemaining };
                }

                if (destroyProjectile)
                {
                    // Transition to dying state. The fade-out duration is
                    // NOT supplied here — ProjectileFadeOutSystem reads it
                    // from the projectile PREFAB on the first dying-frame
                    // (CyberPickleProjectileVisual.GetTotalFadeDuration for
                    // hybrid prefabs, longest-particle heuristic for
                    // legacy fallback). The prefab owns its own timing
                    // because a weapon can fire many element-coupled
                    // variants with different particle timings.
                    //
                    // The transition:
                    //   - SNAP LocalTransform.Position to enemyPos → bullet
                    //     visually freezes AT the impact point (not 0.6m short
                    //     where the hit radius first overlapped). Without this
                    //     snap, the bullet visibly stops before reaching the
                    //     enemy and the hit VFX appears in a different spot —
                    //     "detached" feel.
                    //   - REMOVE ProjectileTag → collision/movement systems
                    //     skip this entity from now on.
                    //   - ZERO ProjectileVelocity → bullet stays put
                    //     (Hovl-style "rb.constraints = FreezeAll" equivalent).
                    //   - ADD ProjectileDying with TimeRemaining = 0
                    //     (placeholder — fade-out system computes the real
                    //     duration from the prefab on first encounter).
                    var oldT = projTransform.ValueRO;
                    ecb.SetComponent(projEntity, new LocalTransform
                    {
                        Position = killingHitEnemyPos,
                        Rotation = oldT.Rotation,
                        Scale    = oldT.Scale,
                    });
                    ecb.RemoveComponent<ProjectileTag>(projEntity);
                    ecb.SetComponent(projEntity, new ProjectileVelocity { Value = float3.zero });
                    ecb.AddComponent(projEntity, new ProjectileDying
                    {
                        TimeRemaining       = 0f, // computed by ProjectileFadeOutSystem on first frame
                        EmissionStoppedFlag = 0,
                        ContactPosition     = killingHitEnemyPos,
                        ContactNormal       = killingHitContactNormal,
                    });
                }
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();
        }
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyDeathSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Handles enemy death by converting a living entity into a "ragdoll
// corpse" entity. Instead of destroying the entity (which would kill
// the visual binding and prevent any post-death physics), we:
//
//   1. Tag the entity with Dead so EnemyMovementSystem stops driving it.
//   2. Unlock rotation in PhysicsMass so the body can tumble.
//   3. Apply a launch impulse — direction comes from the killer (the
//      vector from the impact source toward the body) plus an upward
//      kick. For now the killer position is approximated as the player
//      position; later this becomes the projectile's position at impact.
//   4. Trigger the death animation on the bound visual (IsDead bool +
//      random DeathVariant int) and disable ZombieAnimDriver so it
//      doesn't fight the death state.
//
// The visual stays bound to the entity via EnemyVisualBridge — it
// follows the body as physics simulates the tumble + fall + landing.
// Death animation plays simultaneously so the bones flail while the
// whole rig is being thrown around. Approximates a ragdoll effect
// without paying the cost of a multi-body articulated ragdoll.
//
// SystemBase (managed) — needs Animator parameter writes and bridge
// dictionary access. The work is bounded to entities that died THIS
// frame, which is small.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Bridge;
using CyberPickle.DOTS.Components;
using CyberPickle.DOTS.Visual;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.RunState;
using URandom = UnityEngine.Random;

namespace CyberPickle.DOTS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EnemyDeathSystem : SystemBase
    {
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int DeathVariantHash = Animator.StringToHash("DeathVariant");

        // Death variant convention (matches Animator transition conditions
        // and EnemyVisualType enum):
        //   StandardHumanoid -> variant 0 or 1 (random)
        //   BigHumanoid       -> variant 2
        // Add more rules here as new EnemyVisualType entries ship.
        private const int StandardDeathVariantCount = 2;
        private const int BigHumanoidDeathVariant   = 2;
        private const int BigHumanoidVisualType     = 1; // = (int)EnemyVisualType.BigHumanoid

        // Launch impulse parameters. Linear speed = launch in horizontal
        // direction away from killer. Vertical kick adds the "fly up" arc.
        private const float LaunchHorizontalSpeed = 4.5f;
        private const float LaunchVerticalSpeed = 5.0f;
        private const float LaunchAngularSpeed = 6f;     // radians/sec spin

        // Inverse inertia for tumbling. Larger = spins more readily.
        // Roughly matches a m=50 capsule of h=1.8, r=0.4.
        private static readonly float3 RagdollInverseInertia = new float3(0.06f, 0.25f, 0.06f);

        protected override void OnCreate()
        {
            RequireForUpdate<EnemyTag>();
        }

        protected override void OnUpdate()
        {
            var bridge = EnemyVisualBridge.Instance;
            var em = EntityManager;

            // Source of "knockback away from" — the player position. Approximation
            // until projectiles tag their last-known impact position on the entity.
            float3 killerPos = float3.zero;
            if (SystemAPI.HasSingleton<PlayerPositionData>())
            {
                killerPos = SystemAPI.GetSingleton<PlayerPositionData>().Position;
            }

            using var dyingEntities = new NativeList<Entity>(32, Allocator.Temp);

            foreach (var (health, entity) in
                     SystemAPI.Query<RefRO<Health>>()
                              .WithAll<EnemyTag>()
                              .WithNone<Dead>()
                              .WithEntityAccess())
            {
                if (health.ValueRO.Current <= 0f)
                {
                    dyingEntities.Add(entity);
                }
            }

            // Notify the run-stats tracker of kills this frame. We do this
            // ONCE with the count instead of per-entity in the loop below
            // to keep the managed-call boundary minimal.
            if (dyingEntities.Length > 0 && RunStatsTracker.Instance != null)
            {
                for (int i = 0; i < dyingEntities.Length; i++)
                    RunStatsTracker.Instance.RecordEnemyKilled();
            }

            // Music event broadcast — one per death. EnemyDeathSystem is
            // SystemBase (managed), so calling MusicEventBus.Fire from here
            // is safe; ISystem / Burst systems would need an event-entity
            // bridge instead. Stage 0: Debug.Log. Stage 2 (Wwise): per-kill
            // particle / stinger trigger; aggregated kills drive the
            // CombatIntensity RTPC.
            for (int i = 0; i < dyingEntities.Length; i++)
            {
                MusicEventBus.Fire(MusicEvent.EnemyDeath);
            }

            for (int i = 0; i < dyingEntities.Length; i++)
            {
                var entity = dyingEntities[i];

                // ─── 1. Compute launch direction (away from killer) ───
                float3 entityPos = em.GetComponentData<LocalTransform>(entity).Position;
                float3 awayFromKiller = entityPos - killerPos;
                awayFromKiller.y = 0f;
                float lenSq = math.lengthsq(awayFromKiller);
                float3 launchDir = lenSq > 0.0001f
                    ? awayFromKiller / math.sqrt(lenSq)
                    : new float3(0f, 0f, 1f);

                // ─── 2. Unlock rotation so the body can tumble ───
                if (em.HasComponent<PhysicsMass>(entity))
                {
                    var mass = em.GetComponentData<PhysicsMass>(entity);
                    mass.InverseInertia = RagdollInverseInertia;
                    em.SetComponentData(entity, mass);
                }

                // ─── 3. Apply launch impulse + random spin ───
                if (em.HasComponent<PhysicsVelocity>(entity))
                {
                    // Random spin axis biased horizontal so the body tumbles forward,
                    // not just spinning on the vertical axis like a top.
                    var spinAxis = math.normalize(new float3(
                        UnityEngine.Random.Range(-1f, 1f),
                        UnityEngine.Random.Range(-0.3f, 0.3f),
                        UnityEngine.Random.Range(-1f, 1f)));

                    em.SetComponentData(entity, new PhysicsVelocity
                    {
                        Linear = new float3(
                            launchDir.x * LaunchHorizontalSpeed,
                            LaunchVerticalSpeed,
                            launchDir.z * LaunchHorizontalSpeed),
                        Angular = spinAxis * LaunchAngularSpeed
                    });
                }

                // ─── 4. Trigger death animation on the bound visual ───
                if (bridge != null && bridge.TryGet(entity, out var visualTransform) && visualTransform != null)
                {
                    var visual = visualTransform.gameObject;

                    var animator = visual.GetComponent<Animator>();
                    if (animator != null)
                    {
                        // Pick the death variant based on the entity's visual classification.
                        // Big enemies always play their dedicated death state (variant 2).
                        // Standard humanoids randomize between the two zombie variants.
                        int variant;
                        if (em.HasComponent<EnemyVisualTypeId>(entity)
                            && em.GetComponentData<EnemyVisualTypeId>(entity).Value == BigHumanoidVisualType)
                        {
                            variant = BigHumanoidDeathVariant;
                        }
                        else
                        {
                            variant = UnityEngine.Random.Range(0, StandardDeathVariantCount);
                        }

                        animator.SetInteger(DeathVariantHash, variant);
                        animator.SetBool(IsDeadHash, true);
                    }

                    var driver = visual.GetComponent<ZombieAnimDriver>();
                    if (driver != null) driver.enabled = false;

                    visual.name = $"Corpse_{entity.Index}";
                }

                // ─── 4.5. XP gem drops ───
                // Currency drops are intentionally NOT spawned here. Currency
                // (Neural Credits / Cybercoins) is awarded by environment
                // destruction, not enemy kills, per the GDD economy split.
                // When that environment-destruction system ships, it lives in
                // its own destruction handler — not in this death path.
                SpawnXPGemDrops(em, entity, entityPos);

                // ─── 5. Mark entity Dead — movement system stops driving it ───
                em.AddComponent<Dead>(entity);

                // ─── 6. Start the corpse lifecycle clock ───
                // CorpseLifecycleSystem ticks this and triggers the dissolve
                // visual + entity destroy when the timeline expires. Per-enemy
                // timing comes from EnemyCorpseConfig (baked from EnemyData).
                float delay = 3f;
                float dissolve = 1.5f;
                if (em.HasComponent<EnemyCorpseConfig>(entity))
                {
                    var cfg = em.GetComponentData<EnemyCorpseConfig>(entity);
                    delay = cfg.DelayBeforeDissolve;
                    dissolve = cfg.DissolveDuration;
                }
                em.AddComponentData(entity, new CorpseLifecycle
                {
                    DeathTime           = SystemAPI.Time.ElapsedTime,
                    DelayBeforeDissolve = delay,
                    DissolveDuration    = dissolve,
                    DissolveSignaled    = false,
                });

                // NOTE: Entity is NOT destroyed. It lives on as a corpse, simulated
                // by Unity Physics. Cleanup (despawn timer / distance / scene unload)
                // is a future system.
            }
        }

        // ─── XP gem drop logic ──────────────────────────────────────────────

        /// <summary>
        /// Rolls the cascade on EnemyXPDropChances and Instantiates one XP gem
        /// (or a multi-drop burst for bosses) at the entity's position. The gem
        /// gets its tier-appropriate XPGemValue stamped from the registry and
        /// is offset by a small random vector so multi-drops don't overlap.
        /// </summary>
        private void SpawnXPGemDrops(EntityManager em, Entity dyingEntity, float3 spawnPos)
        {
            // No drop chances baked = enemy that doesn't drop XP. Skip silently.
            if (!em.HasComponent<EnemyXPDropChances>(dyingEntity)) return;

            var chances = em.GetComponentData<EnemyXPDropChances>(dyingEntity);

            // Boss multi-drop: spawn N Tier 4 gems in a circle, then ALSO do the
            // cascade roll for one bonus gem on top. Spectacular, designed to
            // feel like the body explodes loot.
            bool isBoss = em.HasComponent<BossTag>(dyingEntity);
            if (isBoss && chances.BossMultiDropCount > 0)
            {
                SpawnBossGemBurst(em, spawnPos, chances.BossMultiDropCount);
            }

            // Cascade roll — one gem per kill regardless of size.
            int tier = RollDropTier(chances);
            SpawnGem(em, spawnPos, tier, jitter: 0.2f);
        }

        /// <summary>
        /// Walks the cascade highest -> lowest. Returns tier 0 if nothing higher
        /// triggers (so something always drops). Tier 5 (Sentinel Prime — jackpot)
        /// is checked first so its small probability gets a clean slice off the
        /// 0..1 roll before the lower tiers consume the rest.
        /// </summary>
        private static int RollDropTier(EnemyXPDropChances c)
        {
            float roll = URandom.value; // 0..1
            float threshold = c.Tier5Chance;
            if (roll < threshold) return 5;
            threshold += c.Tier4Chance;
            if (roll < threshold) return 4;
            threshold += c.Tier3Chance;
            if (roll < threshold) return 3;
            threshold += c.Tier2Chance;
            if (roll < threshold) return 2;
            threshold += c.Tier1Chance;
            if (roll < threshold) return 1;
            return 0;
        }

        /// <summary>
        /// Spawns N Tier 4 gems in a ring around the boss's death position.
        /// Each gem gets a small random scatter so they don't perfectly stack.
        /// </summary>
        private void SpawnBossGemBurst(EntityManager em, float3 origin, int count)
        {
            float two_pi = 2f * math.PI;
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * two_pi + URandom.Range(-0.1f, 0.1f);
                float r = URandom.Range(1.0f, 2.5f);
                float3 pos = new float3(
                    origin.x + math.cos(angle) * r,
                    origin.y,
                    origin.z + math.sin(angle) * r);
                SpawnGem(em, pos, tier: 4, jitter: 0f);
            }
        }

        /// <summary>
        /// Look up the gem registry, pick the tier prefab, Instantiate at pos.
        /// Adds a small random XZ jitter so single drops don't perfectly stack
        /// on the corpse and double-pickups visually.
        /// </summary>
        private void SpawnGem(EntityManager em, float3 pos, int tier, float jitter)
        {
            using var registryQuery = em.CreateEntityQuery(ComponentType.ReadOnly<XPGemPrefabBufferElement>());
            if (registryQuery.CalculateEntityCount() == 0) return;

            using var registries = registryQuery.ToEntityArray(Allocator.Temp);
            for (int r = 0; r < registries.Length; r++)
            {
                var buffer = em.GetBuffer<XPGemPrefabBufferElement>(registries[r], isReadOnly: true);
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Tier != tier) continue;
                    if (buffer[i].Prefab == Entity.Null) return;

                    Entity gem = em.Instantiate(buffer[i].Prefab);

                    float3 jitteredPos = pos;
                    if (jitter > 0f)
                    {
                        jitteredPos.x += URandom.Range(-jitter, jitter);
                        jitteredPos.z += URandom.Range(-jitter, jitter);
                    }

                    // Preserve the prefab's authored rotation + scale. We only
                    // want to replace position. Reading the existing transform
                    // and mutating Position avoids accidentally clobbering any
                    // designer-authored rotation / scale values.
                    bool hasLT = em.HasComponent<LocalTransform>(gem);
                    if (hasLT)
                    {
                        var t = em.GetComponentData<LocalTransform>(gem);
                        t.Position = jitteredPos;
                        em.SetComponentData(gem, t);
                    }

                    // Also force-write LocalToWorld immediately so first-frame
                    // rendering doesn't show the gem at the prefab's bake-time
                    // position before TransformSystemGroup runs.
                    if (em.HasComponent<Unity.Transforms.LocalToWorld>(gem))
                    {
                        var t = em.GetComponentData<LocalTransform>(gem);
                        em.SetComponentData(gem, new Unity.Transforms.LocalToWorld
                        {
                            Value = float4x4.TRS(t.Position, t.Rotation, t.Scale)
                        });
                    }

                    // ALSO update child LocalToWorlds via the LinkedEntityGroup so
                    // a parent-with-children gem prefab (empty parent + sphere child)
                    // doesn't render the visible child at world origin while the
                    // parent invisibly sits at the correct position. We compute each
                    // child's LocalToWorld = parent's LocalToWorld × child's
                    // LocalTransform, mirroring what TransformSystemGroup will do
                    // next frame anyway.
                    if (hasLT && em.HasBuffer<LinkedEntityGroup>(gem))
                    {
                        var parentLTW = em.GetComponentData<Unity.Transforms.LocalToWorld>(gem).Value;
                        var group = em.GetBuffer<LinkedEntityGroup>(gem);
                        for (int g = 0; g < group.Length; g++)
                        {
                            var child = group[g].Value;
                            if (child == gem) continue;
                            if (!em.HasComponent<Unity.Transforms.LocalToWorld>(child)) continue;
                            if (!em.HasComponent<LocalTransform>(child)) continue;

                            var childLT = em.GetComponentData<LocalTransform>(child);
                            float4x4 childLocal = float4x4.TRS(childLT.Position, childLT.Rotation, childLT.Scale);
                            em.SetComponentData(child, new Unity.Transforms.LocalToWorld
                            {
                                Value = math.mul(parentLTW, childLocal)
                            });
                        }
                    }

                    if (em.HasComponent<XPGemValue>(gem))
                        em.SetComponentData(gem, new XPGemValue { Value = buffer[i].XPValue });
                    return;
                }
            }
        }
    }
}

// File: Assets/_CyberPickle/Code/DOTS/Systems/XPMagnetSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that drives every XP gem each frame:
//
//   1. Computes XZ distance to the player (PlayerPositionData singleton).
//   2. If within MagnetRadius:
//      - Smoothly accelerates the gem's velocity toward the player at
//        MaxPullSpeed using frame-rate-independent exponential smoothing.
//      - Integrates position from velocity.
//   3. If within CollectRadius:
//      - Increments PlayerXP.CurrentXP by the gem's XPValue.
//      - Destroys the gem entity (deferred via ECB so we don't invalidate
//        the iterator).
//
// Tunables are static constants for now — when the player stats system
// lands, replace these with reads from a PlayerMagnetStats singleton
// (radius / collect / pullSpeed scaled by gear & level-up upgrades).

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct XPMagnetSystem : ISystem
    {
        // ─── Tunables ────────────────────────────────────────────────────────
        // BaseMagnetRadius is the design constant; the effective radius each
        // frame is BaseMagnetRadius × PlayerStatsData.MagneticField (skill /
        // equipment / breakpoint upgrades scale this naturally).

        /// <summary>Base magnet radius before the player's MagneticField multiplier. Effective radius = base × multiplier.</summary>
        private const float BaseMagnetRadius = 4f;

        /// <summary>Distance from player at which gems are auto-collected. Doesn't scale with stats — represents physical pickup hitbox.</summary>
        private const float CollectRadius = 0.6f;

        /// <summary>Top horizontal speed a gem reaches when fully magnetized.</summary>
        private const float MaxPullSpeed = 18f;

        /// <summary>Exponential smoothing factor for velocity ramp-up. Higher = snappier acceleration.</summary>
        private const float PullSmoothing = 9f;

        /// <summary>Cached squared collection radius (collection radius is constant).</summary>
        private const float CollectRadiusSq = CollectRadius * CollectRadius;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerPositionData>();
            state.RequireForUpdate<XPGemTag>();
            state.RequireForUpdate<PlayerXP>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            float3 playerPos = SystemAPI.GetSingleton<PlayerPositionData>().Position;

            // Read player's MagneticField + NeuralAdaptation multipliers from
            // the ECS singleton. If the bridge hasn't created the singleton yet
            // (player not spawned / bridge disabled), fall back to neutral
            // multipliers so default magnet + XP behavior still works.
            float magnetMultiplier = 1f;
            float xpGainMultiplier = 1f;
            if (SystemAPI.HasSingleton<PlayerStatsData>())
            {
                var s = SystemAPI.GetSingleton<PlayerStatsData>();
                magnetMultiplier = s.MagneticField;
                xpGainMultiplier = s.NeuralAdaptation;
                if (magnetMultiplier <= 0.01f) magnetMultiplier = 1f; // safety: never collapse to 0
                if (xpGainMultiplier <= 0.01f) xpGainMultiplier = 1f;
            }

            float effectiveMagnetRadius   = BaseMagnetRadius * magnetMultiplier;
            float effectiveMagnetRadiusSq = effectiveMagnetRadius * effectiveMagnetRadius;

            // Pull-toward smoothing factor for this frame (frame-rate independent).
            float lerpT = 1f - math.exp(-PullSmoothing * dt);

            // Accumulate XP awarded this frame so we do one Set on the singleton
            // at the end instead of N reads/writes.
            int xpAwarded = 0;

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (transform, velocity, value, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<XPGemVelocity>, RefRO<XPGemValue>>()
                              .WithAll<XPGemTag>()
                              .WithEntityAccess())
            {
                float3 gemPos = transform.ValueRO.Position;
                float3 toPlayer = playerPos - gemPos;
                toPlayer.y = 0f;
                float distSq = math.lengthsq(toPlayer);

                // Outside magnet radius — gem sits idle (just decay any residual velocity).
                if (distSq > effectiveMagnetRadiusSq)
                {
                    velocity.ValueRW.Value *= math.max(0f, 1f - dt * 2f); // gentle decay
                    continue;
                }

                // Within collect radius — pickup. Apply NeuralAdaptation
                // multiplier to scale per-gem XP. Round to int to keep
                // PlayerXP.CurrentXP integer-clean.
                if (distSq <= CollectRadiusSq)
                {
                    int scaledXp = (int)math.round(value.ValueRO.Value * xpGainMultiplier);
                    if (scaledXp < 1) scaledXp = 1; // minimum 1 XP per gem (avoid total loss at extreme low multipliers)
                    xpAwarded += scaledXp;
                    ecb.DestroyEntity(entity);
                    continue;
                }

                // In magnet range — accelerate toward player.
                float dist = math.sqrt(distSq);
                float3 dir = toPlayer / dist;

                // Speed scales linearly with proximity (closer = faster pull) for a
                // gravitational feel without needing real 1/r² physics.
                float t = 1f - (dist / effectiveMagnetRadius);
                float desiredSpeed = MaxPullSpeed * math.lerp(0.4f, 1f, t);
                float3 desiredVel = dir * desiredSpeed;

                velocity.ValueRW.Value = math.lerp(velocity.ValueRO.Value, desiredVel, lerpT);
                transform.ValueRW.Position += velocity.ValueRO.Value * dt;
            }

            // Single write to the XP singleton — caps managed boundary cost regardless
            // of how many gems were collected this frame.
            if (xpAwarded > 0)
            {
                var xpEntity = SystemAPI.GetSingletonEntity<PlayerXP>();
                var xp = SystemAPI.GetComponent<PlayerXP>(xpEntity);
                xp.CurrentXP += xpAwarded;
                SystemAPI.SetComponent(xpEntity, xp);
            }
        }
    }
}

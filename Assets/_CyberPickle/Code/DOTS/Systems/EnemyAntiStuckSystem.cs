// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyAntiStuckSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that detects enemies whose entities aren't
// translating despite EnemyMovementSystem writing them a pursuit
// velocity, and "kicks" them out of the wedge by rotating their
// velocity vector. Compensates for Unity Physics body-sleep state
// not consistently waking on direct PhysicsVelocity writes — visible
// as "stuck-still enemies that still deal contact damage" reported
// 2026-05-12.
//
// Diagnostic basis: when a Unity Physics body enters sleep (low motion
// energy for several frames — typical when an enemy is wedged in a
// clump or against a wall), our system's PhysicsVelocity writes via
// RefRW update the component but the simulation may not pick up the
// change (sleep mask not reset). External collision impulses DO wake
// the body — which is exactly why the user observed "other enemies
// pushing them wakes them briefly, then they stop again". The body
// rapidly re-sleeps once the external impulse is gone.
//
// Fix: detect "didn't translate" frames per-enemy. After N stuck
// frames, rotate the body's linear velocity by a few degrees AND
// re-write it. The rotation breaks the wedge symmetry (so the body
// tries to move sideways out of the corner) AND the velocity write
// happens with a different value, which is more likely to reset the
// sleep state.
//
// Runs AFTER PhysicsSystemGroup so it sees the post-physics position
// (the position the body actually ended up at this frame, not the
// pre-collision-response position).

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct EnemyAntiStuckSystem : ISystem
    {
        /// <summary>Seconds an enemy must be "not translating" before we kick it. 0.4s = ~24 frames at 60fps. Long enough to ignore single-frame stalls (collision response, frame hitches); short enough to feel responsive.</summary>
        private const float StuckTimeThreshold = 0.4f;

        /// <summary>Squared distance threshold to count as "moved this frame". 0.005m = ~5mm. Below this, the enemy is considered stationary.</summary>
        private const float MovedDistSqThreshold = 0.005f * 0.005f;

        /// <summary>Angle to rotate the velocity vector by when unsticking (degrees). Big enough to escape a wedge; small enough not to look like the enemy lost interest in the player.</summary>
        private const float KickRotationDegrees = 45f;

        /// <summary>Minimum kick magnitude when current velocity is near-zero (e.g., enemy is on top of the player). Gives a fresh impulse so the body has motion energy.</summary>
        private const float KickMagnitudeWhenZero = 1.0f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnemyTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            EntityCommandBuffer ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // ─── Lazily add EnemyStuckTracker to enemies that lack it ───
            // Done via ECB to keep the structural change off the main path.
            foreach (var (_, entity) in
                     SystemAPI.Query<RefRO<LocalTransform>>()
                              .WithAll<EnemyTag>()
                              .WithNone<EnemyStuckTracker, Dead>()
                              .WithEntityAccess())
            {
                ecb.AddComponent(entity, new EnemyStuckTracker
                {
                    LastPosition  = float3.zero, // will be initialized on the next frame
                    StuckSeconds  = 0f,
                    KickDirection = 0,
                });
            }

            // Predicted-velocity lookup — anti-stuck rotates the body's
            // linear velocity to escape a wedge; mirror that rotation into
            // the prediction so weapons leading this target this same frame
            // use the actual escape direction rather than the (now stale)
            // toward-player direction that EnemyMovementSystem wrote earlier
            // this frame.
            var predLookup = SystemAPI.GetComponentLookup<EnemyPredictedVelocity>(isReadOnly: false);

            // ─── Stuck-detection pass over tracked enemies ───
            foreach (var (transform, velocity, tracker, enemy) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<EnemyStuckTracker>>()
                              .WithAll<EnemyTag>()
                              .WithNone<Dead>()
                              .WithEntityAccess())
            {
                float3 currentPos = transform.ValueRO.Position;
                float3 lastPos    = tracker.ValueRO.LastPosition;
                float3 delta      = currentPos - lastPos;
                delta.y = 0f; // only horizontal motion counts (gravity / jumping aren't "pursuit")

                bool firstFrame = math.lengthsq(lastPos) < 0.0001f; // tracker was just added
                bool moved      = math.lengthsq(delta) >= MovedDistSqThreshold;

                if (firstFrame || moved)
                {
                    // Either the very first observation (no baseline yet) or
                    // the enemy is translating fine — reset the stuck timer.
                    tracker.ValueRW.StuckSeconds = 0f;
                }
                else
                {
                    // Entity didn't translate. Accumulate stuck time.
                    tracker.ValueRW.StuckSeconds += dt;

                    if (tracker.ValueRO.StuckSeconds >= StuckTimeThreshold)
                    {
                        // ─── Kick the body out of its wedge ───
                        // Rotate the XZ component of the velocity by a fixed
                        // angle (alternating direction per kick so we don't
                        // just oscillate the same way each time). If velocity
                        // is near zero (e.g., enemy is right on top of the
                        // player), seed it with a kick magnitude so the body
                        // has something to wake on.
                        float3 v   = velocity.ValueRO.Linear;
                        float3 vXZ = new float3(v.x, 0f, v.z);
                        float vMag = math.length(vXZ);

                        if (vMag < 0.001f)
                        {
                            // No usable pursuit direction — kick in a deterministic
                            // direction derived from the toggle so multiple stuck
                            // enemies don't all kick the same way.
                            float kickAngle = tracker.ValueRO.KickDirection == 0 ? 0f : math.PI;
                            vXZ = new float3(
                                math.cos(kickAngle) * KickMagnitudeWhenZero,
                                0f,
                                math.sin(kickAngle) * KickMagnitudeWhenZero);
                        }
                        else
                        {
                            // Rotate the existing XZ velocity by ±KickRotationDegrees.
                            float sign  = tracker.ValueRO.KickDirection == 0 ? +1f : -1f;
                            float angle = math.radians(KickRotationDegrees) * sign;
                            float c = math.cos(angle);
                            float s = math.sin(angle);
                            vXZ = new float3(
                                vXZ.x * c - vXZ.z * s,
                                0f,
                                vXZ.x * s + vXZ.z * c);
                        }

                        velocity.ValueRW.Linear = new float3(vXZ.x, v.y, vXZ.z);

                        // Mirror the kick into the prediction so any weapon
                        // firing this same frame leads the enemy along the
                        // new escape vector (XZ only — prediction never
                        // carries vertical intent).
                        if (predLookup.HasComponent(enemy))
                        {
                            predLookup[enemy] = new EnemyPredictedVelocity
                            {
                                Value = new float3(vXZ.x, 0f, vXZ.z)
                            };
                        }

                        // Flip the toggle so the next kick tries the other direction
                        // — pure CW kicks could re-wedge the body in a symmetric
                        // failure mode.
                        tracker.ValueRW.KickDirection = (byte)(1 - tracker.ValueRO.KickDirection);

                        // Reset the timer; if the kick didn't unstick the body,
                        // we'll try again in StuckTimeThreshold seconds.
                        tracker.ValueRW.StuckSeconds = 0f;
                    }
                }

                // Always record current position for next frame's delta.
                tracker.ValueRW.LastPosition = currentPos;
            }
        }
    }
}

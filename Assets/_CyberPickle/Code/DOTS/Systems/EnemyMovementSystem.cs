// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyMovementSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that drives each EnemyTag entity toward the
// player by writing PhysicsVelocity. Unity Physics simulates the bodies
// each frame — that means inter-enemy pushing, wall stopping, and
// explosion knockback all "just work" via the existing capsule colliders
// instead of being faked with separation magic numbers.
//
// Why velocity instead of force/impulse: enemies are not realistic
// agents, they're game characters that should reach a target speed
// regardless of how many friends are in the way. Setting Linear directly
// means MoveSpeed is the cap; physics resolves contact penetration on
// top of that without the steering ever fighting a force.
//
// Why we still set Y velocity to (preserved): leaves the door open for
// gravity, jumps, knockback-with-arc, etc. — the system only owns the
// horizontal pursuit channel.
//
// Run order: BEFORE PhysicsSystemGroup so the velocity we write is what
// the simulation steps with this frame.

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
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerPositionData>();
            state.RequireForUpdate<EnemyTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float3 playerPos = SystemAPI.GetSingleton<PlayerPositionData>().Position;

            // Predicted-velocity lookup — written every frame so weapons
            // (grenade launcher) can lead the target by predictedVel × flightTime.
            // Optional on the enemy archetype (defensive HasComponent below)
            // so we don't crash if some baked enemy variant lacks it.
            var predLookup = SystemAPI.GetComponentLookup<EnemyPredictedVelocity>(isReadOnly: false);

            foreach (var (transform, velocity, speed, enemy) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRO<MoveSpeed>>()
                              .WithAll<EnemyTag>()
                              .WithNone<Dead>()
                              .WithEntityAccess())
            {
                float3 selfPos = transform.ValueRO.Position;
                float3 toPlayer = playerPos - selfPos;
                toPlayer.y = 0f; // pursuit is XZ-only

                float distSq = math.lengthsq(toPlayer);

                // Preserve any vertical velocity (gravity / pop-up knockback / etc.)
                // and drop angular velocity so collisions don't tumble the body.
                float currentVy = velocity.ValueRO.Linear.y;
                velocity.ValueRW.Angular = float3.zero;

                if (distSq < 0.0001f)
                {
                    // On top of the player — stop horizontal motion.
                    velocity.ValueRW.Linear = new float3(0f, currentVy, 0f);
                    // Prediction also zero: enemy isn't heading anywhere
                    // useful, weapons should aim at current position.
                    if (predLookup.HasComponent(enemy))
                        predLookup[enemy] = new EnemyPredictedVelocity { Value = float3.zero };
                    continue;
                }

                float3 dir = toPlayer / math.sqrt(distSq);
                float speedVal = speed.ValueRO.Value;

                float3 newLinear = new float3(
                    dir.x * speedVal,
                    currentVy,
                    dir.z * speedVal);
                velocity.ValueRW.Linear = newLinear;

                // Publish the predicted velocity. We use the XZ component
                // only (Y is gravity/knockback, not pursuit intent) so
                // weapons leading the target don't accidentally aim
                // into the sky for a knockback-popped-up enemy.
                if (predLookup.HasComponent(enemy))
                {
                    predLookup[enemy] = new EnemyPredictedVelocity
                    {
                        Value = new float3(newLinear.x, 0f, newLinear.z)
                    };
                }

                // Face the player. Rotation written here survives the physics
                // step because angular velocity is zeroed every frame and the
                // body's locked rotation (PhysicsMass.InverseInertia = 0 set
                // on the prefab) keeps physics from re-orienting it.
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(dir, math.up());
            }
        }
    }
}

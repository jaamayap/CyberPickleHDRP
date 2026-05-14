// File: Assets/_CyberPickle/Code/DOTS/Systems/ProjectileMovementSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that advances all active projectile entities
// along their velocity each tick. Prefab entities are excluded
// automatically (they have the Prefab component, which the default
// query excludes).
//
// 2026-05-11 (M9 PR E): supports parabolic projectiles via two optional
// components:
//   - ProjectileGravity → velocity += Acceleration × dt each tick.
//     Stamped on grenade-style weapons (WeaponData.trajectory = Parabolic).
//   - ProjectileTumble  → rotation *= Euler(AnglesPerSecondRad × dt).
//     Visual flair so the grenade visibly spins through the air.
//
// Both components are queried as OPTIONAL (WithEntityAccess + manual
// lookup) so projectiles without them just fly straight as before.

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    public partial struct ProjectileMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Optional component lookups for parabolic / tumble. The
            // movement query stays simple (RW LocalTransform + RW velocity);
            // we apply gravity / tumble per-entity by looking up the optional
            // components.
            var gravityLookup = SystemAPI.GetComponentLookup<ProjectileGravity>(isReadOnly: true);
            var tumbleLookup  = SystemAPI.GetComponentLookup<ProjectileTumble>(isReadOnly: true);

            foreach (var (transform, velocity, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileVelocity>>()
                              .WithAll<ProjectileTag>()
                              .WithEntityAccess())
            {
                // 1. Advance position by current velocity.
                transform.ValueRW.Position += velocity.ValueRO.Value * deltaTime;

                // 2. Gravity (parabolic projectiles only): integrate
                //    acceleration into velocity. Order matters — apply
                //    AFTER the position update so the position uses the
                //    velocity from the start of the tick (semi-implicit
                //    Euler — stable for the projectile-arc use case).
                if (gravityLookup.HasComponent(entity))
                {
                    float3 g = gravityLookup[entity].Acceleration;
                    velocity.ValueRW.Value += g * deltaTime;
                }

                // 3. Tumble (parabolic visuals): accumulate rotation about
                //    each local axis. Quaternion-multiply the per-frame
                //    delta into the current rotation. Local-axis tumble
                //    (not world-axis) — feels like an object spinning in
                //    its own frame as it flies.
                if (tumbleLookup.HasComponent(entity))
                {
                    float3 ratesRad = tumbleLookup[entity].AnglesPerSecondRad;
                    float3 deltaEuler = ratesRad * deltaTime;
                    quaternion deltaRot = quaternion.EulerXYZ(deltaEuler);
                    transform.ValueRW.Rotation = math.mul(transform.ValueRO.Rotation, deltaRot);
                }
            }
        }
    }
}

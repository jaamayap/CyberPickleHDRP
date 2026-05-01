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

                // ─── 5. Mark entity Dead — movement system stops driving it ───
                em.AddComponent<Dead>(entity);

                // NOTE: Entity is NOT destroyed. It lives on as a corpse, simulated
                // by Unity Physics. Cleanup (despawn timer / distance / scene unload)
                // is a future system.
            }
        }
    }
}

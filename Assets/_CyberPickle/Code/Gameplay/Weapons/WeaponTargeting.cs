// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponTargeting.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Auto-targeting for weapons. Each frame queries the ECS world for all
// entities tagged as enemies, picks one based on TargetingStrategy, and
// rotates the weapon to face that target's position.
//
// Lives MonoBehaviour-side because weapons are spawned/parented to the
// player (a MonoBehaviour). Reads from the ECS world via EntityManager
// (no Burst — per-frame query is fine for the per-weapon count we have).

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.Gameplay.Weapons
{
    public enum TargetingStrategy
    {
        Closest,    // nearest enemy
        Weakest,    // lowest current HP
        Strongest,  // highest current HP
    }

    [DisallowMultipleComponent]
    public class WeaponTargeting : MonoBehaviour
    {
        [Header("Targeting")]
        [Tooltip("Strategy for picking which enemy to target each frame.")]
        [SerializeField] private TargetingStrategy strategy = TargetingStrategy.Closest;

        [Tooltip("Maximum range (world units) to consider a target.")]
        [SerializeField] private float range = 15f;

        [Header("Aim")]
        [Tooltip("Degrees per second the weapon can rotate to face the target. 720 = 2 full turns/sec.")]
        [SerializeField] private float rotationSpeed = 720f;

        /// <summary>The currently-locked enemy entity, or Entity.Null if no valid target in range.</summary>
        public Entity CurrentTarget { get; private set; } = Entity.Null;

        /// <summary>World-space position of the current target. Valid only when HasTarget is true.</summary>
        public Vector3 TargetPosition { get; private set; }

        public bool HasTarget => CurrentTarget != Entity.Null;

        private World world;
        private EntityManager entityManager;
        private EntityQuery enemyQuery;
        private bool initialized;

        private void Awake()
        {
            EnsureWorld();
        }

        private void EnsureWorld()
        {
            if (initialized) return;

            world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            entityManager = world.EntityManager;
            enemyQuery = entityManager.CreateEntityQuery(
                typeof(EnemyTag),
                typeof(Health),
                typeof(LocalTransform));
            initialized = true;
        }

        private void Update()
        {
            if (!initialized) EnsureWorld();
            if (!initialized) return;

            FindBestTarget();
            RotateTowardTarget();
        }

        private void FindBestTarget()
        {
            CurrentTarget = Entity.Null;

            if (enemyQuery.IsEmpty) return;

            NativeArray<Entity> enemies = enemyQuery.ToEntityArray(Allocator.Temp);
            float3 weaponPos = transform.position;
            float rangeSq = range * range;

            float bestScore = float.PositiveInfinity;
            Entity bestEntity = Entity.Null;
            float3 bestPos = float3.zero;

            for (int i = 0; i < enemies.Length; i++)
            {
                Entity e = enemies[i];
                LocalTransform t = entityManager.GetComponentData<LocalTransform>(e);
                Health h = entityManager.GetComponentData<Health>(e);

                float distSq = math.distancesq(weaponPos, t.Position);
                if (distSq > rangeSq) continue;

                // Score: lower is better. Closest -> distance. Weakest -> current HP.
                // Strongest -> negative HP (so highest HP scores lowest).
                float score = strategy switch
                {
                    TargetingStrategy.Closest   => distSq,
                    TargetingStrategy.Weakest   => h.Current,
                    TargetingStrategy.Strongest => -h.Current,
                    _                           => distSq
                };

                if (score < bestScore)
                {
                    bestScore = score;
                    bestEntity = e;
                    bestPos = t.Position;
                }
            }

            enemies.Dispose();

            CurrentTarget = bestEntity;
            if (HasTarget) TargetPosition = bestPos;
        }

        private void RotateTowardTarget()
        {
            if (!HasTarget) return;

            Vector3 toTarget = TargetPosition - transform.position;
            toTarget.y = 0f;  // keep weapon level on the XZ plane
            if (toTarget.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(toTarget, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
}

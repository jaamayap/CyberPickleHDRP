// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponFiring.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Auto-fires entity-based projectiles at the WeaponTargeting's current
// target on a fire-rate cooldown. Lives MonoBehaviour-side (one per
// weapon), but spawns ECS-side projectiles for runtime performance.
//
// Spawn flow:
//   1. On first Fire(), lazily resolve the projectile prefab entity by
//      querying the world for entities with ProjectileTag + Prefab.
//      (The user authors the prefab in a SubScene; the Baker in
//      ProjectileAuthoring marks it with the built-in Prefab component
//      so it's inactive until Instantiate creates a copy.)
//   2. EntityManager.Instantiate(prefabEntity) creates an active copy
//      with a Companion GameObject (Hovl visual particles) auto-attached
//      by Entities Graphics.
//   3. We override LocalTransform / ProjectileVelocity / ProjectileDamage
//      / ProjectileLifetime per-spawn so the same prefab can carry
//      different stats from different weapons.

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.Gameplay.Weapons
{
    [RequireComponent(typeof(WeaponTargeting))]
    [DisallowMultipleComponent]
    public class WeaponFiring : MonoBehaviour
    {
        [Header("Fire Rate")]
        [Tooltip("Shots per second.")]
        [SerializeField] private float fireRate = 2f;

        [Header("Spawn")]
        [Tooltip("Optional muzzle/barrel transform — projectiles spawn from here. Falls back to the weapon's own transform.")]
        [SerializeField] private Transform muzzle;

        [Header("Projectile Stats (per-spawn overrides)")]
        [Tooltip("Projectile travel speed (world units/sec).")]
        [SerializeField] private float projectileSpeed = 20f;

        [Tooltip("Damage applied to enemy on hit.")]
        [SerializeField] private float projectileDamage = 5f;

        [Tooltip("Maximum lifetime of a projectile in seconds (despawns if it doesn't hit).")]
        [SerializeField] private float projectileLifetime = 3f;

        private WeaponTargeting targeting;
        private float cooldown;

        private World world;
        private EntityManager entityManager;
        private Entity prefabEntity = Entity.Null;
        private bool dotsInitialized;

        private void Awake()
        {
            targeting = GetComponent<WeaponTargeting>();
            if (muzzle == null) muzzle = transform;
        }

        private void Update()
        {
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;
            if (!targeting.HasTarget) return;

            if (!ResolvePrefab()) return;

            Fire();
            cooldown = 1f / Mathf.Max(0.01f, fireRate);
        }

        private bool ResolvePrefab()
        {
            if (prefabEntity != Entity.Null) return true;

            if (!dotsInitialized)
            {
                world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return false;
                entityManager = world.EntityManager;
                dotsInitialized = true;
            }

            // Look up the projectile prefab via the ProjectilePrefabHolder
            // singleton, populated by ProjectilePrefabSetupAuthoring at bake
            // time. This indirection ensures the prefab was baked via
            // GetEntity(prefab, flags) — which sets up LinkedEntityGroup so
            // Instantiate duplicates the full hierarchy (visual children).
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ProjectilePrefabHolder));

            if (query.CalculateEntityCount() == 0)
            {
                // SubScene not loaded yet, or ProjectilePrefabSetupAuthoring not configured.
                return false;
            }

            ProjectilePrefabHolder holder = query.GetSingleton<ProjectilePrefabHolder>();
            if (holder.Value == Entity.Null)
            {
                Debug.LogWarning("[WeaponFiring] ProjectilePrefabHolder.Value is Entity.Null — did you forget to assign the projectilePrefab on ProjectilePrefabSetupAuthoring?");
                return false;
            }

            prefabEntity = holder.Value;
            return true;
        }

        private void Fire()
        {
            Entity projectile = entityManager.Instantiate(prefabEntity);

            float3 spawnPos = muzzle.position;
            quaternion spawnRot = muzzle.rotation;
            float3 velocity = ((float3)muzzle.forward) * projectileSpeed;

            entityManager.SetComponentData(projectile, LocalTransform.FromPositionRotation(spawnPos, spawnRot));
            entityManager.SetComponentData(projectile, new ProjectileVelocity { Value = velocity });
            entityManager.SetComponentData(projectile, new ProjectileDamage   { Value = projectileDamage });
            entityManager.SetComponentData(projectile, new Lifetime           { Remaining = projectileLifetime });
        }
    }
}

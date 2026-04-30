// File: Assets/_CyberPickle/Code/DOTS/Authoring/ProjectileAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Authors a projectile prefab into a baked entity prefab with:
//   - Prefab marker (inactive in the world until WeaponFiring instantiates)
//   - ProjectileTag, ProjectileVelocity, ProjectileDamage (data)
//   - Lifetime (auto-despawn timer; overridden per-spawn by WeaponFiring)
//   - HitVFXPrefabRef (entity reference to the linked hit VFX prefab,
//     spawned by ProjectileCollisionSystem on impact)
//
// To use: place this on the root of a projectile GameObject (e.g., a Hovl
// Studio projectile prefab with HS_ProjectileMover removed), then drop
// that GameObject into a SubScene attached to the Game scene. Plug a
// HitVFX GameObject prefab (also with HitVFXAuthoring) into the
// hitVFXPrefab field — the Baker will bake its entity prefab too and
// store the reference here.

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class ProjectileAuthoring : MonoBehaviour
    {
        [Header("Projectile Stats (per-spawn overrides)")]
        [Tooltip("Default speed (units/sec). Overridden per-spawn by WeaponFiring.")]
        public float defaultSpeed = 20f;

        [Tooltip("Default damage. Overridden per-spawn by WeaponFiring.")]
        public float defaultDamage = 5f;

        [Tooltip("Default lifetime (seconds) before auto-despawn. Overridden per-spawn.")]
        public float defaultLifetime = 3f;

        [Header("Linked VFX")]
        [Tooltip("Hit VFX GameObject prefab spawned at the projectile's position when it collides with an enemy. Should have a HitVFXAuthoring component on its root.")]
        public GameObject hitVFXPrefab;

        public class Baker : Baker<ProjectileAuthoring>
        {
            public override void Bake(ProjectileAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                // NOTE: do NOT manually AddComponent<Prefab> here. When this
                // prefab is referenced via GetEntity(prefab, flags) from a
                // SubScene baker (ProjectilePrefabSetupAuthoring), Unity sets
                // up Prefab + LinkedEntityGroup automatically — so Instantiate
                // duplicates the full hierarchy including visual children.

                AddComponent<ProjectileTag>(entity);
                AddComponent(entity, new ProjectileVelocity { Value = float3.zero });
                AddComponent(entity, new ProjectileDamage   { Value = authoring.defaultDamage });
                AddComponent(entity, new Lifetime           { Remaining = authoring.defaultLifetime });

                // Link the hit VFX prefab — Baker.GetEntity recursively bakes the
                // referenced GameObject prefab (which carries HitVFXAuthoring),
                // returning its entity prefab handle.
                if (authoring.hitVFXPrefab != null)
                {
                    Entity hitVfxPrefab = GetEntity(authoring.hitVFXPrefab, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new HitVFXPrefabRef { Value = hitVfxPrefab });
                }
                else
                {
                    AddComponent(entity, new HitVFXPrefabRef { Value = Entity.Null });
                }
            }
        }
    }
}

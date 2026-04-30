// File: Assets/_CyberPickle/Code/DOTS/Authoring/ProjectilePrefabSetupAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Place this on a single empty GameObject inside a SubScene attached to
// the Game scene. Drag the projectile prefab (a regular project asset)
// into the projectilePrefab field. At bake time, GetEntity recursively
// bakes the prefab and all its children — properly populating
// LinkedEntityGroup so EntityManager.Instantiate later duplicates the
// full visual hierarchy (Hovl Studio's particle children, lights, etc.).
//
// The resulting entity reference is stored in a singleton component
// (ProjectilePrefabHolder), which WeaponFiring queries at runtime.
//
// Why this layer exists: a baker can only be invoked from a SubScene
// authoring component. Our weapons (and projectile prefabs themselves)
// aren't baked — they're MonoBehaviours spawned at runtime. We need a
// trampoline in a SubScene that points to the prefab and runs the bake.
//
// When we add multiple projectile types (per-weapon variants), this
// becomes a registry component with a list/buffer of prefabs.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class ProjectilePrefabSetupAuthoring : MonoBehaviour
    {
        [Tooltip("The projectile prefab to bake (a regular project asset — NOT placed in this SubScene). The baker bakes its full hierarchy here.")]
        public GameObject projectilePrefab;

        public class Baker : Baker<ProjectilePrefabSetupAuthoring>
        {
            public override void Bake(ProjectilePrefabSetupAuthoring authoring)
            {
                // The setup GameObject doesn't need transform sync — it's just a holder.
                Entity setupEntity = GetEntity(TransformUsageFlags.None);

                Entity prefabEntity = (authoring.projectilePrefab != null)
                    ? GetEntity(authoring.projectilePrefab, TransformUsageFlags.Dynamic)
                    : Entity.Null;

                AddComponent(setupEntity, new ProjectilePrefabHolder { Value = prefabEntity });
            }
        }
    }
}

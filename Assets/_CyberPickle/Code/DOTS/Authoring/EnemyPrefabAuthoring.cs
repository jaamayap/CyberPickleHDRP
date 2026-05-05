// File: Assets/_CyberPickle/Code/DOTS/Authoring/EnemyPrefabAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Authoring component that registers a baked entity-prefab in the world
// so a runtime spawner can Instantiate() copies of it. Place this on a
// single GameObject inside the EnemisSubScene and assign the entity
// authoring prefab (e.g., Zombie1_Entity.prefab) to the field.
//
// At bake time:
//   1. The SubScene contains a "registry" GameObject carrying this
//      authoring component.
//   2. The Baker registers the entity authoring prefab through
//      Baker.GetEntity() — Unity bakes the prefab as a Prefab-tagged
//      entity in the world.
//   3. The Baker creates an EnemyPrefabSingleton on the registry entity
//      pointing at that baked prefab entity.
//
// Single-prefab MVP. Replaced by a multi-entry registry in chunk 5b.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class EnemyPrefabAuthoring : MonoBehaviour
    {
        [Tooltip("Entity authoring prefab — a lightweight prefab carrying EnemyAuthoring (and any other ECS authoring components). Should NOT carry SkinnedMeshRenderer / Animator; those live on the visual prefab referenced by EnemyData.")]
        public GameObject enemyPrefab;

        public class Baker : Baker<EnemyPrefabAuthoring>
        {
            public override void Bake(EnemyPrefabAuthoring authoring)
            {
                if (authoring.enemyPrefab == null)
                {
                    Debug.LogWarning($"[EnemyPrefabAuthoring] '{authoring.name}' has no enemyPrefab assigned — no entity prefab will be registered.", authoring);
                    return;
                }

                // Self entity is just a holder for the singleton.
                Entity self = GetEntity(TransformUsageFlags.None);

                // Register the prefab GameObject as a baked entity. Unity will
                // bake it once and tag it with Prefab so it doesn't run as a
                // live entity. Returned Entity is the prefab-entity to instantiate.
                Entity prefabEntity = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic);

                AddComponent(self, new EnemyPrefabSingleton
                {
                    Value = prefabEntity
                });
            }
        }
    }
}

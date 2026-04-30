// File: Assets/_CyberPickle/Code/DOTS/Authoring/EnemyAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// MonoBehaviour authoring component for placing enemies in SubScenes.
// At bake time (when the SubScene is closed in the editor or the project
// builds), the Baker converts the GameObject + this component into an
// entity with EnemyTag + Health + MoveSpeed components.
//
// To use:
//   1. Open or create a SubScene
//   2. Create a GameObject (e.g., a Cube primitive) inside the SubScene
//   3. Add this component to that GameObject
//   4. Tune health / speed in the inspector
//   5. Close the SubScene → the GameObject is replaced by an entity
//      with the right components at runtime

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Authoring
{
    public class EnemyAuthoring : MonoBehaviour
    {
        [Tooltip("Starting health for this enemy.")]
        public float maxHealth = 10f;

        [Tooltip("Movement speed toward the player, in world units/second.")]
        public float moveSpeed = 2f;

        public class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                // Dynamic = the entity will move at runtime (LocalTransform is writable).
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<EnemyTag>(entity);
                AddComponent(entity, new Health
                {
                    Current = authoring.maxHealth,
                    Max     = authoring.maxHealth
                });
                AddComponent(entity, new MoveSpeed
                {
                    Value = authoring.moveSpeed
                });
            }
        }
    }
}

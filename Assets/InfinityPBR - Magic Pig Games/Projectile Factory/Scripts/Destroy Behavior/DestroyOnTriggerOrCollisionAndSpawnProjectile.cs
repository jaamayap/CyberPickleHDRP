using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Destroy and Spawn Projectile",
        menuName = "Projectile Factory/Destroy Behavior/Destroy and Spawn Projectile")]
    [Serializable]
    public class DestroyOnTriggerOrCollisionAndSpawnProjectile : DestroyOnTriggerOrCollision
    {
        public GameObject objectToSpawn;
        public Vector3 rotationFromParent = new(0, 180, 0);

        protected bool _spawned;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This behavior destroys the projectile and spawns a new one at the collision point.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "CreateObject";

        public override void LaunchProjectile(Projectile projectile)
        {
            _spawned = false;
        }

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var pointOfCollision = collider.ClosestPointOnBounds(ProjectilePosition);
            DoDestroyAndSpawn(pointOfCollision);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var pointOfCollision = collision.contacts[0].point;
            DoDestroyAndSpawn(pointOfCollision);
        }

        public override void DoDestroy(Projectile projectile)
        {
            Debug.Log($"Do Destroy. Spawned is {_spawned} ");
            DoDestroyAndSpawn(Projectile.transform.position);
        }

        public virtual void DoDestroyAndSpawn(Vector3 position)
        {
            SpawnNewProjectile(Projectile.transform, position, Projectile.Target);
            RemoveObject();
        }

        protected virtual void SpawnNewProjectile(Transform parentTransform, Vector3 position, GameObject target)
        {
            if (objectToSpawn == null || _spawned) return;

            _spawned = true;

            Debug.Log($"{parentTransform.gameObject.name} is spawning a new one");
            var newProjectileObject = Instantiate(objectToSpawn, position, Quaternion.identity);
            var newProjectile = newProjectileObject.GetComponent<Projectile>();
            newProjectile.SetProjectileSpawner(ProjectileSpawner);
            newProjectile.ParentPrefab = objectToSpawn;
            parentTransform.Rotate(rotationFromParent);
            newProjectile.Launch(parentTransform, target);
        }
    }
}
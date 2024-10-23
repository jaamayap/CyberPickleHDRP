using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class MultiShotProjectile : MonoBehaviour, ICanLaunch
    {
        /*
        [Header("Required")]
        public GameObject projectilePrefab;

        [Header("Options")]
        public int numberOfProjectiles = 5;
        public float startAngle = 0f;
        [Range(0f, 360f)]
        public float spreadAngle = 90f;
        public TrajectoryBehavior preLaunchTrajectory;
        public virtual bool HasPreLaunchTrajectoryBehavior => preLaunchTrajectory != null;
        public TrajectoryBehavior PreLaunchTrajectory() => preLaunchTrajectory;

        private float AngleStep => spreadAngle / (numberOfProjectiles -1);
        private float AngleAdjustment => (numberOfProjectiles % 2 == 0) ? AngleStep / 2 : 0;

        public virtual void Launch(Transform spawnTransform, GameObject targetObject)
        {
            var currentAngle = startAngle - (spreadAngle / 2f) + AngleAdjustment;
            currentAngle += ProjectileSpawner.RotatingTransform.eulerAngles.y;

            for (var i = 0; i < numberOfProjectiles; i++)
            {
                var projectileObject = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
                var projectile = projectileObject.GetComponent<Projectile>();

                projectile.transform.rotation = Quaternion.Euler(0f, currentAngle, 0f);
                projectile.SetProjectileSpawner(ProjectileSpawner);
                projectile.Launch(spawnTransform, targetObject);
                currentAngle += AngleStep;
            }
        }

        private ProjectileSpawner _projectileSpawner;
        public void SetProjectileSpawner(ProjectileSpawner value) => _projectileSpawner = value;
        public ProjectileSpawner ProjectileSpawner => _projectileSpawner;

        private GameObject _target;
        public virtual void SetTarget(GameObject value) => _target = value;
        public GameObject Target => _target;
        */
        /*
        public virtual void LookAt(Vector3 value, bool onInstantiate = false)
        {
            switch (onInstantiate)
            {
                case true:
                    transform.LookAt(ProjectileSpawner.SpawnForwardPosition);
                    return;
                default:
                    Debug.Log($"value is {value} and onInstantiate is {onInstantiate}");

                    // Convert value from a direction in world space to a direction in local space
                    var localDirection = transform.InverseTransformDirection(value);
                    transform.LookAt(localDirection);
                    break;
            }
        }
        */
        public void Launch(Transform spawnTransform, GameObject targetObject)
        {
            throw new NotImplementedException();
        }

        public void SetProjectileSpawner(ProjectileSpawner projectileSpawner)
        {
            throw new NotImplementedException();
        }

        public void SetTarget(GameObject value)
        {
            throw new NotImplementedException();
        }

        public TrajectoryBehavior PreLaunchTrajectory()
        {
            throw new NotImplementedException();
        }

        public void LookAt(Vector3 value, bool onInstantiate = false)
        {
            throw new NotImplementedException();
        }
    }
}
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    public interface ICanLaunch
    {
        void Launch(Transform spawnTransform, GameObject targetObject);
        void SetProjectileSpawner(ProjectileSpawner projectileSpawner);

        void SetTarget(GameObject value);

        //void LookAt(Vector3 value, bool onInstantiate = false);
        TrajectoryBehavior PreLaunchTrajectory();
    }
}
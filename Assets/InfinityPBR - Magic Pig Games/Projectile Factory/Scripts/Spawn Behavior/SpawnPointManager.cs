using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Sequential Spawn Points",
        menuName = "Projectile Factory/Spawn Point Managers/Sequential Spawn Points")]
    [Serializable]
    public class SpawnPointManager : ScriptableObject
    {
        public virtual int NextSpawnPointIndex(ProjectileSpawner projectileSpawner, int value = 1)
        {
            if (projectileSpawner.spawnPoints.Count == 0)
                throw new InvalidOperationException("There are no spawn points available.");

            projectileSpawner.SpawnPointIndex += value;

            if (projectileSpawner.SpawnPointIndex < 0)
                projectileSpawner.SpawnPointIndex += projectileSpawner.spawnPoints.Count;
            projectileSpawner.SpawnPointIndex %= projectileSpawner.spawnPoints.Count;

            return projectileSpawner.SpawnPointIndex;
        }
    }
}
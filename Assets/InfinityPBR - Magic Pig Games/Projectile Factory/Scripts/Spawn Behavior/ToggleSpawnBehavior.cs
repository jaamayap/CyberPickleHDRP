using System;
using System.Collections;
using UnityEngine;

/*
 * This inherits from SpawnBehavior. The only difference is that this one, when the LaunchProjectiles coroutine is
 * finished, it will set the _isSpawning flag to false. This is useful for lasers and other projectiles that will
 * turn "on", and then should stay on, without new ones spawning, until they are stopped.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Toggle Spawn Behavior",
        menuName = "Projectile Factory/Spawn Behavior/Toggle Spawn Behavior")]
    [Serializable]
    public class ToggleSpawnBehavior : SpawnBehavior
    {
        [Header("Toggle Options")]
        [Tooltip(
            "If this is less than 0, then it will not auto stop. You will need to call Stop() on the ProjectileSpawner " +
            "to stop it.")]
        public float autoStopDelay = -1f;

        private float _autoStopTimer;

        protected override void LaunchCoroutineEnd()
        {
            if (autoStopDelay < 0) return;

            _autoStopTimer = autoStopDelay;
            CoroutineManager.Instance.StartCoroutine(AutoStop());
        }

        private IEnumerator AutoStop()
        {
            while (_autoStopTimer > 0)
            {
                _autoStopTimer -= Time.deltaTime;
                yield return null;
            }

            StopSpawning();
        }

        public void StopSpawning()
        {
            Debug.Log("Stop Spawning on Toggle Spawn Behavior");
            _isSpawning = false;
        }
    }
}
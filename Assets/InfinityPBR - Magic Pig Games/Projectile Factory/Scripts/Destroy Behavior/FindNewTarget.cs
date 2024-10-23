using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.ProjectileFactory
{
    public enum TargetSelection
    {
        Random,
        ClosestToSpawner,
        ClosestToProjectile,
        Custom
    }

    [CreateAssetMenu(fileName = "New Find New Target",
        menuName = "Projectile Factory/Generic Behavior/Find New Target")]
    [Serializable]
    public class FindNewTarget : ProjectileBehavior
    {
        [Header("Search Settings")]
        [Tooltip("The delay before the projectile searches for its first new target.")]
        [ShowInProjectileEditor("Start Search Delay")]
        public float startSearchDelay;

        [Tooltip("The number of attempts to find a new target. If the number of attempts is reached, the projectile " +
                 "will be destroyed.")]
        [ShowInProjectileEditor("Search Attempts")]
        public int searchAttempts = 5;

        [Tooltip("The delay between each search attempt.")] [ShowInProjectileEditor("Delay Between Attempts")]
        public float delayBetweenAttempts = 1f;

        [Tooltip("The radius to search for a new target.")] [ShowInProjectileEditor("Search Radius")]
        public float searchRadius = 50f;

        [Tooltip("The selection method for the target.")] [ShowInProjectileEditor("Target Selection")]
        public TargetSelection targetSelection;

        [Header("Other Options")]
        [Tooltip("When true, the projectile will seek new, closer targets. Otherwise, it will keep the first target " +
                 "it finds. If it keeps seeking, it will use up its search attempts, but if it has a target, and no " +
                 "more attempts, it will continue to seek the active target.")]
        [ShowInProjectileEditor("Seek New Targets")]
        public bool seekNewTargets;

        [Tooltip("When true, the projectile will inherit the default target of the spawner. Otherwise, it will " +
                 "always search for a target at least once, potentially assigning a different target to the Projectile.")]
        [ShowInProjectileEditor("Inherit Default Target")]
        public bool inheritDefaultTarget;

        [Tooltip("When true, the projectile will use the collision mask of the projectile. Otherwise, it will use " +
                 "the custom collision mask.")]
        public bool overrideCollisionMask;

        [Tooltip("The custom collision mask to use when searching for a new target.")]
        public LayerMask customCollisionMask;

        private int _searchAttemptsUsed;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Finds a new target for the projectile.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Question";
        public LayerMask CollisionMask => overrideCollisionMask ? customCollisionMask : Projectile.CollisionMask;

        public override void LaunchProjectile(Projectile projectile)
        {
            if (!Projectile.gameObject.activeInHierarchy)
                Projectile.gameObject.SetActive(true);
            // Start Coroutine to search for a new target
            Projectile.StartCoroutine(SearchForNewTarget(Projectile));
        }

        protected virtual void CustomTarget(Projectile projectile, Collider[] colliders)
        {
            Debug.Log("Custom Target is selected, but this class is not yet overridden so we will default " +
                      "to the closest target to the projectile. If you want to use a custom target, you must create " +
                      "a new class that inherits from FindNewTarget and override the CustomTarget method.");
            FindClosestTargetInRadius(projectile, colliders);
        }

        protected virtual GameObject GetTarget(Projectile projectile)
        {
            var sourcePosition = targetSelection == TargetSelection.ClosestToSpawner
                ? projectile.ProjectileSpawner.transform.position
                : ProjectilePosition;

            var colliders = GetCollidersInRange(projectile, sourcePosition);

            if (targetSelection is TargetSelection.ClosestToProjectile or TargetSelection.ClosestToSpawner)
                return FindClosestTargetInRadius(projectile, colliders);

            if (targetSelection == TargetSelection.Random)
                return colliders.Length > 0 ? colliders[Random.Range(0, colliders.Length)].gameObject : null;

            // Fallback
            return FindClosestTargetInRadius(projectile, colliders);
        }

        private IEnumerator SearchForNewTarget(Projectile projectile)
        {
            // Wait for the startSearchDelay
            yield return new WaitForSeconds(startSearchDelay);

            // If we have a target, we don't need to search for a new one
            if (projectile.Target != null && (_searchAttemptsUsed > 0 || inheritDefaultTarget))
                yield break;

            // If we don't have a target, we need to search for one
            for (var i = 0; i < searchAttempts; i++)
            {
                var newTarget = GetTarget(projectile);

                if (newTarget != null)
                {
                    projectile.SetTarget(newTarget); // Set the target

                    // If we aren't going to seek new targets, we're done!
                    if (!seekNewTargets || (seekNewTargets && _searchAttemptsUsed == searchAttempts))
                        yield break;
                }

                yield return new WaitForSeconds(delayBetweenAttempts);
            }

            // If we didn't find a target, we destroy the projectile
            projectile.TriggerDestroy();
        }


        protected virtual Collider[] GetCollidersInRange(Projectile projectile, Vector3 sourcePosition)
        {
            return Physics.OverlapSphere(sourcePosition, searchRadius, CollisionMask);
        }

        private GameObject FindClosestTargetInRadius(Projectile projectile, Collider[] colliders)
        {
            var closestDistance = float.MaxValue;
            GameObject closestTarget = null;
            foreach (var col in colliders)
            {
                if (col.gameObject == projectile.ProjectileSpawner.gameObject) continue; // Ignore ourselves
                if (col.gameObject == projectile.gameObject) continue; // Ignore the projectile
                var distance = (col.transform.position - projectile.transform.position).sqrMagnitude;
                if (!(distance < closestDistance)) continue;
                closestDistance = distance;
                closestTarget = col.gameObject;
            }

            return closestTarget;
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            // Reset
            _searchAttemptsUsed = 0;
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }
    }
}
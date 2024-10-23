using System;
using System.Collections;
using System.Collections.Generic;
using InfinityPBR;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

/*
 * Spawn Behavior is a ScriptableObject that controls how a projectile is spawned. At its most basic, it controls the
 * number of projectiles being spawned, with some additional options. You can override this to create custom spawning
 * logic that fits your project more precisely.
 *
 * Note: When the ProjectileSpawner does not have a SpawnBehavior objects assigned, it will spawn a single projectile
 * with no delay, forward from the spawn position. SpawnBehavior is only required if you want to have something other
 * than this.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawn Behavior", menuName = "Projectile Factory/Spawn Behavior/Spawn Behavior")]
    [Serializable]
    public class SpawnBehavior : ScriptableObject
    {
        public enum MultiShotBehavior
        {
            Allowed,
            Blocked,
            StopActiveShot
        }

        public enum SpreadPattern
        {
            Random, // Random value between min and max, can be ANY value between those
            RandomIntervals, // Randomly chosen, but will be on an equal interval between min and max. May repeat.
            RandomIntervalsNoRepeat, // Randomly chosen, but will use all potential spread rotations before repeating
            NegativeToPositive, // Often this means left to right
            PositiveToNegative // Often this means right to left
        }

        [Header("Projectile Control")]
        [Tooltip("Controls how many projectiles will be spawned.")]
        [Min(1)]
        [ShowInProjectileEditor("Number of Projectiles")]
        public int numberOfProjectiles = 1;

        [Tooltip("This determines what happens when the Launch() method is called while the firing sequence is " +
                 "already active.")]
        [ShowInProjectileEditor("Multi Shot Behavior")]
        public MultiShotBehavior multiShotBehavior = MultiShotBehavior.Allowed;

        [Tooltip("Optional delay before the first projectile is spawned.")]
        [Min(0)]
        [ShowInProjectileEditor("Delay Before First Projectile")]
        public float delayBeforeFirstProjectile;

        [Tooltip("Optional delay between each projectile.")]
        [Min(0)]
        [ShowInProjectileEditor("Delay Between Projectiles")]
        public float delayBetweenProjectiles;

        [FormerlySerializedAs("switchBarrelsBetweenShots")]
        [Tooltip("If false, all projectiles from one volley will be spawned from the same barrel.")]
        [ShowInProjectileEditor("Switch Barrels Between Shots")]
        public bool switchSpawnPointsBetweenShots = true;

        [Tooltip(
            "Projectile spawn position will be offset by this value, prior to computing the Forward Offset value.")]
        [ShowInProjectileEditor("Position Offset From Spawn Position")]
        public Vector3 positionOffsetFromSpawnPosition = Vector3.zero;

        [FormerlySerializedAs("offsetFromSpawnPosition")]
        [Tooltip(
            "Projectile will be spawned at this offset from the spawn position, at the angle of fire. Useful for " +
            "situations where the projectiles should spawn \"around\" the spawner, and avoid colliding with the " +
            "spawner itself.")]
        [ShowInProjectileEditor("Forward Offset From Spawn Position")]
        public float forwardOffsetFromSpawnPosition;

        [FormerlySerializedAs("startAngle")]
        [Header("Spread Options")]
        [Tooltip("The left/right angle of fire from the local direction of the spawner. A value of 0 is \"Forward\".")]
        [ShowInProjectileEditor("Start Angle X")]
        public float startAngleX;

        [Tooltip("The up/down angle of fire from the local direction of the spawner. A value of 0 is \"Forward\".")]
        [ShowInProjectileEditor("Start Angle Y")]
        public float startAngleY;

        [FormerlySerializedAs("spreadAngle")]
        [Tooltip("Maximum angle of horizontal firing. Will be 1/2 this value to the left and right of the startAngleX")]
        [ShowInProjectileEditor("Spread Angle X")]
        [Range(0f, 360f)]
        public float spreadAngleX;

        [Tooltip("Maximum angle of vertical firing. Will be 1/2 this value to the up and down of the startAngleY")]
        [Range(0f, 360f)]
        [ShowInProjectileEditor("Spread Angle Y")]
        public float spreadAngleY;

        [Tooltip("Determines the horizontal spread pattern. If spreadAngleX is 0, shots will fire forward based on " +
                 "startAngleX. Mix this with spreadPatternY for various effects.")]
        [ShowInProjectileEditor("Spread Pattern X")]
        public SpreadPattern spreadPatternX = SpreadPattern.Random;

        [Tooltip("Determines the vertical spread pattern. If spreadAngleY is 0, shots will fire forward based on " +
                 "startAngleY. Mix this with spreadPatternX for various effects.")]
        [ShowInProjectileEditor("Spread Pattern Y")]
        public SpreadPattern spreadPatternY = SpreadPattern.Random;

        [Header("Options")]
        [Tooltip(
            "When true, spawning will be stopped by the Stop(false) method. If you want the spawner to always emit " +
            "the full number of particles, set this false. If you want the spawning to stop, such as when the player " +
            "takes their finger off the trigger, keep this true. Spawning will stop if the spawner is destroyed or " +
            "disabled.")]
        [ShowInProjectileEditor("Can Stop Mid Spawning")]
        public bool canStopMidSpawning = true;

        [Tooltip("If true, when the ProjectileSpawner is missing or destroyed, this will stop spawning projectiles.")]
        [ShowInProjectileEditor("Stop On Spawner Destroy")]
        public bool stopOnSpawnerDestroy = true;

        [Header("Repetition")]
        [Min(-1)]
        [Tooltip("0 = No repetition. -1 = Repeat forever. 1 = Repeat once, etc.")]
        [ShowInProjectileEditor("Repeat Count")]
        public int repeatCount;

        [Min(0)] [Tooltip("Delay between repetitions.")] [ShowInProjectileEditor("Repeat Delay")]
        public float repeatDelay;

        [FormerlySerializedAs("switchBarrelsBetweenVolleys")]
        [Tooltip("If true, the barrels will switch between each repetition of particle groups. IMPORTANT: If " +
                 "switchBarrelsBetweenShots is true, this will be ignored.")]
        [ShowInProjectileEditor("Switch Barrels Between Volleys")]
        public bool switchSpawnPointsBetweenVolleys = true;

        private List<float> _availableIntervalValues;
        private List<float> _intervals;

        protected bool _isSpawning;

        // Method to provide a StopPosition (Vector3) for each projectile, which is the "forward" position based on
        // the rotation of the spawner. This is used by the ProjectileSpawner to determine where to stop each projectile.
        /*
        public virtual Vector3 ForwardPosition(int projectileIndex, float length = 1)
        {
            var angleX = startAngleX - (spreadAngleX / 2f) + AngleAdjustmentX;
            angleX += AngleStepX * projectileIndex;

            var angleY = startAngleY - (spreadAngleY / 2f) + AngleAdjustmentY;
            angleY += AngleStepY * projectileIndex;

            var rotation = Quaternion.Euler(angleY, angleX, 0);
            var rotatedTransform = rotation * ProjectileSpawner.RotatingTransform.forward;

            return LaunchPosition(angleX + ProjectileSpawner.RotatingTransform.eulerAngles.y, angleY) + rotatedTransform * length;
        }*/

        protected Coroutine _launchCoroutine;

        private Vector3 _rotatingTransformEulerAngles;

        private Vector3 _tiltingTransformEulerAngles;

        private WaitForSeconds DelayBeforeFirstProjectileWait;
        private WaitForSeconds DelayBetweenProjectilesWait;
        private WaitForSeconds DelayBetweenRepetitionsWait;

        protected int repetitionCount;

        private float totalAngleStepX;
        private float totalAngleStepY;

        /*
        [Header("Pre Launch Trajectory Override")]
        [Tooltip("When populated, this trajectory will be used pre-launch rather than the trajectory on the Projectile. " +
                 "This is useful for situations where there are multiple projectiles being launched, to display the " +
                 "correct trajectories. Generally the trajectory on a projectile will assume only one projectile will " +
                 "be spawned.")]
        [ShowInProjectileEditor("Show Trajectory Per Projectile")]
        public bool showTrajectoryPerProjectile = true;
        */

        // March 3 2024 -- I think we aren't using this. However... I'm not sure. I'm going to leave it in for now.
        //public TrajectoryBehavior preLaunchTrajectory;
        //public virtual bool HasPreLaunchTrajectoryBehavior => preLaunchTrajectory != null;
        //public TrajectoryBehavior PreLaunchTrajectory() => preLaunchTrajectory;

        private float AngleStepX => spreadAngleX / (numberOfProjectiles - 1);
        private float AngleStepY => spreadAngleY / (numberOfProjectiles - 1);

        private float AngleAdjustmentX =>
            numberOfProjectiles == 2 ? 0 : numberOfProjectiles % 2 == 0 ? AngleStepX / 2 : 0;

        private float AngleAdjustmentY =>
            numberOfProjectiles == 2 ? 0 : numberOfProjectiles % 2 == 0 ? AngleStepY / 2 : 0;

        public ProjectileSpawner Spawner { get; private set; }

        protected GameObject ProjectilePrefab => Spawner.ProjectileObject;
        protected Transform SpawnTransform => Spawner.SpawnTransform;
        protected GameObject Target => Spawner.Target;

        protected bool SpawnerIsAlive => Spawner != null && Spawner.gameObject != null;
        public virtual bool IsSpawning => _isSpawning;

        public void SetProjectileSpawner(ProjectileSpawner value)
        {
            Spawner = value;
        }

        // Method to provide a StartPosition (Vector3) for each projectile, based on the offsets
        // provided in the SpawnBehavior. This is used by the ProjectileSpawner to determine where
        // to spawn each projectile.
        public virtual Vector3 StartPosition(int projectileIndex)
        {
            var angleX = startAngleX - spreadAngleX / 2f + AngleAdjustmentX;
            angleX += Spawner.RotatingTransform.eulerAngles.y;
            angleX += AngleStepX * projectileIndex;

            var angleY = startAngleY - spreadAngleY / 2f + AngleAdjustmentY;
            angleY += Spawner.RotatingTransform.eulerAngles.y;
            angleY += AngleStepY * projectileIndex;

            return LaunchPosition(angleX, angleY);
        }

        public bool Launch(ProjectileSpawner spawner)
        {
            // If we can't have multi shots, then return.
            if (IsSpawning && multiShotBehavior == MultiShotBehavior.Blocked)
                return false;

            // If we must stop the active shot, do that first -- use forceStop
            if (IsSpawning && multiShotBehavior == MultiShotBehavior.StopActiveShot)
                Stop();

            _launchCoroutine = CoroutineManager.Instance.StartCoroutine(LaunchProjectiles(spawner));
            return true;
        }

        protected virtual void InitializeWaitForSeconds()
        {
            // Note to future self: If we want to have an option to only delay before the FIRST projectile, then
            // update this line here. 

            DelayBeforeFirstProjectileWait = new WaitForSeconds(delayBeforeFirstProjectile);
            DelayBetweenProjectilesWait = new WaitForSeconds(delayBetweenProjectiles);

            // Infinite loop protection
            if (repeatCount == -1 && repeatDelay == 0)
                repeatDelay = 0.05f;

            DelayBetweenRepetitionsWait = new WaitForSeconds(repeatDelay);
        }

        public virtual IEnumerator LaunchProjectiles(ProjectileSpawner spawner)
        {
            LaunchCoroutineStart(spawner);
            
            yield return DelayBeforeFirstProjectileWait;
            do
            {
                totalAngleStepX = 0;
                totalAngleStepY = 0;

                for (var i = 0; i < numberOfProjectiles; i++)
                {
                    if (!SpawnerIsAlive && stopOnSpawnerDestroy)
                    {
                        Stop();
                        yield break;
                    }

                    //SpawnPositionLookAtTarget();
                    LaunchProjectile(i);

                    if (i != numberOfProjectiles - 1 && delayBetweenProjectiles != 0)
                        yield return DelayBetweenProjectilesWait;
                }

                if (repeatCount != -1)
                    repetitionCount++;

                yield return DelayBetweenRepetitionsWait;
            } while (repeatCount == -1 || repetitionCount < repeatCount);

            LaunchCoroutineEnd();
        }

        private void SpawnPositionLookAtTarget()
        {
            if (!Spawner.spawnPositionLookAtTarget) 
                return;

            if (Spawner.Target == null)
                return;
            
            Spawner.SpawnTransform.LookAt(Target.transform.position);
        }

        private void LaunchProjectile(int index)
        {
            CacheEulerAngles(); // Save the current angles of the spawning object
            var currentAngleX = CalculateCurrentAngle(spreadPatternX, _rotatingTransformEulerAngles.y,
                startAngleX, spreadAngleX, AngleStepX, index, totalAngleStepX, AngleAdjustmentX);
            var currentAngleY = CalculateCurrentAngle(spreadPatternY, _tiltingTransformEulerAngles.x,
                startAngleY, spreadAngleY, AngleStepY, index, totalAngleStepY, AngleAdjustmentY);

            SpawnProjectile(currentAngleY, currentAngleX);

            // If we are switching barrels between shots, do that now.
            // If we are switching between volleys, and this is the first shot, switch now.
            if (switchSpawnPointsBetweenShots || (switchSpawnPointsBetweenVolleys && index == numberOfProjectiles - 1))
                Spawner.NextSpawnPoint();

            totalAngleStepX += AngleStepX;
            totalAngleStepY += AngleStepY;
        }

        private void LaunchCoroutineStart(ProjectileSpawner spawner)
        {
            _isSpawning = true;
            repetitionCount = 0;

            SetProjectileSpawner(spawner);
            Spawner.DebugMessage("<color=green>Launch Coroutine Start</color>");
            InitializeWaitForSeconds();
        }

        protected virtual void LaunchCoroutineEnd()
        {
            _launchCoroutine = null;
            _isSpawning = false;
        }

        private float CalculateCurrentAngle(SpreadPattern spreadPattern, float eulerAngles, float startAngle,
            float spreadAngle, float angleStep, int i, float totalAngleStep, float angleAdjustment)
        {
            return spreadPattern switch
            {
                SpreadPattern.Random => DetermineAngleRandom(eulerAngles, startAngle, spreadAngle),
                SpreadPattern.RandomIntervals => DetermineAngleRandomInterval(angleStep, eulerAngles, startAngle,
                    spreadAngle, angleAdjustment, i, true),
                SpreadPattern.RandomIntervalsNoRepeat => DetermineAngleRandomInterval(angleStep, eulerAngles,
                    startAngle, spreadAngle, angleAdjustment, i, false),
                SpreadPattern.NegativeToPositive => DetermineAngleNegativeToPositive(totalAngleStep, eulerAngles,
                    startAngle, spreadAngle, angleAdjustment),
                SpreadPattern.PositiveToNegative => DetermineAnglePositiveToNegative(totalAngleStep, eulerAngles,
                    startAngle, spreadAngle, angleAdjustment),
                _ => throw new ArgumentOutOfRangeException(nameof(spreadPattern), spreadPattern, null)
            };
        }

        private void ComputeIntervalValues(float angleStep, float startAngle, float spreadAngle, float angleAdjustment)
        {
            _availableIntervalValues = new List<float>();
            var currentAngle = startAngle - spreadAngle / 2f + angleAdjustment;
            for (var i = 0; i < numberOfProjectiles; i++)
            {
                _availableIntervalValues.Add(currentAngle);
                currentAngle += angleStep;
            }
        }

        private void SetIntervalValues(bool repeat)
        {
            _intervals = new List<float>();
            for (var i = 0; i < numberOfProjectiles; i++)
            {
                var randomIndex = _availableIntervalValues.TakeRandomIndex();
                _intervals.Add(_availableIntervalValues[randomIndex]);
                if (repeat) continue;

                _availableIntervalValues[randomIndex] = _availableIntervalValues[_availableIntervalValues.Count - 1];
                _availableIntervalValues.RemoveAt(_availableIntervalValues.Count - 1);
            }
        }

        public float DetermineAngleRandomInterval(float angleStep, float eulerAngles, float startAngle,
            float spreadAngle, float angleAdjustment, int interval, bool repeat)
        {
            if (interval == 0)
            {
                ComputeIntervalValues(angleStep, startAngle, spreadAngle, angleAdjustment);
                SetIntervalValues(repeat);
            }

            return _intervals[interval] + eulerAngles;
        }


        private float DetermineAngleRandom(float rotation, float startAngle, float spreadAngle)
        {
            // Calculate a random angle between startAngle - (spreadAngle / 2f) and startAngle + (spreadAngle / 2f)
            var randomAngle = Random.Range(startAngle - spreadAngle / 2f, startAngle + spreadAngle / 2f);
            return randomAngle + rotation;
        }

        private float DetermineAngleNegativeToPositive(float totalAngleStep, float rotation, float startAngle,
            float spreadAngle, float angleAdjustment)
        {
            var currentAngle = startAngle - spreadAngle / 2f + angleAdjustment;
            currentAngle += rotation;
            return currentAngle + totalAngleStep;
        }

        private float DetermineAnglePositiveToNegative(float totalAngleStep, float rotation, float startAngle,
            float spreadAngle, float angleAdjustment)
        {
            var currentAngle = startAngle + spreadAngle / 2f - angleAdjustment;
            currentAngle -= rotation;
            return currentAngle - totalAngleStep;
        }

        private float DetermineAngle(float rotation, float startAngle, float spreadAngle, float angleAdjustment)
        {
            var currentAngle = startAngle - spreadAngle / 2f + angleAdjustment;
            currentAngle += rotation;

            return currentAngle;
        }

        private void SpawnProjectile(float angleY, float angleX)
        {
            Spawner.DebugMessage("Spawn Projectile");
            
            GameObject projectileObject;

            var projectile = ProjectilePrefab.GetComponent<Projectile>();

            if (projectile.useObjectPool && ProjectilePoolManager.instance == null)
                Debug.LogWarning("There is no ProjectilePoolManager in the scene. Please bring one into the " +
                                 "scene to use the Projectile Spawner, or set usePoolManager to false in your " +
                                 "Projectile.");

            var objectCameFromPool = false;

            // If we aren't using the objectPool or the pool manager is not found, create a new object
            if (!projectile.useObjectPool || ProjectilePoolManager.instance == null)
            {
                projectileObject = InstantiateNewProjectile(ProjectilePrefab, LaunchPosition(angleX, angleY),
                    Quaternion.identity);
            }
            else
            {
                // Get a projectile from the pool -- if one is not found (null), create a new one instead.
                projectileObject = ProjectilePoolManager.instance.GetProjectile(ProjectilePrefab);
                if (projectileObject == null)
                {
                    projectileObject = InstantiateNewProjectile(ProjectilePrefab, LaunchPosition(angleX, angleY),
                        Quaternion.identity);
                }
                else
                {
                    // Reset the position and rotation
                    projectileObject.SetActive(false); // Turn off while we move it to avoid particle system issues
                    projectileObject.transform.position = LaunchPosition(angleX, angleY);
                    projectileObject.transform.rotation = Quaternion.identity;
                    projectileObject.SetActive(true);
                    objectCameFromPool = true;
                }
            }
            
            // Update the layer of the projectile and its children, if option is set
            if (Spawner.setLayerOfProjectile)
                ProjectileUtilities.SetLayer(projectileObject, Spawner.projectileLayer, true);

            // Get the instance of the object
            projectile = projectileObject.GetComponent<Projectile>();
            projectile.enabled = true; // Make sure this is enabled -- after getting from pool, it may not be.
            projectile.ParentPrefab = ProjectilePrefab; // Ensure the parent object is linked

            projectile.transform.rotation =
                Quaternion.Euler(angleY, angleX, 0f); // Set the starting rotation of the projectile
            projectile.SetProjectileSpawner(Spawner); // Set the projectile spawner to the object spawning it this time.
            projectile.AddObservers(); // Add any observers from the projectile spawner to the projectile

            projectile.CopyEventsFromSpawner();

            // Run all spawn modifications before any behavior logic
            DoSpawnModifications(projectileObject, projectile);

            // If we came from the pool, trigger this event
            if (objectCameFromPool)
                projectile.DoGetFromPool();

            projectile.Launch(SpawnTransform, Target); // LAUNCH!
        }

        protected virtual GameObject InstantiateNewProjectile(GameObject projectilePrefab, Vector3 launchPosition,
            Quaternion launchRotation)
        {
            return Instantiate(projectilePrefab, launchPosition, launchRotation);
        }

        private void DoSpawnModifications(GameObject projectileObject, Projectile basicProjectile)
        {
            foreach (var modification in basicProjectile.spawnBehaviorModifications)
            {
                // create new instance of modification
                var spawnModification = Instantiate(modification);
                spawnModification.OnSpawn(projectileObject, basicProjectile);
            }
        }

        /*
         // March 3 2024 -- I think this is no longer used. I'm going to comment it out for now.
        protected virtual void LaunchModification(Projectile projectile)
        {
            // No action -- available for override
        }
        */


        // This will cache the angles of the object doing the spawning, both the rotating portion of the object and
        // the tilting -- they may be the same, or different.
        private void CacheEulerAngles()
        {
            _rotatingTransformEulerAngles = Spawner.RotatingTransform.eulerAngles;
            _tiltingTransformEulerAngles = Spawner.TiltingTransform.eulerAngles;
        }

        public virtual void Stop(bool forceStop = true)
        {
            if (_launchCoroutine == null) return;
            if (!canStopMidSpawning && !forceStop) return;
            CoroutineManager.Instance.StopCoroutine(_launchCoroutine);
            _launchCoroutine = null;
            _isSpawning = false;
        }

        // This method is used to determine the position of the projectile based on the angle of fire. It's used by the
        // StartPosition method to determine where to spawn each projectile.
        public virtual Vector3 LaunchPosition(float angleX, float angleY)
        {
            if (forwardOffsetFromSpawnPosition == 0 && positionOffsetFromSpawnPosition == Vector3.zero)
                return SpawnTransform.position;

            var offset = positionOffsetFromSpawnPosition
                         + Quaternion.Euler(angleY, angleX, 0f)
                         * (Vector3.forward * forwardOffsetFromSpawnPosition);
            return SpawnTransform.position + offset;
        }
    }
}
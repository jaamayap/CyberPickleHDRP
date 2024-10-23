using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/*
 * ProjectileSpawner is a thing that produces projectiles. Could be a "gun" or a "caster" or a "canon" or anything else
 * that will spawn out projectiles. Which projectile is spawned is up to you!
 */

namespace MagicPigGames.ProjectileFactory
{
    public enum SpawnerLifecycleEvent
    {
        Start,
        StartProjectile,
        StopProjectile
    }

    [Serializable]
    public class ProjectileSpawner : MonoBehaviour
    {
        public delegate void ProjectileHandler(GameObject projectileObject, SpawnBehavior spawnBehaviorInstance,
            Transform spawnTransform);

        public delegate void ProjectileStoppedEventHandler();

        [Header("Required")]
        [Tooltip("This is the layer mask that the projectiles will collide with. Individual Projectiles may override " +
                 "this value.")]
        [ShowInProjectileEditor("Collision Mask")]
        public LayerMask collisionMask;

        [Header("Target")]
        [Tooltip("This is the target that the projectiles will aim at. This can be set at runtime.")]
        [ShowInProjectileEditor("Target")]
        [SerializeField]
        private GameObject target;
        [Tooltip("When true, the spawn position will look at the target, if there is one.")]
        [ShowInProjectileEditor("Spawn Position Look At Target")]
        public bool spawnPositionLookAtTarget = false;

        [Header("Setup")]
        [Tooltip(
            "These are the projectiles that will be spawned. You can have as many as you like, and switch between them " +
            "at runtime.")]
        [ShowInProjectileEditor("Projectiles")]
        public List<GameObject> projectiles = new();

        [Tooltip("These observers will be added to all spawned projectiles, and allow for custom behaviors during " +
                 "the projectile lifecycle.")]
        [ShowInProjectileEditor("Observers")]
        public List<ProjectileObserver> observers = new();
        
        [Tooltip("Optionally set the layer of the projectile when it is launched, useful if you want to separate " +
                 "projectiles from \"Player\" and \"Enemy\" layers, as an example.")]
        [ShowInProjectileEditor("Set Layer of Projectile")]
        public bool setLayerOfProjectile = false;
        [Tooltip("If you are setting the layer of the projectile, this is the layer that will be set.")]
        [ShowInProjectileEditor("Projectile Layer")]
        [Layer]
        public int projectileLayer;

        // SPAWN POINTS
        /*
         * Spawn Points are the points where the projectiles spawn. Perhaps you only have one, or you have a ton all
         * over a spaceship. You can have as many as you'd like, and use a SpawnPointManager to cycle through them with
         * logic specific to your game.
         */
        [Tooltip("If you have multiple spawn points, you can use this to cycle through them. If you don't " +
                 "have multiple spawn points, then this can be left empty. If you do have multiple spawn points, " +
                 "then you should assign them here.")]
        [ShowInProjectileEditor("Spawn Points")]
        public List<SpawnPoint> spawnPoints = new();

        [Tooltip("This is the manager that will handle the logic for cycling through the spawn points.")]
        [ShowInProjectileEditor("Spawn Point Manager")]
        public SpawnPointManager spawnPointManager;


        [Header("Trajectory (Optional)")]
        [Tooltip("This is the default trajectory for any projectile. Each projectile may override this with its own " +
                 "trajectory behavior. If the projectile does not have a trajectory behavior, then this will be used.")]
        [ShowInProjectileEditor("Default Trajectory Behavior")]
        public TrajectoryBehavior defaultTrajectoryBehavior;

        [Tooltip("When true, the trajectory can show even when no projectile is ready to be used.")]
        [ShowInProjectileEditor("Show Trajectory Without Projectile")]
        public bool showTrajectoryWithoutProjectile;

        [Tooltip("When true, the trajectory will always show.")] [ShowInProjectileEditor("Always Show Trajectory")]
        public bool alwaysShowTrajectory;

        [Tooltip(
            "Shows the trajectory when true, and hides it when false. If alwaysShowTrajectory is true, then this " +
            "value is ignored. Set this from another script to control the trajectory visibility.")]
        [ShowInProjectileEditor("Show Trajectory")]
        public bool showTrajectory;

        [Header("Unity Events")] public UnityEvent OnSpawnProjectile;

        public UnityEvent OnStopProjectile;
        public UnityEvent OnShowTrajectoryStart;
        public UnityEvent OnShowTrajectoryStop;

        // These are the holders for the Unity Events which will be passed onto each projectile
        // as they are spawned.

        public Projectile.ProjectileEvent ProjectileOnLaunch;
        public Projectile.ProjectileEvent ProjectileOnProjectileStopped;
        public Projectile.ProjectileTriggerEvent ProjectileOnTriggerEnter;
        public Projectile.ProjectileTriggerEvent ProjectileOnTriggerExit;
        public Projectile.ProjectileTriggerEvent ProjectileOnTriggerStay;
        public Projectile.ProjectileEvent ProjectileOnReturnToPool;
        public Projectile.ProjectileEvent ProjectileOnGetFromPool;
        public Projectile.ProjectileCollisionEvent ProjectileOnCollisionEnter;
        public Projectile.ProjectileCollisionEvent ProjectileOnCollisionExit;
        public Projectile.ProjectileCollisionEvent ProjectileOnCollisionStay;
        public Projectile.ProjectileEvent ProjectileOnDoDestroy;
        public Projectile.ProjectileEvent ProjectileOnDoDisable;
        public Projectile.ProjectileEvent ProjectileOnDoEnable;
        public Projectile.ProjectileEvent ProjectileOnReset;
        public Projectile.ProjectileSpawnerEvent ProjectileOnProjectileSpawnerSet;
        public OnNewProjectileSelectedEvent OnNewProjectileSelected;
        public OnNewSpawnPointSelectedEvent OnNewSpawnPointSelected;

        [Header("Debugging")]
        [Tooltip("When true, debug messages will be sent to the console.")]
        [ShowInProjectileEditor("Log To Console")]
        public bool logToConsole;

        // The Projectile is set automatically when Projectile is called, if it is not yet cached.
        private Projectile _projectile;
        private int _projectileIndex;

        private bool _showingTrajectory;

        // This is the runtime instance of the SpawnBehavior, which is the behavior that will be used to launch
        // the projectile.
        private SpawnBehavior _spawnBehaviorInstance;
        protected SpawnPoint lastSpawnPoint;
        public LayerMask CollisionMask => collisionMask;

        // These properties are specific to the active target.
        public GameObject Target => target;
        public Vector3 TargetPosition => target.transform.position;
        public Vector3 TargetForward => target.transform.forward;
        public Vector3 TargetForwardPosition => target.transform.position + target.transform.forward;
        public float DistanceToTarget => Vector3.Distance(SpawnPosition, TargetPosition);
        public int SpawnPointIndex { get; set; }

        public virtual SpawnPoint SpawnPoint => spawnPoints[SpawnPointIndex];
        public virtual SpawnPoint LastSpawnPoint => lastSpawnPoint ?? SpawnPoint;
        public virtual Transform SpawnTransform => SpawnPoint.transform;
        public virtual Vector3 SpawnPosition => SpawnPoint.Position;
        public virtual Vector3 SpawnRotation => SpawnPoint.Rotation;
        public virtual Vector3 SpawnForward => SpawnTransform.forward;
        public virtual Vector3 SpawnForwardPosition => SpawnPoint.Position + SpawnForward;

        public virtual Transform LastSpawnTransform => LastSpawnPoint.transform;
        public virtual Vector3 LastSpawnPosition => LastSpawnPoint.Position;
        public virtual Vector3 LastSpawnRotation => LastSpawnPoint.Rotation;
        public virtual Vector3 LastSpawnForward => LastSpawnTransform.forward;
        public virtual Vector3 LastSpawnForwardPosition => LastSpawnPosition + LastSpawnForward;

        public virtual bool IsSpawning { get; private set; }

        public Rigidbody Rigidbody { get; private set; }

        public virtual Transform RotatingTransform =>
            SpawnPoint.rotatingTransform ? SpawnPoint.rotatingTransform : transform;

        public virtual Transform TiltingTransform =>
            SpawnPoint.tiltingTransform ? SpawnPoint.tiltingTransform : transform;

        // This is the projectile that is on deck to be spawned. It is not the projectile that is currently in the scene.
        public virtual GameObject ProjectileObject
        {
            get
            {
                if (projectiles.Count == 0) return default; // Return default if we have none
                if (_projectileIndex >= projectiles.Count) return projectiles[^1]; // Never return out of range
                return projectiles[_projectileIndex]; // This is the projectile that will be spawned
            }
        }

        public virtual Projectile Projectile
        {
            get
            {
                if (ProjectileObject is null) return default; // If we have no projectile object, return default
                return _projectile ??= ProjectileObject.GetComponent<Projectile>(); // Get the projectile component
            }
        }

        public GameObject LastProjectileObject { get; private set; }

        protected virtual void Start()
        {
            OnShowTrajectoryStart ??= new UnityEvent();
            OnShowTrajectoryStop ??= new UnityEvent();
            OnNewProjectileSelected ??= new OnNewProjectileSelectedEvent();
            OnNewSpawnPointSelected ??= new OnNewSpawnPointSelectedEvent();
            OnSpawnProjectile ??= new UnityEvent();
            OnStopProjectile ??= new UnityEvent();

            Rigidbody = GetComponent<Rigidbody>();
        }

        public virtual void Update()
        {
            CheckTrajectoryStatus();
            ProjectileSpawnerTicks();
        }

        protected virtual void OnEnable()
        {
            // March 31, 2024 -- We dont' need to do this here -- the global observers should communicate
            // only with the FactoryManager. We will still register the spawner with the FactoryManager.
            //this.AttachGlobalObservers(); // Add the global observers to the ProjectileSpawner; Uses the ProjectileFactoryManager
            FactoryManager.RegisterProjectileSpawner(this);
        }

        protected virtual void OnDisable()
        {
            //this.DetachGlobalObservers(); // Remove all global observers from the ProjectileSpawner; Uses the ProjectileFactoryManager
            FactoryManager.UnregisterProjectileSpawner(this);
            ClearTrajectory();
            StopProjectile(true);
            ClearSpawnBehavior();
        }

        public void AddToCollisionMask(LayerMask mask)
        {
            collisionMask |= mask;
        }

        public void RemoveFromCollisionMask(LayerMask mask)
        {
            collisionMask &= ~mask;
        }

        public bool SetTarget(GameObject value)
        {
            return target = value;
        }

        public virtual Vector3 SpawnForwardDistance(float distance)
        {
            return SpawnPoint.Position + SpawnForward * distance;
        }

        /// <summary>
        ///     Shows the trajectory if it is allowed to be shown, and one is available.
        /// </summary>
        /// <returns></returns>
        public bool ShowTrajectory()
        {
            // We must be opting to show trajectory.
            if (!showTrajectory && !alwaysShowTrajectory)
                return false;

            // Don't show trajectory when no projectile is on deck, unless we are specifically allowing it.
            if (Projectile == null && !showTrajectoryWithoutProjectile)
                return false;

            // Figure out which trajectory we will be using
            var trajectoryBehavior = TrajectoryToUse();

            // If we have no behavior, then we can't show the trajectory.
            if (trajectoryBehavior == null)
                return false;

            // If the trajectory is not allowed to be used pre-launch, then don't show it.
            //if (!trajectoryBehavior.displayPreLaunch)
            //    return false;

            // We can show the trajectory!
            return true;
        }

        protected virtual TrajectoryBehavior TrajectoryToUse()
        {
            return Projectile.preLaunchTrajectory == null
                ? defaultTrajectoryBehavior
                : Projectile.preLaunchTrajectory;
        }

        protected virtual void ProjectileSpawnerTicks()
        {
            if (Projectile is null)
                return;

            if (ShowTrajectory())
                // We call the version of Tick() that requires the ProjectileSpawner, as in this case the Projectile itself is
                // not live in the scene. The "Projectile" for the ProjectileSpawner is the "on deck" projectile which will be 
                // spawned. Once a Projectile is spawned, there will be an Instance of the TrajectoryBehavior on the
                // Projectile, which will be used for any "live" changes in the trajectory view.
                Projectile.preLaunchTrajectoryInstance.Tick();
        }

        // Toggles the start and stop showing of the trajectory based on the two values which may have changed.
        protected virtual void CheckTrajectoryStatus()
        {
            if (ShowTrajectory() && !_showingTrajectory)
                ShowTrajectoryStart();
            else if (!ShowTrajectory() && _showingTrajectory)
                ShowTrajectoryStop();
        }

        public virtual void ShowTrajectoryStart()
        {
            if (!ShowTrajectory()) return;
            if (!Projectile) return; // If there is no ProjectileBase assigned, then return

            DebugMessage("Show Trajectory Start");
            _showingTrajectory = true;

            // Determine which trajectory to use -- the projectile can have its own, or use the default.
            var trajectoryBehavior = TrajectoryToUse();
            if (trajectoryBehavior == null)
            {
                DebugMessage("Both the default and the projectile trajectory are null. Cannot show trajectory.");
                return;
            }

            OnShowTrajectoryStart.Invoke();
            Projectile.preLaunchTrajectoryInstance = Instantiate(trajectoryBehavior);
            Projectile.preLaunchTrajectoryInstance.SetSpawnerAndProjectile(this, Projectile); // Added this June 5 2024
            Projectile.preLaunchTrajectoryInstance.ShowTrajectoryStart();
        }

        public virtual void ShowTrajectoryStop()
        {
            if (ShowTrajectory()) return;
            if (!Projectile) return; // If there is no ProjectileBase assigned, then return
            if (!Projectile.HasPreLaunchTrajectoryBehavior)
                return; // If the ProjectileBase does not have a TrajectoryBehavior, then return

            DebugMessage("Show Trajectory Stop");
            OnShowTrajectoryStop.Invoke();
            _showingTrajectory = false;
            SignalTrajectoryToStop();
            Projectile.preLaunchTrajectoryInstance = null;
        }

        protected virtual void SignalTrajectoryToStop()
        {
            if (Projectile.preLaunchTrajectoryInstance == null)
                return;

            Projectile.preLaunchTrajectoryInstance.ShowTrajectoryStop();
        }

        /// <summary>
        ///     Call this to clear the trajectory of a projectile. Useful for switching projectiles, for clearing when
        ///     the projectile is fired, or in other situations, such as when the user controls whether the trajectory is
        ///     visible or not.
        /// </summary>
        /// <param name="trajectoryProjectileIndex"></param>
        public virtual void ClearTrajectory(int trajectoryProjectileIndex = -1)
        {
            if (trajectoryProjectileIndex == -1)
                trajectoryProjectileIndex = _projectileIndex;
            if (trajectoryProjectileIndex < 0 || trajectoryProjectileIndex >= projectiles.Count)
                return;

            var lastProjectile = projectiles[trajectoryProjectileIndex].GetComponent<Projectile>();
            if (lastProjectile is null) return;
            if (!lastProjectile.HasPreLaunchTrajectoryBehavior) return;
            if (lastProjectile.preLaunchTrajectoryInstance == null) return;

            lastProjectile.preLaunchTrajectoryInstance.ShowTrajectoryStop();
            lastProjectile.preLaunchTrajectoryInstance = null;
        }

        public event ProjectileStoppedEventHandler OnProjectileStopped;

        public virtual void StopProjectile(bool forceStop = false)
        {
            if (_spawnBehaviorInstance != null)
                _spawnBehaviorInstance.Stop(forceStop);

            IsSpawning = false;

            OnProjectileStopped?.Invoke();
            OnStopProjectile?.Invoke();
            ClearSpawnBehavior();
        }

        public event ProjectileHandler OnProjectileLaunched;

        /// <summary>
        /// This will spawn the projectile, using the current projectile object. If you want to spawn a different
        /// projectile, you can pass it in as a parameter -- if it is not yet in the Projectiles list, it will be added.
        /// </summary>
        /// <param name="setThisProjectile"></param>
        public virtual void SpawnProjectile(GameObject setThisProjectile = null)
        {
            if (ProjectileObject == null && setThisProjectile == null)
                return;

            if (setThisProjectile != null)
                SetProjectile(setThisProjectile);

            LastProjectileObject = ProjectileObject;

            CreateSpawnBehaviorInstance();
            
            IsSpawning = true;
            
            _spawnBehaviorInstance.Launch(this);
            OnProjectileLaunched?.Invoke(ProjectileObject, _spawnBehaviorInstance, SpawnTransform);
            OnSpawnProjectile?.Invoke();
        }

        /// <summary>
        ///     Moves to the next spawn point, utilizing the SpawnPointManager logic. Include a negative value to
        ///     move to a previous spawn point.
        /// </summary>
        /// <param name="indexStep"></param>
        public virtual void NextSpawnPoint(int indexStep = 1)
        {
            if (spawnPointManager == null) return;
            if (spawnPoints.Count == 0) return;

            DebugMessage($"Next Spawn Point: Step value is {indexStep}");

            // Move to the next spawn point using the logic in the Spawn Point manager
            //spawnTransform = spawnPointManager.NextSpawnPoint();
            lastSpawnPoint = SpawnPoint;
            spawnPointManager.NextSpawnPointIndex(this, indexStep);
            var newSpawnPoint = SpawnPoint;
            OnNewSpawnPointSelected?.Invoke(newSpawnPoint, lastSpawnPoint);
        }

        /// <summary>
        ///     Sets the spawn point to the specified index.
        /// </summary>
        /// <param name="index"></param>
        public virtual void SetSpawnPoint(int index)
        {
            if (index < 0 || index >= spawnPoints.Count)
            {
                Debug.LogWarning($"Index out of range. There are {spawnPoints.Count} spawn points. Index was {index}.");
                return;
            }

            var oldSpawnPoint = SpawnPoint;
            SpawnPointIndex = index;
            var newSpawnPoint = SpawnPoint;
            OnNewSpawnPointSelected?.Invoke(newSpawnPoint, oldSpawnPoint);
        }

        /// <summary>
        ///     Sets the spawn point to the specified SpawnPoint.
        /// </summary>
        /// <param name="spawnPoint"></param>
        public virtual void SetSpawnPoint(SpawnPoint spawnPoint)
        {
            if (!spawnPoints.Contains(spawnPoint))
            {
                Debug.LogWarning("Spawn Point not found in the list of spawn points.");
                return;
            }

            var oldSpawnPoint = SpawnPoint;
            SpawnPointIndex = spawnPoints.IndexOf(spawnPoint);
            var newSpawnPoint = SpawnPoint;
            OnNewSpawnPointSelected?.Invoke(newSpawnPoint, oldSpawnPoint);
        }

        /// <summary>
        ///     Removes the specified spawn point from the list of spawn points.
        /// </summary>
        /// <param name="spawnPoint"></param>
        public virtual void RemoveSpawnPoint(SpawnPoint spawnPoint)
        {
            if (!spawnPoints.Contains(spawnPoint))
            {
                Debug.LogWarning("Spawn Point not found in the list of spawn points.");
                return;
            }

            spawnPoints.Remove(spawnPoint);
        }

        public virtual void RemoveSpawnPoint(int index)
        {
            if (index < 0 || index >= spawnPoints.Count)
            {
                Debug.LogWarning($"Index out of range. There are {spawnPoints.Count} spawn points. Index was {index}.");
                return;
            }

            spawnPoints.RemoveAt(index);
        }

        // This creates the SpawnBehavior instance, which is the behavior that will be used to launch the projectile.
        // Keep in mind we create instances of the scriptable objects to avoid overwriting the data on the objects.
        protected virtual void CreateSpawnBehaviorInstance()
        {
            if (_spawnBehaviorInstance != null)
            {
                DebugMessage("Instance already exists. Cannot create another.");
                return;
            }

            _spawnBehaviorInstance = Instantiate(Projectile.SpawnBehavior);
        }

        /// <summary>
        ///     Moves to the next projectile.
        /// </summary>
        /// <param name="indexStep">The step of the index to advance. Defaults to 1.</param>
        public virtual void NextProjectile(int indexStep = 1)
        {
            SignalTrajectoryToStop();

            _projectileIndex += indexStep;
            if (_projectileIndex < 0)
                _projectileIndex += projectiles.Count;
            _projectileIndex %= projectiles.Count;

            SetProjectile(_projectileIndex);
        }

        /// <summary>
        ///     Adds a projectile to the GameObject.
        /// </summary>
        /// <param name="projectile"></param>
        public virtual void AddProjectile(GameObject projectile)
        {
            if (projectile == null)
            {
                Debug.LogWarning("Cannot add a null projectile.");
                return;
            }

            projectiles.Add(projectile);
        }

        /// <summary>
        ///     Removes a projectile from the list. Can not remove the active projectile or the last projectile.
        /// </summary>
        /// <param name="projectile"></param>
        public virtual void RemoveProjectile(GameObject projectile)
        {
            if (projectile == null)
            {
                Debug.LogWarning("Cannot remove a null projectile.");
                return;
            }

            if (projectile == ProjectileObject)
            {
                Debug.LogError("Cannot remove the current projectile. Please switch to another projectile first.");
                return;
            }

            if (projectiles.Count == 1)
            {
                Debug.LogError("Cannot remove the last projectile. Please add another projectile first.");
                return;
            }

            var projectileIndex = projectiles.IndexOf(projectile);

            // Set the index to the previous projectile, unless it is the first projectile, in which case we will 
            // leave it at 0, i.e. the next projectile.
            if (projectileIndex > 0)
                SetProjectile(projectileIndex - 1);
            projectiles.Remove(projectile);
        }

        /// <summary>
        ///     Switches the projectile at the specified index with the new projectile (not from the list).
        /// </summary>
        /// <param name="newProjectile"></param>
        /// <param name="oldProjectile"></param>
        public virtual void SwitchProjectile(GameObject newProjectile, GameObject oldProjectile)
        {
            var index = projectiles.IndexOf(oldProjectile);
            if (index == -1)
            {
                Debug.LogWarning($"Old projectile {oldProjectile.name} not found in the list of projectiles.");
                return;
            }

            SwitchProjectile(newProjectile, index);
        }

        /// <summary>
        ///     Switches the projectile at the specified index with the new projectile (not from the list).
        /// </summary>
        /// <param name="newProjectile"></param>
        /// <param name="oldProjectileIndex"></param>
        public virtual void SwitchProjectile(GameObject newProjectile, int oldProjectileIndex)
        {
            // Cache if the old projectile is active
            var oldIsActive = oldProjectileIndex == _projectileIndex;

            // If it's active, stop it
            if (oldIsActive)
                StopProjectile(true);

            projectiles[oldProjectileIndex] = newProjectile; // Set the new projectile

            // If old was active, set up the new one as active
            if (oldIsActive)
                SetProjectile(oldProjectileIndex);
        }

        /// <summary>
        ///     Sets the projectile for the GameObject.
        /// </summary>
        /// <param name="projectile">The projectile GameObject to be set.</param>
        /// <param name="addIfNotInList">If true, the object will be added to the list if it isn't there already.</param>
        public virtual void SetProjectile(GameObject projectile, bool addIfNotInList = true)
        {
            if (projectile == null)
            {
                Debug.LogWarning("Cannot set a null projectile.");
                return;
            }
            
            for (var i = 0; i < projectiles.Count; i++)
            {
                if (projectiles[i] != projectile) continue;
                SetProjectile(i);
                return;
            }
            
            // Did not contain, so try to add the projectile object
            if (!addIfNotInList)
                return;
            
            projectiles.Add(projectile);
            SetProjectile(projectiles.Count - 1);
        }

        /// <summary>
        ///     Sets the current active projectile at the specified index.
        /// </summary>
        /// <param name="index">The index of the projectile.</param>
        public virtual void SetProjectile(int index)
        {
            // If the index is out of range, return
            var oldProjectile = Projectile; // Cache the old for the event
            if (index < 0 || index >= projectiles.Count)
                return;
            _projectileIndex = index;
            var newProjectile = Projectile; // Cache the new for the event
            NewProjectileLogistics(newProjectile, oldProjectile);
        }
        
        /// <summary>
        /// Toggle the SetLayerOfProjectile value.
        /// </summary>
        public virtual void ToggleSetLayerOfProjectile() => SetSetLayerOfProjectile(!setLayerOfProjectile);

        /// <summary>
        /// Set the SetLayerOfProjectile value.
        /// </summary>
        /// <param name="value"></param>
        public virtual void SetSetLayerOfProjectile(bool value) => setLayerOfProjectile = value;
        
        /// <summary>
        /// Set the layer to use when the projectile is launched, if setLayerOfProjectile is true.
        /// </summary>
        /// <param name="layer"></param>
        public virtual void SetLayerOfProjectile(int layer) => projectileLayer = layer;

        protected virtual void NewProjectileLogistics(Projectile newBasicProjectile, Projectile oldBasicProjectile)
        {
            DebugMessage("Setting a new Projectile");

            ClearTrajectory(_projectileIndex); // Clear the trajectory for the last selected Projectile
            StopProjectile(true);
            ClearSpawnBehavior();

            _projectile = null; // Clear this to have it recache next time Projectile is called
            OnNewProjectileSelected?.Invoke(newBasicProjectile, oldBasicProjectile);
            ShowTrajectoryStart(); // Start the trajectory for the newly selected Projectile
        }

        public virtual void ToggleShowTrajectoryWithoutProjectile()
        {
            showTrajectoryWithoutProjectile = !showTrajectoryWithoutProjectile;
        }

        public virtual void ToggleAlwaysShowTrajectory()
        {
            alwaysShowTrajectory = !alwaysShowTrajectory;
        }

        public virtual void ToggleShowTrajectory()
        {
            showTrajectory = !showTrajectory;
        }

        public virtual void SetAlwaysShowTrajectory(bool value)
        {
            alwaysShowTrajectory = value;
        }

        public virtual void SetShowTrajectory(bool value)
        {
            showTrajectory = value;
        }

        protected virtual void ClearSpawnBehavior()
        {
            _spawnBehaviorInstance = null;
        }

        public virtual void DebugMessage(string message)
        {
            if (!logToConsole)
                return;
            Debug.Log($"{gameObject.name} <color=cyan>Spawner</color>: {message}");
        }

        [Serializable]
        public class OnNewProjectileSelectedEvent : UnityEvent<Projectile, Projectile>
        {
        }

        [Serializable]
        public class OnNewSpawnPointSelectedEvent : UnityEvent<SpawnPoint, SpawnPoint>
        {
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Random = UnityEngine.Random;

namespace MagicPigGames.ProjectileFactory
{
    public enum LifecycleEvent
    {
        Start,
        LaunchProjectile,
        CollisionEnter,
        CollisionExit,
        CollisionStay,
        TriggerEnter,
        TriggerExit,
        TriggerStay,
        DoDestroy,
        Disable,
        Enable,
        OnReturnToPool,
        OnGetFromPool,
        ProjectileStopped // This is technically from the spawner, but we can use it here.
    }

    [Serializable]
    public class Projectile : MonoBehaviour, ICanLaunch
    {
        public delegate void ProjectileCollisionHandler(Projectile projectile, Collision collision,
            GameObject objectHit = null, Vector3 contactPoint = default);

        public delegate void ProjectileHandler(Projectile projectile);

        public delegate void ProjectileTriggerHandler(Projectile projectile, Collider collider,
            GameObject objectHit = null, Vector3 contactPoint = default);

        // OCTOBER 13, 2024: Made projectileData private, so you'll need to use the new Property instead. (ProjectileData)
        [Header("DataObject")]
        [SerializeField] private ProjectileData projectileData; // ScriptableObject containing common projectile data
        private ProjectileData _projectileDataInstance;
        public ProjectileData ProjectileData
        {
            get {
                if (_projectileDataInstance == null)
                    _projectileDataInstance = Instantiate(projectileData);
                return _projectileDataInstance;
            }
        }
        
        [Header("Spawn Behavior")]
        [Tooltip("This is the behavior that will be used to spawn the projectile(s).")]
        [SerializeField] private SpawnBehavior spawnBehavior;
        private SpawnBehavior _spawnBehaviorInstance;
        public SpawnBehavior SpawnBehavior
        {
            get {
                if (_spawnBehaviorInstance == null)
                    _spawnBehaviorInstance = Instantiate(spawnBehavior);
                return _spawnBehaviorInstance;
            }
        }

        [Tooltip("These modifications will be applied to the projectile after it is Instantiated, but prior to " +
                 "the OnLaunch() method being called on other behaviors.")]
        public List<SpawnBehaviorModification> spawnBehaviorModifications = new();

        [Header("Particle Behaviors")] public List<ProjectileBehavior> behaviors = new();

        [Header("Observers")] [SerializeField] private List<ProjectileObserver> observers;

        [Tooltip(
            "Projectile Factory Manager enables other objects in the scene to see all projectiles and spawners, unless " +
            "you choose to not register this projectile with the Projectile Factory Manager.")]
        public bool registerWithProjectileFactoryManager = true;

        [Header("Object Pooling")] public bool useObjectPool = true;

        public int poolUniqueID = -1;

        [HideInInspector] public List<SpawnBehaviorModification> spawnBehaviorModificationsInstances = new();

        public ProjectileEvent OnLaunch;
        public ProjectileEvent OnProjectileStopped;
        public ProjectileTriggerEvent OnDoTriggerEnter;
        public ProjectileTriggerEvent OnDoTriggerExit;
        public ProjectileTriggerEvent OnDoTriggerStay;
        public ProjectileEvent OnReturnToPool;
        public ProjectileEvent OnGetFromPool;
        public ProjectileCollisionEvent OnDoCollisionEnter;
        public ProjectileCollisionEvent OnDoCollisionExit;
        public ProjectileCollisionEvent OnDoCollisionStay;
        public ProjectileEvent OnDoDestroy;
        public ProjectileEvent OnDoDisable;
        public ProjectileEvent OnDoEnable;
        public ProjectileEvent OnReset;
        public ProjectileSpawnerEvent OnProjectileSpawnerSet;

        [Header("Options")]
        [Tooltip("This is the maximum number of collisions that can occur in a single frame. A value of 1 means the " +
                 "projectile can only register a single hit. However, you can still \"Collide\" with multiple other " +
                 "objects using a behavior to cause damage etc. -1 means no limit.")]
        public int maxCollisionsPerFrame = 1;

        [Tooltip("When true, the projectile will rotate to look at the spawner forward direction. Warning: This may " +
                 "cause unexpected behavior for line renderers and other types.")]
        public bool lookForwardAtSpawn;

        [Tooltip("This LayerMask determines which layers the projectile can collide with.")]
        public LayerMask collisionMask; // LayerMask to determine what the projectile can collide with

        [Tooltip("If true, the Projectile Spawner CollisionMask will be replaced by this collision mask.")]
        public bool overrideCollisionMask;

        [Tooltip("This is an optional trajectory that can be displayed before the projectile is launched.")]
        public TrajectoryBehavior preLaunchTrajectory;

        [HideInInspector] public TrajectoryBehavior preLaunchTrajectoryInstance;

        [HideInInspector] public bool useAltTargetPosition;
        [HideInInspector] public bool hideTarget;
        [HideInInspector] public Vector3 altTargetPosition;
        private Dictionary<ProjectileBehavior, ProjectileBehavior> _behaviorInstances = new(); // runtime instances

        private int _collisionCountThisFrame;

        // We need to keep track of whether the projectile is in the pool or not, because it is possible that the 
        // collision events are triggered multiple times per frame, due to small colliders being hit all at once.

        private GameObject _target;

        public List<ProjectileBehavior> Behaviors => behaviors;
        public int CountBehaviors => behaviors.Count;
        public int CountObservers => observers.Count;
        public List<ProjectileObserver> Observers => observers;
        public Rigidbody Rigidbody { get; private set; }

        public bool IsInPool { get; private set; }

        public bool Launched { get; private set; }

        public LayerMask CollisionMask =>
            ProjectileSpawner == null || overrideCollisionMask
                ? collisionMask
                : ProjectileSpawner.CollisionMask;

        public ProjectileSpawner ProjectileSpawner { get; private set; }

        public GameObject Target => GetTarget();
        public Transform TargetTransform => Target.transform;
        public GameObject Particle { get; }

        public Vector3 LastPosition { get; private set; }
        public Vector3 TargetPosition => useAltTargetPosition ? altTargetPosition : Target.transform.position;

        public virtual bool HasPreLaunchTrajectoryBehavior => preLaunchTrajectory != null;

        public GameObject ParentPrefab { get; set; }

        protected virtual void Start()
        {
            OnLaunch = new ProjectileEvent();
            OnProjectileStopped = new ProjectileEvent();
            OnDoTriggerEnter = new ProjectileTriggerEvent();
            OnDoTriggerExit = new ProjectileTriggerEvent();
            OnDoTriggerStay = new ProjectileTriggerEvent();
            OnReturnToPool = new ProjectileEvent();
            OnGetFromPool = new ProjectileEvent();
            OnDoCollisionEnter = new ProjectileCollisionEvent();
            OnDoCollisionExit = new ProjectileCollisionEvent();
            OnDoCollisionStay = new ProjectileCollisionEvent();
            OnDoDestroy = new ProjectileEvent();
            OnDoDisable = new ProjectileEvent();
            OnDoEnable = new ProjectileEvent();
            OnReset = new ProjectileEvent();
            OnProjectileSpawnerSet = new ProjectileSpawnerEvent();

            Rigidbody = GetComponentInChildren<Rigidbody>();
        }

        protected void Update()
        {
            foreach (var behavior in behaviors)
            {
                if (behavior == null)
                    continue;
                GetBehavior(behavior).Tick();
            }
        }

        protected void LateUpdate()
        {
            foreach (var behavior in behaviors)
            {
                if (behavior == null)
                    continue;
                GetBehavior(behavior).LateTick();
            }

            EndOfFrameReset();
            LastPosition = transform.position;
        }

        protected virtual void OnEnable()
        {
            DoEnable();
        }

        protected virtual void OnDisable()
        {
            DoDisable();
        }

        public void OnCollisionEnter(Collision collision)
        {
            DoCollisionEnter(collision);
        }

        public void OnCollisionExit(Collision collision)
        {
            DoCollisionExit(collision);
        }

        public void OnCollisionStay(Collision collision)
        {
            DoCollisionStay(collision);
        }

        public void OnTriggerEnter(Collider other)
        {
            DoTriggerEnter(other);
        }

        public void OnTriggerExit(Collider other)
        {
            DoTriggerExit(other);
        }

        public void OnTriggerStay(Collider other)
        {
            DoTriggerStay(other);
        }

        public virtual void SetTarget(GameObject value)
        {
            _target = value;
        }

        public TrajectoryBehavior PreLaunchTrajectory()
        {
            return preLaunchTrajectory;
        }

        public void SetProjectileSpawner(ProjectileSpawner value)
        {
            ProjectileSpawner = value;
            SubscribeToEvents();
            OnProjectileSpawnerSet.Invoke(value, this);
        }

        public virtual void Launch(Transform spawnTransform = null, GameObject targetObject = null)
        {
            if (targetObject != null)
                SetTarget(targetObject);

            // Initialize the behaviors -- These are initialized via the GetBehavior(behavior) method!!
            foreach (var behavior in behaviors)
            {
                if (behavior == null)
                    continue;
                if (GetBehavior(behavior) != null)
                {
                }
            }

            Launched = true;
            LaunchProjectile?.Invoke(this);
            OnLaunch.Invoke(this);
        }

        public void SetInPool(bool value)
        {
            IsInPool = value;
        }

        /// <summary>
        ///     Returns true if the projectile has a behavior of the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool HasBehaviorOfType<T>() where T : ProjectileBehavior
        {
            return behaviors.OfType<T>().Any();
        }

        public event ProjectileHandler LaunchProjectile;
        public event ProjectileTriggerHandler TriggerEnter;
        public event ProjectileTriggerHandler TriggerExit;
        public event ProjectileTriggerHandler TriggerStay;
        public event ProjectileHandler ReturnToPool;
        public event ProjectileHandler GetFromPool;
        public event ProjectileCollisionHandler CollisionEnter;
        public event ProjectileCollisionHandler CollisionExit;
        public event ProjectileCollisionHandler CollisionStay;
        public event ProjectileHandler DoDestroy;
        public event ProjectileHandler Disable;
        public event ProjectileHandler Enable;

        protected T GetBehavior<T>(T behavior, ref T cachedValue) where T : ProjectileBehavior
        {
            if (cachedValue != null)
                return cachedValue;

            if (behavior == null)
                return default;

            cachedValue = Instantiate(behavior);
            cachedValue.SetSpawnerAndProjectile(ProjectileSpawner, this);
            cachedValue.SubscribeToEvents(this);
            return cachedValue;
        }

        protected T GetBehavior<T>(T behavior) where T : ProjectileBehavior
        {
            if (_behaviorInstances.TryGetValue(behavior, out var instance))
                return (T)instance;

            var newInstance = Instantiate(behavior);
            newInstance.SetSpawnerAndProjectile(ProjectileSpawner, this);
            newInstance.SubscribeToEvents(this);
            _behaviorInstances.Add(behavior, newInstance);
            return newInstance;
        }

        public virtual void ResetProjectile()
        {
            if (poolUniqueID < 0) poolUniqueID = Random.Range(1000, 99999);

            transform.parent = null;
            ClearBehaviorInstances(); // Remove all the instances of the behaviors
            UnsubscribeBehaviorsFromEvents(); // Unsubscribe from events
            ClearCachedValues(); // Reset the instances of the behaviors etc.

            if (Rigidbody != null && !Rigidbody.isKinematic)
            {
                Rigidbody.velocity = Vector3.zero;
                Rigidbody.angularVelocity = Vector3.zero;
            }

            OnReset.Invoke(this);
        }

        private void ClearBehaviorInstances()
        {
            foreach (var behavior in _behaviorInstances.Values)
            {
                if (behavior == null)
                    continue;
                Destroy(behavior);
            }

            _behaviorInstances.Clear();
        }

        public virtual void ClearCachedValues()
        {
            _behaviorInstances.Clear();
            spawnBehaviorModificationsInstances.Clear();
            observers.Clear();
        }

        public virtual void UnsubscribeBehaviorsFromEvents()
        {
            foreach (var behavior in behaviors)
            {
                if (behavior == null)
                    continue;
                behavior.UnsubscribeFromEvents(this);
            }

            foreach (var observer in observers)
            {
                if (observer == null)
                    continue;
                observer.UnsubscribeFromEvents(this);
            }
        }

        public void AddToCollisionMask(LayerMask mask)
        {
            collisionMask |= mask;
        }

        public void RemoveFromCollisionMask(LayerMask mask)
        {
            collisionMask &= ~mask;
        }

        protected virtual GameObject GetTarget()
        {
            return _target;
        }

        public virtual void AddObservers()
        {
            observers.Clear(); // Clear the list first
            foreach (var observer in ProjectileSpawner.observers)
                AddObserver(observer);
        }

        public virtual ProjectileObserver AddObserver(ProjectileObserver observer)
        {
            var newObserver = Instantiate(observer);
            newObserver.SetSpawnerAndProjectile(ProjectileSpawner, this);
            newObserver.SubscribeToEvents(this);
            observers.Add(newObserver);

            return observers[^1]; // Return the last observer added
        }

        public virtual void RemoveObserver(ProjectileObserver observer)
        {
            if (!observers.Contains(observer))
                return;

            observers.Remove(observer);
            Destroy(observer);
        }


        public virtual void DoReturnToPool()
        {
            ReturnToPool?.Invoke(this);
            OnReturnToPool.Invoke(this);
        }

        public virtual void DoGetFromPool()
        {
            GetFromPool?.Invoke(this);
            OnGetFromPool.Invoke(this);
        }

        protected virtual void EndOfFrameReset()
        {
            _collisionCountThisFrame = 0;
        }

        public virtual void TriggerCollisionWithObject(GameObject objectHit, Vector3 pointOfContact = default)
        {
            CollisionEnter?.Invoke(this, null, objectHit, pointOfContact);
            OnDoCollisionEnter.Invoke(this, null, objectHit, pointOfContact);
        }

        public void DoCollisionEnter(Collision collision)
        {
            // Return if collision is not in the LayerMask
            if ((CollisionMask.value & (1 << collision.gameObject.layer)) == 0) return;

            if (maxCollisionsPerFrame > 0 && _collisionCountThisFrame >= maxCollisionsPerFrame)
                return;

            _collisionCountThisFrame += 1;

            var pointOfContact = collision.contacts.Length > 0 ? collision.contacts[0].point : default;
            CollisionEnter?.Invoke(this, collision, collision.gameObject, pointOfContact);
            OnDoCollisionEnter.Invoke(this, collision, collision.gameObject, pointOfContact);
        }

        public void DoCollisionExit(Collision collision)
        {
            // Return if collision is not in the LayerMask
            if ((CollisionMask.value & (1 << collision.gameObject.layer)) == 0) return;

            var pointOfContact = collision.contacts.Length > 0 ? collision.contacts[0].point : default;
            CollisionExit?.Invoke(this, collision, collision.gameObject, pointOfContact);
            OnDoCollisionExit.Invoke(this, collision, collision.gameObject, pointOfContact);
        }

        public void DoCollisionStay(Collision collision)
        {
            // Return if collision is not in the LayerMask
            if ((CollisionMask.value & (1 << collision.gameObject.layer)) == 0) return;

            var pointOfContact = collision.contacts.Length > 0 ? collision.contacts[0].point : default;
            CollisionStay?.Invoke(this, collision, collision.gameObject, pointOfContact);
            OnDoCollisionStay.Invoke(this, collision, collision.gameObject, pointOfContact);
        }

        public void DoTriggerEnter(Collider other)
        {
            // Return if the collider is not in the layermask
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0) return;

            var pointOfContact = other.ClosestPointOnBounds(transform.position);
            TriggerEnter?.Invoke(this, other, other.gameObject, pointOfContact);
            OnDoTriggerEnter.Invoke(this, other, other.gameObject, pointOfContact);
        }

        public void DoTriggerExit(Collider other)
        {
            // Return if the collider is not in the layermask
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0) return;

            var pointOfContact = other.ClosestPointOnBounds(transform.position);
            TriggerExit?.Invoke(this, other, other.gameObject, pointOfContact);
            OnDoTriggerExit.Invoke(this, other, other.gameObject, pointOfContact);
        }

        public void DoTriggerStay(Collider other)
        {
            // Return if the collider is not in the layermask
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0) return;

            var pointOfContact = other.ClosestPointOnBounds(transform.position);
            TriggerStay?.Invoke(this, other, other.gameObject, pointOfContact);
            OnDoTriggerStay.Invoke(this, other, other.gameObject, pointOfContact);
        }

        /// <summary>
        ///     Destroy the projectile and run the OnDestroy() method.
        /// </summary>
        public virtual void TriggerDestroy()
        {
            InvokeDestroy();
        }

        protected virtual void InvokeDestroy()
        {
            DoDestroy?.Invoke(this);
            OnDoDestroy.Invoke(this);
        }

        public virtual void DoDisable()
        {
            UnregisterWithProjectileFactoryManager();
            UnsubscribeFromEvents();
            Disable?.Invoke(this);
            OnDoDisable.Invoke(this);
        }

        public virtual void DoEnable()
        {
            RegisterWithProjectileFactoryManager();
            Enable?.Invoke(this);
            OnDoEnable.Invoke(this);
        }

        private void RegisterWithProjectileFactoryManager()
        {
            if (!registerWithProjectileFactoryManager) return;
            FactoryManager.RegisterProjectile(this);
        }

        // We don't check registerWithProjectileFactoryManager, in case somehow that was turned off at runtime.
        private void UnregisterWithProjectileFactoryManager()
        {
            FactoryManager.UnregisterProjectile(this);
        }

        protected virtual void SubscribeToEvents()
        {
            if (ProjectileSpawner == null)
                return;

            ProjectileSpawner.OnProjectileStopped += ProjectileStopped;
        }

        protected virtual void UnsubscribeFromEvents()
        {
            if (ProjectileSpawner == null)
                return;

            ProjectileSpawner.OnProjectileStopped -= ProjectileStopped;
        }

        protected virtual void ProjectileStopped()
        {
            Launched = false;
            OnProjectileStopped.Invoke(this);
        }

        public virtual void AddSpawnBehaviorModification(SpawnBehaviorModification value)
        {
            if (value == null)
                return;

            spawnBehaviorModifications.Add(value);
        }

        /// <summary>
        /// Add a behavior to the projectile.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="initialize"></param>
        public virtual void AddBehavior(ProjectileBehavior value, bool initialize = true)
        {
            if (value == null)
                return;

            behaviors.Add(value);
            if (initialize)
                GetBehavior(value);
        }

        /// <summary>
        /// Remove the behavior from the projectile.
        /// </summary>
        /// <param name="value"></param>
        public virtual void RemoveBehavior(ProjectileBehavior value)
        {
            if (value == null)
                return;

            behaviors.Remove(value);

            // Handle instances of the behavior
            if (!_behaviorInstances.TryGetValue(value, out var instance))
                return;

            Destroy(instance);
            _behaviorInstances.Remove(value);
        }


        // This copies the Unity Events set in the Projectile Spawner inspector to 
        // the new Projectiles Unity Events. The events are called from the projectile, 
        // not the spawner, but must be set in the Inspector from the spawner to work 
        // at runtime with in-scene objects.
        public void CopyEventsFromSpawner()
        {
            if (ProjectileSpawner == null) return;

            OnLaunch = ProjectileSpawner.ProjectileOnLaunch;
            OnProjectileStopped = ProjectileSpawner.ProjectileOnProjectileStopped;
            OnDoTriggerEnter = ProjectileSpawner.ProjectileOnTriggerEnter;
            OnDoTriggerExit = ProjectileSpawner.ProjectileOnTriggerExit;
            OnDoTriggerStay = ProjectileSpawner.ProjectileOnTriggerStay;
            OnReturnToPool = ProjectileSpawner.ProjectileOnReturnToPool;
            OnGetFromPool = ProjectileSpawner.ProjectileOnGetFromPool;
            OnDoCollisionEnter = ProjectileSpawner.ProjectileOnCollisionEnter;
            OnDoCollisionExit = ProjectileSpawner.ProjectileOnCollisionExit;
            OnDoCollisionStay = ProjectileSpawner.ProjectileOnCollisionStay;
            OnDoDestroy = ProjectileSpawner.ProjectileOnDoDestroy;
            OnDoDisable = ProjectileSpawner.ProjectileOnDoDisable;
            OnDoEnable = ProjectileSpawner.ProjectileOnDoEnable;
            OnReset = ProjectileSpawner.ProjectileOnReset;
            OnProjectileSpawnerSet = ProjectileSpawner.ProjectileOnProjectileSpawnerSet;
        }

        // UNITY EVENTS
        [Serializable]
        public class ProjectileEvent : UnityEvent<Projectile>
        {
        }

        [Serializable]
        public class ProjectileSpawnerEvent : UnityEvent<ProjectileSpawner, Projectile>
        {
        }

        [Serializable]
        public class ProjectileCollisionEvent : UnityEvent<Projectile, Collision, GameObject, Vector3>
        {
        }

        [Serializable]
        public class ProjectileTriggerEvent : UnityEvent<Projectile, Collider, GameObject, Vector3>
        {
        }

        public void SetProjectileData(ProjectileData newProjectileData)
        {
            projectileData = newProjectileData;
        }

        public void SetSpawnBehavior(SpawnBehavior newSpawnBehavior)
        {
            spawnBehavior = newSpawnBehavior;
        }
    }
}
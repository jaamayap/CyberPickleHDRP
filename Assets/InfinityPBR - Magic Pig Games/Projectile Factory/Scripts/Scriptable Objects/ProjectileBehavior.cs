using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public abstract class ProjectileBehavior : ScriptableObject
    {
        protected bool ProjectileLaunched => Projectile.Launched;

        public Projectile Projectile { get; private set; }

        public ProjectileSpawner ProjectileSpawner { get; private set; }

        public GameObject ProjectileOwner => Projectile.ProjectileSpawner.gameObject;
        public float ProjectileSpeed => Projectile.ProjectileData.Speed;
        public Vector3 ProjectilePosition => Projectile.transform.position;
        public Vector3 ProjectileRotation => Projectile.transform.rotation.eulerAngles;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public virtual string InternalDescription =>
            "This behavior doesn't yet have an internal string. Open the script and " +
            "override the protected string _internalDescription to add one.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public virtual string InternalIcon => "Gear";

        protected virtual void OnDisable()
        {
            if (Projectile == null) return;
            UnsubscribeFromEvents(Projectile);
        }

        protected virtual void OnDestroy()
        {
            if (Projectile == null) return;
            UnsubscribeFromEvents(Projectile);
        }

        /*
         * NOTE:
         *
         * This is a Scriptable Object, so Update and similar methods will not be called automatically. Instead,
         * if you have created an instance of this Scriptable Object, you will need to call Update manually from a
         * MonoBehaviour script.
         *
         * Also, this is the base class for all Projectile Behaviors. It is not intended to be used directly. Each
         * behavior will optionally override the methods it needs to implement.
         */

        public abstract void OnReturnToPool(Projectile projectile);
        public abstract void OnGetFromPool(Projectile projectile);

        public virtual void SetSpawnerAndProjectile(ProjectileSpawner projectileSpawner, Projectile projectile)
        {
            SetProjectileSpawner(projectileSpawner);
            SetProjectile(projectile);
        }

        public virtual void SetProjectile(Projectile value)
        {
            Projectile = value;
        }

        public virtual void SetProjectileSpawner(ProjectileSpawner value)
        {
            ProjectileSpawner = value;
        }

        public virtual void SubscribeToEvents(Projectile projectile)
        {
            UnsubscribeFromEvents(projectile);
            projectile.LaunchProjectile += LaunchProjectile;
            projectile.CollisionEnter += CollisionEnter;
            projectile.CollisionExit += CollisionExit;
            projectile.CollisionStay += CollisionStay;
            projectile.TriggerEnter += TriggerEnter;
            projectile.TriggerExit += TriggerExit;
            projectile.TriggerStay += TriggerStay;
            projectile.DoDestroy += DoDestroy;
            projectile.Disable += Disable;
            projectile.Enable += Enable;
            projectile.ReturnToPool += OnReturnToPool;
            projectile.GetFromPool += OnGetFromPool;

            if (projectile.ProjectileSpawner != null)
                projectile.ProjectileSpawner.OnProjectileStopped += ProjectileStopped;
        }

        public virtual void UnsubscribeFromEvents(Projectile projectile)
        {
            projectile.LaunchProjectile -= LaunchProjectile;
            projectile.CollisionEnter -= CollisionEnter;
            projectile.CollisionExit -= CollisionExit;
            projectile.CollisionStay -= CollisionStay;
            projectile.TriggerEnter -= TriggerEnter;
            projectile.TriggerExit -= TriggerExit;
            projectile.TriggerStay -= TriggerStay;
            projectile.DoDestroy -= DoDestroy;
            projectile.Disable -= Disable;
            projectile.Enable -= Enable;
            projectile.ReturnToPool -= OnReturnToPool;
            projectile.GetFromPool -= OnGetFromPool;

            if (projectile.ProjectileSpawner != null)
                projectile.ProjectileSpawner.OnProjectileStopped -= ProjectileStopped;
        }

        protected virtual void ProjectileStopped()
        {
            // Do nothing
        }

        // Use this in situations where the projectile may not be live in the scene (i.e. Trajectory)
        public virtual void Tick()
        {
            // Do nothing
        }

        protected virtual void FixedTick()
        {
            // Do nothing
        }

        public virtual void LateTick()
        {
            // Do nothing
        }

        /*
        // Use this when you know the projectile is live in the scene, and want that guaranteed or throw an error.
        public virtual void Tick(Projectile projectile)
        {
            // Do nothing
        }

        protected virtual void FixedTick(Projectile projectile) {
            // Do nothing
        }

        public virtual void LateTick(Projectile projectile) {
            // Do nothing
        }
        */

        public virtual void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void CollisionExit(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void CollisionStay(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void TriggerExit(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void TriggerStay(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void LaunchProjectile(Projectile projectile)
        {
            // Do nothing
        }

        public virtual void ResetValues()
        {
            // Do Nothing
        }

        public virtual void DoDestroy(Projectile projectile)
        {
            UnsubscribeFromEvents(Projectile);
        }

        public virtual void DrawGizmos()
        {
            // Do nothing
        }

        public virtual void DrawGizmosSelected()
        {
            // Do nothing
        }

        public virtual void Validate()
        {
            // Do nothing
        }

        public virtual void Disable(Projectile projectile)
        {
            // Do nothing
        }

        public virtual void Enable(Projectile projectile)
        {
            // Do nothing
        }

        /*
        public virtual void OnReturnToPool() {
            // Do nothing
        }

        public virtual void OnGetFromPool() {
            // Do nothing
        }
        */

        // TRAJECTORY
        // These are for the TrajectoryBehavior, which is a special case.
        public virtual void ShowTrajectoryStart()
        {
            // Do nothing
        }

        /*
        public virtual bool ShowTrajectoryPerProjectileStart(ProjectileSpawner projectileSpawner, Projectile projectile)
        {
            if (!projectile.SpawnBehavior.showTrajectoryPerProjectile) return false;

            // Do nothing

            return true;
        }
        */

        public virtual void ShowTrajectoryStop()
        {
            // Do nothing
        }

        // This will create an object either from the Object Pool, or by instantiate.
        public virtual GameObject CreateObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                Debug.LogError(
                    $"Behavior {name} on projectile {Projectile.name}: Prefab is null. Cannot create object.");
                return null;
            }

            if (!Projectile.useObjectPool || ProjectilePoolManager.instance == null)
                return InstantiateNewObject(prefab, position, rotation);

            var projectileObject = ProjectilePoolManager.instance.GetProjectile(prefab);
            if (projectileObject == null)
            {
                var obj = InstantiateNewObject(prefab, position, rotation);
                obj.name = obj.name.RemoveClone();
                return obj;
            }

            // Reset the position and rotation
            projectileObject.SetActive(false); // Turn off while we move it to avoid particle system issues
            projectileObject.transform.position = position;
            projectileObject.transform.rotation = Quaternion.identity;
            projectileObject.SetActive(true);
            return projectileObject;
        }

        protected virtual GameObject InstantiateNewObject(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var obj = Instantiate(prefab, position, rotation);
            obj.name = obj.name.RemoveClone();
            return obj;
        }

        protected virtual void OnDrawGizmos()
        {
            
        }
    }
}
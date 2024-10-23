using System;
using UnityEngine;

/*
 * An ObserverObject is a Monobehavior which can observe a projectile and react to events. It has
 * the same lifecycle events as the other observer types.
 */

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("Observer Objects are Monobehaviors that can observe a projectile and react to events. " +
                   "They have the same lifecycle events as the other observer types. [This one doesn't seem " +
                   "to have an updated Internal Description. Please add one by overriding this string.]",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class ObserverObject : MonoBehaviour
    {
        [Header("Projectile to Observe")] public Projectile targetProjectile;

        protected virtual void Start()
        {
            if (targetProjectile == null)
                targetProjectile = GetComponent<Projectile>();

            if (targetProjectile == null)
            {
                Debug.LogError($"No projectile assigned to ObserverObject {this}. Please assign a projectile.");
                Destroy(this);
                return;
            }

            SubscribeToEvents(targetProjectile);
        }

        protected virtual void OnDestroy()
        {
            if (targetProjectile == null) return;
            UnsubscribeFromEvents(targetProjectile);
        }

        protected virtual void OnValidate()
        {
            if (targetProjectile == null || targetProjectile.transform.IsChildOf(transform.root))
                targetProjectile = GetComponent<Projectile>();
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

        public virtual void TriggerEnter(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void TriggerExit(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        public virtual void TriggerStay(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
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
            // Do nothing
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

        public virtual void OnReturnToPool(Projectile projectile)
        {
            // Do nothing
        }

        public virtual void OnGetFromPool(Projectile projectile)
        {
            // Do nothing
        }


        private void AddObserverToProjectile(ObserverObjectObserver observer)
        {
            Debug.Log($"Adding observer {observer} to projectile {targetProjectile}");

            var observerInstance = targetProjectile.AddObserver(observer) as ObserverObjectObserver;
            if (observerInstance == null)
            {
                Debug.LogError(
                    $"Observer {observer} is not an ObserverObjectObserver. It cannot be added to an ObserverObject.");
                return;
            }

            observerInstance.AddedFromObserverObject(this);
        }

        public void AddNewObserver(ObserverObjectObserver selectedNewObject)
        {
            //if (observers.Contains(selectedNewObject))
            //    return;

            //observers.Add(selectedNewObject);
        }
    }
}
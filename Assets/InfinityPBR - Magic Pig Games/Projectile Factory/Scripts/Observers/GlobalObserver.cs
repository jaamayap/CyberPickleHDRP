using System;
using UnityEngine;

/*
 * A "Global Observer" automatically registers to the events of the ProjectileFactoryManager and listens for all
 * events of ALL projectiles (unless those projectiles are set to not send their events to the ProjectileFactoryManager).
 */

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This is the base class for all Global Observers. Global Observers are automatically " +
                   "registered to the events of the ProjectileFactoryManager and listen for all events of ALL " +
                   "projectiles (unless those projectiles are set to not send their events to the " +
                   "ProjectileFactoryManager).",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/observers-global-observers-and-observer-objects")]
    [Serializable]
    public class GlobalObserver : MonoBehaviour
    {
        // We will subscribe to the events of the FactoryManager
        protected virtual void OnEnable()
        {
            FactoryManager.OnLaunchGlobal += OnLaunch;
            FactoryManager.CollisionEnterGlobal += CollisionEnter;
            FactoryManager.CollisionStayGlobal += CollisionStay;
            FactoryManager.CollisionExitGlobal += CollisionExit;
            FactoryManager.TriggerEnterGlobal += TriggerEnter;
            FactoryManager.TriggerStayGlobal += TriggerStay;
            FactoryManager.TriggerExitGlobal += TriggerExit;
            FactoryManager.DoDestroyGlobal += DoDestroy;
            FactoryManager.DisableGlobal += Disable;
            FactoryManager.EnableGlobal += Enable;
            FactoryManager.ReturnToPoolGlobal += ReturnToPool;
            FactoryManager.GetFromPoolGlobal += GetFromPool;
        }

        // We will unsubscribe from the events of the FactoryManager
        protected virtual void OnDisable()
        {
            FactoryManager.OnLaunchGlobal -= OnLaunch;
            FactoryManager.CollisionEnterGlobal -= CollisionEnter;
            FactoryManager.CollisionStayGlobal -= CollisionStay;
            FactoryManager.CollisionExitGlobal -= CollisionExit;
            FactoryManager.TriggerEnterGlobal -= TriggerEnter;
            FactoryManager.TriggerStayGlobal -= TriggerStay;
            FactoryManager.TriggerExitGlobal -= TriggerExit;
            FactoryManager.DoDestroyGlobal -= DoDestroy;
            FactoryManager.DisableGlobal -= Disable;
            FactoryManager.EnableGlobal -= Enable;
            FactoryManager.ReturnToPoolGlobal -= ReturnToPool;
            FactoryManager.GetFromPoolGlobal -= GetFromPool;
        }

        protected virtual void OnLaunch(Projectile projectile)
        {
            // Do nothing
        }

        protected virtual void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        protected virtual void CollisionStay(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        protected virtual void CollisionExit(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        protected virtual void TriggerEnter(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        protected virtual void TriggerStay(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        protected virtual void TriggerExit(Projectile projectile, Collider colliderValue, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            // Do nothing
        }

        protected virtual void DoDestroy(Projectile projectile)
        {
            // Do nothing
        }

        protected virtual void Disable(Projectile projectile)
        {
            // Do nothing
        }

        protected virtual void Enable(Projectile projectile)
        {
            // Do nothing
        }

        protected virtual void ReturnToPool(Projectile projectile)
        {
            // Do nothing
        }

        protected virtual void GetFromPool(Projectile projectile)
        {
            // Do nothing
        }
    }
}
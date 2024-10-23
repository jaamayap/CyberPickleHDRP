using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Simple Destroy or Pool",
        menuName = "Projectile Factory/Destroy Behavior/Simple Destroy or Pool")]
    [Serializable]
    public class DestroyBehavior : ProjectileBehavior
    {
        private float _delayTimer;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "Destroys the projectile immediately when DoDestroy is called. Note: This does not directly " +
            "call DoDestroy. Another behavior will need to do that.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Destroy";

        public override void OnReturnToPool(Projectile projectile)
        {
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }

        public override void DoDestroy(Projectile projectile)
        {
            RemoveObject();
        }

        protected virtual void PutBackInPool(float delay = 0)
        {
            ProjectilePoolManager.instance.PutBackProjectile(Projectile, delay);
        }

        public virtual void RemoveObject(float delay = 0)
        {
            if (Projectile.useObjectPool)
                PutBackInPool(delay);
            else
                Destroy(Projectile.gameObject, delay);
        }
    }
}
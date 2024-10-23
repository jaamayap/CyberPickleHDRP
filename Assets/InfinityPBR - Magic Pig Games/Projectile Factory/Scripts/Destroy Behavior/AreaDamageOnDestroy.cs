using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory.Demo
{
    [CreateAssetMenu(fileName = "New Area Damage on Destroy",
        menuName = "Projectile Factory/Destroy Behavior/Area Damage on Destroy")]
    [Serializable]
    public class AreaDamageOnDestroy : DestroyBehavior
    {
        [ShowInProjectileEditor("Radius")] public float radius = 6;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This behavior causes damage to all actors within a radius of the projectile.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Area Damage";

        // In this basic form, we will spawn the hitEffect at the collision point.
        public override void DoDestroy(Projectile projectile)
        {
            CauseDamageAtPosition(Projectile.transform.position);
            base.DoDestroy(Projectile);
        }

        protected virtual void CauseDamageAtPosition(Vector3 position)
        {
            var layerMask = Projectile.CollisionMask;
            var owner = Projectile.ProjectileSpawner.GetComponent<ProjectileDemoActor>();
            var colliders = Physics.OverlapSphere(position, radius, layerMask);
            foreach (var collider in colliders)
            {
                var actor = collider.GetComponent<ProjectileDemoActor>();
                if (actor == null)
                    continue;

                owner.RegisterHit(Projectile, actor);
            }
        }
    }
}
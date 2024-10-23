using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory.Demo
{
    [CreateAssetMenu(fileName = "New Trigger Destroy After Variable Hits",
        menuName = "Projectile Factory/Collision Behavior/Trigger Destroy After Variable Hits")]
    [Serializable]
    public class TriggerDestroyAfterVariableHits : CollisionBehavior
    {
        [Header("Hit Count")]
        [Tooltip("The number of hits before the projectile is destroyed.")]
        [ShowInProjectileEditor("Max Hits")]
        public int maxHits = 1;

        private int _hitCount;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This behavior causes the projectile to be destroyed after a certain number of hits.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Gear";

        // In this basic form, we will spawn the hitEffect at the collision point.
        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TriggerHit();
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TriggerHit();
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            _hitCount = 0;
        }

        private void TriggerHit()
        {
            _hitCount += 1;
            if (_hitCount >= maxHits) Projectile.TriggerDestroy();
        }
    }
}
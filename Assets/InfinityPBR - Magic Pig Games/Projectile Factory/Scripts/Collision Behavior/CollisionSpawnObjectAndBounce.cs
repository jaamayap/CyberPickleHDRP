using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Trigger Enter Spawn Object and Bounce",
        menuName = "Projectile Factory/Collision Behavior/Trigger Enter Spawn Object and Bounce")]
    [Serializable]
    public class CollisionSpawnObjectAndBounce : SpawnObjectOnTriggerOrCollision
    {
        // Internal icon is used for the custom inspector to show an icon of the behavior.
        //public override string InternalIcon => "Gear";


        [Header("Max Bounces - Optional")]
        [Tooltip(
            "If maxBounce > -1 then a DestroyBehavior must be set. Also, there should not be any DestroyBehavior " +
            "set on the projectile itself.")]
        [ShowInProjectileEditor("Max Bounces")]
        public int maxBounce = -1;

        [FormerlySerializedAs("destroyOrPoolOnTriggerOrCollisionAfterMaxBounces")]
        [Tooltip(
            "If maxBounce > -1 then a DestroyBehavior must be set. Also, there should not be any DestroyBehavior " +
            "set on the projectile itself.")]
        [ShowInProjectileEditor("Destroy or Pool On Trigger or Collision After Max Bounces")]
        public DestroyOnTriggerOrCollision destroyOnTriggerOrCollisionAfterMaxBounces;

        private int _bounceCount;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Collision behavior spawns an object at the collision point " +
            "and then bounces the projectile.";

        protected override void SpawnObjectAtPosition(Vector3 position)
        {
            base.SpawnObjectAtPosition(position);
            Bounce();
        }

        protected virtual void Bounce()
        {
            var position = -Projectile.transform.forward;
            Projectile.transform.LookAt(position);
            _bounceCount++;

            if (_bounceCount < maxBounce
                || maxBounce == -1
                || destroyOnTriggerOrCollisionAfterMaxBounces == null)
                return;

            destroyOnTriggerOrCollisionAfterMaxBounces = Instantiate(destroyOnTriggerOrCollisionAfterMaxBounces);
            destroyOnTriggerOrCollisionAfterMaxBounces.LaunchProjectile(Projectile);
            destroyOnTriggerOrCollisionAfterMaxBounces.DoDestroy(Projectile);
        }
    }
}
using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class CollisionBehavior : ProjectileBehavior
    {
        [Header("Collision Options")]
        [Tooltip(
            "If true, the projectile will use the custom collision mask. If false, it will use the default collision mask provided by " +
            "the Projectile or, more likely, Projectile Spawner.")]
        [ShowInProjectileEditor("Override Collision Mask")]
        public bool overrideProjectileCollisionMask;

        [Tooltip("The custom collision mask to use if overrideProjectileCollisionMask is true.")]
        [ShowInProjectileEditor("Custom Collision Mask")]
        public LayerMask customCollisionMask;

        public LayerMask CollisionMask =>
            overrideProjectileCollisionMask ? customCollisionMask : Projectile.CollisionMask;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Handles collision behavior for the projectile.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Gear";

        public override void OnReturnToPool(Projectile projectile)
        {
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }
    }
}
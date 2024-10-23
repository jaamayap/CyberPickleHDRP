using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Parent to Hit Object",
        menuName = "Projectile Factory/Collision Behavior/Parent to Hit Object")]
    [Serializable]
    public class CollisionParentToHitObject : CollisionBehavior
    {
        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Collision behavior will parent the projectile to the object it hits.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Cube";

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (objectHit == null) return;

            Projectile.transform.SetParent(objectHit.transform);
        }
    }
}
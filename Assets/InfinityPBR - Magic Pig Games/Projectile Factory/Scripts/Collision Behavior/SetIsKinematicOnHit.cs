using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Set Rigidbody Is Kinematic On Hit",
        menuName = "Projectile Factory/Collision Behavior/Set Rigidbody Is Kinematic On Hit")]
    [Serializable]
    public class SetIsKinematicOnHit : CollisionBehavior
    {
        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This behavior sets the Rigidbody of the projectile to be kinematic on hit.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Gear";

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            var rb = Projectile.GetComponent<Rigidbody>();
            if (rb != null)
                rb.isKinematic = true;
        }
    }
}
using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Look At Target Movement Behavior",
        menuName = "Projectile Factory/Moving Behavior/Look At Target")]
    [Serializable]
    public class LookAtTargetMovementBehavior : MovementBehavior
    {
        [ShowInProjectileEditor("Look Offset")]
        public Vector3 offset = new(0, 0, 0);

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Makes the projectile look at the target.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";

        public override void Tick()
        {
            if (Projectile.Target == null) return;
            Projectile.transform.LookAt(Projectile.TargetTransform.position + offset);
        }
    }
}
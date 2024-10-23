using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Basic Forward Movement Behavior",
        menuName = "Projectile Factory/Moving Behavior/Basic Forward Movement Behavior")]
    [Serializable]
    public class MovementBehaviorForward : MovementBehavior
    {
        [SerializeField] private Vector3 localDirection = Vector3.forward;
        
        [Header("Collision Options")] 
        [ShowInProjectileEditor("Change Direction After Collision")]
        public bool changeDirectionAfterCollision = true;

        [ShowInProjectileEditor("Local Direction After Collision")]
        public Vector3 localDirectionAfterCollision = Vector3.zero;

        protected bool _collided;
        private Vector3 _localDirection;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "Moves the projectile forward in the direction of the localDirection.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Move";

        public Vector3 LocalDirection => CalculateLocalDirection();

        protected virtual void Reset()
        {
            _collided = false;
            _localDirection = localDirection;
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            Reset();
        }

        protected virtual Vector3 CalculateLocalDirection()
        {
            return _localDirection;
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (!changeDirectionAfterCollision)
                return;

            _localDirection = localDirectionAfterCollision;
            _collided = true;
        }

        public override void Tick()
        {
            Move();
        }

        protected virtual void Move()
        {
            Projectile.transform.Translate(LocalDirection * (ProjectileSpeed * Time.deltaTime)
                , Space.Self);
        }
    }
}
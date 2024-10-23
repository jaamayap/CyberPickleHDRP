using UnityEngine;

/*
 * Add this to any particle that handles collision detection itself. This was made originally for integration with
 * Realistic Effects Pack 4, but will work with any particle that ITSELF collides with the target. In those cases,
 * Projectile Power won't be able to handle the collision, so this script will be used to receive the event, and then
 * pass that along to the Projectile class.
 */

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("Add this to any particle that handles collision detection itself. This was made " +
                   "originally for integration with Realistic Effects Pack 4, but will work with any particle that " +
                   "ITSELF collides with the target. In those cases, Projectile Power won't be able to handle the " +
                   "collision, so this script will be used to receive the event, and then pass that along to the " +
                   "Projectile class.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/overview-and-quickstart")]
    public class CollisionDetectorTurnOffObjects : MonoBehaviour
    {
        public Projectile projectile;
        public GameObject[] turnOffOnCollision;
        public bool turnOffCollider = true;
        public bool turnOnColliderOnEnable = true;

        [Header("Options")] public bool inheritCollisionMask = true;

        public LayerMask customCollisionMask;
        private Collider _collider;

        public LayerMask CollisionMask => inheritCollisionMask ? projectile.CollisionMask : customCollisionMask;

        private void OnEnable()
        {
            if (_collider == null)
                _collider = GetComponent<Collider>();
            if (turnOnColliderOnEnable && _collider != null)
                _collider.enabled = true;
        }

        private void OnCollisionEnter(Collision other)
        {
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            projectile.DoCollisionEnter(other);
            TurnOffObjects();
        }

        private void OnCollisionExit(Collision other)
        {
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            projectile.DoCollisionExit(other);
            TurnOffObjects();
        }

        private void OnCollisionStay(Collision other)
        {
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            projectile.DoCollisionStay(other);
            TurnOffObjects();
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            projectile.DoTriggerEnter(other);
            TurnOffObjects();
        }

        private void OnTriggerExit(Collider other)
        {
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            projectile.DoTriggerExit(other);
            TurnOffObjects();
        }

        private void OnTriggerStay(Collider other)
        {
            if ((CollisionMask.value & (1 << other.gameObject.layer)) == 0)
                return;

            projectile.DoTriggerStay(other);
            TurnOffObjects();
        }

        private void TurnOffObjects()
        {
            foreach (var obj in turnOffOnCollision)
                obj.SetActive(false);

            TurnOffCollider();
        }

        private void TurnOffCollider()
        {
            if (!turnOffCollider) return;
            if (_collider == null) return;

            _collider.enabled = false;
        }
    }
}
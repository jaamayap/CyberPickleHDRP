using System;
using UnityEngine;

/*
 * In this version, there is no actual projectile, so nothing will collide with anything, or trigger anything. Instead,
 * we will look to see if we hit something, and then register a hit or a miss, which will simulate the projectile
 * colliding or triggering something.
 *
 * Check the RegisterHit() method to see how we call the OnTriggerEnter method on the projectile manually, allowing
 * observers to know that we "hit" something.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Instant Hit Behavior",
        menuName = "Projectile Factory/Collision Behavior/Instant Hit Forward")]
    [Serializable]
    public class InstantHitForward : CollisionBehavior
    {
        [SerializeField] private Vector3 localDirection = Vector3.forward;

        [Tooltip(
            "The delay before the projectile checks for a hit. This is useful for things like beams that need to " +
            "wait for the first frame to pass.")]
        [ShowInProjectileEditor("Hit Check Delay")]
        public float hitCheckDelay;

        [Header("Hit Detection")]
        [ShowInProjectileEditor("Max Checks")]
        [Tooltip("The number of checks to perform. Set to -1 for infinite checks.")]
        public int maxChecks = -1;

        [ShowInProjectileEditor("Use Sphere Cast")]
        public bool useSphereCast;

        [Range(0.1f, 10f)] [ShowInProjectileEditor("Sphere Cast Radius")]
        public float sphereCastRadius = 0.1f;

        [Tooltip("Any distance greater than this will be considered a miss, and the projectile will be removed.")]
        [ShowInProjectileEditor("Max Distance")]
        public float maxDistance = 50f;

        /*
        [ShowInProjectileEditor("Override Layer Mask")]
        public bool overrideLayerMask = false;
        [ShowInProjectileEditor("Layer Mask")]
        public LayerMask layerMask;
        */

        [Header("Hit Particle")] [ShowInProjectileEditor("Hit Particle")]
        public GameObject hitParticle;

        [ShowInProjectileEditor("Hit Particle Life")]
        public float hitParticleLife = 5f;

        [ShowInProjectileEditor("Override Hit Particle Layer Mask")]
        public bool overrideHitParticleLayerMask;

        [ShowInProjectileEditor("Hit Particle Layer Mask")]
        public LayerMask hitParticleLayerMask;

        [Header("Hit Decal")] [ShowInProjectileEditor("Hit Decal")]
        public GameObject hitDecal;

        [ShowInProjectileEditor("Hit Decal Life")]
        public float hitDecalLife = 5f;

        [ShowInProjectileEditor("Override Hit Decal Layer Mask")]
        public bool overrideHitDecalLayerMask;

        [ShowInProjectileEditor("Hit Decal Layer Mask")]
        public LayerMask hitDecalLayerMask;

        private int _checks;

        private bool _stopCalled;

        //public LayerMask CollisionMask => overrideLayerMask ? layerMask : Projectile.CollisionMask;
        public LayerMask CollisionMaskDecal => overrideHitDecalLayerMask ? hitDecalLayerMask : Projectile.CollisionMask;

        public LayerMask CollisionMaskParticle =>
            overrideHitParticleLayerMask ? hitParticleLayerMask : Projectile.CollisionMask;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This behavior will check for an instant hit in the forward direction of the projectile. Note: It will " +
            "always use the Projectile Spawner CollisionMask for the hit check.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Raycast";


        private void OnEnable()
        {
            _checks = 0;
            _stopCalled = false;
        }
        //private bool _launchCalled = false;


        public Vector3 LocalDirection()
        {
            var direction = Projectile.ProjectileSpawner.RotatingTransform.TransformDirection(localDirection);
            return direction;
        }

        public override void Tick()
        {
            if (_stopCalled) return;
            if (maxChecks > 0 && _checks >= maxChecks) return;

            if (hitCheckDelay > 0)
            {
                hitCheckDelay -= Time.deltaTime;
                return;
            }

            CalculateHit();
            _checks += 1;
        }

        protected override void ProjectileStopped()
        {
            //_stopCalled = true;
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            //_launchCalled = true;
        }

        public override void DoDestroy(Projectile projectile)
        {
            _stopCalled = false;
        }

        protected void CalculateHit()
        {
            if (useSphereCast &&
                ProjectileSpawner.TryGetSphereCastHit(out var sphereHit, sphereCastRadius, maxDistance))
                RegisterHit(sphereHit);
            else if (!useSphereCast && Projectile.ProjectileSpawner.TryGetRaycastHit(out var hit,
                         Projectile.ProjectileSpawner.SpawnPosition, maxDistance))
                RegisterHit(hit);
            else
                RegisterMiss(Projectile);
        }


        protected virtual void RegisterHit(RaycastHit hitInfo)
        {
            SpawnHitEffect(hitInfo);

            // Register a collision at this point
            Projectile.TriggerCollisionWithObject(hitInfo.collider.gameObject, hitInfo.point);

            /*
             * April 14 2024
             * I've removed the code below as it was an earlier version, and unneeded. I've also added the
             * TriggerCollisionWithObject line above, which is newer than the code below, and is the proper
             * way to trigger a collision with an object.
             */
            // Convert hitInfo to a collider for the observer, and call the OnTriggerEnter event manually. This will
            // ensure any observers that are looking for OnTriggerEnter events will be notified.
            //var collider = hitInfo.collider;
            //Projectile.DoTriggerEnter(collider);

            //Projectile.TriggerDestroy();
        }

        protected virtual void SpawnHitEffect(RaycastHit hitInfo)
        {
            // see if the object that was hit is in the hitParticleLayerMask
            if (CollisionMaskParticle == (CollisionMaskParticle | (1 << hitInfo.collider.gameObject.layer)))
                SpawnHitParticle(Projectile, hitInfo);
            if (CollisionMaskDecal == (CollisionMaskDecal | (1 << hitInfo.collider.gameObject.layer)))
                SpawnHitDecal(hitInfo);
        }

        protected virtual void SpawnHitDecal(RaycastHit hitInfo)
        {
            if (hitDecal == null) return;

            var hitEffectRotation = Quaternion.LookRotation(hitInfo.normal);
            var instance = Instantiate(hitDecal, hitInfo.point, hitEffectRotation);

            instance.transform.SetParent(hitInfo.collider.transform);
            instance.transform.position += instance.transform.forward * 0.01f;
            Destroy(instance, hitDecalLife);
        }

        protected virtual void SpawnHitParticle(Projectile projectile, RaycastHit hitInfo)
        {
            if (hitParticle == null) return;

            var instance = Instantiate(hitParticle, hitInfo.point, Quaternion.identity);
            Destroy(instance, hitParticleLife);
        }

        protected virtual void RegisterMiss(Projectile projectile)
        {
            //Debug.Log("Miss");
            projectile.TriggerDestroy();
        }
    }
}
using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Turn Off Particles on Collision",
        menuName = "Projectile Factory/Collision Behavior/Turn Off Particles on Collision")]
    [Serializable]
    public class TurnOffParticlesOnCollision : CollisionBehavior
    {
        [Header("Turn Off Particles Options")] [ShowInProjectileEditor("Include Children")]
        public bool includeChildren = true;

        [ShowInProjectileEditor("Include Trails")]
        public bool includeTrails;

        [ShowInProjectileEditor("Include Emission Over Distance")]
        public bool includeEmissionOverDistance;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Turns off particles on collision.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Gear";

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TurnOffParticles();
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            TurnOffParticles();
        }

        private void TurnOffParticles()
        {
            if (!includeChildren)
            {
                HandleParticleSystem(Projectile.GetComponent<ParticleSystem>());
                return;
            }

            var particleSystems = Projectile.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particleSystems)
                HandleParticleSystem(ps);
        }

        private void HandleParticleSystem(ParticleSystem ps)
        {
            if (!includeTrails && ps.trails.enabled)
                return;
            if (!includeEmissionOverDistance && ps.emission.rateOverDistance.constant > 0)
                return;
            ps.Stop();
        }
    }
}
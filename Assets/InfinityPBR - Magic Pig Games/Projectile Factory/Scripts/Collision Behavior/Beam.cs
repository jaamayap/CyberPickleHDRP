using System;
using UnityEngine;

/*
 * The "Beam" essentially attaches itself to the spawn object, and stays "on" until destroy is called. This is useful
 * for laser beams, flames, and other things that are not projectiles per se, but a single thing that is constant.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Beam", menuName = "Projectile Factory/Collision Behavior/Beam")]
    [Serializable]
    public class Beam : InstantHitForward
    {
        public bool _returnedToPool;

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        //public override string InternalIcon => "Gear";


        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "The Beam behavior attaches the projectile to the spawn object, and " +
            "stays on until destroy is called. Useful for laser beams, flames, and " +
            "other things that are not projectiles per se, but a single thing that is constant.";

        public bool Attached { get; private set; }

        public override void Tick()
        {
            if (_returnedToPool) return;

            AttachProjectile(); // this will only happen once

            base.Tick();
        }

        protected void AttachProjectile()
        {
            if (Attached) return;

            Projectile.transform.parent = Projectile.ProjectileSpawner.SpawnTransform;
            Attached = true;
        }

        protected override void RegisterHit(RaycastHit hitInfo)
        {
            // Convert hitInfo to a collider for the observer, and call the OnTriggerEnter event manually. This will
            // ensure any observers that are looking for OnTriggerEnter events will be notified.
            var collider = hitInfo.collider;
            Projectile.DoTriggerEnter(collider);

            SpawnHitEffect(hitInfo);
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            Attached = false;
            _returnedToPool = true;
        }

        public override void OnGetFromPool(Projectile projectile)
        {
            _returnedToPool = false;
        }

        public override void DoDestroy(Projectile projectile)
        {
        }
    }
}
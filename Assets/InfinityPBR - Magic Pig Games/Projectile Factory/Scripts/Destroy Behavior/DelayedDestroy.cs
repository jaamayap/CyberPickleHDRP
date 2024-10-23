using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Delayed Destroy",
        menuName = "Projectile Factory/Destroy Behavior/Delayed Destroy")]
    [Serializable]
    public class DelayedDestroy : DestroyBehavior
    {
        [Header("Options")] [ShowInProjectileEditor("Destroy Delay")]
        public float destroyDelay = 13f;

        private bool _done;

        private float _timer;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "This behavior destroys the projectile after a delay.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Destroy";

        public override void LaunchProjectile(Projectile projectile)
        {
            ResetValues();
        }

        public override void ResetValues()
        {
            _timer = 0f;
            _done = false;
        }

        public override void Tick()
        {
            if (_done)
                return;
            _timer += Time.deltaTime;
            if (_timer >= destroyDelay)
            {
                _done = true;
                Projectile.TriggerDestroy();
            }
        }
    }
}
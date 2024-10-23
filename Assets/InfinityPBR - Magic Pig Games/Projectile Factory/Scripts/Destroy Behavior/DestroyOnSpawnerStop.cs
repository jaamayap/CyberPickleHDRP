using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Destroy On Spawner Stop",
        menuName = "Projectile Factory/Destroy Behavior/Destroy On Spawner Stop")]
    [Serializable]
    public class DestroyOnSpawnerStop : DestroyBehavior
    {
        [Header("Options")] [ShowInProjectileEditor("Delay")]
        public float destroyDelay;

        [Header("Fallback Destroy")] [ShowInProjectileEditor("Destroy If No Stop Occurs")]
        public bool destroyIfNoStopOccurs;

        [ShowInProjectileEditor("Wait Timer For Stop")]
        public float waitTimerForStop = 5f;

        private float _waitTimerForStop;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "This behavior destroys the projectile when the spawner stops.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Destroy";

        protected override void ProjectileStopped()
        {
            DoDestroy(Projectile);
        }

        public override void DoDestroy(Projectile projectile)
        {
            RemoveObject(destroyDelay);
        }

        public override void Tick()
        {
            if (!destroyIfNoStopOccurs)
                return;

            _waitTimerForStop += Time.deltaTime;
            if (_waitTimerForStop > waitTimerForStop)
                DoDestroy(Projectile);
        }
    }
}
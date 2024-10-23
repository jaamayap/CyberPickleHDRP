using System;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.ProjectileUtilities;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class DemoControllerMultipleBarrels : DemoController
    {
        public ProjectileSpawner[] barrels;
        public TriggerMovementOnSpawnPointChange[] _triggerMovements;


        protected override void SetTrajectoryValue()
        {
            barrels[0].showTrajectory = Input.GetKey(showTrajectoryKey);
        }

        protected override void TryChangeProjectile()
        {
            if (!Input.GetKeyDown(nextProjectileKey)) return;

            if (Input.GetKey(oppositeModifierKey))
                foreach (var barrel in barrels)
                    barrel.NextProjectile(-1);
            else
                foreach (var barrel in barrels)
                    barrel.NextProjectile();
        }

        protected override bool TryShoot()
        {
            if (!Input.GetKeyDown(shootKey)) return false;

            if (IsPointerOverUIElementWithTag(clickBlockString))
                return false;

            for (var index = 0; index < barrels.Length; index++)
            {
                var barrel = barrels[index];
                barrel.SpawnProjectile();
                TriggerMovementInBarrel(index);
            }

            return true;
        }

        private void TriggerMovementInBarrel(int index)
        {
            if (_triggerMovements[index] == null)
                _triggerMovements[index] = barrels[index].GetComponent<TriggerMovementOnSpawnPointChange>();

            _triggerMovements[index].Fire();
        }

        protected override bool TryStopShooting()
        {
            if (!Input.GetKeyUp(shootKey)) return false;

            foreach (var barrel in barrels)
                barrel.StopProjectile();

            return true;
        }
    }
}
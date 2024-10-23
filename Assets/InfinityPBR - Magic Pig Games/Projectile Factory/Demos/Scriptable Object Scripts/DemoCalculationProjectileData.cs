using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Demo Calculation ProjectileData",
        menuName = "Projectile Factory/Enemy Projectile Data")]
    public class DemoCalculationProjectileData : ProjectileData
    {
        public float speedMultiplier;
        public float maxDamage;

        protected override float CalculateSpeed()
        {
            return speed * speedMultiplier;
        }

        protected override float CalculateDamage()
        {
            return damage >= maxDamage ? damage : Random.Range(damage, maxDamage);
        }
    }
}
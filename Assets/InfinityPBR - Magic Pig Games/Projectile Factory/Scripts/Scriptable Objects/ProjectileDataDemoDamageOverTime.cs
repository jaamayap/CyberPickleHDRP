using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Damage over Time Projectile Data",
        menuName = "Projectile Factory/Projectile Data - Demo Damage Over Time")]
    [Serializable]
    public class ProjectileDataDemoDamageOverTime : ProjectileData
    {
        protected override float CalculateDamage()
        {
            return damage * Time.deltaTime;
        }
    }
}
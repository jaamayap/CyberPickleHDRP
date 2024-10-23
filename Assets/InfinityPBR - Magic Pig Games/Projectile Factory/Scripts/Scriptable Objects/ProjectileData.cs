using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Basic ProjectileData", menuName = "Projectile Factory/Projectile Data")]
    [Serializable]
    public class ProjectileData : ScriptableObject
    {
        [Header("Required Values")]
        [ShowInProjectileEditor("Speed")] // Use this to get the field to show in the custom inspector!
        [Tooltip("The speed of the projectile. You may wish to override ProjectileData with your own speed logic!")]
        public float speed;

        [Header("Optional Values: For your custom scripts")]
        [Tooltip("The speed of the projectile. You should override ProjectileData with your own speed logic, as this " +
                 "is really just for demo purposes only!")]
        [ShowInProjectileEditor("Damage")]
        // Use this to get the field to show in the custom inspector!
        public float damage;

        public float Speed => CalculateSpeed();
        public float Damage => CalculateDamage();

        /*
         * Note: You will likely derive from this class to create your own ProjectileData. If you do, you will need to
         * write a more specific version of CalculateSpeed() and CalculateDamage() in your derived class. For example,
         * one that calculates the speed and damage based on the distance to the target or stats or whatever else. You
         * may also want to chance the computed values in a new private variable if they won't change during gameplay.
         *
         * Entirely up to you.
         */

        protected virtual float CalculateSpeed()
        {
            return speed;
        }

        protected virtual float CalculateDamage()
        {
            return damage;
        }

        // Other common properties
    }
}
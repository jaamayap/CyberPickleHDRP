using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation(
        "This script applies a force to objects within a radius of the projectile. Useful for impact particles and " +
        "the end of line renderers, such as lasers. This version adds a check to only apply force when the projectile is launched.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/project-force")]
    [Serializable]
    public class ProjectForceProjectile : ProjectForce
    {
        [Header("Projectile Options")] public Projectile projectile;

        public bool onlyApplyForceWhenLaunched = true;

        protected override void Update()
        {
            SetForceActive(!(onlyApplyForceWhenLaunched && !projectile.Launched));

            base.Update();
        }
    }
}
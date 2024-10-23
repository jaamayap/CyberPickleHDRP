using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class SpawnBehaviorModification : ScriptableObject
    {
        protected SpawnBehaviorModification thisInstanceOnProjectile;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public virtual string InternalDescription =>
            "This behavior doesn't yet have an internal string. Open the script and " +
            "override the protected string _internalDescription to add one.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public virtual string InternalIcon => "Gear";

        public virtual void OnSpawn(GameObject projectileObject, Projectile projectile)
        {
            AddThisInstanceToProjectile(projectile);
        }

        protected virtual void AddThisInstanceToProjectile(Projectile basicProjectile)
        {
            basicProjectile.spawnBehaviorModificationsInstances.Add(this);
            thisInstanceOnProjectile = basicProjectile.spawnBehaviorModificationsInstances[^1]; // Cache this
        }
    }
}
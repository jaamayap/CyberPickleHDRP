using System;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class ProjectileAudioBehavior : ProjectileBehavior
    {
        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Audio behavior doesn't yet have an internal string. Open the script and " +
            "override the protected string _internalDescription to add one.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Audio";

        public override void LaunchProjectile(Projectile projectile)
        {
        }

        public override void OnReturnToPool(Projectile projectile)
        {
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }
    }
}
using System;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class ProjectileObserver : ProjectileBehavior
    {
        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "Observers watch a projectile and react to events. This Observer " +
            "does not yet have an InternalDescription. Please add one.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Eye";

        public override void OnReturnToPool(Projectile projectile)
        {
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }
    }
}
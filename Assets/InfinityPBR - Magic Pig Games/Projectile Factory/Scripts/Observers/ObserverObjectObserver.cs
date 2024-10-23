using System;

namespace MagicPigGames.ProjectileFactory
{
    /*
     * This is a base class for any Observer that is intended to be used with an ObserverObject.
     * ObserverObjects are not Projectile objects, but other objects related to the projectile which
     * want to do something based on the Projectile lifecycle events.
     */

    [Serializable]
    public class ObserverObjectObserver : ProjectileObserver
    {
        // The ObserverObject class calls this. Each ProjectileObserver that is intended to be used on a game object
        // should override this method and set up the ObserverObject as needed.
        public virtual void AddedFromObserverObject(ObserverObject observerObject)
        {
            // Do Nothing
        }
    }
}
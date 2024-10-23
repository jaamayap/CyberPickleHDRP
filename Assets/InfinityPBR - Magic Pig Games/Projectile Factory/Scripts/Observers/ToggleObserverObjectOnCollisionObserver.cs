using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Toggle Observer Object On Collision Observer",
        menuName = "Projectile Factory/Observers/Toggle Observer Object On Collision")]
    [Serializable]
    public class ToggleObserverObjectOnCollisionObserver : ObserverObjectObserver
    {
        private List<GameObject> _objectsToToggle = new();

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "Add this to a ObserverObject " +
            "to turn off the object when the projectile collides with something, and turn it " +
            "back on at Launch.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Eye";

        public override void AddedFromObserverObject(ObserverObject observerObject)
        {
            /*
            foreach (var obj in observerObject.objects)
            {
                if (_objectsToToggle.Contains(obj)) continue;
                _objectsToToggle.Add(obj);
            }
            */
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            Debug.Log("COLLISION ENTER");

            foreach (var obj in _objectsToToggle)
                obj.SetActive(false);
        }

        public override void OnGetFromPool(Projectile projectile)
        {
            Debug.Log("OnGetFromPool PROJECTILE");
            foreach (var obj in _objectsToToggle)
                obj.SetActive(true);
        }
    }
}
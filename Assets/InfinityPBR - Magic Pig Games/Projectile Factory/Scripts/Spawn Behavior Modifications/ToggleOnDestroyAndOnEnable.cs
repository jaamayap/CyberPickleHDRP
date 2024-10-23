using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class ToggleOnDestroyAndOnEnable : MonoBehaviour
    {
        [FormerlySerializedAs("projectile")] [Header("Plumbing")]
        public Projectile basicProjectile;

        public GameObject[] objectsToHandle;

        public void OnEnable()
        {
            if (basicProjectile == null)
                basicProjectile = GetComponent<Projectile>();

            TurnOnObjects();
            Subscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            basicProjectile.DoDestroy += DoDestroy;
        }

        private void Unsubscribe()
        {
            basicProjectile.DoDestroy -= DoDestroy;
        }

        public void TurnOnObjects()
        {
            foreach (var obj in objectsToHandle)
                obj.SetActive(true);
        }

        public void DoDestroy(Projectile basicProjectile)
        {
            foreach (var obj in objectsToHandle)
                obj.SetActive(false);
        }
    }
}
using TMPro;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory.Demo
{
    public class ActiveProjectileText : MonoBehaviour
    {
        public ProjectileSpawner projectileSpawner;
        public TextMeshProUGUI text;

        private void Update()
        {
            if (projectileSpawner.ProjectileObject is null)
            {
                text.text = "No projectile selected";
                return;
            }

            text.text = projectileSpawner.ProjectileObject.name;
        }
    }
}
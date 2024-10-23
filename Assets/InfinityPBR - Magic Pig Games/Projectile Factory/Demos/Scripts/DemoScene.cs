using UnityEngine;

namespace MagicPigGames.ProjectileFactory.Demo
{
    public class DemoScene : MonoBehaviour
    {
        public static DemoScene instance;

        public float DamageInflicted { get; private set; }

        private void Awake()
        {
            instance = this;
        }

        public void AddDamage(float value)
        {
            DamageInflicted += value;
        }
    }
}
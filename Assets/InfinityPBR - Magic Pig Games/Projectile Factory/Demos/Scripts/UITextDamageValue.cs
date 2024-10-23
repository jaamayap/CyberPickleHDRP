using TMPro;
using UnityEngine;
using static MagicPigGames.ProjectileFactory.Demo.DemoScene;

namespace MagicPigGames.ProjectileFactory.Demo
{
    public class UITextDamageValue : MonoBehaviour
    {
        public TextMeshProUGUI text;

        private void Update()
        {
            text.text = "Points: " + Mathf.Round(instance.DamageInflicted);
        }
    }
}
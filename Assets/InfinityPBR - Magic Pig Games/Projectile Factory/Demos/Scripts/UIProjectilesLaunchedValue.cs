using TMPro;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory.Demo
{
    public class UIProjectilesLaunchedValue : MonoBehaviour
    {
        public TextMeshProUGUI text;
        public ProjectileDemoActor demoActor;

        private void Update()
        {
            text.text = "Launched: " + demoActor.ProjectilesLaunched;
        }
    }
}
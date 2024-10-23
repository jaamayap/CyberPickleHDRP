using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace MagicPigGames.ProjectileFactory.Demo
{
    public class TrajectoryLockText : MonoBehaviour
    {
        [FormerlySerializedAs("projector")] [FormerlySerializedAs("demoCaster")]
        public ProjectileSpawner projectileSpawner;

        public TextMeshProUGUI text;

        private void Update()
        {
            text.text
                = projectileSpawner.alwaysShowTrajectory
                    ? "Always Show Trajectory: ON"
                    : "Always Show Trajectory: OFF";
        }
    }
}
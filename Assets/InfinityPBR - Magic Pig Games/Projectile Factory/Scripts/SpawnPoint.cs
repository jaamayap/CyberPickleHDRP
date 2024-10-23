using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class SpawnPoint
    {
        public Transform transform;
        public Transform rotatingTransform;
        public Transform tiltingTransform;

        public Vector3 Position => transform.position;
        public Vector3 Rotation => transform.rotation.eulerAngles;

        public Vector3 ForwardDistance(float distance)
        {
            return Position + transform.forward * distance;
        }
    }
}
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation(
        "Use in conjunction with RaycastShooter.cs to set the LineRenderer length and position. This will also animated the materials offsets",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/projectile-factory-documentation/additional-scripts/raycast-shooter-+-handlers")]
    [RequireComponent(typeof(LineRenderer))]
    public class LineRendererLengthAnimateOffsets : LineRendererLength, IHandleRaycasts
    {
        [Header("Animation Options")] public bool animateOffsets = true;

        public Vector2 offsetSpeed = new(-3f, 0f);
        public string textureName = "_MainTex";

        protected override void Update()
        {
            base.Update();
            AnimateOffsets();
        }

        protected virtual void AnimateOffsets()
        {
            if (!animateOffsets) return;

            var offset = Time.time * offsetSpeed;
            lineRenderer.material.SetTextureOffset(textureName, offset);
        }
    }
}
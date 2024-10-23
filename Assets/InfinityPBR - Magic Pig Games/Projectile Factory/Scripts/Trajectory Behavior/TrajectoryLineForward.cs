using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Forward Line Trajectory",
        menuName = "Projectile Factory/Trajectory Behavior/Forward Line Trajectory")]
    [Serializable]
    public class TrajectoryLineForward : TrajectoryBehavior
    {
        [Header("Line Renderer Options")]
        [Min(0f)]
        [Tooltip("The width of the line at the start.")]
        [ShowInProjectileEditor("Start Width")]
        public float startWidth = 0.05f;

        [Min(0f)] [Tooltip("The width of the line at the end.")] [ShowInProjectileEditor("End Width")]
        public float endWidth = 0.05f;

        [Min(0f)] [Tooltip("The length of the line.")] [ShowInProjectileEditor("Length")]
        public float length = 1f;

        [Tooltip("The color of the line.")] [ShowInProjectileEditor("Line Color")]
        public Color lineColor = Color.red;

        private Material _lineRendererMaterial;

        /*
         * Trajectory Behavior includes all the standard methods available to override as the other behaviors, but
         * it is generally handled prior to the projectile being launched. Therefore, it is not necessary to override
         * everything unless there are changes that happen while the projectile is launched, such as changing the
         * visual effects of a target overlay, etc.
         *
         * In many cases, the trajectory will be displayed prior to launch, and will no longer be displayed once a
         * projectile has launched. Up to you and your game! :)
         */

        protected List<LineRenderer> lineRenderers = new();

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Displays a line forward from the projectile spawn position.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Target";

        /// <summary>
        ///     Shows the start of the projectile trajectory by setting the projectile spawner and creating a line renderer.
        /// </summary>
        /// <param name="projectileSpawner">The projectile spawner used to spawn the projectile.</param>
        /// <param name="basicProjectile projectile to show the trajectory for.
        /// 
        /// 
        /// 
        /// </param>
        public override void ShowTrajectoryStart()
        {
            CreateLineRenderers();
            // Create a single line renderer
        }

        // This is called when we start showing trajectory, and it creates the line renderers, one for each
        // spawn point if we are showing from all spawn points.
        protected virtual void CreateLineRenderers()
        {
            if (!showFromAllSpawnPoints)
            {
                CreateLineRenderer();
                return;
            }

            // We are showing all, so create multiple
            foreach (var spawnPoint in ProjectileSpawner.spawnPoints)
                CreateLineRenderer();
        }

        // This is called when we stop showing the trajectory, and it disables all line renderers.
        public override void ShowTrajectoryStop()
        {
            if (lineRenderers == null) return;

            foreach (var lineRenderer in lineRenderers)
                lineRenderer.enabled = false;
            lineRenderers.Clear();
        }

        // Here we pass both the projector and the projectile, as the projectile may not be live, so it won't have
        // a reference to the projector. Depending on your behavior, you may not need to use the projector. When 
        // a Projectile is live in the scene, it will call this with it's own Projector reference.
        public override void Tick()
        {
            if (lineRenderers == null)
                CreateLineRenderer();

            if (lineRenderers == null)
                return;

            if (lineRenderers.Count == 0) return;

            SetLineRendererPosition();
            SetLineColor();
            SetLineWidth();
        }

        protected virtual void SetLineWidth()
        {
            foreach (var lineRenderer in lineRenderers)
            {
                lineRenderer.startWidth = startWidth;
                lineRenderer.endWidth = endWidth;
            }
        }

        protected virtual void SetLineColor()
        {
            _lineRendererMaterial.color = lineColor;
        }

        // Stop showing the trajectory line when the projectile is launched.
        public override void LaunchProjectile(Projectile projectile)
        {
            ShowTrajectoryStop();
        }

        private void SetLineRendererPosition()
        {
            // If we are not showing all points, then just do the first one, and choose the
            // active spawn position.
            if (!showFromAllSpawnPoints)
            {
                var lineRenderer = lineRenderers[0];
                PositionLineRenderer(lineRenderer
                    , ProjectileSpawner.SpawnPosition
                    , ProjectileSpawner.SpawnForwardDistance(length));
                return;
            }

            // If we are showing all points, then do all of them.
            for (var i = 0; i < lineRenderers.Count; i++)
                PositionLineRenderer(lineRenderers[i]
                    , ProjectileSpawner.spawnPoints[i].Position
                    , ProjectileSpawner.spawnPoints[i].ForwardDistance(length));
        }

        // This will position the line renderer from the start to the stop position.
        private void PositionLineRenderer(LineRenderer lineRenderer, Vector3 startPos, Vector3 stopPos)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, startPos);
            lineRenderer.SetPosition(1, stopPos);
            lineRenderer.enabled = true;
        }

        // Creates a new Line Renderer and adds it to the list of line renderers.
        protected virtual void CreateLineRenderer()
        {
            var newLineRenderer = CreateAndConfigureLineRendererObject();
            lineRenderers.Add(newLineRenderer);

            newLineRenderer.startWidth = startWidth;
            newLineRenderer.endWidth = endWidth;

            newLineRenderer.material = new Material(Shader.Find("Unlit/Color")) { color = lineColor };
            _lineRendererMaterial = newLineRenderer.material;
        }

        // Creates a new Line Renderer object and returns the Line Renderer component.
        private LineRenderer CreateAndConfigureLineRendererObject()
        {
            var lineRendererObj = new GameObject("TrajectoryLine");
            lineRendererObj.AddComponent<LineRenderer>();
            return lineRendererObj.GetComponent<LineRenderer>();
        }
    }
}
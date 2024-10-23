using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagicPigGames.ProjectileFactory
{
    [Serializable]
    public class TrajectoryBehavior : ProjectileBehavior
    {
        [Header("Pre Launch Settings")]
        [Tooltip("[Logic must be set up per-trajectory] This is the range at which the preview will check for hits.")]
        [ShowInProjectileEditor("Hit Preview Range")]
        public float hitPreviewRange = 20f;

        [Tooltip(
            "[Logic must be set up per-trajectory] If true, the preview will show from all spawn points on the projectile.")]
        [ShowInProjectileEditor("Show From All Spawn Points")]
        public bool showFromAllSpawnPoints = true;

        [Header("Debug Line")]
        [Tooltip("If true, a debug line will be drawn in the scene view.")]
        [ShowInProjectileEditor("Show Debug Line")]
        public bool showDebugLine = true;

        [Tooltip("The color of the debug line.")] [ShowInProjectileEditor("Debug Line Color")]
        public Color debugLineColor = Color.grey;

        [Tooltip("[Logic must be set up per-trajectory] The color of the debug line when it hits a target.")]
        [ShowInProjectileEditor("Debug Line Color on Target")]
        public Color debugLineColorOnTarget = Color.red;

        protected LayerMask _collisionMask;

        protected Transform _spawnTransform;

        /*
         * Trajectory Behavior includes all the standard methods available to override as the other behaviors, but
         * it is generally handled prior to the projectile being launched. Therefore, it is not necessary to override
         * everything unless there are changes that happen while the projectile is launched, such as changing the
         * visual effects of a target overlay, etc.
         *
         * In many cases, the trajectory will be displayed prior to launch, and will no longer be displayed once a
         * projectile has launched. Up to you and your game! :)
         */

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "This Trajectory behavior doesn't yet have an internal string. Open the script and " +
            "override the protected string _internalDescription to add one.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Target";

        public override void ShowTrajectoryStop()
        {
            // Do Nothing
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            // Do Nothing
        }

        public override void OnGetFromPool(Projectile projectile)
        {
            // Do Nothing
        }

        // TRAJECTORY
        // These are for the TrajectoryBehavior, which is a special case.
        public override void ShowTrajectoryStart()
        {
            // Do nothing
        }

        // This is a simple forward line. For trajectories that aren't forward lines, this doesn't make as much
        // sense, but those trajectories can override this method, if it's important to see the preview in the 
        // Editor scene view. Generally, it likely won't be that important, I'd think!
        protected virtual void DrawDebugLine(RaycastHit hit = default)
        {
            if (!showDebugLine) return;
            if (_spawnTransform == null) return;

            var lineColor = debugLineColor;
            if (ProjectileSpawner != null)
                lineColor = hit.collider != null && ProjectileSpawner.collisionMask ==
                    (ProjectileSpawner.collisionMask | (1 << hit.collider.gameObject.layer))
                        ? debugLineColorOnTarget
                        : debugLineColor;

            var range = hit.collider != null ? hit.distance : hitPreviewRange;

            Debug.DrawLine(_spawnTransform.position, _spawnTransform.position + _spawnTransform.forward * range,
                lineColor);
        }

        // Use this to grab the canvas in the scene, if the trajectory requires a canvas (for overlays etc).
        public virtual Canvas GetCanvasInScene()
        {
            var canvas = FindObjectOfType<Canvas>();

            if (canvas != null)
                return canvas;

            Debug.LogWarning("No Canvas found in scene. We will add one for you. If this is not your " +
                             "intent, please add a canvas to the scene.");

            // Create a canvas
            var canvasGameObject = new GameObject("Canvas");
            canvas = canvasGameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGameObject.AddComponent<CanvasScaler>();
            canvasGameObject.AddComponent<GraphicRaycaster>();

            return canvas;
        }
    }
}
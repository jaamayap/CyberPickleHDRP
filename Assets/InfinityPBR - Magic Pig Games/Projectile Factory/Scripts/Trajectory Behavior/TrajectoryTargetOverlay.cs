using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Target Overlay Trajectory",
        menuName = "Projectile Factory/Trajectory Behavior/Target Overlay")]
    [Serializable]
    public class TrajectoryTargetOverlay : TrajectoryBehavior
    {
        [Header("Target Overlay Settings")]
        [Tooltip("The sprite to use for the target overlay")]
        [ShowInProjectileEditor("Target Overlay Sprite")]
        public Sprite targetSprite;

        [Tooltip("The scale of the target overlay")] [ShowInProjectileEditor("Target Overlay Scale")]
        public float scale = 1f;

        [Tooltip("The color of the target overlay")] [ShowInProjectileEditor("Target Overlay Color")]
        public Color spriteColor = Color.red;

        private Camera _camera;
        protected Canvas canvas;

        protected Image[] images = Array.Empty<Image>();

        protected GameObject[] targetOverlays = Array.Empty<GameObject>();

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Displays a target overlay on the screen " +
                                                      "where the projectile is aiming.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Target";
        public GameObject[] TargetOverlays => targetOverlays;
        public Image[] Images => images;
        public Camera Camera => _camera ? _camera : _camera = Camera.main;

        /// <summary>
        ///     Start showing the trajectory overlay
        /// </summary>
        /// <param name="projectileSpawner"></param>
        /// <param name="basicProjectilearam>
        public override void ShowTrajectoryStart()
        {
            if (!targetSprite)
            {
                Debug.LogError("No target overlay prefab set on TrajectoryTargetOverlay");
                return;
            }

            canvas = GetCanvasInScene();
            if (canvas == null)
            {
                Debug.LogError("No canvas is in the scene. One should have been created, so this error should " +
                               "not have occurred.");
                return;
            }

            CreateTargetOverlays();
        }

        // Here we will create the overlays. If showFromAllSpawnPoints is true, we will create one for each spawn point, 
        // otherwise we will create just one. Note that both the targetOverlays and images are arrays.
        protected virtual void CreateTargetOverlays()
        {
            if (!showFromAllSpawnPoints)
            {
                targetOverlays = new GameObject[1];
                images = new Image[1];
                CreateOverlay(0);
            }
            else
            {
                targetOverlays = new GameObject[ProjectileSpawner.spawnPoints.Count];
                images = new Image[ProjectileSpawner.spawnPoints.Count];
                for (var i = 0; i < ProjectileSpawner.spawnPoints.Count; i++)
                    CreateOverlay(i);
            }
        }

        // Create a single overlay at index i, and set up the image
        protected virtual GameObject CreateOverlay(int i)
        {
            targetOverlays[i] = new GameObject("Target Overlay");
            images[i] = targetOverlays[i].AddComponent<Image>();
            images[i].sprite = targetSprite;
            images[i].color = spriteColor;
            images[i].rectTransform.localScale = Vector3.one * scale;
            images[i].rectTransform.SetParent(canvas.transform);
            return targetOverlays[i];
        }

        /// <summary>
        ///     Stop the trajectory overlays
        /// </summary>
        /// <param name="projectileSpawner"></param>
        /// <param name="basicProjectilearam>
        public override void ShowTrajectoryStop()
        {
            DestroyOverlays();
        }

        // Here we pass both the projector and the projectile, as the projectile may not be live, so it won't have
        // a reference to the projector. Depending on your behavior, you may not need to use the projector. When 
        // a Projectile is live in the scene, it will call this with it's own Projector reference.
        public override void Tick()
        {
            if (ProjectileSpawner.Target == null) return;

            for (var i = 0; i < ProjectileSpawner.spawnPoints.Count; i++)
                UpdateOverlayPosition(i);
        }

        // Updates the position of a single overlay. The logic here is simple, we just get the screen point of the
        // target, and put the overlay there.
        protected virtual void UpdateOverlayPosition(int i)
        {
            if (ProjectileSpawner.Target == null) return;

            var screenPoint = Camera.WorldToScreenPoint(ProjectileSpawner.Target.transform.position);
            targetOverlays[i].transform.position = screenPoint;
        }

        // Stop showing the trajectory overlay when the projectile is launched.
        public override void LaunchProjectile(Projectile projectile)
        {
            ShowTrajectoryStop();
        }

        // Make sure we destroy the overlays when we return to the pool
        public override void OnReturnToPool(Projectile projectile)
        {
            DestroyOverlays();
        }

        // Destroy all of the overlays in targetOverlays and images
        protected virtual void DestroyOverlays()
        {
            if (targetOverlays.Length == 0) return;

            foreach (var overlay in targetOverlays)
                Destroy(overlay);

            foreach (var image in images)
            {
                if (image == null)
                    continue;
                Destroy(image);
            }

            targetOverlays = Array.Empty<GameObject>();
            images = Array.Empty<Image>();
        }
    }
}
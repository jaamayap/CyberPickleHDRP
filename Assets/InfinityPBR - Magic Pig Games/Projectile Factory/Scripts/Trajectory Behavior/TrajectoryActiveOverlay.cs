using System;
using UnityEngine;
using UnityEngine.UI;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Active Overlay",
        menuName = "Projectile Factory/Trajectory Behavior/Active Overlay")]
    [Serializable]
    public class TrajectoryActiveOverlay : TrajectoryBehavior
    {
        [Header("Target Overlay Settings")]
        [Tooltip("The sprite to use for the target overlay")]
        [ShowInProjectileEditor("Target Overlay Sprite")]
        public Sprite targetSprite;

        [Header("Target Options")]
        [Tooltip(
            "When true, the overlay will fallback to the spawner's target position if the projectile target is null.")]
        [ShowInProjectileEditor("Fallback To Spawner Target Position")]
        public bool fallbackToSpawnerTargetPosition;

        [Header("Options")]
        [Tooltip(
            "When true, the scale, color and opacity will change based on the distance to the target. Otherwise the " +
            "\"Close\" versions of each will be used")]
        [ShowInProjectileEditor("Scale Values With Distance")]
        public bool scaleValuesWithDistance = true;

        [Tooltip("The maximum distance for transitions between scale, color and opacity.")]
        [ShowInProjectileEditor("Far Distance")]
        public float farDistance = 10f;

        [Tooltip("The minimum distance for transitions between scale, color and opacity.")]
        [ShowInProjectileEditor("Close Distance")]
        public float closeDistance = 1f;

        [Tooltip("The maximum scale for the overlay at the furthest distance.")] [ShowInProjectileEditor("Far Scale")]
        public float farScale = 2f;

        [Tooltip("The minimum scale for the overlay at the closest distance.")] [ShowInProjectileEditor("Close Scale")]
        public float closeScale = 1f;

        [Tooltip("The maximum opacity for the overlay at the furthest distance.")]
        [ShowInProjectileEditor("Far Opacity")]
        public float farOpacity;

        [Tooltip("The minimum opacity for the overlay at the closest distance.")]
        [ShowInProjectileEditor("Close Opacity")]
        public float closeOpacity = 1f;

        [Tooltip("The maximum color for the overlay at the furthest distance.")] [ShowInProjectileEditor("Far Color")]
        public Color farColor = Color.white;

        [Tooltip("The minimum color for the overlay at the closest distance.")] [ShowInProjectileEditor("Close Color")]
        public Color closeColor = Color.red;

        private Camera _camera;

        private Canvas _canvas;
        protected Image _image;

        protected float _lastTimeSinceStart = -1;
        protected float _t;

        private Transform _targetTransform;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Displays a target overlay on the screen " +
                                                      "where the projectile is aiming, and changes color, scale and " +
                                                      "opacity based on distance.";

        //// Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Target";
        public GameObject TargetOverlay { get; private set; }

        public virtual float FarDistance => farDistance;
        public virtual float CloseDistance => closeDistance;
        public virtual Camera Camera => _camera ? _camera : _camera = Camera.main;

        public virtual float T
        {
            get
            {
                if (Math.Abs(_lastTimeSinceStart - Time.time) < 0.001f)
                    return _t;

                _lastTimeSinceStart = Time.time;

                _t = Mathf.InverseLerp(FarDistance, CloseDistance, DistanceToTarget());
                return _t;
            }
        }

        public virtual Color SpriteColor => !scaleValuesWithDistance ? closeColor : Color.Lerp(farColor, closeColor, T);

        public virtual float SpriteOpacity
        {
            get
            {
                if (Projectile != null && Projectile.hideTarget)
                    return 0;
                return !scaleValuesWithDistance ? closeOpacity : Mathf.Lerp(farOpacity, closeOpacity, T);
            }
        }

        public virtual float SpriteScale => !scaleValuesWithDistance ? closeScale : Mathf.Lerp(farScale, closeScale, T);
        public virtual Color ColorWithOpacity => new(SpriteColor.r, SpriteColor.g, SpriteColor.b, SpriteOpacity);

        protected GameObject TargetObject
            => Projectile.Target == null && fallbackToSpawnerTargetPosition
                ? ProjectileSpawner.Target
                : Projectile.Target;

        public virtual float DistanceToTarget()
        {
            if (_targetTransform == null) return -1f;
            if (Projectile == null && ProjectileSpawner == null) return -1f;
            return Vector3.Distance(_targetTransform.position, Projectile == null
                ? ProjectileSpawner.transform.position
                : Projectile.transform.position);
        }

        //public override void LaunchProjectile(Projectile basicProjectile) 
        //    => ShowTrajectoryStart(basicProjectile.ProjectileSpawner, basicProjectile);

        public override void ShowTrajectoryStart()
        {
            if (!targetSprite)
                return;

            // If the image is already populated, then we don't need to do this again -- it has already been done.
            if (_image != null)
                return;

            _canvas = GetCanvasInScene();
            if (_canvas == null)
            {
                Debug.LogError("No canvas is in the scene. One should have been created, so this error should " +
                               "not have occurred.");
                return;
            }

            // Spawn a UI Image on the main canvas with targetSprite as the sprite and lineColor as the color
            TargetOverlay = new GameObject("Target Overlay");
            _image = TargetOverlay.AddComponent<Image>();
            _image.rectTransform.SetParent(_canvas.transform);
        }

        private void SetSpriteValues()
        {
            _image.sprite = targetSprite;
            _image.color = ColorWithOpacity;
            _image.rectTransform.localScale = Vector3.one * SpriteScale;
        }

        public override Canvas GetCanvasInScene()
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

        public override void ShowTrajectoryStop()
        {
            if (!TargetOverlay) return;

            // Destroy the UI Image
            Destroy(TargetOverlay);
        }

        // Here we pass both the projector and the projectile, as the projectile may not be live, so it won't have
        // a reference to the projector. Depending on your behavior, you may not need to use the projector. When 
        // a Projectile is live in the scene, it will call this with it's own Projector reference.
        public override void LateTick()
        {
            //SetProjectileSpawner(projectileSpawner);
            //SetProjectile(projectile);

            // If we haven't created the overlay yet, create it now
            if (!TargetOverlay)
                ShowTrajectoryStart();
            if (!TargetOverlay) return; // Should not happen, but just in case

            SetTargetTransform();
            if (_targetTransform == null) return; // Should not happen, but just in case

            // position the overlay on the projector.Target
            var screenPoint = GetScreenPoint();
            TargetOverlay.transform.position = screenPoint;

            // We only do this on Tick() if the scaleValuesWithDistance is true, otherwise we set the values in
            // Launch()
            if (scaleValuesWithDistance)
                SetSpriteValues();
        }

        protected virtual void SetTargetTransform()
        {
            _targetTransform = TargetObject == null ? default : TargetObject.transform;
        }

        protected virtual Vector3 GetScreenPoint()
        {
            return Camera.WorldToScreenPoint(TargetObject.transform.position);
        }

        public override void DoDestroy(Projectile projectile)
        {
            ShowTrajectoryStop();
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            if (TargetOverlay != null)
                Destroy(TargetOverlay);
        }
    }
}
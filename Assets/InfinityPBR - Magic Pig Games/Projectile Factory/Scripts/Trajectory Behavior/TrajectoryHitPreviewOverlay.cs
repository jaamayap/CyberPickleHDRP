using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Hit Preview Overlay Trajectory",
        menuName = "Projectile Factory/Trajectory Behavior/Hit Preview Overlay")]
    [Serializable]
    public class TrajectoryHitPreviewOverlay : TrajectoryTargetOverlay
    {
        [Header("Hit Preview Settings")]
        [Tooltip("When true, the color will change if we are over a valid target")]
        [ShowInProjectileEditor("Change Color on Target")]
        public bool changeColorOnTarget = true;

        [Tooltip("The color of the target overlay")] [ShowInProjectileEditor("Target Overlay Color")]
        public Color spriteColorTarget;

        [Tooltip("This is the layer that active targets are on -- it is used to determine the color of the preview " +
                 "in situations where you want that to be different if a target is in sight.")]
        [ShowInProjectileEditor("Target Mask")]
        public LayerMask targetMask;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Displays a target overlay on the screen " +
                                                      "where the projectile is aiming, and changes color based on target mask.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Target";

        public override void ShowTrajectoryStart()
        {
            _spawnTransform = ProjectileSpawner.SpawnTransform;
            _collisionMask = ProjectileSpawner.Projectile.overrideCollisionMask
                ? ProjectileSpawner.Projectile.CollisionMask
                : ProjectileSpawner.CollisionMask;

            base.ShowTrajectoryStart();
        }

        public override void Tick()
        {
            CreateOverlaysIfTheyDontExist();
            PositionOverlays();
        }

        protected virtual void PositionOverlays()
        {
            var totalHits = 0;
            if (!showFromAllSpawnPoints)
            {
                // If we don't hit (single ray) then stop the trajectory
                if (PositionOverlaySingle())
                    totalHits += 1;
            }
            else
            {
                for (var i = 0; i < ProjectileSpawner.spawnPoints.Count; i++)
                    if (PositionOverlay(i))
                        totalHits += 1;
            }

            // If we didn't hit anything, stop the trajectory
            if (totalHits == 0)
                ShowTrajectoryStop();
        }

        protected virtual bool PositionOverlaySingle()
        {
            if (ProjectileSpawner.TryGetRaycastHit(out var hit, ProjectileSpawner.SpawnPosition, hitPreviewRange))
            {
                var screenPoint = Camera.WorldToScreenPoint(hit.point);
                targetOverlays[0].transform.position = screenPoint;
            }

            SetColorOfImage(hit); // Force i = 0 since we only have one
            DrawDebugLine(hit);

            return hit.collider != null; // Return true if we hit something
        }

        protected virtual bool PositionOverlay(int i)
        {
            if (ProjectileSpawner.TryGetRaycastHit(out var hit, i, hitPreviewRange))
            {
                var screenPoint = Camera.WorldToScreenPoint(hit.point);
                targetOverlays[i].transform.position = screenPoint;
            }

            SetColorOfImage(hit, i);
            DrawDebugLine(hit);
            return hit.collider != null; // Return true if we hit something
        }


        protected void CreateOverlaysIfTheyDontExist()
        {
            if (showFromAllSpawnPoints && targetOverlays.Length == ProjectileSpawner.spawnPoints.Count)
                return;

            if (targetOverlays.Length == 1)
                return;

            ShowTrajectoryStart();
        }

        protected virtual void SetColorOfImage(RaycastHit hit = default, int i = 0)
        {
            if (!changeColorOnTarget) return;
            if (targetMask == -1) return;

            var hitTarget = false;
            if (hit.collider != null)
                // If hit object is on targetMask, set the color of the image to spriteColorTarget
                hitTarget = targetMask == (targetMask | (1 << hit.collider.gameObject.layer));


            // If hit object is on targetMask, set the color of the image to spriteColorTarget
            images[i].color = hitTarget ? spriteColorTarget : spriteColor;
        }
    }
}
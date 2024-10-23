using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

/*
 * Utilities! For Projectile Factory
 */

namespace MagicPigGames.ProjectileFactory
{
    public enum AngleModMode
    {
        UpDown,
        LeftRight,
        AllDirections,
        None
    }

    public static class ProjectileUtilities
    {
        // Method to set the layer of a GameObject and optionally its children
        public static void SetLayer(GameObject obj, int newLayer, bool includeChildren = true)
        {
            if (obj == null)
                return;

            obj.layer = newLayer;

            if (!includeChildren) return;
            
            foreach (Transform child in obj.transform)
            {
                if (child == null)
                    continue;
                SetLayer(child.gameObject, newLayer, true);
            }
        }
        
        public static bool TryGetRaycastHit(this ProjectileSpawner projectileSpawner, out RaycastHit hit,
            int spawnPointIndex = 0, float range = 20)
        {
            return Physics.Raycast(projectileSpawner.spawnPoints[spawnPointIndex].Position,
                projectileSpawner.SpawnForward,
                out hit,
                range,
                projectileSpawner.Projectile.CollisionMask);
        }

        public static bool TryGetRaycastHit(this ProjectileSpawner projectileSpawner, out RaycastHit hit,
            Vector3 spawnPosition, float range = 20)
        {
            var hitTest = Physics.Raycast(spawnPosition,
                projectileSpawner.SpawnForward,
                out hit,
                range,
                projectileSpawner.Projectile.CollisionMask);

            return Physics.Raycast(spawnPosition,
                projectileSpawner.SpawnForward,
                out hit,
                range,
                projectileSpawner.Projectile.CollisionMask);
        }

        // Will look for the ProjectilePowerDestroy component on the GameObject and set the projectile if one is found.
        public static bool ProjectilePowerDestroySetProjectile(this GameObject obj, Projectile basicProjectile)
        {
            var projectilePowerDestroy = obj.GetComponent<DestroyOrPoolObject>();
            if (projectilePowerDestroy == null)
                return false;

            projectilePowerDestroy.SetProjectile(basicProjectile);
            return true;
        }

        public static float DistanceFrom(this Vector3 position1, Vector3 position2)
        {
            return Vector3.Distance(position1, position2);
        }

        public static bool TryGetRaycastHit(this ProjectileSpawner projectileSpawner, float angle, out RaycastHit hit,
            float range = 20)
        {
            var direction = Quaternion.Euler(0, angle, 0) * projectileSpawner.SpawnForward;

            return Physics.Raycast(projectileSpawner.SpawnPosition,
                direction,
                out hit,
                range,
                projectileSpawner.CollisionMask);
        }

        public static bool TryGetSphereCastHit(this ProjectileSpawner projectileSpawner, out RaycastHit hit,
            float radius = 1, float range = 20)
        {
            return Physics.SphereCast(
                projectileSpawner.LastSpawnPosition, // Use last one, because it may have changed literally just now
                radius,
                projectileSpawner.SpawnForward,
                out hit,
                range,
                projectileSpawner.CollisionMask);
        }

        public static bool TryGetSphereCastHit(this ProjectileSpawner projectileSpawner, float angle,
            out RaycastHit hit, float radius = 1, float range = 20)
        {
            var direction = Quaternion.Euler(0, angle, 0) * projectileSpawner.SpawnForward;

            return Physics.SphereCast(
                projectileSpawner.LastSpawnPosition,
                radius,
                direction,
                out hit,
                range,
                projectileSpawner.Projectile.CollisionMask);
        }

        public static List<float> Shuffle(this List<float> list)
        {
            var random = new System.Random();
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = random.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }

            return list;
        }

        // Calculates the impulse force of an object, to get it moving at the desired speed
        public static float ImpulseForce(float mass = 1, float speed = 20)
        {
            return mass * speed;
        }

        /// <summary>
        ///     Applies an impulse force to the Rigidbody, in the specified direction, with a random inaccuracy offset,
        ///     which will change where the projectile ends up landing.
        /// </summary>
        /// <param name="rb">The Rigidbody to apply the force to.</param>
        /// <param name="launchAngle">The angle at which to launch the projectile (in radians).</param>
        /// <param name="speed">The speed of the projectile.</param>
        /// <param name="inaccuracy">The amount of inaccuracy to apply to the launch angle.</param>
        public static void ApplyImpulseForce(this Rigidbody rb, float speed = 10, float inaccuracy = 0,
            float launchAngle = 0)
        {
            var force = ImpulseForce(rb.mass, speed);

            // Calculate the direction based on the launch angle
            //var launchDirection = Quaternion.AngleAxis(Mathf.Rad2Deg * launchAngle, rb.transform.right) * rb.transform.forward;
            var launchDirection = Quaternion.AngleAxis(-Mathf.Rad2Deg * launchAngle, rb.transform.right) *
                                  rb.transform.forward;


            // Create a random inaccuracy offset within the accuracy range
            var inaccuracyOffset = new Vector3(Random.Range(-inaccuracy, inaccuracy),
                Random.Range(-inaccuracy, inaccuracy), Random.Range(-inaccuracy, inaccuracy));

            // Apply force in the inaccurate direction
            rb.AddForce((launchDirection + inaccuracyOffset).normalized * force, ForceMode.Impulse);
        }

        // Makes the Rigidbody face in the direction of movement
        public static void FaceDirectionOfMovement(this Rigidbody rb, float rotationSpeed = 10.0f)
        {
            // If the Rigidbody is not moving, don't attempt to rotate it.
            // Vector3.magnitude is expensive, so we use Vector3.sqrMagnitude for comparison with zero.
            if (rb.velocity.sqrMagnitude == 0)
                return;

            // Calculate the rotation for the object to face the direction of movement
            var directionRotation = Quaternion.LookRotation(rb.velocity.normalized);

            // Smoothly rotate the object to the target rotation
            rb.rotation = Quaternion.Slerp(rb.rotation, directionRotation, rotationSpeed * Time.deltaTime);
            Debug.Log($"rb.rotation is {rb.rotation}");
        }

        public static float CalculateLaunchAngle(Vector3 startPosition,
            Vector3 targetPosition, float speed, float defaultAngle = -15f,
            bool useLowerAngle = true)
        {
            var direction = (targetPosition - startPosition).normalized;
            var gravity = Mathf.Abs(Physics.gravity.y);

            // Calculate the horizontal distance and vertical difference between the start and target positions
            var horizontalDistance = Vector3.Distance(new Vector3(startPosition.x, 0, startPosition.z),
                new Vector3(targetPosition.x, 0, targetPosition.z));
            var verticalDifference = targetPosition.y - startPosition.y;

            // Calculate the square of the speed
            var speedSquared = Mathf.Pow(speed, 2);

            // Calculate the term under the square root in the launch angle formula
            var underRoot = speedSquared * speedSquared - gravity *
                (gravity * horizontalDistance * horizontalDistance + 2 * verticalDifference * speedSquared);

            // Check if the target is reachable
            if (underRoot < 0) return defaultAngle;

            // Calculate the two possible launch angles
            var angle1 = Mathf.Atan((speedSquared + Mathf.Sqrt(underRoot)) / (gravity * horizontalDistance));
            var angle2 = Mathf.Atan((speedSquared - Mathf.Sqrt(underRoot)) / (gravity * horizontalDistance));

            // Return the desired angle in degrees
            var lowerAngle = Mathf.Min(angle1, angle2) * Mathf.Rad2Deg;
            var upperAngle = Mathf.Max(angle1, angle2) * Mathf.Rad2Deg;
            return useLowerAngle ? -lowerAngle : -upperAngle;
        }


        /// <summary>
        ///     Computes the direction to hit a target based on the initial position, initial direction, target position,
        ///     initial speed, and angle mod mode. Note: It will return a direction that has speed included in it, you
        ///     do not need to multiply this by speed.
        /// </summary>
        /// <param name="initialPosition">The initial position of the shooter.</param>
        /// <param name="initialDirection">The initial direction of the shooter.</param>
        /// <param name="targetPosition">The position of the target.</param>
        /// <param name="initialSpeed">The initial speed of the projectile.</param>
        /// <param name="angleModMode">The mode used to adjust the angle of the projectile.</param>
        /// <returns>The direction in which to hit the target.</returns>
        /// <exception cref="ArgumentException">Thrown when an invalid angleModMode is provided.</exception>
        public static Vector3 ComputeDirectionToHitTarget(Vector3 initialPosition, Vector3 initialDirection,
            Vector3 targetPosition, float initialSpeed, AngleModMode angleModMode)
        {
            // calculate the relative position of the target with respect to the initial direction
            var directionToTarget = (targetPosition - initialPosition).normalized;

            // calculate the change in direction needed to aim at the target
            var deltaDirection = directionToTarget - initialDirection;

            // dependent on directionMode, calculate the new direction
            var newDirection = angleModMode switch
            {
                AngleModMode.UpDown => //adjust up/down angle
                    initialDirection + new Vector3(0, deltaDirection.y, 0),
                AngleModMode.LeftRight => //adjust left/right angle
                    initialDirection + new Vector3(deltaDirection.x, 0, deltaDirection.z),
                AngleModMode.AllDirections => //adjust both directions
                    directionToTarget,
                AngleModMode.None => //No adjustment
                    initialDirection,
                _ => initialDirection //default to no adjustment
            };

            // scale the new direction by the speed to give the velocity
            return newDirection * initialSpeed;
        }


        public static Vector3 ComputeDirectionToHitTarget(Vector3 initialPosition, Vector3 initialDirection,
            Vector3 targetPosition, float initialSpeed,
            AngleModMode angleModMode,
            float maxModAngle,
            AngleModMode modifyAngleMode)
        {
            // Calculate the horizontal distance and height difference between the initial position and the target
            var horizontalDelta = new Vector3(targetPosition.x - initialPosition.x, 0,
                targetPosition.z - initialPosition.z);
            var horizontalDistance = horizontalDelta.magnitude;
            var heightDifference = targetPosition.y - initialPosition.y;

            // Calculate the angle required to hit the target with the given initial speed and gravity
            var g = Physics.gravity.magnitude;
            var angleRadians = Mathf.Atan((Mathf.Pow(initialSpeed, 2) + Mathf.Sqrt(Mathf.Pow(initialSpeed, 4) -
                                              g * (g * Mathf.Pow(horizontalDistance, 2) +
                                                   2 * heightDifference * Mathf.Pow(initialSpeed, 2)))) /
                                          (g * horizontalDistance));

            // Convert the angle to degrees and clamp it within the allowable range
            var angleDegrees = Mathf.Rad2Deg * angleRadians;
            Debug.Log($"Calculated angle (degrees): {angleDegrees}");

// Convert the angle to degrees and clamp it within the allowable range
            angleDegrees = Mathf.Clamp(angleDegrees, -maxModAngle, maxModAngle);
            Debug.Log($"Clamped angle (degrees): {angleDegrees}");

            // Calculate the initial velocity vector in the direction of the target, with the calculated angle
            var directionToTarget = horizontalDelta.normalized;
            var initialVelocity = Quaternion.AngleAxis(angleDegrees, Vector3.Cross(Vector3.up, directionToTarget)) *
                                  directionToTarget * initialSpeed;
            Debug.Log($"Initial velocity: {initialVelocity}");

            // Adjust the direction based on the angle modification mode
            Vector3 finalVelocity;
            switch (modifyAngleMode)
            {
                case AngleModMode.UpDown:
                    finalVelocity = new Vector3(initialVelocity.x,
                        Mathf.Clamp(initialVelocity.y, -maxModAngle, maxModAngle), initialVelocity.z);
                    break;
                case AngleModMode.LeftRight:
                    finalVelocity = new Vector3(Mathf.Clamp(initialVelocity.x, -maxModAngle, maxModAngle),
                        initialVelocity.y, Mathf.Clamp(initialVelocity.z, -maxModAngle, maxModAngle));
                    break;
                case AngleModMode.AllDirections:
                    finalVelocity = initialVelocity;
                    break;
                default:
                    finalVelocity = initialDirection * initialSpeed;
                    break;
            }

            Debug.Log($"Final velocity: {finalVelocity}");

            return finalVelocity;
        }

        /*
           public static Vector3 ComputeDirectionToHitTarget(Vector3 initialPosition, Vector3 initialDirection,
               Vector3 targetPosition, float initialSpeed,
               AngleModMode angleModMode,
               float maxModAngle,
               AngleModMode modifyAngleMode)
           {
               // calculate the relative position of the target with respect to the initial direction
               var directionToTarget = (targetPosition - initialPosition).normalized;

               // calculate the change in direction needed to aim at the target
               var deltaDirection = directionToTarget - initialDirection;

               // calculate total change in azimuth based on deltaDirection
               var totalChangeInAzimuth = Vector3.Angle(initialDirection, directionToTarget);

               // if totalChangeInAzimuth > maxModAngle accordingly with modifyAngleMode
               // then no modification is applied and initial direction is returned
               if (modifyAngleMode == AngleModMode.None ||
                   (modifyAngleMode == AngleModMode.AllDirections &&
                    totalChangeInAzimuth > maxModAngle) ||
                   (modifyAngleMode == AngleModMode.UpDown &&
                    Mathf.Abs(deltaDirection.y) * Mathf.Rad2Deg > maxModAngle) ||
                   (modifyAngleMode == AngleModMode.LeftRight &&
                    Vector3.Angle(new Vector3(deltaDirection.x, 0, deltaDirection.z), Vector3.forward) > maxModAngle))
               {
                   // Return initial direction if maxModAngle is exceeded
                   return initialDirection * initialSpeed;
               }

               // dependent on directionMode, calculate the new direction
               var newDirection = angleModMode switch
               {
                   AngleModMode.UpDown => //adjust up/down angle
                       initialDirection + new Vector3(0, deltaDirection.y, 0),
                   AngleModMode.LeftRight => //adjust left/right angle
                       initialDirection + new Vector3(deltaDirection.x, 0, deltaDirection.z),
                   AngleModMode.AllDirections => //adjust both directions
                       directionToTarget,
                   AngleModMode.None => //No adjustment
                       initialDirection,
               };

               // scale the new direction by the speed to give the velocity
               return newDirection * initialSpeed;
           }
           */
        public static Vector3 DirectionRequiredToHitTarget(Vector3 startPos, Vector3 targetPos, float initialSpeed)
        {
            var dir = targetPos - startPos; // get target direction
            var h = dir.y; // get height difference
            dir.y = 0; // retain only the horizontal direction
            var dist = dir.magnitude; // get horizontal distance

            if (dist == 0)
                // Target is directly above or below the start position.
                // Handle this case separately if needed.
                return Vector3.zero;

            var a = dir.normalized.x;
            var b = dir.normalized.z;
            var g = Physics.gravity.y; // get the gravity
            var s = initialSpeed;
            var s2 = s * s;

            // Calculate the initial angle
            var underRoot = s2 * s2 - g * (g * (dist * dist) + 2 * h * s2);

            if (underRoot < 0)
            {
                Debug.Log("Target out of range");
                // Target is out of range.
                // Handle this case separately if needed.
                return Vector3.zero;
            }

            var angle = Mathf.Atan((s2 - Mathf.Sqrt(underRoot)) / (g * dist));

            // Create a new vector with the calculated initial velocity, the calculated angle, and the direction
            var Vy = s * Mathf.Sin(angle);
            var Vx = s * Mathf.Cos(angle);

            var finalVelocity = new Vector3(a * Vx, Vy, b * Vx);

            return finalVelocity;
        }


        // A static method which returns a random Vector3 from between two Vector3s
        public static Vector3 RandomVector3(Vector3 min, Vector3 max)
        {
            return new Vector3(Random.Range(min.x, max.x)
                , Random.Range(min.y, max.y)
                , Random.Range(min.z, max.z));
        }

        public static Vector3 GetModifiedPosition(Vector3 sourcePosition, Vector3 targetPosition, Vector3 positionMod,
            bool faceSource = true)
        {
            if (!faceSource)
                return targetPosition + positionMod;

            // Calculate the direction from the source to the target
            var direction = (targetPosition - sourcePosition).normalized;

            // Rotate the positionMod vector to align with the direction
            var rotation = Quaternion.LookRotation(direction);
            var rotatedMod = rotation * positionMod;

            return targetPosition + rotatedMod;
        }

        public static bool IsPointerOverUIElementWithTag(string tag)
        {
            var eventData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            return results.Any(result => result.gameObject.CompareTag(tag));
        }

        public static string RemoveClone(this string str)
        {
            return str.Replace("(Clone)", "");
        }

        public static Vector3 GetWorldPosition(Transform transform, Camera targetCamera)
        {
            var mousePosition = Input.mousePosition;
            var position = transform.position;
            mousePosition.z = targetCamera.WorldToScreenPoint(position).z;
            var worldPosition = targetCamera.ScreenToWorldPoint(mousePosition);
            return worldPosition;
        }

        public static Vector3 ProjectVectorOnPlane(Vector3 planeVector3, Vector3 vector)
        {
            return vector - Vector3.Dot(vector, planeVector3) * planeVector3;
        }

        public static float SignedVectorAngle(Vector3 vectorA, Vector3 vectorB, Vector3 normal)
        {
            var perpVector = Vector3.Cross(normal, vectorA);
            return Vector3.Angle(vectorA, vectorB) * Mathf.Sign(Vector3.Dot(perpVector, vectorB));
        }
    }
}

/*
public static Vector3 ComputeDirectionToHitTarget(Vector3 initialPosition, Vector3 initialDirection,
       Vector3 targetPosition, float initialSpeed,
       AngleModMode angleModMode,
       float maxModAngle,
       AngleModMode modifyAngleMode)
   {
       // calculate the relative position of the target with respect to the initial direction
       var directionToTarget = (targetPosition - initialPosition).normalized;

       // calculate the change in direction needed to aim at the target
       var deltaDirection = directionToTarget - initialDirection;

       // calculate total change in azimuth based on deltaDirection
       var totalChangeInAzimuth = Vector3.Angle(initialDirection, directionToTarget);

       // if totalChangeInAzimuth > maxModAngle accordingly with modifyAngleMode
       // then no modification is applied and initial direction is returned
       if ((modifyAngleMode == AngleModMode.AllDirections &&
            totalChangeInAzimuth > maxModAngle) ||
           (modifyAngleMode == AngleModMode.UpDown &&
            Mathf.Abs(deltaDirection.y) * Mathf.Rad2Deg > maxModAngle) ||
           (modifyAngleMode == AngleModMode.LeftRight &&
            Vector3.Angle(new Vector3(deltaDirection.x, 0, deltaDirection.z), Vector3.forward) > maxModAngle))
       {
           // Return initial direction if maxModAngle is exceeded
           return initialDirection * initialSpeed;
       }

       // dependent on directionMode, calculate the new direction
       var newDirection = angleModMode switch
       {
           AngleModMode.UpDown => //adjust up/down angle
               initialDirection + new Vector3(0, deltaDirection.y, 0),
           AngleModMode.LeftRight => //adjust left/right angle
               initialDirection + new Vector3(deltaDirection.x, 0, deltaDirection.z),
           AngleModMode.AllDirections => //adjust both directions
               directionToTarget,
           AngleModMode.None => //No adjustment
               initialDirection,
       };

       // scale the new direction by the speed to give the velocity
       return newDirection * initialSpeed;
   }
   */
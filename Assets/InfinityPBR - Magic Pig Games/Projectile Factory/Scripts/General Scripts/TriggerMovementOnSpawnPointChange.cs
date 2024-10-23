using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This will check the current spawn point, and if it changes to THIS one, then we will fire " +
                   "our movement actions. The spawn point should only change right before the projectile is fired.")]
    [Serializable]
    public class TriggerMovementOnSpawnPointChange : MonoBehaviour
    {
        public ProjectileSpawner spawner;
        public Transform thisSpawnPosition;

        [Space] public Vector3 targetPosition;

        public float speed = 5f;
        public float returnSpeed = 2f;

        private Transform _activeSpawnPoint;
        private bool _isFiring;
        private bool _isReturning;

        private Vector3 _originalPosition;

        private void Start()
        {
            _originalPosition = transform.localPosition; // Cache our original position
            _activeSpawnPoint = spawner.SpawnTransform; // Cache the current spawn point
        }

        private void Update()
        {
            CheckForSpawnPointChange();
            DoMovement();
        }

        // If the active point hasn't changed, return. Otherwise, set the active point and if it is thisSpawnPoint
        // then we will Fire() our action.
        private void CheckForSpawnPointChange()
        {
            if (_activeSpawnPoint == spawner.SpawnTransform)
                return;

            _activeSpawnPoint = spawner.SpawnTransform;
            if (spawner.SpawnTransform == thisSpawnPosition)
                Fire();
        }

        // Handles the motion
        private void DoMovement()
        {
            if (_isFiring)
            {
                Move(targetPosition, speed, ref _isFiring);
                return;
            }

            if (_isReturning)
                Move(_originalPosition, returnSpeed, ref _isReturning);
        }

        // Fire the action
        public void Fire()
        {
            if (!_isFiring && !_isReturning)
                _isFiring = true;
            else if (!_isReturning) // Allow firing if it's not currently returning
                _isFiring = true;
        }

        // Moves the object
        private void Move(Vector3 targetPos, float moveSpeed, ref bool flag)
        {
            transform.localPosition =
                Vector3.MoveTowards(transform.localPosition, targetPos, moveSpeed * Time.deltaTime);
            if (transform.localPosition != targetPos) return;

            flag = false;
            if (targetPos != targetPosition) return;
            _isReturning = true;
        }
    }
}
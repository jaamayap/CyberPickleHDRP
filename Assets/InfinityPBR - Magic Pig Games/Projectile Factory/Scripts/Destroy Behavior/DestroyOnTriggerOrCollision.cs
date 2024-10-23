using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Destroy or Pool on Trigger or Collision",
        menuName = "Projectile Factory/Destroy Behavior/Destroy or Pool on Trigger or Collision")]
    [Serializable]
    public class DestroyOnTriggerOrCollision : DestroyBehavior
    {
        [Min(1)] [ShowInProjectileEditor("Max Hits")]
        public int maxHits = 1;

        [ShowInProjectileEditor("Destroy Delay")]
        public float destroyDelay;

        public int hitsPerFrame = 1;

        [Header("Fallback Destroy")] [ShowInProjectileEditor("Destroy If No Collision")]
        public bool destroyIfNoCollision;

        [ShowInProjectileEditor("Wait Timer For Collision")]
        public float waitTimerForCollision = 5f;

        private bool _hasBeenDestroyed;


        private int _hitCount;
        private int _hitsPerFrameCount;
        private bool _skipHitCount;

        private float _waitTimerForCollision;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Destroys the projectile after a certain number of hits.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Destroy";

        protected virtual void Reset()
        {
            _hitCount = 0;
            _waitTimerForCollision = 0f;
            _skipHitCount = false;
            _hasBeenDestroyed = false;
        }

        protected virtual void OnEnable()
        {
            Reset();
        }

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            DoDestroy(Projectile);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            _hitCount++;
            if (_hitCount < maxHits && !_skipHitCount)
                return;
            if (_hitsPerFrameCount >= hitsPerFrame)
                return;

            _hitsPerFrameCount += 1;
            DoDestroy(Projectile);
        }

        public override void Tick()
        {
            _hitsPerFrameCount = 0;
            if (!destroyIfNoCollision)
                return;

            _waitTimerForCollision += Time.deltaTime;

            if (_waitTimerForCollision > waitTimerForCollision)
            {
                _skipHitCount = true; // Set this so that we ignore hit counts for a force destroy
                Projectile.TriggerDestroy();
            }
        }

        public override void OnReturnToPool(Projectile projectile)
        {
            Reset();
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            Reset();
        }

        public override void DoDestroy(Projectile projectile)
        {
            if (_hasBeenDestroyed) return;
            _hasBeenDestroyed = true;

            RemoveObject(destroyDelay);
        }
    }
}
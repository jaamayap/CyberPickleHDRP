using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Play Audio Clip On Spawner",
        menuName = "Projectile Factory/Generic Behavior/Play Audio Clip On Spawner")]
    [Serializable]
    public class PlayAudioClipOnSpawner : ProjectileBehavior
    {
        [Header("Launch / Stop")] public AudioClip audioClipOnLaunch;

        public bool launchClipLoop;
        public AudioClip audioClipOnStop;
        public bool stopClipLoop;
        public bool stopLaunchClipOnStop = true;

        [Header("Collision / Trigger")] public AudioClip audioClipOnCollision;

        public bool collisionClipLoop;

        [Header("Destroy")] public AudioClip audioClipOnDestroy;

        public bool destroyClipLoop;

        [Header("Enable / Disable")] public AudioClip audioClipOnEnable;

        public bool enableClipLoop;
        public AudioClip audioClipOnDisable;
        public bool disableClipLoop;

        private AudioSource _audioSource;

        public AudioSource AudioSource => GetAudioSource();

        private AudioSource GetAudioSource()
        {
            if (_audioSource != null)
                return _audioSource;

            _audioSource = Projectile.ProjectileSpawner.GetComponent<AudioSource>();

            if (_audioSource == null)
                _audioSource = Projectile.ProjectileSpawner.gameObject.AddComponent<AudioSource>();

            return _audioSource;
        }

        public override void LaunchProjectile(Projectile projectile)
        {
            if (audioClipOnLaunch != null)
                PlayClip(audioClipOnLaunch, launchClipLoop);
        }

        protected override void ProjectileStopped()
        {
            if (stopLaunchClipOnStop && audioClipOnLaunch != null)
                AudioSource.Stop();
            if (audioClipOnStop != null)
                PlayClip(audioClipOnStop, stopClipLoop);
        }

        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (audioClipOnCollision != null)
                PlayClip(audioClipOnCollision, collisionClipLoop);
        }

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (audioClipOnCollision != null)
                PlayClip(audioClipOnCollision, collisionClipLoop);
        }

        public override void DoDestroy(Projectile projectile)
        {
            if (audioClipOnDestroy != null)
                PlayClip(audioClipOnDestroy, destroyClipLoop);
        }

        public override void Disable(Projectile projectile)
        {
            if (audioClipOnDisable != null)
                PlayClip(audioClipOnDisable, disableClipLoop);
        }

        public override void Enable(Projectile projectile)
        {
            if (audioClipOnEnable != null)
                PlayClip(audioClipOnEnable, enableClipLoop);
        }

        protected virtual void PlayClip(AudioClip clip, bool loop = false)
        {
            AudioSource.loop = loop;
            AudioSource.clip = clip;
            AudioSource.Play();
        }

        public override void OnReturnToPool(Projectile projectile)
        {
        }

        public override void OnGetFromPool(Projectile projectile)
        {
        }
    }
}
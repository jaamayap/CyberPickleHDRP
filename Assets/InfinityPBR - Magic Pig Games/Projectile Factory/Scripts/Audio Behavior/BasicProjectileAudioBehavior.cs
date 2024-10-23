using System;
using UnityEngine;

/*
 * NOTE: While this behavior exists, it's probably a lot easier to just use AudioSource components on muzzle objects
 * and explosions, and the projectile itself (for looped audio). This is one way to do audio, but keeping it simple
 * is often the best way to go.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Basic Audio Behavior",
        menuName = "Projectile Factory/Audio Behavior/Basic Audio Behavior")]
    [Serializable]
    public class BasicProjectileAudioBehavior : ProjectileAudioBehavior
    {
        [Header("Audio Clips")] [ShowInProjectileEditor("onTriggerOrColliderClip")]
        public AudioClip onTriggerOrColliderClip;

        [ShowInProjectileEditor("onLaunchClip")]
        public AudioClip onLaunchClip;

        [Header("Audio Source")] public AudioSource audioSource;

        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription => "Plays audio clips on launch and on trigger or collision.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Audio";

        public override void LaunchProjectile(Projectile projectile)
        {
            PlayClip(onLaunchClip);
        }

        public override void TriggerEnter(Projectile projectile, Collider other, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            PlayOnTriggerOrColliderClip();
        }

        public override void CollisionEnter(Projectile projectile, Collision other, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            PlayOnTriggerOrColliderClip();
        }

        public void PlayOnTriggerOrColliderClip()
        {
            PlayClip(onTriggerOrColliderClip);
        }

        protected virtual void PlayClip(AudioClip clip)
        {
            CreateAudioSource();
            if (audioSource == null) return;
            if (clip == null) return;

            audioSource.PlayOneShot(clip);
        }

        private void CreateAudioSource()
        {
            if (audioSource != null) return;
            audioSource = Projectile.GetComponent<AudioSource>();
            if (audioSource != null) return;

            audioSource = Projectile.gameObject.AddComponent<AudioSource>();
        }
    }
}
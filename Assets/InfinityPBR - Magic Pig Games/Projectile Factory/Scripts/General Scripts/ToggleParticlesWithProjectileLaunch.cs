using System;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory
{
    [Documentation("This script toggles particles on/off when the projectile is launched.")]
    [Serializable]
    public class ToggleParticlesWithProjectileLaunch : MonoBehaviour
    {
        public Projectile projectile;
        private bool _isPlaying;

        private ParticleSystem[] _particleSystems;

        protected virtual void Update()
        {
            if (!_isPlaying && projectile.Launched)
                PlayParticles();
            else if (_isPlaying && !projectile.Launched)
                StopParticles();
        }

        protected virtual void OnEnable()
        {
            // Add all particles in this and children to _particleSystems
            _particleSystems = GetComponentsInChildren<ParticleSystem>();

            if (projectile.Launched == false)
                StopParticles(true);
            else
                PlayParticles();
        }

        protected void OnDisable()
        {
            StopParticles();
        }

        private void PlayParticles()
        {
            _isPlaying = true;

            foreach (var particle in _particleSystems)
                particle.Play();
        }

        private void StopParticles(bool clearAll = false)
        {
            _isPlaying = false;

            if (_particleSystems == null)
                return;

            foreach (var particle in _particleSystems)
            {
                particle.Stop();
                if (clearAll)
                    particle.Clear();
            }
        }
    }
}
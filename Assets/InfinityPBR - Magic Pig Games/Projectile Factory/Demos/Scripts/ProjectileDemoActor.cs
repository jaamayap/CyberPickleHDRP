using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MagicPigGames.ProjectileFactory.Demo
{
    [Documentation("This is a simple demo actor to demonstrate how we can use a ProjectileObserver to pass " +
                   "information to an actor class. Your class will be unique, but this pattern should work for most any " +
                   "game, really. Rather than actually doing damage, we'll just put a Debug.Log out to the console to " +
                   "show that the Observer is working, and call developers attention to this script.",
        "https://infinitypbr.gitbook.io/infinity-pbr/projectile-factory/overview-and-quickstart")]
    [Serializable]
    public class ProjectileDemoActor : MonoBehaviour
    {
        public delegate void ProjectileHandlerNoVariables();

        [Header("Demo Stats")] public float minDamage = 3;

        public float maxDamage = 5;

        [Header("Options")] public AudioClip gotHitClip;

        [Range(0.05f, 0.3f)] public float clipPlayGap = 0.05f;

        private AudioSource _audioSource;

        private float _clipPlayTimer;

        public AudioSource AudioSource => GetAudioSource();
        public int ProjectilesLaunched { get; private set; }

        private float Damage => Random.Range(minDamage, maxDamage); // Simple random damage calculation

        // The ProjectileObserver will call this method when it registers that one of our projectiles has hit
        // another ProjectileDemoActor. We will know who it is and, in more complex games, determine whether we should
        // apply damage, what amount of damage, and any other effects, score keeping, and so on.
        public virtual void RegisterHit(Projectile projectile, ProjectileDemoActor actor)
        {
            // Note that we have access to the projectileData, another extensible class, which you can use to add
            // additional unique logic to your system.
            var projectileDamage = projectile.ProjectileData.Damage;
            var isDamageOverTime = projectile.ProjectileData is ProjectileDataDemoDamageOverTime;

            var actorBaseDamage = isDamageOverTime ? Damage * Time.deltaTime : Damage;
            var damage = actorBaseDamage + projectileDamage;
            Debug.Log($"<color=#00ff00>{name} successfully hit {actor.name} with {projectile.name} and will " +
                      $"give {damage} damage! ({projectileDamage} of the damage came from the projectilData class on " +
                      "the projectile.)</color>");
            actor.GotHit(this, projectile, damage);
        }

        public virtual event ProjectileHandlerNoVariables ActorGotHit;

        // This is where we would apply the damage, or any other effects, to the actor. This is called from another
        // ProjectileDemoActor, which has determined it should apply damage to this actor. We know who the attacker
        // is, so you could add additional logic that mitigates the damage, records who has been attacking this actor,
        // or other complex logic for your game.
        public virtual void GotHit(ProjectileDemoActor attackingActor, Projectile projectile, float damage)
        {
            Debug.Log($"<color=magenta>I, {name} got hit by {attackingActor.name} with a {projectile.name}, " +
                      $"and took {damage} damage!</color>");

            PlayClip(gotHitClip);
            InvokeActorGotHit();
            if (DemoScene.instance != null)
                DemoScene.instance.AddDamage(damage);
        }

        protected virtual void InvokeActorGotHit()
        {
            ActorGotHit?.Invoke();
        }

        protected virtual void PlayClip(AudioClip audioClip)
        {
            if (audioClip == null)
                return;

            if (_clipPlayTimer > Time.time)
                return;

            AudioSource.PlayOneShot(audioClip);
            _clipPlayTimer = Time.time + clipPlayGap;
        }

        protected virtual AudioSource GetAudioSource()
        {
            if (_audioSource != null)
                return _audioSource;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();

            return _audioSource;
        }

        public virtual void AddProjectileLaunched()
        {
            ProjectilesLaunched += 1;
        }
    }
}
using System;
using MagicPigGames.ProjectileFactory.Demo;
using UnityEngine;

/*
 * This class is a demonstration of how you can create custom Observers to do additional logic in your game.
 * In this case, we are creating an Observer that listens for when a projectile hits an actor and then applies damage
 * to that actor.
 *
 * We don't actually do any damage, we'll just put a Debug.Log out to the console to show that the Observer is working.
 */

namespace MagicPigGames.ProjectileFactory
{
    [CreateAssetMenu(fileName = "New Spawner Is Actor Damage Observer (DEMO)",
        menuName = "Projectile Factory/Observers/Demo/Spawner Is Actor Damage Observer")]
    [Serializable]
    public class SpawnerIsActorDamageObserverDemo : ProjectileObserver
    {
        // Internal description is used for the custom inspector to show a description of the behavior.
        public override string InternalDescription =>
            "[DEMO] This Observer is used to apply damage to an actor when a projectile hits it. This is a demo Observer.";

        // Internal icon is used for the custom inspector to show an icon of the behavior.
        public override string InternalIcon => "Eye";

        public ProjectileDemoActor Actor { get; set; }

        // In this demo, we will register the owner of the projectile OnLaunch, so that when the projectile hits another
        // actor, we can apply damage to that actor. And the other actor will know where the damage came from! Often
        // damage will be based on the stats of the attacking actor, so it's important to know who the attacker is.
        public override void LaunchProjectile(Projectile projectile)
        {
            var actor = ProjectileOwner.GetComponent<ProjectileDemoActor>();
            if (actor == null)
            {
                Debug.LogError("Projectile owner does not have a ProjectileDemoActor component!");
                return;
            }

            Actor = actor;
        }

        // This script will handle both CollisionEnter and TriggerEnter events, and will cache the actor we hit.
        public override void CollisionEnter(Projectile projectile, Collision collision, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (Actor == null)
                return;

            HitObject(objectHit);
        }

        public override void TriggerEnter(Projectile projectile, Collider collider, GameObject objectHit = null,
            Vector3 contactPoint = default)
        {
            if (Actor == null)
                return;

            HitObject(objectHit);
        }

        // This is where we ensure the object we hit was an actor, and then send the information to our actor, which
        // will handle the logic of actually doing the damage. This just says "Hey, we hit another actor!"
        protected virtual void HitObject(GameObject objectHit)
        {
            var actorHit = objectHit.GetComponent<ProjectileDemoActor>();
            if (actorHit == null)
                return;

            Actor.RegisterHit(Projectile, actorHit); // Send the hit information to the actor
        }
    }
}
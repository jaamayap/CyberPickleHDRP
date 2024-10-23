using System;
using System.Collections;
using UnityEngine;

namespace MagicPigGames.ProjectileFactory.Demo
{
    [RequireComponent(typeof(ProjectileDemoActor))]
    [Serializable]
    public class ProjectileDemoActorGotHitReaction : MonoBehaviour
    {
        public MeshRenderer meshRenderer;
        public Color color = Color.black;
        public float fadeTime = 0.5f;

        private Coroutine _fadeCoroutine;
        private Color _initialColor;
        public ProjectileDemoActor Actor { get; private set; }

        protected virtual void Awake()
        {
            _initialColor = meshRenderer.material.color;
            Actor = GetComponent<ProjectileDemoActor>();
        }

        protected virtual void OnEnable()
        {
            Subscribe();
        }

        protected void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            Actor.ActorGotHit += OnGotHit;
        }

        private void Unsubscribe()
        {
            Actor.ActorGotHit -= OnGotHit;
        }

        private void OnGotHit()
        {
            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeOutColor());
        }

        private IEnumerator FadeOutColor()
        {
            meshRenderer.material.color = color;
            var t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                meshRenderer.material.color = Color.Lerp(color, _initialColor, t / fadeTime);
                yield return null;
            }

            _fadeCoroutine = null;
        }
    }
}
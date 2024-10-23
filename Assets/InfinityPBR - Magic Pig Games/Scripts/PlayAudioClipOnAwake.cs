using System;
using InfinityPBR;
using UnityEngine;

// Play an audio clip on awake.

namespace MagicPigGames
{
    [Documentation("Play an audio clip on awake. This has the ability to select a random clip, which is " +
                   "why you might use this instead of just adding an AudioSource component to the object.")]
    [Serializable]
    public class PlayAudioClipOnAwake : MonoBehaviour
    {
        public AudioClip[] audioClip;
        public AudioSource audioSource;
        public AudioClip RandomClip => audioClip.TakeRandom();

        private void Start()
        {
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = RandomClip;
            audioSource.Play();
        }
    }
}
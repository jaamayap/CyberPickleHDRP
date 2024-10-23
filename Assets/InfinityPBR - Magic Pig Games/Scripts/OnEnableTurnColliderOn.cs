using System;
using System.Collections;
using UnityEngine;

// Turn the collider on when the object is enabled.

namespace MagicPigGames
{
    [Documentation("Turn the collider on when the object is enabled.")]
    [Serializable]
    public class OnEnableTurnColliderOn : MonoBehaviour
    {
        public Collider thisCollider;
        public float delay;
        public bool turnOffOnDisable = true;

        private void OnEnable()
        {
            if (thisCollider == null)
                thisCollider = GetComponent<Collider>();

            if (delay > 0)
                StartCoroutine(TurnColliderOn());
            else
                thisCollider.enabled = true;
        }

        private void OnDisable()
        {
            if (thisCollider != null && turnOffOnDisable)
                thisCollider.enabled = false;
        }

        private IEnumerator TurnColliderOn()
        {
            yield return new WaitForSeconds(delay);
            thisCollider.enabled = true;
        }
    }
}
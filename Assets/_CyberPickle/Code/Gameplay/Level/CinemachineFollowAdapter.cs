// File: Assets/_CyberPickle/Code/Gameplay/Level/CinemachineFollowAdapter.cs
// Namespace: CyberPickle.Gameplay.Level
//
// Purpose: Tiny adapter that exposes a SetFollowTarget(GameObject) method
// usable from a UnityEvent<GameObject>. Place this on the same GameObject
// as a CinemachineVirtualCamera, then wire GameSceneBootstrap's
// OnPlayerSpawned event to it in the inspector — no Cinemachine import
// needed inside the bootstrap itself.

using UnityEngine;
using Cinemachine;

namespace CyberPickle.Gameplay.Level
{
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    [DisallowMultipleComponent]
    public class CinemachineFollowAdapter : MonoBehaviour
    {
        [Tooltip("If true, also sets LookAt to the same target. Usually you want this on for top-down character cameras.")]
        [SerializeField] private bool alsoSetLookAt = true;

        private CinemachineVirtualCamera vcam;

        private void Awake()
        {
            vcam = GetComponent<CinemachineVirtualCamera>();
        }

        /// <summary>
        /// Inspector-wirable method. Call this from a UnityEvent&lt;GameObject&gt;
        /// to retarget the virtual camera at runtime — e.g., after the player spawns.
        /// </summary>
        public void SetFollowTarget(GameObject target)
        {
            if (target == null || vcam == null) return;

            vcam.Follow = target.transform;
            if (alsoSetLookAt)
            {
                vcam.LookAt = target.transform;
            }
        }
    }
}

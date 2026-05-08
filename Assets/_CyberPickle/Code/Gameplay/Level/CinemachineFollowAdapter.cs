// File: Assets/_CyberPickle/Code/Gameplay/Level/CinemachineFollowAdapter.cs
// Namespace: CyberPickle.Gameplay.Level
//
// Purpose: Tiny adapter that exposes a SetFollowTarget(GameObject) method
// usable from a UnityEvent<GameObject>. Place this on the same GameObject
// as a CinemachineCamera, then wire GameSceneBootstrap's OnPlayerSpawned
// event to it in the inspector — no Cinemachine import needed inside the
// bootstrap itself.
//
// Migrated from Cinemachine 2.x (CinemachineVirtualCamera, Cinemachine
// namespace) to Cinemachine 3.x (CinemachineCamera, Unity.Cinemachine
// namespace) on 2026-05-09 when Unity 6.4's package registry stopped
// shipping 2.x. The Follow / LookAt properties have identical semantics
// in both versions, so only the type name and namespace changed.
//
// Scene-side migration the user must do once per virtual camera:
//   1. The OLD CinemachineVirtualCamera component will appear as "missing
//      script" after the upgrade.
//   2. Remove it. Add Component → Cinemachine → Cinemachine Camera.
//   3. Re-add this CinemachineFollowAdapter on the same GameObject.
//   4. Re-wire GameSceneBootstrap.OnPlayerSpawned → SetFollowTarget(GameObject).
//   5. Set the camera's Body/Aim behaviors as before — Cinemachine 3.x
//      uses separate component-based behaviors (CinemachineFollow,
//      CinemachineRotationComposer, etc.) instead of the 2.x dropdown
//      menu inside the virtual camera.

using UnityEngine;
using Unity.Cinemachine;

namespace CyberPickle.Gameplay.Level
{
    [RequireComponent(typeof(CinemachineCamera))]
    [DisallowMultipleComponent]
    public class CinemachineFollowAdapter : MonoBehaviour
    {
        [Tooltip("If true, also sets LookAt to the same target. Usually you want this on for top-down character cameras.")]
        [SerializeField] private bool alsoSetLookAt = true;

        private CinemachineCamera vcam;

        private void Awake()
        {
            vcam = GetComponent<CinemachineCamera>();
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

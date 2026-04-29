// File: Assets/_CyberPickle/Code/Gameplay/Level/GameSceneBootstrap.cs
// Namespace: CyberPickle.Gameplay.Level
//
// Purpose: Drives Game scene initialization. On Start, reads the active
// profile via ProfileManager + CharacterManager, instantiates the selected
// character's prefab at the SpawnPoint Transform, and notifies listeners
// (e.g., a camera follow adapter) via UnityEvent + C# event.
//
// Future responsibilities (added as later milestones land):
//   - Apply the character's equipped loadout (weapons, armor, amulet, power-ups)
//   - Initialize the wave director / boss spawner
//   - Bind the gameplay HUD
//   - Register the player with the ECS bridge (PlayerData singleton entity)

using System;
using UnityEngine;
using UnityEngine.Events;
using CyberPickle.Core.Services.Authentication;
using CyberPickle.Characters;
using CyberPickle.Gameplay.Player;

namespace CyberPickle.Gameplay.Level
{
    /// <summary>
    /// Drop on a single empty GameObject in the Game scene. Runs once on Start().
    /// </summary>
    [DisallowMultipleComponent]
    public class GameSceneBootstrap : MonoBehaviour
    {
        [Header("Spawn")]
        [Tooltip("Transform whose position/rotation define where the character spawns. Falls back to this GameObject's transform if null.")]
        [SerializeField] private Transform spawnPoint;

        [Header("Direct-Play Fallback (development only)")]
        [Tooltip("If true and the active profile / selected character can't be resolved (e.g. you pressed Play directly in Game.unity), spawn the fallback prefab instead of erroring out.")]
        [SerializeField] private bool allowFallbackForDirectPlay = true;

        [Tooltip("Prefab spawned only when the fallback path is taken.")]
        [SerializeField] private GameObject fallbackCharacterPrefab;

        [Header("Events")]
        [Tooltip("Invoked once after the player is instantiated. Wire camera-follow / HUD-bind here in inspector.")]
        public UnityEvent<GameObject> OnPlayerSpawned;

        /// <summary>C# event for code-side subscribers (e.g., other systems instantiated at runtime).</summary>
        public event Action<GameObject> PlayerSpawned;

        /// <summary>Reference to the spawned player. Null until <see cref="Start"/> finishes.</summary>
        public GameObject SpawnedPlayer { get; private set; }

        private void Start()
        {
            SpawnSelectedCharacter();
        }

        private void SpawnSelectedCharacter()
        {
            GameObject prefabToSpawn = ResolveCharacterPrefab(out string source);

            if (prefabToSpawn == null)
            {
                Debug.LogError("[GameSceneBootstrap] Could not resolve a character prefab to spawn. " +
                               "Boot through Boot.unity and select a character, OR assign a 'Fallback Character Prefab' in the inspector for direct-play testing.");
                return;
            }

            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

            SpawnedPlayer = Instantiate(prefabToSpawn, pos, rot);
            SpawnedPlayer.name = $"Player ({prefabToSpawn.name})";

            // Character prefabs are shared between meta-game previews
            // (CharacterSelect, EquipmentHub) and gameplay. They ship with
            // gameplay components DISABLED so they stay visually inert in
            // those preview contexts. The Game scene activates them here.
            ActivateGameplayComponents(SpawnedPlayer);

            Debug.Log($"<color=cyan>[GameSceneBootstrap]</color> Spawned '{prefabToSpawn.name}' at {pos}. Source: {source}.");

            // Notify both inspector-wired and code-wired listeners.
            OnPlayerSpawned?.Invoke(SpawnedPlayer);
            PlayerSpawned?.Invoke(SpawnedPlayer);
        }

        /// <summary>
        /// Wakes up the gameplay-only components on the spawned player.
        /// Idempotent and tolerant of missing components — logs a warning
        /// per missing piece so misconfigured prefabs are easy to spot.
        /// </summary>
        private static void ActivateGameplayComponents(GameObject player)
        {
            // Physics: enable simulation.
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }
            else
            {
                Debug.LogWarning($"[GameSceneBootstrap] Spawned '{player.name}' has no Rigidbody — character will not be physically simulated.");
            }

            // Input: enable polling + action callbacks.
            var input = player.GetComponent<PlayerInput>();
            if (input != null)
            {
                input.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[GameSceneBootstrap] Spawned '{player.name}' has no PlayerInput — character will not respond to controls.");
            }

            // Motor: enable movement loop.
            var motor = player.GetComponent<PlayerMotor>();
            if (motor != null)
            {
                motor.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[GameSceneBootstrap] Spawned '{player.name}' has no PlayerMotor — character will not move.");
            }
        }

        private GameObject ResolveCharacterPrefab(out string source)
        {
            source = "unresolved";

            ProfileManager profileManager = ProfileManager.Instance;
            CharacterManager characterManager = CharacterManager.Instance;

            // Prefer the profile-driven path (the proper flow).
            if (profileManager != null && profileManager.ActiveProfile != null && characterManager != null)
            {
                string characterId = profileManager.ActiveProfile.LastSelectedCharacterId;
                if (!string.IsNullOrEmpty(characterId))
                {
                    var characterData = characterManager.GetCharacterDataById(characterId);
                    if (characterData != null && characterData.characterPrefab != null)
                    {
                        source = $"ActiveProfile.LastSelectedCharacterId='{characterId}'";
                        return characterData.characterPrefab;
                    }

                    Debug.LogWarning($"[GameSceneBootstrap] No CharacterData / prefab found for id '{characterId}'.");
                }
                else
                {
                    Debug.LogWarning("[GameSceneBootstrap] ActiveProfile.LastSelectedCharacterId is empty.");
                }
            }

            // Fallback for direct-play testing (skips the boot flow).
            if (allowFallbackForDirectPlay && fallbackCharacterPrefab != null)
            {
                source = "fallback (direct-play)";
                return fallbackCharacterPrefab;
            }

            return null;
        }
    }
}

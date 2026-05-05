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
using CyberPickle.Characters.Data;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.RunState;
using CyberPickle.Gameplay.Stats;

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
            // Start the run in Loading phase. SpawnSelectedCharacter transitions
            // to Running once the player is fully spawned + initialized.
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.TransitionTo(RunStatePhase.Loading);

            SpawnSelectedCharacter();
        }

        private void SpawnSelectedCharacter()
        {
            CharacterData characterData = ResolveCharacterData(out string source);
            GameObject prefabToSpawn = characterData != null
                ? characterData.characterPrefab
                : fallbackCharacterPrefab;

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

            // Initialize PlayerStats from the selected character's BaseStats.
            // If we took the fallback (no CharacterData), use BaseStats.Defaults
            // so the player still has sane stats for direct-play testing.
            BaseStats startingStats = characterData != null
                ? characterData.baseStats
                : BaseStats.Defaults;
            InitializePlayerStats(SpawnedPlayer, startingStats);

            // PlayerHealth must be reset AFTER PlayerStats.Initialize so MaxHealth
            // reflects the spawned character's BaseStats (otherwise health resets
            // to the Defaults value from BaseStats.Defaults). Also subscribes to
            // OnPlayerDied to disable input on death — replaced by a proper
            // RunStateManager in M7.2.
            InitializePlayerHealth(SpawnedPlayer);

            Debug.Log($"<color=cyan>[GameSceneBootstrap]</color> Spawned '{prefabToSpawn.name}' at {pos}. Source: {source}.");

            // Player is fully set up — transition the run from Loading to
            // Running. RunStateManager sets Time.timeScale = 1 and the
            // RunTime timer starts ticking.
            if (RunStateManager.Instance != null)
                RunStateManager.Instance.TransitionTo(RunStatePhase.Running);

            // Notify both inspector-wired and code-wired listeners.
            OnPlayerSpawned?.Invoke(SpawnedPlayer);
            PlayerSpawned?.Invoke(SpawnedPlayer);
        }

        /// <summary>
        /// Initializes PlayerStats with the spawned character's base stats.
        /// PlayerStats clears any existing modifiers, so this is safe to call
        /// even if equipped-item modifiers will be applied immediately after.
        /// </summary>
        private static void InitializePlayerStats(GameObject player, BaseStats baseStats)
        {
            var stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.Initialize(baseStats);
            }
            else
            {
                Debug.LogWarning($"[GameSceneBootstrap] Spawned '{player.name}' has no PlayerStats component — gameplay systems that read stats will see defaults.");
            }
        }

        /// <summary>
        /// Resets PlayerHealth to full and wires OnPlayerDied to transition
        /// the run state to GameOver. RunStateManager handles Time.timeScale
        /// and event dispatch from there — ResultsScreenController listens
        /// for the GameOver phase to show the results panel.
        /// </summary>
        private void InitializePlayerHealth(GameObject player)
        {
            var health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                Debug.LogWarning($"[GameSceneBootstrap] Spawned '{player.name}' has no PlayerHealth component — player will be invulnerable.");
                return;
            }

            health.ResetToFull();
            health.OnPlayerDied += HandlePlayerDeath;
        }

        /// <summary>
        /// On player death, transition the run to GameOver. RunStateManager
        /// freezes Time.timeScale; ResultsScreenController shows the results
        /// panel with the final RunStats values.
        /// </summary>
        private static void HandlePlayerDeath()
        {
            Debug.Log($"<color=red>[GameSceneBootstrap]</color> Player died — transitioning to GameOver.");

            if (RunStateManager.Instance != null)
                RunStateManager.Instance.TransitionTo(RunStatePhase.GameOver);
            else
                Debug.LogWarning("[GameSceneBootstrap] No RunStateManager — death will not show results screen.");
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

            // Loadout loader: enable so it spawns equipped weapons at mount points.
            // (Optional component — characters without weapon mounts simply skip this.)
            var loadout = player.GetComponent<PlayerLoadoutLoader>();
            if (loadout != null)
            {
                loadout.enabled = true;
            }

            // Player <-> ECS bridge: writes player position to a singleton entity
            // every frame so DOTS systems (enemy AI, etc.) can read it. Optional —
            // characters without the bridge just won't be visible to ECS systems.
            var positionBridge = player.GetComponent<PlayerPositionBridge>();
            if (positionBridge != null)
            {
                positionBridge.enabled = true;
            }

            // Player Stats <-> ECS bridge: mirrors PlayerStats values into a
            // PlayerStatsData singleton each frame for Burst-side reads (XP magnet
            // radius, damage pipeline Power/Crit, etc.). Optional — characters
            // without the bridge fall back to whatever the singleton currently
            // holds (BaseStats.Defaults if never written).
            var statsBridge = player.GetComponent<PlayerStatsBridge>();
            if (statsBridge != null)
            {
                statsBridge.enabled = true;
            }

            // Player Health <-> ECS bridge: mirrors CurrentHealth / MaxHealth /
            // IsAlive to a PlayerHealthData singleton AND drains ECS-accumulated
            // damage (from EnemyContactDamageSystem and future enemy-projectile
            // systems) into PlayerHealth.TakeDamage each frame. Without this
            // bridge, ECS damage sources can't reach the MonoBehaviour
            // PlayerHealth.
            var healthBridge = player.GetComponent<PlayerHealthBridge>();
            if (healthBridge != null)
            {
                healthBridge.enabled = true;
            }
        }

        /// <summary>
        /// Returns the CharacterData for the active profile's selected character,
        /// or null if none can be resolved. The fallback prefab path is handled
        /// by the caller (it has no CharacterData).
        /// </summary>
        private CharacterData ResolveCharacterData(out string source)
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
                        return characterData;
                    }

                    Debug.LogWarning($"[GameSceneBootstrap] No CharacterData / prefab found for id '{characterId}'.");
                }
                else
                {
                    Debug.LogWarning("[GameSceneBootstrap] ActiveProfile.LastSelectedCharacterId is empty.");
                }
            }

            // Fallback path is handled by the caller — it uses
            // fallbackCharacterPrefab (a plain GameObject) and BaseStats.Defaults.
            if (allowFallbackForDirectPlay && fallbackCharacterPrefab != null)
            {
                source = "fallback (direct-play, no CharacterData)";
            }

            return null;
        }
    }
}

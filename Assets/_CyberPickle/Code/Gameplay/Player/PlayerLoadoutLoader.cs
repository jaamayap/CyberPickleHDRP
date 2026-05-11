// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerLoadoutLoader.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Purpose: At gameplay start, reads the active profile's equipped items
// for the active character and instantiates each item's prefab as a child
// of the matching mount on WeaponMountPoints. Visual-only — no firing,
// no stats wiring (those are later milestones).
//
// Lifecycle: this component ships DISABLED on the character prefab
// (matching the pattern used for PlayerInput / PlayerMotor — preview
// scenes shouldn't see attached weapons). GameSceneBootstrap activates
// it at runtime in the Game scene; OnEnable runs the loadout query and
// spawns the items. OnDisable cleans them up.

using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Player
{
    [RequireComponent(typeof(WeaponMountPoints))]
    [DisallowMultipleComponent]
    public class PlayerLoadoutLoader : MonoBehaviour
    {
        [Header("Direct-Play Fallback (development only)")]
        [Tooltip("If true and no equipped weapons can be loaded (e.g., you pressed Play directly in Game.unity without booting through Boot.unity), spawn the fallback prefabs at their respective mount points.")]
        [SerializeField] private bool allowFallbackForDirectPlay = true;

        [Tooltip("Weapon prefab spawned at the right-hand mount when no profile-equipped weapon is available.")]
        [SerializeField] private GameObject fallbackHandWeaponPrefab;

        private WeaponMountPoints mounts;
        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        private void Awake()
        {
            mounts = GetComponent<WeaponMountPoints>();
        }

        private void OnEnable()
        {
            // GameSceneBootstrap enables this component after spawn. EquipmentManager
            // and ProfileManager are singletons booted via Boot.unity — already
            // initialized by the time we get here.
            LoadAndSpawn();
        }

        private void OnDisable()
        {
            ClearSpawnedItems();
        }

        private void LoadAndSpawn()
        {
            ClearSpawnedItems();

            // Also clear the run-state loadout authority so retry-without-
            // scene-reload starts with a clean slate. Visual prefabs and
            // runtime instances are repopulated below by SpawnAtMount.
            var existingLoadout = WeaponLoadoutRuntime.Instance;
            if (existingLoadout != null) existingLoadout.ClearLoadout();

            int spawnedCount = 0;

            EquipmentManager equipmentManager = EquipmentManager.Instance;
            if (equipmentManager != null)
            {
                // GetEquippedEquipment() resolves the active character's loadout via
                // ProfileManager + CharacterProgressionData internally and returns
                // EquipmentData references grouped by slot type.
                Dictionary<EquipmentSlotType, List<EquipmentData>> equipped = equipmentManager.GetEquippedEquipment();
                if (equipped != null && equipped.Count > 0)
                {
                    spawnedCount = SpawnFromEquipped(equipped);
                }
            }
            else
            {
                Debug.LogWarning("[PlayerLoadoutLoader] EquipmentManager.Instance is null (likely direct-play in Game.unity).");
            }

            // If nothing was loaded from the profile and a fallback is configured,
            // spawn it so the Game scene is testable in isolation.
            if (spawnedCount == 0 && allowFallbackForDirectPlay && fallbackHandWeaponPrefab != null && mounts.HandR != null)
            {
                Debug.Log($"<color=yellow>[PlayerLoadoutLoader]</color> No profile-equipped weapons found — spawning fallback '{fallbackHandWeaponPrefab.name}' at HandR for direct-play.");
                GameObject instance = Instantiate(fallbackHandWeaponPrefab, mounts.HandR);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.name = $"{fallbackHandWeaponPrefab.name} (Fallback)";
                spawnedItems.Add(instance);
            }
        }

        private int SpawnFromEquipped(Dictionary<EquipmentSlotType, List<EquipmentData>> equipped)
        {
            // M9 PR B: weapons spawn at the cross-axis mount matching the
            // axis they LAND in inside WeaponLoadoutRuntime. The flow is:
            //   1. Iterate equipped weapons (HandWeapon + BodyWeapon types)
            //      in a stable order.
            //   2. SpawnAtMount registers each weapon with the loadout
            //      runtime, which appends to the first empty axis (0..3).
            //   3. The returned axisIndex picks the mount transform from
            //      WeaponMountPoints.GetMountForAxis(axisIndex):
            //          0 → Front, 1 → Right, 2 → Back, 3 → Left
            //
            // The old "hand-weapons go in HandR/HandL, body in Body" mapping
            // is preserved as fallback inside WeaponMountPoints itself, so
            // characters with only the legacy 3 mounts still work.

            int count = 0;
            var ordered = new List<WeaponData>(4);

            if (equipped.TryGetValue(EquipmentSlotType.HandWeapon, out var handWeapons))
                foreach (var w in handWeapons) if (w is WeaponData wd) ordered.Add(wd);

            if (equipped.TryGetValue(EquipmentSlotType.BodyWeapon, out var bodyWeapons))
                foreach (var w in bodyWeapons) if (w is WeaponData wd) ordered.Add(wd);

            foreach (var weapon in ordered)
            {
                if (SpawnAtAxis(weapon)) count++;
            }

            // PowerUps / Armor / Amulet: visual representation deferred to later
            // milestones (those slots are currently stat-modifier-only and don't
            // necessarily need a 3D prefab attached to the character).
            return count;
        }

        /// <summary>
        /// Register the weapon with the loadout runtime (it picks the next
        /// empty axis), then spawn its visual at the mount matching that
        /// axis. The mount mapping is owned by <see cref="WeaponMountPoints"/>.
        /// </summary>
        private bool SpawnAtAxis(WeaponData weapon)
        {
            if (weapon == null || weapon.equipmentPrefab == null) return false;

            var loadout = WeaponLoadoutRuntime.Instance;
            if (loadout == null)
            {
                Debug.LogWarning("[PlayerLoadoutLoader] WeaponLoadoutRuntime.Instance is null — falling back to legacy mount mapping.");
                // Legacy fallback: drop on HandR if available, else skip.
                return mounts.HandR != null && SpawnAtMount(weapon, mounts.HandR);
            }

            if (!loadout.TryAddWeapon(weapon, Rarity.Common, out var added) || added == null)
            {
                Debug.LogWarning($"[PlayerLoadoutLoader] WeaponLoadoutRuntime rejected '{weapon.displayName}' (loadout full?).");
                return false;
            }

            int axis = added.slotIndex;
            Transform mount = mounts.GetMountForAxis(axis);
            if (mount == null)
            {
                Debug.LogWarning($"[PlayerLoadoutLoader] No mount found for axis {axis} on '{name}'. '{weapon.displayName}' registered with loadout but has no visual.");
                return true; // logical add succeeded even if visual didn't
            }

            return SpawnAtMount(weapon, mount, alreadyRegistered: true);
        }

        private bool SpawnAtMount(EquipmentData data, Transform mount, bool alreadyRegistered = false)
        {
            if (data == null || data.equipmentPrefab == null) return false;

            GameObject instance = Instantiate(data.equipmentPrefab, mount);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.name = $"{data.displayName} (Equipped)";
            spawnedItems.Add(instance);

            // Legacy registration path — used when the new SpawnAtAxis flow
            // isn't applicable (e.g., direct-Play fallback, or future
            // equipment types we add later). When alreadyRegistered is true,
            // the caller (SpawnAtAxis) has already done this step.
            if (!alreadyRegistered && data is WeaponData weaponData)
            {
                var loadout = WeaponLoadoutRuntime.Instance;
                if (loadout != null)
                {
                    if (!loadout.TryAddWeapon(weaponData, Rarity.Common, out var added))
                        Debug.LogWarning($"[PlayerLoadoutLoader] WeaponLoadoutRuntime rejected '{data.displayName}' (loadout full?).");
                    else if (added != null)
                        Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> Registered '{data.displayName}' with WeaponLoadoutRuntime in axis {added.slotIndex}.");
                }
                else
                {
                    Debug.LogWarning("[PlayerLoadoutLoader] WeaponLoadoutRuntime.Instance is null — HUD weapon slots won't show this weapon.");
                }
            }

            Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> Spawned '{data.displayName}' at '{mount.name}'.");
            return true;
        }

        private void ClearSpawnedItems()
        {
            for (int i = spawnedItems.Count - 1; i >= 0; i--)
            {
                if (spawnedItems[i] != null)
                {
                    Destroy(spawnedItems[i]);
                }
            }
            spawnedItems.Clear();
        }
    }
}

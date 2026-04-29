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
using CyberPickle.Core.Services.Authentication.Data;
using CyberPickle.Shop.Equipment;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Player
{
    [RequireComponent(typeof(WeaponMountPoints))]
    [DisallowMultipleComponent]
    public class PlayerLoadoutLoader : MonoBehaviour
    {
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

            EquipmentManager equipmentManager = EquipmentManager.Instance;
            if (equipmentManager == null)
            {
                Debug.LogWarning("[PlayerLoadoutLoader] EquipmentManager.Instance is null — skipping loadout spawn. Did you press Play from Boot.unity?");
                return;
            }

            // GetEquippedEquipment() resolves the active character's loadout via
            // ProfileManager + CharacterProgressionData internally and returns
            // EquipmentData references grouped by slot type.
            Dictionary<EquipmentSlotType, List<EquipmentData>> equipped = equipmentManager.GetEquippedEquipment();
            if (equipped == null || equipped.Count == 0)
            {
                Debug.Log("[PlayerLoadoutLoader] No equipped items found for active character. Spawning empty loadout.");
                return;
            }

            // Hand weapons (max 2 — slot 0 right, slot 1 left)
            if (equipped.TryGetValue(EquipmentSlotType.HandWeapon, out var handWeapons))
            {
                for (int i = 0; i < handWeapons.Count; i++)
                {
                    Transform mount = mounts.GetHandMount(i);
                    if (mount == null)
                    {
                        Debug.LogWarning($"[PlayerLoadoutLoader] No hand mount available for slot {i} on '{name}'. Skipping '{handWeapons[i]?.displayName}'.");
                        continue;
                    }
                    SpawnAtMount(handWeapons[i], mount);
                }
            }

            // Body weapon (max 1)
            if (equipped.TryGetValue(EquipmentSlotType.BodyWeapon, out var bodyWeapons) && bodyWeapons.Count > 0)
            {
                if (mounts.Body == null)
                {
                    Debug.LogWarning($"[PlayerLoadoutLoader] No body mount on '{name}'. Skipping '{bodyWeapons[0]?.displayName}'.");
                }
                else
                {
                    SpawnAtMount(bodyWeapons[0], mounts.Body);
                }
            }

            // PowerUps / Armor / Amulet: visual representation deferred to later
            // milestones (those slots are currently stat-modifier-only and don't
            // necessarily need a 3D prefab attached to the character).
        }

        private void SpawnAtMount(EquipmentData data, Transform mount)
        {
            if (data == null || data.equipmentPrefab == null) return;

            GameObject instance = Instantiate(data.equipmentPrefab, mount);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.name = $"{data.displayName} (Equipped)";
            spawnedItems.Add(instance);

            Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> Spawned '{data.displayName}' at '{mount.name}'.");
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

// File: Assets/_CyberPickle/Code/Gameplay/Player/PlayerLoadoutLoader.cs
// Namespace: CyberPickle.Gameplay.Player
//
// Lives on the player character. Two responsibilities:
//
//   1. On scene-load (OnEnable): read the active profile's equipped weapons
//      from EquipmentManager and register each with WeaponLoadoutRuntime.
//      The runtime appends each to its first empty axis (0..3, mapping to
//      N/E/S/W cross axes — see WeaponMountPoints.GetMountForAxis).
//
//   2. Subscribe to WeaponLoadoutRuntime.OnWeaponAdded and spawn the
//      visual prefab + override the WeaponFiring slotIndex whenever a
//      weapon is added — whether from the scene-load pass OR from a mid-run
//      NewWeapon draft card. ONE visual-spawn path for both cases.
//
// Before this consolidation (M9 PR G follow-up), scene-load weapons spawned
// visuals directly inside LoadAndSpawn but mid-run NewWeapon cards only
// updated the loadout — leaving the HUD slot visible but the world-side
// weapon invisible. Now every WeaponLoadoutRuntime.TryAddWeapon /
// TryAddWeaponAt call fires OnWeaponAdded → HandleWeaponAdded spawns the
// visual at the correct mount and tells the spawned WeaponFiring which
// axis it represents.
//
// Lifecycle: this component ships DISABLED on the character prefab.
// GameSceneBootstrap activates it at runtime. OnEnable subscribes +
// runs the initial load; OnDisable unsubscribes + cleans up visuals.

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
        [Header("Test Override (development only)")]
        [Tooltip("If assigned, BYPASSES the EquipmentManager entirely and loads ONLY this one weapon at axis 0 (front mount). Use for isolated weapon testing — e.g., drop the sniper SO here to test pierce mechanics without messing with the EquipmentHub saved data. Leave empty for normal play (the loadout reads from the active profile's equipped weapons).")]
        [SerializeField] private WeaponData forcedTestWeapon;

        [Header("Direct-Play Fallback (development only)")]
        [Tooltip("If true and no equipped weapons can be loaded (e.g., direct-Play in Game.unity without booting through Boot.unity), spawn the fallback prefab at the front mount.")]
        [SerializeField] private bool allowFallbackForDirectPlay = true;

        [Tooltip("Weapon prefab spawned at the front mount when no profile-equipped weapon is available. This is a RAW prefab — doesn't go through WeaponData / WeaponLoadoutRuntime registration. For real testing use a profile-equipped weapon.")]
        [SerializeField] private GameObject fallbackHandWeaponPrefab;

        private WeaponMountPoints mounts;
        private readonly List<GameObject> spawnedItems = new List<GameObject>();
        private bool _subscribed;

        private void Awake()
        {
            mounts = GetComponent<WeaponMountPoints>();
        }

        private void OnEnable()
        {
            // Subscribe FIRST so we catch the OnWeaponAdded events that
            // LoadAndSpawn's TryAddWeapon calls will fire. Single visual-
            // spawn path → no double-spawn bookkeeping.
            BindToLoadout();
            LoadAndSpawn();
        }

        private void OnDisable()
        {
            UnbindFromLoadout();
            ClearSpawnedItems();
        }

        // ─── Loadout binding ──────────────────────────────────────────────

        private void BindToLoadout()
        {
            if (_subscribed) return;
            var loadout = WeaponLoadoutRuntime.Instance;
            if (loadout == null) return;
            loadout.OnWeaponAdded   += HandleWeaponAdded;
            loadout.OnLoadoutCleared += HandleLoadoutCleared;
            _subscribed = true;
        }

        private void UnbindFromLoadout()
        {
            if (!_subscribed) return;
            var loadout = WeaponLoadoutRuntime.Instance;
            if (loadout != null)
            {
                loadout.OnWeaponAdded   -= HandleWeaponAdded;
                loadout.OnLoadoutCleared -= HandleLoadoutCleared;
            }
            _subscribed = false;
        }

        // ─── Initial scene-load spawn ─────────────────────────────────────

        private void LoadAndSpawn()
        {
            // Clear any leftover visuals (retry-without-scene-reload). The
            // loadout.ClearLoadout below fires OnLoadoutCleared → HandleLoadoutCleared
            // which also calls ClearSpawnedItems — both run, idempotent.
            ClearSpawnedItems();

            var loadout = WeaponLoadoutRuntime.Instance;
            if (loadout != null) loadout.ClearLoadout();

            int registered = 0;

            // Test-override path: when forcedTestWeapon is assigned, skip the
            // EquipmentManager entirely and register JUST this one weapon at
            // axis 0. Useful for isolated weapon testing — e.g., drop the
            // sniper SO here to test pierce without touching the EquipmentHub
            // saved data. Production play leaves this field null.
            if (forcedTestWeapon != null && loadout != null)
            {
                Debug.Log($"<color=magenta>[PlayerLoadoutLoader]</color> TEST OVERRIDE — loading only '{forcedTestWeapon.displayName}' (EquipmentManager bypassed). Clear PlayerLoadoutLoader.forcedTestWeapon for normal play.");
                if (loadout.TryAddWeapon(forcedTestWeapon, Rarity.Common, out _))
                    registered = 1;
                else
                    Debug.LogWarning($"[PlayerLoadoutLoader] Loadout rejected test-override weapon '{forcedTestWeapon.displayName}'.");
            }
            else
            {
                EquipmentManager equipmentManager = EquipmentManager.Instance;
                if (equipmentManager != null)
                {
                    var equipped = equipmentManager.GetEquippedEquipment();
                    LogEquippedSnapshot(equipped);
                    if (equipped != null && equipped.Count > 0)
                        registered = RegisterEquipped(equipped, loadout);
                }
                else
                {
                    Debug.LogWarning("[PlayerLoadoutLoader] EquipmentManager.Instance is null (likely direct-play in Game.unity).");
                }
            }

            Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> LoadAndSpawn finished — {registered} weapon(s) registered with the loadout. Fallback will fire = {(registered == 0 && allowFallbackForDirectPlay && fallbackHandWeaponPrefab != null)}.");

            // Fallback: if nothing got registered, spawn the dev fallback
            // RAW prefab at the front mount. This path skips the loadout
            // entirely (the prefab isn't a WeaponData asset). The weapon's
            // inspector-fallback fireRate/projectileSpeed/projectileDamage
            // on WeaponFiring kick in.
            if (registered == 0 && allowFallbackForDirectPlay && fallbackHandWeaponPrefab != null)
            {
                Transform mount = mounts.GetMountForAxis(0); // North / Front
                if (mount != null)
                {
                    Debug.Log($"<color=yellow>[PlayerLoadoutLoader]</color> No profile-equipped weapons — spawning fallback '{fallbackHandWeaponPrefab.name}' at front mount for direct-play.");
                    GameObject instance = Instantiate(fallbackHandWeaponPrefab, mount);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.identity;
                    instance.name = $"{fallbackHandWeaponPrefab.name} (Fallback)";
                    spawnedItems.Add(instance);
                }
            }
        }

        /// <summary>
        /// Diagnostic: dumps the contents of the equipped-equipment snapshot
        /// to the console so we can see exactly what the EquipmentManager
        /// thinks is equipped at boot. Useful when "the player spawned with
        /// the wrong weapon" symptoms appear — usually the saved data
        /// contains different items than the player expects.
        /// </summary>
        private void LogEquippedSnapshot(Dictionary<EquipmentSlotType, List<EquipmentData>> equipped)
        {
            if (equipped == null)
            {
                Debug.LogWarning("[PlayerLoadoutLoader] GetEquippedEquipment() returned null.");
                return;
            }

            var sb = new System.Text.StringBuilder(256);
            sb.Append("<color=cyan>[PlayerLoadoutLoader]</color> EquipmentManager snapshot — ");
            int total = 0;
            foreach (var kvp in equipped)
            {
                if (kvp.Value == null || kvp.Value.Count == 0) continue;
                sb.Append(kvp.Key).Append(": [");
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    var e = kvp.Value[i];
                    if (e == null) { sb.Append("<null>"); continue; }
                    sb.Append($"{e.displayName} (id={e.equipmentId}, isWeaponData={e is WeaponData})");
                    total++;
                }
                sb.Append("]  ");
            }
            sb.Append($"— {total} item(s) total.");
            if (total == 0) sb.Append(" (NO ITEMS — fallback path will engage if enabled.)");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Register each equipped weapon with the loadout runtime. The
        /// runtime appends each to its first empty axis and fires
        /// OnWeaponAdded — HandleWeaponAdded spawns the visual.
        /// </summary>
        private int RegisterEquipped(Dictionary<EquipmentSlotType, List<EquipmentData>> equipped, WeaponLoadoutRuntime loadout)
        {
            if (loadout == null) return 0;

            // Flatten HandWeapon + BodyWeapon entries into a stable order.
            // The loadout itself owns which axis (0..3) each one lands at.
            var ordered = new List<WeaponData>(4);
            if (equipped.TryGetValue(EquipmentSlotType.HandWeapon, out var hands))
                foreach (var w in hands) if (w is WeaponData wd) ordered.Add(wd);
            if (equipped.TryGetValue(EquipmentSlotType.BodyWeapon, out var bodies))
                foreach (var w in bodies) if (w is WeaponData wd) ordered.Add(wd);

            Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> RegisterEquipped — {ordered.Count} weapon(s) after WeaponData filter: [{string.Join(", ", ordered.ConvertAll(w => w != null ? w.displayName : "<null>"))}]");

            int count = 0;
            foreach (var weapon in ordered)
            {
                if (weapon == null) continue;
                if (loadout.TryAddWeapon(weapon, Rarity.Common, out _))
                {
                    count++;
                    Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> Loadout ACCEPTED '{weapon.displayName}'.");
                }
                else
                    Debug.LogWarning($"[PlayerLoadoutLoader] Loadout REJECTED '{weapon.displayName}' (full or already present?).");
            }
            return count;
        }

        // ─── Event handler: ONE place that spawns a weapon's world visual ─

        /// <summary>
        /// Fires for both initial scene-load adds AND mid-run NewWeapon
        /// card picks. Spawns the weapon's <c>equipmentPrefab</c> at the
        /// mount matching the added axis, then overrides the WeaponFiring
        /// component's <c>slotIndex</c> so it reads its own loadout entry
        /// (rather than the prefab's authored default of 0, which would
        /// make all dynamically-spawned weapons read axis 0's instance).
        /// </summary>
        private void HandleWeaponAdded(WeaponInstanceData added)
        {
            if (added == null || !added.IsValid) return;
            if (added.weaponData.equipmentPrefab == null)
            {
                Debug.LogWarning($"[PlayerLoadoutLoader] Weapon '{added.WeaponId}' has no equipmentPrefab — no visual will spawn.");
                return;
            }

            int axis = added.slotIndex;
            Transform mount = mounts.GetMountForAxis(axis);
            if (mount == null)
            {
                Debug.LogWarning($"[PlayerLoadoutLoader] No mount transform for axis {axis} on '{name}'. '{added.weaponData.displayName}' registered but invisible. Wire Front/Right/Back/Left mounts on WeaponMountPoints.");
                return;
            }

            GameObject instance = Instantiate(added.weaponData.equipmentPrefab, mount);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.name = $"{added.weaponData.displayName} (axis {axis})";
            spawnedItems.Add(instance);

            // Override the WeaponFiring's slotIndex so it reads ITS OWN
            // axis's instance data. Without this, every dynamically-spawned
            // weapon would inherit the prefab's authored slotIndex (typically
            // 0) and they'd all fight over axis 0's WeaponInstanceData.
            var firing = instance.GetComponentInChildren<WeaponFiring>(includeInactive: true);
            if (firing != null) firing.SetSlotIndex(axis);

            // Same for WeaponTargeting (M9 PR C) — it now reads range +
            // strategy + cone-angle + cluster-radius from the loadout slot's
            // WeaponData, so it needs to know which slot is "its" data.
            var targeting = instance.GetComponentInChildren<WeaponTargeting>(includeInactive: true);
            if (targeting != null) targeting.SetSlotIndex(axis);

            Debug.Log($"<color=cyan>[PlayerLoadoutLoader]</color> Spawned '{added.weaponData.displayName}' at axis {axis} ({mount.name}).");
        }

        private void HandleLoadoutCleared()
        {
            ClearSpawnedItems();
        }

        // ─── Cleanup ──────────────────────────────────────────────────────

        private void ClearSpawnedItems()
        {
            for (int i = spawnedItems.Count - 1; i >= 0; i--)
            {
                if (spawnedItems[i] != null) Destroy(spawnedItems[i]);
            }
            spawnedItems.Clear();
        }
    }
}

// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponFiring.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Auto-fires entity-based projectiles at the WeaponTargeting's current
// target on a fire-rate cooldown. Lives MonoBehaviour-side (one per
// weapon), but spawns ECS-side projectiles for runtime performance.
//
// 2026-05-10 refactor (Phase 4):
//   - Reads per-shot stats from WeaponLoadoutRuntime + WeaponData instead
//     of hardcoded inspector fields. The dual-axis weapon model is now wired:
//
//       Damage    = weaponData.GetDamageForRarity(instance.rarity)
//                   (Power × crit applied at hit time by ProjectileCollisionSystem)
//
//       FireRate  = weaponData.GetFireRateForLevel(instance.level)
//                   (Level → fire rate scaling — Level does NOT scale damage)
//
//   - Old inspector fields (fireRate, projectileDamage, projectileSpeed)
//     are RETAINED as fallbacks for scene-test setups where the loadout
//     runtime isn't running yet. In production, weaponData is assigned
//     and the runtime instance drives everything.
//
// Spawn flow:
//   1. On first Fire(), lazily resolve the projectile prefab entity by
//      querying the world for entities with ProjectileTag + Prefab.
//   2. EntityManager.Instantiate(prefabEntity) creates an active copy.
//   3. We override LocalTransform / ProjectileVelocity / ProjectileDamage
//      / ProjectileLifetime per-spawn so the same prefab can carry
//      different stats from different weapons.
//   4. We stamp WeaponLevel + WeaponRarity ECS components for any future
//      Burst-side consumer (currently optional — collision system reads
//      pre-baked damage from ProjectileDamage).
//   5. ProjectileSource (FixedString64Bytes weaponId) is stamped for
//      PerWeaponStatsTracker attribution.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.Core;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.Stats;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    [RequireComponent(typeof(WeaponTargeting))]
    [DisallowMultipleComponent]
    public class WeaponFiring : MonoBehaviour
    {
        [Header("Weapon Definition")]
        [Tooltip("The weapon's design SO. When assigned, base damage / fire-rate / projectile-speed come from this asset and are scaled per the runtime instance's Level + Rarity from WeaponLoadoutRuntime. Leave null only for scene-test setups; production uses weaponData.")]
        [SerializeField] private WeaponData weaponData;

        [Tooltip("Loadout slot this WeaponFiring component represents. 0 = starting weapon (typical); 1..3 = drafted in-run weapons (these are usually spawned dynamically). The runtime reads the matching WeaponInstanceData from WeaponLoadoutRuntime.GetSlot(slotIndex) on each fire.")]
        [SerializeField, Range(0, WeaponLoadoutRuntime.MaxSlots - 1)] private int slotIndex = 0;

        [Header("Fallback Stats (used only when WeaponData is null)")]
        [Tooltip("Shots per second — fallback for inspector tests when WeaponData is unassigned.")]
        [SerializeField] private float fireRate = 2f;

        [Tooltip("Projectile travel speed (world units/sec) — fallback when WeaponData is unassigned.")]
        [SerializeField] private float projectileSpeed = 20f;

        [Tooltip("Damage applied to enemy on hit — fallback when WeaponData is unassigned.")]
        [SerializeField] private float projectileDamage = 5f;

        [Header("Spawn")]
        [Tooltip(
            "LEGACY single muzzle — projectiles spawn from here. " +
            "Used only when muzzleTransforms is empty. Falls back to the " +
            "weapon's own transform if both are empty.")]
        [SerializeField] private Transform muzzle;

        [Tooltip(
            "Multiple muzzles (M9 PR B). Fires ONE projectile per muzzle per " +
            "shot — shotgun has 3 muzzles, pistol/sniper have 1. Each muzzle's " +
            "OWN forward vector is the projectile direction, so authored local " +
            "rotations on the side muzzles create natural spread without " +
            "per-muzzle aiming (per the shotgun design from chat 2026-05-11: " +
            "central muzzle aims via WeaponTargeting; side muzzles fire " +
            "forward along the weapon's overall facing, offset by their " +
            "authored local rotations). Leave empty to use the legacy " +
            "single 'muzzle' field above.")]
        [SerializeField] private Transform[] muzzleTransforms;

        [Tooltip("Maximum lifetime of a projectile in seconds (despawns if it doesn't hit). Independent of weapon level/rarity — projectiles always self-expire on the same timer regardless of who fired them.")]
        [SerializeField] private float projectileLifetime = 3f;

        [Header("Identity (fallback)")]
        [Tooltip("Stable id used for per-weapon stats attribution. Used when WeaponData is null OR as a sanity name for ProjectileSource. When WeaponData is assigned, weaponData.equipmentId takes priority.")]
        [SerializeField] private string weaponId;

        [Header("Diagnostics")]
        [Tooltip("Verbose logging for fire events. Off by default to avoid log flood.")]
        [SerializeField] private bool verboseLogging = false;

        // ─── Runtime state ────────────────────────────────────────────────

        private WeaponTargeting targeting;
        private float cooldown;
        private FixedString64Bytes _weaponIdFixed;

        private World world;
        private EntityManager entityManager;
        private Entity prefabEntity = Entity.Null;        // legacy single-prefab cache
        private Entity prefabRegistryEntity = Entity.Null; // entity holding the per-element buffer
        private bool dotsInitialized;

        private void Awake()
        {
            targeting = GetComponent<WeaponTargeting>();
            if (muzzle == null) muzzle = transform;

            // Resolve a stable weapon id once at Awake. Used by ProjectileSource
            // for per-weapon damage attribution (PerWeaponStatsTracker, hover
            // tooltips). Priority: WeaponData.equipmentId > inspector weaponId
            // > GameObject name. FixedString64Bytes is Burst-compatible.
            string id = ResolveWeaponId();
            _weaponIdFixed = new FixedString64Bytes(id);
        }

        private string ResolveWeaponId()
        {
            if (weaponData != null && !string.IsNullOrEmpty(weaponData.equipmentId))
                return weaponData.equipmentId;
            if (!string.IsNullOrWhiteSpace(weaponId))
                return weaponId;
            return gameObject.name.ToLowerInvariant().Replace(' ', '_');
        }

        private void Update()
        {
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;
            if (!targeting.HasTarget) return;

            if (!ResolvePrefab()) return;

            var instance = GetCurrentInstance();
            Fire(instance);

            float effectiveRate = GetEffectiveFireRate(instance);
            cooldown = 1f / Mathf.Max(0.01f, effectiveRate);
        }

        // ─── Effective stat helpers ───────────────────────────────────────

        /// <summary>
        /// Returns the WeaponInstanceData for our slot, or null if the
        /// loadout runtime isn't available / hasn't populated this slot
        /// (e.g., scene-test setups, or before RunStart populates slot 0).
        /// </summary>
        private WeaponInstanceData GetCurrentInstance()
        {
            // Manager<T>.Instance returns null in non-play mode; that's fine,
            // we won't be ticking Update() outside of play mode anyway.
            var loadout = WeaponLoadoutRuntime.Instance;
            return loadout?.GetSlot(slotIndex);
        }

        /// <summary>
        /// Resolve the effective WeaponData reference for this fire-frame.
        /// Priority: inspector-assigned > loadout-instance > null. The loadout
        /// fallback was added 2026-05-10: legacy weapon prefabs don't have
        /// the new <c>weaponData</c> field assigned, but the loadout DOES
        /// (PlayerLoadoutLoader.SpawnAtMount registers it). Falling back to
        /// the loadout's weaponData lets us keep accurate Damage/FireRate/
        /// equipmentId attribution without requiring designers to migrate
        /// every weapon prefab.
        /// </summary>
        private WeaponData ResolveWeaponData(WeaponInstanceData instance)
        {
            if (weaponData != null) return weaponData;
            if (instance != null && instance.IsValid) return instance.weaponData;
            return null;
        }

        /// <summary>
        /// Effective fire rate (shots/sec). Pulls from the new pattern-driven
        /// formula on <see cref="WeaponData"/>: active-cells per level × BPM
        /// (BPM scales with player Dexterity). Falls back to flat baseFireRate
        /// (Dex-scaled) when patterns aren't authored, and finally to the
        /// inspector <c>fireRate</c> field when no <see cref="WeaponData"/>.
        /// </summary>
        private float GetEffectiveFireRate(WeaponInstanceData instance)
        {
            var data = ResolveWeaponData(instance);
            float dex = ResolvePlayerDexterity();
            if (data != null && instance != null && instance.IsValid)
                return data.GetFireRateForLevel(instance.level, dex);
            if (data != null)
                return data.GetFireRateForLevel(1, dex);
            return fireRate;
        }

        /// <summary>
        /// Read player Dexterity from PlayerStats. Cheap on the firing path
        /// (one component lookup + a cached array index). Returns 0 if no
        /// player has spawned yet.
        /// </summary>
        private PlayerStats _cachedStats;
        private float ResolvePlayerDexterity()
        {
            if (_cachedStats == null) _cachedStats = Object.FindFirstObjectByType<PlayerStats>();
            return _cachedStats != null ? _cachedStats.Get(PlayerStatType.Dexterity) : 0f;
        }

        /// <summary>
        /// Effective per-shot damage. The locked formula is
        /// <c>damage = baseDamage × Rarity.DamageMultiplier()</c>; Power and
        /// crit are applied at hit time by ProjectileCollisionSystem on top
        /// of this baked value (so the final-on-hit damage is
        /// <c>baseDamage × Rarity × (1 + Power*0.01) × critMul</c>).
        /// </summary>
        private float GetEffectiveDamage(WeaponInstanceData instance)
        {
            var data = ResolveWeaponData(instance);
            if (data != null && instance != null && instance.IsValid)
                return data.GetDamageForRarity(instance.rarity);
            if (data != null)
                return data.baseDamage;
            return projectileDamage;
        }

        /// <summary>
        /// Effective projectile speed. Currently unscaled by level/rarity
        /// in the v1 design — those axes drive fire rate and damage only.
        /// Could be extended to scale with level (faster projectiles at
        /// higher level for visual punch) when M9 polish lands.
        /// </summary>
        private float GetEffectiveProjectileSpeed(WeaponInstanceData instance)
        {
            var data = ResolveWeaponData(instance);
            if (data != null)
                return data.baseProjectileSpeed;
            return projectileSpeed;
        }

        // ─── Prefab resolution ────────────────────────────────────────────

        /// <summary>
        /// Verify the DOTS world is ready + cache the registry entity. The
        /// per-shot ELEMENT lookup happens later in <see cref="ResolvePrefabForElement"/>.
        /// </summary>
        private bool ResolvePrefab()
        {
            if (!dotsInitialized)
            {
                world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return false;
                entityManager = world.EntityManager;
                dotsInitialized = true;
            }

            // Cache the entity that owns the per-element buffer + legacy
            // holder. Refreshed lazily because SubScenes can be re-loaded.
            if (prefabRegistryEntity == Entity.Null || !entityManager.Exists(prefabRegistryEntity))
            {
                using var query = entityManager.CreateEntityQuery(typeof(ProjectilePrefabHolder));
                if (query.CalculateEntityCount() == 0) return false;
                prefabRegistryEntity = query.GetSingletonEntity();
            }

            // Refresh legacy single-prefab cache (used as None / fallback
            // when the per-element buffer doesn't have an entry).
            if (entityManager.HasComponent<ProjectilePrefabHolder>(prefabRegistryEntity))
            {
                var holder = entityManager.GetComponentData<ProjectilePrefabHolder>(prefabRegistryEntity);
                prefabEntity = holder.Value;
            }

            // Even if the legacy field is Entity.Null, the per-element
            // buffer may be populated — return true so Fire() can call
            // ResolvePrefabForElement, which checks the buffer too.
            return true;
        }

        /// <summary>
        /// Per-shot lookup: pick the baked projectile entity matching the
        /// weapon's currently-coupled element. Falls back to the None/
        /// legacy entry when no per-element entry exists.
        /// </summary>
        private Entity ResolvePrefabForElement(ElementId element)
        {
            if (!dotsInitialized || prefabRegistryEntity == Entity.Null)
                return Entity.Null;

            // Try the per-element buffer first.
            if (entityManager.HasBuffer<ProjectilePrefabEntry>(prefabRegistryEntity))
            {
                var buffer = entityManager.GetBuffer<ProjectilePrefabEntry>(prefabRegistryEntity);
                byte elemByte = (byte)element;
                Entity noneEntry = Entity.Null;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Element == elemByte && buffer[i].Prefab != Entity.Null)
                        return buffer[i].Prefab;
                    if (buffer[i].Element == (byte)ElementId.None && buffer[i].Prefab != Entity.Null)
                        noneEntry = buffer[i].Prefab;
                }
                // No match for this element; fall back to None entry.
                if (noneEntry != Entity.Null) return noneEntry;
            }

            // Last-resort: legacy single-prefab field.
            return prefabEntity;
        }

        // ─── Fire ─────────────────────────────────────────────────────────

        private void Fire(WeaponInstanceData instance)
        {
            float effectiveSpeed  = GetEffectiveProjectileSpeed(instance);
            float effectiveDamage = GetEffectiveDamage(instance);

            // Resolve the attribution id once — same for every muzzle of this shot.
            FixedString64Bytes idForSource = _weaponIdFixed;
            var resolvedData = ResolveWeaponData(instance);
            if (resolvedData != null && !string.IsNullOrEmpty(resolvedData.equipmentId))
                idForSource = new FixedString64Bytes(resolvedData.equipmentId);

            // Resolve the projectile prefab for this shot's element. Falls
            // back to None / legacy entry inside ResolvePrefabForElement.
            ElementId element = instance != null && instance.IsValid ? instance.element : ElementId.None;
            Entity perElementPrefab = ResolvePrefabForElement(element);
            if (perElementPrefab == Entity.Null)
            {
                if (verboseLogging)
                    Debug.LogWarning($"[WeaponFiring] No projectile prefab baked for element {element} (and no fallback). Skipping shot.");
                return;
            }

            // Determine the muzzle set for this shot. Multi-muzzle weapons
            // (shotgun has 3) fire one projectile per muzzle, each along its
            // OWN forward — authored local rotations on the side muzzles
            // create natural spread without per-muzzle aiming. Pistol /
            // sniper / grenade have 1 muzzle.
            int muzzleCount = (muzzleTransforms != null && muzzleTransforms.Length > 0)
                              ? muzzleTransforms.Length
                              : 1;

            for (int i = 0; i < muzzleCount; i++)
            {
                Transform m = (muzzleTransforms != null && muzzleTransforms.Length > 0)
                              ? muzzleTransforms[i]
                              : muzzle;
                if (m == null) continue;

                FireOneProjectile(m, instance, effectiveSpeed, effectiveDamage, idForSource, perElementPrefab);
                SpawnMuzzleFlash(m, instance);
            }

            if (verboseLogging)
            {
                string lvl = instance != null ? instance.level.ToString() + (instance.evolved ? "E" : "") : "?";
                string rar = instance != null ? instance.rarity.ToString() : "?";
                Debug.Log($"<color=cyan>[WeaponFiring]</color> Slot {slotIndex} '{idForSource}' fired ×{muzzleCount} muzzle(s) — L{lvl} {rar} dmg={effectiveDamage:F1} spd={effectiveSpeed:F1}.");
            }

            // Broadcast to the audio bus. Stage 0: a Debug.Log entry per shot
            // (only when VerboseLogging is on — off by default to avoid log
            // flood). Stage 2 (M9 Wwise): this becomes the per-shot Ak event
            // post that schedules the weapon's musical note on the next grid
            // boundary. The payload will eventually carry weapon-id + element
            // so the conductor can pick the right pitch/sample; for the stub
            // we just signal that A shot happened.
            MusicEventBus.Fire(MusicEvent.WeaponFire, gameObject.name);
        }

        /// <summary>
        /// Spawn one ECS projectile from the given muzzle Transform. Splits
        /// out from Fire() so multi-muzzle weapons (shotgun) can call this
        /// once per muzzle without duplicating the prefab-Instantiate + per-
        /// shot component stamping.
        /// </summary>
        private void FireOneProjectile(Transform fromMuzzle, WeaponInstanceData instance,
                                       float effectiveSpeed, float effectiveDamage,
                                       FixedString64Bytes idForSource,
                                       Entity sourcePrefab)
        {
            Entity projectile = entityManager.Instantiate(sourcePrefab);

            float3 spawnPos = fromMuzzle.position;
            quaternion spawnRot = fromMuzzle.rotation;
            // Each muzzle's OWN forward — side muzzles authored with offset
            // local rotations produce spread for free.
            float3 velocity = ((float3)fromMuzzle.forward) * effectiveSpeed;

            // AddOrSet for all gameplay components — the prefab bake adds
            // these (via ProjectilePrefabSetupAuthoring), but using
            // AddOrSetComponent keeps this defensive against future prefab
            // configurations that skip the bake-side stamp.
            AddOrSetComponent(projectile, LocalTransform.FromPositionRotation(spawnPos, spawnRot));
            AddOrSetComponent(projectile, new ProjectileVelocity { Value = velocity });
            AddOrSetComponent(projectile, new ProjectileDamage   { Value = effectiveDamage });
            AddOrSetComponent(projectile, new Lifetime           { Remaining = projectileLifetime });

            if (instance != null && instance.IsValid)
            {
                AddOrSetComponent(projectile, new WeaponLevel
                {
                    Value       = (byte)Mathf.Clamp(instance.level, 1, 5),
                    EvolvedFlag = (byte)(instance.evolved ? 1 : 0),
                });
                AddOrSetComponent(projectile, new WeaponRarity
                {
                    Value = (byte)instance.rarity,
                });
            }

            if (entityManager.HasComponent<ProjectileSource>(projectile))
                entityManager.SetComponentData(projectile, new ProjectileSource { WeaponId = idForSource });
            else
                entityManager.AddComponentData(projectile, new ProjectileSource { WeaponId = idForSource });
        }

        // ─── Muzzle flash (M9 PR A) ───────────────────────────────────────

        /// <summary>
        /// Spawn the muzzle-flash GameObject from a specific muzzle Transform.
        /// Visual picked from <see cref="ElementVfxLibrary"/> by the weapon's
        /// currently-coupled <see cref="ElementId"/>. Scaled by
        /// <c>weaponData.muzzleFlashScale</c>.
        ///
        /// One-shot — auto-destroys after the longest particle system's
        /// duration so we don't leak GameObjects. Spawned UNPARENTED (so it
        /// stays where it was fired even as the weapon rotates to track the
        /// next target). Multi-muzzle weapons (shotgun) call this once per
        /// muzzle — 3 flashes spawn simultaneously.
        ///
        /// Silent no-op when:
        ///   - The library asset is missing (warned once via the library)
        ///   - The element has no flashPrefab authored
        ///   - The flash scale is zero (designer explicitly disabled it)
        /// </summary>
        private void SpawnMuzzleFlash(Transform fromMuzzle, WeaponInstanceData instance)
        {
            if (fromMuzzle == null) return;
            var lib = ElementVfxLibrary.Instance;
            if (lib == null) return;

            ElementId element = instance != null && instance.IsValid ? instance.element : ElementId.None;
            var entry = lib.Get(element);
            if (entry.flashPrefab == null) return;

            float scale = weaponData != null ? weaponData.muzzleFlashScale : 1f;
            if (scale <= 0f) return;

            // Spawn at muzzle, oriented along the muzzle's forward.
            GameObject flash = UnityEngine.Object.Instantiate(entry.flashPrefab, fromMuzzle.position, fromMuzzle.rotation);
            flash.transform.localScale = Vector3.one * scale;

            // Auto-cleanup. Use the longest particle system duration + a
            // small grace period so trailing particles finish before destroy.
            float maxDuration = 1.0f;
            foreach (var ps in flash.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                float dur = main.duration + main.startLifetime.constantMax;
                if (dur > maxDuration) maxDuration = dur;
            }
            UnityEngine.Object.Destroy(flash, maxDuration + 0.5f);
        }

        /// <summary>
        /// Helper: SetComponentData if the projectile already has the
        /// component (from prefab baking), else AddComponentData. Avoids
        /// duplicate-add exceptions from EntityManager.
        /// </summary>
        private void AddOrSetComponent<T>(Entity entity, T value) where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(entity))
                entityManager.SetComponentData(entity, value);
            else
                entityManager.AddComponentData(entity, value);
        }
    }
}

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
        [Tooltip("Optional muzzle/barrel transform — projectiles spawn from here. Falls back to the weapon's own transform.")]
        [SerializeField] private Transform muzzle;

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
        private Entity prefabEntity = Entity.Null;
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

        private bool ResolvePrefab()
        {
            if (prefabEntity != Entity.Null) return true;

            if (!dotsInitialized)
            {
                world = World.DefaultGameObjectInjectionWorld;
                if (world == null) return false;
                entityManager = world.EntityManager;
                dotsInitialized = true;
            }

            // Look up the projectile prefab via the ProjectilePrefabHolder
            // singleton, populated by ProjectilePrefabSetupAuthoring at bake
            // time. This indirection ensures the prefab was baked via
            // GetEntity(prefab, flags) — which sets up LinkedEntityGroup so
            // Instantiate duplicates the full hierarchy (visual children).
            EntityQuery query = entityManager.CreateEntityQuery(typeof(ProjectilePrefabHolder));

            if (query.CalculateEntityCount() == 0)
            {
                // SubScene not loaded yet, or ProjectilePrefabSetupAuthoring not configured.
                return false;
            }

            ProjectilePrefabHolder holder = query.GetSingleton<ProjectilePrefabHolder>();
            if (holder.Value == Entity.Null)
            {
                Debug.LogWarning("[WeaponFiring] ProjectilePrefabHolder.Value is Entity.Null — did you forget to assign the projectilePrefab on ProjectilePrefabSetupAuthoring?");
                return false;
            }

            prefabEntity = holder.Value;
            return true;
        }

        // ─── Fire ─────────────────────────────────────────────────────────

        private void Fire(WeaponInstanceData instance)
        {
            Entity projectile = entityManager.Instantiate(prefabEntity);

            float3 spawnPos = muzzle.position;
            quaternion spawnRot = muzzle.rotation;

            float effectiveSpeed  = GetEffectiveProjectileSpeed(instance);
            float effectiveDamage = GetEffectiveDamage(instance);

            float3 velocity = ((float3)muzzle.forward) * effectiveSpeed;

            entityManager.SetComponentData(projectile, LocalTransform.FromPositionRotation(spawnPos, spawnRot));
            entityManager.SetComponentData(projectile, new ProjectileVelocity { Value = velocity });
            entityManager.SetComponentData(projectile, new ProjectileDamage   { Value = effectiveDamage });
            entityManager.SetComponentData(projectile, new Lifetime           { Remaining = projectileLifetime });

            // Stamp WeaponLevel + WeaponRarity for downstream Burst consumers.
            // ProjectileCollisionSystem doesn't currently read these — damage
            // is pre-baked above — but having them on the projectile means
            // future systems (e.g., per-rarity-tier hit effects, music-side
            // projectile tracking) can read level/rarity without a Mono lookup.
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

            // Attribute the projectile to its source weapon. ProjectileCollisionSystem
            // reads this on hit to enqueue a DamageHitReport for PerWeaponStatsTracker.
            // Priority: inspector weaponData.equipmentId → loadout weaponData.equipmentId
            // → cached _weaponIdFixed (from inspector weaponId or GO name). This way
            // the projectile is attributed under the SAME equipmentId the HUD slot
            // looks up — without that match, GetStats(weaponId) returns null and
            // the slot shows no DPS / hits / kills.
            FixedString64Bytes idForSource = _weaponIdFixed;
            var resolvedData = ResolveWeaponData(instance);
            if (resolvedData != null && !string.IsNullOrEmpty(resolvedData.equipmentId))
            {
                idForSource = new FixedString64Bytes(resolvedData.equipmentId);
            }
            if (entityManager.HasComponent<ProjectileSource>(projectile))
                entityManager.SetComponentData(projectile, new ProjectileSource { WeaponId = idForSource });
            else
                entityManager.AddComponentData(projectile, new ProjectileSource { WeaponId = idForSource });

            if (verboseLogging)
            {
                string lvl = instance != null ? instance.level.ToString() + (instance.evolved ? "E" : "") : "?";
                string rar = instance != null ? instance.rarity.ToString() : "?";
                Debug.Log($"<color=cyan>[WeaponFiring]</color> Slot {slotIndex} '{idForSource}' fired — L{lvl} {rar} dmg={effectiveDamage:F1} spd={effectiveSpeed:F1}.");
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

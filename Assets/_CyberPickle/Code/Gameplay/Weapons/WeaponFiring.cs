// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponFiring.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Auto-fires entity-based projectiles at the WeaponTargeting's current
// target. Lives MonoBehaviour-side (one per weapon), but spawns ECS-side
// projectiles for runtime performance.
//
// 2026-05-11 refactor (M9 PR G2 — PHASE-LOCK TO MUSIC GRID):
//   - Replaced the per-weapon float cooldown ticker (which drifted
//     between weapons via FP error, no-target gating, mid-cycle rate
//     changes, and resume-from-pause phase offsets) with a subscription
//     to MusicConductor.OnSubdivision.
//   - Every shot is now sampled against the master beat grid:
//
//       totalSubdivs = barCount × beatsPerBar × conductor.SubdivisionsPerBeat
//       fireCells    = weaponData.GetFireCellsForLevel(level, totalSubdivs)
//       fire when    = fireCells.Contains(conductor.CurrentSubdivision % totalSubdivs)
//
//     ALL weapons sampling the same grid coincide at every common multiple
//     of their intervals → mathematically cannot drift. Pistol firing 8/bar
//     and sniper firing 2/bar share fire cells at {0, 8} every bar forever.
//
//   - Pause / level-up / mid-run weapon adds: conductor handles all of
//     these correctly (clock preserved through pauses, new subscribers
//     pick up the next aligned tick). Nothing local to compensate for.
//
//   - Fallback Time.deltaTime cooldown is RETAINED for scene-test setups
//     where MusicConductor.Instance is null (editor preview, one-weapon
//     isolated test). Won't phase-lock to anything, but lets the weapon
//     fire so the test scene is usable.
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

        [Tooltip("Loadout slot this WeaponFiring component represents. 0 = North axis (typical for the starting weapon); 1..3 = drafted weapons. The runtime reads the matching WeaponInstanceData from WeaponLoadoutRuntime.GetSlot(slotIndex) on each fire. Designers can author this for fixed scene-test setups; PlayerLoadoutLoader overrides it via SetSlotIndex when spawning dynamically.")]
        [SerializeField, Range(0, WeaponLoadoutRuntime.MaxSlots - 1)] private int slotIndex = 0;

        /// <summary>
        /// Override the slot/axis index at runtime. Called by
        /// PlayerLoadoutLoader after Instantiate-ing the weapon prefab so
        /// each spawned weapon reads its OWN loadout axis (otherwise every
        /// dynamically-spawned weapon would inherit the prefab's authored
        /// slotIndex = 0 and they'd all read the same axis's instance data).
        /// </summary>
        public void SetSlotIndex(int idx)
        {
            slotIndex = Mathf.Clamp(idx, 0, WeaponLoadoutRuntime.MaxSlots - 1);
        }

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
        private FixedString64Bytes _weaponIdFixed;

        private World world;
        private EntityManager entityManager;
        private Entity prefabEntity = Entity.Null;        // legacy single-prefab cache
        private Entity prefabRegistryEntity = Entity.Null; // entity holding the per-element buffer
        private bool dotsInitialized;

        // Grid-locked firing (M9 PR G2). Subscribed to MusicConductor
        // .OnSubdivision; HandleSubdivision tests if the current grid tick
        // is one of our pattern's fire cells and shoots if so. Cells are
        // cached per (level, totalSubdivs) tuple; recomputed lazily when
        // either changes (level-up, BPM change wouldn't change cells —
        // subdivision count is BPM-independent — but conductor grid
        // resolution change would).
        private bool _gridSubscribed;
        private int[] _fireCellsCache;
        private int _cachedLevel = -1;
        private int _cachedTotalSubdivs = -1;

        // Fallback ticker — used ONLY when MusicConductor.Instance is null
        // (scene-test setups). Same drift-prone behavior as the pre-PR-G2
        // code, but the only path that exists when the conductor is absent.
        private float _fallbackCooldown;

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

        // ─── Subscription lifecycle ───────────────────────────────────────

        private void OnEnable()
        {
            // PlayerLoadoutLoader spawns weapon prefabs after the conductor
            // exists in the Game scene boot order. Subscribe to the grid;
            // if the conductor is absent (scene-test, editor preview), the
            // fallback ticker in Update() handles firing — desyncs but works.
            var conductor = MusicConductor.Instance;
            if (conductor != null)
            {
                conductor.OnSubdivision += HandleSubdivision;
                _gridSubscribed = true;
            }
        }

        private void OnDisable()
        {
            if (!_gridSubscribed) return;
            var conductor = MusicConductor.Instance;
            if (conductor != null)
                conductor.OnSubdivision -= HandleSubdivision;
            _gridSubscribed = false;
        }

        // ─── Grid-locked tick (the main firing path) ─────────────────────

        /// <summary>
        /// Fires once per master grid subdivision. Tests whether the
        /// conductor's current subdivision is one of this weapon's pattern
        /// fire-cells; shoots if so. Phase-locked across all weapons
        /// because they all sample the same monotonic CurrentSubdivision
        /// counter.
        /// </summary>
        private void HandleSubdivision()
        {
            if (!targeting.HasTarget) return;
            if (!ResolvePrefab()) return;

            var conductor = MusicConductor.Instance;
            if (conductor == null) return; // shouldn't happen — we'd have unsubscribed

            var instance = GetCurrentInstance();
            var data = ResolveWeaponData(instance);
            if (data == null) return;

            int subdivPerBeat = conductor.SubdivisionsPerBeat;
            int totalSubdivs = data.GetTotalSubdivisions(subdivPerBeat);
            if (totalSubdivs <= 0) return;

            int level = (instance != null && instance.IsValid) ? instance.level : 1;
            EnsureFireCellsCache(data, level, totalSubdivs);
            if (_fireCellsCache == null || _fireCellsCache.Length == 0) return;

            // Phase within the pattern. CurrentSubdivision is the
            // conductor's monotonic counter since RunStart — modulo our
            // pattern length gives our position in the loop.
            int phase = conductor.CurrentSubdivision % totalSubdivs;

            // Cells are sorted ascending; linear scan is fine for
            // typical cell counts (≤64 in default L1..L5 config).
            for (int i = 0; i < _fireCellsCache.Length; i++)
            {
                int cell = _fireCellsCache[i];
                if (cell == phase)
                {
                    Fire(instance);
                    return;
                }
                if (cell > phase) return; // sorted — no further match possible
            }
        }

        /// <summary>
        /// Rebuilds the fire-cell index list when (level, totalSubdivs)
        /// changes. Otherwise reuses the cached array — no per-tick
        /// allocation. Triggers naturally on level-up (instance.level
        /// changes) and on conductor grid-resolution changes (rare).
        /// </summary>
        private void EnsureFireCellsCache(WeaponData data, int level, int totalSubdivs)
        {
            if (_fireCellsCache != null
                && _cachedLevel == level
                && _cachedTotalSubdivs == totalSubdivs)
                return;

            _fireCellsCache = data.GetFireCellsForLevel(level, totalSubdivs);
            _cachedLevel = level;
            _cachedTotalSubdivs = totalSubdivs;
        }

        // ─── Fallback ticker (no MusicConductor — scene-test only) ──────

        private void Update()
        {
            // Grid path handles firing when subscribed; nothing to do here.
            if (_gridSubscribed) return;

            // No conductor available — fall back to the legacy cooldown
            // model so isolated scene tests still work. Won't be
            // phase-locked to anything, but a single weapon by itself
            // doesn't need to be.
            _fallbackCooldown -= Time.deltaTime;
            if (_fallbackCooldown > 0f) return;
            if (!targeting.HasTarget) return;
            if (!ResolvePrefab()) return;

            var instance = GetCurrentInstance();
            Fire(instance);

            float effectiveRate = GetEffectiveFireRate(instance);
            _fallbackCooldown = 1f / Mathf.Max(0.01f, effectiveRate);
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
        /// Effective fire rate (shots/sec). Pulls from the pattern-driven
        /// formula on <see cref="WeaponData"/>: active-cells per level / pattern
        /// duration at the current global BPM (read from MusicConductor).
        /// Falls back to the inspector <c>fireRate</c> only when no
        /// <see cref="WeaponData"/> is wired (scene-test setups).
        ///
        /// Returns 0 when WeaponData exists but has no pattern authored —
        /// indicates a misconfigured weapon. We log a warning once so it's
        /// visible without spamming the console every frame.
        /// </summary>
        private bool _warnedNoPattern;
        private float GetEffectiveFireRate(WeaponInstanceData instance)
        {
            var data = ResolveWeaponData(instance);
            if (data != null)
            {
                int level = (instance != null && instance.IsValid) ? instance.level : 1;
                float rate = data.GetFireRateForLevel(level);
                if (rate <= 0f && !_warnedNoPattern)
                {
                    Debug.LogWarning(
                        $"[WeaponFiring] '{data.displayName}' has no activeCellsPerLevel " +
                        $"authored — fire rate = 0. Author the pattern in the inspector " +
                        $"or this weapon will never fire.");
                    _warnedNoPattern = true;
                }
                return rate;
            }
            return fireRate;
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

            // Resolve trajectory and ballistic parameters up-front. Default
            // = Straight (no gravity, no tumble, fixed speed along muzzle.forward).
            // Parabolic computes a launch velocity that lands at the targeting
            // system's locked target after WeaponData.flightBeats × 60/BPM
            // seconds — naturally puts the impact on the snare beat
            // (assuming the weapon's activeCellsPerLevel pattern places
            // fires on the kick beats).
            var resolvedData = ResolveWeaponData(instance);
            var trajectory = resolvedData != null ? resolvedData.trajectory : ProjectileTrajectory.Straight;

            float3 velocity;
            float lifetime = projectileLifetime;
            bool isParabolic = trajectory == ProjectileTrajectory.Parabolic;
            float3 gravityAccel = float3.zero;

            if (isParabolic && resolvedData != null)
            {
                // Parabolic: target lookup → shared v0 math on WeaponData.
                // Same helper used by WeaponTargeting for the visual aim,
                // so the weapon's gun model points along the actual launch
                // direction (instead of resting flat).
                float flightTime = resolvedData.GetParabolicFlightTimeSeconds();

                Vector3 target;
                if (targeting != null && targeting.HasTarget)
                {
                    // Target the ENEMY'S full 3D position — feet/ground
                    // level. This is the OPPOSITE of the bullet fix:
                    // bullets snap-XZ-preserve-Y so they hit at chest;
                    // grenades go all the way down to the ground so the
                    // explosion happens on the floor where it belongs.
                    // The parabola arcs from chest (spawnPos) up to apex,
                    // then DOWN to the ground at target.xz.
                    target = targeting.TargetPosition;
                }
                else
                {
                    // No target — throw forward toward the ground at a
                    // moderate distance so the grenade still lands somewhere
                    // visible. Aim slightly downward so the parabola actually
                    // descends to ground level.
                    Vector3 forwardLanding = (Vector3)spawnPos + fromMuzzle.forward * Mathf.Max(1f, effectiveSpeed) * flightTime;
                    forwardLanding.y = 0f; // ground
                    target = forwardLanding;
                }

                Vector3 v0 = WeaponData.ComputeParabolicLaunchVelocity((Vector3)spawnPos, target, flightTime);
                velocity = v0;
                gravityAccel = new float3(0f, -WeaponData.ParabolicGravityMagnitude, 0f);

                // Lifetime = exactly flightTime. ProjectileExplosionSystem
                // detonates the grenade the tick its Lifetime hits zero —
                // rhythm-locked to the snare beat (= fire-beat + flightBeats).
                // No proximity-collision involvement; the grenade is a
                // PURE TIMED BOMB. Whether it visually passes over enemies
                // mid-arc is irrelevant — it commits to its scheduled
                // detonation.
                lifetime = flightTime;

                // Initial rotation = aim direction. Tumble takes over per-tick.
                if (math.lengthsq(velocity) > 0.0001f)
                    spawnRot = quaternion.LookRotation(math.normalize(velocity), math.up());
            }
            else
            {
                // Straight: existing behavior — velocity along the muzzle's
                // OWN forward at fixed speed. Side muzzles authored with
                // offset local rotations produce spread for free.
                velocity = ((float3)fromMuzzle.forward) * effectiveSpeed;
            }

            // AddOrSet for all gameplay components. The user's element
            // prefabs (Hovl etc.) are pure visuals — no authoring on them.
            // The SubScene baker (ProjectilePrefabSetupAuthoring) can't
            // stamp these onto the prefab entity (Unity baker isolation
            // forbids modifying entities owned by a different authoring),
            // so we add them per-Instantiate here. The first shot from a
            // freshly-baked prefab causes a chunk migration; later shots
            // hit the same archetype and don't migrate. Acceptable cost
            // at our projectile counts (peak ~100 in flight).
            AddOrSetComponent(projectile, LocalTransform.FromPositionRotation(spawnPos, spawnRot));
            AddOrSetComponent(projectile, new ProjectileVelocity { Value = velocity });
            AddOrSetComponent(projectile, new ProjectileDamage   { Value = effectiveDamage });
            AddOrSetComponent(projectile, new Lifetime           { Remaining = lifetime });

            // Parabolic-only components: gravity (drags the arc down),
            // tumble (visual spin), AoE (explosion-style damage on impact).
            if (isParabolic && resolvedData != null)
            {
                AddOrSetComponent(projectile, new ProjectileGravity { Acceleration = gravityAccel });

                Vector3 tumbleDeg = resolvedData.tumbleRateDegreesPerSecond;
                float3 tumbleRad = new float3(
                    Mathf.Deg2Rad * tumbleDeg.x,
                    Mathf.Deg2Rad * tumbleDeg.y,
                    Mathf.Deg2Rad * tumbleDeg.z);
                AddOrSetComponent(projectile, new ProjectileTumble { AnglesPerSecondRad = tumbleRad });

                float aoeRadius = Mathf.Max(0.1f, resolvedData.baseAreaOfEffect);
                AddOrSetComponent(projectile, new ProjectileAoE { Radius = aoeRadius });

                Debug.Log($"<color=yellow>[WeaponFiring]</color> Parabolic launch: weapon='{resolvedData.displayName}' lifetime={lifetime:F2}s AoE radius={aoeRadius:F1}m. ProjectileExplosionSystem should detonate on Lifetime expiry.");
            }
            else if (resolvedData != null && resolvedData.trajectory == ProjectileTrajectory.Parabolic)
            {
                Debug.LogWarning($"[WeaponFiring] Weapon '{resolvedData.displayName}' has trajectory=Parabolic but isParabolic={isParabolic}, resolvedData null={resolvedData == null}. AoE will NOT be stamped → grenade will use proximity collision (the wrong path).");
            }
            else
            {
                // Belt-and-braces: ensure no stale parabolic components
                // linger from a previous Instantiate of the same archetype.
                if (entityManager.HasComponent<ProjectileGravity>(projectile))
                    entityManager.RemoveComponent<ProjectileGravity>(projectile);
                if (entityManager.HasComponent<ProjectileTumble>(projectile))
                    entityManager.RemoveComponent<ProjectileTumble>(projectile);
                if (entityManager.HasComponent<ProjectileAoE>(projectile))
                    entityManager.RemoveComponent<ProjectileAoE>(projectile);
            }

            // Tag + HitVFX ref required by the projectile systems.
            // ProjectileMovementSystem queries WithAll<ProjectileTag>;
            // ProjectileCollisionSystem queries WithAll<HitVFXPrefabRef>
            // (and null-checks the ref before spawning). Both must be on
            // the entity before the next ECS frame.
            if (!entityManager.HasComponent<ProjectileTag>(projectile))
                entityManager.AddComponent<ProjectileTag>(projectile);
            AddOrSetComponent(projectile, new HitVFXPrefabRef { Value = Entity.Null });

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
                // Stamp the element AT FIRE TIME — projectiles in flight
                // keep their original element even if the axis's power-up
                // changes mid-flight (per M9 PR F design).
                AddOrSetComponent(projectile, new WeaponElement
                {
                    Value = (byte)instance.element,
                });
            }

            // Pierce (M9 PR D). Computed from (level, rarity) via WeaponData.
            // Non-pierce weapons (basePierceCount == 0) get Remaining = 0
            // and the collision system destroys-on-first-hit as before —
            // back-compat, no per-weapon scene migration needed. Pierce
            // weapons ALSO get an empty hit-targets buffer so the collision
            // system can dedup repeat hits on the same enemy across frames.
            int pierceCount = 0;
            if (resolvedData != null && instance != null && instance.IsValid)
                pierceCount = resolvedData.GetPierceCountForLevelAndRarity(instance.level, instance.rarity);

            // AoE projectiles ignore pierce — you can't pierce + explode.
            if (isParabolic) pierceCount = 0;

            AddOrSetComponent(projectile, new ProjectilePierce
            {
                Remaining = (byte)Mathf.Clamp(pierceCount, 0, 255),
            });

            if (pierceCount > 0)
            {
                // Freshly-instantiated projectiles don't carry the buffer
                // from the prefab (we never authored it there). Add on
                // first use; on subsequent same-archetype shots, clear the
                // existing buffer so we don't leak last shot's hit list.
                if (!entityManager.HasBuffer<ProjectileHitTarget>(projectile))
                    entityManager.AddBuffer<ProjectileHitTarget>(projectile);
                else
                    entityManager.GetBuffer<ProjectileHitTarget>(projectile).Clear();
            }

            // (Trail-linger seconds USED to be stamped here from
            // WeaponData.trailLingerSeconds. That field was removed — the
            // fade duration is now read directly from the projectile
            // PREFAB by ProjectileFadeOutSystem on the first dying-frame:
            // CyberPickleProjectileVisual.GetTotalFadeDuration() for
            // hybrid prefabs, or the longest particle lifetime in the
            // Companion hierarchy for legacy fallback. The prefab owns
            // its own timing because a weapon can fire many element-
            // coupled variants with different particle timings.)

            if (entityManager.HasComponent<ProjectileSource>(projectile))
                entityManager.SetComponentData(projectile, new ProjectileSource { WeaponId = idForSource });
            else
                entityManager.AddComponentData(projectile, new ProjectileSource { WeaponId = idForSource });

            // Hybrid visual tag (M9 follow-up). If the spawned projectile's
            // Companion GameObject carries a CyberPickleProjectileVisual,
            // tag the entity so DamageReportDrainSystem suppresses the
            // parallel HitVfxApplier.Play call — Hovl's authored hit GO
            // (fired by the script's OnHit) is the only hit visual.
            // Prevents the "double hit at slightly different positions"
            // weird behavior reported pre-fix.
            if (entityManager.HasComponent<UnityEngine.Transform>(projectile))
            {
                var companionT = entityManager.GetComponentObject<UnityEngine.Transform>(projectile);
                if (companionT != null && companionT.GetComponent<CyberPickleProjectileVisual>() != null)
                {
                    if (!entityManager.HasComponent<ProjectileHasHybridVisual>(projectile))
                        entityManager.AddComponent<ProjectileHasHybridVisual>(projectile);
                }
            }
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

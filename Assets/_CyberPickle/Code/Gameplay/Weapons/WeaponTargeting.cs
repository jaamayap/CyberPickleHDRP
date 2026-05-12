// File: Assets/_CyberPickle/Code/Gameplay/Weapons/WeaponTargeting.cs
// Namespace: CyberPickle.Gameplay.Weapons
//
// Auto-targeting for weapons. Lives MonoBehaviour-side (one per weapon).
// Two responsibilities, on different cadences:
//
//   1. Re-target on every BEAT (via MusicConductor.OnBeat). Queries the
//      ECS world for enemies in range and picks one per the weapon's
//      TargetingStrategy.
//
//   2. Rotate toward the cached target every FRAME (Update). Aiming
//      doesn't need to be beat-locked — only target SELECTION does.
//
// 2026-05-11 refactor (M9 PR C):
//   - Range + strategy now sourced from WeaponData via the loadout instance.
//     Inspector fields remain as fallbacks for scene-test setups.
//   - Re-PICKING (deciding WHICH enemy to lock onto) moved off the per-frame
//     Update tick onto OnBeat (~2× per second at 128 BPM). Per the M9 perf
//     audit, this was finding #1 — the O(N) (Closest) and O(N²)
//     (MostInLine, DensestCluster) selection scans for 4 weapons at 60Hz
//     was the largest CPU hotspot. Beat-throttling cuts it ~30×.
//   - Re-TRACKING (re-reading the locked enemy's current position) STAYS
//     per-frame. This is the bug we shipped briefly: between beats the
//     weapon was aiming at the enemy's BEAT-CAPTURED position, which at
//     slow BPMs let the enemy walk meters away from the aim point. Now
//     we read the live LocalTransform every frame so the weapon tracks
//     fluidly between target-selection beats. Cost is one
//     GetComponentData per frame per weapon — trivial.
//   - Defensive re-pick: if the locked target dies or leaves range
//     between beats, re-pick immediately. Don't make the player wait
//     1+ seconds for the next beat to react.
//   - New strategies MostInLine (sniper) and DensestCluster (grenade)
//     added. Both are O(N²) — the beat throttle is what makes them viable.
//
// Scene-test fallback: if MusicConductor.Instance is null, Update() runs
// re-pick every frame (the original behavior). Single-weapon-in-isolation
// tests keep working.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Shop.Equipment.Data;

namespace CyberPickle.Gameplay.Weapons
{
    [DisallowMultipleComponent]
    public class WeaponTargeting : MonoBehaviour
    {
        [Header("Weapon Definition (preferred — overrides inspector fallbacks)")]
        [Tooltip("The weapon's design SO. When assigned, range + strategy come from here. Leave null only for scene-test setups; production wires it via the loadout (resolved at runtime through slotIndex).")]
        [SerializeField] private WeaponData weaponData;

        [Tooltip("Loadout axis this targeter represents. Mirrors WeaponFiring.slotIndex so we read the right loadout instance. Set automatically by PlayerLoadoutLoader on spawn — designers can pre-author for fixed scene-test setups.")]
        [SerializeField, Range(0, WeaponLoadoutRuntime.MaxSlots - 1)] private int slotIndex = 0;

        /// <summary>Set the slot index at runtime — called by PlayerLoadoutLoader after spawning the weapon prefab.</summary>
        public void SetSlotIndex(int idx)
        {
            slotIndex = Mathf.Clamp(idx, 0, WeaponLoadoutRuntime.MaxSlots - 1);
        }

        [Header("Fallback (used only when WeaponData is unresolved)")]
        [Tooltip("Maximum targeting range (world units). Fallback for scene-test setups where WeaponData isn't wired.")]
        [SerializeField] private float fallbackRange = 15f;

        [Tooltip("Targeting strategy fallback for scene-test setups where WeaponData isn't wired.")]
        [SerializeField] private TargetingStrategy fallbackStrategy = TargetingStrategy.Closest;

        [Tooltip("Cone half-angle (degrees) used by MostInLine when WeaponData isn't resolved. 8° matches the WeaponData default.")]
        [SerializeField, Range(1f, 45f)] private float fallbackConeHalfAngleDeg = 8f;

        [Tooltip("Cluster radius used by DensestCluster when WeaponData isn't resolved. 1.0 matches the WeaponData default for baseAreaOfEffect.")]
        [SerializeField, Min(0.1f)] private float fallbackClusterRadius = 1f;

        [Header("Aim")]
        [Tooltip("Degrees per second the weapon can rotate to face the target. 720 = 2 full turns/sec.")]
        [SerializeField] private float rotationSpeed = 720f;

        // ─── Public state ─────────────────────────────────────────────────

        /// <summary>The currently-locked enemy entity, or Entity.Null if no valid target in range.</summary>
        public Entity CurrentTarget { get; private set; } = Entity.Null;

        /// <summary>World-space position of the current target. Valid only when HasTarget is true.</summary>
        public Vector3 TargetPosition { get; private set; }

        public bool HasTarget => CurrentTarget != Entity.Null;

        // ─── Runtime state ────────────────────────────────────────────────

        private World world;
        private EntityManager entityManager;
        private EntityQuery enemyQuery;
        private bool initialized;
        private bool _beatSubscribed;

        // ─── Lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureWorld();
        }

        private void OnEnable()
        {
            EnsureWorld();

            // Subscribe to the beat clock. PlayerLoadoutLoader spawns weapons
            // after the conductor exists in the Game scene boot order. If the
            // conductor is null (scene-test, editor preview), the Update path
            // below falls back to per-frame re-targeting.
            var conductor = MusicConductor.Instance;
            if (conductor != null)
            {
                conductor.OnBeat += HandleBeat;
                _beatSubscribed = true;
                // Initial re-target so we have a target on frame 1 (otherwise
                // we'd wait up to one beat — ~0.5s at 128 BPM — to lock).
                FindBestTarget();
            }
        }

        private void OnDisable()
        {
            if (!_beatSubscribed) return;
            var conductor = MusicConductor.Instance;
            if (conductor != null) conductor.OnBeat -= HandleBeat;
            _beatSubscribed = false;
        }

        private void EnsureWorld()
        {
            if (initialized) return;
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            entityManager = world.EntityManager;
            // Exclude Dead entities so weapons don't lock onto fresh corpses.
            // Without this filter, the closest "enemy" is often a body that
            // just died at the player's feet.
            enemyQuery = entityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All  = new[] {
                    ComponentType.ReadOnly<EnemyTag>(),
                    ComponentType.ReadOnly<Health>(),
                    ComponentType.ReadOnly<LocalTransform>()
                },
                None = new[] {
                    ComponentType.ReadOnly<Dead>()
                }
            });
            initialized = true;
        }

        // ─── Tick (rotation every frame; re-targeting on beat) ───────────

        private void HandleBeat()
        {
            if (!initialized) EnsureWorld();
            if (!initialized) return;
            FindBestTarget();
        }

        private void Update()
        {
            if (!initialized) EnsureWorld();
            if (!initialized) return;

            if (!_beatSubscribed)
            {
                // Scene-test fallback (no conductor) — per-frame re-pick.
                FindBestTarget();
            }
            else
            {
                // Beat-throttled flow: re-read the locked target's LIVE
                // position so aim tracks fluidly between beat re-picks.
                // Also defensively re-picks if the target died, despawned,
                // or walked out of range — player shouldn't have to wait
                // for the next beat when their lock vanished.
                RefreshOrReacquireTarget();
            }

            RotateTowardTarget();
        }

        /// <summary>
        /// Between beat-triggered re-picks, re-read the current target's
        /// LIVE LocalTransform position so the rotation in
        /// <see cref="RotateTowardTarget"/> tracks the enemy fluidly.
        /// Without this, aim would stick to the position the enemy had at
        /// beat-time, drifting up to half-a-second of enemy movement at
        /// 128 BPM (or a full second at 60 BPM). Felt sluggish and
        /// inaccurate.
        ///
        /// Also triggers an immediate re-pick when:
        ///   • the locked entity no longer exists (despawned)
        ///   • the locked entity is now <see cref="Dead"/>
        ///   • the locked entity walked outside the weapon's range
        ///
        /// Per-frame cost: one GetComponentData<LocalTransform> per weapon
        /// + a sqrMagnitude compare. Trivial vs. the full FindBestTarget
        /// scan that used to run every frame.
        /// </summary>
        private void RefreshOrReacquireTarget()
        {
            if (CurrentTarget == Entity.Null)
            {
                // No current target. Don't full-scan here — beat handler
                // will pick one. (We could opportunistically scan when
                // idle, but the design pillar is "rhythm-locked target
                // SHIFTS"; the weapon firing pattern is also rhythm-locked
                // so missing one beat of fire-without-target is fine.)
                return;
            }

            // Defensive: entity might've been destroyed by enemy death
            // system / despawner between our last beat tick and now.
            if (!entityManager.Exists(CurrentTarget) ||
                entityManager.HasComponent<Dead>(CurrentTarget) ||
                !entityManager.HasComponent<LocalTransform>(CurrentTarget))
            {
                FindBestTarget();
                return;
            }

            // Read live position.
            var t = entityManager.GetComponentData<LocalTransform>(CurrentTarget);
            TargetPosition = t.Position;

            // Out of range? Lock is stale; re-pick now.
            float range = GetEffectiveRange();
            float rangeSq = range * range;
            float3 weaponPos = transform.position;
            if (math.distancesq(weaponPos, t.Position) > rangeSq)
            {
                FindBestTarget();
            }
        }

        // ─── Effective config resolution ─────────────────────────────────

        /// <summary>Resolve the WeaponData for this targeter. Inspector field wins; falls back to the loadout instance.</summary>
        private WeaponData ResolveWeaponData()
        {
            if (weaponData != null) return weaponData;
            var loadout = WeaponLoadoutRuntime.Instance;
            var instance = loadout?.GetSlot(slotIndex);
            return (instance != null && instance.IsValid) ? instance.weaponData : null;
        }

        private float              GetEffectiveRange()             { var d = ResolveWeaponData(); return d != null ? d.baseRange                : fallbackRange; }
        private TargetingStrategy  GetEffectiveStrategy()          { var d = ResolveWeaponData(); return d != null ? d.targetingStrategy        : fallbackStrategy; }
        private float              GetEffectiveConeHalfAngleDeg()  { var d = ResolveWeaponData(); return d != null ? d.targetingConeHalfAngleDeg: fallbackConeHalfAngleDeg; }
        private float              GetEffectiveClusterRadius()     { var d = ResolveWeaponData(); return d != null ? d.baseAreaOfEffect        : fallbackClusterRadius; }

        // ─── Target search ───────────────────────────────────────────────

        /// <summary>
        /// Scan all alive enemies, filter to those in range, and pick one
        /// per the active strategy. Caches positions + healths into Temp
        /// NativeArrays once per call — the O(N²) strategies (MostInLine,
        /// DensestCluster) re-read positions across the inner loop, so the
        /// cache avoids repeated GetComponentData calls.
        ///
        /// Allocator.Temp is auto-freed after 4 frames, but we Dispose
        /// explicitly to be tidy.
        /// </summary>
        private void FindBestTarget()
        {
            CurrentTarget = Entity.Null;
            if (enemyQuery.IsEmpty) return;

            float range = GetEffectiveRange();
            float rangeSq = range * range;
            TargetingStrategy strategy = GetEffectiveStrategy();
            float3 weaponPos = transform.position;

            NativeArray<Entity> enemies = enemyQuery.ToEntityArray(Allocator.Temp);
            int n = enemies.Length;

            var positions = new NativeArray<float3>(n, Allocator.Temp);
            var healths   = new NativeArray<float>(n, Allocator.Temp);
            var inRange   = new NativeArray<bool>(n, Allocator.Temp);

            for (int i = 0; i < n; i++)
            {
                LocalTransform t = entityManager.GetComponentData<LocalTransform>(enemies[i]);
                Health h = entityManager.GetComponentData<Health>(enemies[i]);
                positions[i] = t.Position;
                healths[i]   = h.Current;
                inRange[i]   = math.distancesq(weaponPos, t.Position) <= rangeSq;
            }

            int bestIdx = -1;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < n; i++)
            {
                if (!inRange[i]) continue;
                float score = ScoreCandidate(i, n, positions, healths, weaponPos, strategy);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                CurrentTarget = enemies[bestIdx];
                TargetPosition = positions[bestIdx];
            }

            positions.Dispose();
            healths.Dispose();
            inRange.Dispose();
            enemies.Dispose();
        }

        /// <summary>
        /// Score one candidate enemy under the active strategy. Higher score
        /// wins. Each branch returns a number where "better" maps cleanly
        /// to "larger" — Closest negates distance, Weakest negates HP, etc.
        /// MostInLine / DensestCluster use <c>count × 1000 - distSq</c>
        /// so the count dominates and distance is the tiebreaker.
        /// </summary>
        private float ScoreCandidate(int idx, int n,
                                     NativeArray<float3> positions,
                                     NativeArray<float> healths,
                                     float3 weaponPos,
                                     TargetingStrategy strategy)
        {
            switch (strategy)
            {
                case TargetingStrategy.Closest:
                    return -math.distancesq(weaponPos, positions[idx]);

                case TargetingStrategy.Weakest:
                    return -healths[idx];

                case TargetingStrategy.Strongest:
                    return healths[idx];

                case TargetingStrategy.MostInLine:
                    return ScoreMostInLine(idx, n, positions, weaponPos);

                case TargetingStrategy.DensestCluster:
                    return ScoreDensestCluster(idx, n, positions, weaponPos);

                default:
                    return -math.distancesq(weaponPos, positions[idx]);
            }
        }

        /// <summary>
        /// MostInLine score: count of OTHER enemies that lie within the
        /// narrow cone from weapon→candidate AND are farther from the
        /// weapon than the candidate (i.e., "behind" them from the weapon's
        /// POV). The first enemy a pierce shot hits is the candidate; the
        /// "behind" enemies are the ones the projectile chains through.
        ///
        /// Cone test via <c>dot(candDir, toOther/otherDist) ≥ cos(halfAngle)</c> —
        /// no acos call needed. The * 1000f multiplier ensures count
        /// dominates distance in the score.
        /// </summary>
        private float ScoreMostInLine(int candIdx, int n, NativeArray<float3> positions, float3 weaponPos)
        {
            float3 toCand = positions[candIdx] - weaponPos;
            float candDistSq = math.lengthsq(toCand);
            if (candDistSq < 0.0001f) return 0f;
            float candDist = math.sqrt(candDistSq);
            float3 candDir = toCand / candDist;

            float cosCone = math.cos(math.radians(GetEffectiveConeHalfAngleDeg()));

            int linedUp = 0;
            for (int j = 0; j < n; j++)
            {
                if (j == candIdx) continue;
                float3 toOther = positions[j] - weaponPos;
                float otherDistSq = math.lengthsq(toOther);
                if (otherDistSq <= candDistSq) continue; // must be behind candidate
                float otherDist = math.sqrt(otherDistSq);
                float cosAngle = math.dot(candDir, toOther) / otherDist;
                if (cosAngle >= cosCone) linedUp++;
            }

            return linedUp * 1000f - candDistSq;
        }

        /// <summary>
        /// DensestCluster score: count of OTHER enemies within
        /// <c>baseAreaOfEffect</c> radius of the candidate. The grenade
        /// will explode AT the candidate; this scoring picks the candidate
        /// whose neighborhood contains the most kills-per-shot.
        /// Tiebreaker: closer candidate wins (less travel time, less time
        /// for enemies to disperse before the lob lands).
        /// </summary>
        private float ScoreDensestCluster(int candIdx, int n, NativeArray<float3> positions, float3 weaponPos)
        {
            float3 candPos = positions[candIdx];
            float aoeRadius = GetEffectiveClusterRadius();
            float aoeRadiusSq = aoeRadius * aoeRadius;

            int cluster = 0;
            for (int j = 0; j < n; j++)
            {
                if (j == candIdx) continue;
                if (math.distancesq(positions[j], candPos) <= aoeRadiusSq) cluster++;
            }

            float candDistSq = math.distancesq(weaponPos, candPos);
            return cluster * 1000f - candDistSq;
        }

        // ─── Aim ─────────────────────────────────────────────────────────

        private void RotateTowardTarget()
        {
            if (!HasTarget) return;

            // Aim direction depends on trajectory:
            //   - Straight  → horizontal aim (toTarget with Y zeroed).
            //                 Pistol/shotgun/sniper point flat toward enemy.
            //   - Parabolic → along the LAUNCH VELOCITY vector. The
            //                 grenade launcher tilts UP at an angle that
            //                 varies with target distance + flight time +
            //                 gravity (computed via the shared helper on
            //                 WeaponData). Without this, the grenade
            //                 launcher rests flat while the projectile
            //                 lobs through the air — visually disconnected.
            Vector3 aimDir;
            var data = ResolveWeaponData();
            if (data != null && data.trajectory == ProjectileTrajectory.Parabolic)
            {
                float flightTime = data.GetParabolicFlightTimeSeconds();
                aimDir = WeaponData.ComputeParabolicLaunchVelocity(transform.position, TargetPosition, flightTime);
            }
            else
            {
                aimDir = TargetPosition - transform.position;
                aimDir.y = 0f;  // keep direct-aim weapons level on the XZ plane
            }

            if (aimDir.sqrMagnitude < 0.0001f) return;

            Quaternion targetRot = Quaternion.LookRotation(aimDir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }
}

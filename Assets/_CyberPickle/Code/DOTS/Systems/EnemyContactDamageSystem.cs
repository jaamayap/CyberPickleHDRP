// File: Assets/_CyberPickle/Code/DOTS/Systems/EnemyContactDamageSystem.cs
// Namespace: CyberPickle.DOTS.Systems
//
// Burst-compiled ISystem that accumulates contact damage from enemies
// overlapping the player. Runs each frame:
//
//   1. Read player position from PlayerPositionData singleton.
//   2. For each living enemy entity with ContactDamage:
//      - Compute XZ distance to player.
//      - If within ContactRadius, += ContactDamage × dt to PendingDamage.
//   3. Bridge drains PendingDamage and applies via PlayerHealth.TakeDamage
//      (which handles Defense reduction + i-frames).
//
// Why DPS-style (× dt) instead of discrete hits:
//   - Simpler — no per-enemy hit-cooldown component to track.
//   - PlayerHealth's i-frames already prevent multi-enemy pile-up — only
//     the first hit per i-frame window lands; the rest accumulate but
//     are discarded by TakeDamage's invuln check.
//   - Matches Vampire Survivors / Halls of Torment style where contact
//     damage feels like a steady drain when stuck in a swarm.
//
// Excludes Dead enemies — corpses don't deal damage. Bosses + special
// enemies use the same ContactDamage component, no special-case code.
//
// 2026-05-12: also excludes enemies WITHOUT HasVisualTag. The visual
// GameObject is instantiated by EnemyVisualBindingSystem one phase later
// (PresentationSystemGroup), so newly-spawned enemies have a 1-frame
// "ghost" window where the entity has collision/damage but no visual.
// At 60 FPS that's 16ms — barely noticeable. At 4 FPS during a hitch,
// that's 250ms of invisible damage, and the user reports being killed
// by enemies they never saw. Gating on HasVisualTag is a tiny grace
// period that closes this window: an enemy can only damage you once
// you've had a chance to see it.
//
// Performance: O(n) over enemies. With 500 enemies, ~5000 ops/frame at
// 60fps ≈ trivial. If swarm sizes grow past ~2000, swap the foreach
// for an IJobEntity parallel write into a per-thread accumulator —
// same logic, different scheduling.

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyContactDamageSystem : ISystem
    {
        /// <summary>Distance from player at which an enemy is considered "in contact" — slightly larger than the player+enemy capsule radii sum to feel right.</summary>
        private const float ContactRadius = 1.2f;

        private static readonly float ContactRadiusSq = ContactRadius * ContactRadius;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerPositionData>();
            state.RequireForUpdate<PlayerHealthData>();
            state.RequireForUpdate<EnemyTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Skip damage processing entirely if the player is already dead.
            var healthEntity = SystemAPI.GetSingletonEntity<PlayerHealthData>();
            var health = SystemAPI.GetComponent<PlayerHealthData>(healthEntity);
            if (!health.IsAlive) return;

            float3 playerPos = SystemAPI.GetSingleton<PlayerPositionData>().Position;
            float dt = SystemAPI.Time.DeltaTime;

            float damageThisFrame = 0f;

            foreach (var (transform, contact) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<ContactDamage>>()
                              .WithAll<EnemyTag, HasVisualTag>()
                              .WithNone<Dead>())
            {
                float3 toPlayer = playerPos - transform.ValueRO.Position;
                toPlayer.y = 0f;
                float distSq = math.lengthsq(toPlayer);
                if (distSq > ContactRadiusSq) continue;

                damageThisFrame += contact.ValueRO.Value * dt;
            }

            if (damageThisFrame > 0f)
            {
                health.PendingDamage += damageThisFrame;
                SystemAPI.SetComponent(healthEntity, health);
            }
        }
    }
}

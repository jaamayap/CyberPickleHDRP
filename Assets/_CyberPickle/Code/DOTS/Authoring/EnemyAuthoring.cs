// File: Assets/_CyberPickle/Code/DOTS/Authoring/EnemyAuthoring.cs
// Namespace: CyberPickle.DOTS.Authoring
//
// Authors an enemy entity from a designer-facing EnemyData ScriptableObject.
// Place this component on the visual prefab; drag the matching EnemyData
// asset into the data field. At bake time, the Baker reads ONLY the SO
// fields whose runtime systems exist today and copies them into IComponentData.
// As future milestones land (drops, AI variants, defenses, etc.), this
// Baker grows by ~1 line per system to bake the additional component.
//
// All other EnemyData fields (defenses, AI behavior, VFX, audio, boss
// phases, etc.) stay on the SO until their consuming systems ship — no
// perf cost, no migration, just one form per enemy filled out by designers.
//
// Why DependsOn(authoring.data): tells Unity's bake pipeline to re-bake
// this entity whenever the SO changes. Without it, designers tweaking
// EnemyData values wouldn't see updates in entities until manual bake.

using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;
using CyberPickle.Gameplay.Enemies;

namespace CyberPickle.DOTS.Authoring
{
    public class EnemyAuthoring : MonoBehaviour
    {
        [Tooltip("EnemyData ScriptableObject defining this enemy's stats, behavior, drops, etc.")]
        public EnemyData data;

        public class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                if (authoring.data == null)
                {
                    Debug.LogWarning($"[EnemyAuthoring] '{authoring.name}' has no EnemyData assigned — entity will be baked WITHOUT enemy components, will not appear as an enemy at runtime.", authoring);
                    return;
                }

                // Tell the bake system to invalidate this entity if the SO changes.
                DependsOn(authoring.data);

                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                // ─── Components consumed by Milestone 5 systems (active now) ───

                AddComponent<EnemyTag>(entity);

                AddComponent(entity, new Health
                {
                    Current = authoring.data.maxHealth,
                    Max     = authoring.data.maxHealth
                });

                AddComponent(entity, new MoveSpeed
                {
                    Value = authoring.data.moveSpeed
                });

                // ─── Components baked now, consumed by later milestones ───

                // ContactDamage — read by M6 contact-damage-to-player system.
                // Cheap to bake now (4 bytes); avoids re-bake when M6 ships.
                AddComponent(entity, new ContactDamage
                {
                    Value = authoring.data.contactDamage
                });

                // EnemyTypeId — used by drops (M6) and prefab registry lookup.
                // Stable hash from Animator.StringToHash, Burst-friendly.
                AddComponent(entity, new EnemyTypeId
                {
                    Value = authoring.data.GetIdHash()
                });

                // Boss marker — drives M? HUD, music, drops scaling, phase logic.
                if (authoring.data.isBoss)
                {
                    AddComponent<BossTag>(entity);
                }

                // ─── NOT baked yet (deferred to system-ship milestones): ───
                // Defenses (armor, knockback/stun resistance, element mults) → M7
                // AI behavior (aiPattern, aggroRange, attackRange, ...)       → M8
                // VFX prefab refs (deathVfx, hitReactionVfx, ...)            → M9
                // Audio event names                                           → M10
                // Loot drop table                                             → M7
            }
        }
    }
}

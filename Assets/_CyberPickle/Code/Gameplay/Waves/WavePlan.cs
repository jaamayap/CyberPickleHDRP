// File: Assets/_CyberPickle/Code/Gameplay/Waves/WavePlan.cs
// Namespace: CyberPickle.Gameplay.Waves
//
// Designer-facing ScriptableObject describing a run's spawn schedule.
//
// Model — overlapping continuous spawn directives keyed to time windows.
// At any given moment of the run, MANY directives may be active. The
// WaveSpawner sums their rates to produce the per-frame swarm composition.
//
//   Example: a 6-minute run might be authored as:
//     - zombie_1   1.0/sec  start=0:00  end=∞
//     - zombie_1   2.0/sec  start=2:00  end=∞    (rate doubles after 2 min)
//     - mutant     0.2/sec  start=1:30  end=∞
//     - boss_a     1 once   start=5:00            (single-shot via burst flag)
//
// Designers add rows to the list in the inspector — no code needed for
// new compositions. Difficulty curve is just "add a higher-rate row at
// later start time."

using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Gameplay.Enemies;

namespace CyberPickle.Gameplay.Waves
{
    [CreateAssetMenu(fileName = "NewWavePlan", menuName = "CyberPickle/Waves/Wave Plan", order = 1)]
    public class WavePlan : ScriptableObject
    {
        [Header("Spawn Directives")]
        [Tooltip("Each row is a continuous spawn directive — 'spawn this enemy at this rate during this time window'. Multiple directives can be active simultaneously and their rates sum.")]
        public List<SpawnDirective> directives = new List<SpawnDirective>();

        [Header("Plan-Level")]
        [Tooltip("Total run length (seconds). Directives' endTime values clamp to this.")]
        [Min(10f)] public float planDuration = 600f; // 10 minutes default

        [Tooltip("If true, restart from t=0 when the plan duration is reached. If false, spawning stops when the last directive ends.")]
        public bool loop = false;
    }

    /// <summary>
    /// One spawn directive — "spawn this enemy at this rate, during this
    /// time window." Multiple directives compose; the WaveSpawner runs all
    /// active directives in parallel.
    /// </summary>
    [System.Serializable]
    public class SpawnDirective
    {
        [Tooltip("Designer label for this row, shown in inspector. Has no effect on runtime.")]
        public string label = "directive";

        [Tooltip("Which enemy to spawn. Looked up in EnemyPrefabRegistryAuthoring by enemyId hash.")]
        public EnemyData enemyType;

        [Tooltip("Spawn rate while active (enemies per second). Use 0.5 for 'one every 2 seconds', 5 for 'panic phase'.")]
        [Min(0f)] public float spawnsPerSecond = 1f;

        [Tooltip("Run-time (seconds) at which this directive becomes active.")]
        [Min(0f)] public float startTime = 0f;

        [Tooltip("Run-time (seconds) at which this directive deactivates. Set to a very large number for 'forever'. Clamped to plan duration.")]
        [Min(0f)] public float endTime = 99999f;

        [Tooltip("If true, this directive spawns its rate × duration ONCE as a single burst at startTime, instead of spawning continuously. Useful for boss spawns or scripted reinforcements. The 'rate × duration' is rounded to int.")]
        public bool oneShotBurst = false;

        [Header("Spawn Placement")]
        [Tooltip("Distance from the player at which enemies appear. Should be slightly larger than the camera frustum so they walk in from off-screen.")]
        [Min(1f)] public float spawnRadius = 20f;

        [Tooltip("Random radial jitter so spawns don't land on a perfect circle.")]
        [Min(0f)] public float radialJitter = 2f;

        [Tooltip("Per-instance MoveSpeed multiplier (±this value). Decorrelates pack arrival timing.")]
        [Range(0f, 0.5f)] public float speedJitter = 0.25f;
    }
}

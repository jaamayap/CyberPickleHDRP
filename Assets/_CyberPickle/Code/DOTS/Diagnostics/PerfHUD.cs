// File: Assets/_CyberPickle/Code/DOTS/Diagnostics/PerfHUD.cs
// Namespace: CyberPickle.DOTS.Diagnostics
//
// Lightweight on-screen perf overlay for in-build measurement.
//
// Shows:
//   - FPS (rolling avg over `sampleSize` frames)
//   - Frame time in ms
//   - 1% low FPS (worst 1% of recent frames — the "feel" metric)
//   - Living enemy count (entities with EnemyTag, no Dead)
//   - Dead enemy count (entities with EnemyTag + Dead)
//
// Toggle with F1. Designed to work in standalone builds (no editor-only
// APIs). Drop a single PerfHUD GameObject into Game.unity and forget it.
//
// Why custom HUD instead of Unity's built-in Stats overlay: the built-in
// overlay only works in editor / development builds and is inconsistent
// across versions. This one is reliable, comparable across runs, and
// captures 1% lows which Unity's overlay doesn't show.

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using CyberPickle.DOTS.Components;

namespace CyberPickle.DOTS.Diagnostics
{
    [DisallowMultipleComponent]
    public class PerfHUD : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Toggle the HUD on / off with this key.")]
        public KeyCode toggleKey = KeyCode.F1;

        [Tooltip("Top-left corner position of the HUD in pixels.")]
        public Vector2 position = new Vector2(20, 20);

        [Tooltip("HUD font size.")]
        [Min(8)] public int fontSize = 14;

        [Header("Sampling")]
        [Tooltip("Number of frames kept in the rolling window for avg + 1% low.")]
        [Range(60, 1000)] public int sampleSize = 240;

        [Tooltip("Refresh the displayed numbers every N frames so the text doesn't flicker.")]
        [Range(1, 30)] public int updateInterval = 6;

        [Header("Visibility")]
        [Tooltip("Show the HUD on Start. Toggle key still works at runtime.")]
        public bool visibleOnStart = true;

        // ─── Runtime state ───
        private bool visible;
        private GUIStyle bgStyle;
        private GUIStyle textStyle;

        private readonly List<float> frameTimes = new List<float>(1024);
        private int framesSinceUpdate;

        private float displayFps;
        private float displayFrameTimeMs;
        private float displayLowFps;
        private int displayLivingEnemies;
        private int displayDeadEnemies;

        private EntityQuery livingQuery;
        private EntityQuery deadQuery;
        private bool queriesReady;

        private void Awake()
        {
            visible = visibleOnStart;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                visible = !visible;

            float dt = Time.unscaledDeltaTime;
            frameTimes.Add(dt);
            if (frameTimes.Count > sampleSize)
                frameTimes.RemoveAt(0);

            framesSinceUpdate++;
            if (framesSinceUpdate >= updateInterval)
            {
                framesSinceUpdate = 0;
                Recompute();
            }
        }

        private void EnsureQueries()
        {
            if (queriesReady) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            livingQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All  = new[] { ComponentType.ReadOnly<EnemyTag>() },
                None = new[] { ComponentType.ReadOnly<Dead>() }
            });
            deadQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<EnemyTag>(),
                ComponentType.ReadOnly<Dead>());
            queriesReady = true;
        }

        private void Recompute()
        {
            // Rolling-window average frame time.
            float total = 0f;
            for (int i = 0; i < frameTimes.Count; i++) total += frameTimes[i];
            float avg = frameTimes.Count > 0 ? total / frameTimes.Count : 0f;

            displayFrameTimeMs = avg * 1000f;
            displayFps = avg > 0f ? 1f / avg : 0f;

            // 1% low — sort, take the worst 1% of frames, average them.
            if (frameTimes.Count >= 100)
            {
                var sorted = new List<float>(frameTimes);
                sorted.Sort();
                int n = Mathf.Max(1, sorted.Count / 100);
                float worstTotal = 0f;
                for (int i = sorted.Count - n; i < sorted.Count; i++)
                    worstTotal += sorted[i];
                float worstAvg = worstTotal / n;
                displayLowFps = worstAvg > 0f ? 1f / worstAvg : 0f;
            }

            EnsureQueries();
            if (queriesReady)
            {
                displayLivingEnemies = livingQuery.CalculateEntityCount();
                displayDeadEnemies = deadQuery.CalculateEntityCount();
            }
        }

        private void OnGUI()
        {
            if (!visible) return;

            if (textStyle == null)
            {
                bgStyle = new GUIStyle(GUI.skin.box);
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = fontSize,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(10, 10, 6, 6)
                };
                textStyle.normal.textColor = Color.white;
            }

            // Color-code FPS for at-a-glance reads.
            string fpsColor =
                displayFps >= 55f ? "#7CFC7C" : // green
                displayFps >= 30f ? "#FFD27A" : // amber
                                    "#FF7A7A";  // red

            string text =
                $"<color={fpsColor}><b>{displayFps,5:0.0} FPS</b></color>  " +
                $"({displayFrameTimeMs:0.00} ms)\n" +
                $"1% LOW:  {displayLowFps,5:0.0} FPS\n" +
                $"\n" +
                $"Alive:   {displayLivingEnemies}\n" +
                $"Dead:    {displayDeadEnemies}\n" +
                $"Total:   {displayLivingEnemies + displayDeadEnemies}\n" +
                $"\n" +
                $"<size={fontSize - 2}>[{toggleKey}] toggle</size>";

            float h = (fontSize + 5) * 9;
            Rect rect = new Rect(position.x, position.y, 320f, h);

            GUI.Box(rect, GUIContent.none, bgStyle);

            // GUIStyle.richText must be true for color/bold tags.
            var richStyle = new GUIStyle(textStyle) { richText = true };
            GUI.Label(rect, text, richStyle);
        }
    }
}

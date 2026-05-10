// File: Assets/_CyberPickle/Code/UI/HUD/PlayerStatsPanel.cs
// Namespace: CyberPickle.UI.HUD
//
// Container for all 14 player-stat rows. On Awake, instantiates one
// StatRowUI per PlayerStatType and lays them out as children. On
// MusicEvent.RunStart, binds to PlayerStats.OnStatsChanged and pushes
// refreshes to every child row.
//
// Why dynamic instantiation (vs 14 hand-authored children): avoids
// drift when stats are added/renamed and lets the layout group do
// the heavy lifting. The row prefab is the only manual piece.

using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.Stats;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class PlayerStatsPanel : MonoBehaviour
    {
        [Header("Row prefab")]
        [Tooltip("StatRowUI prefab spawned 14 times — one per PlayerStatType. Required.")]
        [SerializeField] private StatRowUI rowPrefab;

        [Tooltip("Parent under which spawned rows are placed. Should have a VerticalLayoutGroup. Required.")]
        [SerializeField] private RectTransform rowParent;

        [Header("Diagnostics")]
        [SerializeField] private bool verbose;

        [Header("Visibility (driven by CharacterIconWidget)")]
        [Tooltip("CanvasGroup used to fade the panel in/out. Auto-discovered if left null. Required (added by [RequireComponent]).")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Tooltip("Seconds for the fade-in animation when the panel is shown. Uses unscaled time so it works during pause states.")]
        [Min(0f)] [SerializeField] private float fadeDuration = 0.15f;

        private readonly List<StatRowUI> _rows = new List<StatRowUI>(PlayerStatTypeMeta.Count);
        private PlayerStats _stats;
        private bool _subscribed;

        // Visibility state.
        private float _targetAlpha = 0f;  // 0=hidden, 1=visible
        private bool _isLocked = false;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            // Hidden by default — CharacterIconWidget controls when we appear.
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            BuildRows();
        }

        /// <summary>
        /// Show or hide the panel. Called by CharacterIconWidget on hover/click.
        /// `locked=true` means the panel persists past mouse-exit (Europa-style
        /// click-or-hold-to-lock); the field is informational only — visibility
        /// itself is driven purely by the show parameter.
        ///
        /// Defensive lazy-init on canvasGroup: Awake order between this panel
        /// and CharacterIconWidget isn't guaranteed, so the widget may call
        /// SetVisible before our Awake has populated the field. Lazy-resolve
        /// here to avoid an UnassignedReferenceException.
        /// </summary>
        public void SetVisible(bool show, bool locked)
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) return; // GameObject deleted or component missing — bail.

            _targetAlpha = show ? 1f : 0f;
            _isLocked = locked;
            // Block raycasts only when fully visible — when fading out we want
            // mouse events to start passing through immediately.
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;
        }

        /// <summary>True iff the panel is currently in its "shown" state.</summary>
        public bool IsVisible => _targetAlpha > 0.5f;

        /// <summary>True iff the panel is locked (won't auto-hide on mouse-exit).</summary>
        public bool IsLocked => _isLocked;

        private void Update()
        {
            // Smooth alpha tween to target. Cheap, works during pause via unscaled time.
            if (canvasGroup == null) return;
            float current = canvasGroup.alpha;
            if (Mathf.Abs(current - _targetAlpha) < 0.001f) return;
            float step = (fadeDuration > 0f) ? Time.unscaledDeltaTime / fadeDuration : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(current, _targetAlpha, step);
        }

        private void BuildRows()
        {
            if (rowPrefab == null || rowParent == null)
            {
                Debug.LogError("[PlayerStatsPanel] rowPrefab and rowParent must both be assigned.");
                return;
            }

            for (int i = 0; i < PlayerStatTypeMeta.Count; i++)
            {
                var row = Instantiate(rowPrefab, rowParent);
                row.SetStatType((PlayerStatType)i);
                row.gameObject.name = $"StatRow_{(PlayerStatType)i}";
                _rows.Add(row);
            }
        }

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
            UnsubscribeFromStats();
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            if (type == MusicEvent.RunStart) BindToStats();
        }

        private void BindToStats()
        {
            if (_stats == null) _stats = FindFirstObjectByType<PlayerStats>();
            if (_stats == null)
            {
                Debug.LogError("[PlayerStatsPanel] No PlayerStats found at RunStart. Player did not spawn?");
                return;
            }

            if (!_subscribed)
            {
                _stats.OnStatsChanged += HandleStatsChanged;
                _subscribed = true;
            }

            // Initial paint — push current values to every row.
            RefreshAll();

            if (verbose) Debug.Log($"[PlayerStatsPanel] Bound to PlayerStats on '{_stats.name}'.");
        }

        private void UnsubscribeFromStats()
        {
            if (_subscribed && _stats != null)
            {
                _stats.OnStatsChanged -= HandleStatsChanged;
                _subscribed = false;
            }
        }

        private void HandleStatsChanged(PlayerStatType _)
        {
            // PlayerStats fires per-stat events for single-stat updates and
            // a default(=MaxHealth) event for bulk changes. Subscribers are
            // expected to refresh whatever they care about — so we just
            // refresh all rows. 14 rows × ~1 dictionary lookup each = trivial.
            RefreshAll();
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rows[i].Refresh(_stats);
            }
        }
    }
}

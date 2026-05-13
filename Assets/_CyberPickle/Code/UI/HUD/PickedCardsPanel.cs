// File: Assets/_CyberPickle/Code/UI/HUD/PickedCardsPanel.cs
// Namespace: CyberPickle.UI.HUD
//
// HUD widget showing the cards picked this run, latest first. One
// PickedCardEntryUI is instantiated per pick; the list grows as the
// player levels up.
//
// Subscribes to LevelUpCoordinator.OnCardApplied for the canonical
// "card was picked AND applied" signal. Resets on MusicEvent.RunStart
// (the dedicated event RunStateManager fires for fresh-run starts).

using System.Collections.Generic;
using UnityEngine;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Progression;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class PickedCardsPanel : MonoBehaviour
    {
        [Header("Entry prefab")]
        [Tooltip("PickedCardEntryUI prefab spawned per picked card. Required.")]
        [SerializeField] private PickedCardEntryUI entryPrefab;

        [Tooltip("Parent under which spawned entries are placed. Should have a layout group. Required.")]
        [SerializeField] private RectTransform entryParent;

        [Header("Behavior")]
        [Tooltip("If true, newer entries appear at the top (latest-first). If false, appended to the bottom.")]
        [SerializeField] private bool latestFirst = true;

        [Tooltip("Maximum entries kept on screen. Older entries are destroyed when the cap is hit. 0 = unlimited.")]
        [Min(0)] [SerializeField] private int maxEntries = 0;

        [Header("Diagnostics")]
        [SerializeField] private bool verbose;

        private readonly List<PickedCardEntryUI> _entries = new List<PickedCardEntryUI>(32);
        private LevelUpCoordinator _coord;
        private bool _bound;

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
            UnbindCoordinator();
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            switch (type)
            {
                case MusicEvent.RunStart:
                    BindToCoordinator();
                    ClearEntries();
                    break;
            }
        }

        private void BindToCoordinator()
        {
            // LevelUpCoordinator is a plain MonoBehaviour (not Manager<T>),
            // so we discover it via the scene at RunStart — same pattern as
            // HealthBarUI binding to PlayerHealth.
            if (_coord == null) _coord = FindFirstObjectByType<LevelUpCoordinator>();
            if (_coord == null)
            {
                Debug.LogError("[PickedCardsPanel] No LevelUpCoordinator found in scene at RunStart.");
                return;
            }
            if (!_bound)
            {
                _coord.OnCardApplied += HandleCardApplied;
                _bound = true;
            }
            if (verbose) Debug.Log("[PickedCardsPanel] Bound to LevelUpCoordinator.");
        }

        private void UnbindCoordinator()
        {
            if (_bound && _coord != null)
            {
                _coord.OnCardApplied -= HandleCardApplied;
                _bound = false;
            }
        }

        private void HandleCardApplied(DraftedCard card)
        {
            if (!card.IsValid || entryPrefab == null || entryParent == null) return;

            var entry = Instantiate(entryPrefab, entryParent);
            entry.Bind(card);
            entry.gameObject.name = $"PickedCard_{card.source.cardId}";

            if (latestFirst) entry.transform.SetAsFirstSibling();
            // else: default — appended last via instantiate

            _entries.Add(entry);

            if (maxEntries > 0 && _entries.Count > maxEntries)
            {
                // Remove oldest first; with latestFirst layout that's the
                // last child in the hierarchy (or the oldest in our list).
                var oldest = _entries[0];
                _entries.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }

        private void ClearEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i] != null) Destroy(_entries[i].gameObject);
            _entries.Clear();
        }
    }
}

// File: Assets/_CyberPickle/Code/UI/Tooltip/TooltipController.cs
// Namespace: CyberPickle.UI.Tooltip
//
// Europa-Universalis-style hover-tooltip controller. Owns the lifecycle
// of every tooltip on screen — both the transient (mouse-following) one
// while the user is hovering, and any number of "locked" ones the user
// has pinned by hovering for >lockDelay seconds OR by clicking the
// hoverable element directly.
//
// Interaction model:
//   - Mouse enters a HoverableElement → transient tooltip appears,
//     follows mouse with offset.
//   - User keeps mouse stationary on the element for `lockDelay` seconds
//     OR clicks the element → tooltip "locks": position freezes, lock
//     icon shows, tooltip persists even when mouse leaves the element.
//   - User clicks the element again → unlocks (toggle).
//   - User clicks anywhere outside any locked tooltip + its anchor →
//     ALL locked tooltips dismiss. Clicking on another HoverableElement
//     instead locks that element's tooltip too (multi-lock supported).
//
// Why during gameplay (not just pause): in CyberPickle the player drives
// movement with WASD and aim is auto-targeted, so the mouse is otherwise
// idle. Wiring the mouse to inspect HUD content gives that hand a job
// during normal play, not just during the level-up modal.
//
// Scene-bound Manager<T>: tooltips are run-scoped UI; persisting across
// scenes would carry stale anchor references.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using CyberPickle.Core.Management;

namespace CyberPickle.UI.Tooltip
{
    /// <summary>
    /// Plain-data record for tooltip content. Built by HoverableElement
    /// subclasses on demand; the controller turns this into a TooltipPanel.
    /// </summary>
    public struct TooltipContent
    {
        public string title;
        public string body;

        public bool IsEmpty => string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body);

        public static TooltipContent Empty => new TooltipContent();
    }

    [DisallowMultipleComponent]
    public class TooltipController : Manager<TooltipController>
    {
        [Header("Prefab + Parent")]
        [Tooltip("Prefab spawned per tooltip instance. Must have a TooltipPanel component on its root.")]
        [SerializeField] private TooltipPanel panelPrefab;

        [Tooltip("RectTransform under which tooltips spawn. Should be at the top of the HUD canvas's child order so tooltips render above everything else. Required.")]
        [SerializeField] private RectTransform tooltipParent;

        [Tooltip("Optional full-screen invisible Image (with raycast target) that catches clicks outside any tooltip / hoverable. If assigned, its OnClick fires DismissAllLocked. If null, click-outside detection runs in Update via Input.GetMouseButtonDown — works fine but slightly less precise.")]
        [SerializeField] private RectTransform clickCatcher;

        [Header("Behavior")]
        [Tooltip("Seconds the mouse must rest on a hoverable before its tooltip auto-locks. Set to a large number (e.g. 9999) to require an explicit click to lock. Default 3.0s — long enough to read the tooltip's transient content before it locks and shifts position.")]
        [Min(0f)] [SerializeField] private float lockDelay = 3.0f;

        [Tooltip("Pixel offset between mouse cursor and the tooltip's top-left corner while transient (following mouse).")]
        [SerializeField] private Vector2 mouseOffset = new Vector2(20f, -20f);

        [Tooltip("Padding (px) kept between the locked tooltip and screen edges. The tooltip is clamped so it can't render off-screen.")]
        [Min(0f)] [SerializeField] private float screenEdgePadding = 8f;

        [Tooltip("Visual gap (px) between the tooltip and its anchor element when smart-placed (above / left of the anchor). Prevents the tooltip's edge from touching the anchor's edge.")]
        [Min(0f)] [SerializeField] private float anchorGap = 12f;

        [Header("Diagnostics")]
        [SerializeField] private bool verbose = false;

        // ─── State ────────────────────────────────────────────────────────

        // The single transient tooltip (follows mouse while hovering).
        private TooltipPanel _transient;

        // The element currently hovered (or null if none).
        private HoverableElement _currentHover;

        // Time the current hover has been stable (resets on enter, advances each Update).
        private float _hoverTime;

        // Locked tooltips, keyed by their anchor element. Multiple may be open at once.
        private readonly Dictionary<HoverableElement, TooltipPanel> _locked
            = new Dictionary<HoverableElement, TooltipPanel>();

        // Cached list for click-outside iteration without modifying-while-enumerating.
        private readonly List<HoverableElement> _scratchAnchors = new List<HoverableElement>();

        // Live-content refresh: rebuild content of every visible tooltip
        // every <see cref="ContentRefreshInterval"/> seconds so DPS / kills /
        // stat values update while the tooltip is shown without the user
        // having to re-hover.
        private const float ContentRefreshInterval = 0.5f;
        private float _contentRefreshTimer;

        // ─── Manager lifecycle ────────────────────────────────────────────

        // Scene-bound: tooltips reference scene-only HoverableElements.
        protected override bool PersistAcrossScenes => false;

        protected override void OnManagerEnabled()
        {
            base.OnManagerEnabled();
            // We don't subscribe to MusicEventBus here — TooltipController
            // is purely UI-driven, fed by HoverableElement registration.
        }

        // ─── Public API called by HoverableElement ────────────────────────

        public void OnHoverEnter(HoverableElement element)
        {
            if (element == null) return;
            _currentHover = element;
            _hoverTime = 0f;

            // If we already have this element locked, no need for a transient
            // (the locked one is the source of truth). Else show transient.
            if (!_locked.ContainsKey(element))
            {
                ShowTransientFor(element);
            }

            if (verbose) Debug.Log($"[TooltipController] HoverEnter '{element.name}'.");
        }

        public void OnHoverExit(HoverableElement element)
        {
            if (element == null) return;

            // If exiting the element we're tracking, hide the transient.
            if (_currentHover == element)
            {
                _currentHover = null;
                _hoverTime = 0f;
                HideTransient();
            }

            if (verbose) Debug.Log($"[TooltipController] HoverExit '{element.name}'.");
        }

        public void OnHoverClick(HoverableElement element)
        {
            if (element == null) return;

            // Non-lockable hoverables ignore clicks — tooltip stays purely
            // hover-driven. (Their content is static, so locking adds no value.)
            if (!element.IsLockable && !_locked.ContainsKey(element)) return;

            // Click toggles lock: locked → unlock; unlocked → lock.
            if (_locked.ContainsKey(element))
            {
                UnlockFor(element);
            }
            else
            {
                LockFor(element);
            }
        }

        /// <summary>
        /// Dismiss every locked tooltip. Wire this to the click-catcher
        /// overlay's OnClick if you have one; otherwise it fires from the
        /// Update-side click-outside check.
        /// </summary>
        public void DismissAllLocked()
        {
            if (_locked.Count == 0) return;

            _scratchAnchors.Clear();
            foreach (var kv in _locked) _scratchAnchors.Add(kv.Key);
            for (int i = 0; i < _scratchAnchors.Count; i++)
            {
                UnlockFor(_scratchAnchors[i]);
            }

            if (verbose) Debug.Log("[TooltipController] Dismissed all locked tooltips.");
        }

        /// <summary>How many tooltips are currently locked.</summary>
        public int LockedCount => _locked.Count;

        // ─── Update — auto-lock + mouse-follow + click-outside ────────────

        private void Update()
        {
            // Hover-to-auto-lock — only for lockable hoverables. Non-lockable
            // ones (stat rows, picked-card entries) keep the transient flowing
            // with the mouse and skip both the progress bar and the auto-lock.
            if (_currentHover != null && _currentHover.IsLockable && !_locked.ContainsKey(_currentHover))
            {
                _hoverTime += Time.unscaledDeltaTime;

                if (_transient != null && lockDelay > 0f)
                {
                    _transient.SetLockProgress(Mathf.Clamp01(_hoverTime / lockDelay));
                }

                if (_hoverTime >= lockDelay)
                {
                    LockFor(_currentHover);
                }
            }
            else if (_transient != null)
            {
                // Non-lockable: ensure the progress bar is hidden / empty.
                _transient.SetLockProgress(0f);
            }

            // The transient is anchored to its element (no mouse-follow) so
            // the tooltip position is stable while the user reads. Re-position
            // each frame in case the anchor moved (rare, but defensive).
            if (_transient != null && _transient.gameObject.activeSelf && _currentHover != null && !_locked.ContainsKey(_currentHover))
            {
                RepositionTooltipFor(_transient, _currentHover);
            }

            // Click-outside detection (only matters if anything is locked).
            // Skipped when a click-catcher overlay is wired (it'll call DismissAllLocked directly).
            if (clickCatcher == null && _locked.Count > 0 && Input.GetMouseButtonDown(0))
            {
                CheckClickOutside();
            }

            // Live-content refresh. Every ~500ms, rebuild the content of
            // every visible tooltip from its anchor's BuildContent() so
            // live values (DPS, kills, hit count, current stat values
            // mid-modifier-application) update without re-hover.
            _contentRefreshTimer -= Time.unscaledDeltaTime;
            if (_contentRefreshTimer <= 0f)
            {
                _contentRefreshTimer = ContentRefreshInterval;
                RefreshAllVisibleContent();
            }
        }

        private void RefreshAllVisibleContent()
        {
            // Transient: refresh while shown and hovered. Re-run smart
            // placement after content change because content may have
            // grown / shrunk.
            if (_transient != null && _transient.gameObject.activeSelf && _currentHover != null)
            {
                var content = _currentHover.BuildContent();
                if (!content.IsEmpty)
                {
                    _transient.SetContent(content);
                    RepositionTooltipFor(_transient, _currentHover);
                }
            }

            // Locked: refresh every locked panel from its anchor + reposition.
            foreach (var kv in _locked)
            {
                var anchor = kv.Key;
                var panel  = kv.Value;
                if (anchor == null || panel == null) continue;
                var content = anchor.BuildContent();
                if (!content.IsEmpty)
                {
                    panel.SetContent(content);
                    RepositionTooltipFor(panel, anchor);
                }
            }
        }

        /// <summary>
        /// Force a layout rebuild on the panel (so any content-driven
        /// resize via ContentSizeFitter is applied), then run smart
        /// placement using the actual rendered size. Final clamp keeps
        /// the panel inside the screen.
        /// </summary>
        private void RepositionTooltipFor(TooltipPanel panel, HoverableElement anchor)
        {
            if (panel == null || anchor == null) return;
            var rt = (RectTransform)panel.transform;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Vector2 size = panel.GetSize();
            Vector2 pos = GetAnchorScreenPosition(anchor, size);
            pos = ClampToScreen(pos, size);
            panel.SetScreenPosition(pos);
        }

        private void CheckClickOutside()
        {
            // If the pointer is currently over a UI element, see whether that
            // element is inside any of our locked anchors / tooltip panels.
            if (EventSystem.current == null) { DismissAllLocked(); return; }

            var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new List<RaycastResult>(8);
            EventSystem.current.RaycastAll(pointer, results);

            foreach (var hit in results)
            {
                var hitTransform = hit.gameObject.transform;
                // Is this hit a descendant of any locked anchor? If so, click was
                // inside a still-relevant element — don't dismiss.
                foreach (var kv in _locked)
                {
                    var anchor = kv.Key;
                    var panel  = kv.Value;
                    if (anchor != null && IsDescendant(hitTransform, anchor.transform)) return;
                    if (panel  != null && IsDescendant(hitTransform, panel.transform))  return;
                }
            }

            // No raycast hit landed inside an anchor or tooltip → dismiss all locked.
            DismissAllLocked();
        }

        private static bool IsDescendant(Transform candidate, Transform potentialAncestor)
        {
            for (var t = candidate; t != null; t = t.parent)
                if (t == potentialAncestor) return true;
            return false;
        }

        // ─── Lock / unlock plumbing ───────────────────────────────────────

        private void LockFor(HoverableElement element)
        {
            if (panelPrefab == null || tooltipParent == null)
            {
                Debug.LogWarning("[TooltipController] Lock requested but panelPrefab/tooltipParent not assigned.");
                return;
            }
            if (_locked.ContainsKey(element)) return;

            var content = element.BuildContent();
            if (content.IsEmpty) return;

            var panel = Instantiate(panelPrefab, tooltipParent);
            panel.SetContent(content);
            panel.SetLocked(true);
            panel.AnchorElement = element;
            // Force layout rebuild + smart placement using actual
            // post-resize dimensions. Above-anchor placement keeps the
            // tooltip's bottom edge fixed (with anchorGap) so it grows
            // UPWARD when content is taller — never overlaps the anchor.
            RepositionTooltipFor(panel, element);

            _locked[element] = panel;

            // Hide the transient if it was for this same element — the locked
            // panel takes over.
            if (_currentHover == element) HideTransient();

            if (verbose) Debug.Log($"[TooltipController] Locked tooltip for '{element.name}'. Total locked: {_locked.Count}.");
        }

        private void UnlockFor(HoverableElement element)
        {
            if (!_locked.TryGetValue(element, out var panel)) return;
            _locked.Remove(element);
            if (panel != null) Destroy(panel.gameObject);
            if (verbose) Debug.Log($"[TooltipController] Unlocked tooltip for '{element.name}'. Total locked: {_locked.Count}.");
        }

        // ─── Transient plumbing ───────────────────────────────────────────

        private void ShowTransientFor(HoverableElement element)
        {
            if (panelPrefab == null || tooltipParent == null) return;

            var content = element.BuildContent();
            if (content.IsEmpty) { HideTransient(); return; }

            if (_transient == null)
            {
                _transient = Instantiate(panelPrefab, tooltipParent);
            }
            _transient.SetContent(content);
            _transient.SetLocked(false);
            _transient.SetLockProgress(0f);
            _transient.gameObject.SetActive(true);
            _transient.AnchorElement = element;
            // Position via smart placement so the tooltip lands at the
            // anchor's edge (above for bottom-screen anchors, left for
            // right-screen anchors) and not where the mouse is. Forces
            // a layout rebuild first so we read the correct content-driven
            // size when content overflows the fixed panel.
            RepositionTooltipFor(_transient, element);
        }

        private void HideTransient()
        {
            if (_transient != null) _transient.gameObject.SetActive(false);
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Position the locked tooltip relative to its anchor element with
        /// smart edge avoidance. Default: tooltip's top-left at anchor's
        /// top-right (extends down-right). If that would overflow the
        /// screen bottom, flips so the tooltip's BOTTOM-LEFT is at anchor's
        /// TOP-LEFT (extends UP-right). If overflow right, places left of
        /// anchor (extends down-LEFT). Final <see cref="ClampToScreen"/>
        /// is still applied as a safety net by the caller.
        /// </summary>
        private Vector2 GetAnchorScreenPosition(HoverableElement element, Vector2 tooltipSize)
        {
            var rt = element.transform as RectTransform;
            if (rt == null) return (Vector2)Input.mousePosition + mouseOffset;

            // Anchor screen-space corners.
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var canvas = tooltipParent != null ? tooltipParent.GetComponentInParent<Canvas>() : null;
            bool isOverlay = canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay;
            var cam = canvas != null ? canvas.worldCamera : null;

            Vector2 ToScreen(Vector3 worldCorner)
            {
                if (isOverlay) return worldCorner;
                return RectTransformUtility.WorldToScreenPoint(cam, worldCorner);
            }

            Vector2 anchorBL = ToScreen(corners[0]);  // bottom-left
            Vector2 anchorTL = ToScreen(corners[1]);  // top-left
            Vector2 anchorTR = ToScreen(corners[2]);  // top-right
            Vector2 anchorBR = ToScreen(corners[3]);  // bottom-right

            float w = Screen.width;
            float h = Screen.height;
            float pad = screenEdgePadding;

            // Default: tooltip top-left at anchor TR (extends down-right).
            // With pivot (0,1): pos.x = TR.x, pos.y = TR.y; tooltip rect spans
            //   x: [pos.x, pos.x + size.x],  y: [pos.y - size.y, pos.y].
            Vector2 placeDownRight = new Vector2(anchorTR.x, anchorTR.y);
            bool overflowsBottomDR = placeDownRight.y - tooltipSize.y < pad;
            bool overflowsRightDR  = placeDownRight.x + tooltipSize.x > w - pad;

            // Try alternative placements based on overflow. anchorGap separates
            // the tooltip from the anchor visually so they don't touch.
            // 1) overflow bottom only → flip up
            // 2) overflow right only  → flip left
            // 3) overflow both        → flip up + left

            Vector2 chosen = placeDownRight;

            if (overflowsBottomDR && !overflowsRightDR)
            {
                // Place ABOVE anchor (extends up): bottom edge = anchor TOP - anchorGap.
                // Tooltip top-left pos.y = anchor.top + tooltipSize.y + anchorGap.
                chosen = new Vector2(anchorTL.x, anchorTL.y + tooltipSize.y + anchorGap);
            }
            else if (!overflowsBottomDR && overflowsRightDR)
            {
                // Place LEFT of anchor: right edge = anchor LEFT - anchorGap.
                // Tooltip top-left pos.x = anchor.left - tooltipSize.x - anchorGap.
                chosen = new Vector2(anchorTL.x - tooltipSize.x - anchorGap, anchorTR.y);
            }
            else if (overflowsBottomDR && overflowsRightDR)
            {
                // Place ABOVE-LEFT, both gaps applied.
                chosen = new Vector2(anchorTL.x - tooltipSize.x - anchorGap, anchorTL.y + tooltipSize.y + anchorGap);
            }

            return chosen;
        }

        /// <summary>
        /// Clamp a screen-space tooltip position so the tooltip's bounding
        /// box stays inside the screen with <see cref="screenEdgePadding"/> margin.
        /// Assumes the tooltip pivot is top-left (the project's TooltipPanel
        /// prefab uses pivot=(0,1)).
        /// </summary>
        private Vector2 ClampToScreen(Vector2 pos, Vector2 panelSize)
        {
            float w = Screen.width;
            float h = Screen.height;
            float pad = screenEdgePadding;

            // Right edge = pos.x + panelSize.x (pivot is top-left).
            if (pos.x + panelSize.x > w - pad) pos.x = w - panelSize.x - pad;
            if (pos.x < pad)                   pos.x = pad;

            // Top edge = pos.y; bottom edge = pos.y - panelSize.y.
            if (pos.y > h - pad)                pos.y = h - pad;
            if (pos.y - panelSize.y < pad)      pos.y = panelSize.y + pad;

            return pos;
        }
    }
}

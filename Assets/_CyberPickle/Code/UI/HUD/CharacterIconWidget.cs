// File: Assets/_CyberPickle/Code/UI/HUD/CharacterIconWidget.cs
// Namespace: CyberPickle.UI.HUD
//
// Top-left character portrait that controls visibility of the
// PlayerStatsPanel. The user hovers the icon → the panel slides in
// (visibility controlled via CanvasGroup alpha). After lockDelay
// seconds of stable hover OR on click, the panel "locks open" — stays
// visible after the mouse leaves the icon. Click anywhere outside
// (icon + panel) → panel closes.
//
// This is a sibling to the TooltipController system; the stats panel
// is a persistent panel, not a transient tooltip, so it has its own
// dedicated controller. Both share the same Europa-style hover/lock
// vocabulary so the UX feels consistent.
//
// Authoring:
//   - Drop this on a top-left UI GameObject with an Image (raycast
//     target) for the character portrait.
//   - Assign `statsPanel` to the PlayerStatsPanel scene reference.
//   - Optionally assign `gracePeriod` (default 200ms) — lets the user
//     move from icon down into the panel without the panel flickering
//     closed mid-transition.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class CharacterIconWidget : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Target panel")]
        [Tooltip("PlayerStatsPanel scene reference. The widget shows / hides this panel based on hover + click. Required.")]
        [SerializeField] private PlayerStatsPanel statsPanel;

        [Header("Behavior")]
        [Tooltip("Seconds of stable hover before the panel locks open. Matches TooltipController's lockDelay so UX feels consistent. Default 3.0s.")]
        [Min(0f)] [SerializeField] private float lockDelay = 3.0f;

        [Tooltip("Grace period (s) after pointer exits before the panel hides — lets the user move the mouse from the icon into the panel without the panel flickering closed. Default 0.2s.")]
        [Min(0f)] [SerializeField] private float gracePeriod = 0.2f;

        [Tooltip("Optional progress fill that visualizes lock-delay countdown. Image with Type=Filled. Hidden when locked. Mirrors TooltipPanel's lockProgressBar pattern.")]
        [SerializeField] private Image lockProgressBar;

        // ─── Runtime state ────────────────────────────────────────────────

        private bool _hovering;          // mouse is over the icon
        private bool _hoveringPanel;     // mouse moved into the stats panel itself
        private float _hoverTime;        // total hover time accumulated this session
        private float _exitGraceTimer;   // counts down once mouse exits both icon + panel
        private bool _locked;            // user clicked or held lockDelay → panel persists
        private bool _panelVisible;

        private void Awake()
        {
            // Hide the stats panel by default. Visible only on hover/lock.
            if (statsPanel != null) statsPanel.SetVisible(false, locked: false);
            UpdateProgressVisual(0f);
        }

        private void Update()
        {
            // Auto-lock countdown — only when hovering AND not yet locked.
            if (_hovering && !_locked && lockDelay > 0f)
            {
                _hoverTime += Time.unscaledDeltaTime;
                UpdateProgressVisual(_hoverTime / lockDelay);
                if (_hoverTime >= lockDelay)
                {
                    _locked = true;
                    if (statsPanel != null) statsPanel.SetVisible(true, locked: true);
                    UpdateProgressVisual(0f);
                }
            }

            // Grace-period decay when not hovering anything related.
            if (_panelVisible && !_locked && !_hovering && !_hoveringPanel)
            {
                _exitGraceTimer -= Time.unscaledDeltaTime;
                if (_exitGraceTimer <= 0f)
                {
                    HidePanel();
                }
            }

            // Click-outside detection while locked.
            if (_locked && Input.GetMouseButtonDown(0))
            {
                if (!IsPointerOverIconOrPanel())
                {
                    Unlock();
                }
            }

            // Track whether mouse is currently over the panel — needed for
            // grace-period logic. Cheap RectTransformUtility check; avoids
            // requiring the panel to forward enter/exit events.
            if (_panelVisible) _hoveringPanel = IsPointerOverPanel();
        }

        // ─── Pointer handlers ─────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            _exitGraceTimer = gracePeriod;
            if (!_panelVisible) ShowPanel();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            _exitGraceTimer = gracePeriod;
            // Don't reset _hoverTime — if the user re-enters quickly we want
            // the lock countdown to resume from where it left off, NOT restart.
            // (Standard Europa-style behavior.)
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // Toggle lock on click.
            if (_locked) Unlock();
            else
            {
                _locked = true;
                if (statsPanel != null) statsPanel.SetVisible(true, locked: true);
                UpdateProgressVisual(0f);
            }
        }

        // ─── State helpers ────────────────────────────────────────────────

        private void ShowPanel()
        {
            if (statsPanel != null) statsPanel.SetVisible(true, locked: false);
            _panelVisible = true;
        }

        private void HidePanel()
        {
            if (statsPanel != null) statsPanel.SetVisible(false, locked: false);
            _panelVisible = false;
            _locked = false;
            _hoverTime = 0f;
            UpdateProgressVisual(0f);
        }

        private void Unlock()
        {
            _locked = false;
            HidePanel();
        }

        private void UpdateProgressVisual(float progress01)
        {
            if (lockProgressBar == null) return;
            // Hide the bar entirely when fully empty or when locked — we want
            // it visible only as feedback during the build-up to lock.
            bool show = !_locked && progress01 > 0.001f;
            lockProgressBar.gameObject.SetActive(show);
            lockProgressBar.fillAmount = Mathf.Clamp01(progress01);
        }

        // ─── Pointer-target queries ───────────────────────────────────────

        private bool IsPointerOverIconOrPanel()
        {
            return IsPointerOverThis() || IsPointerOverPanel();
        }

        private bool IsPointerOverThis()
        {
            var rt = transform as RectTransform;
            if (rt == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition);
        }

        private bool IsPointerOverPanel()
        {
            if (statsPanel == null) return false;
            var rt = statsPanel.transform as RectTransform;
            if (rt == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition);
        }
    }
}

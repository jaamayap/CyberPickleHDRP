// File: Assets/_CyberPickle/Code/UI/Tooltip/TooltipPanel.cs
// Namespace: CyberPickle.UI.Tooltip
//
// The visual half of the tooltip system. Owns nothing about hover lifecycle —
// just receives content + position + locked-state from TooltipController and
// renders. One per visible tooltip; the controller manages the pool.
//
// Authoring expectations:
//   - Root has a CanvasGroup so the controller can fade in/out.
//   - Has a child TextMeshProUGUI for title and another for body.
//   - Optional: a small "lock" GameObject that toggles when SetLocked(true).
//   - Optional: a RaycastTarget Image as backplate so the tooltip can
//     receive its own clicks (so clicks on the tooltip itself don't
//     dismiss it via click-outside).
//
// Position: always set in screen-space pixels. SetScreenPosition handles
// the canvas's renderMode so callers don't have to think about it.

using TMPro;
using UnityEngine;

namespace CyberPickle.UI.Tooltip
{
    [DisallowMultipleComponent]
    public class TooltipPanel : MonoBehaviour
    {
        [Header("Content slots")]
        [Tooltip("Title text shown at top of the tooltip. Bold-rendered. Optional — leave empty if your design has title-less tooltips.")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Tooltip("Body text — supports TMP rich tags (color, bold, size). Required.")]
        [SerializeField] private TextMeshProUGUI bodyText;

        [Tooltip("Optional GameObject shown when the tooltip is locked (e.g., a small padlock icon). Hidden when transient.")]
        [SerializeField] private GameObject lockIndicator;

        [Tooltip("Optional Image (Type=Filled, FillMethod=Horizontal) that fills 0..1 as the lock-delay timer counts up while transient. Hidden when locked. The fillAmount is driven by TooltipController.")]
        [SerializeField] private UnityEngine.UI.Image lockProgressBar;

        [Header("Layout")]
        [Tooltip("RectTransform of this panel. Auto-assigned if left null.")]
        [SerializeField] private RectTransform rect;

        // Set by TooltipController after creation, cleared on Destroy.
        // Used by click-outside detection to know whether a click was
        // inside this tooltip's anchor.
        public HoverableElement AnchorElement { get; set; }

        private void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;
        }

        public void SetContent(TooltipContent content)
        {
            if (titleText != null)
            {
                titleText.text = content.title ?? string.Empty;
                titleText.gameObject.SetActive(!string.IsNullOrEmpty(content.title));
            }
            if (bodyText != null)
            {
                bodyText.text = content.body ?? string.Empty;
            }
        }

        public void SetLocked(bool locked)
        {
            if (lockIndicator != null) lockIndicator.SetActive(locked);
            if (lockProgressBar != null)
            {
                // Hide the progress bar when locked (lock is achieved); show
                // when transient so it can fill up as the user hovers.
                lockProgressBar.gameObject.SetActive(!locked);
                if (locked) lockProgressBar.fillAmount = 0f;
            }
        }

        /// <summary>
        /// Update the lock-progress visual (0..1). Called by TooltipController
        /// each frame while the tooltip is transient and accumulating hover time.
        /// </summary>
        public void SetLockProgress(float progress01)
        {
            if (lockProgressBar == null) return;
            lockProgressBar.fillAmount = Mathf.Clamp01(progress01);
        }

        /// <summary>
        /// Pixel size of the panel's RectTransform in screen-space units.
        /// Used by TooltipController for screen-edge clamping when locking.
        /// </summary>
        public Vector2 GetSize()
        {
            if (rect == null) rect = (RectTransform)transform;
            return rect.rect.size;
        }

        /// <summary>
        /// Set the panel's screen-space position (the panel's pivot point
        /// will land here). The controller passes the mouse position +
        /// offset; this method handles the conversion to RectTransform-local.
        /// </summary>
        public void SetScreenPosition(Vector2 screenPos)
        {
            if (rect == null) return;
            var canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                rect.position = screenPos;
                return;
            }

            // Screen-space overlay: position is screen pixels directly.
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                rect.position = screenPos;
                return;
            }

            // Screen-space camera or world-space: convert via the canvas's camera.
            var cam = canvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)canvas.transform, screenPos, cam, out var localPoint);
            rect.localPosition = localPoint;
        }
    }
}

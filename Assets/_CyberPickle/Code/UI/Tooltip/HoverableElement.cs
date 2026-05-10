// File: Assets/_CyberPickle/Code/UI/Tooltip/HoverableElement.cs
// Namespace: CyberPickle.UI.Tooltip
//
// Abstract base for any UI element that wants to surface a tooltip on
// hover. Subclasses override BuildContent() to provide the content
// (title + body) for their specific data — weapon stats, stat
// breakdowns, picked-card history, etc.
//
// The element implements Unity's pointer-event interfaces and forwards
// enter / exit / click to TooltipController, which owns lifecycle.
//
// Authoring requirement: the GameObject this sits on MUST have a
// raycast-target component (Image, Text, or any Graphic with Raycast
// Target = true), otherwise pointer events won't fire.

using UnityEngine;
using UnityEngine.EventSystems;

namespace CyberPickle.UI.Tooltip
{
    [DisallowMultipleComponent]
    public abstract class HoverableElement : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>
        /// Build the tooltip content for this element. Called every time
        /// the tooltip is shown — return fresh data each call so the
        /// tooltip reflects the latest state (e.g., live DPS, current
        /// rarity, in-flight modifier breakdown).
        /// </summary>
        public abstract TooltipContent BuildContent();

        /// <summary>
        /// Whether this hoverable supports the Europa-style lock behavior
        /// (auto-lock-on-hover-time + click-to-lock + click-outside-to-dismiss
        /// + lock progress slider). Default true.
        ///
        /// Override to false on hoverables whose tooltip CONTENT is static
        /// (e.g., picked-card history showing the modifiers a card applied
        /// when picked — those modifiers won't change after pick). For
        /// non-lockable hoverables the tooltip simply follows the mouse and
        /// vanishes when hover ends — no progress bar, no auto-lock.
        ///
        /// Lockable hoverables in the project:
        ///   - WeaponSlotUI    (DPS / damage / kills change continuously)
        ///   - StatRowUI       — NO (modifier breakdown is static after pick)
        ///   - PickedCardEntryUI — NO (card details static)
        ///   - CharacterIcon (handled by its own widget, not via this base)
        /// </summary>
        public virtual bool IsLockable => true;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var ctrl = TooltipController.Instance;
            if (ctrl != null) ctrl.OnHoverEnter(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var ctrl = TooltipController.Instance;
            if (ctrl != null) ctrl.OnHoverExit(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Only left-click toggles lock. Right-click / middle-click are
            // ignored — could be wired later for context menus.
            if (eventData.button != PointerEventData.InputButton.Left) return;
            var ctrl = TooltipController.Instance;
            if (ctrl != null) ctrl.OnHoverClick(this);
        }
    }
}

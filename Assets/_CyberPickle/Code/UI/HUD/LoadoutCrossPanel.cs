// File: Assets/_CyberPickle/Code/UI/HUD/LoadoutCrossPanel.cs
// Namespace: CyberPickle.UI.HUD
//
// The cross-shaped loadout HUD. Replaces WeaponSlotsPanel — instead of a
// horizontal row of 4 weapon slots, this widget owns 4 weapon slots + 4
// power-up slots arranged as a cross (one weapon + one power-up per axis).
//
// Per chat 2026-05-11, the same widget acts as both the in-game HUD AND
// the level-up modal's slot picker — when a NewWeapon or NewPowerUp card
// is picked, the panel enters slot-picker mode (empty eligible slots
// highlight, the player clicks one to commit). Animation between compact
// and expanded states is M8 step 5 polish — for step 4 we just toggle
// SetState(Compact|Expanded) instantaneously.
//
// Tooltips are preserved per the user's request: each cell is a
// HoverableElement (WeaponSlotUI / PowerUpSlotUI) so hover shows the
// existing tooltip behavior — including the lock-on-hover slider for
// weapon slots (which have live DPS).
//
// Authoring:
//   - Drop this on a parent GameObject under the HUD canvas.
//   - Set weaponSlots[0..N-1] to the WeaponSlotUI children, ordered by axis index.
//   - Set powerUpSlots[0..N-1] to the PowerUpSlotUI children, ordered by axis index.
//   - The two arrays must be the SAME LENGTH and that length must match
//     the runtime axis count (default 4; configurable via the loadout's
//     SetAxisCount before any axis operations).

using System;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Weapons;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class LoadoutCrossPanel : MonoBehaviour
    {
        public enum CrossState
        {
            /// <summary>Default in-game HUD layout — small, anchored to corner.</summary>
            Compact,
            /// <summary>Level-up modal layout — larger, centered, ready to host cards in the middle.</summary>
            Expanded,
        }

        public enum SlotKind
        {
            Weapon,
            PowerUp,
        }

        [Header("Slots — ordered by axis index (must be same length)")]
        [Tooltip("Weapon slots, one per axis. Index = axisIndex. Required.")]
        [SerializeField] private WeaponSlotUI[] weaponSlots = new WeaponSlotUI[WeaponLoadoutRuntime.DefaultAxisCount];

        [Tooltip("Power-up slots, one per axis. Index = axisIndex. Required and must be the same length as weaponSlots.")]
        [SerializeField] private PowerUpSlotUI[] powerUpSlots = new PowerUpSlotUI[WeaponLoadoutRuntime.DefaultAxisCount];

        [Header("Slot-picker visuals")]
        [Tooltip("Glow / highlight image overlaid on each ELIGIBLE empty slot during slot-picker mode. One per axis (matches array length). Optional but recommended for a clear UX cue.")]
        [SerializeField] private Image[] weaponSlotHighlights;

        [Tooltip("Glow / highlight image overlaid on each ELIGIBLE empty power-up slot during slot-picker mode. One per axis. Optional.")]
        [SerializeField] private Image[] powerUpSlotHighlights;

        [Tooltip("Color for highlighted (eligible) slots during slot-picker mode.")]
        [SerializeField] private Color slotPickerEligibleColor = new Color(0.40f, 0.95f, 1.00f, 0.60f);

        [Header("State / Layout")]
        [Tooltip("RectTransform of the panel. Auto-fetched if null. Used by SetState to reposition between Compact and Expanded — currently instant; M8 step 5 adds DOTween animation.")]
        [SerializeField] private RectTransform rect;

        [Tooltip("Anchored position when in Compact (in-game HUD) state. Typically a screen corner.")]
        [SerializeField] private Vector2 compactPosition = new Vector2(-200f, 100f);

        [Tooltip("Local scale when in Compact state. Default 1.")]
        [SerializeField] private Vector3 compactScale = Vector3.one;

        [Tooltip("Anchored position when in Expanded (level-up modal) state. Typically (0,0) for screen-center.")]
        [SerializeField] private Vector2 expandedPosition = Vector2.zero;

        [Tooltip("Local scale when in Expanded state. Larger so the cross dominates the screen during the modal.")]
        [SerializeField] private Vector3 expandedScale = new Vector3(1.6f, 1.6f, 1f);

        [Header("Diagnostics")]
        [SerializeField] private bool verbose = false;

        // ─── Public API ──────────────────────────────────────────────────

        /// <summary>Current state of the cross — Compact (in-game HUD) or Expanded (modal).</summary>
        public CrossState State { get; private set; } = CrossState.Compact;

        /// <summary>True iff the cross is currently in slot-picker mode (waiting for the player to click an eligible slot).</summary>
        public bool IsPicking { get; private set; }

        /// <summary>
        /// Fires when the player picks an eligible slot during slot-picker mode.
        /// Argument is the axis index they chose.
        /// </summary>
        public event Action<int> OnSlotPicked;

        /// <summary>
        /// Fires when slot-picker mode is cancelled (programmatically — no
        /// in-built ESC handler yet; the level-up modal can cancel via
        /// <see cref="CancelSlotPicker"/>).
        /// </summary>
        public event Action OnSlotPickerCancelled;

        /// <summary>
        /// Set the cross's visual state (Compact ↔ Expanded). Currently
        /// instant — DOTween animation lands in M8 step 5.
        /// </summary>
        public void SetState(CrossState state)
        {
            State = state;
            ApplyState();
        }

        /// <summary>
        /// Begin slot-picker mode. Highlights eligible (empty) slots of the
        /// requested kind and waits for a click. Click → <see cref="OnSlotPicked"/>
        /// fires + slot-picker mode ends.
        /// </summary>
        public void BeginSlotPicker(SlotKind kind)
        {
            if (IsPicking) CancelSlotPicker(); // defensive — clean previous picker

            EnsureLoadoutBound();
            _pickerKind = kind;
            IsPicking = true;

            UpdatePickerHighlights();

            if (verbose) Debug.Log($"[LoadoutCrossPanel] BeginSlotPicker({kind}).");
        }

        /// <summary>
        /// Cancel slot-picker mode without resolving (e.g., player pressed
        /// Cancel on the level-up modal). Fires <see cref="OnSlotPickerCancelled"/>.
        /// </summary>
        public void CancelSlotPicker()
        {
            if (!IsPicking) return;
            IsPicking = false;
            ClearPickerHighlights();
            OnSlotPickerCancelled?.Invoke();
            if (verbose) Debug.Log("[LoadoutCrossPanel] CancelSlotPicker.");
        }

        // ─── Internal: lifecycle + binding ───────────────────────────────

        private WeaponLoadoutRuntime _loadout;
        private bool _bound;
        private SlotKind _pickerKind;

        private void Awake()
        {
            if (rect == null) rect = (RectTransform)transform;

            // Stamp axis index on each slot so it knows which loadout axis to read.
            for (int i = 0; i < weaponSlots.Length; i++)
                if (weaponSlots[i] != null) weaponSlots[i].SetSlotIndex(i);
            for (int i = 0; i < powerUpSlots.Length; i++)
                if (powerUpSlots[i] != null) powerUpSlots[i].SetAxisIndex(i);

            // Wire each slot's pointer events for slot-picker click handling.
            // We use the slot's RectTransform position as the click target;
            // the existing HoverableElement pointer click also fires (for
            // tooltip lock toggling) — both are compatible because the
            // HoverableElement's IsLockable governs whether the click
            // toggles a lock. During picker mode we listen at this panel
            // level via Update + raycasts (simpler than wiring per-slot
            // delegates and tearing them down). See HandleSlotPickerInput.

            ApplyState();
        }

        private void OnEnable()
        {
            MusicEventBus.OnEvent += HandleMusicEvent;
        }

        private void OnDisable()
        {
            MusicEventBus.OnEvent -= HandleMusicEvent;
            UnbindLoadout();
        }

        private void HandleMusicEvent(MusicEvent type, object payload)
        {
            if (type == MusicEvent.RunStart) BindLoadout();
        }

        private void EnsureLoadoutBound()
        {
            if (_bound) return;
            BindLoadout();
        }

        private void BindLoadout()
        {
            if (_loadout == null) _loadout = WeaponLoadoutRuntime.Instance;
            if (_loadout == null)
            {
                Debug.LogError("[LoadoutCrossPanel] No WeaponLoadoutRuntime found.");
                return;
            }
            if (_bound) return;

            _loadout.OnWeaponAdded         += HandleWeaponAdded;
            _loadout.OnWeaponLevelChanged  += HandleAxisChanged;
            _loadout.OnWeaponRarityChanged += HandleAxisChanged;
            _loadout.OnWeaponEvolved       += HandleAxisChanged;
            _loadout.OnWeaponElementChanged+= HandleAxisChanged;
            _loadout.OnPowerUpAdded        += HandlePowerUpAdded;
            _loadout.OnPowerUpRemoved      += HandleAxisChanged;
            _loadout.OnPowerUpLevelChanged += HandleAxisChanged;
            _loadout.OnPowerUpRarityChanged+= HandleAxisChanged;
            _loadout.OnLoadoutCleared      += HandleLoadoutCleared;
            _bound = true;

            // Initial paint — show whatever's already in the loadout.
            RefreshAll();
            if (verbose) Debug.Log("[LoadoutCrossPanel] Bound to WeaponLoadoutRuntime.");
        }

        private void UnbindLoadout()
        {
            if (!_bound || _loadout == null) return;
            _loadout.OnWeaponAdded         -= HandleWeaponAdded;
            _loadout.OnWeaponLevelChanged  -= HandleAxisChanged;
            _loadout.OnWeaponRarityChanged -= HandleAxisChanged;
            _loadout.OnWeaponEvolved       -= HandleAxisChanged;
            _loadout.OnWeaponElementChanged-= HandleAxisChanged;
            _loadout.OnPowerUpAdded        -= HandlePowerUpAdded;
            _loadout.OnPowerUpRemoved      -= HandleAxisChanged;
            _loadout.OnPowerUpLevelChanged -= HandleAxisChanged;
            _loadout.OnPowerUpRarityChanged-= HandleAxisChanged;
            _loadout.OnLoadoutCleared      -= HandleLoadoutCleared;
            _bound = false;
        }

        // ─── Refresh dispatchers ─────────────────────────────────────────

        private void HandleWeaponAdded(WeaponInstanceData added)
        {
            if (added != null) RefreshAxis(added.slotIndex);
            UpdatePickerHighlights(); // axis just filled, highlights may change
        }

        private void HandlePowerUpAdded(PowerUpInstanceData added)
        {
            if (added != null) RefreshAxis(added.axisIndex);
            UpdatePickerHighlights();
        }

        private void HandleAxisChanged(int axisIndex)
        {
            RefreshAxis(axisIndex);
            UpdatePickerHighlights();
        }

        private void HandleLoadoutCleared()
        {
            RefreshAll();
            UpdatePickerHighlights();
        }

        private void RefreshAxis(int axisIndex)
        {
            if (_loadout == null) return;
            var axis = _loadout.GetAxis(axisIndex);
            if (axis == null) return;

            if (axisIndex < weaponSlots.Length && weaponSlots[axisIndex] != null)
                weaponSlots[axisIndex].Refresh(axis.weapon);
            if (axisIndex < powerUpSlots.Length && powerUpSlots[axisIndex] != null)
                powerUpSlots[axisIndex].Refresh(axis.powerUp);
        }

        private void RefreshAll()
        {
            int n = weaponSlots.Length;
            for (int i = 0; i < n; i++) RefreshAxis(i);
        }

        // ─── State (Compact / Expanded) ─────────────────────────────────

        private void ApplyState()
        {
            if (rect == null) return;
            switch (State)
            {
                case CrossState.Compact:
                    rect.anchoredPosition = compactPosition;
                    rect.localScale       = compactScale;
                    break;
                case CrossState.Expanded:
                    rect.anchoredPosition = expandedPosition;
                    rect.localScale       = expandedScale;
                    break;
            }
        }

        // ─── Slot-picker visuals + input ────────────────────────────────

        private void UpdatePickerHighlights()
        {
            if (!IsPicking)
            {
                ClearPickerHighlights();
                return;
            }
            if (_loadout == null) return;

            int n = _loadout.AxisCount;
            switch (_pickerKind)
            {
                case SlotKind.Weapon:
                    for (int i = 0; i < n; i++)
                    {
                        bool eligible = !_loadout.GetAxis(i).HasWeapon;
                        SetHighlight(weaponSlotHighlights, i, eligible);
                        SetHighlight(powerUpSlotHighlights, i, false);
                    }
                    break;
                case SlotKind.PowerUp:
                    for (int i = 0; i < n; i++)
                    {
                        bool eligible = !_loadout.GetAxis(i).HasPowerUp;
                        SetHighlight(weaponSlotHighlights, i, false);
                        SetHighlight(powerUpSlotHighlights, i, eligible);
                    }
                    break;
            }
        }

        private void ClearPickerHighlights()
        {
            for (int i = 0; i < (weaponSlotHighlights?.Length ?? 0); i++)
                SetHighlight(weaponSlotHighlights, i, false);
            for (int i = 0; i < (powerUpSlotHighlights?.Length ?? 0); i++)
                SetHighlight(powerUpSlotHighlights, i, false);
        }

        private void SetHighlight(Image[] arr, int idx, bool on)
        {
            if (arr == null || idx < 0 || idx >= arr.Length) return;
            var img = arr[idx];
            if (img == null) return;
            img.gameObject.SetActive(on);
            if (on) img.color = slotPickerEligibleColor;
        }

        private void Update()
        {
            if (!IsPicking) return;
            if (_loadout == null) return;
            if (!Input.GetMouseButtonDown(0)) return;

            // Pointer over which slot? For each eligible cell, hit-test the
            // pointer position against the slot's RectTransform. First match
            // wins. This avoids per-slot delegate wiring + lets us cleanly
            // stop listening when picker mode ends.
            int picked = HitTestPickerSlots();
            if (picked < 0) return;

            int axisIndex = picked;
            IsPicking = false;
            ClearPickerHighlights();
            if (verbose) Debug.Log($"[LoadoutCrossPanel] Slot picked: axis {axisIndex} ({_pickerKind}).");
            OnSlotPicked?.Invoke(axisIndex);
        }

        private int HitTestPickerSlots()
        {
            int n = _loadout.AxisCount;
            Vector2 mouse = Input.mousePosition;
            switch (_pickerKind)
            {
                case SlotKind.Weapon:
                    for (int i = 0; i < n && i < weaponSlots.Length; i++)
                    {
                        if (_loadout.GetAxis(i).HasWeapon) continue; // not eligible
                        if (weaponSlots[i] == null) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(
                                (RectTransform)weaponSlots[i].transform, mouse, GetEventCamera()))
                            return i;
                    }
                    break;

                case SlotKind.PowerUp:
                    for (int i = 0; i < n && i < powerUpSlots.Length; i++)
                    {
                        if (_loadout.GetAxis(i).HasPowerUp) continue;
                        if (powerUpSlots[i] == null) continue;
                        if (RectTransformUtility.RectangleContainsScreenPoint(
                                (RectTransform)powerUpSlots[i].transform, mouse, GetEventCamera()))
                            return i;
                    }
                    break;
            }
            return -1;
        }

        private Camera GetEventCamera()
        {
            // Screen Space Overlay canvases pass null to RectangleContainsScreenPoint;
            // ScreenSpaceCamera / WorldSpace pass the canvas's worldCamera.
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
    }
}

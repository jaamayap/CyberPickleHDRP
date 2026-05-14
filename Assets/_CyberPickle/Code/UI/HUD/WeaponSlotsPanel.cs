// File: Assets/_CyberPickle/Code/UI/HUD/WeaponSlotsPanel.cs
// Namespace: CyberPickle.UI.HUD
//
// Container for the 4 weapon slot widgets in the in-run HUD. Owns one
// subscription to WeaponLoadoutRuntime + dispatches refresh calls to
// each child WeaponSlotUI. Cleaner than each slot owning its own
// subscription — same data, one listener, four refreshes.
//
// Authoring: drop this on a parent GameObject under the HUD canvas, set
// the `slots` array to the 4 child WeaponSlotUI components in slot-index
// order (0..3). Slot 0 is the starting weapon.

using UnityEngine;
using CyberPickle.Gameplay.Audio;
using CyberPickle.Gameplay.Weapons;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class WeaponSlotsPanel : MonoBehaviour
    {
        [Header("Slots (ordered by slotIndex 0..3)")]
        [Tooltip("Four child WeaponSlotUI components, one per loadout slot. Index = slotIndex. Required.")]
        [SerializeField] private WeaponSlotUI[] slots = new WeaponSlotUI[WeaponLoadoutRuntime.MaxSlots];

        [Tooltip("Optional — four child WeaponSlotBeatPulse components, one per loadout slot, same ordering as `slots`. Drives the per-shot dance + fuse-ring anticipation visuals. Leave empty if you don't want beat-pulses on a particular layout.")]
        [SerializeField] private WeaponSlotBeatPulse[] beatPulses = new WeaponSlotBeatPulse[WeaponLoadoutRuntime.MaxSlots];

        [Header("Diagnostics")]
        [SerializeField] private bool verbose;

        private WeaponLoadoutRuntime _loadout;
        private bool _bound;

        private void Awake()
        {
            // Stamp slot indices on the children so they don't have to be
            // configured by hand. Designer just orders the array; index
            // comes from array position. Same rule applies to the parallel
            // beatPulses array — index in the array IS the slot index.
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].SetSlotIndex(i);
            }
            for (int i = 0; i < beatPulses.Length; i++)
            {
                if (beatPulses[i] != null) beatPulses[i].SetSlotIndex(i);
            }
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
            if (type == MusicEvent.RunStart) BindToLoadout();
        }

        private void BindToLoadout()
        {
            if (_loadout == null) _loadout = WeaponLoadoutRuntime.Instance;
            if (_loadout == null)
            {
                Debug.LogError("[WeaponSlotsPanel] No WeaponLoadoutRuntime found at RunStart.");
                return;
            }
            if (_bound) return;

            _loadout.OnWeaponAdded         += HandleWeaponAdded;
            _loadout.OnWeaponLevelChanged  += HandleSlotChanged;
            _loadout.OnWeaponRarityChanged += HandleSlotChanged;
            _loadout.OnWeaponEvolved       += HandleSlotChanged;
            _loadout.OnLoadoutCleared      += HandleLoadoutCleared;
            _bound = true;

            // Initial paint — show whatever's already in the loadout.
            RefreshAll();

            if (verbose) Debug.Log("[WeaponSlotsPanel] Bound to WeaponLoadoutRuntime.");
        }

        private void UnbindLoadout()
        {
            if (!_bound || _loadout == null) return;
            _loadout.OnWeaponAdded         -= HandleWeaponAdded;
            _loadout.OnWeaponLevelChanged  -= HandleSlotChanged;
            _loadout.OnWeaponRarityChanged -= HandleSlotChanged;
            _loadout.OnWeaponEvolved       -= HandleSlotChanged;
            _loadout.OnLoadoutCleared      -= HandleLoadoutCleared;
            _bound = false;
        }

        private void HandleWeaponAdded(WeaponInstanceData added)
        {
            if (added == null) return;
            RefreshSlot(added.slotIndex);
        }

        private void HandleSlotChanged(int slotIndex)
        {
            RefreshSlot(slotIndex);
        }

        private void HandleLoadoutCleared()
        {
            RefreshAll();
        }

        private void RefreshSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            var slot = slots[slotIndex];
            if (slot == null) return;
            var instance = _loadout != null ? _loadout.GetSlot(slotIndex) : null;
            slot.Refresh(instance);
        }

        private void RefreshAll()
        {
            for (int i = 0; i < slots.Length; i++) RefreshSlot(i);
        }
    }
}

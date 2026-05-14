// File: Assets/_CyberPickle/Code/UI/HUD/WeaponSlotUI.cs
// Namespace: CyberPickle.UI.HUD
//
// One weapon-slot display in the HUD. Shows compact summary (level +
// rarity + name + DPS) and surfaces a detailed tooltip on hover via the
// HoverableElement base.
//
// Data sources read on every refresh / tooltip build:
//   - WeaponLoadoutRuntime.GetSlot(slotIndex) — current Level/Rarity/Element
//   - WeaponData fields via the instance's weaponData ref
//   - PerWeaponStatsTracker.GetStats(weaponId) — live DPS / damage / kills
//
// Empty slots render as "—". The HoverableElement still works — the
// tooltip will simply describe the slot as unequipped.

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CyberPickle.Core;
using CyberPickle.Gameplay.Combat;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.Stats;
using CyberPickle.Gameplay.Weapons;
using CyberPickle.Shop.Equipment.Data;
using CyberPickle.UI.Tooltip;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class WeaponSlotUI : HoverableElement
    {
        [Header("Display")]
        [Tooltip("Headline label — typically '[L2 Legendary] Plasma Lance' or '—' for empty. Required.")]
        [SerializeField] private TextMeshProUGUI labelText;

        [Tooltip("Live DPS readout — e.g. 'DPS 28.4'. Optional — leave empty for compact layouts.")]
        [SerializeField] private TextMeshProUGUI dpsText;

        [Tooltip("Image whose color is tinted by the current weapon's rarity (frame, glow, fill — designer's call). Optional.")]
        [SerializeField] private Image rarityFrame;

        [Tooltip("Image whose color is tinted by the current weapon's element. Optional — leave null if your design folds element into the rarity frame.")]
        [SerializeField] private Image elementFrame;

        [Tooltip("Image showing the weapon's sprite icon (from WeaponData.equipmentIcon). Hidden when slot is empty or weapon has no icon assigned. Optional.")]
        [SerializeField] private Image iconImage;

        // Slot index is set by the parent WeaponSlotsPanel based on array position.
        private int _slotIndex;

        // Cached instance from the last Refresh — used by Update() to
        // periodically re-format the DPS label without the parent panel
        // having to call Refresh() every frame. The parent only calls
        // Refresh on loadout-change events (weapon added / leveled / etc.),
        // which would otherwise leave the DPS number frozen at whatever
        // value it had on the last upgrade. The poll loop below keeps the
        // DPS number current as the player fires.
        private WeaponInstanceData _cachedInstance;
        private float _dpsRefreshTimer;
        private const float DpsRefreshInterval = 0.5f;

        public void SetSlotIndex(int idx) => _slotIndex = idx;

        /// <summary>
        /// Read-only access to the slot index this UI represents. Used by
        /// sibling components (e.g. WeaponSlotBeatPulse) that want to
        /// inherit the same index without requiring the parent panel to
        /// wire them up in a second array.
        /// </summary>
        public int SlotIndex => _slotIndex;

        // ─── Refresh from current loadout state ───────────────────────────

        public void Refresh(WeaponInstanceData instance)
        {
            bool valid = instance != null && instance.IsValid;

            // Cache for the Update() poll so DPS keeps refreshing between
            // explicit Refresh() calls (which only fire on loadout changes).
            _cachedInstance = valid ? instance : null;

            if (labelText != null)
            {
                labelText.text = valid
                    ? $"L{instance.level}{(instance.evolved ? "E" : "")} {instance.rarity.DisplayName()} | {instance.weaponData.displayName}"
                    : "—";
            }

            if (dpsText != null)
            {
                dpsText.text = valid ? FormatLiveDps(instance) : string.Empty;
            }

            if (rarityFrame != null)
            {
                rarityFrame.color = valid ? instance.rarity.DisplayColor() : new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            if (elementFrame != null)
            {
                elementFrame.color = valid ? instance.element.DisplayColor() : new Color(0.3f, 0.3f, 0.3f, 1f);
            }

            if (iconImage != null)
            {
                Sprite sprite = (valid && instance.weaponData != null) ? instance.weaponData.equipmentIcon : null;
                if (sprite != null)
                {
                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                }
                else
                {
                    // Empty slot or no icon — show a faint placeholder so the layout cell stays consistent.
                    iconImage.sprite = null;
                    iconImage.color = new Color(1f, 1f, 1f, 0.10f);
                    iconImage.enabled = true;
                }
            }
        }

        private static string FormatLiveDps(WeaponInstanceData instance)
        {
            var tracker = PerWeaponStatsTracker.Instance;
            if (tracker == null) return string.Empty;
            var stats = tracker.GetStats(instance.WeaponId);
            if (stats == null) return "DPS —";

            // Show TOTAL-RUN DPS, not rolling-window. The rolling value is
            // noisy (a sniper firing once per 4 beats fluctuates ±50% as
            // shots enter/exit the 5s window) and reads as a "live"
            // metric — but the slot label is glanced at periodically, not
            // watched continuously, so a stable cumulative number is more
            // useful AND matches the tooltip's "Run:" row exactly. Both
            // values come from the same per-weapon stats.
            float runTime = CyberPickle.Gameplay.RunState.RunStateManager.Instance != null
                ? CyberPickle.Gameplay.RunState.RunStateManager.Instance.RunTime
                : 0f;
            return $"DPS {stats.GetTotalRunDps(runTime):F1}";
        }

        // Poll the DPS label every DpsRefreshInterval seconds. Without this,
        // the DPS text only updates on loadout changes (the parent panel's
        // event-driven refresh model) and stays frozen at the last-upgrade
        // value through the rest of the run. Unscaled time so the label
        // stays current even while paused (no harm — DPS doesn't change
        // during pause, but the math is cheap).
        private void Update()
        {
            if (dpsText == null) return;
            _dpsRefreshTimer -= Time.unscaledDeltaTime;
            if (_dpsRefreshTimer > 0f) return;
            _dpsRefreshTimer = DpsRefreshInterval;

            dpsText.text = (_cachedInstance != null && _cachedInstance.IsValid)
                ? FormatLiveDps(_cachedInstance)
                : string.Empty;
        }

        // ─── Tooltip content ──────────────────────────────────────────────

        public override TooltipContent BuildContent()
        {
            var loadout = WeaponLoadoutRuntime.Instance;
            var instance = loadout != null ? loadout.GetSlot(_slotIndex) : null;

            if (instance == null || !instance.IsValid)
            {
                return new TooltipContent
                {
                    title = $"Slot {_slotIndex}",
                    body  = "<i>Empty — no weapon equipped.</i>",
                };
            }

            var sb = new StringBuilder(512);
            string rarityHex = ColorUtility.ToHtmlStringRGB(instance.rarity.DisplayColor());
            string elementHex = ColorUtility.ToHtmlStringRGB(instance.element.DisplayColor());

            sb.AppendLine($"<color=#{rarityHex}>{instance.rarity.DisplayName()}</color>  •  L{instance.level}{(instance.evolved ? " (Evolved)" : "")}");
            sb.AppendLine($"<color=#{elementHex}>{instance.element.DisplayName()}</color>");
            sb.AppendLine();
            sb.AppendLine("<b>Stats</b>");

            // Read live PlayerStats for the damage breakdown. BPM is now
            // global (MusicConductor.Instance.BPM) — read directly via the
            // static helper on WeaponData.
            var playerStats = Object.FindFirstObjectByType<PlayerStats>();
            float power      = playerStats != null ? playerStats.Get(PlayerStatType.Power)      : 0f;
            float critChance = playerStats != null ? playerStats.Get(PlayerStatType.CritChance) : 0f;

            float baseDmg     = instance.weaponData.baseDamage;
            float rarityMul   = instance.rarity.DamageMultiplier();
            float powerMul    = 1f + power * 0.01f;
            const float critMul = 2f;
            float perShotNoCrit = baseDmg * rarityMul * powerMul;
            float perShotCrit   = perShotNoCrit * critMul;

            // Compact damage breakdown.
            sb.AppendLine($"<color=#ffd66e>{perShotNoCrit:F1}</color> dmg/shot  <size=80%>(base {baseDmg:F0} × {rarityMul:F2} × Power {powerMul:F2})</size>");
            if (critChance > 0f)
                sb.AppendLine($"<color=#ffaaaa>{perShotCrit:F1}</color> on crit  <size=80%>({critChance * 100f:F0}% chance)</size>");

            // Pattern-driven fire rate. BPM is global (set by MusicConductor,
            // ultimately driven by Dexterity).
            float fireRate = instance.weaponData.GetFireRateForLevel(instance.level);
            float bpm      = WeaponData.CurrentBPM();
            int activeCells = (instance.weaponData.activeCellsPerLevel != null && instance.weaponData.activeCellsPerLevel.Length > 0)
                ? instance.weaponData.activeCellsPerLevel[Mathf.Clamp(instance.level - 1, 0, instance.weaponData.activeCellsPerLevel.Length - 1)]
                : 0;
            sb.AppendLine($"{fireRate:F2}/s  <size=80%>({activeCells} cells / {instance.weaponData.barCount} bars @ {bpm:F0} BPM)</size>");

            float avgDmg = perShotNoCrit * (1f - critChance) + perShotCrit * critChance;
            float expectedDps = avgDmg * fireRate;
            // Label clearly so the player understands this is the THEORETICAL
            // ceiling (every shot lands, current Power + crit, current BPM),
            // not the actual cumulative DPS that the "Run:" row shows below.
            sb.AppendLine($"<b>Expected DPS</b>  <color=#ffd66e>{expectedDps:F1}</color>  <size=80%><i>(theoretical max)</i></size>");

            // Per-tier bonus perk (if authored on the WeaponData).
            var perk = instance.weaponData.GetPerkForRarity(instance.rarity);
            if (perk != null)
            {
                sb.AppendLine();
                sb.AppendLine($"<b>{instance.rarity.DisplayName()}</b> {perk.title}");
            }

            // Live run stats — compact.
            var tracker = PerWeaponStatsTracker.Instance;
            var live = tracker != null ? tracker.GetStats(instance.WeaponId) : null;
            if (live != null)
            {
                // Use TotalRunDps (total damage / run time) so this number is
                // (a) stable across consecutive tooltip refreshes, and
                // (b) consistent with the slot label, which uses the same
                //     calculation.
                // Previously this used RollingDps — its noisy nature made
                // it look like "Run:" was wrong (slot showed 83, tooltip showed
                // 64.1 at the same instant). Now both render the same number.
                float runTime = CyberPickle.Gameplay.RunState.RunStateManager.Instance != null
                    ? CyberPickle.Gameplay.RunState.RunStateManager.Instance.RunTime
                    : 0f;
                float runDps = live.GetTotalRunDps(runTime);
                sb.AppendLine();
                sb.AppendLine($"Run: <b>{live.TotalKills}</b> kills · {live.TotalHits} hits · {runDps:F1} DPS");
            }

            return new TooltipContent
            {
                title = instance.weaponData.displayName,
                body  = sb.ToString(),
            };
        }
    }
}

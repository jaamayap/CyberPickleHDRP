// File: Assets/_CyberPickle/Code/UI/HUD/StatRowUI.cs
// Namespace: CyberPickle.UI.HUD
//
// One row in the PlayerStatsPanel. Shows a stat's name + current
// effective value. Hovering surfaces a detailed breakdown (base + each
// modifier with its sourceId + final), via PlayerStats.GetModifierBreakdown.
//
// Authoring: ONE prefab, instantiated 14× by PlayerStatsPanel — the
// panel sets the stat type via SetStatType. Each row needs a
// raycast-target Image as backplate so hover events fire.

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using CyberPickle.Gameplay.Player;
using CyberPickle.Gameplay.Stats;
using CyberPickle.UI.Tooltip;

namespace CyberPickle.UI.HUD
{
    [DisallowMultipleComponent]
    public class StatRowUI : HoverableElement
    {
        [Header("Display")]
        [Tooltip("TMP for the stat's display name (left side of row). Required.")]
        [SerializeField] private TextMeshProUGUI nameText;

        [Tooltip("TMP for the stat's current effective value (right side of row). Required.")]
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Formatting")]
        [Tooltip("Numeric format for the value text. Default 'F2' shows two decimals; 'F0' for integer-looking, 'P0' for percent display (multiplies by 100), etc.")]
        [SerializeField] private string valueFormat = "F2";

        // Set by PlayerStatsPanel after instantiation.
        private PlayerStatType _stat;
        private PlayerStats _statsRef;

        // Reused across BuildContent calls — saves GC.
        private readonly StringBuilder _sb = new StringBuilder(256);
        private readonly List<StatModifier> _breakdownBuf = new List<StatModifier>(16);

        // Stat-row tooltips show the modifier breakdown for a stat — that's
        // a static list (modifiers only change when the player picks a card,
        // which closes/reopens the tooltip anyway). No need to lock.
        public override bool IsLockable => false;

        public void SetStatType(PlayerStatType type)
        {
            _stat = type;
            if (nameText != null) nameText.text = FormatStatName(type);
        }

        // ─── Refresh — called by PlayerStatsPanel on stat change ──────────

        public void Refresh(PlayerStats stats)
        {
            _statsRef = stats;
            if (valueText == null) return;
            if (stats == null) { valueText.text = "—"; return; }

            float v = stats.Get(_stat);
            valueText.text = FormatValue(v);
        }

        // ─── Tooltip content — modifier breakdown ─────────────────────────

        public override TooltipContent BuildContent()
        {
            if (_statsRef == null)
            {
                return new TooltipContent
                {
                    title = FormatStatName(_stat),
                    body  = "<i>Stats not bound yet.</i>",
                };
            }

            float baseVal = _statsRef.Base.Get(_stat);
            float effVal  = _statsRef.Get(_stat);

            _statsRef.GetModifierBreakdownNonAlloc(_stat, _breakdownBuf);

            _sb.Clear();
            _sb.AppendLine($"<b>Effective:</b>  <color=#ffd66e>{effVal:F2}</color>");
            _sb.AppendLine($"<b>Base:</b>       {baseVal:F2}");

            if (_breakdownBuf.Count == 0)
            {
                _sb.AppendLine();
                _sb.AppendLine("<i>No modifiers active.</i>");
            }
            else
            {
                _sb.AppendLine();
                _sb.AppendLine($"<b>Modifiers ({_breakdownBuf.Count})</b>");
                foreach (var m in _breakdownBuf)
                {
                    _sb.AppendLine($"  <color=#aaaaaa>{m.sourceId}</color>  <color=#88c8ff>{m.kind}</color>  {FormatModifierValue(m)}");
                }
            }

            return new TooltipContent
            {
                title = FormatStatName(_stat),
                body  = _sb.ToString(),
            };
        }

        // ─── Formatting helpers ───────────────────────────────────────────

        private string FormatValue(float v)
        {
            if (string.IsNullOrEmpty(valueFormat)) return v.ToString();
            return v.ToString(valueFormat);
        }

        private static string FormatStatName(PlayerStatType type)
        {
            // CamelCase → spaced words. Lightweight, runs once per row.
            string raw = type.ToString();
            var sb = new StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i])) sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static string FormatModifierValue(StatModifier m)
        {
            // AddPercent values are decimal fractions — show as percent for readability.
            switch (m.kind)
            {
                case ModifierKind.AddBase:    return $"+{m.value:F2}";
                case ModifierKind.AddPercent: return (m.value >= 0 ? "+" : "") + $"{m.value * 100f:F0}%";
                case ModifierKind.MultFinal:  return $"×{m.value:F2}";
                case ModifierKind.Override:   return $"= {m.value:F2}";
                default:                      return m.value.ToString("F2");
            }
        }
    }
}

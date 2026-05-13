// File: Assets/_CyberPickle/Code/Gameplay/Progression/DraftedCard.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Runtime wrapper around an UpgradeCardSO with the values rolled at
// draft time:
//   - Rarity (rolled per the pool's Luck-modulated weights)
//   - Element (rolled uniformly per the 7 elements; only meaningful for
//     power-up cards)
//
// Why this exists: a single power-up template asset (e.g., "Fire-Rate
// Boost") shows up as multiple element-flavored cards in the draft —
// Fire / Lightning / Ice / etc. — without authoring a separate asset
// per element. The pool drawing logic rolls the element at draft time
// and stores it on this struct.
//
// For non-power-up cards (StatModifier buffs, Weapon level-ups, etc.),
// the rolled element is ElementId.None and ignored when the card is
// applied.

using CyberPickle.Core;

namespace CyberPickle.Gameplay.Progression
{
    /// <summary>
    /// One drafted card — a template (UpgradeCardSO) plus the values
    /// rolled at draft time.
    /// </summary>
    public struct DraftedCard
    {
        /// <summary>The template asset. Carries all immutable design data.</summary>
        public UpgradeCardSO source;

        /// <summary>Rarity rolled at draft time (per pool's Luck-modulated weights).</summary>
        public Rarity rolledRarity;

        /// <summary>
        /// Element rolled at draft time. Only meaningful for
        /// <see cref="CardType.NewPowerUp"/> cards — other types ignore
        /// this field and use ElementId.None.
        /// </summary>
        public ElementId rolledElement;

        public bool IsValid => source != null;
        public string CardId => source != null ? source.cardId : string.Empty;
        public CardType Kind => source != null ? source.cardType : CardType.StatModifier;

        /// <summary>
        /// True if the player has to pick a slot on the cross UI before
        /// the card can be applied. Used by the level-up screen to enter
        /// a "slot-picker" mode after a slottable card is clicked.
        /// </summary>
        public bool RequiresSlotSelection
            => source != null && (source.cardType == CardType.NewWeapon ||
                                  source.cardType == CardType.NewPowerUp);
    }
}

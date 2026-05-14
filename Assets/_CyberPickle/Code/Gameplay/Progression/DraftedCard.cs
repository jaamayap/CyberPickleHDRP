// File: Assets/_CyberPickle/Code/Gameplay/Progression/DraftedCard.cs
// Namespace: CyberPickle.Gameplay.Progression
//
// Runtime wrapper around an UpgradeCardSO with the values rolled at
// draft time:
//   - Rarity (rolled per the pool's Luck-modulated weights)
//   - Element (rolled uniformly per the 7 elements; only meaningful for
//     power-up cards)
//   - Resolved weapon / power-up target (for TEMPLATE cards — see below)
//
// Why this exists:
//   1. Element variance: a single power-up template asset (e.g., "Fire-Rate
//      Boost") shows up as multiple element-flavored cards in the draft —
//      Fire / Lightning / Ice / etc. — without authoring a separate asset
//      per element.
//   2. Weapon-target variance (M9 PR card-system polish): instead of one
//      card per (weapon × type) combination, ONE generic template card
//      (e.g., "Card_LevelUpWeapon_Template" with targetWeaponData = null)
//      resolves to a random currently-equipped weapon at draft time. The
//      resolved target lives on this struct. Eliminates 12+ per-weapon
//      card assets and auto-uses WeaponData.displayName everywhere.
//
// For non-templated cards (legacy specific cards still in the pool, or
// StatModifier buffs), the resolved fields are null and the card's
// authored target is used directly.

using CyberPickle.Core;
using CyberPickle.Shop.Equipment.Data;

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

        /// <summary>
        /// Weapon target resolved at draft time. Populated by UpgradePoolSO
        /// for TEMPLATE cards (source.targetWeaponData == null) where the
        /// pool resolved a concrete weapon from the loadout / unlocked
        /// inventory. <c>null</c> for non-templated cards — in that case
        /// <c>source.targetWeaponData</c> is the target.
        ///
        /// Apply / display code prefers this when non-null and falls back
        /// to <c>source.targetWeaponData</c> otherwise.
        /// </summary>
        public WeaponData resolvedTargetWeapon;

        /// <summary>
        /// Power-up target resolved at draft time. Same pattern as
        /// <see cref="resolvedTargetWeapon"/> for power-up templates.
        /// </summary>
        public PowerUpData resolvedTargetPowerUp;

        public bool IsValid => source != null;
        public string CardId => source != null ? source.cardId : string.Empty;
        public CardType Kind => source != null ? source.cardType : CardType.StatModifier;

        /// <summary>Effective weapon target: resolved (template) first, then authored fallback.</summary>
        public WeaponData EffectiveWeaponTarget => resolvedTargetWeapon != null ? resolvedTargetWeapon : source?.targetWeaponData;

        /// <summary>Effective power-up target: resolved (template) first, then authored fallback.</summary>
        public PowerUpData EffectivePowerUpTarget => resolvedTargetPowerUp != null ? resolvedTargetPowerUp : source?.targetPowerUpData;

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

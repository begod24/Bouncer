using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Upgrades;
using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>
    /// Подсказки на карточках без лишнего текста: из каких карточек комбо, с чем карточка складывается в комбо,
    /// с какими мячами лучше и с какими спит. Всё берётся из колоды — новые комбо подхватываются сами.
    /// </summary>
    public static class CardHints
    {
        static readonly List<UpgradeCard> s_combos = new();

        static CardDeck Deck => CardDeck.Active;

        /// <summary>Все комбо колоды, в которые входит карточка.</summary>
        public static void CombosWith(UpgradeCard card, List<UpgradeCard> result)
        {
            result.Clear();
            if (card == null || Deck == null)
                return;
            foreach (var combo in Deck.deck)
                if (combo && combo.IsCombo && System.Array.IndexOf(combo.requires, card) >= 0)
                    result.Add(combo);
        }

        /// <summary>Вторая часть комбо (для комбо из двух карточек).</summary>
        public static UpgradeCard Partner(UpgradeCard combo, UpgradeCard card)
        {
            if (combo == null || !combo.IsCombo)
                return null;
            foreach (var part in combo.requires)
                if (part && part != card)
                    return part;
            return null;
        }

        /// <summary>
        /// Самое полезное комбо для подсказки: где напарник уже в кармане или в руках, иначе первое.
        /// </summary>
        public static UpgradeCard BestCombo(UpgradeCard card, PlayerCards owner, out bool ready)
        {
            ready = false;
            CombosWith(card, s_combos);
            UpgradeCard best = null;
            foreach (var combo in s_combos)
            {
                var partner = Partner(combo, card);
                bool has = owner != null && partner != null && (partner is BallTypeCard ball
                    ? ball.ballPrefab == owner.CurrentBall
                    : owner.Owns(partner));
                if (has)
                {
                    ready = true;
                    return combo;
                }
                best ??= combo;
            }
            return best;
        }

        /// <summary>Мячи, с которыми карточка складывается в комбо — «лучше всего с».</summary>
        public static void GoodBalls(UpgradeCard card, List<BallTypeCard> result)
        {
            result.Clear();
            CombosWith(card, s_combos);
            foreach (var combo in s_combos)
                if (combo.BallPart is { } ball && ball != card && !result.Contains(ball))
                    result.Add(ball);
        }

        /// <summary>Карточки мячей, с которыми эта карточка спит.</summary>
        public static void SleepyBalls(UpgradeCard card, List<BallTypeCard> result)
        {
            result.Clear();
            if (card == null || card.IsCombo || card.sleepsWith == null || Deck == null)
                return;
            foreach (var other in Deck.deck)
                if (other is BallTypeCard ball && ball.ballPrefab
                    && System.Array.IndexOf(card.sleepsWith, ball.ballPrefab.Definition) >= 0)
                    result.Add(ball);
        }
    }
}

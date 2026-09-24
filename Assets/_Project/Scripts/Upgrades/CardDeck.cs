using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>Сколько шансов у каждой редкости. Нули — такая редкость отсюда не выпадает.</summary>
    [Serializable]
    public struct RarityWeights
    {
        [Min(0f)] public float common;
        [Min(0f)] public float rare;
        [Min(0f)] public float gold;

        public RarityWeights(float common, float rare, float gold)
        {
            this.common = common;
            this.rare = rare;
            this.gold = gold;
        }

        public float For(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => common,
            CardRarity.Rare => rare,
            _ => gold,
        };
    }

    /// <summary>
    /// Колода вкладышей забега: какие карточки есть, откуда какие редкости выпадают (старт, портфель, босс, ларёк)
    /// и цены в ларьке «Союзпечать».
    /// </summary>
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Card Deck", fileName = "CardDeck_")]
    public sealed class CardDeck : ScriptableObject
    {
        static readonly List<UpgradeCard> s_candidates = new();

        [Header("Вкладыши")]
        [Tooltip("Сколько карточек на выбор")]
        [Min(1)] public int choices = 3;
        public List<UpgradeCard> deck = new();
        [Tooltip("Выпадает, когда подходящих карточек меньше, чем мест на выбор")]
        public UpgradeCard filler;
        [Tooltip("Пауза перед показом выбора, с (реального времени)")]
        [Min(0f)] public float offerDelay = 0.35f;

        [Header("Какие редкости откуда выпадают")]
        [Tooltip("Стартовая карточка: 1 из 3 простых")]
        public RarityWeights start = new(1f, 0f, 0f);
        [Tooltip("Портфель с элитного врага или найденный на арене")]
        public RarityWeights portfolio = new(60f, 32f, 8f);
        [Tooltip("Карточка после босса: редкие и золотые")]
        public RarityWeights boss = new(0f, 75f, 25f);
        [Tooltip("Витрина ларька")]
        public RarityWeights shop = new(60f, 32f, 8f);
        [Tooltip("Доступная комбо-карточка выпадает во столько раз чаще обычной карточки своей редкости")]
        [Min(1f)] public float comboBoost = 4f;

        [Header("Ларёк «Союзпечать»: цены в монетках")]
        [Min(1)] public int shopSlots = 4;
        [Min(0)] public int priceCommon = 20;
        [Min(0)] public int priceRare = 40;
        [Min(0)] public int priceGold = 75;
        [Tooltip("Цены растут с каждой следующей ареной: 0.15 = +15%")]
        [Min(0f)] public float priceGrowth = 0.15f;
        [Tooltip("Перебрать витрину: первая цена за визит и прибавка за каждый следующий раз")]
        [Min(0)] public int rerollPrice = 5;
        [Min(0)] public int rerollStep = 3;
        [Min(0)] public int lemonadePrice = 10;
        [Tooltip("Сколько сердец возвращает лимонад")]
        [Min(1)] public int lemonadeHeal = 1;
        [Tooltip("Бутерброд возвращает все сердца")]
        [Min(0)] public int sandwichPrice = 25;
        [Tooltip("«Копилка»: +1 монетка за каждые столько монеток в кармане, когда открывается ларёк")]
        [Min(1)] public int interestPer = 10;

        public int PriceOf(CardRarity rarity, int arenaIndex)
        {
            int price = rarity switch
            {
                CardRarity.Common => priceCommon,
                CardRarity.Rare => priceRare,
                _ => priceGold,
            };
            return Mathf.RoundToInt(price * (1f + priceGrowth * Mathf.Max(0, arenaIndex)));
        }

        /// <summary>
        /// Добавить в result до count разных карточек: сначала по весам выбирается редкость (из тех, что ещё есть),
        /// потом карточка этой редкости по своему весу. available решает, можно ли карточку предложить сейчас.
        /// </summary>
        public void Roll(List<UpgradeCard> result, int count, in RarityWeights weights, Predicate<UpgradeCard> available)
        {
            s_candidates.Clear();
            foreach (var card in deck)
                if (card && card.weight > 0f && weights.For(card.rarity) > 0f && !result.Contains(card)
                    && !s_candidates.Contains(card) && available(card))
                    s_candidates.Add(card);

            for (int picked = 0; picked < count && s_candidates.Count > 0; picked++)
            {
                var rarity = PickRarity(weights);
                float total = 0f;
                foreach (var card in s_candidates)
                    if (card.rarity == rarity)
                        total += CardWeight(card);
                float roll = UnityEngine.Random.value * total;
                int pick = -1;
                for (int i = 0; i < s_candidates.Count; i++)
                {
                    if (s_candidates[i].rarity != rarity)
                        continue;
                    pick = i;
                    roll -= CardWeight(s_candidates[i]);
                    if (roll <= 0f)
                        break;
                }
                if (pick < 0)
                    break;
                result.Add(s_candidates[pick]);
                s_candidates.RemoveAt(pick);
            }
            s_candidates.Clear();
        }

        float CardWeight(UpgradeCard card) => card.IsCombo ? card.weight * comboBoost : card.weight;

        /// <summary>Редкость по весам среди тех, что ещё остались в кандидатах.</summary>
        static CardRarity PickRarity(in RarityWeights weights)
        {
            float common = Has(CardRarity.Common) ? weights.common : 0f;
            float rare = Has(CardRarity.Rare) ? weights.rare : 0f;
            float gold = Has(CardRarity.Gold) ? weights.gold : 0f;
            float roll = UnityEngine.Random.value * (common + rare + gold);
            if (roll < common)
                return CardRarity.Common;
            return roll < common + rare ? CardRarity.Rare : CardRarity.Gold;
        }

        static bool Has(CardRarity rarity)
        {
            foreach (var card in s_candidates)
                if (card.rarity == rarity)
                    return true;
            return false;
        }
    }
}

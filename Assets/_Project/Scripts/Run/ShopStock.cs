using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Upgrades;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>Что не так с покупкой — для отклика на экране.</summary>
    public enum PurchaseResult
    {
        Ok,
        NotEnoughCoins,
        /// <summary>Нечего покупать: жвачка продана, сердца полные и т.п.</summary>
        Unavailable,
        /// <summary>Карманы полны: сначала продать карточку.</summary>
        PocketsFull,
    }

    /// <summary>
    /// Витрина ларька «Союзпечать» на одну стоянку: жвачки с видимыми вкладышами по цене их редкости
    /// (каждая покупка делает следующие дороже), перебор витрины (каждый раз дороже), замок (жвачка ждёт
    /// в следующем ларьке), лимонад и бутерброд, продажа карточки из кармана за полцены — отдельно или обменом
    /// на жвачку, когда карманы полны.
    /// Монетки — в <see cref="RunState"/>, отложенные жвачки — в <see cref="RunCards.Locked"/> (всё — покупателя).
    /// </summary>
    public sealed class ShopStock
    {
        public sealed class Slot
        {
            public UpgradeCard Card;
            public int Price;
            public bool Locked;
            public bool Sold;
        }

        static readonly List<UpgradeCard> s_rolled = new();

        readonly CardDeck _deck;
        readonly PlayerCards _cards;
        readonly int _arenaIndex;
        readonly List<Slot> _slots = new();
        int _rerolls;
        int _purchases;

        public IReadOnlyList<Slot> Slots => _slots;
        /// <summary>Цена перебора: «Шпаргалка» делает первые переборы в этом ларьке бесплатными.</summary>
        public int RerollPrice
        {
            get
            {
                int free = _cards.Player.Modifiers.FreeRerolls;
                return _rerolls < free ? 0 : _deck.rerollPrice + _deck.rerollStep * (_rerolls - free);
            }
        }
        public int LemonadePrice => _deck.HealPrice(_deck.lemonadePrice, _arenaIndex);
        public int SandwichPrice => _deck.HealPrice(_deck.sandwichPrice, _arenaIndex);
        public PlayerCards Customer => _cards;
        /// <summary>Сколько дадут за карточку из кармана.</summary>
        public int SellPriceOf(UpgradeCard card) => _deck.SellPrice(card, _arenaIndex);
        public bool HeartsFull => _cards.Player.Health.Current >= _cards.Player.Health.Max;

        /// <summary>Витрина или цены изменились.</summary>
        public event Action Changed;

        public ShopStock(CardDeck deck, PlayerCards cards, int arenaIndex)
        {
            _deck = deck;
            _cards = cards;
            _arenaIndex = arenaIndex;
            for (int i = 0; i < deck.shopSlots; i++)
                _slots.Add(new Slot());
            Fill(keepLocked: true);
        }

        /// <summary>Жвачку с этого места можно купить сейчас (не продана, карточку можно взять).</summary>
        public bool CanBuy(int index) =>
            index >= 0 && index < _slots.Count && _slots[index].Card != null && !_slots[index].Sold
            && _cards.CanOffer(_slots[index].Card);

        /// <summary>
        /// Купить жвачку. Карманы полны — <see cref="PurchaseResult.PocketsFull"/>, если продажа одной из карточек
        /// покроет цену (тогда её можно обменять, <see cref="SellAndBuy"/>), иначе — не хватает монеток.
        /// </summary>
        public PurchaseResult Buy(int index)
        {
            if (!CanBuy(index))
                return PurchaseResult.Unavailable;
            var slot = _slots[index];
            if (_cards.PocketsFullFor(slot.Card))
                return RunState.CoinsOf(_cards.Slot) + BestSellPrice() >= slot.Price ? PurchaseResult.PocketsFull : PurchaseResult.NotEnoughCoins;
            if (!RunState.TrySpend(_cards.Slot, slot.Price))
                return PurchaseResult.NotEnoughCoins;
            slot.Sold = true;
            slot.Locked = false;
            RunCards.Locked(_cards.Slot).Remove(slot.Card);
            _cards.Take(slot.Card);
            _purchases++;
            UpdatePrices();
            GameEvents.PlaySound(SoundCue.Purchase, Vector3.zero);
            Changed?.Invoke();
            return PurchaseResult.Ok;
        }

        /// <summary>Продать карточку из кармана: монетки за неё, карточка возвращается в колоду.</summary>
        public PurchaseResult Sell(UpgradeCard card)
        {
            if (!CanSell(card))
                return PurchaseResult.Unavailable;
            int price = SellPriceOf(card);
            _cards.Discard(card);
            RunState.AddCoins(_cards.Slot, price);
            GameEvents.PlaySound(SoundCue.Purchase, Vector3.zero);
            Changed?.Invoke();
            return PurchaseResult.Ok;
        }

        public bool CanSell(UpgradeCard card) => card && _cards.Owns(card) && card.TakesPocket && !_cards.IsAbsorbed(card);

        /// <summary>Хватит ли монеток на жвачку index, если продать card.</summary>
        public bool SwapAffordable(int index, UpgradeCard card) =>
            CanBuy(index) && RunState.CoinsOf(_cards.Slot) + SellPriceOf(card) >= _slots[index].Price;

        /// <summary>
        /// Карманы полны: продать card и сразу купить жвачку index. Если монеток и после продажи не хватит,
        /// ничего не продаётся.
        /// </summary>
        public PurchaseResult SellAndBuy(int index, UpgradeCard card)
        {
            if (!CanBuy(index) || !CanSell(card))
                return PurchaseResult.Unavailable;
            if (!SwapAffordable(index, card))
                return PurchaseResult.NotEnoughCoins;
            Sell(card);
            return Buy(index);
        }

        /// <summary>Сколько дадут за самую дорогую карточку в карманах.</summary>
        int BestSellPrice()
        {
            int best = 0;
            foreach (var card in _cards.PocketCards)
                if (CanSell(card))
                    best = Mathf.Max(best, SellPriceOf(card));
            return best;
        }

        /// <summary>Показать другие жвачки на всех местах, кроме отложенных.</summary>
        public PurchaseResult Reroll()
        {
            if (!RunState.TrySpend(_cards.Slot, RerollPrice))
                return PurchaseResult.NotEnoughCoins;
            _rerolls++;
            Fill(keepLocked: false);
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
            Changed?.Invoke();
            return PurchaseResult.Ok;
        }

        /// <summary>Отложить жвачку до следующего ларька или снять замок.</summary>
        public void ToggleLock(int index)
        {
            if (index < 0 || index >= _slots.Count)
                return;
            var slot = _slots[index];
            if (slot.Card == null || slot.Sold)
                return;
            slot.Locked = !slot.Locked;
            if (slot.Locked)
            {
                if (!RunCards.Locked(_cards.Slot).Contains(slot.Card))
                    RunCards.Locked(_cards.Slot).Add(slot.Card);
            }
            else
            {
                RunCards.Locked(_cards.Slot).Remove(slot.Card);
            }
            GameEvents.PlaySound(SoundCue.UiMove, Vector3.zero);
            Changed?.Invoke();
        }

        public PurchaseResult BuyLemonade() => BuyHeal(LemonadePrice, _deck.lemonadeHeal);

        public PurchaseResult BuySandwich() => BuyHeal(SandwichPrice, int.MaxValue);

        PurchaseResult BuyHeal(int price, int amount)
        {
            var health = _cards.Player.Health;
            if (health.IsDead || health.Current >= health.Max)
                return PurchaseResult.Unavailable;
            if (!RunState.TrySpend(_cards.Slot, price))
                return PurchaseResult.NotEnoughCoins;
            health.Heal(Mathf.Min(amount, health.Max - health.Current));
            GameEvents.PlaySound(SoundCue.Purchase, Vector3.zero);
            Changed?.Invoke();
            return PurchaseResult.Ok;
        }

        /// <summary>
        /// Заполнить витрину. keepLocked: при открытии ларька отложенные в прошлом жвачки встают первыми.
        /// Без него (перебор) отложенные на своих местах остаются, остальные меняются.
        /// </summary>
        void Fill(bool keepLocked)
        {
            s_rolled.Clear();
            if (keepLocked)
            {
                // Отложенное, которое уже нельзя купить (взято из портфеля до предела), выбрасываем.
                RunCards.Locked(_cards.Slot).RemoveAll(card => card == null || !_cards.CanOffer(card));
                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    slot.Card = i < RunCards.Locked(_cards.Slot).Count ? RunCards.Locked(_cards.Slot)[i] : null;
                    slot.Locked = slot.Card != null;
                    slot.Sold = false;
                }
            }
            foreach (var slot in _slots)
                if (slot.Locked && slot.Card)
                    s_rolled.Add(slot.Card);

            int wanted = 0;
            foreach (var slot in _slots)
                if (!slot.Locked)
                    wanted++;
            int before = s_rolled.Count;
            _deck.Roll(s_rolled, wanted, _deck.shop, _cards.CanOffer);

            int next = before;
            foreach (var slot in _slots)
            {
                if (slot.Locked)
                    continue;
                slot.Card = next < s_rolled.Count ? s_rolled[next] : null;
                slot.Sold = false;
                next++;
            }
            UpdatePrices();
            s_rolled.Clear();
        }

        void UpdatePrices()
        {
            foreach (var slot in _slots)
                if (!slot.Sold)
                    slot.Price = slot.Card ? _deck.PriceOf(slot.Card.rarity, _arenaIndex, _purchases) : 0;
        }
    }
}

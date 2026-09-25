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
    }

    /// <summary>
    /// Витрина ларька «Союзпечать» на одну стоянку: жвачки с видимыми вкладышами по цене их редкости,
    /// перебор витрины (каждый раз дороже), замок (жвачка ждёт в следующем ларьке), лимонад и бутерброд.
    /// Монетки — в <see cref="RunState"/>, отложенные жвачки — в <see cref="RunCards.Locked"/>.
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
        public int LemonadePrice => _deck.lemonadePrice;
        public int SandwichPrice => _deck.sandwichPrice;
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

        public PurchaseResult Buy(int index)
        {
            if (index < 0 || index >= _slots.Count)
                return PurchaseResult.Unavailable;
            var slot = _slots[index];
            if (slot.Card == null || slot.Sold || !_cards.CanOffer(slot.Card))
                return PurchaseResult.Unavailable;
            if (!RunState.TrySpend(slot.Price))
                return PurchaseResult.NotEnoughCoins;
            slot.Sold = true;
            slot.Locked = false;
            RunCards.Locked.Remove(slot.Card);
            _cards.Take(slot.Card);
            GameEvents.PlaySound(SoundCue.Purchase, Vector3.zero);
            Changed?.Invoke();
            return PurchaseResult.Ok;
        }

        /// <summary>Показать другие жвачки на всех местах, кроме отложенных.</summary>
        public PurchaseResult Reroll()
        {
            if (!RunState.TrySpend(RerollPrice))
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
                if (!RunCards.Locked.Contains(slot.Card))
                    RunCards.Locked.Add(slot.Card);
            }
            else
            {
                RunCards.Locked.Remove(slot.Card);
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
            if (!RunState.TrySpend(price))
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
                RunCards.Locked.RemoveAll(card => card == null || !_cards.CanOffer(card));
                for (int i = 0; i < _slots.Count; i++)
                {
                    var slot = _slots[i];
                    slot.Card = i < RunCards.Locked.Count ? RunCards.Locked[i] : null;
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
            foreach (var slot in _slots)
                slot.Price = slot.Card ? _deck.PriceOf(slot.Card.rarity, _arenaIndex) : 0;
            s_rolled.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Bouncer.Upgrades
{
    /// <summary>Что будет с карманами, если взять карточку (подпись на вкладыше в выборе и в ларьке).</summary>
    public enum PocketNeed
    {
        /// <summary>Займёт свободный карман.</summary>
        New,
        /// <summary>Уже есть: повтор ляжет в тот же карман.</summary>
        Stack,
        /// <summary>Тип мяча, утешительный вкладыш, карточка-деньги: кармана не занимает.</summary>
        Free,
        /// <summary>Комбо: заберёт свои части и освободит карман.</summary>
        Combo,
        /// <summary>Нужен карман, а свободных нет: придётся выкинуть или продать одну.</summary>
        Full,
    }

    /// <summary>Откуда выбор «1 из 3».</summary>
    public enum OfferKind
    {
        /// <summary>Начало прогулки: 1 из 3 простых.</summary>
        Start,
        /// <summary>Портфель с элитного врага или найденный на арене.</summary>
        Portfolio,
        /// <summary>Босс выбит: редкие и золотые.</summary>
        Boss,
    }

    /// <summary>
    /// Карточки игрока за прогулку. Опыта и уровней нет: карточки дают старт, портфели, босс и ларёк.
    /// Выбор «1 из 3» ставится в очередь (<see cref="QueueOffer"/>) и открывается, когда игрок может действовать;
    /// на это время игра встаёт. Сам экран — в UI, он показывает <see cref="Offer"/> и передаёт <see cref="Choose"/>.
    /// На новой арене карточки прошлых арен применяются заново, а сердца берутся из <see cref="RunState.LivesOf"/>.
    /// Сборка ограничена: карманов <see cref="CardDeck.pockets"/> (повторы — один карман, комбо вбирает свои
    /// части), золотых и комбо за прогулку — не больше <see cref="CardDeck.maxGolds"/> / <see cref="CardDeck.maxCombos"/>.
    /// Если карманы полны, выбранная карточка ждёт (<see cref="PendingCard"/>), пока игрок не выкинет одну
    /// (<see cref="ResolveDiscard"/>); выкинутая возвращается в колоду, её действие снимается. Само предложение
    /// при этом не пропадает: можно вернуться к нему и взять другую (<see cref="CancelDiscard"/>).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCards : MonoBehaviour
    {
        [FormerlySerializedAs("definition")]
        [SerializeField] CardDeck deck;

        readonly Dictionary<UpgradeCard, int> _stacks = new();
        readonly List<UpgradeCard> _offer = new();
        readonly List<UpgradeCard> _pockets = new();
        readonly Queue<OfferKind> _pending = new();
        PlayerController _player;
        float _offerAt;

        public CardDeck Deck => deck;
        public PlayerController Player => _player;
        /// <summary>Карточки на выбор. Пусто — выбора сейчас нет.</summary>
        public IReadOnlyList<UpgradeCard> Offer => _offer;
        public OfferKind OfferKind { get; private set; }
        /// <summary>Открыт выбор «1 из 3» (а не решение, что выкинуть ради выбранной).</summary>
        public bool IsChoosing => _offer.Count > 0 && PendingCard == null;
        /// <summary>Сколько карточек взято за прогулку.</summary>
        public int Count => RunCards.Taken(Slot).Count;
        /// <summary>Номер игрока в прогулке: по нему лежат его карточки (<see cref="RunCards"/>) и монетки.</summary>
        public int Slot => _player ? _player.Slot : 0;
        public int MaxPockets => deck ? deck.pockets : 6;
        /// <summary>Выбранная карточка ждёт свободного кармана: игрок решает, что выкинуть.</summary>
        public UpgradeCard PendingCard { get; private set; }
        public bool IsDiscarding => PendingCard != null;

        /// <summary>Предложение карточек открылось, сменилось или закрылось.</summary>
        public event Action OfferChanged;
        /// <summary>Карточка взята: из выбора или куплена в ларьке.</summary>
        public event Action<UpgradeCard> Picked;
        /// <summary>Карточка выкинута или продана из кармана.</summary>
        public event Action<UpgradeCard> Discarded;
        /// <summary>Карманы полны и надо выбрать, что выкинуть (или выбор закрыт).</summary>
        public event Action DiscardChanged;

        void Awake() => _player = GetComponent<PlayerController>();

        void Start()
        {
            if (!RunState.Active)
                return;
            // Новая арена той же прогулки: игрок новый, поэтому взятое раньше применяется заново, без эффектов.
            UpgradeCard.Replaying = true;
            try
            {
                foreach (var card in RunCards.Taken(Slot))
                    ApplyCard(card);
            }
            finally
            {
                UpgradeCard.Replaying = false;
            }
            int lives = RunState.LivesOf(Slot);
            if (lives > 0)
                _player.Health.SetCurrent(lives);
            // «Бабушкины пирожки»: на каждой следующей арене прибавляется сердце.
            if (RunState.ArenaIndex > 0 && _player.Modifiers.ArenaHeal > 0)
                _player.Health.Heal(_player.Modifiers.ArenaHeal);
        }

        public int StacksOf(UpgradeCard card) => card && _stacks.TryGetValue(card, out int stacks) ? stacks : 0;

        public bool Owns(UpgradeCard card) => StacksOf(card) > 0;

        /// <summary>
        /// Можно ли предложить карточку сейчас: не набрана до предела, у комбо есть все части, золотых и комбо
        /// за прогулку не больше лимита колоды.
        /// </summary>
        public bool CanOffer(UpgradeCard card)
        {
            if (!card || !card.CanOffer(_player, StacksOf(card)))
                return false;
            if (card.IsCombo)
            {
                if (deck && RunCards.CombosTaken(Slot) >= deck.maxCombos)
                    return false;
                foreach (var part in card.requires)
                    if (!Owns(part))
                        return false;
            }
            else if (card.rarity == CardRarity.Gold && deck && RunCards.GoldsTaken >= deck.maxGolds && !Owns(card))
            {
                return false;
            }
            return true;
        }

        /// <summary>Карточки в карманах — по порядку взятия (части собранных комбо не считаются).</summary>
        public IReadOnlyList<UpgradeCard> PocketCards
        {
            get
            {
                _pockets.Clear();
                foreach (var card in RunCards.Taken(Slot))
                    if (card && card.TakesPocket && !_pockets.Contains(card) && !IsAbsorbed(card))
                        _pockets.Add(card);
                return _pockets;
            }
        }

        public int PocketsUsed => PocketCards.Count;

        /// <summary>Часть собранного комбо: своего кармана не занимает и отдельно не выкидывается.</summary>
        public bool IsAbsorbed(UpgradeCard card)
        {
            if (!card)
                return false;
            foreach (var taken in RunCards.Taken(Slot))
                if (taken && taken.IsCombo && Array.IndexOf(taken.requires, card) >= 0)
                    return true;
            return false;
        }

        /// <summary>Этой карточке нужен свободный карман, а его нет.</summary>
        public bool PocketsFullFor(UpgradeCard card) => card && NeedFor(card) == PocketNeed.Full;

        /// <summary>Что будет с карманами, если взять карточку.</summary>
        public PocketNeed NeedFor(UpgradeCard card)
        {
            if (!card || !card.TakesPocket)
                return PocketNeed.Free;
            if (card.IsCombo)
                return PocketNeed.Combo;
            if (Owns(card))
                return PocketNeed.Stack;
            return PocketsUsed >= MaxPockets ? PocketNeed.Full : PocketNeed.New;
        }

        /// <summary>
        /// Взятые карточки, которые кармана не занимают: мяч, которым игрок бросает сейчас (последний взятый тип
        /// мяча), и карточки-деньги по порядку взятия. Утешительные вкладыши не считаются.
        /// </summary>
        public void GetFreeCards(List<UpgradeCard> result)
        {
            result.Clear();
            UpgradeCard ball = null;
            foreach (var card in RunCards.Taken(Slot))
            {
                if (!card || card.TakesPocket || card.category == UpgradeCategory.Treat)
                    continue;
                if (card.category == UpgradeCategory.Ball)
                    ball = card;
                else if (!result.Contains(card))
                    result.Add(card);
            }
            if (ball)
                result.Insert(0, ball);
        }

        /// <summary>
        /// Выкинуть карточку из кармана (у комбо — вместе с частями): она возвращается в колоду, действие снимается.
        /// </summary>
        public void Discard(UpgradeCard card)
        {
            if (!card || !Owns(card))
                return;
            RunCards.Remove(Slot, card);
            if (card.IsCombo)
                foreach (var part in card.requires)
                    RunCards.Remove(Slot, part);
            RunCards.Locked(Slot).Remove(card);
            Rebuild();
            Discarded?.Invoke(card);
        }

        /// <summary>
        /// Карманы полны: выкинуть victim и взять ждущую карточку. victim — сама ждущая карточка или null —
        /// её не брать.
        /// </summary>
        public void ResolveDiscard(UpgradeCard victim)
        {
            var card = PendingCard;
            if (card == null)
                return;
            PendingCard = null;
            _offer.Clear();
            if (victim != null && victim != card)
            {
                Discard(victim);
                Take(card);
                GameEvents.PlaySound(SoundCue.CardPick, transform.position);
            }
            DiscardChanged?.Invoke();
            ContinueChoices();
        }

        /// <summary>Не выкидывать ничего и вернуться к выбору «1 из 3»: взять другую карточку.</summary>
        public void CancelDiscard()
        {
            if (PendingCard == null)
                return;
            PendingCard = null;
            DiscardChanged?.Invoke();
            OfferChanged?.Invoke();
        }

        /// <summary>Снять действие всех карточек и применить заново те, что остались (после выкидывания).</summary>
        void Rebuild()
        {
            var mods = _player.Modifiers;
            mods.Reset();
            _stacks.Clear();
            _player.Balls.ResetBallPrefab();
            UpgradeCard.Replaying = true;
            UpgradeCard.Rebuilding = true;
            try
            {
                foreach (var card in RunCards.Taken(Slot))
                    ApplyCard(card);
            }
            finally
            {
                UpgradeCard.Replaying = false;
                UpgradeCard.Rebuilding = false;
            }
            _player.Balls.ClampToMax();
            mods.NotifyChanged();
        }

        /// <summary>Поставить выбор «1 из 3» в очередь. Откроется, когда игрок сможет действовать.</summary>
        public void QueueOffer(OfferKind kind)
        {
            if (deck == null)
                return;
            _pending.Enqueue(kind);
            if (!IsChoosing)
                _offerAt = Time.unscaledTime + deck.offerDelay;
        }

        /// <summary>Взять карточку: сразу действует и запоминается на всю прогулку.</summary>
        public void Take(UpgradeCard card)
        {
            if (!card)
                return;
            ApplyCard(card);
            RunCards.Record(Slot, card);
            Picked?.Invoke(card);
        }

        void ApplyCard(UpgradeCard card)
        {
            if (!card)
                return;
            card.Apply(_player);
            _stacks[card] = StacksOf(card) + 1;
        }

        void Update()
        {
            if (deck == null)
                return;
            if (RunState.NeedsStartCard(Slot))
            {
                RunState.StartCardChosen(Slot);
                QueueOffer(OfferKind.Start);
            }
            if (_pending.Count == 0 || IsChoosing || IsDiscarding || Time.unscaledTime < _offerAt || _player.IsDead)
                return;
            // Выбор открывается только посреди обычной игры: не на паузе, не в ларьке, не во время перехода.
            var session = GameSession.Instance;
            if (session != null && !session.PlayerCanAct)
                return;
            if (!OpenNext())
                return;
            if (session != null)
                session.BeginUpgradeChoice();
            OfferChanged?.Invoke();
        }

        public void Choose(int index)
        {
            if (!IsChoosing || index < 0 || index >= _offer.Count)
                return;

            var card = _offer[index];
            // Карманы полны — сначала выбрать, что выкинуть; игра стоит, пока не решено. Предложение ждёт:
            // передумал — можно вернуться и взять другую.
            if (PocketsFullFor(card))
            {
                PendingCard = card;
                OfferChanged?.Invoke();
                DiscardChanged?.Invoke();
                return;
            }
            _offer.Clear();
            Take(card);
            GameEvents.PlaySound(SoundCue.CardPick, transform.position);
            ContinueChoices();
        }

        /// <summary>Несколько выборов подряд (босс и его портфель) — следующий сразу, без возврата в игру.</summary>
        void ContinueChoices()
        {
            if (!OpenNext() && GameSession.Instance != null)
                GameSession.Instance.EndUpgradeChoice();
            OfferChanged?.Invoke();
        }

        /// <summary>Собрать следующее предложение из очереди. false — очередь кончилась.</summary>
        bool OpenNext()
        {
            while (_pending.Count > 0)
            {
                var kind = _pending.Dequeue();
                if (BuildOffer(kind))
                {
                    OfferKind = kind;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Случайные карточки без повторов по редкостям источника; не хватает — добиваем любыми и утешительной.</summary>
        bool BuildOffer(OfferKind kind)
        {
            _offer.Clear();
            var weights = kind switch
            {
                OfferKind.Start => deck.start,
                OfferKind.Portfolio => deck.portfolio,
                _ => deck.boss,
            };
            // «Счастливый фантик»: в портфеле и за босса карточек на выбор больше.
            int choices = deck.choices + (kind == OfferKind.Start ? 0 : _player.Modifiers.ExtraChoices);
            deck.Roll(_offer, choices, weights, CanOffer);
            if (_offer.Count < choices)
                deck.Roll(_offer, choices - _offer.Count, new RarityWeights(1f, 1f, 1f), CanOffer);
            if (_offer.Count < choices && deck.filler && !_offer.Contains(deck.filler))
                _offer.Add(deck.filler);
            return _offer.Count > 0;
        }
    }
}

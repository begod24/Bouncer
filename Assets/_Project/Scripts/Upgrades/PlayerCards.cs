using System;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Bouncer.Upgrades
{
    public enum PocketNeed
    {
        New,
        Stack,
        Free,
        Combo,
        Full,
        Swap,
        ComboSame,
    }

    public enum OfferKind
    {
        Start,
        Portfolio,
        Boss,
    }

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
        bool _initialized;

        public CardDeck Deck => deck;
        public PlayerController Player => _player;
        public IReadOnlyList<UpgradeCard> Offer => _offer;
        public OfferKind OfferKind { get; private set; }
        public bool IsChoosing => _offer.Count > 0 && PendingCard == null;
        public int Count => RunCards.Taken(Slot).Count;
        public int Slot => _player ? _player.Slot : 0;
        public int MaxPockets => deck ? deck.PocketsFor(RunState.Active ? RunState.PlayerCount : 1) : 6;
        public UpgradeCard PendingCard { get; private set; }
        public bool IsDiscarding => PendingCard != null;
        public bool IsBusy => _offer.Count > 0 || PendingCard != null;
        public int Backpack => RunCards.BackpackOf(Slot);
        public bool StartPending => RunState.NeedsStartCard(Slot) || _pending.Contains(OfferKind.Start)
                                    || (_offer.Count > 0 && OfferKind == OfferKind.Start);

        public event Action OfferChanged;
        public event Action<UpgradeCard> Picked;
        public event Action<UpgradeCard> Discarded;
        public event Action DiscardChanged;
        public event Action BackpackChanged;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            if (deck)
                CardDeck.Active = deck;
        }

        void Start() => InitFromRun();

        public void InitFromRun()
        {
            if (_initialized || !RunState.Active || !_player.IsLocal)
                return;
            _initialized = true;
            UpgradeCard.Replaying = true;
            _player.Balls.BeginBatch();
            try
            {
                foreach (var card in RunCards.Taken(Slot))
                    ApplyCard(card);
            }
            finally
            {
                UpgradeCard.Replaying = false;
                _player.Balls.EndBatch();
            }
            bool resumed = RunState.TakeResume(Slot, out int hands, out bool down);
            int lives = RunState.LivesOf(Slot);
            if (lives > 0)
                _player.Health.SetCurrent(lives);
            var carried = RunCards.Carried(Slot);
            foreach (var kind in carried)
                QueueOffer(kind);
            carried.Clear();
            if (!resumed)
                return;
            _player.Balls.SetHands(hands);
            _player.Modifiers.NotifyChanged();
            BackpackChanged?.Invoke();
            if (down)
                _player.Health.Kill(default);
        }

        void OnDestroy()
        {
            if (!RunState.Active || _player == null || !_player.IsLocal || (_offer.Count == 0 && _pending.Count == 0))
                return;
            var carried = RunCards.Carried(Slot);
            if (_offer.Count > 0)
                carried.Add(OfferKind);
            carried.AddRange(_pending);
        }

        public void GetOpenOffers(List<OfferKind> result)
        {
            result.Clear();
            if (RunState.NeedsStartCard(Slot))
                result.Add(OfferKind.Start);
            if (_offer.Count > 0 || PendingCard != null)
                result.Add(OfferKind);
            result.AddRange(_pending);
            result.AddRange(RunCards.Carried(Slot));
        }

        public void AddToBackpack()
        {
            RunCards.SetBackpack(Slot, Backpack + 1);
            GameEvents.PlaySound(SoundCue.Portfolio, transform.position);
            BackpackChanged?.Invoke();
        }

        public bool OpenBackpack()
        {
            if (Backpack <= 0 || _player.IsDead || IsBusy || _pending.Count > 0)
                return false;
            RunCards.SetBackpack(Slot, Backpack - 1);
            QueueOffer(OfferKind.Portfolio);
            BackpackChanged?.Invoke();
            return true;
        }

        public void OpenWholeBackpack()
        {
            if (Backpack <= 0)
                return;
            for (int i = Backpack; i > 0; i--)
                QueueOffer(OfferKind.Portfolio);
            RunCards.SetBackpack(Slot, 0);
            BackpackChanged?.Invoke();
        }

        public int StacksOf(UpgradeCard card) => card && _stacks.TryGetValue(card, out int stacks) ? stacks : 0;

        public bool Owns(UpgradeCard card) => StacksOf(card) > 0;

        /// <summary>Под этим ключом собранное комбо открывается в Тетрадке.</summary>
        public static string ComboId(UpgradeCard combo) => combo ? "combo." + combo.name : null;

        /// <summary>Какой мяч в руках после всех взятых карточек: последняя карточка мяча, иначе обычный.</summary>
        public Ball CurrentBall
        {
            get
            {
                Ball ball = _player.Balls.DefaultBallPrefab;
                foreach (var taken in RunCards.Taken(Slot))
                    if (taken is BallTypeCard type && type.ballPrefab)
                        ball = type.ballPrefab;
                return ball;
            }
        }

        /// <summary>
        /// Спит ли карточка с нынешним мячом: такая лежит в кармане серой и ничего не делает
        /// (Попрыгунчик с футбольным, комбо мяча после смены мяча).
        /// </summary>
        public bool IsAsleep(UpgradeCard card) => card && card.SleepsWith(CurrentBall);

        /// <summary>
        /// Для предупреждения на карточке мяча: названия своих карточек, которые с ним уснут, через запятую.
        /// null — ничего не уснёт.
        /// </summary>
        public string SleepingWith(UpgradeCard card)
        {
            if (card is not BallTypeCard type || !type.ballPrefab)
                return null;
            var names = new List<string>();
            foreach (var pocket in PocketCards)
                if (!IsAsleep(pocket) && pocket.SleepsWith(type.ballPrefab))
                    names.Add(pocket.title.GetLocalizedString());
            return names.Count == 0 ? null : string.Join(", ", names);
        }

        public bool CanOffer(UpgradeCard card)
        {
            if (!card || !card.CanOffer(_player, StacksOf(card)))
                return false;
            if (card is AbilityCard && !card.IsCombo && OwnedAbility(card) is { IsCombo: true })
                return false;
            // спящая с нынешним мячом карточка не выпадает
            if (IsAsleep(card))
                return false;
            if (card.IsCombo)
            {
                if (deck && RunCards.CombosTaken(Slot) >= deck.maxCombos)
                    return false;
                // мяч — часть комбо, когда он в руках (IsAsleep выше), остальные части — в карманах
                foreach (var part in card.requires)
                    if (part is not BallTypeCard && !Owns(part))
                        return false;
            }
            else if (card.rarity == CardRarity.Gold && deck && !Owns(card)
                     && (OwnsGold() || RunCards.GoldsTaken >= deck.MaxGoldsFor(RunState.Active ? RunState.PlayerCount : 1)))
            {
                return false;
            }
            return true;
        }

        /// <summary>Своя золотая (не комбо) уже в карманах: вторая этому ребёнку не выпадает.</summary>
        bool OwnsGold()
        {
            foreach (var taken in RunCards.Taken(Slot))
                if (taken && !taken.IsCombo && taken.rarity == CardRarity.Gold)
                    return true;
            return false;
        }

        /// <summary>Сколько монеток дадут за карточку, если её продать или заменить другой.</summary>
        public int SellPrice(UpgradeCard card) => deck && card ? deck.SellPrice(card, RunState.ArenaIndex) : 0;

        /// <summary>Замена при полных карманах и смена умения: старая карточка уходит за полцены, как в ларьке.</summary>
        void SellAway(UpgradeCard card)
        {
            int price = SellPrice(card);
            Discard(card);
            if (price <= 0)
                return;
            RunState.AddCoins(Slot, price);
            GameEvents.PlaySound(SoundCue.Coin, transform.position);
        }

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

        public bool IsAbsorbed(UpgradeCard card)
        {
            if (!card)
                return false;
            foreach (var taken in RunCards.Taken(Slot))
                if (taken && taken.IsCombo && Array.IndexOf(taken.requires, card) >= 0)
                    return true;
            return false;
        }

        public string BlockReason(UpgradeCard card) => card && _player ? card.BlockReason(_player) : null;

        public string BallTitle()
        {
            if (deck != null)
                foreach (var card in deck.deck)
                    if (card is BallTypeCard type && type.ballPrefab == _player.Balls.BallPrefab && !card.title.IsEmpty)
                        return card.title.GetLocalizedString();
            return string.Empty;
        }

        public bool PocketsFullFor(UpgradeCard card) => card && NeedFor(card) == PocketNeed.Full;

        public PocketNeed NeedFor(UpgradeCard card)
        {
            if (!card || !card.TakesPocket)
                return PocketNeed.Free;
            if (card.IsCombo)
                // комбо мяча занимает карман своей карточки (мяч кармана не занимает) — не освобождает
                return card.BallPart != null ? PocketNeed.ComboSame : PocketNeed.Combo;
            if (Owns(card))
                return PocketNeed.Stack;
            if (card is AbilityCard && OwnedAbility(card) != null)
                return PocketNeed.Swap;
            return PocketsUsed >= MaxPockets ? PocketNeed.Full : PocketNeed.New;
        }

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

        public void Discard(UpgradeCard card)
        {
            if (!card || !Owns(card))
                return;
            RunCards.Remove(Slot, card);
            // комбо продаётся целиком, но мяч остаётся в руках
            if (card.IsCombo)
                foreach (var part in card.requires)
                    if (part is not BallTypeCard)
                        RunCards.Remove(Slot, part);
            RunCards.Locked(Slot).Remove(card);
            Rebuild();
            Discarded?.Invoke(card);
        }

        public void ResolveDiscard(UpgradeCard victim)
        {
            var card = PendingCard;
            if (card == null)
                return;
            PendingCard = null;
            _offer.Clear();
            if (victim != null && victim != card)
            {
                SellAway(victim);
                Take(card);
                GameEvents.PlaySound(SoundCue.CardPick, transform.position);
            }
            DiscardChanged?.Invoke();
            ContinueChoices();
        }

        public void CancelDiscard()
        {
            if (PendingCard == null)
                return;
            PendingCard = null;
            DiscardChanged?.Invoke();
            OfferChanged?.Invoke();
        }

        /// <summary>Выбор из портфеля можно закрыть и вернуться к нему позже (стартовый вкладыш — нет).</summary>
        public bool CanPostpone => IsChoosing && OfferKind != OfferKind.Start;

        /// <summary>
        /// «Отложить»: портфель возвращается в рюкзак (откроется по клавише рюкзака, как и найденные в коопе),
        /// трофей за босса уходит в «перенесённые» и снова предложится в начале следующей арены.
        /// </summary>
        public void Postpone()
        {
            if (!CanPostpone)
                return;
            var kind = OfferKind;
            _offer.Clear();
            if (kind == OfferKind.Boss)
            {
                RunCards.Carried(Slot).Add(kind);
            }
            else
            {
                RunCards.SetBackpack(Slot, Backpack + 1);
                BackpackChanged?.Invoke();
            }
            GameEvents.PlaySound(SoundCue.Portfolio, transform.position);
            ContinueChoices();
        }

        void Rebuild()
        {
            var mods = _player.Modifiers;
            // Пачка: пока бонусы сбрасываются и накладываются заново, мяч на миг становится обычным —
            // без пачки на этом шаге в руки выдавались лишние мячи. Число мячей сверяется один раз в конце.
            _player.Balls.BeginBatch();
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
                _player.Balls.EndBatch();
            }
            _player.RefreshMaxLives();
            mods.NotifyChanged();
        }

        public void QueueOffer(OfferKind kind)
        {
            if (deck == null)
                return;
            _pending.Enqueue(kind);
            if (!IsChoosing)
                _offerAt = Time.unscaledTime + deck.offerDelay;
        }

        public UpgradeCard OwnedAbility(UpgradeCard except = null)
        {
            foreach (var taken in RunCards.Taken(Slot))
            {
                if (taken is not AbilityCard || taken == except || IsAbsorbed(taken))
                    continue;
                if (except != null && System.Array.IndexOf(except.requires, taken) >= 0)
                    continue;
                return taken;
            }
            return null;
        }

        public void Take(UpgradeCard card)
        {
            if (!card)
                return;
            if (card is AbilityCard && !card.IsCombo && OwnedAbility(card) is { } old)
                SellAway(old);
            ApplyCard(card);
            RunCards.Record(Slot, card);
            if (card.IsCombo)
                BestiaryProgress.Open(ComboId(card));
            // новый мяч будит одни карточки и усыпляет другие — пересобираем бонусы под него
            if (card is BallTypeCard)
                Rebuild();
            Picked?.Invoke(card);
        }

        void ApplyCard(UpgradeCard card)
        {
            if (!card)
                return;
            // спящая карточка лежит в кармане, но не действует; мяч берётся по последней карточке мяча
            if (!IsAsleep(card))
                card.Apply(_player);
            _stacks[card] = StacksOf(card) + 1;
        }

        void Update()
        {
            if (deck == null || !_player.IsLocal)
                return;
            if (RunState.NeedsStartCard(Slot))
            {
                RunState.StartCardChosen(Slot);
                QueueOffer(OfferKind.Start);
            }
            var session = GameSession.Instance;
            if (Backpack > 0 && _player.LastIntent.BackpackPressed && (session == null || session.PlayerCanAct))
                OpenBackpack();
            if (_pending.Count == 0 || IsChoosing || IsDiscarding || Time.unscaledTime < _offerAt || _player.IsDead)
                return;
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
            if (BlockReason(card) != null)
            {
                GameEvents.PlaySound(SoundCue.NotEnoughCoins, transform.position);
                return;
            }
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

        void ContinueChoices()
        {
            if (!OpenNext() && GameSession.Instance != null)
                GameSession.Instance.EndUpgradeChoice();
            OfferChanged?.Invoke();
        }

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

        bool BuildOffer(OfferKind kind)
        {
            _offer.Clear();
            var weights = kind switch
            {
                OfferKind.Start => deck.start,
                OfferKind.Portfolio => deck.portfolio,
                _ => deck.boss,
            };
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

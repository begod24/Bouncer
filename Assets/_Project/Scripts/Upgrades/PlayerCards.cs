using System;
using System.Collections.Generic;
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

        void Awake() => _player = GetComponent<PlayerController>();

        void Start()
        {
            if (!RunState.Active || !_player.IsLocal)
                return;
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
            if (RunState.ArenaIndex > 0 && _player.Modifiers.ArenaHeal > 0)
                _player.Health.Heal(_player.Modifiers.ArenaHeal);
            var carried = RunCards.Carried(Slot);
            foreach (var kind in carried)
                QueueOffer(kind);
            carried.Clear();
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

        public bool PocketsFullFor(UpgradeCard card) => card && NeedFor(card) == PocketNeed.Full;

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
            if (card.IsCombo)
                foreach (var part in card.requires)
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
                Discard(victim);
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

        public void QueueOffer(OfferKind kind)
        {
            if (deck == null)
                return;
            _pending.Enqueue(kind);
            if (!IsChoosing)
                _offerAt = Time.unscaledTime + deck.offerDelay;
        }

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

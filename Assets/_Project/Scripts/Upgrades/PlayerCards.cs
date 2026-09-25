using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace Bouncer.Upgrades
{
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
    /// На новой арене карточки прошлых арен применяются заново, а сердца берутся из <see cref="RunState.Lives"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCards : MonoBehaviour
    {
        [FormerlySerializedAs("definition")]
        [SerializeField] CardDeck deck;

        readonly Dictionary<UpgradeCard, int> _stacks = new();
        readonly List<UpgradeCard> _offer = new();
        readonly Queue<OfferKind> _pending = new();
        PlayerController _player;
        float _offerAt;

        public CardDeck Deck => deck;
        public PlayerController Player => _player;
        /// <summary>Карточки на выбор. Пусто — выбора сейчас нет.</summary>
        public IReadOnlyList<UpgradeCard> Offer => _offer;
        public OfferKind OfferKind { get; private set; }
        public bool IsChoosing => _offer.Count > 0;
        /// <summary>Сколько карточек взято за прогулку.</summary>
        public int Count => RunCards.Taken.Count;

        /// <summary>Предложение карточек открылось, сменилось или закрылось.</summary>
        public event Action OfferChanged;
        /// <summary>Карточка взята: из выбора или куплена в ларьке.</summary>
        public event Action<UpgradeCard> Picked;

        void Awake() => _player = GetComponent<PlayerController>();

        void Start()
        {
            if (!RunState.Active)
                return;
            // Новая арена той же прогулки: игрок новый, поэтому взятое раньше применяется заново, без эффектов.
            UpgradeCard.Replaying = true;
            try
            {
                foreach (var card in RunCards.Taken)
                    ApplyCard(card);
            }
            finally
            {
                UpgradeCard.Replaying = false;
            }
            if (RunState.Lives > 0)
                _player.Health.SetCurrent(RunState.Lives);
            // «Бабушкины пирожки»: на каждой следующей арене прибавляется сердце.
            if (RunState.ArenaIndex > 0 && _player.Modifiers.ArenaHeal > 0)
                _player.Health.Heal(_player.Modifiers.ArenaHeal);
        }

        public int StacksOf(UpgradeCard card) => card && _stacks.TryGetValue(card, out int stacks) ? stacks : 0;

        public bool Owns(UpgradeCard card) => StacksOf(card) > 0;

        /// <summary>Можно ли предложить карточку сейчас: не набрана до предела, а у комбо есть все части.</summary>
        public bool CanOffer(UpgradeCard card)
        {
            if (!card || !card.CanOffer(_player, StacksOf(card)))
                return false;
            if (card.IsCombo)
                foreach (var part in card.requires)
                    if (!Owns(part))
                        return false;
            return true;
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
            RunCards.Record(card);
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
            if (RunState.Active && RunState.NeedsStartCard)
            {
                RunState.NeedsStartCard = false;
                QueueOffer(OfferKind.Start);
            }
            if (_pending.Count == 0 || IsChoosing || Time.unscaledTime < _offerAt || _player.IsDead)
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
            if (index < 0 || index >= _offer.Count)
                return;

            var card = _offer[index];
            _offer.Clear();
            Take(card);
            GameEvents.PlaySound(SoundCue.CardPick, transform.position);

            // Несколько выборов подряд (босс и его портфель) — следующий сразу, без возврата в игру.
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

using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>
    /// Опыт и уровни игрока за забег. Опыт даётся за выбитых врагов (<see cref="EnemyReward"/>).
    /// На новом уровне забег замирает и игроку предлагаются карточки (<see cref="Offer"/>); выбор — <see cref="Choose"/>.
    /// Сам экран карточек — в UI, он только показывает предложение и передаёт выбор.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerProgression : MonoBehaviour
    {
        [SerializeField] ProgressionDefinition definition;

        readonly Dictionary<UpgradeCard, int> _stacks = new();
        readonly List<UpgradeCard> _offer = new();
        readonly List<UpgradeCard> _candidates = new();
        PlayerController _player;
        float _offerAt;

        public int Level { get; private set; } = 1;
        /// <summary>Опыт, набранный на текущем уровне.</summary>
        public int Experience { get; private set; }
        public int ExperienceToNext => definition ? definition.ExperienceFor(Level) : 1;
        public float Experience01 => Mathf.Clamp01((float)Experience / Mathf.Max(1, ExperienceToNext));
        /// <summary>Новые уровни, за которые карточка ещё не выбрана.</summary>
        public int PendingLevelUps { get; private set; }
        /// <summary>Карточки на выбор. Пусто — выбора сейчас нет.</summary>
        public IReadOnlyList<UpgradeCard> Offer => _offer;
        public bool IsChoosing => _offer.Count > 0;

        public event Action<int> LeveledUp;
        /// <summary>Предложение карточек открылось, сменилось или закрылось.</summary>
        public event Action OfferChanged;
        public event Action<UpgradeCard> Picked;

        void Awake() => _player = GetComponent<PlayerController>();

        void OnEnable() => GameEvents.EnemyKilled += OnEnemyKilled;

        void OnDisable() => GameEvents.EnemyKilled -= OnEnemyKilled;

        public int StacksOf(UpgradeCard card) => card && _stacks.TryGetValue(card, out int stacks) ? stacks : 0;

        public void AddExperience(int amount)
        {
            if (amount <= 0 || definition == null)
                return;
            Experience += amount;
            while (Experience >= ExperienceToNext)
            {
                Experience -= ExperienceToNext;
                Level++;
                PendingLevelUps++;
                _offerAt = Time.unscaledTime + definition.offerDelay;
                GameEvents.PlaySound(SoundCue.LevelUp, transform.position);
                LeveledUp?.Invoke(Level);
            }
        }

        void Update()
        {
            // Карточки открываются не в момент выбивания, а чуть позже и только посреди обычной игры.
            if (PendingLevelUps == 0 || IsChoosing || Time.unscaledTime < _offerAt || _player.IsDead)
                return;
            var session = GameSession.Instance;
            if (session != null && !session.IsPlaying)
                return;

            BuildOffer();
            if (_offer.Count == 0)
            {
                PendingLevelUps = 0;
                return;
            }
            if (session != null)
                session.BeginUpgradeChoice();
            OfferChanged?.Invoke();
        }

        public void Choose(int index)
        {
            if (index < 0 || index >= _offer.Count)
                return;

            var card = _offer[index];
            card.Apply(_player);
            _stacks[card] = StacksOf(card) + 1;
            GameEvents.PlaySound(SoundCue.CardPick, transform.position);
            PendingLevelUps = Mathf.Max(0, PendingLevelUps - 1);
            _offer.Clear();
            Picked?.Invoke(card);

            // Несколько уровней подряд — следующее предложение сразу, без возврата в игру.
            if (PendingLevelUps > 0)
                BuildOffer();
            if (_offer.Count == 0)
            {
                PendingLevelUps = 0;
                if (GameSession.Instance != null)
                    GameSession.Instance.EndUpgradeChoice();
            }
            OfferChanged?.Invoke();
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (!_player.IsDead && enemy && enemy.TryGetComponent(out EnemyReward reward))
                AddExperience(reward.Experience);
        }

        /// <summary>Случайные карточки без повторов, с учётом веса; не хватает — добиваем утешительной.</summary>
        void BuildOffer()
        {
            _offer.Clear();
            _candidates.Clear();
            foreach (var card in definition.deck)
                if (card && card.weight > 0f && card.CanOffer(_player, StacksOf(card)) && !_candidates.Contains(card))
                    _candidates.Add(card);

            while (_offer.Count < definition.choices && _candidates.Count > 0)
            {
                float total = 0f;
                foreach (var card in _candidates)
                    total += card.weight;
                float roll = UnityEngine.Random.value * total;
                int pick = _candidates.Count - 1;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    roll -= _candidates[i].weight;
                    if (roll <= 0f)
                    {
                        pick = i;
                        break;
                    }
                }
                _offer.Add(_candidates[pick]);
                _candidates.RemoveAt(pick);
            }

            if (_offer.Count < definition.choices && definition.filler && !_offer.Contains(definition.filler))
                _offer.Add(definition.filler);
        }
    }
}

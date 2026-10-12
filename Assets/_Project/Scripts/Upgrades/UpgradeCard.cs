using Bouncer.Core;
using Bouncer.Player;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Upgrades
{
    public enum UpgradeCategory
    {
        Ball,
        Modifier,
        Passive,
        Treat,
    }

    public enum CardRarity
    {
        Common,
        Rare,
        Gold,
    }

    public abstract class UpgradeCard : ScriptableObject
    {
        [Header("Вкладыш")]
        [Tooltip("Название и описание — строки таблицы «Content» (card.<имя>.title / .description)")]
        public LocalizedString title;
        public LocalizedString description;
        public UpgradeCategory category;
        [Tooltip("Картинка на вкладыше")]
        public Sprite icon;
        [Tooltip("Цвет обёртки вкладыша")]
        public Color wrapperColor = new(0.95f, 0.45f, 0.35f);

        [Header("Выпадение")]
        public CardRarity rarity;
        [Tooltip("Сколько раз за забег можно взять эту карточку")]
        [Min(1)] public int maxStacks = 1;
        [Tooltip("Чем больше, тем чаще выпадает среди карточек своей редкости")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("Комбо-карточка: выпадает, только когда у игрока уже есть все эти карточки")]
        public UpgradeCard[] requires = System.Array.Empty<UpgradeCard>();
        [Tooltip("Карточка-деньги («Копилка», «Шпаргалка», «Счастливый фантик»): не занимает карман")]
        public bool freePocket;
        [Tooltip("Выпадает только в игре вдвоём и больше")]
        public bool coopOnly;
        [Tooltip("С этими мячами карточка спит: не выпадает, в кармане серая и ничего не делает, " +
                 "пока в руках такой мяч (Попрыгунчик и футбольный, Стеночка и набивной…)")]
        public Bouncer.Balls.BallDefinition[] sleepsWith = System.Array.Empty<Bouncer.Balls.BallDefinition>();

        public static bool Replaying { get; internal set; }

        public static bool Rebuilding { get; internal set; }

        public bool IsCombo => requires != null && requires.Length > 0;

        /// <summary>Комбо мяча: среди частей есть карточка мяча — комбо работает, только пока этот мяч в руках.</summary>
        public BallTypeCard BallPart
        {
            get
            {
                if (requires != null)
                    foreach (var part in requires)
                        if (part is BallTypeCard ball)
                            return ball;
                return null;
            }
        }

        /// <summary>Спит ли карточка, когда в руках мяч <paramref name="ball"/>.</summary>
        public bool SleepsWith(Bouncer.Balls.Ball ball)
        {
            if (IsCombo)
                return BallPart is { } part && part.ballPrefab != ball;
            if (ball == null || sleepsWith == null)
                return false;
            return System.Array.IndexOf(sleepsWith, ball.Definition) >= 0;
        }

        public bool TakesPocket => category != UpgradeCategory.Ball && category != UpgradeCategory.Treat && !freePocket;

        public virtual bool CanOffer(PlayerController player, int stacks) =>
            stacks < maxStacks && (!coopOnly || (RunState.Active && RunState.PlayerCount > 1));

        public virtual string BlockReason(PlayerController player) => null;

        public abstract void Apply(PlayerController player);
    }
}

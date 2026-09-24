using Bouncer.Player;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Upgrades
{
    public enum UpgradeCategory
    {
        /// <summary>Другой мяч: волейбольный, набивной, теннисный.</summary>
        Ball,
        /// <summary>Модификатор мяча: бумеранг, раскол, резинка.</summary>
        Modifier,
        /// <summary>Пассивка игрока: скорость, ловля, подбор, рывок.</summary>
        Passive,
        /// <summary>Утешительный вкладыш, когда подходящих карточек не осталось.</summary>
        Treat,
    }

    /// <summary>Редкость вкладыша — как сорта жвачки в ларьке. От неё зависят цена и где карточка выпадает.</summary>
    public enum CardRarity
    {
        Common,
        Rare,
        Gold,
    }

    /// <summary>
    /// Карточка-вкладыш: выпадает на выбор 1 из 3 (старт, портфель, босс) или продаётся в ларьке.
    /// Потомки решают, что она делает с игроком.
    /// </summary>
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

        /// <summary>
        /// Карточки заново применяются на новой арене (сцена новая — игрок тоже). В это время мгновенные эффекты
        /// вроде лечения не срабатывают: сердца переносятся отдельно.
        /// </summary>
        public static bool Replaying { get; internal set; }

        public bool IsCombo => requires != null && requires.Length > 0;

        /// <summary>Можно ли предложить карточку сейчас. stacks — сколько раз её уже взяли.</summary>
        public virtual bool CanOffer(PlayerController player, int stacks) => stacks < maxStacks;

        public abstract void Apply(PlayerController player);
    }
}

using Bouncer.Player;
using UnityEngine;

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

    /// <summary>
    /// Карточка-вкладыш: выпадает 1 из 3 при новом уровне. Потомки решают, что она делает с игроком.
    /// </summary>
    public abstract class UpgradeCard : ScriptableObject
    {
        [Header("Вкладыш")]
        public string title = "Вкладыш";
        [TextArea(2, 4)] public string description;
        public UpgradeCategory category;
        [Tooltip("Картинка на вкладыше")]
        public Sprite icon;
        [Tooltip("Цвет обёртки вкладыша")]
        public Color wrapperColor = new(0.95f, 0.45f, 0.35f);

        [Header("Выпадение")]
        [Tooltip("Сколько раз за забег можно взять эту карточку")]
        [Min(1)] public int maxStacks = 1;
        [Tooltip("Чем больше, тем чаще выпадает")]
        [Min(0f)] public float weight = 1f;

        /// <summary>Можно ли предложить карточку сейчас. stacks — сколько раз её уже взяли.</summary>
        public virtual bool CanOffer(PlayerController player, int stacks) => stacks < maxStacks;

        public abstract void Apply(PlayerController player);
    }
}

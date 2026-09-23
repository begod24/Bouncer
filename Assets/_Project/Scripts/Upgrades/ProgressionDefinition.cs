using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>Опыт по уровням и колода карточек-вкладышей забега.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Progression", fileName = "Progression_")]
    public sealed class ProgressionDefinition : ScriptableObject
    {
        [Header("Опыт до следующего уровня = база + рост × (уровень − 1)")]
        [Min(1)] public int experienceBase = 6;
        [Min(0)] public int experienceGrowth = 3;
        [Tooltip("Пауза после нового уровня перед показом карточек, с (реального времени)")]
        [Min(0f)] public float offerDelay = 0.35f;

        [Header("Вкладыши")]
        [Tooltip("Сколько карточек на выбор")]
        [Min(1)] public int choices = 3;
        public List<UpgradeCard> deck = new();
        [Tooltip("Выпадает, когда подходящих карточек в колоде меньше, чем мест на выбор")]
        public UpgradeCard filler;

        public int ExperienceFor(int level) => experienceBase + experienceGrowth * Mathf.Max(0, level - 1);
    }
}

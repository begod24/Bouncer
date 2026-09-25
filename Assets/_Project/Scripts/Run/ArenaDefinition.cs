using Bouncer.Core;
using Bouncer.Visuals;
using Bouncer.Waves;
using UnityEngine;
using UnityEngine.Localization;

namespace Bouncer.Run
{
    /// <summary>Когда арена пройдена.</summary>
    public enum ArenaGoal
    {
        /// <summary>Босс арены выбит целиком.</summary>
        DefeatBoss,
        /// <summary>Волны кончились (последней выходит пачка элитных) и все враги выбиты.</summary>
        SurviveAndClear,
        /// <summary>Финал: продержаться до зова мамы — или выбить босса раньше.</summary>
        SurviveUntilCall,
    }

    /// <summary>
    /// Одна арена прогулки: сцена, волны, время суток, условие победы, ларёк и стрелка дальше.
    /// Одна сцена может играть несколько арен (двор утром и двор ночью) — разница только в этих данных.
    /// </summary>
    [CreateAssetMenu(menuName = "Bouncer/Run/Arena", fileName = "Arena_")]
    public sealed class ArenaDefinition : ScriptableObject
    {
        [Tooltip("Сцена арены — должна быть в списке сборки")]
        public string sceneName = "Yard";
        [Tooltip("Название на плашке в начале арены — строка таблицы «UI»")]
        public LocalizedString title;
        public WaveDefinition wave;
        [Tooltip("Время суток по ходу боя: ключи равномерно распределены от начала до конца волн")]
        public TimeOfDayProfile[] timeOfDay;
        public ArenaGoal goal = ArenaGoal.DefeatBoss;

        [Header("После боя")]
        [Tooltip("На пройденной арене открывается ларёк «Союзпечать»")]
        public bool kiosk = true;
        [Tooltip("Надпись у стрелки, которая ведёт СЮДА с прошлой арены (например, «В коробку →») — строка таблицы «UI»")]
        public LocalizedString arrivalLabel;

        [Header("Находки")]
        [Tooltip("Сколько портфелей за бой находится на арене")]
        [Min(0)] public int portfolioFinds = 1;
        [Tooltip("Портфель появляется в случайный момент между этими долями боя")]
        public Vector2 portfolioWindow = new(0.3f, 0.6f);

        [Header("Погода — случайное событие")]
        [Tooltip("С какой вероятностью на арене непогода")]
        [Range(0f, 1f)] public float weatherChance = 0.3f;
        [Tooltip("Какая непогода может выпасть (одна из списка)")]
        public WeatherKind[] weathers = { WeatherKind.Rain, WeatherKind.Storm, WeatherKind.Fog };
    }
}

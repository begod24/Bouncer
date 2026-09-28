using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Уровни опасности прогулки (как Danger в Brotato): 1 — обычная игра, каждый следующий открывается победой
    /// на предыдущем и добавляет своё к прошлым. Открытые уровни общие для всех детей и хранятся в PlayerPrefs.
    /// Уровень прогулки лежит в <see cref="RunState.Danger"/>; остальные читают отсюда готовые множители.
    /// </summary>
    public static class Danger
    {
        public const int Max = 5;
        const string UnlockedKey = "bouncer.danger.unlocked";
        const string SelectedKey = "bouncer.danger.selected";

        /// <summary>Опасность текущей прогулки.</summary>
        public static int Level => Mathf.Clamp(RunState.Danger, 1, Max);

        /// <summary>До какого уровня открыто.</summary>
        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Max);

        /// <summary>Какой уровень выбран для следующей прогулки (запоминается между запусками).</summary>
        public static int Selected
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 1), 1, Unlocked);
            set
            {
                PlayerPrefs.SetInt(SelectedKey, Mathf.Clamp(value, 1, Unlocked));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Прогулка на этом уровне выиграна: открыть следующий. true — открыт новый.</summary>
        public static bool UnlockAfterWin(int level)
        {
            if (level < Unlocked || level >= Max)
                return false;
            PlayerPrefs.SetInt(UnlockedKey, level + 1);
            PlayerPrefs.Save();
            return true;
        }

        // 2: +1 элитка на арену, у элиток всегда есть свойство.
        public static int ExtraElites => Level >= 2 ? 1 : 0;
        public static bool EliteAffixAlways => Level >= 2;
        /// <summary>С каким шансом элитка получает свойство («шустрый», «командир», «ловкач»). В обучении — без свойств.</summary>
        public static float EliteAffixChance => Tutorial.Active ? 0f : EliteAffixAlways ? 1f : 0.5f;

        // 3: враги на 10% быстрее, солдатики чаще кидают ёжиков (в обучении — никогда: там учатся ловить).
        public static float EnemySpeed => Level >= 3 ? 1.1f : 1f;
        public static float HedgehogChance => Tutorial.Active ? 0f : Level >= 3 ? 0.35f : 0.2f;

        // 4: монеток на четверть меньше, лечение в ларьке в полтора раза дороже.
        public static float CoinMultiplier => Level >= 4 ? 0.75f : 1f;
        public static float HealPriceMultiplier => Level >= 4 ? 1.5f : 1f;

        // 5: боссы живучее и чаще финтят, мама зовёт позже.
        public static float BossHitsMultiplier => Level >= 5 ? 1.3f : 1f;
        public static float FeintChance => Level >= 5 ? 0.5f : 0.25f;
        public static float FinalExtraTime => Level >= 5 ? 30f : 0f;
    }
}

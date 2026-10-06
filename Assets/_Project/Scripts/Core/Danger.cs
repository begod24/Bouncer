using UnityEngine;

namespace Bouncer.Core
{
    public static class Danger
    {
        public const int Max = 5;
        const string UnlockedKey = "bouncer.danger.unlocked";
        const string SelectedKey = "bouncer.danger.selected";

        public static int Level => Mathf.Clamp(RunState.Danger, 1, Max);

        public static int Unlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, 1), 1, Max);

        public static int Selected
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, 1), 1, Unlocked);
            set
            {
                PlayerPrefs.SetInt(SelectedKey, Mathf.Clamp(value, 1, Unlocked));
                PlayerPrefs.Save();
            }
        }

        public static bool UnlockAfterWin(int level)
        {
            if (level < Unlocked || level >= Max)
                return false;
            PlayerPrefs.SetInt(UnlockedKey, level + 1);
            PlayerPrefs.Save();
            return true;
        }

        public static int ExtraElites => Level >= 2 ? 1 : 0;
        public static bool EliteAffixAlways => Level >= 2;
        public static float EliteAffixChance => Tutorial.Active ? 0f : EliteAffixAlways ? 1f : 0.5f;

        public static float EnemySpeed => Level >= 3 ? 1.1f : 1f;
        public static float HedgehogChance => Tutorial.Active ? 0f : Level >= 3 ? 0.35f : 0.2f;

        public static float CoinMultiplier => Level >= 4 ? 0.75f : 1f;
        public static float HealPriceMultiplier => Level >= 4 ? 1.5f : 1f;

        public static float BossHitsMultiplier => Level >= 5 ? 1.3f : 1f;
        public static float FeintChance => Level >= 5 ? 0.5f : 0.25f;
        public static float FinalExtraTime => Level >= 5 ? 30f : 0f;
    }
}

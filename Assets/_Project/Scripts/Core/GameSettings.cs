using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Настройки игрока (экран «Настройки»). Хранятся в PlayerPrefs.
    /// Разрешение и режим окна движок запоминает сам, здесь их нет. Все звуки игры идут через
    /// SoundPlayer и MusicPlayer, поэтому общая громкость — множитель в их громкости, а не AudioListener.volume.
    /// </summary>
    public static class GameSettings
    {
        public static bool AutoAimMouse;
        public static bool AutoAimGamepad = true;
        public static bool ShowAimPreview = true;

        /// <summary>Громкости — положение ползунка 0–1. В AudioSource идёт <see cref="VolumeGain"/>.</summary>
        public static float MasterVolume = 1f;
        public static float MusicVolume = 1f;
        public static float SfxVolume = 1f;
        /// <summary>Тряска камеры, 0–1: множитель поверх тряски из GameFeel.</summary>
        public static float ScreenShake = 1f;
        public static bool VSync = true;
        /// <summary>Ребёнок, с которым игрок выходит гулять (номер в KidRoster). Запоминается между запусками.</summary>
        public static int Kid;

        public static bool AutoAimFor(bool gamepad) => gamepad ? AutoAimGamepad : AutoAimMouse;

        /// <summary>Ползунок громкости → множитель: квадрат, чтобы на слух ползунок шёл равномерно.</summary>
        public static float VolumeGain(float slider) => slider * slider;

        public static float MusicGain => VolumeGain(MasterVolume) * VolumeGain(MusicVolume);
        public static float SfxGain => VolumeGain(MasterVolume) * VolumeGain(SfxVolume);

        const string Prefix = "Bouncer.Settings.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            AutoAimMouse = GetBool(nameof(AutoAimMouse), false);
            AutoAimGamepad = GetBool(nameof(AutoAimGamepad), true);
            ShowAimPreview = GetBool(nameof(ShowAimPreview), true);
            MasterVolume = GetFloat(nameof(MasterVolume), 1f);
            MusicVolume = GetFloat(nameof(MusicVolume), 1f);
            SfxVolume = GetFloat(nameof(SfxVolume), 1f);
            ScreenShake = GetFloat(nameof(ScreenShake), 1f);
            VSync = GetBool(nameof(VSync), true);
            Kid = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + nameof(Kid), 0));
            Apply();
        }

        /// <summary>Передать движку вертикальную синхронизацию.</summary>
        public static void Apply()
        {
            // В редакторе синхронизацию задаёт окно Game, а смена из кода осталась бы в настройках качества проекта.
            if (!Application.isEditor)
                QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        public static void Save()
        {
            SetBool(nameof(AutoAimMouse), AutoAimMouse);
            SetBool(nameof(AutoAimGamepad), AutoAimGamepad);
            SetBool(nameof(ShowAimPreview), ShowAimPreview);
            PlayerPrefs.SetFloat(Prefix + nameof(MasterVolume), MasterVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(MusicVolume), MusicVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(SfxVolume), SfxVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(ScreenShake), ScreenShake);
            SetBool(nameof(VSync), VSync);
            PlayerPrefs.SetInt(Prefix + nameof(Kid), Kid);
            PlayerPrefs.Save();
        }

        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;

        static void SetBool(string key, bool value) => PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);

        static float GetFloat(string key, float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, fallback));
    }
}

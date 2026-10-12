using UnityEngine;

namespace Bouncer.Core
{
    public static class GameSettings
    {
        public static bool AutoAimMouse;
        public static bool AutoAimGamepad = true;
        public static bool ShowAimPreview = true;
        public static bool ShowHints = true;

        public static float MasterVolume = 1f;
        public static float MusicVolume = 1f;
        public static float SfxVolume = 1f;
        public static float ScreenShake = 1f;
        public static bool VSync = true;
        /// <summary>Номер в <see cref="FrameLimits"/>: лимит кадров, когда VSync выключен.</summary>
        public static int FrameLimit;
        public static int Kid;
        public static string PlayerName = "";

        public const int PlayerNameLength = 16;

        /// <summary>Лимит кадров без VSync: 0 — как частота монитора, −1 — без лимита.</summary>
        public static readonly int[] FrameLimits = { 0, 30, 60, 120, 144, -1 };

        public static bool AutoAimFor(bool gamepad) => gamepad ? AutoAimGamepad : AutoAimMouse;

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
            ShowHints = GetBool(nameof(ShowHints), true);
            MasterVolume = GetFloat(nameof(MasterVolume), 1f);
            MusicVolume = GetFloat(nameof(MusicVolume), 1f);
            SfxVolume = GetFloat(nameof(SfxVolume), 1f);
            ScreenShake = GetFloat(nameof(ScreenShake), 1f);
            VSync = GetBool(nameof(VSync), true);
            FrameLimit = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + nameof(FrameLimit), 0), 0, FrameLimits.Length - 1);
            Kid = Mathf.Max(0, PlayerPrefs.GetInt(Prefix + nameof(Kid), 0));
            PlayerName = PlayerPrefs.GetString(Prefix + nameof(PlayerName), "");
            Apply();
        }

        public static void Apply()
        {
            if (!Application.isEditor)
                QualitySettings.vSyncCount = VSync ? 1 : 0;
            // С VSync кадры и так идут по монитору; без него лимит не даёт видеокарте крутить сотни кадров.
            Application.targetFrameRate = VSync ? -1 : TargetFrameRate(FrameLimits[Mathf.Clamp(FrameLimit, 0, FrameLimits.Length - 1)]);
        }

        static int TargetFrameRate(int limit)
        {
            if (limit != 0)
                return limit;
            double hz = Screen.currentResolution.refreshRateRatio.value;
            return hz >= 1d ? Mathf.RoundToInt((float)hz) : 60;
        }

        public static void Save()
        {
            SetBool(nameof(AutoAimMouse), AutoAimMouse);
            SetBool(nameof(AutoAimGamepad), AutoAimGamepad);
            SetBool(nameof(ShowAimPreview), ShowAimPreview);
            SetBool(nameof(ShowHints), ShowHints);
            PlayerPrefs.SetFloat(Prefix + nameof(MasterVolume), MasterVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(MusicVolume), MusicVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(SfxVolume), SfxVolume);
            PlayerPrefs.SetFloat(Prefix + nameof(ScreenShake), ScreenShake);
            SetBool(nameof(VSync), VSync);
            PlayerPrefs.SetInt(Prefix + nameof(FrameLimit), FrameLimit);
            PlayerPrefs.SetInt(Prefix + nameof(Kid), Kid);
            PlayerPrefs.SetString(Prefix + nameof(PlayerName), PlayerName ?? "");
            PlayerPrefs.Save();
        }

        static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;

        static void SetBool(string key, bool value) => PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);

        static float GetFloat(string key, float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + key, fallback));
    }
}

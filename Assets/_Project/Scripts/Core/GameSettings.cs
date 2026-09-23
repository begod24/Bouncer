using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Настройки игрока (позже переедут в меню настроек). Хранятся в PlayerPrefs.</summary>
    public static class GameSettings
    {
        public static bool AutoAimMouse;
        public static bool AutoAimGamepad = true;
        public static bool ShowAimPreview = true;
        public static bool GodMode;

        /// <summary>Курсор над отладочным окном — клики мыши не должны бросать мяч.</summary>
        public static bool PointerOverDebugUI;

        public static bool AutoAimFor(bool gamepad) => gamepad ? AutoAimGamepad : AutoAimMouse;

        const string Prefix = "Bouncer.Settings.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            AutoAimMouse = PlayerPrefs.GetInt(Prefix + nameof(AutoAimMouse), 0) == 1;
            AutoAimGamepad = PlayerPrefs.GetInt(Prefix + nameof(AutoAimGamepad), 1) == 1;
            ShowAimPreview = PlayerPrefs.GetInt(Prefix + nameof(ShowAimPreview), 1) == 1;
            GodMode = false;
            PointerOverDebugUI = false;
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(Prefix + nameof(AutoAimMouse), AutoAimMouse ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + nameof(AutoAimGamepad), AutoAimGamepad ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + nameof(ShowAimPreview), ShowAimPreview ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}

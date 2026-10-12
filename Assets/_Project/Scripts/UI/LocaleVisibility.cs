using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    /// <summary>
    /// Языки, перевод которых ещё не готов: их нет в списке настроек, а если такой язык уже выбран
    /// (сохранён раньше или пришёл из системы), игра переключается на его запасной.
    /// Перевод готов — убери код языка из <see cref="Hidden"/>.
    /// </summary>
    public static class LocaleVisibility
    {
        static readonly string[] Hidden = { "kk" };

        public static bool IsHidden(Locale locale) => locale != null && Array.IndexOf(Hidden, locale.Identifier.Code) >= 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LeaveHiddenLocale()
        {
            var locale = LocalizationSettings.SelectedLocale;
            if (!IsHidden(locale))
                return;
            var fallback = FallbackFor(locale);
            if (fallback != null)
                LocalizationSettings.SelectedLocale = fallback;
        }

        static Locale FallbackFor(Locale locale)
        {
            var fallback = locale.Metadata?.GetMetadata<FallbackLocale>()?.Locale;
            if (fallback != null && !IsHidden(fallback))
                return fallback;
            var project = LocalizationSettings.ProjectLocale;
            if (project != null && !IsHidden(project))
                return project;
            foreach (var other in LocalizationSettings.AvailableLocales.Locales)
                if (!IsHidden(other))
                    return other;
            return null;
        }
    }
}

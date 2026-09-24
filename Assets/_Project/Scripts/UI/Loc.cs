using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    /// <summary>
    /// Строки таблицы «UI» пакета Localization для текстов, которые собираются в коде (счёт, уровень,
    /// статистика). Неизменные надписи в префабах переводит компонент LocalizeStringEvent.
    /// Подстановки {0}, {1} — как в string.Format.
    /// </summary>
    public static class Loc
    {
        const string Table = "UI";

        public static string Get(string key) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);

        public static string Format(string key, params object[] args) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, (System.Collections.Generic.IList<object>)args);
    }
}

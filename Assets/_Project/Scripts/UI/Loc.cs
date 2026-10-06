using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    public static class Loc
    {
        const string Table = "UI";

        public static string Get(string key) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(Table, key);

        public static string Format(string key, params object[] args) =>
            LocalizationSettings.StringDatabase.GetLocalizedString(Table, key, (System.Collections.Generic.IList<object>)args);
    }
}

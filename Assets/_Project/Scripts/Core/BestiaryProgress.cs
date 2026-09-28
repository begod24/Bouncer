using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Что открыто в бестиарии. Обычные враги и элитки открыты всегда, босс — после победы над ним:
    /// отметка по имени префаба босса лежит в PlayerPrefs и переживает прогулки. Только что открытая страница
    /// помечена «новое!», пока игрок её не посмотрит (<see cref="MarkSeen"/>).
    /// </summary>
    public static class BestiaryProgress
    {
        const string Prefix = "bouncer.bestiary.";
        const string NewPrefix = "bouncer.bestiary.new.";

        public static bool IsOpen(string id) => !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(Prefix + id, 0) == 1;

        /// <summary>Страница открыта, но игрок её ещё не видел.</summary>
        public static bool IsNew(string id) => !string.IsNullOrEmpty(id) && PlayerPrefs.GetInt(NewPrefix + id, 0) == 1;

        /// <summary>Открыть страницу. true — открыта только что.</summary>
        public static bool Open(string id)
        {
            if (string.IsNullOrEmpty(id) || IsOpen(id))
                return false;
            PlayerPrefs.SetInt(Prefix + id, 1);
            PlayerPrefs.SetInt(NewPrefix + id, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void MarkSeen(string id)
        {
            if (!IsNew(id))
                return;
            PlayerPrefs.DeleteKey(NewPrefix + id);
            PlayerPrefs.Save();
        }
    }
}

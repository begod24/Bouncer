using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Обучение — «тренировка во дворе»: отдельный заход на первую арену без волн, где задания мелом ведут
    /// по шагам (бег, бросок, ловля, рывок, бой, элитка, карманы, ларёк). Здесь только флаги поверх сцен:
    /// заказано ли обучение на следующую загрузку, идёт ли оно сейчас, спрашивали ли о нём и пройдено ли оно.
    /// Шаги ведёт TutorialDirector (сборка Run), задания показывает TutorialPanel (UI).
    /// </summary>
    public static class Tutorial
    {
        const string OfferedKey = "bouncer.tutorial.offered";
        const string DoneKey = "bouncer.tutorial.done";

        static bool s_requested;

        /// <summary>Сцена сейчас играет обучение, а не прогулку.</summary>
        public static bool Active { get; private set; }

        /// <summary>После обучения («Гулять!») заставка сразу открывает выбор ребёнка.</summary>
        public static bool OpenKidsOnTitle { get; set; }

        /// <summary>Обучение хоть раз пройдено до конца.</summary>
        public static bool Completed => PlayerPrefs.GetInt(DoneKey, 0) == 1;

        /// <summary>Первое «Играть» спрашивает про обучение — если о нём ещё не спрашивали и его не проходили.</summary>
        public static bool ShouldAsk => PlayerPrefs.GetInt(OfferedKey, 0) == 0 && !Completed;

        /// <summary>Следующая загрузка первой арены — обучение.</summary>
        public static void Request() => s_requested = true;

        /// <summary>Сцена проснулась: заказано ли обучение. Заказ снимается — «Заново» закажет его снова.</summary>
        public static bool Begin()
        {
            Active = s_requested;
            s_requested = false;
            return Active;
        }

        /// <summary>Игрок ответил на «Пройти обучение?» — больше не спрашивать.</summary>
        public static void MarkOffered()
        {
            PlayerPrefs.SetInt(OfferedKey, 1);
            PlayerPrefs.Save();
        }

        public static void MarkCompleted()
        {
            PlayerPrefs.SetInt(OfferedKey, 1);
            PlayerPrefs.SetInt(DoneKey, 1);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_requested = false;
            Active = false;
            OpenKidsOnTitle = false;
        }
    }
}

using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Сетевая комната, как её видят Core и UI: уйти из неё и узнать, хозяин ли здесь.</summary>
    public interface IOnlineSession
    {
        /// <summary>Этот компьютер — хозяин комнаты (хост): у него идёт игра, остальные её смотрят.</summary>
        bool IsHost { get; }

        /// <summary>Уйти из комнаты и закрыть соединение. Хозяин уходит — комната закрывается у всех.</summary>
        void Leave();
    }

    /// <summary>
    /// Игра идёт по сети (кооп или PvP). Время тогда общее на всех: его нельзя останавливать или замедлять
    /// для одного игрока, поэтому <see cref="GameFeel"/> не трогает Time.timeScale — пауза и выбор карточки
    /// игру не останавливают, стоп-кадр и замедления выключены, а «Замри!» замораживает врагов.
    /// Включает сетевая сессия (<see cref="Session"/>), в соло всегда выключено.
    /// </summary>
    public static class Online
    {
        public static bool Active { get; set; }

        /// <summary>Сетевая комната, пока она есть. null — соло.</summary>
        public static IOnlineSession Session { get; set; }

        /// <summary>Здесь решается игра: в соло всегда, по сети — только у хозяина комнаты.</summary>
        public static bool IsHost => !Active || Session == null || Session.IsHost;

        /// <summary>
        /// Часы волн стоят: в начале сетевой прогулки ждут, пока все выберут стартовую карточку (в соло на это
        /// время встаёт вся игра). Ставит сетевая комната.
        /// </summary>
        public static bool WavesHeld { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Active = false;
            Session = null;
            WavesHeld = false;
        }
    }
}

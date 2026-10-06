using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Что игрок делает прямо сейчас — для анимации ребёнка. Свой игрок собирает это сам, чужой (по сети)
    /// получает от своего компьютера.
    /// </summary>
    public struct PlayerActionState
    {
        public bool Charging;
        /// <summary>Заряд броска 0–1.</summary>
        public float Charge01;
        public bool Catching;
        public bool Dashing;
        /// <summary>Рывок с «Подкатом»: ребёнок скользит, а не ныряет.</summary>
        public bool Sliding;
        public Vector3 DashDirection;
        /// <summary>Выбит: лежит.</summary>
        public bool Down;
        /// <summary>Светит наводящим фонариком (тёмные арены).</summary>
        public bool Flashlight;
    }

    /// <summary>Разовые движения ребёнка, которые чужой игрок показывает по сигналу своего компьютера.</summary>
    public enum PlayerCue : byte
    {
        Throw,
        Caught,
        Hurt,
    }
}

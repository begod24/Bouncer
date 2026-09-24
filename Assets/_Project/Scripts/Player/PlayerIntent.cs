using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Намерение игрока за кадр в мировых координатах. Логика игрока видит только его,
    /// а не устройства ввода, поэтому на этапе 3 намерения смогут приходить по сети.
    /// </summary>
    public struct PlayerIntent
    {
        /// <summary>Движение в плоскости XZ, длина не больше 1.</summary>
        public Vector3 Move;
        /// <summary>Направление прицела в плоскости XZ (нормализовано) или ноль.</summary>
        public Vector3 Aim;
        public bool AimFromPointer;
        public bool UsingGamepad;

        public bool ThrowHeld;
        public bool ThrowPressed;
        public bool ThrowReleased;
        public bool CatchPressed;
        public bool DashPressed;
        public bool PausePressed;
        /// <summary>Взаимодействие: открыть ларёк и т.п.</summary>
        public bool InteractPressed;
    }

    public interface IPlayerIntentSource
    {
        PlayerIntent ReadIntent();
    }
}

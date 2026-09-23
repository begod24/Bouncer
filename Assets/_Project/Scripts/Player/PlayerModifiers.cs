using System;
using Bouncer.Balls;

namespace Bouncer.Player
{
    /// <summary>
    /// Прибавки к игроку от карточек за забег. Базовые значения лежат в <see cref="PlayerStats"/> и не меняются
    /// (их крутит отладочное окно), итог = база × множитель или база + прибавка.
    /// </summary>
    public sealed class PlayerModifiers
    {
        public float MoveSpeed = 1f;
        public float DashDistance = 1f;
        public float DashCooldown = 1f;
        public float CatchWindow = 1f;
        public float CatchRadius = 1f;
        public float PickupRadius = 1f;
        public int ExtraBalls;
        /// <summary>Модификаторы мяча от карточек; складываются со свойствами типа мяча.</summary>
        public BallPerks Perks;

        public event Action Changed;

        public void NotifyChanged() => Changed?.Invoke();
    }
}

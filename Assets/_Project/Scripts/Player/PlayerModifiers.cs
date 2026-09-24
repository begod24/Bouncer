using System;
using Bouncer.Balls;

namespace Bouncer.Player
{
    /// <summary>
    /// Прибавки к игроку от карточек за забег. Базовые значения лежат в ассете <see cref="PlayerStats"/>
    /// и за забег не меняются, итог = база × множитель или база + прибавка.
    /// </summary>
    public sealed class PlayerModifiers
    {
        public float MoveSpeed = 1f;
        public float DashDistance = 1f;
        public float DashCooldown = 1f;
        public float CatchWindow = 1f;
        public float CatchRadius = 1f;
        public float PickupRadius = 1f;
        /// <summary>Сколько мячей помещается в руки сверх обычного. Бывает и меньше нуля (хулиганство).</summary>
        public int ExtraBalls;
        /// <summary>Прибавка к урону каждого броска (хулиганство).</summary>
        public int BonusDamage;
        /// <summary>Подкат: урон врагам, которых задел рывок. 0 — рывок никого не бьёт.</summary>
        public int TackleDamage;
        /// <summary>«Замри!»: на сколько секунд удачная ловля замедляет всё вокруг. 0 — не замедляет.</summary>
        public float CatchFreeze;
        /// <summary>Модификаторы мяча от карточек; складываются со свойствами типа мяча.</summary>
        public BallPerks Perks;
        /// <summary>Свойства только следующего броска после удачной ловли (горячая картошка).</summary>
        public BallPerks CatchPerks;
        public bool HasCatchPerks;

        public event Action Changed;

        public void NotifyChanged() => Changed?.Invoke();
    }
}

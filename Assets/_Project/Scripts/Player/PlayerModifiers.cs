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
        /// <summary>«Домино»: с каким уроном выбитый враг сбивает соседей. 0 — не сбивает.</summary>
        public int DominoDamage;
        /// <summary>«Копилка»: сколько монеток прибавляется у ларька за каждые 10 в кармане.</summary>
        public int CoinInterest;
        /// <summary>«Крышка от кастрюли»: раз в столько секунд блокирует удар. 0 — крышки нет.</summary>
        public float LidCooldown;
        /// <summary>«Зеркальце»: манекены замирают и в секторе за спиной — половина угла, градусы. 0 — нет.</summary>
        public float MirrorAngle;
        /// <summary>«Фонарик»: радиус света вокруг игрока, м — в нём тень твёрдая. 0 — нет.</summary>
        public float LanternRadius;
        /// <summary>«Свисток»: идеальная ловля замораживает врагов вокруг на столько секунд. 0 — нет.</summary>
        public float WhistleFreeze;
        /// <summary>«Второе дыхание»: раз за прогулку удар, который выбил бы, оставляет с одним сердцем.</summary>
        public bool SecondWind;
        /// <summary>«Бабушкины пирожки»: столько сердец прибавляется в начале каждой следующей арены.</summary>
        public int ArenaHeal;
        /// <summary>«Резиновые сапоги»: песок и лужи не замедляют.</summary>
        public bool IgnoreGround;
        /// <summary>«Кувырок»: рывок сквозь вражеский мяч ловит его.</summary>
        public bool DashCatch;
        /// <summary>«Шпаргалка»: столько переборов витрины в каждом ларьке бесплатно.</summary>
        public int FreeRerolls;
        /// <summary>«Счастливый фантик»: на столько карточек больше на выбор в портфеле и за босса.</summary>
        public int ExtraChoices;
        /// <summary>Модификаторы мяча от карточек; складываются со свойствами типа мяча.</summary>
        public BallPerks Perks;
        /// <summary>Свойства только следующего броска после удачной ловли (горячая картошка).</summary>
        public BallPerks CatchPerks;
        public bool HasCatchPerks;

        public event Action Changed;

        public void NotifyChanged() => Changed?.Invoke();
    }
}

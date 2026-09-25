using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>Пассивка: прибавки к характеристикам игрока. Можно брать несколько раз — прибавки складываются.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Stat Card", fileName = "Card_Stat_")]
    public sealed class StatCard : UpgradeCard
    {
        [Header("Прибавки за одну карточку (доли: 0.15 = +15%)")]
        public float moveSpeed;
        public float dashDistance;
        [Tooltip("Отрицательное число — рывок перезаряжается быстрее")]
        public float dashCooldown;
        public float catchWindow;
        public float catchRadius;
        public float pickupRadius;
        [Tooltip("Сколько мячей можно держать в руках сверх обычного (и сразу получить). " +
                 "Отрицательное — меньше: лишний мяч из рук падает под ноги")]
        public int extraBalls;
        [Tooltip("Сразу вылечить столько жизней")]
        [Min(0)] public int heal;

        [Header("Броски, рывок и ловля")]
        [Tooltip("Прибавка к урону каждого броска")]
        [Min(0)] public int bonusDamage;
        [Tooltip("Подкат: рывок бьёт задетых врагов с таким уроном и сбивает с ног")]
        [Min(0)] public int tackleDamage;
        [Tooltip("«Замри!»: на сколько секунд удачная ловля замедляет всё вокруг")]
        [Min(0f)] public float catchFreeze;

        [Header("Особые")]
        [Tooltip("«Домино»: выбитый враг сбивает соседей с таким уроном")]
        [Min(0)] public int dominoDamage;
        [Tooltip("«Копилка»: у ларька +столько монеток за каждые 10 в кармане")]
        [Min(0)] public int coinInterest;
        [Tooltip("«Крышка от кастрюли»: раз в столько секунд блокирует один удар. 0 — нет")]
        [Min(0f)] public float lidCooldown;
        [Tooltip("«Зеркальце»: манекены замирают и в секторе за спиной — половина угла, градусы")]
        [Min(0f)] public float mirrorAngle;
        [Tooltip("«Фонарик»: радиус света вокруг игрока, м — в нём тень твёрдая")]
        [Min(0f)] public float lanternRadius;
        [Tooltip("«Свисток»: идеальная ловля замораживает врагов вокруг на столько секунд")]
        [Min(0f)] public float whistleFreeze;
        [Tooltip("«Второе дыхание»: раз за прогулку удар, который выбил бы, оставляет с одним сердцем")]
        public bool secondWind;
        [Tooltip("«Бабушкины пирожки»: столько сердец в начале каждой следующей арены")]
        [Min(0)] public int arenaHeal;
        [Tooltip("«Резиновые сапоги»: песок и лужи не замедляют")]
        public bool ignoreGround;
        [Tooltip("«Кувырок»: рывок сквозь вражеский мяч ловит его")]
        public bool dashCatch;
        [Tooltip("«Шпаргалка»: столько переборов витрины в каждом ларьке бесплатно")]
        [Min(0)] public int freeRerolls;
        [Tooltip("«Счастливый фантик»: на столько карточек больше на выбор в портфеле и за босса")]
        [Min(0)] public int extraChoices;

        // Карточка с минусом к мячам не должна оставить игрока совсем без мячей.
        public override bool CanOffer(PlayerController player, int stacks) =>
            base.CanOffer(player, stacks) && player.Balls.MaxBalls + extraBalls >= 1;

        public override void Apply(PlayerController player)
        {
            var mods = player.Modifiers;
            mods.MoveSpeed += moveSpeed;
            mods.DashDistance += dashDistance;
            mods.DashCooldown = Mathf.Max(0.2f, mods.DashCooldown + dashCooldown);
            mods.CatchWindow += catchWindow;
            mods.CatchRadius += catchRadius;
            mods.PickupRadius += pickupRadius;
            mods.ExtraBalls += extraBalls;
            mods.BonusDamage += bonusDamage;
            mods.TackleDamage += tackleDamage;
            mods.CatchFreeze += catchFreeze;
            mods.DominoDamage += dominoDamage;
            mods.CoinInterest += coinInterest;
            if (lidCooldown > 0f)
                mods.LidCooldown = mods.LidCooldown > 0f ? Mathf.Min(mods.LidCooldown, lidCooldown) : lidCooldown;
            mods.MirrorAngle = Mathf.Max(mods.MirrorAngle, mirrorAngle);
            mods.LanternRadius = Mathf.Max(mods.LanternRadius, lanternRadius);
            mods.WhistleFreeze = Mathf.Max(mods.WhistleFreeze, whistleFreeze);
            mods.SecondWind |= secondWind;
            mods.ArenaHeal += arenaHeal;
            mods.IgnoreGround |= ignoreGround;
            mods.DashCatch |= dashCatch;
            mods.FreeRerolls += freeRerolls;
            mods.ExtraChoices += extraChoices;
            if (extraBalls > 0)
                player.Balls.GiveBall(extraBalls);
            else if (extraBalls < 0)
            {
                // На новой арене мячей на полу ещё нет: лишние просто не выдаются.
                if (Replaying)
                    player.Balls.ClampToMax();
                else
                    player.Balls.DropExcess();
            }
            if (heal > 0 && !Replaying)
                player.Health.Heal(heal);
            mods.NotifyChanged();
        }
    }
}

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

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
        [Tooltip("Сколько мячей можно держать в руках сверх обычного (и сразу получить)")]
        [Min(0)] public int extraBalls;
        [Tooltip("Сразу вылечить столько жизней")]
        [Min(0)] public int heal;

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
            if (extraBalls > 0)
                player.Balls.GiveBall(extraBalls);
            if (heal > 0)
                player.Health.Heal(heal);
            mods.NotifyChanged();
        }
    }
}

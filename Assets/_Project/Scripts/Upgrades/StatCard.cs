using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
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
        [Tooltip("Подкат: больше 0 — рывок сбивает задетых врагов с ног и оглушает (урона нет)")]
        [Min(0)] public int tackleDamage;

        [Header("Особые")]
        [Tooltip("«Домино»: выбитый враг сбивает соседей с таким уроном")]
        [Min(0)] public int dominoDamage;
        [Tooltip("«Копилка»: у ларька +столько монеток за каждые 10 в кармане")]
        [Min(0)] public int coinInterest;
        [Tooltip("«Крышка от кастрюли»: раз в столько секунд блокирует один удар. 0 — нет")]
        [Min(0f)] public float lidCooldown;
        [Tooltip("«Фонарик»: радиус света вокруг игрока, м — в нём тень твёрдая")]
        [Min(0f)] public float lanternRadius;
        [Tooltip("«Замри!»: идеальная ловля замораживает врагов вокруг на столько секунд; " +
                 "каждая следующая карточка — дальше и дольше (PlayerStats.whistleStack*)")]
        [Min(0f)] public float whistleFreeze;
        [Tooltip("«Второе дыхание»: раз за прогулку выбитый игрок сам встаёт с одним сердцем")]
        public bool secondWind;
        [Tooltip("«Бабушкины пирожки»: +столько сердец к максимуму (всего не больше PlayerStats.maxLivesCap)")]
        [Min(0)] public int maxLives;
        [Tooltip("«Чехарда»: рывок во врага — прыжок через него, приземление сбивает всех вокруг")]
        public bool leapfrog;
        [Tooltip("«Счастливый фантик»: на столько карточек больше на выбор в портфеле и за босса")]
        [Min(0)] public int extraChoices;
        [Tooltip("«Считалочка»: каждый такой по счёту бросок сам заряжен. 0 — нет")]
        [Min(0)] public int countEvery;
        [Tooltip("«Считалочка»: и бьёт на столько сильнее")]
        [Min(0)] public int countBonus;
        [Tooltip("«Тамагочи»: сытый питомец спасает от выбивания")]
        public bool tamagotchi;
        [Tooltip("«Своих не бросаем»: подняв товарища, замораживаешь врагов вокруг")]
        public bool circleGuard;

        public override bool CanOffer(PlayerController player, int stacks) =>
            base.CanOffer(player, stacks) && player.Balls.MaxBalls + extraBalls >= 1;

        public override void Apply(PlayerController player)
        {
            // Число мячей в руках подстраивается в EndBatch: +мяч сразу в руки, −мяч — тает лишний свой.
            player.Balls.BeginBatch();
            try
            {
                ApplyStats(player);
            }
            finally
            {
                player.Balls.EndBatch();
            }
            // при пересборке максимум сверяется один раз в конце, иначе сердце сверх 3 пропало бы на полпути
            if (maxLives != 0 && !Rebuilding)
                player.RefreshMaxLives();
            if (heal > 0 && !Replaying)
                player.Health.Heal(heal);
            player.Modifiers.NotifyChanged();
        }

        void ApplyStats(PlayerController player)
        {
            var mods = player.Modifiers;
            mods.MoveSpeed += moveSpeed;
            mods.DashDistance += dashDistance;
            mods.DashCooldown = Mathf.Max(0.2f, mods.DashCooldown + dashCooldown);
            mods.CatchWindow += catchWindow;
            mods.CatchRadius += catchRadius;
            mods.PickupRadius += pickupRadius;
            if (extraBalls > 0)
                mods.ExtraBalls += extraBalls;
            else
                mods.FewerBalls -= extraBalls;
            mods.BonusDamage += bonusDamage;
            mods.TackleDamage += tackleDamage;
            mods.DominoDamage += dominoDamage;
            mods.CoinInterest += coinInterest;
            if (lidCooldown > 0f)
                mods.LidCooldown = mods.LidCooldown > 0f ? Mathf.Min(mods.LidCooldown, lidCooldown) : lidCooldown;
            mods.LanternRadius = Mathf.Max(mods.LanternRadius, lanternRadius);
            mods.WhistleFreeze = Mathf.Max(mods.WhistleFreeze, whistleFreeze);
            if (whistleFreeze > 0f)
                mods.WhistleStacks++;
            mods.ExtraLives += maxLives;
            mods.SecondWind |= secondWind;
            mods.Leapfrog |= leapfrog;
            mods.ExtraChoices += extraChoices;
            if (countEvery > 0)
                mods.CountEvery = mods.CountEvery > 0 ? Mathf.Min(mods.CountEvery, countEvery) : countEvery;
            mods.CountBonus += countBonus;
            mods.Tamagotchi |= tamagotchi;
            mods.CircleGuard |= circleGuard;
        }
    }
}

using System;
using Bouncer.Balls;

namespace Bouncer.Player
{
    public sealed class PlayerModifiers
    {
        public float MoveSpeed = 1f;
        public float DashDistance = 1f;
        public float DashCooldown = 1f;
        public float CatchWindow = 1f;
        public float CatchRadius = 1f;
        public float PickupRadius = 1f;
        public int ExtraBalls;
        public int BonusDamage;
        public int TackleDamage;
        public float CatchFreeze;
        public int DominoDamage;
        public int CoinInterest;
        public float LidCooldown;
        public float MirrorAngle;
        public float LanternRadius;
        public float WhistleFreeze;
        public bool SecondWind;
        public int ArenaHeal;
        public bool IgnoreGround;
        public bool DashCatch;
        public int FreeRerolls;
        public int ExtraChoices;
        public BallPerks Perks;
        public BallPerks CatchPerks;
        public bool HasCatchPerks;
        public AbilityDefinition Ability;
        public int CountEvery;
        public int CountBonus;
        public bool PassCharge;
        public float GumBubbleCooldown;
        public bool Tamagotchi;
        public bool CircleGuard;

        public event Action Changed;

        public void NotifyChanged() => Changed?.Invoke();

        public void Reset()
        {
            MoveSpeed = 1f;
            DashDistance = 1f;
            DashCooldown = 1f;
            CatchWindow = 1f;
            CatchRadius = 1f;
            PickupRadius = 1f;
            ExtraBalls = 0;
            BonusDamage = 0;
            TackleDamage = 0;
            CatchFreeze = 0f;
            DominoDamage = 0;
            CoinInterest = 0;
            LidCooldown = 0f;
            MirrorAngle = 0f;
            LanternRadius = 0f;
            WhistleFreeze = 0f;
            SecondWind = false;
            ArenaHeal = 0;
            IgnoreGround = false;
            DashCatch = false;
            FreeRerolls = 0;
            ExtraChoices = 0;
            Perks = default;
            CatchPerks = default;
            HasCatchPerks = false;
            Ability = null;
            CountEvery = 0;
            CountBonus = 0;
            PassCharge = false;
            GumBubbleCooldown = 0f;
            Tamagotchi = false;
            CircleGuard = false;
        }
    }
}

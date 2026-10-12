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
        public int FewerBalls;
        public int BonusDamage;
        public int TackleDamage;
        public int DominoDamage;
        public int CoinInterest;
        public float LidCooldown;
        public float LanternRadius;
        public float WhistleFreeze;
        public int WhistleStacks;
        public int ExtraLives;
        public bool SecondWind;
        public bool Leapfrog;
        public int ExtraChoices;
        public BallPerks Perks;
        public BallPerks CatchPerks;
        public bool HasCatchPerks;
        public AbilityDefinition Ability;
        public int CountEvery;
        public int CountBonus;
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
            FewerBalls = 0;
            BonusDamage = 0;
            TackleDamage = 0;
            DominoDamage = 0;
            CoinInterest = 0;
            LidCooldown = 0f;
            LanternRadius = 0f;
            WhistleFreeze = 0f;
            WhistleStacks = 0;
            ExtraLives = 0;
            SecondWind = false;
            Leapfrog = false;
            ExtraChoices = 0;
            Perks = default;
            CatchPerks = default;
            HasCatchPerks = false;
            Ability = null;
            CountEvery = 0;
            CountBonus = 0;
            Tamagotchi = false;
            CircleGuard = false;
        }
    }
}

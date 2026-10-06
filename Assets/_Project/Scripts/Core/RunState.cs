using System;
using UnityEngine;

namespace Bouncer.Core
{
    public static class RunState
    {
        public const int MaxPlayers = 4;

        static readonly float[] s_lootShare = { 1f, 0.7f, 0.55f, 0.45f };

        sealed class PlayerRun
        {
            public int Coins;
            public float CoinCarry;
            public int Lives;
            public bool SecondWindUsed;
            public bool NeedsStartCard;
        }

        static readonly PlayerRun[] s_players = { new(), new(), new(), new() };

        public static bool Active { get; private set; }
        public static int RunId { get; private set; }
        public static int PlayerCount { get; private set; } = 1;
        public static int ArenaIndex { get; private set; }
        public static int ArenaVariant { get; private set; }
        public static int NextVariant { get; set; }
        public static bool ContinuesRun { get; private set; }
        public static string FirstScene { get; set; }

        public static float PastTime { get; private set; }
        public static int PastKills { get; private set; }
        public static int CoinsEarned { get; private set; }
        public static int Danger { get; private set; } = 1;
        public static float RunClock { get; set; }

        public static event Action<int, int> CoinsChanged;

        public static float LootShare => s_lootShare[Mathf.Clamp(PlayerCount, 1, MaxPlayers) - 1];

        static PlayerRun Of(int slot) => s_players[Mathf.Clamp(slot, 0, MaxPlayers - 1)];

        public static int CoinsOf(int slot) => Of(slot).Coins;

        public static int LivesOf(int slot) => Of(slot).Lives;

        public static void SetLives(int slot, int lives) => Of(slot).Lives = Mathf.Max(0, lives);

        public static bool SecondWindUsed(int slot) => Of(slot).SecondWindUsed;

        public static void UseSecondWind(int slot) => Of(slot).SecondWindUsed = true;

        public static bool NeedsStartCard(int slot) => Active && Of(slot).NeedsStartCard;

        public static void StartCardChosen(int slot) => Of(slot).NeedsStartCard = false;

        public static void BeginNew(int players = 1)
        {
            Active = true;
            RunId++;
            PlayerCount = Mathf.Clamp(players, 1, MaxPlayers);
            ArenaIndex = 0;
            ArenaVariant = 0;
            NextVariant = 0;
            ResetPlayers(needStartCard: true);
            ContinuesRun = false;
            PastTime = 0f;
            PastKills = 0;
            CoinsEarned = 0;
            Danger = Bouncer.Core.Danger.Selected;
            RunClock = 0f;
            for (int slot = 0; slot < MaxPlayers; slot++)
                CoinsChanged?.Invoke(slot, 0);
        }

        public static void BeginOnline(int players, int danger)
        {
            BeginNew(players);
            Danger = Mathf.Clamp(danger, 1, Bouncer.Core.Danger.Max);
            ContinuesRun = true;
        }

        public static void BeginTutorial()
        {
            BeginNew();
            ResetPlayers(needStartCard: false);
            Danger = 1;
        }

        public static void Clear()
        {
            Active = false;
            PlayerCount = 1;
            ArenaIndex = 0;
            ArenaVariant = 0;
            NextVariant = 0;
            ResetPlayers(needStartCard: false);
            ContinuesRun = false;
            PastTime = 0f;
            PastKills = 0;
            CoinsEarned = 0;
            RunClock = 0f;
        }

        static void ResetPlayers(bool needStartCard)
        {
            foreach (var player in s_players)
            {
                player.Coins = 0;
                player.CoinCarry = 0f;
                player.Lives = 0;
                player.SecondWindUsed = false;
                player.NeedsStartCard = needStartCard;
            }
        }

        public static void BeginAt(int arenaIndex, int variant = 0)
        {
            BeginNew();
            ArenaIndex = Mathf.Max(0, arenaIndex);
            ArenaVariant = Mathf.Max(0, variant);
        }

        public static void AddCoins(int slot, int amount)
        {
            if (amount <= 0)
                return;
            Of(slot).Coins += amount;
            CoinsEarned += amount;
            CoinsChanged?.Invoke(slot, amount);
        }

        public static void AddLoot(int amount)
        {
            if (amount <= 0)
                return;
            CoinsEarned += amount;
            float share = amount * LootShare;
            for (int slot = 0; slot < PlayerCount; slot++)
            {
                var player = s_players[slot];
                float total = player.CoinCarry + share;
                int whole = Mathf.FloorToInt(total + 1e-4f);
                player.CoinCarry = Mathf.Max(0f, total - whole);
                if (whole <= 0)
                    continue;
                player.Coins += whole;
                CoinsChanged?.Invoke(slot, whole);
            }
        }

        public static bool TrySpend(int slot, int amount)
        {
            var player = Of(slot);
            if (amount < 0 || amount > player.Coins)
                return false;
            player.Coins -= amount;
            CoinsChanged?.Invoke(slot, -amount);
            return true;
        }

        public static void AdvanceArena(float arenaTime, int arenaKills)
        {
            PastTime += arenaTime;
            PastKills += arenaKills;
            ArenaIndex++;
            ArenaVariant = Mathf.Max(0, NextVariant);
            NextVariant = 0;
            ContinuesRun = true;
        }

        public static void ConsumeContinuation() => ContinuesRun = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Clear();
            RunId = 0;
            FirstScene = null;
            CoinsChanged = null;
        }
    }
}

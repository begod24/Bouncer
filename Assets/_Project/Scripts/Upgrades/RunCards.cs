using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Upgrades
{
    public static class RunCards
    {
        sealed class PlayerRunCards
        {
            public readonly List<UpgradeCard> Taken = new();
            public readonly List<UpgradeCard> Locked = new();
            public readonly List<OfferKind> Carried = new();
            public int Combos;
            public int Backpack;

            public void Clear()
            {
                Taken.Clear();
                Locked.Clear();
                Carried.Clear();
                Combos = 0;
                Backpack = 0;
            }
        }

        static readonly PlayerRunCards[] s_players = { new(), new(), new(), new() };
        static int s_runId = -1;
        static int s_golds;
        static int s_sharedGolds;

        public static int GoldsTaken
        {
            get
            {
                Sync();
                return Mathf.Max(s_golds, s_sharedGolds);
            }
        }

        public static int SharedGolds
        {
            get
            {
                Sync();
                return s_sharedGolds;
            }
            set
            {
                Sync();
                s_sharedGolds = Mathf.Max(0, value);
            }
        }

        public static event System.Action GoldRecorded;

        public static int CombosTaken(int slot) => Of(slot).Combos;

        public static IReadOnlyList<UpgradeCard> Taken(int slot) => Of(slot).Taken;

        public static List<UpgradeCard> Locked(int slot) => Of(slot).Locked;

        public static List<OfferKind> Carried(int slot) => Of(slot).Carried;

        public static int BackpackOf(int slot) => Of(slot).Backpack;

        public static void SetBackpack(int slot, int count) => Of(slot).Backpack = Mathf.Max(0, count);

        public static void Record(int slot, UpgradeCard card)
        {
            var player = Of(slot);
            if (!card)
                return;
            player.Taken.Add(card);
            if (card.IsCombo)
            {
                player.Combos++;
            }
            else if (card.rarity == CardRarity.Gold)
            {
                s_golds++;
                GoldRecorded?.Invoke();
            }
        }

        public static void Restore(int slot, IEnumerable<UpgradeCard> taken, IEnumerable<UpgradeCard> locked,
            IEnumerable<OfferKind> carried, int backpack)
        {
            var player = Of(slot);
            player.Clear();
            foreach (var card in taken)
            {
                if (!card)
                    continue;
                player.Taken.Add(card);
                if (card.IsCombo)
                    player.Combos++;
                else if (card.rarity == CardRarity.Gold)
                    s_golds++;
            }
            foreach (var card in locked)
                if (card)
                    player.Locked.Add(card);
            player.Carried.AddRange(carried);
            player.Backpack = Mathf.Max(0, backpack);
        }

        public static void Remove(int slot, UpgradeCard card) => Of(slot).Taken.RemoveAll(taken => taken == card);

        static PlayerRunCards Of(int slot)
        {
            Sync();
            return s_players[Mathf.Clamp(slot, 0, s_players.Length - 1)];
        }

        static void Sync()
        {
            if (s_runId == RunState.RunId)
                return;
            s_runId = RunState.RunId;
            foreach (var player in s_players)
                player.Clear();
            s_golds = 0;
            s_sharedGolds = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (var player in s_players)
                player.Clear();
            s_runId = -1;
            s_golds = 0;
            s_sharedGolds = 0;
            GoldRecorded = null;
        }
    }
}

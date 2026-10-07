using System;
using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Run;
using Bouncer.Upgrades;
using Unity.Netcode;

namespace Bouncer.Net
{
    public struct NetRunSave : INetworkSerializable, IEquatable<NetRunSave>
    {
        const ushort Unknown = ushort.MaxValue;

        public ushort[] Cards;
        public ushort[] Locked;
        public byte[] Carried;
        public byte Backpack;
        public int Coins;
        public float CoinCarry;
        public bool SecondWindUsed;
        public byte ClearedArena;

        public static NetRunSave Empty => new()
        {
            Cards = Array.Empty<ushort>(),
            Locked = Array.Empty<ushort>(),
            Carried = Array.Empty<byte>(),
        };

        public static NetRunSave Capture(PlayerCards cards, List<OfferKind> offers)
        {
            int slot = cards.Slot;
            var deck = cards.Deck;
            cards.GetOpenOffers(offers);
            var carried = new byte[offers.Count];
            for (int i = 0; i < carried.Length; i++)
                carried[i] = (byte)offers[i];
            var director = ArenaDirector.Instance;
            return new NetRunSave
            {
                Cards = Indices(deck, RunCards.Taken(slot)),
                Locked = Indices(deck, RunCards.Locked(slot)),
                Carried = carried,
                Backpack = (byte)Math.Clamp(RunCards.BackpackOf(slot), 0, 255),
                Coins = RunState.CoinsOf(slot),
                CoinCarry = RunState.CoinCarryOf(slot),
                SecondWindUsed = RunState.SecondWindUsed(slot),
                ClearedArena = (byte)(director != null && director.IsComplete ? RunState.ArenaIndex + 1 : 0),
            };
        }

        public static void ToCards(CardDeck deck, ushort[] indices, List<UpgradeCard> result)
        {
            result.Clear();
            if (indices == null)
                return;
            foreach (ushort index in indices)
            {
                var card = CardAt(deck, index);
                if (card)
                    result.Add(card);
            }
        }

        static ushort[] Indices(CardDeck deck, IReadOnlyList<UpgradeCard> cards)
        {
            var result = new ushort[cards.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = IndexOf(deck, cards[i]);
            return result;
        }

        static ushort IndexOf(CardDeck deck, UpgradeCard card)
        {
            if (deck == null || card == null)
                return Unknown;
            int index = deck.deck.IndexOf(card);
            if (index >= 0)
                return (ushort)index;
            return card == deck.filler ? (ushort)deck.deck.Count : Unknown;
        }

        static UpgradeCard CardAt(CardDeck deck, ushort index)
        {
            if (deck == null || index == Unknown)
                return null;
            if (index < deck.deck.Count)
                return deck.deck[index];
            return index == deck.deck.Count ? deck.filler : null;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            Cards ??= Array.Empty<ushort>();
            Locked ??= Array.Empty<ushort>();
            Carried ??= Array.Empty<byte>();
            serializer.SerializeValue(ref Cards);
            serializer.SerializeValue(ref Locked);
            serializer.SerializeValue(ref Carried);
            serializer.SerializeValue(ref Backpack);
            serializer.SerializeValue(ref Coins);
            serializer.SerializeValue(ref CoinCarry);
            serializer.SerializeValue(ref SecondWindUsed);
            serializer.SerializeValue(ref ClearedArena);
        }

        public bool Equals(NetRunSave other) =>
            Same(Cards, other.Cards) && Same(Locked, other.Locked) && Same(Carried, other.Carried) && Backpack == other.Backpack
            && Coins == other.Coins && CoinCarry.Equals(other.CoinCarry) && SecondWindUsed == other.SecondWindUsed
            && ClearedArena == other.ClearedArena;

        public override bool Equals(object obj) => obj is NetRunSave other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Cards?.Length ?? 0, Backpack, Coins, ClearedArena);

        static bool Same<TItem>(TItem[] a, TItem[] b) where TItem : IEquatable<TItem>
        {
            int lengthA = a?.Length ?? 0;
            int lengthB = b?.Length ?? 0;
            if (lengthA != lengthB)
                return false;
            for (int i = 0; i < lengthA; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }
    }

    public struct NetRejoin : INetworkSerializable
    {
        public byte Slot;
        public sbyte Kid;
        public byte Players;
        public byte Danger;
        public byte ArenaIndex;
        public byte ArenaVariant;
        public float PastTime;
        public int PastKills;
        public int CoinsEarned;
        public float RunClock;
        public float ArenaTime;
        public byte Lives;
        public byte Hands;
        public bool Down;
        public bool Complete;
        public bool ClearBoss;
        public byte BossCards;
        public byte TeamGolds;
        public NetRunSave Save;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Kid);
            serializer.SerializeValue(ref Players);
            serializer.SerializeValue(ref Danger);
            serializer.SerializeValue(ref ArenaIndex);
            serializer.SerializeValue(ref ArenaVariant);
            serializer.SerializeValue(ref PastTime);
            serializer.SerializeValue(ref PastKills);
            serializer.SerializeValue(ref CoinsEarned);
            serializer.SerializeValue(ref RunClock);
            serializer.SerializeValue(ref ArenaTime);
            serializer.SerializeValue(ref Lives);
            serializer.SerializeValue(ref Hands);
            serializer.SerializeValue(ref Down);
            serializer.SerializeValue(ref Complete);
            serializer.SerializeValue(ref ClearBoss);
            serializer.SerializeValue(ref BossCards);
            serializer.SerializeValue(ref TeamGolds);
            Save.NetworkSerialize(serializer);
        }
    }
}

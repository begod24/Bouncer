using System;
using Unity.Collections;
using Unity.Netcode;

namespace Bouncer.Net
{
    /// <summary>Игрок в комнате: кто он, под каким номером, кем гуляет и готов ли.</summary>
    public struct RoomMember : INetworkSerializable, IEquatable<RoomMember>
    {
        public ulong ClientId;
        /// <summary>Номер игрока в прогулке (0–3): точка старта, цвет, монетки и сердца в RunState.</summary>
        public byte Slot;
        /// <summary>Номер ребёнка в KidRoster; -1 — ещё не выбран.</summary>
        public sbyte Kid;
        public FixedString64Bytes Name;
        public bool Ready;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Slot);
            serializer.SerializeValue(ref Kid);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Ready);
        }

        public bool Equals(RoomMember other) =>
            ClientId == other.ClientId && Slot == other.Slot && Kid == other.Kid && Name.Equals(other.Name) && Ready == other.Ready;

        public override bool Equals(object obj) => obj is RoomMember other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(ClientId, Slot, Kid, Ready);
    }
}

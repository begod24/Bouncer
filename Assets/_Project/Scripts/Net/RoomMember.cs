using System;
using Unity.Collections;
using Unity.Netcode;

namespace Bouncer.Net
{
    public struct RoomMember : INetworkSerializable, IEquatable<RoomMember>
    {
        public ulong ClientId;
        public byte Slot;
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

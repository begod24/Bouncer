using System;
using Bouncer.Player;
using Unity.Netcode;
using UnityEngine;

namespace Bouncer.Net
{
    public struct NetPose : INetworkSerializable, IEquatable<NetPose>
    {
        const byte ChargingFlag = 1 << 0;
        const byte CatchingFlag = 1 << 1;
        const byte DashingFlag = 1 << 2;
        const byte SlidingFlag = 1 << 3;
        const byte DownFlag = 1 << 4;

        public const int Size = 3;

        byte _flags;
        byte _charge;
        byte _dashAngle;

        public void Write(byte[] bytes)
        {
            bytes[0] = _flags;
            bytes[1] = _charge;
            bytes[2] = _dashAngle;
        }

        public static NetPose Read(byte[] bytes) => new() { _flags = bytes[0], _charge = bytes[1], _dashAngle = bytes[2] };

        public static NetPose From(in PlayerActionState action)
        {
            byte flags = 0;
            if (action.Charging)
                flags |= ChargingFlag;
            if (action.Catching)
                flags |= CatchingFlag;
            if (action.Dashing)
                flags |= DashingFlag;
            if (action.Sliding)
                flags |= SlidingFlag;
            if (action.Down)
                flags |= DownFlag;
            float angle = action.Dashing ? Mathf.Atan2(action.DashDirection.x, action.DashDirection.z) * Mathf.Rad2Deg : 0f;
            return new NetPose
            {
                _flags = flags,
                _charge = action.Charging ? (byte)Mathf.RoundToInt(Mathf.Clamp01(action.Charge01) * 255f) : (byte)0,
                _dashAngle = (byte)Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 360f * 255f),
            };
        }

        public PlayerActionState ToAction()
        {
            float angle = _dashAngle / 255f * 360f * Mathf.Deg2Rad;
            bool dashing = (_flags & DashingFlag) != 0;
            return new PlayerActionState
            {
                Charging = (_flags & ChargingFlag) != 0,
                Charge01 = _charge / 255f,
                Catching = (_flags & CatchingFlag) != 0,
                Dashing = dashing,
                Sliding = (_flags & SlidingFlag) != 0,
                DashDirection = dashing ? new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) : Vector3.zero,
                Down = (_flags & DownFlag) != 0,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _flags);
            serializer.SerializeValue(ref _charge);
            serializer.SerializeValue(ref _dashAngle);
        }

        public bool Equals(NetPose other) => _flags == other._flags && _charge == other._charge && _dashAngle == other._dashAngle;

        public bool SameFlags(NetPose other) => _flags == other._flags;

        public override bool Equals(object obj) => obj is NetPose other && Equals(other);

        public override int GetHashCode() => _flags | (_charge << 8) | (_dashAngle << 16);
    }
}

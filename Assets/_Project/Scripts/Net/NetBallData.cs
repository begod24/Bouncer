using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Unity.Netcode;
using UnityEngine;

namespace Bouncer.Net
{
    public struct NetBallState : INetworkSerializable
    {
        public const byte KindUpsert = 0;
        public const byte KindDespawn = 1;

        const byte PhantomBit = 1 << 0;
        const byte YoyoBit = 1 << 1;
        const byte HotBit = 1 << 2;

        public ushort Id;
        public byte Kind;
        public byte Prefab;
        public BallState State;
        public Team Team;
        public HitFlags Flags;
        public byte Bits;
        public sbyte OwnerSlot;
        public sbyte ThrowerSlot;
        public byte Damage;
        public float Knockback;
        public double Time;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Gravity;
        public sbyte PredictSlot;
        public ushort PredictSeq;

        public bool Phantom
        {
            readonly get => (Bits & PhantomBit) != 0;
            set => Bits = value ? (byte)(Bits | PhantomBit) : (byte)(Bits & ~PhantomBit);
        }

        public bool YoyoString
        {
            readonly get => (Bits & YoyoBit) != 0;
            set => Bits = value ? (byte)(Bits | YoyoBit) : (byte)(Bits & ~YoyoBit);
        }

        public bool Hot
        {
            readonly get => (Bits & HotBit) != 0;
            set => Bits = value ? (byte)(Bits | HotBit) : (byte)(Bits & ~HotBit);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref PredictSlot);
            serializer.SerializeValue(ref PredictSeq);
            if (Kind == KindDespawn)
                return;
            serializer.SerializeValue(ref Prefab);
            serializer.SerializeValue(ref State);
            serializer.SerializeValue(ref Team);
            serializer.SerializeValue(ref Flags);
            serializer.SerializeValue(ref Bits);
            serializer.SerializeValue(ref OwnerSlot);
            serializer.SerializeValue(ref ThrowerSlot);
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref Knockback);
            serializer.SerializeValue(ref Time);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref Gravity);
        }
    }

    public struct NetBallBatch : INetworkSerializable
    {
        public List<NetBallState> Entries;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            int count = Entries?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                Entries = new List<NetBallState>(count);
            for (int i = 0; i < count; i++)
            {
                var entry = serializer.IsReader ? default : Entries[i];
                entry.NetworkSerialize(serializer);
                if (serializer.IsReader)
                    Entries.Add(entry);
            }
        }
    }

    public struct NetThrowRequest : INetworkSerializable
    {
        public ushort Seq;
        public byte Prefab;
        public double HostTime;
        public Vector3 Origin;
        public Vector3 Direction;
        public ThrowStats Stats;
        public BallPerks Perks;
        public bool Phantom;
        public bool YoyoString;
        public sbyte OwnerSlot;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Seq);
            serializer.SerializeValue(ref Prefab);
            serializer.SerializeValue(ref HostTime);
            serializer.SerializeValue(ref Origin);
            serializer.SerializeValue(ref Direction);
            SerializeStats(serializer, ref Stats);
            SerializePerks(serializer, ref Perks);
            serializer.SerializeValue(ref Phantom);
            serializer.SerializeValue(ref YoyoString);
            serializer.SerializeValue(ref OwnerSlot);
        }

        static void SerializeStats<T>(BufferSerializer<T> s, ref ThrowStats stats) where T : IReaderWriter
        {
            s.SerializeValue(ref stats.Speed);
            s.SerializeValue(ref stats.UpVelocity);
            s.SerializeValue(ref stats.Gravity);
            s.SerializeValue(ref stats.Damage);
            s.SerializeValue(ref stats.BonusDamage);
            s.SerializeValue(ref stats.Knockback);
            s.SerializeValue(ref stats.Flags);
        }

        static void SerializePerks<T>(BufferSerializer<T> s, ref BallPerks p) where T : IReaderWriter
        {
            s.SerializeValue(ref p.extraShots);
            s.SerializeValue(ref p.spreadAngle);
            s.SerializeValue(ref p.chainBounces);
            s.SerializeValue(ref p.areaRadius);
            s.SerializeValue(ref p.areaDamage);
            s.SerializeValue(ref p.splitOnHit);
            s.SerializeValue(ref p.boomerang);
            s.SerializeValue(ref p.elastic);
            s.SerializeValue(ref p.floorBounces);
            s.SerializeValue(ref p.gumTrail);
            s.SerializeValue(ref p.blastRadius);
            s.SerializeValue(ref p.blastDamage);
            s.SerializeValue(ref p.snakeAmplitude);
            s.SerializeValue(ref p.grazeRadius);
            s.SerializeValue(ref p.wallDamage);
            s.SerializeValue(ref p.chargedPierce);
            s.SerializeValue(ref p.homing);
            s.SerializeValue(ref p.yoyo);
            s.SerializeValue(ref p.bounceBlastRadius);
            s.SerializeValue(ref p.bounceBlastDamage);
            s.SerializeValue(ref p.twinSplit);
            s.SerializeValue(ref p.areaStun);
            s.SerializeValue(ref p.curve);
            s.SerializeValue(ref p.lob);
            s.SerializeValue(ref p.groundRoll);
            s.SerializeValue(ref p.grazeStun);
            s.SerializeValue(ref p.chalk);
            s.SerializeValue(ref p.frozenBonus);
            s.SerializeValue(ref p.bounceAssist);
        }
    }

    public static class BallMotion
    {
        const float MaxTime = 1.5f;

        public static void Extrapolate(BallState state, Vector3 p0, Vector3 v0, float gravity, float radius, float t,
            out Vector3 position, out Vector3 velocity)
        {
            t = Mathf.Clamp(t, 0f, MaxTime);
            switch (state)
            {
                case BallState.Live:
                case BallState.Popped:
                    velocity = v0 + Vector3.down * (gravity * t);
                    position = p0 + v0 * t + Vector3.down * (0.5f * gravity * t * t);
                    break;
                case BallState.Loose:
                {
                    float k = Mathf.Max(0.01f, gravity);
                    float decay = Mathf.Exp(-k * t);
                    float g = -Physics.gravity.y;
                    velocity = new Vector3(v0.x * decay, v0.y - g * t, v0.z * decay);
                    position = p0 + new Vector3(v0.x, 0f, v0.z) * ((1f - decay) / k)
                               + Vector3.up * (v0.y * t - 0.5f * g * t * t);
                    if (Mathf.Abs(v0.y) < 0.05f)
                    {
                        position.y = p0.y;
                        velocity.y = 0f;
                    }
                    break;
                }
                case BallState.Returning:
                    velocity = v0;
                    position = p0 + v0 * t;
                    break;
                default:
                    velocity = Vector3.zero;
                    position = p0;
                    break;
            }
            if (state is BallState.Live or BallState.Popped or BallState.Loose && position.y < radius && p0.y >= radius - 0.01f)
            {
                position.y = radius;
                if (velocity.y < 0f)
                    velocity.y = 0f;
            }
        }
    }
}

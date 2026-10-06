using System.Collections.Generic;
using Bouncer.Core;
using Unity.Netcode;
using UnityEngine;

namespace Bouncer.Net
{
    public struct NetEnemySpawn : INetworkSerializable
    {
        public ushort Id;
        public ushort Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
        public byte Affix;
        public ushort Health;
        public ushort MaxHealth;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Prefab);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Affix);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
        }
    }

    public struct NetEnemyMotion
    {
        public ushort Id;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public float Frozen;
        public byte PayloadLength;
        public byte[] Payload;
    }

    public struct NetEnemyMotionBatch : INetworkSerializable
    {
        public double Time;
        public List<NetEnemyMotion> Entries;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Time);
            int count = Entries?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader)
                Entries = new List<NetEnemyMotion>(count);
            for (int i = 0; i < count; i++)
            {
                var entry = serializer.IsReader ? default : Entries[i];
                serializer.SerializeValue(ref entry.Id);
                serializer.SerializeValue(ref entry.Position);
                ushort rx = 0, ry = 0, rz = 0, rw = 0, vx = 0, vy = 0, vz = 0;
                byte frozen = 0;
                if (!serializer.IsReader)
                {
                    rx = Mathf.FloatToHalf(entry.Rotation.x);
                    ry = Mathf.FloatToHalf(entry.Rotation.y);
                    rz = Mathf.FloatToHalf(entry.Rotation.z);
                    rw = Mathf.FloatToHalf(entry.Rotation.w);
                    vx = Mathf.FloatToHalf(entry.Velocity.x);
                    vy = Mathf.FloatToHalf(entry.Velocity.y);
                    vz = Mathf.FloatToHalf(entry.Velocity.z);
                    frozen = (byte)Mathf.Clamp(Mathf.CeilToInt(entry.Frozen / 0.05f), 0, 255);
                }
                serializer.SerializeValue(ref rx);
                serializer.SerializeValue(ref ry);
                serializer.SerializeValue(ref rz);
                serializer.SerializeValue(ref rw);
                serializer.SerializeValue(ref vx);
                serializer.SerializeValue(ref vy);
                serializer.SerializeValue(ref vz);
                serializer.SerializeValue(ref frozen);
                serializer.SerializeValue(ref entry.PayloadLength);
                if (serializer.IsReader)
                {
                    var rotation = new Quaternion(Mathf.HalfToFloat(rx), Mathf.HalfToFloat(ry), Mathf.HalfToFloat(rz), Mathf.HalfToFloat(rw));
                    float norm = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w);
                    entry.Rotation = norm > 1e-4f
                        ? new Quaternion(rotation.x / norm, rotation.y / norm, rotation.z / norm, rotation.w / norm)
                        : Quaternion.identity;
                    entry.Velocity = new Vector3(Mathf.HalfToFloat(vx), Mathf.HalfToFloat(vy), Mathf.HalfToFloat(vz));
                    entry.Frozen = frozen * 0.05f;
                    entry.Payload = new byte[NetWriter.Capacity];
                }
                int length = Mathf.Min(entry.PayloadLength, NetWriter.Capacity);
                for (int b = 0; b < length; b++)
                {
                    byte value = serializer.IsReader ? (byte)0 : entry.Payload[b];
                    serializer.SerializeValue(ref value);
                    if (serializer.IsReader)
                        entry.Payload[b] = value;
                }
                if (serializer.IsReader)
                    Entries.Add(entry);
            }
        }
    }

    public struct NetEnemyHit : INetworkSerializable
    {
        public ushort Id;
        public ushort Health;
        public ushort MaxHealth;
        public Vector3 Point;
        public Vector3 Direction;
        public float Force;
        public HitFlags Flags;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref Health);
            serializer.SerializeValue(ref MaxHealth);
            serializer.SerializeValue(ref Point);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Force);
            serializer.SerializeValue(ref Flags);
        }
    }

    public struct NetHit : INetworkSerializable
    {
        public int Damage;
        public Vector3 Point;
        public Vector3 Direction;
        public float Force;
        public Team SourceTeam;
        public HitFlags Flags;

        public static NetHit From(in HitInfo hit) => new()
        {
            Damage = hit.Damage,
            Point = hit.Point,
            Direction = hit.Direction,
            Force = hit.Force,
            SourceTeam = hit.SourceTeam,
            Flags = hit.Flags,
        };

        public HitInfo ToHit(GameObject source) => new()
        {
            Damage = Damage,
            Point = Point,
            Direction = Direction,
            Force = Force,
            SourceTeam = SourceTeam,
            Source = source,
            Flags = Flags,
        };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref Point);
            serializer.SerializeValue(ref Direction);
            serializer.SerializeValue(ref Force);
            serializer.SerializeValue(ref SourceTeam);
            serializer.SerializeValue(ref Flags);
        }
    }
}

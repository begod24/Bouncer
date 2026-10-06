using System;
using UnityEngine;

namespace Bouncer.Core
{
    public static class NetHooks
    {
        public static bool IsGuest => Online.Active && !Online.IsHost;

        public static Func<Component, HitInfo, bool> ForwardEnemyHit;

        public static Func<Targetable, float, bool> ForwardFreeze;

        public static Func<float, bool> ForwardFreezeEnemies;

        public static Func<GameObject, HitInfo, bool> HitRemotePlayer;

        public static Func<GameObject, int, bool> HealRemotePlayer;

        public static Func<GameObject, bool> GivePortfolio;

        public static Action<GameObject> MirrorSpawn;

        public static Func<int, bool> LeaveArena;

        public static bool ApplyHit(IDamageable target, in HitInfo hit)
        {
            if (target == null)
                return false;
            if (IsGuest && ForwardEnemyHit != null && target is Component component && component
                && component.TryGetComponent(out Targetable targetable) && targetable.Team == Team.Enemy)
                return ForwardEnemyHit(component, hit);
            return target.ApplyHit(hit);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ForwardEnemyHit = null;
            ForwardFreeze = null;
            ForwardFreezeEnemies = null;
            HitRemotePlayer = null;
            HealRemotePlayer = null;
            GivePortfolio = null;
            LeaveArena = null;
            MirrorSpawn = null;
        }
    }

    public interface INetEnemy
    {
        void WriteNet(NetWriter writer);

        void ReadNet(NetReader reader, float age);
    }

    public sealed class NetWriter
    {
        public const int Capacity = 32;

        readonly byte[] _bytes = new byte[Capacity];
        bool _warned;

        public byte[] Bytes => _bytes;
        public int Length { get; private set; }

        public void Reset() => Length = 0;

        public void Byte(byte value)
        {
            if (Length < Capacity)
            {
                _bytes[Length++] = value;
                return;
            }
            if (_warned)
                return;
            _warned = true;
            Debug.LogWarning($"[Net] Состояние врага не влезло в {Capacity} байт.");
        }

        public void Bool(bool value) => Byte(value ? (byte)1 : (byte)0);

        public void UShort(ushort value)
        {
            Byte((byte)value);
            Byte((byte)(value >> 8));
        }

        public void Float(float value)
        {
            int bits = BitConverter.SingleToInt32Bits(value);
            Byte((byte)bits);
            Byte((byte)(bits >> 8));
            Byte((byte)(bits >> 16));
            Byte((byte)(bits >> 24));
        }

        public void Seconds(float value) => UShort((ushort)Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, ushort.MaxValue));

        public void Direction(Vector3 value) =>
            Byte((byte)Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg, 360f) / 360f * 255f));
    }

    public sealed class NetReader
    {
        byte[] _bytes;
        int _length;
        int _position;

        public void Reset(byte[] bytes, int length)
        {
            _bytes = bytes;
            _length = length;
            _position = 0;
        }

        public byte Byte() => _bytes != null && _position < _length ? _bytes[_position++] : (byte)0;

        public bool Bool() => Byte() != 0;

        public ushort UShort() => (ushort)(Byte() | (Byte() << 8));

        public float Float() => BitConverter.Int32BitsToSingle(Byte() | (Byte() << 8) | (Byte() << 16) | (Byte() << 24));

        public float Seconds() => UShort() / 1000f;

        public Vector3 Direction()
        {
            float angle = Byte() / 255f * 360f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
        }
    }
}

using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Unity.Netcode;
using UnityEngine;

namespace Bouncer.Net
{
    /// <summary>
    /// Мяч хозяина, как его видят гости: какой он, чей, чем бьёт и как движется с момента <see cref="Time"/>
    /// (время хозяина). Хозяин шлёт такую запись, когда мяч появился, сменил состояние или ушёл с прежнего пути;
    /// дальше гость сам ведёт копию по <see cref="BallMotion"/>.
    /// </summary>
    public struct NetBallState : INetworkSerializable
    {
        public const byte KindUpsert = 0;
        /// <summary>Мяча больше нет (Id = 0 — бросок гостя, не долетевший до хозяина: убрать его предсказанную копию).</summary>
        public const byte KindDespawn = 1;

        const byte PhantomBit = 1 << 0;
        const byte YoyoBit = 1 << 1;
        const byte HotBit = 1 << 2;

        public ushort Id;
        public byte Kind;
        /// <summary>Номер префаба в <see cref="NetBalls"/>.</summary>
        public byte Prefab;
        public BallState State;
        public Team Team;
        public HitFlags Flags;
        public byte Bits;
        /// <summary>Чей мяч из запаса: номер игрока, -1 — ничей.</summary>
        public sbyte OwnerSlot;
        /// <summary>Кто бросил: номер игрока, -1 — не игрок.</summary>
        public sbyte ThrowerSlot;
        public byte Damage;
        public float Knockback;
        public double Time;
        public Vector3 Position;
        public Vector3 Velocity;
        /// <summary>Гравитация полёта; у лежащего мяча — торможение (см. <see cref="BallMotion"/>).</summary>
        public float Gravity;
        /// <summary>Бросок гостя: его номер и номер броска — чтобы гость узнал свою предсказанную копию. -1 — нет.</summary>
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

        /// <summary>Горячая картошка ещё не взорвалась.</summary>
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

    /// <summary>Все записи о мячах за один такт хозяина — одним сообщением.</summary>
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

    /// <summary>Бросок гостя — хозяину: всё, чтобы бросить у себя тот же мяч.</summary>
    public struct NetThrowRequest : INetworkSerializable
    {
        /// <summary>Номер броска у гостя — по нему он узнает свою предсказанную копию.</summary>
        public ushort Seq;
        public byte Prefab;
        /// <summary>Время хозяина, каким его считал гость в момент броска: насколько прогнать мяч вперёд.</summary>
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
        }
    }

    /// <summary>
    /// Куда мяч долетит за t секунд по последнему известному движению. Одна формула у хозяина (решить, пора ли
    /// слать поправку) и у гостей (вести копию): летящий и «свечка» — по дуге со своей гравитацией, лежащий катится
    /// с торможением и падает на землю, возвращающийся летит прямо. Ниже земли мяч не опускается.
    /// </summary>
    public static class BallMotion
    {
        /// <summary>Дольше этого без вестей от хозяина мяч не ведём — стоит, где был.</summary>
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
                    // Лежащий выше земли (на ящике) не проваливается: падает только тот, что летел вверх или вниз.
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

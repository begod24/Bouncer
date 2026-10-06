using System;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Что подставляет сетевая игра (сборка Bouncer.Net) в остальной код. Мир по сети считает хозяин комнаты:
    /// враги, волны, монетки, находки. У гостя всё это — копии, которыми двигает сеть, поэтому «мозги» врагов у гостя
    /// не работают (<see cref="IsGuest"/>), а то, что гость делает с копиями (подкат, «Свисток», «Домино»), уходит
    /// хозяину. Удары врагов по игроку другого компьютера хозяин отправляет его хозяину. В соло всё пусто.
    /// </summary>
    public static class NetHooks
    {
        /// <summary>Этот компьютер — гость сетевой игры: враги и мир здесь — копии с хозяина.</summary>
        public static bool IsGuest => Online.Active && !Online.IsHost;

        /// <summary>Гость: удар по копии врага — хозяину. true — ушёл.</summary>
        public static Func<Component, HitInfo, bool> ForwardEnemyHit;

        /// <summary>Гость: заморозка копии врага — хозяину. true — ушла.</summary>
        public static Func<Targetable, float, bool> ForwardFreeze;

        /// <summary>Гость: заморозить всех врагов («Замри!») — хозяину. true — ушло.</summary>
        public static Func<float, bool> ForwardFreezeEnemies;

        /// <summary>Хозяин: удар по игроку другого компьютера — ему. true — ушёл.</summary>
        public static Func<GameObject, HitInfo, bool> HitRemotePlayer;

        /// <summary>Хозяин: игроку другого компьютера +сердца (лимонад). true — ушло.</summary>
        public static Func<GameObject, int, bool> HealRemotePlayer;

        /// <summary>Хозяин: игрок другого компьютера подобрал портфель — в его рюкзак. true — ушло.</summary>
        public static Func<GameObject, bool> GivePortfolio;

        /// <summary>
        /// Хозяин: показать гостям такую же штуку там же (огненный след, облако ваты…). У гостя она только для вида:
        /// бьёт и замедляет её копия у хозяина.
        /// </summary>
        public static Action<GameObject> MirrorSpawn;

        /// <summary>
        /// Хозяин: все живые игроки стоят у одной стрелки — вести всех на следующую арену (номер развилки).
        /// true — переход начался.
        /// </summary>
        public static Func<int, bool> LeaveArena;

        /// <summary>
        /// Ударить: у гостя удар по копии врага уходит хозяину, иначе — прямо в цель. Так бьют карточки игрока
        /// (подкат, «Домино»), чтобы по сети их удары засчитывал хозяин.
        /// </summary>
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

    /// <summary>
    /// Враг, которого по сети видят гости: кроме положения ему нужно передать то, что показывает его анимация
    /// (состояние, сколько оно длится, мяч в руке…). Хозяин пишет, гость читает — у гостя «мозги» врага не работают.
    /// </summary>
    public interface INetEnemy
    {
        void WriteNet(NetWriter writer);

        /// <summary>age — сколько секунд прошло с момента, когда хозяин это записал (время состояния идёт дальше).</summary>
        void ReadNet(NetReader reader, float age);
    }

    /// <summary>Маленький буфер для <see cref="INetEnemy"/>: не больше <see cref="Capacity"/> байт.</summary>
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

        /// <summary>Секунды с точностью до миллисекунды, до 65 с.</summary>
        public void Seconds(float value) => UShort((ushort)Mathf.Clamp(Mathf.RoundToInt(value * 1000f), 0, ushort.MaxValue));

        /// <summary>Направление в плоскости XZ одним байтом.</summary>
        public void Direction(Vector3 value) =>
            Byte((byte)Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg, 360f) / 360f * 255f));
    }

    /// <summary>Читает то, что записал <see cref="NetWriter"/>. Кончились байты — нули.</summary>
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

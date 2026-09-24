using System;
using UnityEngine;

namespace Bouncer.Core
{
    [Flags]
    public enum HitFlags
    {
        None = 0,
        Charged = 1 << 0,
        Candle = 1 << 1,
        Melee = 1 << 2,
        /// <summary>Урон по площади (набивной мяч): задевает соседей основной цели.</summary>
        Area = 1 << 3,
        /// <summary>Подкат: рывок игрока сбил с ног.</summary>
        Tackle = 1 << 4,
    }

    public struct HitInfo
    {
        public int Damage;
        public Vector3 Point;
        /// <summary>Горизонтальное направление удара (нормализовано).</summary>
        public Vector3 Direction;
        /// <summary>Сила отброса (импульс).</summary>
        public float Force;
        public Team SourceTeam;
        public GameObject Source;
        public HitFlags Flags;

        public bool Has(HitFlags flag) => (Flags & flag) != 0;
    }

    public interface IDamageable
    {
        /// <summary>true — удар прошёл (не заблокирован неуязвимостью и т.п.).</summary>
        bool ApplyHit(in HitInfo hit);
    }
}

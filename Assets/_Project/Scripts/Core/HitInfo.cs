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
        Area = 1 << 3,
        Tackle = 1 << 4,
        Despawn = 1 << 5,
        Domino = 1 << 6,
        Spiky = 1 << 7,
        Wet = 1 << 8,
        Heavy = 1 << 9,
        Dark = 1 << 10,
        Burn = 1 << 11,
    }

    public struct HitInfo
    {
        public int Damage;
        public Vector3 Point;
        public Vector3 Direction;
        public float Force;
        public Team SourceTeam;
        public GameObject Source;
        public HitFlags Flags;

        public bool Has(HitFlags flag) => (Flags & flag) != 0;
    }

    public interface IDamageable
    {
        bool ApplyHit(in HitInfo hit);
    }

    public interface IBurnable
    {
        bool BurnsInLight { get; }
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
    /// <summary>Что произошло, когда летящий мяч коснулся цели.</summary>
    public enum BallContactResult
    {
        /// <summary>Мяч пролетает насквозь (рывок, неуязвимость).</summary>
        PassThrough,
        /// <summary>Цель поймала мяч — он уже забран или брошен на землю.</summary>
        Caught,
        /// <summary>Попадание — мяч отскакивает вверх («свечка»).</summary>
        Hit,
        /// <summary>Мяч отражается, как от стены.</summary>
        Bounce,
    }

    /// <summary>Всё, во что может попасть мяч: игроки, враги.</summary>
    public interface IBallTarget
    {
        BallContactResult OnBallContact(Ball ball, in RaycastHit hit);
    }

    /// <summary>Параметры полёта и удара конкретного броска.</summary>
    public struct ThrowStats
    {
        public float Speed;
        public float UpVelocity;
        public float Gravity;
        public int Damage;
        public float Knockback;
        public HitFlags Flags;

        public bool Has(HitFlags flag) => (Flags & flag) != 0;
    }

    public struct BallThrow
    {
        public Vector3 Origin;
        /// <summary>Горизонтальное направление броска.</summary>
        public Vector3 Direction;
        public ThrowStats Stats;
        public Team Team;
        public GameObject Thrower;
    }
}

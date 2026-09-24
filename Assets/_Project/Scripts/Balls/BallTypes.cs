using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
    /// <summary>
    /// Особые свойства броска: от типа мяча (волейбольный, набивной, теннисный) и от карточек-модификаторов.
    /// Складываются: тип мяча + все взятые модификаторы.
    /// </summary>
    [Serializable]
    public struct BallPerks
    {
        [Tooltip("Сколько лишних мячей летит веером при броске (теннисный). Лишние исчезают, коснувшись пола")]
        [Min(0)] public int extraShots;
        [Tooltip("Угол веера между соседними мячами, градусы")]
        [Min(0f)] public float spreadAngle;
        [Tooltip("Сколько раз мяч после попадания перелетает к следующему врагу (волейбольный)")]
        [Min(0)] public int chainBounces;
        [Tooltip("Радиус урона по площади при попадании, м (набивной). 0 — нет")]
        [Min(0f)] public float areaRadius;
        [Tooltip("Урон соседям основной цели")]
        [Min(0)] public int areaDamage;
        [Tooltip("При первом попадании раскалывается на два мяча-двойника")]
        public bool splitOnHit;
        [Tooltip("Бумеранг: пролетев немного, разворачивается и летит обратно в руки")]
        public bool boomerang;
        [Tooltip("На резинке: упав на пол, сам возвращается в руки")]
        public bool elastic;
        [Tooltip("Попрыгунчик: сколько раз мяч отскакивает от асфальта и летит дальше опасным")]
        [Min(0)] public int floorBounces;
        [Tooltip("Жвачка: мяч оставляет на асфальте липкий след, в котором вязнут враги")]
        public bool gumTrail;
        [Tooltip("Горячая картошка: радиус взрыва при первом попадании или касании асфальта, м. 0 — нет")]
        [Min(0f)] public float blastRadius;
        [Tooltip("Урон взрыва всем вокруг")]
        [Min(0)] public int blastDamage;
        [Tooltip("Сдутый мяч: летит змейкой, уходя в стороны на столько метров. 0 — прямо")]
        [Min(0f)] public float snakeAmplitude;
        [Tooltip("Сдутый мяч: задевает всех врагов на таком расстоянии от себя и летит дальше, «свечкой» не отскакивает. " +
                 "0 — обычное попадание")]
        [Min(0f)] public float grazeRadius;
        [Tooltip("Стеночка: каждый рикошет от стены прибавляет этому броску столько урона")]
        [Min(0)] public int wallDamage;
        [Tooltip("Рогатка: заряженный бросок пробивает всех врагов насквозь")]
        public bool chargedPierce;
        [Tooltip("Глаз-алмаз: мяч доворачивает к ближайшему врагу впереди, градусов в секунду. 0 — летит прямо")]
        [Min(0f)] public float homing;
        [Tooltip("Йо-йо: после попадания или в конце нити мяч сам летит в руки, задевая всех на обратном пути")]
        public bool yoyo;

        /// <summary>
        /// Что достаётся мячу-двойнику: бьёт и летит так же (цепочка, площадь, отскоки, жвачка, змейка, стеночка,
        /// рогатка, доводка), но не множится дальше, не взрывается и не возвращается в руки — иначе двойники
        /// превращались бы в лишние мячи.
        /// </summary>
        public BallPerks ForTwin() => new()
        {
            chainBounces = chainBounces,
            areaRadius = areaRadius,
            areaDamage = areaDamage,
            floorBounces = floorBounces,
            gumTrail = gumTrail,
            snakeAmplitude = snakeAmplitude,
            grazeRadius = grazeRadius,
            wallDamage = wallDamage,
            chargedPierce = chargedPierce,
            homing = homing,
        };

        public static BallPerks Combine(in BallPerks a, in BallPerks b) => new()
        {
            extraShots = a.extraShots + b.extraShots,
            spreadAngle = Mathf.Max(a.spreadAngle, b.spreadAngle),
            chainBounces = a.chainBounces + b.chainBounces,
            areaRadius = Mathf.Max(a.areaRadius, b.areaRadius),
            areaDamage = Mathf.Max(a.areaDamage, b.areaDamage),
            splitOnHit = a.splitOnHit || b.splitOnHit,
            boomerang = a.boomerang || b.boomerang,
            elastic = a.elastic || b.elastic,
            floorBounces = a.floorBounces + b.floorBounces,
            gumTrail = a.gumTrail || b.gumTrail,
            blastRadius = Mathf.Max(a.blastRadius, b.blastRadius),
            blastDamage = Mathf.Max(a.blastDamage, b.blastDamage),
            snakeAmplitude = Mathf.Max(a.snakeAmplitude, b.snakeAmplitude),
            grazeRadius = Mathf.Max(a.grazeRadius, b.grazeRadius),
            wallDamage = a.wallDamage + b.wallDamage,
            chargedPierce = a.chargedPierce || b.chargedPierce,
            homing = Mathf.Max(a.homing, b.homing),
            yoyo = a.yoyo || b.yoyo,
        };
    }

    /// <summary>Тот, кому мяч может вернуться в руки сам (бумеранг, мяч на резинке).</summary>
    public interface IBallReceiver
    {
        /// <summary>Забрать мяч в руки. false — руки заняты или получатель выбит.</summary>
        bool TryReceive(Ball ball);
    }
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
        /// <summary>Цель выбита, а мяч летит дальше, потеряв часть скорости (лёгкие враги вроде пупсов).</summary>
        Pierce,
        /// <summary>Цель сама задала мячу новый полёт через <see cref="Ball.Redirect"/> (качели).</summary>
        Redirected,
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
        public BallPerks Perks;
        /// <summary>Мяч-двойник (веер теннисных, раскол): бьёт как обычный, но исчезает, коснувшись пола.</summary>
        public bool Phantom;
    }
}

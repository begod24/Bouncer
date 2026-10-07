using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
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
        [Tooltip("Прыгающая бомба: каждый отскок от асфальта взрывается с таким радиусом, м. 0 — нет")]
        [Min(0f)] public float bounceBlastRadius;
        [Tooltip("Прыгающая бомба: урон взрыва на отскоке")]
        [Min(0)] public int bounceBlastDamage;
        [Tooltip("Град: двойники (веер теннисных, раскол) тоже раскалываются при попадании — один раз")]
        public bool twinSplit;
        [Tooltip("Гиря: урон по площади оглушает задетых на столько секунд. 0 — нет")]
        [Min(0f)] public float areaStun;
        [Tooltip("Кручёный мяч: летит дугой, поворачивая на столько градусов в секунду (знак — в какую сторону). 0 — прямо")]
        public float curve;
        [Tooltip("Баскетбольный: летит навесом над головами и щитами, бьёт только на излёте — и по площади, когда падает")]
        public bool lob;
        [Tooltip("Футбольный: катится по асфальту и задевает всех на пути")]
        public bool groundRoll;
        [Tooltip("Задетые мячом (сдутый, футбольный) сбиты с ног на столько секунд. 0 — нет")]
        [Min(0f)] public float grazeStun;
        [Tooltip("Мел: коснувшись асфальта, мяч оставляет меловой крестик — наступил, и бежишь быстрее")]
        public bool chalk;
        [Tooltip("Морская фигура: по замершему врагу этот мяч бьёт на столько сильнее")]
        [Min(0)] public int frozenBonus;
        [Tooltip("Пинг-понг: отскочив от стены, доворачивает к врагу в пределах стольких градусов. 0 — нет")]
        [Min(0f)] public float bounceAssist;

        public BallPerks ForTwin() => new()
        {
            chainBounces = chainBounces,
            areaRadius = areaRadius,
            areaDamage = areaDamage,
            splitOnHit = twinSplit,
            floorBounces = floorBounces,
            gumTrail = gumTrail,
            snakeAmplitude = snakeAmplitude,
            grazeRadius = grazeRadius,
            wallDamage = wallDamage,
            chargedPierce = chargedPierce,
            areaStun = areaStun,
            grazeStun = grazeStun,
            frozenBonus = frozenBonus,
            bounceAssist = bounceAssist,
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
            bounceBlastRadius = Mathf.Max(a.bounceBlastRadius, b.bounceBlastRadius),
            bounceBlastDamage = Mathf.Max(a.bounceBlastDamage, b.bounceBlastDamage),
            twinSplit = a.twinSplit || b.twinSplit,
            areaStun = Mathf.Max(a.areaStun, b.areaStun),
            curve = a.curve + b.curve,
            lob = a.lob || b.lob,
            groundRoll = a.groundRoll || b.groundRoll,
            grazeStun = Mathf.Max(a.grazeStun, b.grazeStun),
            chalk = a.chalk || b.chalk,
            frozenBonus = a.frozenBonus + b.frozenBonus,
            bounceAssist = Mathf.Max(a.bounceAssist, b.bounceAssist),
        };
    }

    public interface IBallInterceptor
    {
        bool TryIntercept(Ball ball);
    }

    public interface IBallShield
    {
        bool Blocks(Ball ball);

        void Struck(Vector3 point);
    }

    public interface IBallReceiver
    {
        bool TryReceive(Ball ball);
    }
    public enum BallContactResult
    {
        PassThrough,
        Caught,
        Hit,
        Bounce,
        Pierce,
        Redirected,
    }

    public interface IBallTarget
    {
        BallContactResult OnBallContact(Ball ball, in RaycastHit hit);
    }

    public struct ThrowStats
    {
        public float Speed;
        public float UpVelocity;
        public float Gravity;
        public int Damage;
        public int BonusDamage;
        public float Knockback;
        public HitFlags Flags;

        public bool Has(HitFlags flag) => (Flags & flag) != 0;

        public ThrowStats WithoutBonus()
        {
            var stats = this;
            stats.Damage = Mathf.Max(0, Damage - BonusDamage);
            stats.BonusDamage = 0;
            return stats;
        }
    }

    public struct BallThrow
    {
        public Vector3 Origin;
        public Vector3 Direction;
        public ThrowStats Stats;
        public Team Team;
        public GameObject Thrower;
        public BallPerks Perks;
        public bool Phantom;
        public GameObject Owner;
        public bool YoyoString;
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
    [CreateAssetMenu(menuName = "Bouncer/Ball Definition", fileName = "Ball_")]
    public sealed class BallDefinition : ScriptableObject
    {
        [Header("Тело")]
        [Min(0.05f)] public float radius = 0.22f;
        [Min(0.01f)] public float mass = 0.45f;

        [Header("Бросок: обычный → полностью заряженный")]
        public float speed = 20f;
        public float chargedSpeed = 32f;
        [Tooltip("Начальная вертикальная скорость: мяч летит чуть вверх, потом снижается")]
        public float upVelocity = 1.2f;
        public float chargedUpVelocity = 0.6f;
        [Tooltip("Гравитация летящего мяча, м/с². Мяч перестаёт быть опасным, когда касается пола.")]
        public float liveGravity = 3f;
        public float chargedLiveGravity = 1.5f;

        [Header("Урон и отброс")]
        [Min(0)] public int damage = 1;
        [Min(0)] public int chargedDamage = 2;
        [Min(0)] public int candleDamage = 3;
        public float knockback = 6f;
        public float chargedKnockback = 11f;
        public float candleKnockback = 16f;
        [Tooltip("Бросок после «свечки» быстрее полностью заряженного во столько раз")]
        public float candleSpeedMultiplier = 1.15f;

        [Header("Рикошеты")]
        [Min(0)] public int maxRicochets = 3;
        [Range(0f, 1f)] public float wallSpeedKeep = 0.9f;
        [Tooltip("Ниже этой горизонтальной скорости мяч перестаёт быть опасным")]
        public float minLiveSpeed = 7f;
        [Min(0.1f)] public float maxLiveTime = 3f;
        [Tooltip("Какую долю скорости мяч сохраняет, пробив лёгкого врага насквозь")]
        [Range(0f, 1f)] public float pierceSpeedKeep = 0.75f;

        [Header("«Свечка»: отскок вверх от тела")]
        public float popUpSpeed = 9f;
        [Range(0f, 1f)] public float popHorizontalKeep = 0.15f;
        public float popGravity = 14f;
        [Range(0f, 1f)] public float poppedWallKeep = 0.5f;

        [Header("Свойства этого типа мяча")]
        public BallPerks perks;
        [Tooltip("Эффект урона по площади (кольцо на земле), из пула")]
        public GameObject areaEffect;

        [Header("Эффекты карточек и типов мячей")]
        [Tooltip("Цепочка: на каком расстоянии мяч ищет следующего врага")]
        public float chainRange = 9f;
        [Tooltip("Цепочка: какую долю скорости мяч сохраняет при перелёте")]
        [Range(0.3f, 1f)] public float chainSpeedKeep = 0.9f;
        [Tooltip("Раскол: угол разлёта двойников от направления мяча, градусы")]
        public float splitAngle = 35f;
        [Range(0.3f, 1f)] public float splitSpeedKeep = 0.8f;
        [Tooltip("Бумеранг: через сколько секунд полёта мяч разворачивается")]
        public float boomerangDelay = 0.35f;
        [Tooltip("Бумеранг: скорость разворота, градусов в секунду")]
        public float boomerangTurnRate = 540f;
        [Tooltip("На резинке: скорость, с которой мяч возвращается в руки")]
        public float elasticReturnSpeed = 18f;

        [Header("Лежащий мяч")]
        [Range(0f, 1f)] public float floorBounceKeep = 0.5f;
        public float looseDamping = 0.6f;
        [Tooltip("Сколько мячей может лежать на арене; лишние исчезают, начиная со старых")]
        [Min(1)] public int maxLooseBalls = 14;

        public ThrowStats GetThrowStats(float charge01, bool candle)
        {
            if (candle)
            {
                return new ThrowStats
                {
                    Speed = chargedSpeed * candleSpeedMultiplier,
                    UpVelocity = chargedUpVelocity,
                    Gravity = chargedLiveGravity,
                    Damage = candleDamage,
                    Knockback = candleKnockback,
                    Flags = HitFlags.Charged | HitFlags.Candle,
                };
            }

            charge01 = Mathf.Clamp01(charge01);
            bool full = charge01 >= 0.999f;
            return new ThrowStats
            {
                Speed = Mathf.Lerp(speed, chargedSpeed, charge01),
                UpVelocity = Mathf.Lerp(upVelocity, chargedUpVelocity, charge01),
                Gravity = Mathf.Lerp(liveGravity, chargedLiveGravity, charge01),
                Damage = full ? chargedDamage : damage,
                Knockback = Mathf.Lerp(knockback, chargedKnockback, charge01),
                Flags = full ? HitFlags.Charged : HitFlags.None,
            };
        }

        /// <summary>Примерная дальность до касания пола (для линии прицела).</summary>
        public float EstimateRange(in ThrowStats stats, float launchHeight)
        {
            float drop = Mathf.Max(0f, launchHeight - radius);
            float gravity = Mathf.Max(0.01f, stats.Gravity);
            float time = (stats.UpVelocity + Mathf.Sqrt(stats.UpVelocity * stats.UpVelocity + 2f * gravity * drop)) / gravity;
            return stats.Speed * Mathf.Min(time, maxLiveTime);
        }
    }
}

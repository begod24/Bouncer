using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Player Stats", fileName = "PlayerStats_")]
    public sealed class PlayerStats : ScriptableObject
    {
        [Header("Движение")]
        public float moveSpeed = 7f;
        public float acceleration = 70f;
        public float deceleration = 90f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 1440f;
        [Range(0f, 1f)] public float chargingMoveMultiplier = 0.55f;
        [Range(0f, 1f)] public float catchingMoveMultiplier = 0.8f;

        [Header("Рывок-уклонение")]
        public float dashDistance = 4.5f;
        public float dashDuration = 0.16f;
        public float dashCooldown = 0.9f;
        [Tooltip("Сколько секунд от начала рывка мячи и удары проходят сквозь игрока")]
        public float dashInvulnerability = 0.22f;

        [Header("Жизни")]
        [Min(1)] public int maxLives = 3;
        public float hurtInvulnerability = 1.2f;
        [Tooltip("Как быстро гаснет отброс после удара")]
        public float knockbackDamping = 10f;

        [Header("Мячи")]
        [Min(0)] public int startBalls = 2;
        [Min(1)] public int maxBalls = 3;
        public float pickupRadius = 1.1f;
        [Tooltip("Высота, с которой вылетает мяч")]
        public float throwHeight = 1.1f;
        public float throwForwardOffset = 0.6f;
        [Tooltip("Отпустил быстрее — обычный бросок. Дольше — начинается заряд.")]
        public float tapThreshold = 0.15f;
        [Tooltip("Время от начала заряда до полного")]
        public float chargeTime = 0.7f;
        public float throwCooldown = 0.18f;

        [Header("Ловля")]
        public float catchWindow = 0.28f;
        public float catchRadius = 1.5f;
        public float catchMissCooldown = 0.5f;
        public float catchSuccessCooldown = 0.1f;
        [Tooltip("До какой высоты над полом можно поймать «свечку»")]
        public float candleCatchHeight = 3.2f;
        [Tooltip("Сколько жизней даёт пойманный мяч врага")]
        [Min(0)] public int catchHeal = 1;

        [Header("Карточки")]
        [Tooltip("Подкат: на каком расстоянии рывок сбивает врагов, м")]
        public float tackleRadius = 1.1f;
        [Tooltip("Подкат: сила отброса сбитого врага")]
        public float tackleKnockback = 14f;
        [Tooltip("«Замри!»: скорость времени вокруг после удачной ловли")]
        [Range(0.05f, 1f)] public float freezeTimeScale = 0.3f;

        [Header("Прицел")]
        [Tooltip("Высота плоскости, на которую проецируется курсор (уровень груди врагов)")]
        public float aimPlaneHeight = 0.8f;
        public float stickDeadzone = 0.25f;
        public float autoAimRange = 18f;
        [Tooltip("Половина угла конуса автоприцела, градусы")]
        public float autoAimAngle = 35f;
    }
}

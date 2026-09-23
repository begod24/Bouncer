using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Enemy Definition", fileName = "Enemy_")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Неваляшка";

        [Header("Живучесть: убивается серией попаданий")]
        [Min(1)] public int hitsToKill = 3;
        [Tooltip("Если за это время не было нового попадания — серия сбрасывается")]
        public float comboResetTime = 3f;

        [Header("Движение")]
        public float moveSpeed = 3.2f;
        [Tooltip("Максимальное ускорение, м/с²")]
        public float acceleration = 14f;
        public float stopDistance = 0.9f;
        public float repathInterval = 0.25f;

        [Header("Атака: замах → бросок телом → восстановление")]
        public float attackRange = 1.7f;
        public float windupTime = 0.45f;
        [Tooltip("Как сильно откидывается назад на замахе")]
        public float windupLean = 6f;
        public float lungeSpeed = 6.5f;
        public float lungeTip = 3f;
        public float lungeTime = 0.35f;
        public float lungeHitRange = 1.35f;
        public float recoverTime = 0.6f;
        public float attackCooldown = 1.4f;
        [Min(0)] public int contactDamage = 1;
        public float attackKnockback = 9f;

        [Header("Тело неваляшки")]
        public float mass = 4f;
        [Tooltip("Центр масс ниже центра нижней сферы — поэтому она сама встаёт")]
        public float centerOfMassHeight = 0.2f;
        [Tooltip("Удержание равновесия, пока не оглушена")]
        public float uprightStrength = 60f;
        public float uprightDamping = 8f;
        public float yawStrength = 25f;
        public float yawDamping = 6f;
        [Tooltip("Покачивание при ходьбе")]
        public float waddleStrength = 5f;
        public float waddleFrequency = 8f;

        [Header("Реакция на попадание")]
        [Tooltip("Доля отброса, которая подбрасывает вверх")]
        public float hitLift = 0.25f;
        [Tooltip("Сколько рад/с опрокидывания даёт единица отброса")]
        public float tipPerImpulse = 0.6f;
        public float stunTime = 0.9f;
        [Tooltip("При каком наклоне (градусы) считается, что уже встала")]
        public float recoverTilt = 12f;
        public float recoverAngularSpeed = 2f;
        [Tooltip("С какой скорости оглушённая неваляшка сбивает соседку")]
        public float chainStunSpeed = 4f;
        public float chainStunTime = 0.6f;

        [Header("Смерть")]
        public float debrisForce = 5f;
    }
}

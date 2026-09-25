using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Fizruk Definition", fileName = "Enemy_Fizruk")]
    public sealed class FizrukDefinition : ScriptableObject
    {
        public string displayName = "Физрук-манекен";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 30;

        [Header("Движение: он босс — ходит всегда, на взгляд не замирает")]
        public float moveSpeed = 3f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 160f;

        [Header("Удар планшетом вблизи: замах → удар → восстановление")]
        public float attackRange = 2.6f;
        public float windupTime = 0.75f;
        public float smashTime = 0.2f;
        public float smashRange = 3.2f;
        [Range(0f, 180f)] public float smashHalfAngle = 80f;
        public float recoverTime = 0.8f;
        public float attackCooldown = 1.6f;
        [Min(0)] public int smashDamage = 1;
        public float smashKnockback = 15f;

        [Header("Сильный мяч издалека")]
        [Tooltip("Бросает, если игрок дальше этого, м")]
        public float throwMinRange = 6f;
        public float throwMaxRange = 20f;
        public float throwWindup = 0.55f;
        public float throwCooldown = 4.5f;
        public float ballSpeed = 18f;
        public float ballGravity = 2f;
        [Min(0)] public int ballDamage = 1;
        public float ballKnockback = 8f;

        [Header("Свисток: правило раунда")]
        [Tooltip("Первый свисток — через столько секунд после выхода")]
        public float firstWhistle = 5f;
        [Tooltip("Пауза между свистками, пока жизней больше половины")]
        public float whistleInterval = 12f;
        [Tooltip("Пауза между свистками, когда жизней меньше половины")]
        public float angryWhistleInterval = 9f;
        [Range(0f, 1f)] public float angryAt = 0.5f;
        [Tooltip("Сколько Физрук набирает воздуха перед свистком (поза: свисток у рта)")]
        public float whistleTime = 0.6f;

        [Header("«Замри!»")]
        [Tooltip("Сколько секунд все стоят")]
        public float freezeTime = 3f;
        [Tooltip("Первые секунды после свистка — время замереть, без наказания")]
        public float freezeGrace = 0.6f;
        [Tooltip("Кто бежит быстрее этого, м/с, тот получает сильный мяч")]
        public float freezeMoveSpeed = 1.6f;
        public float freezePunishCooldown = 0.8f;

        [Header("«Штрафной!»")]
        [Min(1)] public int penaltyBalls = 5;
        [Tooltip("Веер: угол между крайними мячами, градусы")]
        public float penaltySpread = 56f;
        public float penaltyWindup = 0.8f;
        public float penaltySpeed = 14f;

        [Header("«Мяч в игре!»")]
        [Tooltip("Сколько секунд висит надпись; сам мяч катается по своему таймеру")]
        public float ballInPlayTime = 8f;

        [Header("Смерть")]
        public float debrisForce = 5f;
    }
}

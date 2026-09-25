using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Mannequin Definition", fileName = "Enemy_Mannequin")]
    public sealed class MannequinDefinition : ScriptableObject
    {
        public string displayName = "Манекен";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 3;

        [Header("Подкрадывается, пока на него не смотрят")]
        [Tooltip("Скорость, пока никто не смотрит")]
        public float sneakSpeed = 5.2f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 540f;
        [Tooltip("Сколько секунд вне взгляда, прежде чем снова пойти: быстрый взгляд через плечо его держит")]
        public float unfreezeDelay = 0.25f;
        [Tooltip("За сколько секунд манекен встаёт в новую позу, когда замирает")]
        public float poseSnapTime = 0.07f;

        [Header("Удар вблизи: замах → удар → восстановление")]
        public float attackRange = 1.4f;
        [Tooltip("Замах короткий: взгляд на манекен во время замаха его останавливает")]
        public float windupTime = 0.3f;
        public float strikeTime = 0.15f;
        public float strikeRange = 1.8f;
        [Range(0f, 180f)] public float strikeHalfAngle = 70f;
        public float recoverTime = 0.5f;
        public float attackCooldown = 1.1f;
        [Min(0)] public int damage = 1;
        public float knockback = 9f;

        [Header("Попадание")]
        [Tooltip("Сколько секунд идущий манекен шатается после попадания. Замерший — не шатается")]
        public float staggerTime = 0.35f;
        [Tooltip("Какая доля отброса мяча достаётся манекену")]
        public float knockbackScale = 0.2f;
        public float debrisForce = 4f;

        [Header("Элитный: бросок в спину")]
        [Tooltip("Бросает сильный мяч в спину, если на него не смотрят и игрок дальше этого, м. 0 — не бросает")]
        [Min(0f)] public float backThrowMinRange;
        public float backThrowMaxRange = 15f;
        [Tooltip("Замах перед броском: посмотришь — замрёт и не бросит")]
        public float backThrowWindup = 0.45f;
        public float backThrowCooldown = 3.5f;
        public float ballSpeed = 17f;
        public float ballGravity = 2f;
        [Min(0)] public int ballDamage = 1;
        public float ballKnockback = 7f;
    }
}

using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Shadow Definition", fileName = "Enemy_Shadow")]
    public sealed class ShadowDefinition : ScriptableObject
    {
        public string displayName = "Тень";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;

        [Header("Движение: плывёт над землёй")]
        public float moveSpeed = 4.2f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 360f;
        [Tooltip("Сколько секунд проявляется из темноты после появления (неуязвима, не бьёт)")]
        public float emergeTime = 0.8f;

        [Header("Удар: замах (тень плотнеет) → удар → восстановление")]
        public float attackRange = 1.5f;
        [Tooltip("Замах: на это время тень твёрдая даже в темноте — окно, чтобы попасть")]
        public float windupTime = 0.5f;
        public float strikeTime = 0.15f;
        public float strikeRange = 1.9f;
        [Range(0f, 180f)] public float strikeHalfAngle = 75f;
        public float recoverTime = 0.45f;
        public float attackCooldown = 1.2f;
        [Min(0)] public int damage = 1;
        public float knockback = 8f;

        [Header("Попадание")]
        [Tooltip("Какая доля отброса мяча достаётся тени")]
        public float knockbackScale = 0.3f;
        [Tooltip("Попадание сбивает замах")]
        public float staggerTime = 0.4f;

        [Header("Элитная: гасит фонари")]
        [Tooltip("Гасит фонарь, подлетев к краю его круга ближе этого, м. 0 — не гасит")]
        [Min(0f)] public float putOutReach;
        public float putOutTime = 6f;
        public float putOutCooldown = 9f;
    }
}

using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Bear Definition", fileName = "Enemy_Bear")]
    public sealed class BearDefinition : ScriptableObject
    {
        public string displayName = "Плюшевый мишка";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 5;

        [Header("Движение")]
        public float moveSpeed = 2.2f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 240f;

        [Header("Удар лапой: замах → удар → восстановление")]
        public float attackRange = 1.6f;
        public float windupTime = 0.55f;
        public float swipeTime = 0.18f;
        public float swipeRange = 2f;
        [Tooltip("Половина угла, в котором лапа достаёт, градусы")]
        [Range(0f, 180f)] public float swipeHalfAngle = 70f;
        public float recoverTime = 0.7f;
        public float attackCooldown = 1.6f;
        [Min(0)] public int damage = 1;
        public float knockback = 10f;

        [Header("Застрявшие мячи")]
        [Tooltip("Сколько мячей помещается в живот. Следующее попадание выбивает застрявший")]
        [Min(1)] public int maxStuckBalls = 1;
        [Tooltip("С какой скоростью выбитый мяч отлетает: в сторону и вверх")]
        public Vector2 knockOutVelocity = new(4f, 4.5f);
        [Tooltip("Мишка-моряк: через столько секунд выплёвывает застрявший мяч в игрока. 0 — не выплёвывает")]
        [Min(0f)] public float spitDelay;
        public float spitSpeed = 13f;
        public float spitGravity = 3f;
        [Min(0)] public int spitDamage = 1;

        [Header("Попадание и смерть")]
        [Tooltip("Какая доля отброса мяча достаётся мишке — он тяжёлый")]
        public float knockbackScale = 0.12f;
        [Tooltip("Сколько секунд мишка качается после попадания и не бьёт")]
        public float staggerTime = 0.3f;
        public float debrisForce = 3f;
    }
}

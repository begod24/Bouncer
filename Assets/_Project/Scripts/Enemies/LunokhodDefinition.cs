using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Lunokhod Definition", fileName = "Enemy_Lunokhod")]
    public sealed class LunokhodDefinition : ScriptableObject
    {
        public string displayName = "Луноход";

        [Header("Живучесть")]
        [Tooltip("Попаданий при открытой крышке; при закрытой каждое считается за половину")]
        [Min(1)] public int hitsToKill = 5;

        [Header("Движение: медленный, держит дистанцию")]
        public float moveSpeed = 1.3f;
        [Tooltip("Градусов в секунду: разворачивается медленно")]
        public float turnSpeed = 55f;
        public float preferredDistance = 12f;
        [Tooltip("Ближе этого — отъезжает")]
        public float minDistance = 8f;

        [Header("Залп ёжиками")]
        public float fireInterval = 4.5f;
        public float firstFireDelay = 3f;
        [Tooltip("Крышка поднимается — предупреждение")]
        public float lidOpenTime = 0.55f;
        [Min(1)] public int volleyCount = 3;
        [Tooltip("Веер: угол между ёжиками, градусы")]
        public float volleySpread = 18f;
        public float ballSpeed = 10f;
        public float ballGravity = 7f;
        [Min(0)] public int ballDamage = 1;
        public float ballKnockback = 6f;
        [Tooltip("Упреждение: 0 — в игрока, 1 — куда он добежит")]
        [Range(0f, 1f)] public float lead = 0.5f;
        [Tooltip("После залпа крышка ещё открыта столько секунд — окно для бросков")]
        public float openAfterVolley = 1.3f;
        public float lidCloseTime = 0.4f;
        public float maxFireRange = 18f;

        [Header("Попадание")]
        public float knockbackScale = 0.08f;

        [Header("Смерть")]
        public float debrisForce = 5f;
    }
}

using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Rocking Horse Definition", fileName = "Enemy_RockingHorse")]
    public sealed class RockingHorseDefinition : ScriptableObject
    {
        public string displayName = "Лошадка-качалка";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 3;

        [Header("Движение")]
        public float moveSpeed = 2.4f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 300f;

        [Header("Раскачка → таран → качается на месте")]
        [Tooltip("С какого расстояния до игрока начинает раскачиваться")]
        public float chargeRange = 8f;
        public float windupTime = 1.1f;
        [Tooltip("За сколько секунд до рывка перестаёт доворачивать на игрока")]
        public float aimLockTime = 0.25f;
        public float chargeSpeed = 12f;
        public float chargeTime = 0.9f;
        [Tooltip("После тарана качается на месте — окно для бросков")]
        public float recoverTime = 1.2f;
        public float chargeCooldown = 2.5f;
        public float hitDistance = 1.1f;
        [Min(0)] public int damage = 1;
        public float knockback = 13f;
        [Tooltip("Попадание во время раскачки сбивает разбег на столько секунд")]
        public float staggerTime = 0.6f;

        [Header("Качание")]
        public float rockAngle = 8f;
        public float rockFrequency = 2.2f;
        public float windupRockAngle = 22f;
        public float windupRockFrequency = 5f;

        [Header("Конь-огонь")]
        [Tooltip("Через сколько метров тарана остаётся огненное пятно (если у лошадки задан префаб огня)")]
        public float fireSpacing = 0.8f;

        [Header("Попадание и смерть")]
        [Tooltip("Какая доля отброса мяча достаётся лошадке")]
        public float knockbackScale = 0.15f;
        public float debrisForce = 4f;
    }
}

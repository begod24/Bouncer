using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Hare Definition", fileName = "Enemy_Hare")]
    public sealed class HareDefinition : ScriptableObject
    {
        public string displayName = "Большой плюшевый заяц";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 60;

        [Header("Скачет к игроку")]
        public float hopSpeed = 3.2f;
        public float turnSpeed = 180f;
        [Tooltip("Прыжков в секунду (только вид)")]
        public float hopRate = 1.6f;

        [Header("Прыжок с ударом")]
        public float slamCooldown = 5f;
        public float firstSlamDelay = 3f;
        [Tooltip("Приседает, на земле круг — предупреждение")]
        public float crouchTime = 0.7f;
        public float jumpTime = 1.1f;
        public float jumpHeight = 5f;
        public float slamRadius = 4f;
        [Min(0)] public int slamDamage = 1;
        public float slamKnockback = 14f;
        [Tooltip("После приземления сидит столько секунд — открыт")]
        public float stuckTime = 1.4f;
        [Tooltip("Облака ваты вокруг места приземления")]
        [Min(0)] public int cloudsPerSlam = 3;
        [Min(0)] public int cloudsPerSlamRipped = 5;

        [Header("Облака ваты")]
        public float cloudRadius = 2.2f;
        public float cloudLife = 5f;
        [Range(0.1f, 1f)] public float cloudSlow = 0.55f;

        [Header("Морковка-бумеранг")]
        public float carrotCooldown = 7f;
        public float carrotWindup = 0.5f;
        public float carrotRange = 13f;
        public float carrotFlightTime = 2.2f;
        [Min(0)] public int carrotDamage = 1;
        public float carrotKnockback = 8f;
        public float carrotRadius = 0.8f;

        [Header("Уши-хлыст вблизи")]
        public float whipRange = 3.2f;
        [Range(0f, 180f)] public float whipHalfAngle = 65f;
        public float whipWindup = 0.45f;
        public float whipCooldown = 2.5f;
        [Min(0)] public int whipDamage = 1;
        public float whipKnockback = 16f;

        [Header("Шов рвётся")]
        [Tooltip("Доля жизни, на которой рвётся шов")]
        [Range(0f, 1f)] public float ripAt = 0.5f;
        [Tooltip("После разрыва скачет быстрее и чаще прыгает")]
        public float rippedSpeedMultiplier = 1.3f;
        public float rippedCooldownMultiplier = 0.7f;
        [Tooltip("Разорванный теряет вату: облако позади раз в столько секунд")]
        public float trailCloudInterval = 3.5f;

        [Header("Смерть")]
        public float debrisForce = 7f;
    }
}

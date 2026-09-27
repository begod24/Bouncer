using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/RC Car Definition", fileName = "Enemy_RCCar")]
    public sealed class RCCarDefinition : ScriptableObject
    {
        public string displayName = "Машинка на пульте";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;

        [Header("Езда кругами")]
        public float cruiseSpeed = 5.5f;
        public float turnSpeed = 300f;
        [Tooltip("Кружит на таком расстоянии от игрока")]
        public float orbitDistance = 7f;

        [Header("Таран")]
        public float chargeRange = 12f;
        public float chargeCooldown = 1.6f;
        [Tooltip("Газует на месте — предупреждение")]
        public float revTime = 0.55f;
        public float chargeSpeed = 10f;
        public float chargeTime = 1.3f;
        [Tooltip("Занос после тарана")]
        public float skidTime = 0.55f;
        [Tooltip("Насколько разворачивает в заносе, градусы")]
        public float skidTurn = 160f;
        public float hitDistance = 0.9f;
        [Min(0)] public int damage = 1;
        public float knockback = 10f;

        [Header("Попадание")]
        [Tooltip("Мяч переворачивает машинку вверх колёсами на столько секунд")]
        public float flipTime = 1.3f;
        public float knockbackScale = 0.4f;

        [Header("Смерть")]
        public float debrisForce = 5f;
    }
}

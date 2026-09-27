using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Shield Soldier Definition", fileName = "Enemy_ShieldSoldier")]
    public sealed class ShieldSoldierDefinition : ScriptableObject
    {
        public string displayName = "Солдатик с крышкой";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 3;

        [Header("Движение")]
        public float moveSpeed = 2.4f;
        [Tooltip("Скорость поворота, градусов в секунду. Медленный — можно зайти сбоку")]
        public float turnSpeed = 110f;

        [Header("Крышка от кастрюли")]
        [Tooltip("Половина угла спереди, в котором крышка отбивает мячи, градусы")]
        public float guardHalfAngle = 65f;
        [Tooltip("Сильный мяч выбивает крышку в сторону на столько секунд: в это время бьёт любой мяч")]
        public float guardBrokenTime = 2.2f;

        [Header("Удар крышкой вблизи")]
        public float bashRange = 1.5f;
        [Tooltip("Замах половником — предупреждение")]
        public float bashWindup = 0.45f;
        public float bashTime = 0.25f;
        [Tooltip("Рывок вперёд во время удара, м/с")]
        public float bashLunge = 3.5f;
        public float bashCooldown = 1.8f;
        [Min(0)] public int bashDamage = 1;
        public float bashKnockback = 9f;

        [Header("Попадание")]
        public float staggerTime = 0.5f;
        [Tooltip("Какая доля отброса мяча достаётся солдатику")]
        public float knockbackScale = 0.25f;

        [Header("Смерть")]
        public float debrisForce = 4f;
    }
}

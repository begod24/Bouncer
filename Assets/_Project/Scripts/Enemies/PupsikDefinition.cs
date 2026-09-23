using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Pupsik Definition", fileName = "Enemy_Pupsik")]
    public sealed class PupsikDefinition : ScriptableObject
    {
        public string displayName = "Пупс";

        [Header("Живучесть")]
        [Tooltip("Выбитого пупса мяч пробивает насквозь и летит дальше")]
        [Min(1)] public int hitsToKill = 1;

        [Header("Движение")]
        public float moveSpeed = 5.2f;
        [Tooltip("Максимальное ускорение, м/с²")]
        public float acceleration = 40f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 720f;
        public float mass = 1f;
        [Tooltip("Ближе этого расстояния бежит прямо на игрока, если между ними нет препятствий")]
        public float directChaseDistance = 4f;

        [Header("Рой")]
        [Tooltip("На каком расстоянии пупсы расталкивают друг друга")]
        public float separationRadius = 0.9f;
        public float separationStrength = 3f;

        [Header("Прыжок-укус")]
        public float attackRange = 1.9f;
        [Tooltip("Присед перед прыжком — предупреждение")]
        public float windupTime = 0.22f;
        public float hopSpeed = 5.5f;
        public float hopUpSpeed = 2.6f;
        [Tooltip("На каком расстоянии в прыжке кусает")]
        public float biteRadius = 0.75f;
        public float recoverTime = 0.5f;
        public float attackCooldown = 1.3f;
        [Min(0)] public int contactDamage = 1;
        public float attackKnockback = 6f;

        [Header("Попадание и смерть")]
        [Tooltip("Какая доля отброса мяча достаётся живому пупсу (если он пережил удар)")]
        public float knockbackScale = 0.5f;
        public float debrisForce = 4f;
    }
}

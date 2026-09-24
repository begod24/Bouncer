using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Chick Definition", fileName = "Enemy_Chick")]
    public sealed class ChickDefinition : ScriptableObject
    {
        public string displayName = "Заводной цыплёнок";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 1;

        [Header("Завод и бег")]
        [Tooltip("Сколько секунд заводится перед бегом — предупреждение")]
        public float windupTime = 0.8f;
        public float runSpeed = 9f;
        [Tooltip("Столько бежит, потом взрывается сам")]
        public float maxRunTime = 3f;
        [Tooltip("Петушок: сколько раз за разбег поворачивает к игроку (зигзаг)")]
        [Min(0)] public int zigzags;
        public float zigzagInterval = 0.45f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 900f;
        public float keySpinSpeed = 1440f;
        public float mass = 0.6f;

        [Header("Взрыв")]
        public float blastRadius = 2.6f;
        [Tooltip("На каком расстоянии от игрока взрывается")]
        public float triggerDistance = 0.9f;
        [Min(0)] public int playerDamage = 1;
        [Tooltip("Урон врагам вокруг: взрыв задевает всех")]
        [Min(0)] public int enemyDamage = 2;
        public float blastKnockback = 11f;

        [Header("Попадание")]
        [Tooltip("Какая доля отброса мяча достаётся живому цыплёнку")]
        public float knockbackScale = 0.4f;
        public float debrisForce = 5f;
    }
}

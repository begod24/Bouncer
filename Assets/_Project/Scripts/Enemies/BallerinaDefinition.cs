using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Ballerina Definition", fileName = "Enemy_Ballerina")]
    public sealed class BallerinaDefinition : ScriptableObject
    {
        public string displayName = "Балерина из шкатулки";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 3;

        [Header("Движение")]
        [Tooltip("Скользит на подставке к игроку, пока кружится")]
        public float moveSpeed = 1.6f;
        public float turnSpeed = 200f;

        [Header("Пируэт: отражает мячи")]
        public float spinTime = 4.5f;
        [Tooltip("Градусов в секунду — только вид")]
        public float spinSpeed = 600f;
        [Tooltip("Мяч облетает её столько секунд и летит обратно в игрока")]
        public float reflectHold = 0.15f;
        public float reflectSpeed = 15f;
        public float reflectGravity = 3f;

        [Header("Реверанс: открыта")]
        public float restTime = 2.2f;

        [Header("Пачка задевает вблизи")]
        public float contactRange = 1.2f;
        [Min(0)] public int contactDamage = 1;
        public float contactKnockback = 8f;
        public float contactCooldown = 1.2f;

        [Header("Попадание")]
        [Tooltip("Сильный мяч сбивает пируэт: столько секунд качается, потом реверанс")]
        public float staggerTime = 0.6f;
        public float knockbackScale = 0.2f;

        [Header("Смерть")]
        public float debrisForce = 4f;
    }
}

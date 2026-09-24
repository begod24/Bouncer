using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Top Definition", fileName = "Enemy_Top")]
    public sealed class TopDefinition : ScriptableObject
    {
        public string displayName = "Юла";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;

        [Header("Движение: катится к игроку по дуге")]
        public float moveSpeed = 3.6f;
        [Tooltip("Максимальное ускорение, м/с²")]
        public float acceleration = 10f;
        [Tooltip("На сколько градусов путь отклоняется от прямой на игрока")]
        public float arcAngle = 40f;
        [Tooltip("За сколько секунд дуга качается туда и обратно")]
        public float arcPeriod = 3f;
        [Tooltip("Радиус тела юлы, м")]
        public float radius = 0.5f;

        [Header("Вид")]
        public float spinSpeed = 900f;
        [Tooltip("Наклон при вращении, градусы")]
        public float wobbleAngle = 6f;

        [Header("Касание игрока")]
        public float touchDistance = 0.95f;
        [Min(0)] public int damage = 1;
        public float knockback = 9f;
        public float touchCooldown = 1f;

        [Header("Бильярд: после попадания")]
        [Tooltip("С какой скоростью юла отлетает по направлению мяча")]
        public float launchSpeed = 15f;
        [Tooltip("Как быстро гаснет разгон")]
        public float launchDrag = 1.4f;
        [Tooltip("Ниже этой скорости снова катится к игроку")]
        public float launchEndSpeed = 4f;
        [Tooltip("Врагов на пути сбивает с таким уроном")]
        [Min(0)] public int bumpDamage = 1;
        public float bumpKnockback = 10f;
        [Tooltip("Какую долю скорости сохраняет при отскоке от стены")]
        [Range(0f, 1f)] public float wallBounceKeep = 0.85f;

        [Header("Смерть")]
        public float debrisForce = 4f;
    }
}

using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Tin Soldier Definition", fileName = "Enemy_TinSoldier")]
    public sealed class TinSoldierDefinition : ScriptableObject
    {
        public string displayName = "Оловянный солдатик";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;
        [Tooltip("Если за это время не было нового попадания — счётчик попаданий сбрасывается. 0 — не сбрасывается")]
        [Min(0f)] public float comboResetTime;

        [Header("Движение")]
        public float moveSpeed = 2.6f;
        [Tooltip("Скорость поворота, градусов в секунду")]
        public float turnSpeed = 360f;

        [Header("Строй")]
        [Tooltip("На каком расстоянии от игрока строй встаёт для залпа")]
        public float preferredDistance = 11f;
        [Tooltip("Расстояние между солдатиками в шеренге")]
        public float slotSpacing = 1.5f;
        [Tooltip("Как часто строй выбирает новую позицию, с")]
        public float regroupInterval = 2f;

        [Header("Залп")]
        [Tooltip("Замах всем строем перед бросками — предупреждение")]
        public float aimTime = 0.8f;
        [Tooltip("Пауза между бросками соседей по шеренге")]
        public float volleyStagger = 0.2f;
        [Tooltip("Пауза между залпами, с")]
        public float reloadTime = 3.5f;
        [Tooltip("Первый залп — не раньше, чем через столько секунд после появления")]
        public float firstVolleyDelay = 2.5f;
        [Tooltip("Дальше этого расстояния залп не начинается")]
        public float maxThrowRange = 17f;

        [Header("Мяч (его можно поймать)")]
        public float ballSpeed = 14f;
        public float ballGravity = 3f;
        [Min(0)] public int damage = 1;
        public float knockback = 5f;
        [Tooltip("Упреждение: 0 — в текущую позицию игрока, 1 — полное")]
        [Range(0f, 1f)] public float lead = 0.6f;

        [Header("Попадание")]
        [Tooltip("Сколько секунд солдатик качается после попадания и не стреляет")]
        public float staggerTime = 0.6f;
        [Tooltip("Какая доля отброса мяча достаётся солдатику")]
        public float knockbackScale = 0.25f;

        [Header("Смерть")]
        public float debrisForce = 4f;
        [Tooltip("Выбитый солдатик роняет мяч из руки — его можно подобрать")]
        public bool dropBallOnDeath = true;
    }
}

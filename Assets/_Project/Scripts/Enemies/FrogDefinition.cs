using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Frog Definition", fileName = "Enemy_Frog")]
    public sealed class FrogDefinition : ScriptableObject
    {
        public string displayName = "Заводная лягушка";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;

        [Header("Прыжок")]
        [Tooltip("Прыжок не короче и не длиннее, м (ближе игрока — прыгает прямо на него)")]
        public float jumpRangeMin = 2.5f;
        public float jumpRangeMax = 5.5f;
        public float jumpHeight = 1.6f;
        public float jumpTime = 0.65f;
        [Tooltip("Присела перед прыжком — на земле уже виден круг, куда она упадёт")]
        public float crouchTime = 0.4f;
        [Tooltip("Пауза на земле между прыжками")]
        public float sitTime = 0.35f;
        [Tooltip("Упреждение: 0 — прыгает туда, где игрок сейчас, 1 — куда он добежит")]
        [Range(0f, 1f)] public float lead = 0.5f;
        public float turnSpeed = 540f;

        [Header("Приземление")]
        public float landRadius = 1.6f;
        [Min(0)] public int landDamage = 1;
        public float landKnockback = 9f;

        [Header("Завод")]
        [Tooltip("Столько прыжков на одном заводе, потом садится подзавестись")]
        [Min(1)] public int jumpsPerWind = 3;
        [Tooltip("Заводится столько секунд — окно для бросков")]
        public float rewindTime = 1.6f;

        [Header("Попадание")]
        [Tooltip("Сбитая в прыжке падает на спину и лежит столько секунд")]
        public float fallenTime = 1.1f;
        public float staggerTime = 0.4f;
        public float knockbackScale = 0.3f;

        [Header("Смерть")]
        public float debrisForce = 4f;
    }
}

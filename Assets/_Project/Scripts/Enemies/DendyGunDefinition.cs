using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Dendy Gun Definition", fileName = "Enemy_DendyGun")]
    public sealed class DendyGunDefinition : ScriptableObject
    {
        public string displayName = "Пистолет от «Денди»";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;

        [Header("Прыжки на рукоятке")]
        public float hopTime = 0.38f;
        public float hopDistance = 1.2f;
        public float hopHeight = 0.35f;
        public float hopPause = 0.12f;
        [Tooltip("Держится на таком расстоянии от игрока")]
        public float preferredDistance = 11f;
        [Tooltip("Ближе этого — отпрыгивает")]
        public float minDistance = 7f;
        public float turnSpeed = 360f;

        [Header("Лазер и выстрел")]
        public float aimCooldown = 2.8f;
        public float firstAimDelay = 2f;
        [Tooltip("Лазер виден столько секунд до выстрела")]
        public float aimTime = 1.15f;
        [Tooltip("Первые столько секунд лазер ещё ведёт игрока, потом замирает")]
        public float trackTime = 0.75f;
        public float shotRange = 24f;
        [Tooltip("Насколько широкая полоса выстрела, м")]
        public float shotWidth = 0.55f;
        [Min(0)] public int damage = 1;
        public float knockback = 7f;
        [Tooltip("Отдача: отпрыгивает назад после выстрела")]
        public float recoilDistance = 1f;

        [Header("Попадание")]
        public float staggerTime = 0.45f;
        public float knockbackScale = 0.3f;

        [Header("Смерть")]
        public float debrisForce = 3.5f;
    }
}

using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Cry Doll Definition", fileName = "Enemy_CryDoll")]
    public sealed class CryDollDefinition : ScriptableObject
    {
        public string displayName = "Кукла-плакса";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 3;

        [Header("Движение")]
        public float moveSpeed = 1.7f;
        public float turnSpeed = 240f;
        [Tooltip("Подходит к игроку на такое расстояние и плачет")]
        public float preferredDistance = 5.5f;

        [Header("Плач")]
        public float cryInterval = 7f;
        public float firstCryDelay = 3f;
        public float cryTime = 2.2f;
        [Tooltip("Крик слышно в этом радиусе: игрок в нём бежит медленнее")]
        public float cryRadius = 6.5f;
        [Range(0.1f, 1f)] public float slowMultiplier = 0.6f;
        [Tooltip("Волна крика по земле раз в столько секунд")]
        public float waveInterval = 0.55f;

        [Header("Зовёт пупсов")]
        [Tooltip("Каждый такой плач зовёт пупсов")]
        [Min(1)] public int summonEvery = 2;
        [Min(1)] public int summonCount = 3;
        [Tooltip("Если пупсов рядом уже столько — не зовёт")]
        [Min(1)] public int maxPupsiksNear = 6;
        public float pupsikCountRadius = 12f;

        [Header("Попадание")]
        public float staggerTime = 0.5f;
        public float knockbackScale = 0.3f;

        [Header("Смерть")]
        public float debrisForce = 4f;
    }
}

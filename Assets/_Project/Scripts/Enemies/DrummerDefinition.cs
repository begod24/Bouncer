using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Drummer Definition", fileName = "Enemy_Drummer")]
    public sealed class DrummerDefinition : ScriptableObject
    {
        public string displayName = "Пионер-барабанщик";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 2;

        [Header("Движение: держится позади своих")]
        public float moveSpeed = 2.2f;
        public float turnSpeed = 240f;
        [Tooltip("На каком расстоянии от игрока держится, если своих рядом нет")]
        public float preferredDistance = 11f;
        [Tooltip("Ближе этого к игроку — отступает")]
        public float keepAwayDistance = 7f;
        [Tooltip("Насколько позади своей кучки встаёт (от игрока), м")]
        public float behindPack = 2.5f;
        [Tooltip("Свои дальше этого не считаются кучкой")]
        public float packRadius = 14f;

        [Header("Барабан")]
        [Tooltip("Пауза между ударами, с")]
        public float beatInterval = 0.42f;
        [Tooltip("Каждый такой удар — кольцо по земле и звук")]
        [Min(1)] public int ringEveryBeats = 4;
        [Tooltip("Под барабан бегают свои в этом радиусе")]
        public float auraRadius = 7.5f;
        [Tooltip("Во сколько раз быстрее бегают соседи под барабан")]
        public float auraBoost = 1.3f;

        [Header("Попадание")]
        [Tooltip("Сбившийся барабанщик молчит столько секунд")]
        public float staggerTime = 0.8f;
        public float knockbackScale = 0.3f;

        [Header("Смерть")]
        public float debrisForce = 4f;
    }
}

using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Crow Definition", fileName = "Enemy_Crow")]
    public sealed class CrowDefinition : ScriptableObject
    {
        public string displayName = "Ворона";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 1;

        [Header("Полёт: кружит высоко над игроком")]
        public float flySpeed = 9f;
        [Tooltip("Как быстро меняет курс, м/с²")]
        public float acceleration = 30f;
        public float circleHeight = 5f;
        public float circleRadius = 7f;
        [Tooltip("Скорость кружения, градусов в секунду")]
        public float circleSpeed = 70f;
        [Tooltip("Сколько кружит перед следующим заходом: от и до, с")]
        public Vector2 circleTime = new(1f, 2.5f);
        [Tooltip("Ниже этого её достаёт мяч и видит автоприцел, м")]
        public float hittableHeight = 2.4f;

        [Header("Пике: по прямой через игрока, полоса на асфальте мигает заранее")]
        public float markTime = 0.7f;
        public float diveSpeed = 15f;
        [Tooltip("На какой высоте проносится над асфальтом, м")]
        public float diveHeight = 1.1f;
        [Tooltip("Насколько пролетает дальше игрока, м")]
        public float diveOvershoot = 5f;
        [Tooltip("Сколько заходов делает, потом улетает")]
        [Min(1)] public int dives = 2;
        [Min(0)] public int damage = 1;
        public float knockback = 8f;
        [Tooltip("Задевает игрока ближе этого, м")]
        public float hitRadius = 0.8f;

        [Header("Кража мячей: хватает с земли и несёт в мешок босса")]
        [Range(0f, 1f)] public float stealChance = 0.35f;
        [Tooltip("Берёт мяч не ближе этого к игроку, м")]
        public float stealMinDistance = 3f;
        public float carryHeight = 3.5f;

        [Header("Жизнь")]
        [Tooltip("Через столько секунд стая улетает сама")]
        public float lifetime = 20f;
    }
}

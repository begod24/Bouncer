using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Scarecrow Definition", fileName = "Enemy_Scarecrow")]
    public sealed class ScarecrowDefinition : ScriptableObject
    {
        public string displayName = "Чучело";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 3;

        [Header("Прыгает на своём шесте, держит дистанцию")]
        [Tooltip("На каком расстоянии от игрока старается стоять, м")]
        public float keepDistance = 7f;
        [Tooltip("Пауза между прыжками, с")]
        public float hopInterval = 0.9f;
        public float hopTime = 0.35f;
        [Tooltip("Длина прыжка, м")]
        public float hopDistance = 1.2f;
        public float hopHeight = 0.35f;
        [Tooltip("Скорость поворота к игроку, градусов в секунду: медленная — чучело можно обойти сбоку")]
        public float turnSpeed = 110f;

        [Header("Ловит мячи спереди")]
        [Tooltip("Ловит мяч, пролетающий ближе этого к груди, м")]
        public float catchRadius = 1.6f;
        [Tooltip("Половина угла сектора спереди, в котором руки ловят мяч, градусы")]
        [Range(0f, 180f)] public float catchHalfAngle = 65f;
        [Tooltip("Сколько мячей помещается в руках")]
        [Min(1)] public int maxHeld = 1;
        [Tooltip("Заряженный мяч тоже ловит (элитное). Обычное чучело заряженный мяч пробивает")]
        public bool catchCharged;

        [Header("Бросает обратно")]
        [Tooltip("Через сколько секунд после ловли бросает мяч обратно")]
        public float holdTime = 1f;
        public float throwSpeed = 14f;
        public float throwGravity = 3f;
        [Min(0)] public int throwDamage = 1;
        public float throwKnockback = 6f;
        [Tooltip("Бросает сильный мяч: удержит только идеальная ловля (элитное)")]
        public bool throwStrong;
        [Tooltip("Упреждение броска: 0 — в игрока, 1 — полное")]
        [Range(0f, 1f)] public float lead = 0.5f;

        [Header("Попадание и смерть")]
        [Tooltip("Какая доля отброса мяча достаётся чучелу")]
        public float knockbackScale = 0.15f;
        public float debrisForce = 4f;
    }
}

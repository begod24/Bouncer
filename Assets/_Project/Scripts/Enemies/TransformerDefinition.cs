using UnityEngine;

namespace Bouncer.Enemies
{
    [CreateAssetMenu(menuName = "Bouncer/Transformer Definition", fileName = "Enemy_Transformer")]
    public sealed class TransformerDefinition : ScriptableObject
    {
        public string displayName = "Трансформер из ларька";

        [Header("Живучесть")]
        [Min(1)] public int hitsToKill = 34;

        [Header("Машина: носится и таранит")]
        public float driveSpeed = 7f;
        [Tooltip("Носится столько секунд между таранами")]
        public float driveTime = 2.2f;
        [Tooltip("Газует на месте, на земле полоса тарана — предупреждение")]
        public float revTime = 0.75f;
        public float ramSpeed = 14f;
        public float ramTime = 1.6f;
        [Min(0)] public int ramDamage = 1;
        public float ramKnockback = 14f;
        [Tooltip("Прилавки и горы коробок в этом радиусе перед капотом сносятся")]
        public float breakRadius = 1.9f;
        public float breakForce = 8f;
        [Tooltip("Врезался в стену — стоит оглушённый (открыт)")]
        public float crashStun = 1.4f;
        [Min(1)] public int ramsPerCar = 3;

        [Header("Зовёт машинки на пульте (в начале каждой фазы машины)")]
        [Min(0)] public int rcCars = 2;
        [Min(0)] public int maxRcCars = 3;

        [Header("Превращение (открыт)")]
        public float transformTime = 1.3f;

        [Header("Робот: ходит, стреляет, кидает батарейки")]
        public float walkSpeed = 2.2f;
        public float turnSpeed = 140f;
        public float preferredDistance = 9f;
        [Tooltip("Сколько секунд робот, потом снова машина")]
        public float robotTime = 11f;
        public float actionPause = 1.4f;

        [Header("Залп из пушки")]
        [Min(1)] public int volleyCount = 5;
        public float volleySpread = 50f;
        public float volleyWindup = 0.6f;
        public float ballSpeed = 14f;
        public float ballGravity = 3f;
        [Min(0)] public int ballDamage = 1;
        public float ballKnockback = 7f;
        [Tooltip("После залпа открыт столько секунд")]
        public float openAfterVolley = 1f;

        [Header("Батарейки-мины")]
        [Min(1)] public int mines = 3;
        public float mineWindup = 0.5f;
        public float mineFlightTime = 0.9f;
        [Tooltip("Лежит и искрит столько секунд, потом взрывается")]
        public float mineFuse = 1.3f;
        public float mineRadius = 1.6f;
        [Min(0)] public int mineDamage = 1;
        public float mineKnockback = 10f;
        [Tooltip("Батарейки падают вокруг игрока на таком расстоянии")]
        public float mineScatter = 2.4f;

        [Header("Смерть")]
        public float debrisForce = 8f;
    }
}

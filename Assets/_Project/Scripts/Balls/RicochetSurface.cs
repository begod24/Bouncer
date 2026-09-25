using UnityEngine;

namespace Bouncer.Balls
{
    /// <summary>
    /// Стенка с «идеальным» рикошетом — борта хоккейной коробки: летящий мяч отражается без потери скорости,
    /// не опускается ниже, а такой рикошет не тратит лимит рикошетов мяча. Мяч игрока после рикошета
    /// доворачивает на врага, если тот почти на линии отражения. Вешается на коллайдер стенки или его родителя.
    /// </summary>
    public sealed class RicochetSurface : MonoBehaviour
    {
        [Tooltip("Какую долю скорости мяч сохраняет после рикошета. У обычной стены — wallSpeedKeep мяча (0.9)")]
        [SerializeField, Range(0.5f, 1.2f)] float speedKeep = 1f;
        [Tooltip("Рикошет не тратит лимит рикошетов мяча")]
        [SerializeField] bool free = true;
        [Tooltip("После рикошета мяч не идёт вниз: летит дальше на той же высоте")]
        [SerializeField] bool keepHeight = true;
        [Tooltip("Мяч игрока после рикошета доворачивает на врага в пределах этого угла от отражения, градусы. 0 — нет")]
        [SerializeField, Range(0f, 45f)] float aimAssist = 12f;

        public float SpeedKeep => speedKeep;
        public bool Free => free;
        public bool KeepHeight => keepHeight;
        public float AimAssist => aimAssist;
    }
}

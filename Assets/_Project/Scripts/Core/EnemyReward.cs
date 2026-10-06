using UnityEngine;

namespace Bouncer.Core
{
    [DisallowMultipleComponent]
    public sealed class EnemyReward : MonoBehaviour
    {
        [Tooltip("Сколько монеток падает, в тиынах: мелочь раскладывается по номиналам (1 тенге = 20, пятак = 5, тиын = 1)")]
        [SerializeField, Min(0)] int coins = 1;
        [Tooltip("С какой вероятностью монетки вообще падают: пупсов много, с каждого не надо")]
        [SerializeField, Range(0f, 1f)] float chance = 1f;
        [Tooltip("Роняет портфель: кто подобрал, выбирает 1 карточку из 3 (элитные враги)")]
        [SerializeField] bool portfolio;

        public int Coins => coins;
        public float Chance => chance;
        public bool Portfolio => portfolio;
    }
}

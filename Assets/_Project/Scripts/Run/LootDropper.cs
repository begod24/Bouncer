using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Добыча с выбитых врагов: монетки по <see cref="EnemyReward"/> (мелочь раскладывается по номиналам:
    /// тенге, пятаки, тиыны) и портфель у элитных. Врагов, исчезнувших в конце арены, не трогает.
    /// </summary>
    public sealed class LootDropper : MonoBehaviour
    {
        [Header("Монетки: префаб и сколько тиынов стоит")]
        [SerializeField] CoinPickup coinTenge;
        [SerializeField] CoinPickup coinFive;
        [SerializeField] CoinPickup coinOne;
        [SerializeField] PortfolioPickup portfolio;

        void OnEnable() => GameEvents.EnemyKilled += OnEnemyKilled;

        void OnDisable() => GameEvents.EnemyKilled -= OnEnemyKilled;

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (enemy == null || hit.Has(HitFlags.Despawn) || !enemy.TryGetComponent(out EnemyReward reward))
                return;
            Vector3 at = enemy.transform.position;
            at.y = Mathf.Max(at.y, 0.2f);
            if (reward.Coins > 0 && Random.value <= reward.Chance)
                DropCoins(reward.Coins, at);
            if (reward.Portfolio)
                DropPortfolio(at);
        }

        /// <summary>Рассыпать столько тиынов: крупными монетами, сколько получится, остаток мелочью.</summary>
        public void DropCoins(int amount, Vector3 at)
        {
            amount = Drop(coinTenge, amount, at);
            amount = Drop(coinFive, amount, at);
            Drop(coinOne, amount, at);
        }

        public PortfolioPickup DropPortfolio(Vector3 at) =>
            portfolio ? PoolService.Spawn(portfolio, at, Quaternion.identity) : null;

        /// <summary>Положить портфель на землю (находка на арене).</summary>
        public void PlacePortfolio(Vector3 at)
        {
            var pickup = DropPortfolio(at);
            if (pickup)
                pickup.PlaceOnGround();
        }

        static int Drop(CoinPickup prefab, int amount, Vector3 at)
        {
            if (prefab == null || prefab.Value <= 0)
                return amount;
            while (amount >= prefab.Value)
            {
                PoolService.Spawn(prefab, at, Quaternion.identity);
                amount -= prefab.Value;
            }
            return amount;
        }
    }
}

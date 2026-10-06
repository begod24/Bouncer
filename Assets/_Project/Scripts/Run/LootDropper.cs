using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Добыча с выбитых врагов: монетки по <see cref="EnemyReward"/> (мелочь раскладывается по номиналам:
    /// тенге, пятаки, тиыны) и портфель у элитных — но не больше <see cref="ElitePortfolioLimit"/> за арену
    /// (остальные элитки дают только монетки). Находка на арене — кучка монеток или лимонад.
    /// На 4-й опасности монеток меньше. Врагов, исчезнувших в конце арены, не трогает.
    /// По сети добычу роняет только хозяин комнаты, портфель за арену — один на всю команду.
    /// </summary>
    public sealed class LootDropper : MonoBehaviour
    {
        [Header("Монетки: префаб и сколько тиынов стоит")]
        [SerializeField] CoinPickup coinTenge;
        [SerializeField] CoinPickup coinFive;
        [SerializeField] CoinPickup coinOne;
        [SerializeField] PortfolioPickup portfolio;

        int _elitePortfolios;

        /// <summary>Префаб портфеля (по сети у гостя — для копий портфелей хозяина).</summary>
        public PortfolioPickup PortfolioPrefab => portfolio;

        /// <summary>Сколько портфелей за арену дают элитки. Задаёт арена.</summary>
        public int ElitePortfolioLimit { get; set; } = 1;

        void OnEnable() => GameEvents.EnemyKilled += OnEnemyKilled;

        void OnDisable() => GameEvents.EnemyKilled -= OnEnemyKilled;

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (enemy == null || hit.Has(HitFlags.Despawn) || NetHooks.IsGuest || !enemy.TryGetComponent(out EnemyReward reward))
                return;
            Vector3 at = enemy.transform.position;
            at.y = Mathf.Max(at.y, 0.2f);
            // Опасность урезает монетки: с мелочи — реже, с крупных — меньше.
            float multiplier = Danger.CoinMultiplier;
            bool small = reward.Coins <= 2;
            float chance = reward.Chance * (small ? multiplier : 1f);
            int coins = small ? reward.Coins : Mathf.Max(1, Mathf.RoundToInt(reward.Coins * multiplier));
            if (coins > 0 && Random.value <= chance)
                DropCoins(coins, at);
            if (reward.Portfolio && _elitePortfolios < ElitePortfolioLimit)
            {
                _elitePortfolios++;
                DropPortfolio(at);
            }
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

        /// <summary>Положить портфель на землю.</summary>
        public void PlacePortfolio(Vector3 at)
        {
            var pickup = DropPortfolio(at);
            if (pickup)
                pickup.PlaceOnGround();
        }

        /// <summary>Находка на арене: кучка монеток или лимонад — пополам.</summary>
        public void PlaceFind(Vector3 at, HealPickup lemonade, int coins)
        {
            if (lemonade && Random.value < 0.5f)
            {
                PoolService.Spawn(lemonade, at, Quaternion.identity);
                return;
            }
            DropCoins(Mathf.Max(1, Mathf.RoundToInt(coins * Danger.CoinMultiplier)), at + Vector3.up * 0.3f);
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

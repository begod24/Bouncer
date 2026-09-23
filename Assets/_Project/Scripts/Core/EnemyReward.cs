using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>Что игрок получает за выбитого врага. Читается из события GameEvents.EnemyKilled.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyReward : MonoBehaviour
    {
        [Tooltip("Опыт за выбитого врага")]
        [SerializeField, Min(0)] int experience = 1;

        public int Experience => experience;
    }
}

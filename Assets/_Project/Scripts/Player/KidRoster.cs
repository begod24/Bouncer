using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>Все дети, из которых выбирают перед прогулкой, в порядке экрана выбора.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Kid Roster", fileName = "KidRoster")]
    public sealed class KidRoster : ScriptableObject
    {
        [SerializeField] KidDefinition[] kids;

        public int Count => kids != null ? kids.Length : 0;

        /// <summary>Ребёнок по номеру; номер за пределами списка прижимается к краю.</summary>
        public KidDefinition this[int index] => Count == 0 ? null : kids[Clamp(index)];

        public int Clamp(int index) => Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
    }
}

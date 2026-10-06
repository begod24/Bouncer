using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Kid Roster", fileName = "KidRoster")]
    public sealed class KidRoster : ScriptableObject
    {
        [SerializeField] KidDefinition[] kids;

        public int Count => kids != null ? kids.Length : 0;

        public KidDefinition this[int index] => Count == 0 ? null : kids[Clamp(index)];

        public int Clamp(int index) => Mathf.Clamp(index, 0, Mathf.Max(0, Count - 1));
    }
}

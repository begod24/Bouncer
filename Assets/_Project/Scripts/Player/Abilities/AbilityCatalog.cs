using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Ability Catalog", fileName = "AbilityCatalog")]
    public sealed class AbilityCatalog : ScriptableObject
    {
        [Tooltip("Все умения: по сети умение — номер в этом списке")]
        public List<AbilityDefinition> abilities = new();

        public int IndexOf(AbilityDefinition ability) => ability ? abilities.IndexOf(ability) : -1;

        public AbilityDefinition At(int index) => index >= 0 && index < abilities.Count ? abilities[index] : null;
    }
}

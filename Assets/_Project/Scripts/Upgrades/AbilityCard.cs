using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Ability Card", fileName = "Card_Ability_")]
    public sealed class AbilityCard : UpgradeCard
    {
        [Tooltip("Умение на кнопку Q / LB. У ребёнка одно умение: новое заменяет старое")]
        public AbilityDefinition ability;

        public override bool CanOffer(PlayerController player, int stacks) =>
            ability && base.CanOffer(player, stacks) && player.Modifiers.Ability != ability;

        public override void Apply(PlayerController player)
        {
            player.Modifiers.Ability = ability;
            player.Modifiers.NotifyChanged();
        }
    }
}

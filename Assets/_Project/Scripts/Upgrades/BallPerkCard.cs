using Bouncer.Balls;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>Модификатор мяча (бумеранг, раскол, резинка): складывается со свойствами любого типа мяча.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Ball Perk Card", fileName = "Card_Perk_")]
    public sealed class BallPerkCard : UpgradeCard
    {
        public BallPerks perks;

        public override void Apply(PlayerController player)
        {
            var mods = player.Modifiers;
            mods.Perks = BallPerks.Combine(mods.Perks, perks);
            mods.NotifyChanged();
        }
    }
}

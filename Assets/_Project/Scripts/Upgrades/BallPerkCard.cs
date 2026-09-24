using Bouncer.Balls;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>
    /// Модификатор мяча (бумеранг, раскол, резинка): складывается со свойствами любого типа мяча.
    /// Может действовать не на все броски, а только на следующий после удачной ловли (горячая картошка).
    /// </summary>
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Ball Perk Card", fileName = "Card_Perk_")]
    public sealed class BallPerkCard : UpgradeCard
    {
        public BallPerks perks;
        [Tooltip("Действует только на следующий бросок после удачной ловли")]
        public bool nextThrowAfterCatch;

        public override void Apply(PlayerController player)
        {
            var mods = player.Modifiers;
            if (nextThrowAfterCatch)
            {
                mods.CatchPerks = BallPerks.Combine(mods.CatchPerks, perks);
                mods.HasCatchPerks = true;
            }
            else
            {
                mods.Perks = BallPerks.Combine(mods.Perks, perks);
            }
            mods.NotifyChanged();
        }
    }
}

using Bouncer.Balls;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Ball Type Card", fileName = "Card_Ball_")]
    public sealed class BallTypeCard : UpgradeCard
    {
        [Tooltip("Каким мячом игрок бросает после выбора")]
        public Ball ballPrefab;

        public override bool CanOffer(PlayerController player, int stacks) =>
            ballPrefab && player.Balls.BallPrefab != ballPrefab;

        public override void Apply(PlayerController player) => player.Balls.SetBallPrefab(ballPrefab);
    }
}

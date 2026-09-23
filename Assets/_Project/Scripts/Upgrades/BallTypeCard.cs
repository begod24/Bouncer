using Bouncer.Balls;
using Bouncer.Player;
using UnityEngine;

namespace Bouncer.Upgrades
{
    /// <summary>Другой мяч: все следующие броски — им. Свойства мяча лежат в его BallDefinition.</summary>
    [CreateAssetMenu(menuName = "Bouncer/Upgrades/Ball Type Card", fileName = "Card_Ball_")]
    public sealed class BallTypeCard : UpgradeCard
    {
        [Tooltip("Каким мячом игрок бросает после выбора")]
        public Ball ballPrefab;

        // К мячу можно вернуться, сменив его на другой, поэтому смотрим не на число взятий, а на мяч в руках.
        public override bool CanOffer(PlayerController player, int stacks) =>
            ballPrefab && player.Balls.BallPrefab != ballPrefab;

        public override void Apply(PlayerController player) => player.Balls.SetBallPrefab(ballPrefab);
    }
}

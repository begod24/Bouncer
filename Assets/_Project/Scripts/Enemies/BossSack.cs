using Bouncer.Balls;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Мешок за спиной «Того, кто в сумерках» — свой коллайдер сзади: мяч, попавший в него, бьёт босса
    /// и высыпает собранные мячи (<see cref="DuskBoss.OnSackContact"/>).
    /// </summary>
    public sealed class BossSack : MonoBehaviour, IBallTarget
    {
        [SerializeField] DuskBoss boss;

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit) =>
            boss ? boss.OnSackContact(ball, hit) : BallContactResult.PassThrough;
    }
}

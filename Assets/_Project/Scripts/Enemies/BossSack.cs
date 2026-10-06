using Bouncer.Balls;
using UnityEngine;

namespace Bouncer.Enemies
{
    public sealed class BossSack : MonoBehaviour, IBallTarget
    {
        [SerializeField] DuskBoss boss;

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit) =>
            boss ? boss.OnSackContact(ball, hit) : BallContactResult.PassThrough;
    }
}

using UnityEngine;

namespace Bouncer.Player
{
    public struct PlayerIntent
    {
        public Vector3 Move;
        public Vector3 Aim;
        public bool AimFromPointer;
        public bool UsingGamepad;

        public bool ThrowHeld;
        public bool ThrowPressed;
        public bool ThrowReleased;
        public bool CatchPressed;
        public bool DashPressed;
        public bool PausePressed;
        public bool InteractPressed;
        public bool InteractHeld;
        public bool BackpackPressed;
        public bool AbilityPressed;
    }

    public interface IPlayerIntentSource
    {
        PlayerIntent ReadIntent();
    }
}

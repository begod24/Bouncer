using UnityEngine;

namespace Bouncer.Player
{
    public struct PlayerActionState
    {
        public bool Charging;
        public float Charge01;
        public bool Catching;
        public bool Dashing;
        public bool Sliding;
        public Vector3 DashDirection;
        public bool Down;
        public bool Flashlight;
    }

    public enum PlayerCue : byte
    {
        Throw,
        Caught,
        Hurt,
    }
}

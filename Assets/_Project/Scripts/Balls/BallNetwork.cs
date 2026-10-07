using UnityEngine;

namespace Bouncer.Balls
{
    public interface IBallNetwork
    {
        bool IsAuthority { get; }

        Ball Throw(Ball prefab, in BallThrow t);

        void Take(Ball puppet);

        void Drop(Ball puppet, Vector3 position, Vector3 velocity);

        bool Summon(Ball puppet, GameObject taker);

        bool GiveToRemote(Ball ball, GameObject player);

        void Carry(Ball puppet, Vector3 center, Quaternion turn);
    }
}

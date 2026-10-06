using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Enemies
{
    public interface IGroupMember
    {
        void OnGroupSpawned(IReadOnlyList<GameObject> group, int index);
    }
}

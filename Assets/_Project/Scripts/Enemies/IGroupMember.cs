using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>Враг, который действует сообща с теми, кто появился вместе с ним (строй солдатиков).</summary>
    public interface IGroupMember
    {
        /// <summary>
        /// Вызывается у каждого члена группы, когда появилась вся группа.
        /// group — все, кто появился вместе, по порядку; index — место этого врага в группе.
        /// </summary>
        void OnGroupSpawned(IReadOnlyList<GameObject> group, int index);
    }
}

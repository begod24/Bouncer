using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Предмет арены, который можно снести: прилавок барахолки под тараном Трансформера, гора коробок.
    /// Сносит не мяч, а тот, кто знает про этот интерфейс (босс, сильный мяч — решает сам предмет).
    /// </summary>
    public interface IBreakable
    {
        bool IsBroken { get; }

        /// <summary>Снести: куски разлетаются в direction с силой force.</summary>
        void Break(Vector3 direction, float force);
    }
}

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

    /// <summary>
    /// Что-то на арене снесли (по сети хозяин комнаты сносит то же самое у гостей — предмет ищется по месту).
    /// </summary>
    public static class BreakEvents
    {
        public static event System.Action<Component, Vector3, float> Broken;

        /// <summary>Предмет снесён (зовёт сам предмет). У гостя не сообщается — снос пришёл от хозяина.</summary>
        public static void Raise(Component prop, Vector3 direction, float force)
        {
            if (!NetHooks.IsGuest)
                Broken?.Invoke(prop, direction, force);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Broken = null;
    }
}

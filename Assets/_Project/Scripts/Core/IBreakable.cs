using UnityEngine;

namespace Bouncer.Core
{
    public interface IBreakable
    {
        bool IsBroken { get; }

        void Break(Vector3 direction, float force);
    }

    public static class BreakEvents
    {
        public static event System.Action<Component, Vector3, float> Broken;

        public static void Raise(Component prop, Vector3 direction, float force)
        {
            if (!NetHooks.IsGuest)
                Broken?.Invoke(prop, direction, force);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Broken = null;
    }
}

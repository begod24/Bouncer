using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Rewind", fileName = "Ability_Rewind")]
    public sealed class RewindAbility : AbilityDefinition
    {
        [Tooltip("На сколько секунд назад")]
        public float seconds = 3f;
        [Tooltip("Сколько длится сама перемотка, с")]
        public float duration = 0.35f;
        [Tooltip("Неуязвимость после перемотки, с")]
        public float invulnerable = 0.4f;

        public override bool Prepare(ref AbilityCast cast)
        {
            if (cast.Caster == null || !cast.Caster.TryGetComponent(out PlayerRewind rewind)
                || !rewind.TryFindBack(seconds, out Vector3 destination))
                return false;
            cast.Target = destination;
            return true;
        }

        public override void Cast(in AbilityCast cast)
        {
            if (cast.Caster == null || !cast.Caster.TryGetComponent(out PlayerRewind rewind))
                return;
            if (cast.IsOwner)
            {
                rewind.Begin(duration, invulnerable);
                RewindScreen.Play(duration + 0.25f);
            }
            else
            {
                rewind.ShowRemote(cast.Origin, cast.Target, duration);
            }
        }
    }

    public static class RewindScreen
    {
        public static float Until { get; private set; }
        public static float Length { get; private set; } = 1f;

        public static void Play(float seconds)
        {
            Length = Mathf.Max(0.05f, seconds);
            Until = Time.unscaledTime + Length;
        }

        public static float Strength01 => Mathf.Clamp01((Until - Time.unscaledTime) / Length);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Until = 0f;
    }
}

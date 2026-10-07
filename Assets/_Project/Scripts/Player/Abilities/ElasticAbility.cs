using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Elastic", fileName = "Ability_Elastic")]
    public sealed class ElasticAbility : AbilityDefinition
    {
        [SerializeField] ElasticBand band;
        [Tooltip("Товарищ должен быть ближе этого, м")]
        public float range = 14f;
        public float duration = 5f;
        [Tooltip("Споткнувшийся враг стоит столько секунд")]
        public float trip = 1f;

        public override bool Prepare(ref AbilityCast cast)
        {
            PlayerController best = null;
            float bestSqr = range * range;
            foreach (var other in Players.All)
            {
                if (other == cast.Caster || other.IsDead || other.IsHome)
                    continue;
                float sqr = Flat(other.transform.position - cast.Origin).sqrMagnitude;
                if (sqr < bestSqr && sqr > 1f)
                {
                    bestSqr = sqr;
                    best = other;
                }
            }
            if (best == null)
                return false;
            cast.Param = best.Slot;
            return true;
        }

        public override void Cast(in AbilityCast cast)
        {
            var mate = Players.InSlot(cast.Param);
            if (band == null || cast.Caster == null || mate == null || mate == cast.Caster)
                return;
            PoolService.Spawn(band, cast.Origin, Quaternion.identity).Play(cast.Caster.transform, mate.transform, duration, trip,
                cast.IsAuthority);
        }
    }
}

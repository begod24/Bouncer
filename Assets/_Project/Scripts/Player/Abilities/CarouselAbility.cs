using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Carousel", fileName = "Ability_Carousel")]
    public sealed class CarouselAbility : AbilityDefinition
    {
        [Tooltip("Сколько секунд кружишься")]
        public float duration = 1.4f;
        [Tooltip("Чужие мячи ближе этого ловятся сами, м")]
        public float catchRadius = 2.3f;
        [Tooltip("Через сколько секунд мячи из рук разлетаются кольцом")]
        public float throwAt = 0.35f;

        public override void Cast(in AbilityCast cast)
        {
            if (cast.Caster != null && cast.Caster.TryGetComponent(out PlayerSpin spin))
                spin.Begin(duration, catchRadius, throwAt, cast.IsOwner);
        }
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Fence", fileName = "Ability_Fence")]
    public sealed class FenceAbility : AbilityDefinition
    {
        [SerializeField] FenceWall fence;
        [Tooltip("Сколько секунд стоит забор")]
        [Min(0.5f)] public float duration = 4f;
        [Tooltip("Насколько впереди ребёнка встаёт забор, м")]
        public float distance = 1.6f;
        [Tooltip("«Баррикада»: отбитый мяч становится твоим броском")]
        public bool redirect;

        public override void Cast(in AbilityCast cast)
        {
            if (fence == null)
                return;
            Vector3 forward = Flat(cast.Direction).normalized;
            Vector3 position = cast.Origin + forward * distance;
            position.y = cast.Origin.y;
            var wall = PoolService.Spawn(fence, position, Quaternion.LookRotation(forward));
            wall.Play(cast.Caster, duration, redirect);
        }
    }
}

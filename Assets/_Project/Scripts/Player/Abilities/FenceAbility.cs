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
        [Tooltip("«Штаб»: отбитый мяч летит заряженным во врага")]
        public bool redirect;
        [Tooltip("«Штаб»: забор буквой П — спереди и по бокам ребёнка")]
        public bool fort;
        [Tooltip("«Штаб»: боковые стенки на таком расстоянии от ребёнка, м")]
        public float sideDistance = 1.75f;

        public override void Cast(in AbilityCast cast)
        {
            if (fence == null)
                return;
            Vector3 forward = Flat(cast.Direction).normalized;
            Vector3 position = cast.Origin + forward * distance;
            position.y = cast.Origin.y;
            var wall = PoolService.Spawn(fence, position, Quaternion.LookRotation(forward));
            wall.Play(cast.Caster, duration, redirect);
            if (!fort)
                return;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 at = cast.Origin + right * (side * sideDistance) + forward * (distance * 0.45f);
                at.y = cast.Origin.y;
                PoolService.Spawn(fence, at, Quaternion.LookRotation(right * side)).Play(cast.Caster, duration, redirect);
            }
        }
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Soap Bubbles", fileName = "Ability_Bubbles")]
    public sealed class BubblesAbility : AbilityDefinition
    {
        [SerializeField] SoapBubble bubble;
        [Min(1)] public int count = 7;
        [Tooltip("Веер пузырей, градусы")]
        public float spread = 55f;
        public Vector2 speed = new(3.5f, 6f);
        [Tooltip("Сколько секунд живёт пузырь")]
        public float life = 4f;
        [Tooltip("Сколько секунд мяч сидит в пузыре, прежде чем пузырь лопнет")]
        public float hold = 1.5f;
        [Tooltip("Пузыри вылетают не разом: за столько секунд")]
        public float blowTime = 0.5f;

        public override void Cast(in AbilityCast cast)
        {
            if (bubble == null || cast.Caster == null)
                return;
            GameEvents.PlaySound(SoundCue.BubbleBlow, cast.Origin);
            var blower = AbilityRunner.On(cast.Caster.gameObject);
            var random = new System.Random(cast.Seed);
            Vector3 forward = Flat(cast.Direction).normalized;
            bool authority = cast.IsAuthority;
            for (int i = 0; i < count; i++)
            {
                float angle = ((float)random.NextDouble() - 0.5f) * spread;
                float speedValue = Mathf.Lerp(speed.x, speed.y, (float)random.NextDouble());
                float delay = blowTime * i / Mathf.Max(1, count - 1);
                Vector3 velocity = Quaternion.Euler(0f, angle, 0f) * forward * speedValue;
                var caster = cast.Caster.transform;
                blower.After(delay, () =>
                {
                    if (caster == null)
                        return;
                    Vector3 mouth = caster.position + Vector3.up * 1.15f + Flat(caster.forward) * 0.5f;
                    PoolService.Spawn(bubble, mouth, Quaternion.identity).Play(velocity, life, hold, authority);
                });
            }
        }
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Walkie Talkie", fileName = "Ability_Walkie")]
    public sealed class WalkieAbility : AbilityDefinition
    {
        [Tooltip("Цель ищется не дальше этого, м")]
        public float range = 18f;
        [Tooltip("Половина угла поиска по прицелу, градусы")]
        public float halfAngle = 30f;
        public float duration = 6f;
        [Tooltip("Прибавка к урону мячей команды по цели")]
        public int bonus = 1;

        [Header("Эффекты")]
        [SerializeField] AbilityProp walkie;
        [SerializeField] TargetMarkView markView;

        public override bool Prepare(ref AbilityCast cast)
        {
            var aimed = cast.Caster != null ? cast.Caster.Aim.Target : null;
            Targetable best = aimed != null && aimed.Team == Team.Enemy && aimed.IsAlive ? aimed : null;
            if (best == null)
            {
                float bestScore = float.PositiveInfinity;
                foreach (var target in Targetable.All)
                {
                    if (target.Team != Team.Enemy || !target.IsAlive)
                        continue;
                    Vector3 to = Flat(target.Position - cast.Origin);
                    float distance = to.magnitude;
                    float angle = Vector3.Angle(cast.Direction, to);
                    if (distance > range || angle > halfAngle)
                        continue;
                    float score = distance * (1f + angle / halfAngle);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = target;
                    }
                }
            }
            if (best == null)
                return false;
            cast.Target = best.Position;
            return true;
        }

        public override void Cast(in AbilityCast cast)
        {
            GameEvents.PlaySound(SoundCue.RadioCrackle, cast.Origin);
            if (cast.Caster)
                AbilityProp.Show(walkie, cast.Caster.transform, new Vector3(0.28f, 1.55f, 0.05f), Quaternion.identity);
            Targetable best = null;
            float bestSqr = 4f;
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                float sqr = Flat(target.Position - cast.Target).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = target;
                }
            }
            if (best == null)
                return;
            TargetMark.Mark(best, duration, bonus);
            if (markView)
                PoolService.Spawn(markView, best.Position, Quaternion.identity).Play(best, duration);
        }
    }
}

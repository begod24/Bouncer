using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Magnet", fileName = "Ability_Magnet")]
    public sealed class MagnetAbility : AbilityDefinition
    {
        static readonly List<Ball> s_found = new();

        [Tooltip("Ничьи мячи ближе этого летят к тебе, м")]
        public float radius = 9f;
        [Tooltip("Сколько мячей сверх свободных рук можно притянуть (лишние упадут у ног)")]
        public int extra = 2;

        [Header("Эффекты")]
        [SerializeField] AbilityProp magnet;
        [Tooltip("Сжимающееся кольцо, из пула")]
        [SerializeField] ExpandingRing pull;
        [Tooltip("Красно-синие искры у ног, из пула")]
        [SerializeField] ParticleBurst sparks;

        public override void Cast(in AbilityCast cast)
        {
            var caster = cast.Caster;
            GameEvents.PlaySound(SoundCue.MagnetPull, cast.Origin);
            if (caster)
                AbilityProp.Show(magnet, caster.transform, new Vector3(0f, 2.35f, 0f), Quaternion.identity);
            if (pull)
                PoolService.Spawn(pull, cast.Origin + Vector3.up * 0.05f, Quaternion.identity).Play(radius);
            if (sparks)
                PoolService.Spawn(sparks, cast.Origin + Vector3.up * 0.3f, Quaternion.identity);
            if (!cast.IsOwner || caster == null)
                return;

            s_found.Clear();
            var balls = Ball.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball.State != BallState.Loose || Flat(ball.Position - cast.Origin).sqrMagnitude > radius * radius)
                    continue;
                s_found.Add(ball);
            }
            Vector3 origin = cast.Origin;
            s_found.Sort((a, b) => Flat(a.Position - origin).sqrMagnitude.CompareTo(Flat(b.Position - origin).sqrMagnitude));
            int room = Mathf.Max(0, caster.Balls.MaxBalls - caster.Balls.Balls) + extra;
            for (int i = 0; i < s_found.Count && room > 0; i++)
                if (s_found[i].Summon(caster.gameObject))
                    room--;
            s_found.Clear();
        }
    }
}

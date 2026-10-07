using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [CreateAssetMenu(menuName = "Bouncer/Abilities/Cap Gun", fileName = "Ability_CapGun")]
    public sealed class CapGunAbility : AbilityDefinition
    {
        static readonly Collider[] s_hits = new Collider[32];

        [Header("Хлопок")]
        public float range = 4.5f;
        [Tooltip("Половина угла конуса, градусы")]
        public float halfAngle = 38f;
        [Tooltip("На сколько секунд враги вздрагивают")]
        public float stun = 0.7f;
        public float knockback = 6f;

        [Header("Эффекты")]
        [SerializeField] AbilityProp gun;
        [Tooltip("Дым и бумажные пистоны, из пула")]
        [SerializeField] ParticleBurst smoke;
        [Tooltip("Меловое «БАХ!» над головой")]
        [SerializeField] BillboardPopup bang;
        [Tooltip("Дуга конуса на асфальте, из пула")]
        [SerializeField] ExpandingRing cone;

        public override void Cast(in AbilityCast cast)
        {
            var caster = cast.Caster;
            Vector3 forward = Flat(cast.Direction).normalized;
            Vector3 hand = cast.Origin + Vector3.up * 1.05f + forward * 0.55f;
            GameEvents.PlaySound(SoundCue.CapGun, hand);
            GameFeel.Shake(0.35f);
            if (caster)
                AbilityProp.Show(gun, caster.transform, new Vector3(0.18f, 1.05f, 0.55f), Quaternion.identity);
            if (smoke)
                PoolService.Spawn(smoke, hand, Quaternion.LookRotation(forward));
            BillboardPopup.Show(bang, cast.Origin + Vector3.up * 2.3f, PlayerCardFx.PopupBang, new Color(1f, 0.9f, 0.5f));
            if (cone)
            {
                var ring = PoolService.Spawn(cone, cast.Origin + Vector3.up * 0.05f, Quaternion.LookRotation(forward));
                if (ring.TryGetComponent(out CircleLine line))
                    line.Arc = halfAngle * 2f;
                ring.Play(range);
            }
            if (!cast.IsOwner || caster == null)
                return;

            int count = Physics.OverlapSphereNonAlloc(cast.Origin + Vector3.up * 0.6f, range, s_hits, Layers.EnemyMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var other = s_hits[i];
                var target = other.GetComponentInParent<Targetable>();
                if (target == null || !target.IsAlive || !InCone(cast.Origin, forward, target.Position))
                    continue;
                if (other.GetComponentInParent<IDamageable>() is { } damageable)
                {
                    Vector3 away = Flat(target.Position - cast.Origin);
                    NetHooks.ApplyHit(damageable, new HitInfo
                    {
                        Damage = 0,
                        Point = other.ClosestPoint(hand),
                        Direction = away.sqrMagnitude > 1e-4f ? away.normalized : forward,
                        Force = knockback,
                        SourceTeam = Team.Player,
                        Source = caster.gameObject,
                        Flags = HitFlags.Tackle,
                    });
                }
                target.Freeze(stun);
            }
            var balls = Ball.Active;
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                if (ball.State != BallState.Live || !ball.Team.IsHostileTo(Team.Player) || !InCone(cast.Origin, forward, ball.Position))
                    continue;
                ball.Drop(ball.Position, Flat(ball.Velocity) * 0.1f + Vector3.up * 2f);
            }
        }

        bool InCone(Vector3 origin, Vector3 forward, Vector3 point)
        {
            Vector3 to = Flat(point - origin);
            float distance = to.magnitude;
            return distance <= range && (distance < 0.8f || Vector3.Angle(forward, to) <= halfAngle);
        }
    }
}

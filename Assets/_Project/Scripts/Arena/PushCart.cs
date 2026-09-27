using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Arena
{
    /// <summary>
    /// Тележка с товаром на барахолке. Мяч от неё отскакивает, как от стены, и толкает её по ходу полёта
    /// (заряженный — сильнее); игрок толкает её телом. Разогнавшаяся тележка сбивает врагов на пути: удар и
    /// отброс, по каждому — не чаще раза в секунду. Физика — обычный Rigidbody, крен запрещён.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PushCart : MonoBehaviour, IBallTarget
    {
        [Tooltip("Скорость, которую даёт обычный мяч, м/с (заряженный — вдвое)")]
        [SerializeField] float ballPush = 5f;
        [SerializeField] float chargedMultiplier = 2f;
        [Tooltip("С такой скорости тележка сбивает врагов")]
        [SerializeField] float hitSpeed = 2.5f;
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] float knockback = 9f;

        Rigidbody _body;
        float _lastHitTime = -10f;
        Collider _lastHitCollider;

        void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            Vector3 push = ball.Velocity;
            push.y = 0f;
            if (push.sqrMagnitude < 1e-4f)
                push = -hit.normal;
            push.y = 0f;
            float speed = ballPush * (ball.Stats.Has(HitFlags.Charged) ? chargedMultiplier : 1f);
            _body.AddForce(push.normalized * speed, ForceMode.VelocityChange);
            GameEvents.PlaySound(SoundCue.BallWall, hit.point);
            return BallContactResult.Bounce;
        }

        void OnCollisionEnter(Collision collision)
        {
            Vector3 velocity = _body.linearVelocity;
            velocity.y = 0f;
            if (velocity.magnitude < hitSpeed)
                return;
            if (collision.collider == _lastHitCollider && Time.time - _lastHitTime < 1f)
                return;
            var target = collision.collider.GetComponentInParent<Targetable>();
            if (target == null || target.Team != Team.Enemy || !target.IsAlive
                || !target.TryGetComponent(out IDamageable damageable))
                return;
            _lastHitCollider = collision.collider;
            _lastHitTime = Time.time;
            damageable.ApplyHit(new HitInfo
            {
                Damage = damage,
                Point = collision.GetContact(0).point,
                Direction = velocity.normalized,
                Force = knockback,
                SourceTeam = Team.Player,
                Source = gameObject,
                Flags = HitFlags.Area,
            });
            GameEvents.PlaySound(SoundCue.AreaThud, target.Position);
        }
    }
}

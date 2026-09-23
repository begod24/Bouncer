using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Arena
{
    /// <summary>
    /// Качели: отбивают летящий мяч обратно — быстрее и сильнее, чем он прилетел.
    /// Отбивает зона между стойками (коллайдер на слое Environment), стойки работают как обычная стена.
    /// Сиденье качается само, а от удара мяча раскачивается сильнее (маятник, только визуал).
    /// </summary>
    public sealed class SwingSet : MonoBehaviour, IBallTarget
    {
        [Tooltip("Коллайдер между стойками: мяч, попавший в него, отбивается")]
        [SerializeField] Collider batZone;
        [Tooltip("Сиденье с верёвками: пивот на перекладине, качается вокруг локальной оси X")]
        [SerializeField] Transform seat;

        [Header("Отбивание")]
        [Tooltip("Во сколько раз быстрее летит отбитый мяч")]
        [SerializeField, Min(1f)] float speedMultiplier = 1.35f;
        [SerializeField] float minSpeed = 18f;
        [SerializeField] float maxSpeed = 40f;
        [Tooltip("Прибавка к урону отбитого мяча")]
        [SerializeField, Min(0)] int bonusDamage = 1;
        [SerializeField, Min(0f)] float knockbackMultiplier = 1.3f;
        [Tooltip("Вертикальная скорость отбитого мяча: летит примерно на уровне груди")]
        [SerializeField] float upVelocity = 1.2f;
        [Tooltip("Какую часть скорости вдоль качелей мяч сохраняет. 0 — летит строго поперёк качелей")]
        [SerializeField, Range(0f, 1f)] float sidewaysKeep = 0.6f;
        [Tooltip("Сколько секунд после отбивания качели пропускают тот же мяч")]
        [SerializeField] float sameBallCooldown = 0.3f;

        [Header("Сиденье")]
        [Tooltip("Покачивание без мяча, градусы")]
        [SerializeField] float idleAmplitude = 6f;
        [SerializeField, Min(0.1f)] float idlePeriod = 2.8f;
        [Tooltip("Сколько градусов в секунду добавляет удар мяча")]
        [SerializeField] float kickPerHit = 140f;
        [SerializeField] float maxSwingAngle = 50f;
        [Tooltip("Длина подвеса, м: от неё зависит период раскачки")]
        [SerializeField, Min(0.3f)] float ropeLength = 2f;
        [SerializeField, Min(0f)] float swingDamping = 0.5f;

        float _angle;
        float _angularVelocity;
        float _idlePhase;
        Ball _lastBall;
        float _lastBallUntil;

        void Awake() => _idlePhase = Random.value * Mathf.PI * 2f;

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (hit.collider != batZone)
                return BallContactResult.Bounce;
            if (ball == _lastBall && Time.time < _lastBallUntil)
                return BallContactResult.PassThrough;

            Vector3 normal = Flat(transform.forward).normalized;
            Vector3 incoming = Flat(ball.Velocity);
            float along = Vector3.Dot(incoming, normal);
            Vector3 sideways = incoming - normal * along;
            // Мяч отлетает на ту сторону, с которой прилетел, в основном поперёк качелей.
            float side = Vector3.Dot(ball.Position - transform.position, normal) >= 0f ? 1f : -1f;
            float across = Mathf.Max(Mathf.Abs(along), incoming.magnitude * 0.5f, 1f);
            Vector3 direction = (normal * (side * across) + sideways * sidewaysKeep).normalized;
            float speed = Mathf.Clamp(incoming.magnitude * speedMultiplier, minSpeed, maxSpeed);

            var stats = ball.Stats;
            stats.Speed = speed;
            stats.UpVelocity = upVelocity;
            stats.Damage += bonusDamage;
            stats.Knockback *= knockbackMultiplier;
            stats.Flags |= HitFlags.Charged;
            ball.Redirect(direction * speed + Vector3.up * upVelocity, stats);

            _lastBall = ball;
            _lastBallUntil = Time.time + sameBallCooldown;
            // Положительный угол уводит сиденье к -Z, поэтому толкаем против знака скорости мяча вдоль Z.
            _angularVelocity -= (along >= 0f ? 1f : -1f) * kickPerHit;
            GameFeel.HitStop(0.03f);
            GameFeel.Shake(0.2f);
            GameEvents.PlaySound(SoundCue.SwingBat, hit.point);
            return BallContactResult.Redirected;
        }

        void Update()
        {
            if (!seat)
                return;
            float dt = Time.deltaTime;
            // Маятник: θ'' = -(g / L)·sin θ, плюс затухание. Угол храним в градусах.
            float restoring = -(9.81f / ropeLength) * Mathf.Sin(_angle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
            _angularVelocity += (restoring - swingDamping * _angularVelocity) * dt;
            _angle = Mathf.Clamp(_angle + _angularVelocity * dt, -maxSwingAngle, maxSwingAngle);
            _idlePhase += dt * Mathf.PI * 2f / idlePeriod;
            seat.localRotation = Quaternion.Euler(_angle + Mathf.Sin(_idlePhase) * idleAmplitude, 0f, 0f);
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

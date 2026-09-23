using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Тестовая пушка: бросает в игрока мячи, которые можно поймать. Нужна, чтобы тренировать
    /// ловлю до появления бросающих врагов. Прообраз оловянного солдатика.
    /// </summary>
    public sealed class BallLauncher : MonoBehaviour
    {
        [SerializeField] Ball ballPrefab;
        [SerializeField] Transform muzzle;
        [Tooltip("Поворачивается к цели")]
        [SerializeField] Transform turret;
        [Tooltip("Растёт перед выстрелом — предупреждение")]
        [SerializeField] Renderer telegraph;

        [SerializeField] bool firing = true;
        [SerializeField] float interval = 3f;
        [SerializeField] float windup = 0.6f;
        [SerializeField] float speed = 15f;
        [SerializeField] float gravity = 3f;
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] float knockback = 5f;
        [Tooltip("Упреждение: 0 — в текущую позицию, 1 — полное")]
        [SerializeField, Range(0f, 1f)] float lead = 0.8f;
        [SerializeField] float maxRange = 28f;

        float _timer;

        public bool Firing
        {
            get => firing;
            set
            {
                firing = value;
                _timer = 0f;
            }
        }

        public float Interval
        {
            get => interval;
            set => interval = Mathf.Max(0.3f, value);
        }

        public float Speed
        {
            get => speed;
            set => speed = Mathf.Max(3f, value);
        }

        void Update()
        {
            var target = firing && GameSession.IsGameplayActive
                ? Targetable.FindNearest(muzzle.position, Team.Player, maxRange)
                : null;
            if (target == null)
            {
                _timer = 0f;
                SetTelegraph(0f);
                return;
            }

            Vector3 to = target.Position - transform.position;
            to.y = 0f;
            if (turret && to.sqrMagnitude > 0.01f)
                turret.rotation = Quaternion.Slerp(turret.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-10f * Time.deltaTime));

            _timer += Time.deltaTime;
            float untilFire = interval - _timer;
            SetTelegraph(untilFire <= windup ? 1f - untilFire / Mathf.Max(0.01f, windup) : 0f);
            if (_timer >= interval)
            {
                _timer = 0f;
                Fire(target);
            }
        }

        void SetTelegraph(float amount)
        {
            if (!telegraph)
                return;
            telegraph.enabled = amount > 0f;
            telegraph.transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 0.5f, amount);
        }

        void Fire(Targetable target)
        {
            Vector3 origin = muzzle.position;
            Vector3 aim = target.AimPoint;
            Vector3 flat = aim - origin;
            flat.y = 0f;
            Vector3 velocity = target.Velocity;
            velocity.y = 0f;
            aim += velocity * (flat.magnitude / speed * lead);

            flat = aim - origin;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (distance < 1f)
                return;

            // Подбираем вертикальную скорость так, чтобы мяч прилетел на уровень груди.
            float time = distance / speed;
            float upVelocity = (aim.y - origin.y + 0.5f * gravity * time * time) / time;

            var ball = PoolService.Spawn(ballPrefab, origin, Quaternion.identity);
            ball.Launch(new BallThrow
            {
                Origin = origin,
                Direction = flat / distance,
                Team = Team.Enemy,
                Thrower = gameObject,
                Stats = new ThrowStats
                {
                    Speed = speed,
                    UpVelocity = upVelocity,
                    Gravity = gravity,
                    Damage = damage,
                    Knockback = knockback,
                },
            });
        }
    }
}

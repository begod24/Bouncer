using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class FenceWall : MonoBehaviour, IPoolable, IBallTarget, IBallShield
    {
        [Tooltip("Доски забора: вырастают из асфальта по одной")]
        [SerializeField] Transform[] planks;
        [Tooltip("Меловой контур: сначала рисуется он, потом растут доски")]
        [SerializeField] LineRenderer outline;
        [Tooltip("Пыль, когда доски вылезают из асфальта")]
        [SerializeField] ParticleSystem riseDust;
        [Tooltip("Щепки от попадания, из пула")]
        [SerializeField] ParticleBurst splinters;
        [SerializeField, Min(0.01f)] float outlineTime = 0.12f;
        [SerializeField, Min(0.01f)] float riseTime = 0.18f;
        [Tooltip("Задержка между соседними досками, с")]
        [SerializeField, Min(0f)] float stagger = 0.03f;
        [SerializeField, Min(0.01f)] float sinkTime = 0.3f;
        [Tooltip("Насколько глубоко доска прячется в асфальт, м")]
        [SerializeField] float depth = 1.6f;
        [Tooltip("«Баррикада»: отбитый мяч летит вперёд с такой скоростью (не меньше)")]
        [SerializeField] float redirectSpeed = 22f;

        BoxCollider _collider;
        Vector3[] _rest;
        float[] _wobble;
        float _start;
        float _life;
        bool _redirect;
        PlayerController _caster;
        bool _risen;

        float Age => Time.time - _start;
        bool Standing => Age >= outlineTime && Age < _life - sinkTime * 0.5f;

        void Awake()
        {
            _collider = GetComponent<BoxCollider>();
            _rest = new Vector3[planks.Length];
            _wobble = new float[planks.Length];
            for (int i = 0; i < planks.Length; i++)
                _rest[i] = planks[i].localPosition;
        }

        public void OnSpawned()
        {
            _start = Time.time;
            _risen = false;
            _collider.enabled = false;
            for (int i = 0; i < planks.Length; i++)
            {
                _wobble[i] = 0f;
                planks[i].localPosition = _rest[i] + Vector3.down * depth;
                planks[i].localRotation = Quaternion.identity;
            }
        }

        public void OnDespawned() => _caster = null;

        public void Play(PlayerController caster, float life, bool redirect)
        {
            _caster = caster;
            _life = life;
            _redirect = redirect;
            _start = Time.time;
            GameEvents.PlaySound(SoundCue.FenceRise, transform.position);
        }

        void Update()
        {
            float age = Age;
            if (age >= _life)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            _collider.enabled = Standing;
            if (outline)
            {
                float draw = Mathf.Clamp01(age / outlineTime);
                float fade = Mathf.Clamp01((_life - age) / sinkTime);
                var c = outline.startColor;
                c.a = 0.85f * fade;
                outline.startColor = outline.endColor = c;
                outline.widthMultiplier = 0.06f * Mathf.Lerp(0.3f, 1f, draw);
            }
            if (!_risen && age >= outlineTime)
            {
                _risen = true;
                if (riseDust)
                {
                    riseDust.Clear(true);
                    riseDust.Play(true);
                }
            }
            float sinkStart = _life - sinkTime;
            for (int i = 0; i < planks.Length; i++)
            {
                float up = Mathf.Clamp01((age - outlineTime - i * stagger) / riseTime);
                up = 1f - (1f - up) * (1f - up);
                float overshoot = up < 1f ? 0f : Mathf.Max(0f, 0.12f * Mathf.Sin((age - outlineTime - i * stagger - riseTime) * 30f)
                                                                * Mathf.Exp(-(age - outlineTime - i * stagger - riseTime) * 12f));
                float down = age > sinkStart ? Mathf.Clamp01((age - sinkStart - (planks.Length - 1 - i) * stagger * 0.5f) / sinkTime) : 0f;
                float height = Mathf.Clamp01(up - down * down);
                planks[i].localPosition = _rest[i] + Vector3.down * (depth * (1f - height)) + Vector3.up * overshoot;
                _wobble[i] = Mathf.MoveTowards(_wobble[i], 0f, Time.deltaTime * 4f);
                float tilt = _wobble[i] * 12f * Mathf.Sin(age * 40f + i);
                planks[i].localRotation = Quaternion.Euler(tilt, 0f, 0f);
            }
        }

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (!Blocks(ball))
                return BallContactResult.PassThrough;
            Struck(hit.point);
            if (_redirect && _caster != null && !ball.IsPuppet)
            {
                float speed = Mathf.Max(redirectSpeed, Flat(ball.Velocity).magnitude * 1.1f);
                ball.TurnAround(_caster.gameObject, transform.forward * speed + Vector3.up * 0.8f);
                GameEvents.PlaySound(SoundCue.ShieldBlock, hit.point);
                return BallContactResult.Redirected;
            }
            return BallContactResult.Bounce;
        }

        public bool Blocks(Ball ball) => Standing && ball != null && ball.State == BallState.Live && ball.Team.IsHostileTo(Team.Player);

        public void Struck(Vector3 point)
        {
            GameEvents.PlaySound(SoundCue.FenceKnock, point);
            if (splinters)
                PoolService.Spawn(splinters, point, Quaternion.LookRotation(transform.forward));
            int nearest = 0;
            float best = float.PositiveInfinity;
            for (int i = 0; i < planks.Length; i++)
            {
                float d = (planks[i].position - point).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }
            for (int i = Mathf.Max(0, nearest - 1); i <= Mathf.Min(planks.Length - 1, nearest + 1); i++)
                _wobble[i] = i == nearest ? 1f : 0.5f;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Плюшевая морковка зайца: летит петлёй-бумерангом — от лапы вперёд к цели и назад к хозяину, крутясь.
    /// Бьёт игрока, которого задевает (туда и обратно — по разу). Поймать нельзя, только увернуться. Из пула.
    /// По сети морковку бросает хозяин комнаты и показывает гостям такую же (<see cref="Thrown"/>): у гостя она
    /// летит к копии зайца и обратно, но не бьёт (<see cref="IsPuppet"/>) — удар засчитывает хозяин.
    /// </summary>
    public sealed class CarrotBoomerang : MonoBehaviour, IPoolable
    {
        [Tooltip("Модель морковки: крутится")]
        [SerializeField] Transform visual;
        [SerializeField] float spinSpeed = 900f;

        Transform _owner;
        Func<Vector3> _returnPoint;
        Vector3 _start;
        Vector3 _far;
        Vector3 _side;
        float _flightTime;
        float _started;
        float _radius;
        int _damage;
        float _knockback;
        bool _hitOut;
        bool _hitBack;
        Action _onReturned;

        /// <summary>По сети у гостя: копия морковки хозяина — только летит.</summary>
        public bool IsPuppet { get; set; }

        /// <summary>Морковка брошена: откуда, докуда, сколько летит, радиус удара (по сети хозяин показывает гостям).</summary>
        public static event Action<CarrotBoomerang, Vector3, Vector3, float, float> Thrown;

        public void OnSpawned() => IsPuppet = false;

        public void OnDespawned() => _onReturned = null;

        /// <summary>Бросить: из start к far и обратно в returnPoint(); onReturned — поймали лапой.</summary>
        public void Throw(Transform owner, Vector3 start, Vector3 far, Func<Vector3> returnPoint, float flightTime, float radius,
            int damage, float knockback, Action onReturned)
        {
            _owner = owner;
            _start = start;
            _far = far;
            _returnPoint = returnPoint;
            Vector3 dir = far - start;
            dir.y = 0f;
            _side = Vector3.Cross(Vector3.up, dir.normalized) * Mathf.Min(4f, dir.magnitude * 0.35f);
            _flightTime = Mathf.Max(0.3f, flightTime);
            _started = Time.time;
            _radius = radius;
            _damage = damage;
            _knockback = knockback;
            _hitOut = _hitBack = false;
            _onReturned = onReturned;
            transform.position = start;
            if (!IsPuppet)
                Thrown?.Invoke(this, start, far, _flightTime, radius);
        }

        void Update()
        {
            float t = (Time.time - _started) / _flightTime;
            if (t >= 1f || _owner == null || !_owner.gameObject.activeInHierarchy)
            {
                var done = _onReturned;
                _onReturned = null;
                done?.Invoke();
                PoolService.Despawn(gameObject);
                return;
            }
            // Петля: туда по одной стороне, обратно по другой.
            Vector3 back = _returnPoint != null ? _returnPoint() : _start;
            Vector3 position;
            if (t < 0.5f)
            {
                float k = t * 2f;
                position = Vector3.Lerp(_start, _far, Mathf.SmoothStep(0f, 1f, k)) + _side * Mathf.Sin(k * Mathf.PI);
            }
            else
            {
                float k = (t - 0.5f) * 2f;
                position = Vector3.Lerp(_far, back, Mathf.SmoothStep(0f, 1f, k)) - _side * Mathf.Sin(k * Mathf.PI);
            }
            transform.position = position;
            if (visual)
                visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

            bool outward = t < 0.5f;
            if (IsPuppet || (outward ? _hitOut : _hitBack))
                return;
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - position;
                delta.y = 0f;
                if (delta.sqrMagnitude > _radius * _radius || !target.TryGetComponent(out IDamageable damageable))
                    continue;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = _damage,
                    Point = target.AimPoint,
                    Direction = delta.sqrMagnitude > 1e-4f ? delta.normalized : Vector3.forward,
                    Force = _knockback,
                    SourceTeam = Team.Enemy,
                    Source = _owner ? _owner.gameObject : null,
                    Flags = HitFlags.Melee,
                });
                if (outward)
                    _hitOut = true;
                else
                    _hitBack = true;
                break;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Thrown = null;
    }
}

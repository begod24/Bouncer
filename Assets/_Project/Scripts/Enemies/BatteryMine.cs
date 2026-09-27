using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Батарейка «Крона», брошенная роботом-Трансформером: летит дугой, падает, искрит (на земле круг) и через
    /// секунду взрывается — бьёт игроков в круге. Из пула.
    /// </summary>
    public sealed class BatteryMine : MonoBehaviour, IPoolable
    {
        [SerializeField] GroundMarker markerPrefab;
        [SerializeField] ParticleBurst blastEffect;
        [SerializeField] ExpandingRing blastRing;
        [Tooltip("Модель: мигает и раздувается перед взрывом")]
        [SerializeField] Transform visual;
        [Tooltip("Упав, встаёт торчком: центр на такой высоте")]
        [SerializeField] float landHeight = 0.5f;

        Vector3 _from;
        Vector3 _to;
        float _flightTime;
        float _fuse;
        float _radius;
        int _damage;
        float _knockback;
        GameObject _source;
        float _start;
        bool _landed;
        GroundMarker _marker;
        Vector3 _spin;

        public void OnSpawned() { }

        public void OnDespawned() => HideMarker();

        public void Throw(Vector3 from, Vector3 to, float flightTime, float fuse, float radius, int damage, float knockback, GameObject source)
        {
            _from = from;
            _to = to;
            _flightTime = Mathf.Max(0.1f, flightTime);
            _fuse = fuse;
            _radius = radius;
            _damage = damage;
            _knockback = knockback;
            _source = source;
            _start = Time.time;
            _landed = false;
            _spin = Random.insideUnitSphere * 540f;
            transform.position = from;
            if (visual)
                visual.localScale = Vector3.one;
            if (markerPrefab)
            {
                _marker = PoolService.Spawn(markerPrefab, to, Quaternion.identity);
                _marker.ShowCircle(to, radius, flightTime + fuse);
            }
        }

        void Update()
        {
            float t = (Time.time - _start) / _flightTime;
            if (!_landed)
            {
                float k = Mathf.Clamp01(t);
                transform.position = Vector3.Lerp(_from, _to, k) + Vector3.up * (4f * 2.5f * k * (1f - k));
                transform.Rotate(_spin * Time.deltaTime, Space.World);
                if (k >= 1f)
                {
                    _landed = true;
                    transform.SetPositionAndRotation(_to + Vector3.up * landHeight, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    GameEvents.PlaySound(SoundCue.BallWall, _to);
                }
                return;
            }
            float fuse = Time.time - _start - _flightTime;
            if (visual)
            {
                float pulse = 1f + 0.18f * Mathf.Abs(Mathf.Sin(fuse * (8f + 14f * fuse)));
                visual.localScale = Vector3.one * pulse;
            }
            if (fuse >= _fuse)
                Explode();
        }

        void Explode()
        {
            HideMarker();
            Vector3 center = _to;
            GameEvents.PlaySound(SoundCue.Explosion, center);
            GameFeel.Shake(0.4f);
            if (blastEffect)
                PoolService.Spawn(blastEffect, center + Vector3.up * 0.2f, Quaternion.identity).Play(_radius / 2.6f);
            if (blastRing)
                PoolService.Spawn(blastRing, center + Vector3.up * 0.05f, Quaternion.identity).Play(_radius);
            foreach (var t in Targetable.All)
            {
                if (t.Team != Team.Player || !t.IsAlive)
                    continue;
                Vector3 away = t.Position - center;
                away.y = 0f;
                if (away.sqrMagnitude > _radius * _radius || !t.TryGetComponent(out IDamageable damageable))
                    continue;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = _damage,
                    Point = t.AimPoint,
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward,
                    Force = _knockback,
                    SourceTeam = Team.Enemy,
                    Source = _source,
                    Flags = HitFlags.Area,
                });
            }
            PoolService.Despawn(gameObject);
        }

        void HideMarker()
        {
            if (_marker && _marker.isActiveAndEnabled)
                _marker.Hide();
            _marker = null;
        }
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ElasticBand : MonoBehaviour, IPoolable
    {
        const int Points = 24;
        const float Height = 0.45f;

        [Tooltip("Резинка натягивается за столько секунд")]
        [SerializeField] float stretchTime = 0.2f;
        [SerializeField] float width = 0.09f;

        LineRenderer _line;
        Transform _a;
        Transform _b;
        float _start;
        float _life;
        float _trip;
        bool _authority;
        float _twang;
        readonly System.Collections.Generic.Dictionary<Targetable, float> _side = new();
        readonly System.Collections.Generic.Dictionary<Targetable, float> _cooldown = new();

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.positionCount = Points;
            _line.useWorldSpace = true;
        }

        public void OnSpawned()
        {
            _start = Time.time;
            _twang = 0f;
            _side.Clear();
            _cooldown.Clear();
        }

        public void OnDespawned()
        {
            _a = _b = null;
            _side.Clear();
            _cooldown.Clear();
        }

        public void Play(Transform a, Transform b, float life, float trip, bool authority)
        {
            _a = a;
            _b = b;
            _life = life;
            _trip = trip;
            _authority = authority;
            _start = Time.time;
            GameEvents.PlaySound(SoundCue.ElasticTwang, a.position);
        }

        void Update()
        {
            float age = Time.time - _start;
            if (_a == null || _b == null || age >= _life)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            Vector3 from = _a.position + Vector3.up * Height;
            Vector3 to = _b.position + Vector3.up * Height;
            float stretch = Mathf.Clamp01(age / stretchTime);
            _twang = Mathf.MoveTowards(_twang, 0f, Time.deltaTime * 2.5f);
            float fade = Mathf.Clamp01((_life - age) / 0.3f);
            for (int i = 0; i < Points; i++)
            {
                float t = i / (Points - 1f);
                float along = Mathf.Min(t, stretch);
                Vector3 p = Vector3.Lerp(from, to, along);
                float bow = Mathf.Sin(t * Mathf.PI);
                p += Vector3.down * (0.12f * bow * (1f - _twang));
                p += Vector3.up * (_twang * 0.35f * bow * Mathf.Sin(age * 55f + t * 9f));
                _line.SetPosition(i, p);
            }
            _line.widthMultiplier = width * fade;
            Watch(from, to);
        }

        void Watch(Vector3 from, Vector3 to)
        {
            Vector3 ab = new(to.x - from.x, 0f, to.z - from.z);
            float length = ab.magnitude;
            if (length < 0.5f)
                return;
            Vector3 dir = ab / length;
            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var target = all[i];
                if (target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                Vector3 rel = target.Position - from;
                float along = rel.x * dir.x + rel.z * dir.z;
                if (along < 0f || along > length)
                {
                    _side.Remove(target);
                    continue;
                }
                float side = rel.x * dir.z - rel.z * dir.x;
                if (_side.TryGetValue(target, out float before) && Mathf.Sign(before) != Mathf.Sign(side) && Mathf.Abs(side) < 1.5f)
                    Trip(target);
                _side[target] = side;
            }
        }

        void Trip(Targetable target)
        {
            if (_cooldown.TryGetValue(target, out float until) && Time.time < until)
                return;
            _cooldown[target] = Time.time + 1f;
            _twang = 1f;
            GameEvents.PlaySound(SoundCue.ElasticTwang, target.Position);
            if (!_authority)
                return;
            target.Freeze(_trip);
            if (target.TryGetComponent(out IDamageable damageable))
                damageable.ApplyHit(new HitInfo
                {
                    Damage = 0,
                    Point = target.Position + Vector3.up * 0.3f,
                    Direction = target.Velocity.sqrMagnitude > 0.01f ? target.Velocity.normalized : Vector3.forward,
                    Force = 4f,
                    SourceTeam = Team.Player,
                    Flags = HitFlags.Tackle,
                });
        }
    }
}

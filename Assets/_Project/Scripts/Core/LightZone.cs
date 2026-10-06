using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    public sealed class LightZone : MonoBehaviour
    {
        static readonly List<LightZone> s_all = new();
        static float s_flashUntil;

        [Tooltip("Радиус освещённого круга на земле, м")]
        [SerializeField, Min(0.5f)] float radius = 5f;
        [Tooltip("Центр круга по локальным осям X и Z (голова фонаря висит над дорожкой, а не над столбом)")]
        [SerializeField] Vector2 center;
        [Tooltip("Свет из двери подъезда в финале: сумеречные в него не заходят и оттуда не нападают")]
        [SerializeField] bool repelsDusk;

        float _outUntil;
        float _outStart;

        public static IReadOnlyList<LightZone> All => s_all;

        public static event System.Action<LightZone, float> WentOut;
        public float Radius => radius;
        public bool IsOn => Time.time >= _outUntil;
        public float Out01
        {
            get
            {
                float now = Time.time;
                if (now >= _outUntil)
                    return 0f;
                float fadeIn = Mathf.Clamp01((now - _outStart) / 0.4f);
                float fadeOut = Mathf.Clamp01((_outUntil - now) / 0.6f);
                return Mathf.Min(fadeIn, fadeOut);
            }
        }
        public static bool Flashing => Time.time < s_flashUntil;

        public Vector3 Center
        {
            get
            {
                Vector3 c = transform.TransformPoint(new Vector3(center.x, 0f, center.y));
                c.y = transform.position.y;
                return c;
            }
        }

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        public void PutOut(float seconds)
        {
            if (seconds <= 0f)
                return;
            float now = Time.time;
            if (now >= _outUntil)
                _outStart = now;
            _outUntil = Mathf.Max(_outUntil, now + seconds);
            WentOut?.Invoke(this, seconds);
        }

        public static LightZone FindAt(Vector3 center, float tolerance = 0.5f)
        {
            LightZone best = null;
            float bestSqr = tolerance * tolerance;
            foreach (var zone in s_all)
            {
                Vector3 delta = zone.Center - center;
                delta.y = 0f;
                if (delta.sqrMagnitude <= bestSqr)
                {
                    bestSqr = delta.sqrMagnitude;
                    best = zone;
                }
            }
            return best;
        }

        public void Relight() => _outUntil = Mathf.Min(_outUntil, Time.time);

        public bool Contains(Vector3 position)
        {
            Vector3 delta = position - Center;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
        }

        public static bool IsLit(Vector3 position)
        {
            if (Flashing || LightBeams.Contains(position))
                return true;
            foreach (var zone in s_all)
                if (zone.IsOn && zone.Contains(position))
                    return true;
            foreach (var target in Targetable.All)
            {
                float r = target.LightRadius;
                if (r <= 0f || !target.IsAlive)
                    continue;
                Vector3 delta = position - target.Position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= r * r)
                    return true;
            }
            return false;
        }

        public static LightZone FindNearestOn(Vector3 position, float maxDistance)
        {
            LightZone best = null;
            float bestDistance = maxDistance;
            foreach (var zone in s_all)
            {
                if (!zone.IsOn)
                    continue;
                Vector3 delta = zone.Center - position;
                delta.y = 0f;
                float distance = delta.magnitude - zone.radius;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = zone;
                }
            }
            return best;
        }

        public static void Flash(float seconds) => s_flashUntil = Mathf.Max(s_flashUntil, Time.time + seconds);

        public static bool Repels(Vector3 position)
        {
            foreach (var zone in s_all)
                if (zone.repelsDusk && zone.IsOn && zone.Contains(position))
                    return true;
            return false;
        }

        public static bool PushOutOfRepelling(ref Vector3 point, float margin)
        {
            bool moved = false;
            foreach (var zone in s_all)
            {
                if (!zone.repelsDusk || !zone.IsOn)
                    continue;
                Vector3 c = zone.Center;
                Vector3 delta = point - c;
                delta.y = 0f;
                float limit = zone.radius + margin;
                if (delta.sqrMagnitude >= limit * limit)
                    continue;
                Vector3 away = delta.sqrMagnitude > 1e-4f ? delta.normalized : Vector3.back;
                point = new Vector3(c.x + away.x * limit, point.y, c.z + away.z * limit);
                moved = true;
            }
            return moved;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.5f, 0.6f);
            Vector3 c = Center;
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0) * radius, 0.05f, Mathf.Sin(a0) * radius),
                    c + new Vector3(Mathf.Cos(a1) * radius, 0.05f, Mathf.Sin(a1) * radius));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_all.Clear();
            s_flashUntil = 0f;
            WentOut = null;
        }
    }
}

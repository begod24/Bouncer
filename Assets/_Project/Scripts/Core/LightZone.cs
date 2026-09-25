using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Освещённое место на арене: фонарь, прожектор, бочка с огнём. На свету тень твёрдая, а появляется она
    /// только в темноте. Круг в плоскости XZ, проверка по реестру (как у <see cref="GroundZone"/>).
    /// Фонарь можно погасить на время (элитная тень), вспышка молнии на миг освещает всё.
    /// Сам свет (компонент Light, мерцание) ведёт Visuals, здесь только игровая часть.
    /// </summary>
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
        public float Radius => radius;
        /// <summary>Горит (не погашен тенью).</summary>
        public bool IsOn => Time.time >= _outUntil;
        /// <summary>0 — горит, 1 — погашен; между ними — мигает, пока гаснет и пока загорается.</summary>
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
        /// <summary>Молния: всё освещено до этого момента.</summary>
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

        /// <summary>Погасить на столько секунд: фонарь мигнёт и потухнет, потом загорится снова.</summary>
        public void PutOut(float seconds)
        {
            if (seconds <= 0f)
                return;
            float now = Time.time;
            if (now >= _outUntil)
                _outStart = now;
            _outUntil = Mathf.Max(_outUntil, now + seconds);
        }

        /// <summary>Зажечь погашенный фонарь сейчас же.</summary>
        public void Relight() => _outUntil = Mathf.Min(_outUntil, Time.time);

        public bool Contains(Vector3 position)
        {
            Vector3 delta = position - Center;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// Светло ли в точке: горящий фонарь, «Фонарик» игрока или вспышка молнии.
        /// Маленький круг света вокруг игрока без карточки светом не считается — он только чтобы видеть.
        /// </summary>
        public static bool IsLit(Vector3 position)
        {
            if (Flashing)
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

        /// <summary>Ближайший горящий фонарь в радиусе от точки (до края его круга). null — нет.</summary>
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

        /// <summary>Вспышка молнии: всё освещено столько секунд.</summary>
        public static void Flash(float seconds) => s_flashUntil = Mathf.Max(s_flashUntil, Time.time + seconds);

        /// <summary>Точка в свете, который отгоняет сумеречных (дверь подъезда в финале).</summary>
        public static bool Repels(Vector3 position)
        {
            foreach (var zone in s_all)
                if (zone.repelsDusk && zone.IsOn && zone.Contains(position))
                    return true;
            return false;
        }

        /// <summary>Если точка в отгоняющем свете — сдвинуть её за край круга (с запасом margin). true — сдвинули.</summary>
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
        }
    }
}

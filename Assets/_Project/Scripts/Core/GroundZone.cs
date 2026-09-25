using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Участок земли с особыми свойствами — песок в песочнице: замедляет всех, кто по нему идёт,
    /// и быстро гасит катящиеся мячи. Прямоугольник в плоскости XZ без физических триггеров:
    /// проверка по реестру дешёвая и одинаковая для игрока, врагов и мячей (позже — и для хоста в сети).
    /// </summary>
    public sealed class GroundZone : MonoBehaviour
    {
        static readonly List<GroundZone> s_all = new();

        [Tooltip("Размер участка по локальным осям X и Z")]
        [SerializeField] Vector2 size = new(2.6f, 2.6f);
        [Tooltip("Участок — эллипс, вписанный в этот прямоугольник (лужа), а не сам прямоугольник")]
        [SerializeField] bool round;
        [Tooltip("Центр участка по локальным осям X и Z")]
        [SerializeField] Vector2 center;
        [Tooltip("Множитель скорости бега на участке")]
        [SerializeField, Range(0.1f, 1f)] float moveMultiplier = 0.55f;
        [Tooltip("Дополнительное торможение лежащего мяча")]
        [SerializeField, Min(0f)] float ballDamping = 5f;
        [Tooltip("Выше этой высоты над участком (прыжок, полёт) свойства не действуют")]
        [SerializeField, Min(0f)] float maxHeight = 0.6f;

        public float MoveMultiplier => moveMultiplier;

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        public bool Contains(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            if (local.y > maxHeight)
                return false;
            float x = (local.x - center.x) / Mathf.Max(0.01f, size.x * 0.5f);
            float z = (local.z - center.y) / Mathf.Max(0.01f, size.y * 0.5f);
            return round ? x * x + z * z <= 1f : Mathf.Abs(x) <= 1f && Mathf.Abs(z) <= 1f;
        }

        /// <summary>Множитель скорости в точке: 1 — обычная земля.</summary>
        public static float MoveMultiplierAt(Vector3 position)
        {
            float multiplier = 1f;
            foreach (var zone in s_all)
                if (zone.Contains(position))
                    multiplier = Mathf.Min(multiplier, zone.moveMultiplier);
            return multiplier;
        }

        /// <summary>Дополнительное торможение лежащего мяча в точке: 0 — обычная земля.</summary>
        public static float BallDampingAt(Vector3 position)
        {
            float damping = 0f;
            foreach (var zone in s_all)
                if (zone.Contains(position))
                    damping = Mathf.Max(damping, zone.ballDamping);
            return damping;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.35f);
            Gizmos.DrawCube(new Vector3(center.x, 0.05f, center.y), new Vector3(size.x, 0.1f, size.y));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_all.Clear();
    }
}

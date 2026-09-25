using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Укрытие для «Считалочки»: за ним «Тот, кто в сумерках» игрока не видит (качели, песочница с грибком).
    /// Только коробка для проверки взгляда, без физики — мячи и бег она не трогает. Настоящие препятствия
    /// с коллайдерами (ракета, ларёк) закрывают взгляд и так.
    /// </summary>
    public sealed class CoverVolume : MonoBehaviour
    {
        static readonly List<CoverVolume> s_all = new();

        [Tooltip("Коробка укрытия в локальных осях")]
        [SerializeField] Vector3 center = new(0f, 1.2f, 0f);
        [SerializeField] Vector3 size = new(2f, 2.4f, 2f);

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        /// <summary>Отрезок от from до to проходит сквозь хоть одно укрытие.</summary>
        public static bool Blocks(Vector3 from, Vector3 to)
        {
            foreach (var cover in s_all)
                if (cover.Intersects(from, to))
                    return true;
            return false;
        }

        /// <summary>Отрезок против коробки в её осях (метод «плит»).</summary>
        bool Intersects(Vector3 from, Vector3 to)
        {
            Vector3 a = transform.InverseTransformPoint(from) - center;
            Vector3 b = transform.InverseTransformPoint(to) - center;
            Vector3 half = size * 0.5f;
            Vector3 d = b - a;
            float tMin = 0f, tMax = 1f;
            for (int axis = 0; axis < 3; axis++)
            {
                float origin = a[axis], dir = d[axis], extent = half[axis];
                if (Mathf.Abs(dir) < 1e-6f)
                {
                    if (origin < -extent || origin > extent)
                        return false;
                    continue;
                }
                float t0 = (-extent - origin) / dir;
                float t1 = (extent - origin) / dir;
                if (t0 > t1)
                    (t0, t1) = (t1, t0);
                tMin = Mathf.Max(tMin, t0);
                tMax = Mathf.Min(tMax, t1);
                if (tMin > tMax)
                    return false;
            }
            return true;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(center, size);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_all.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    public enum ArenaSpotKind
    {
        Bench,
        BallGate,
        BossEntrance,
    }

    public sealed class ArenaSpot : MonoBehaviour
    {
        static readonly List<ArenaSpot> s_all = new();

        [SerializeField] ArenaSpotKind kind;

        public ArenaSpotKind Kind => kind;
        public Vector3 Position => transform.position;
        public Vector3 Inward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            }
        }

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        public static ArenaSpot FindFarthest(ArenaSpotKind kind, Vector3 from)
        {
            ArenaSpot best = null;
            float bestSqr = -1f;
            foreach (var spot in s_all)
            {
                if (spot.kind != kind)
                    continue;
                Vector3 delta = spot.Position - from;
                delta.y = 0f;
                if (delta.sqrMagnitude > bestSqr)
                {
                    bestSqr = delta.sqrMagnitude;
                    best = spot;
                }
            }
            return best;
        }

        public static ArenaSpot Find(ArenaSpotKind kind)
        {
            foreach (var spot in s_all)
                if (spot.kind == kind)
                    return spot;
            return null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                ArenaSpotKind.Bench => new Color(0.4f, 0.8f, 1f),
                ArenaSpotKind.BossEntrance => new Color(1f, 0.3f, 0.3f),
                _ => new Color(1f, 0.6f, 0.2f),
            };
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, transform.position + Vector3.up * 0.5f + Inward * 1.5f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_all.Clear();
    }
}

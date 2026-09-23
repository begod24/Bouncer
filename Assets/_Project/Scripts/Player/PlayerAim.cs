using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>Итоговое направление броска: ручной прицел или автоприцел с упреждением.</summary>
    public sealed class PlayerAim : MonoBehaviour
    {
        PlayerStats _stats;

        public Vector3 Direction { get; private set; } = Vector3.forward;
        /// <summary>Цель автоприцела (null — целимся вручную).</summary>
        public Targetable Target { get; private set; }
        public bool HasInput { get; private set; }

        public void Init(PlayerStats stats)
        {
            _stats = stats;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > 1e-4f)
                Direction = forward.normalized;
        }

        public void Tick(in PlayerIntent intent, Vector3 origin, float projectileSpeed)
        {
            Vector3 baseDirection = intent.Aim;
            HasInput = baseDirection.sqrMagnitude > 0.01f;
            if (!HasInput)
                baseDirection = intent.Move.sqrMagnitude > 0.01f ? intent.Move.normalized : Direction;

            Target = GameSettings.AutoAimFor(intent.UsingGamepad) ? FindTarget(origin, baseDirection) : null;
            Direction = Target != null ? LeadDirection(origin, Target, projectileSpeed) : baseDirection;
        }

        Targetable FindTarget(Vector3 origin, Vector3 direction)
        {
            float minDot = Mathf.Cos(_stats.autoAimAngle * Mathf.Deg2Rad);
            float bestScore = float.PositiveInfinity;
            Targetable best = null;
            foreach (var target in Targetable.All)
            {
                if (!target.IsAlive || !Team.Player.IsHostileTo(target.Team))
                    continue;
                Vector3 to = target.AimPoint - origin;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance < 0.01f || distance > _stats.autoAimRange)
                    continue;
                float dot = Vector3.Dot(to / distance, direction);
                if (dot < minDot)
                    continue;
                // Угол важнее расстояния: берём того, кто ближе к линии прицела.
                float score = (1f - dot) * 10f + distance * 0.05f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = target;
                }
            }
            return best;
        }

        static Vector3 LeadDirection(Vector3 origin, Targetable target, float speed)
        {
            Vector3 point = target.AimPoint;
            Vector3 flat = point - origin;
            flat.y = 0f;
            Vector3 velocity = target.Velocity;
            velocity.y = 0f;
            float time = flat.magnitude / Mathf.Max(1f, speed);
            Vector3 lead = flat + velocity * time;
            if (lead.sqrMagnitude < 1e-4f)
                lead = flat;
            return lead.sqrMagnitude > 1e-6f ? lead.normalized : Vector3.forward;
        }
    }
}

using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Линия прицела по полу «мелом»: до первой преграды и короткий отрезок рикошета.
    /// Длина — примерная дальность текущего броска (растёт с зарядом).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AimIndicator : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] float groundHeight = 0.05f;
        [SerializeField] float bounceLength = 3f;
        [SerializeField] Color normalColor = new(1f, 1f, 1f, 0.35f);
        [SerializeField] Color chargedColor = new(1f, 1f, 1f, 0.85f);
        [SerializeField] Color candleColor = new(1f, 0.8f, 0.2f, 0.9f);
        [SerializeField] Color autoAimColor = new(0.5f, 0.9f, 1f, 0.6f);

        LineRenderer _line;
        readonly Vector3[] _points = new Vector3[3];

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
        }

        void LateUpdate()
        {
            var balls = player.Balls;
            var definition = balls.BallDefinition;
            if (!GameSettings.ShowAimPreview || player.IsDead || balls.Balls == 0 || definition == null
                || !GameSession.IsGameplayActive)
            {
                _line.enabled = false;
                return;
            }

            var stats = definition.GetThrowStats(balls.Charge01, balls.CandleReady);
            float range = definition.EstimateRange(stats, player.Stats.throwHeight);
            Vector3 direction = player.Aim.Direction;
            Vector3 origin = player.transform.position + Vector3.up * player.Stats.throwHeight;

            int count = 2;
            _points[0] = origin;
            // Сдутый мяч пролетает сквозь врагов, задевая их, — линию обрывает только окружение.
            int mask = definition.perks.grazeRadius > 0f ? Layers.EnvironmentMask : Layers.EnvironmentMask | Layers.EnemyMask;
            if (Physics.SphereCast(origin, definition.radius, direction, out RaycastHit hit, range, mask,
                    QueryTriggerInteraction.Ignore))
            {
                _points[1] = origin + direction * hit.distance;
                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (hit.collider.gameObject.layer == Layers.Environment && normal.sqrMagnitude > 1e-4f)
                {
                    Vector3 bounce = Vector3.Reflect(direction, normal.normalized);
                    _points[2] = _points[1] + bounce * Mathf.Min(bounceLength, range - hit.distance);
                    count = 3;
                }
            }
            else
            {
                _points[1] = origin + direction * range;
            }

            for (int i = 0; i < count; i++)
                _points[i].y = groundHeight;

            Color color = balls.CandleReady ? candleColor
                : player.Aim.Target != null ? autoAimColor
                : Color.Lerp(normalColor, chargedColor, balls.Charge01);
            _line.enabled = true;
            _line.positionCount = count;
            for (int i = 0; i < count; i++)
                _line.SetPosition(i, _points[i]);
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, color.a * 0.3f);
            _line.widthMultiplier = Mathf.Lerp(0.06f, 0.14f, balls.Charge01);
        }
    }
}

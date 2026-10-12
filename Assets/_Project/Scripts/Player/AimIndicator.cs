using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
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

        [Header("Навес (баскетбольный): кружок, куда упадёт мяч")]
        [SerializeField] GroundMarker landingMarkerPrefab;
        [SerializeField] Color landingColor = new(1f, 0.62f, 0.2f, 0.9f);

        LineRenderer _line;
        readonly Vector3[] _points = new Vector3[3];
        GroundMarker _aimMarker;
        readonly Dictionary<Ball, (GroundMarker marker, int life, float total)> _flying = new();
        readonly List<Ball> _gone = new();

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
        }

        void OnDisable()
        {
            HideAimMarker();
            foreach (var pair in _flying)
                Despawn(pair.Value.marker);
            _flying.Clear();
        }

        void LateUpdate()
        {
            UpdateLandingMarkers();
            var balls = player.Balls;
            var definition = balls.BallDefinition;
            if (!GameSettings.ShowAimPreview || !player.IsLocal || player.IsDead || balls.Balls == 0 || definition == null
                || !GameSession.IsGameplayActive)
            {
                _line.enabled = false;
                return;
            }

            if (definition.perks.lob && balls.TryGetLobLanding(out Vector3 landing, out _))
            {
                ShowLobLine(landing, balls);
                return;
            }

            var stats = definition.GetThrowStats(balls.Charge01, balls.CandleReady);
            float range = definition.EstimateRange(stats, player.Stats.throwHeight);
            Vector3 direction = player.Aim.Direction;
            Vector3 origin = player.transform.position + Vector3.up * player.Stats.throwHeight;

            int count = 2;
            _points[0] = origin;
            int mask = definition.perks.grazeRadius > 0f ? Layers.BallSolidMask : Layers.BallSolidMask | Layers.EnemyMask;
            if (Physics.SphereCast(origin, definition.radius, direction, out RaycastHit hit, range, mask,
                    QueryTriggerInteraction.Ignore))
            {
                _points[1] = origin + direction * hit.distance;
                Vector3 normal = hit.normal;
                normal.y = 0f;
                if (Layers.IsBallSolid(hit.collider.gameObject.layer) && normal.sqrMagnitude > 1e-4f)
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

        void ShowLobLine(Vector3 landing, PlayerBallHandler balls)
        {
            Vector3 from = player.transform.position;
            Color color = balls.CandleReady ? candleColor : Color.Lerp(normalColor, chargedColor, balls.Charge01);
            _line.enabled = true;
            _line.positionCount = 2;
            _line.SetPosition(0, new Vector3(from.x, groundHeight, from.z));
            _line.SetPosition(1, new Vector3(landing.x, groundHeight, landing.z));
            _line.startColor = color;
            _line.endColor = new Color(color.r, color.g, color.b, color.a * 0.3f);
            _line.widthMultiplier = Mathf.Lerp(0.06f, 0.14f, balls.Charge01);
        }

        void UpdateLandingMarkers()
        {
            if (landingMarkerPrefab == null)
                return;
            var balls = player.Balls;
            bool aiming = player.IsLocal && !player.IsDead && balls.Balls > 0 && GameSession.IsPlayerActive
                          && balls.TryGetLobLanding(out _, out _);
            if (aiming && balls.TryGetLobLanding(out Vector3 point, out float radius))
            {
                if (_aimMarker == null || !_aimMarker.isActiveAndEnabled)
                    _aimMarker = Spawn();
                _aimMarker.ShowSteady(point, radius, balls.Charge01, Mathf.Lerp(0.45f, 0.9f, balls.Charge01));
            }
            else
            {
                HideAimMarker();
            }

            var active = Ball.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var ball = active[i];
                if (ball.State != BallState.Live || ball.Thrower != player.gameObject || ball.Definition == null
                    || !ball.Definition.perks.lob || !player.IsLocal)
                    continue;
                float left = TimeToLand(ball);
                if (!_flying.TryGetValue(ball, out var entry) || entry.life != ball.Life || entry.marker == null
                    || !entry.marker.isActiveAndEnabled)
                {
                    if (entry.marker != null && entry.marker.isActiveAndEnabled)
                        Despawn(entry.marker);
                    entry = (Spawn(), ball.Life, Mathf.Max(0.05f, left));
                    _flying[ball] = entry;
                }
                if (ball.TryPredictLanding(out Vector3 landing))
                    entry.marker.ShowSteady(landing, ball.Definition.lobMarkerRadius, 1f - left / entry.total, 0.9f);
            }

            _gone.Clear();
            foreach (var pair in _flying)
                if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Key.State != BallState.Live || pair.Key.Life != pair.Value.life)
                    _gone.Add(pair.Key);
            foreach (var ball in _gone)
            {
                Despawn(_flying[ball].marker);
                _flying.Remove(ball);
            }
        }

        static float TimeToLand(Ball ball)
        {
            float height = Mathf.Max(0f, ball.Position.y - ball.Radius);
            float gravity = Mathf.Max(0.01f, ball.Gravity);
            float vy = ball.Velocity.y;
            return (vy + Mathf.Sqrt(vy * vy + 2f * gravity * height)) / gravity;
        }

        GroundMarker Spawn()
        {
            var marker = PoolService.Spawn(landingMarkerPrefab, transform.position, Quaternion.identity);
            marker.IsPuppet = true;
            marker.SetColor(landingColor);
            return marker;
        }

        void HideAimMarker()
        {
            Despawn(_aimMarker);
            _aimMarker = null;
        }

        static void Despawn(GroundMarker marker)
        {
            if (marker != null && marker.isActiveAndEnabled)
                PoolService.Despawn(marker.gameObject);
        }
    }
}

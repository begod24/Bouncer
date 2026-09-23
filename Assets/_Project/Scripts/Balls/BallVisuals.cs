using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
    /// <summary>
    /// Внешний вид мяча по состоянию: цвет (чей мяч, опасен ли), след в полёте,
    /// метка приземления у «свечки». Логики тут нет.
    /// </summary>
    [RequireComponent(typeof(Ball))]
    public sealed class BallVisuals : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer body;
        [SerializeField] TrailRenderer trail;
        [SerializeField] CircleLine landingMarker;

        [Header("Цвета")]
        [SerializeField] Color looseColor = new(0.85f, 0.18f, 0.15f);
        [SerializeField] Color playerLiveColor = new(1f, 0.45f, 0.15f);
        [SerializeField] Color enemyLiveColor = new(0.6f, 0.2f, 0.95f);
        [SerializeField] Color poppedColor = new(1f, 0.92f, 0.35f);
        [SerializeField] Color candleColor = new(1f, 0.75f, 0.1f);
        [SerializeField] float trailWidth = 0.35f;

        Ball _ball;
        MaterialPropertyBlock _block;

        void Awake()
        {
            _ball = GetComponent<Ball>();
            _block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            _ball.StateChanged += Refresh;
            if (trail)
                trail.Clear();
            Refresh(_ball);
        }

        void OnDisable() => _ball.StateChanged -= Refresh;

        void Refresh(Ball ball)
        {
            Color color = ball.State switch
            {
                BallState.Live when ball.Stats.Has(HitFlags.Candle) => candleColor,
                BallState.Live => ball.Team == Team.Enemy ? enemyLiveColor : playerLiveColor,
                BallState.Popped => poppedColor,
                _ => looseColor,
            };

            if (body)
            {
                body.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                body.SetPropertyBlock(_block);
            }

            if (trail)
            {
                if (ball.State == BallState.Idle)
                    trail.Clear();
                trail.emitting = ball.State is BallState.Live or BallState.Popped;
                trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                bool strong = ball.State == BallState.Live && ball.Stats.Has(HitFlags.Charged);
                trail.widthMultiplier = trailWidth * (strong ? 1.6f : 1f);
            }

            if (landingMarker)
                landingMarker.gameObject.SetActive(ball.State == BallState.Popped);
        }

        void LateUpdate()
        {
            if (!landingMarker || _ball.State != BallState.Popped || !_ball.TryPredictLanding(out Vector3 point))
                return;
            float height01 = Mathf.Clamp01(transform.position.y / 4f);
            landingMarker.transform.SetPositionAndRotation(point + Vector3.up * 0.03f, Quaternion.identity);
            landingMarker.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, height01);
        }
    }
}

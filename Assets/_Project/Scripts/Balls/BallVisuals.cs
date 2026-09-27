using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
    /// <summary>
    /// Внешний вид мяча по состоянию: цвет (чей мяч, опасен ли), след в полёте,
    /// метка приземления у «свечки». Особые мячи врагов видны издалека: ёжик — красный, с шипами и сиянием
    /// (ловить нельзя), мокрый — синеватый, тёмный мяч Бабая — чёрный. Логики тут нет.
    /// </summary>
    [RequireComponent(typeof(Ball))]
    public sealed class BallVisuals : MonoBehaviour
    {
        static readonly int BaseColorId = PaletteShader.BaseColor;

        [SerializeField] Renderer body;
        [SerializeField] TrailRenderer trail;
        [SerializeField] CircleLine landingMarker;
        [Tooltip("Шипы ёжика (включаются, пока летит колючий мяч)")]
        [SerializeField] GameObject spikes;
        [Tooltip("Сияние вокруг ёжика")]
        [SerializeField] GameObject glow;

        [Header("Цвета")]
        [SerializeField] Color looseColor = new(0.85f, 0.18f, 0.15f);
        [SerializeField] Color playerLiveColor = new(1f, 0.45f, 0.15f);
        [SerializeField] Color enemyLiveColor = new(0.6f, 0.2f, 0.95f);
        [SerializeField] Color poppedColor = new(1f, 0.92f, 0.35f);
        [SerializeField] Color candleColor = new(1f, 0.75f, 0.1f);
        [Tooltip("Горячая картошка: летит и ещё не взорвалась")]
        [SerializeField] Color hotColor = new(1f, 0.3f, 0.08f);
        [Tooltip("Ёжик: колючий мяч, ловить нельзя")]
        [SerializeField] Color spikyColor = new(0.95f, 0.1f, 0.08f);
        [SerializeField] Color wetColor = new(0.35f, 0.62f, 1f);
        [SerializeField] Color darkColor = new(0.1f, 0.05f, 0.16f);
        [Tooltip("Пульс сияния ёжика: раз в секунду")]
        [SerializeField] float spikyPulse = 2.2f;
        [Tooltip("Модель мяча на шейдере палитры: насколько перекрашивать её в цвет состояния. Лежащий мяч — своего цвета.")]
        [SerializeField, Range(0f, 1f)] float stateTint = 0.65f;
        [SerializeField] float trailWidth = 0.35f;

        Ball _ball;
        MaterialPropertyBlock _block;
        bool _palette;
        bool _hot;
        bool _spiky;

        void Awake()
        {
            _ball = GetComponent<Ball>();
            _block = new MaterialPropertyBlock();
            _palette = body && PaletteShader.Supports(body.sharedMaterial);
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
            _hot = ball.BlastPending;
            bool live = ball.State == BallState.Live;
            _spiky = live && ball.Stats.Has(HitFlags.Spiky);
            Color color = ball.State switch
            {
                BallState.Live when _hot => hotColor,
                BallState.Live when _spiky => spikyColor,
                BallState.Live when ball.Stats.Has(HitFlags.Dark) => darkColor,
                BallState.Live when ball.Stats.Has(HitFlags.Wet) => wetColor,
                BallState.Live when ball.Stats.Has(HitFlags.Candle) => candleColor,
                BallState.Live => ball.Team == Team.Enemy ? enemyLiveColor : playerLiveColor,
                BallState.Popped => poppedColor,
                _ => looseColor,
            };
            if (spikes)
                spikes.SetActive(_spiky);
            if (glow)
                glow.SetActive(_spiky);

            if (body)
            {
                body.GetPropertyBlock(_block);
                if (_palette)
                {
                    bool flying = ball.State is BallState.Live or BallState.Popped;
                    bool special = live && ball.Stats.Has(HitFlags.Spiky | HitFlags.Dark | HitFlags.Wet);
                    _block.SetColor(PaletteShader.TintColor, PaletteShader.Tint(color, flying ? special ? 0.9f : stateTint : 0f));
                    _block.SetColor(PaletteShader.FlashColor, PaletteShader.Tint(spikyColor, 0f));
                }
                else
                {
                    _block.SetColor(BaseColorId, color);
                }
                body.SetPropertyBlock(_block);
            }

            if (trail)
            {
                if (ball.State == BallState.Idle)
                    trail.Clear();
                trail.emitting = ball.State is BallState.Live or BallState.Popped or BallState.Returning;
                trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                bool strong = ball.State == BallState.Live && (ball.Stats.Has(HitFlags.Charged) || _hot || _spiky);
                trail.widthMultiplier = trailWidth * (strong ? 1.6f : 1f);
            }

            if (landingMarker)
                landingMarker.gameObject.SetActive(ball.State == BallState.Popped);
        }

        void LateUpdate()
        {
            // Горячая картошка взорвалась, не сменив состояния (отскок от асфальта) — гасим цвет.
            if (_hot != _ball.BlastPending)
                Refresh(_ball);
            // Ёжик светится: красная вспышка поверх освещения пульсирует.
            if (_spiky && body && _palette)
            {
                float pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * spikyPulse * Mathf.PI * 2f);
                body.GetPropertyBlock(_block);
                _block.SetColor(PaletteShader.FlashColor, PaletteShader.Tint(spikyColor, pulse));
                body.SetPropertyBlock(_block);
            }
            if (!landingMarker || _ball.State != BallState.Popped || !_ball.TryPredictLanding(out Vector3 point))
                return;
            float height01 = Mathf.Clamp01(transform.position.y / 4f);
            landingMarker.transform.SetPositionAndRotation(point + Vector3.up * 0.03f, Quaternion.identity);
            landingMarker.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, height01);
        }
    }
}

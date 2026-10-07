using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Balls
{
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
        [Tooltip("Ёжик: искры и колючие осколки за мячом в полёте")]
        [SerializeField] ParticleSystem spikySparks;
        [Tooltip("Ёжик: красный круг с зубцами на асфальте под мячом")]
        [SerializeField] Transform spikyWarning;
        [Tooltip("Ёжик: тело темнеет, светятся шипы")]
        [SerializeField] Color spikyBodyColor = new(0.2f, 0.04f, 0.06f);
        [Tooltip("Ёжик: оборотов в секунду на метр скорости (кувырок по ходу полёта)")]
        [SerializeField] float spikyRoll = 0.6f;
        [Tooltip("Ёжик: насколько шипы топорщатся (доля длины)")]
        [SerializeField] float spikyBristle = 0.14f;

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
        Vector3 _bodyLocal;
        bool _offset;
        Quaternion _bodyRotation;
        Vector3 _spikesScale = Vector3.one;
        ParticleSystem[] _sparkSystems = System.Array.Empty<ParticleSystem>();
        Renderer _warningRenderer;
        MaterialPropertyBlock _warningBlock;

        void Awake()
        {
            _ball = GetComponent<Ball>();
            _block = new MaterialPropertyBlock();
            _palette = body && PaletteShader.Supports(body.sharedMaterial);
            if (body)
            {
                _bodyLocal = body.transform.localPosition;
                _bodyRotation = body.transform.localRotation;
            }
            if (spikes)
                _spikesScale = spikes.transform.localScale;
            if (spikySparks)
                _sparkSystems = spikySparks.GetComponentsInChildren<ParticleSystem>(true);
            if (spikyWarning)
            {
                _warningRenderer = spikyWarning.GetComponentInChildren<Renderer>();
                _warningBlock = new MaterialPropertyBlock();
            }
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
            foreach (var system in _sparkSystems)
            {
                var emission = system.emission;
                emission.enabled = _spiky;
                if (_spiky && !system.isPlaying)
                    system.Play(false);
            }
            if (spikyWarning)
                spikyWarning.gameObject.SetActive(_spiky);
            if (!_spiky && body)
                body.transform.localRotation = _bodyRotation;
            // у ёжика свой вид — без контрового света
            if (body)
                FxGlobals.SetRim(body, !_spiky);
            if (!_spiky && spikes)
                spikes.transform.localScale = _spikesScale;

            if (body)
            {
                body.GetPropertyBlock(_block);
                if (_palette)
                {
                    bool flying = ball.State is BallState.Live or BallState.Popped;
                    bool special = live && ball.Stats.Has(HitFlags.Spiky | HitFlags.Dark | HitFlags.Wet);
                    Color tint = _spiky ? spikyBodyColor : color;
                    _block.SetColor(PaletteShader.TintColor, PaletteShader.Tint(tint, flying ? special ? 0.9f : stateTint : 0f));
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
                trail.emitting = ball.State is BallState.Live or BallState.Popped or BallState.Returning && !_offset;
                trail.startColor = new Color(color.r, color.g, color.b, 0.8f);
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                bool strong = ball.State == BallState.Live && (ball.Stats.Has(HitFlags.Charged) || _hot || _spiky);
                trail.widthMultiplier = trailWidth * (strong ? 1.6f : 1f);
            }

            if (landingMarker)
                landingMarker.gameObject.SetActive(ball.State == BallState.Popped);
        }

        void AnimateSpiky()
        {
            float dt = Time.deltaTime;
            float wave = Mathf.Sin(Time.time * spikyPulse * Mathf.PI * 2f);
            if (body && _palette)
            {
                body.GetPropertyBlock(_block);
                _block.SetColor(PaletteShader.FlashColor, PaletteShader.Tint(spikyColor, 0.18f + 0.14f * wave));
                body.SetPropertyBlock(_block);
            }
            Vector3 velocity = _ball.Velocity;
            Vector3 flat = new(velocity.x, 0f, velocity.z);
            if (body && flat.sqrMagnitude > 0.01f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, flat.normalized);
                float degrees = flat.magnitude * spikyRoll * 360f * dt;
                body.transform.rotation = Quaternion.AngleAxis(degrees, axis) * body.transform.rotation;
            }
            if (spikes)
                spikes.transform.localScale = _spikesScale * (1f + spikyBristle * (0.5f + 0.5f * Mathf.Sin(Time.time * 17f)));
            if (spikyWarning)
            {
                Vector3 p = transform.position;
                float height = Mathf.Max(0f, p.y);
                spikyWarning.SetPositionAndRotation(new Vector3(p.x, 0.04f, p.z), Quaternion.Euler(0f, Time.time * 140f, 0f));
                spikyWarning.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.5f, Mathf.Clamp01(height / 3f)) * (1f + 0.08f * wave);
                if (_warningRenderer)
                {
                    _warningRenderer.GetPropertyBlock(_warningBlock);
                    _warningBlock.SetColor(BaseColorId, new Color(1f, 0.18f, 0.12f, 0.55f + 0.3f * wave));
                    _warningRenderer.SetPropertyBlock(_warningBlock);
                }
            }
        }

        void LateUpdate()
        {
            Vector3 offset = _ball.VisualOffset;
            bool shifted = offset != Vector3.zero;
            if (shifted || _offset)
            {
                if (body)
                    body.transform.localPosition = _bodyLocal + transform.InverseTransformVector(offset);
                if (shifted != _offset)
                {
                    _offset = shifted;
                    Refresh(_ball);
                }
            }
            if (_hot != _ball.BlastPending)
                Refresh(_ball);
            if (_spiky)
                AnimateSpiky();
            if (!landingMarker || _ball.State != BallState.Popped || !_ball.TryPredictLanding(out Vector3 point))
                return;
            float height01 = Mathf.Clamp01(transform.position.y / 4f);
            landingMarker.transform.SetPositionAndRotation(point + Vector3.up * 0.03f, Quaternion.identity);
            landingMarker.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.4f, height01);
        }
    }
}

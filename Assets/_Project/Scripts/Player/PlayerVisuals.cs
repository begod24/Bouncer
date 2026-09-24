using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Визуал игрока: мяч в руке и заряд, кольцо ловли, мигание при неуязвимости, след рывка, падение.
    /// Эффекты карточек: горячий мяч в руке, подкат (рывок ногами вперёд), волна «Замри!».
    /// </summary>
    public sealed class PlayerVisuals : MonoBehaviour
    {
        static readonly int BaseColorId = PaletteShader.BaseColor;

        [SerializeField] PlayerController player;
        [SerializeField] Transform body;
        [SerializeField] Renderer[] blinkRenderers;
        [SerializeField] Renderer handBall;
        [SerializeField] CircleLine catchRing;
        [SerializeField] TrailRenderer dashTrail;
        [SerializeField] HitFlash hitFlash;
        [Tooltip("Кольцо «Замри!», из пула")]
        [SerializeField] ExpandingRing freezeWave;
        [SerializeField] float freezeWaveRadius = 7f;

        [Header("Цвета")]
        [SerializeField] Color ballColor = new(0.85f, 0.18f, 0.15f);
        [SerializeField] Color chargedColor = new(1f, 0.95f, 0.8f);
        [SerializeField] Color candleColor = new(1f, 0.75f, 0.1f);
        [Tooltip("Горячая картошка: следующий бросок взорвётся")]
        [SerializeField] Color hotColor = new(1f, 0.3f, 0.08f);
        [SerializeField] Color catchActiveColor = new(0.35f, 1f, 0.45f, 0.95f);
        [SerializeField] Color catchCooldownColor = new(1f, 1f, 1f, 0.25f);

        [Header("Подкат")]
        [Tooltip("Насколько тело откидывается назад в подкате, градусы")]
        [SerializeField] float slideLean = 60f;
        [Tooltip("Насколько тело опускается в подкате, м")]
        [SerializeField] float slideDrop = 0.22f;

        MaterialPropertyBlock _block;
        Vector3 _handBallScale;
        Quaternion _bodyRotation;
        Vector3 _bodyPosition;
        bool _handBallPalette;
        float _slide;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (handBall)
            {
                _handBallScale = handBall.transform.localScale;
                _handBallPalette = PaletteShader.Supports(handBall.sharedMaterial);
            }
            if (body)
            {
                _bodyRotation = body.localRotation;
                _bodyPosition = body.localPosition;
            }
        }

        // Подписка в Start: к этому моменту PlayerController.Awake уже заполнил свои ссылки.
        void Start()
        {
            player.Hurt += OnHurt;
            player.Froze += OnFroze;
            player.Balls.Caught += OnCaught;
            player.Balls.BallTypeChanged += OnBallTypeChanged;
            player.Health.Died += OnDied;
        }

        void OnDestroy()
        {
            if (player == null || player.Balls == null)
                return;
            player.Hurt -= OnHurt;
            player.Froze -= OnFroze;
            player.Balls.Caught -= OnCaught;
            player.Balls.BallTypeChanged -= OnBallTypeChanged;
            player.Health.Died -= OnDied;
        }

        /// <summary>В руке — модель того мяча, которым игрок сейчас бросает.</summary>
        void OnBallTypeChanged()
        {
            var prefab = player.Balls.BallPrefab;
            if (!handBall || !prefab)
                return;
            var source = prefab.GetComponentInChildren<MeshFilter>();
            if (source && handBall.TryGetComponent(out MeshFilter target))
                target.sharedMesh = source.sharedMesh;
            _handBallScale = Vector3.one * (prefab.Definition.radius * 2f);
        }

        void LateUpdate()
        {
            var balls = player.Balls;
            bool dead = player.IsDead;

            if (handBall)
            {
                handBall.enabled = !dead && balls.Balls > 0;
                float pulse = balls.Charge01 >= 1f ? 1f + 0.12f * Mathf.Sin(Time.time * 30f) : 1f;
                handBall.transform.localScale = _handBallScale * ((1f + balls.Charge01 * 0.45f) * pulse);
                float charge = balls.Charge01 >= 1f ? 0.7f : balls.Charge01 * 0.3f;
                // Горячий мяч тлеет, как уголёк, — перебегает от красного к жёлтому.
                Color glow = balls.CatchPerksReady
                    ? Color.Lerp(hotColor, candleColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 9f))
                    : Color.Lerp(candleColor, Color.white, 0.3f + 0.3f * Mathf.Sin(Time.time * 12f));
                bool glowing = balls.CandleReady || balls.CatchPerksReady;
                handBall.GetPropertyBlock(_block);
                if (_handBallPalette)
                {
                    // Модель мяча своего цвета; заряд, «свечка» и горячая картошка перекрашивают её поверх.
                    _block.SetColor(PaletteShader.TintColor, glowing
                        ? PaletteShader.Tint(glow, 0.75f)
                        : PaletteShader.Tint(chargedColor, charge));
                }
                else
                {
                    _block.SetColor(BaseColorId, glowing ? glow : Color.Lerp(ballColor, chargedColor, charge));
                }
                handBall.SetPropertyBlock(_block);
            }

            // Подкат: в рывке игрок едет ногами вперёд, откинувшись назад.
            if (body && !dead)
            {
                bool sliding = player.Motor.IsDashing && player.Modifiers.TackleDamage > 0;
                if (sliding || _slide > 0f)
                {
                    _slide = Mathf.MoveTowards(_slide, sliding ? 1f : 0f, Time.deltaTime * 12f);
                    body.localRotation = Quaternion.Euler(-slideLean * _slide, 0f, 0f) * _bodyRotation;
                    body.localPosition = _bodyPosition + Vector3.down * (slideDrop * _slide);
                }
            }

            if (catchRing)
            {
                var line = catchRing.Line;
                if (!dead && balls.IsCatching)
                {
                    line.enabled = true;
                    catchRing.Radius = balls.CatchRadius;
                    line.startColor = line.endColor = catchActiveColor;
                    line.widthMultiplier = 0.12f;
                }
                else if (!dead && balls.CatchOnCooldown)
                {
                    line.enabled = true;
                    catchRing.Radius = balls.CatchRadius * Mathf.Max(0.15f, balls.CatchCooldown01);
                    line.startColor = line.endColor = catchCooldownColor;
                    line.widthMultiplier = 0.05f;
                }
                else
                {
                    line.enabled = false;
                }
            }

            bool hidden = !dead && player.Health.IsInvulnerable && Mathf.Repeat(Time.time, 0.12f) < 0.05f;
            foreach (var r in blinkRenderers)
                if (r)
                    r.enabled = !hidden;

            if (dashTrail)
                dashTrail.emitting = player.Motor.IsDashing;
        }

        void OnHurt(HitInfo hit)
        {
            if (hitFlash)
                hitFlash.Flash(new Color(1f, 0.25f, 0.2f), 0.2f);
        }

        void OnCaught(CatchInfo info)
        {
            if (hitFlash)
                hitFlash.Flash(info.Candle ? candleColor : catchActiveColor, 0.15f);
        }

        /// <summary>«Замри!»: от игрока расходится волна.</summary>
        void OnFroze()
        {
            if (freezeWave)
                PoolService.Spawn(freezeWave, player.transform.position + Vector3.up * 0.05f, Quaternion.identity).Play(freezeWaveRadius);
        }

        void OnDied(HitInfo hit)
        {
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.3f);
            if (body)
            {
                // Упал на спину от удара.
                body.localRotation = Quaternion.Euler(-80f, 0f, 0f) * _bodyRotation;
                body.localPosition = new Vector3(_bodyPosition.x, 0.35f, _bodyPosition.z);
            }
        }
    }
}

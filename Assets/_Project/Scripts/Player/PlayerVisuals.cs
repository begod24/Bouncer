using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    public sealed class PlayerVisuals : MonoBehaviour
    {
        static readonly int BaseColorId = PaletteShader.BaseColor;

        [SerializeField] PlayerController player;
        [Tooltip("Мигают при неуязвимости. Модель ребёнка отдаёт свои сама (PlayerKid)")]
        [SerializeField] Renderer[] blinkRenderers;
        [SerializeField] Renderer handBall;
        [Tooltip("Мяч в руке меньше настоящего: настоящий крупнее головы ребёнка")]
        [SerializeField] float heldBallScale = 0.8f;
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
        [Tooltip("Начало окна ловли: пойманный сейчас мяч будет пойман идеально")]
        [SerializeField] Color catchPerfectColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] Color catchCooldownColor = new(1f, 1f, 1f, 0.25f);
        [Tooltip("Сильный мяч выбило из рук")]
        [SerializeField] Color fumbleColor = new(0.8f, 0.8f, 0.8f);

        MaterialPropertyBlock _block;
        Vector3 _handBallScale;
        bool _handBallPalette;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (handBall)
            {
                _handBallScale = handBall.transform.localScale;
                _handBallPalette = PaletteShader.Supports(handBall.sharedMaterial);
            }
        }

        public void SetBodyRenderers(Renderer[] renderers) => blinkRenderers = renderers ?? System.Array.Empty<Renderer>();

        void Start()
        {
            player.Hurt += OnHurt;
            player.Froze += OnFroze;
            player.Balls.Caught += OnCaught;
            player.Balls.Fumbled += OnFumbled;
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
            player.Balls.Fumbled -= OnFumbled;
            player.Balls.BallTypeChanged -= OnBallTypeChanged;
            player.Health.Died -= OnDied;
        }

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
                float charge01 = player.Action.Charge01;
                float pulse = charge01 >= 1f ? 1f + 0.12f * Mathf.Sin(Time.time * 30f) : 1f;
                handBall.transform.localScale = _handBallScale * (heldBallScale * (1f + charge01 * 0.45f) * pulse);
                float charge = charge01 >= 1f ? 0.7f : charge01 * 0.3f;
                Color glow = balls.CatchPerksReady
                    ? Color.Lerp(hotColor, candleColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 9f))
                    : Color.Lerp(candleColor, Color.white, 0.3f + 0.3f * Mathf.Sin(Time.time * 12f));
                bool glowing = balls.CandleReady || balls.CatchPerksReady || balls.PassReady;
                handBall.GetPropertyBlock(_block);
                if (_handBallPalette)
                {
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

            if (catchRing)
            {
                var line = catchRing.Line;
                if (!dead && balls.IsCatching)
                {
                    bool perfect = balls.IsCatchPerfect;
                    line.enabled = true;
                    catchRing.Radius = balls.CatchRadius;
                    catchRing.Arc = balls.CatchHalfAngle * 2f;
                    line.startColor = line.endColor = perfect ? catchPerfectColor : catchActiveColor;
                    line.widthMultiplier = perfect ? 0.16f : 0.12f;
                }
                else if (!dead && balls.CatchOnCooldown)
                {
                    line.enabled = true;
                    catchRing.Arc = 360f;
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
                dashTrail.emitting = player.Action.Dashing;
        }

        void OnHurt(HitInfo hit)
        {
            if (hitFlash)
                hitFlash.Flash(new Color(1f, 0.25f, 0.2f), 0.2f);
        }

        void OnCaught(CatchInfo info)
        {
            if (hitFlash)
                hitFlash.Flash(info.Candle || info.Perfect ? candleColor : catchActiveColor, 0.15f);
        }

        void OnFumbled()
        {
            if (hitFlash)
                hitFlash.Flash(fumbleColor, 0.12f);
        }

        void OnFroze()
        {
            if (freezeWave)
                PoolService.Spawn(freezeWave, player.transform.position + Vector3.up * 0.05f, Quaternion.identity).Play(freezeWaveRadius);
        }

        void OnDied(HitInfo hit)
        {
            if (hitFlash)
                hitFlash.Flash(Color.white, 0.3f);
        }
    }
}

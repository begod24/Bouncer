using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Наводящий фонарик на тёмных аренах (стройка, «Гасит свет» в финале): пока кнопка зажата, светит лучом
    /// по прицелу. В луче тень твёрдая и горит (теряет попадание раз в flashlightBurnInterval), манекенов луч
    /// тоже видит — это свет (<see cref="LightBeams"/>). Заряд кончается за flashlightBattery секунд и
    /// восстанавливается, пока фонарик выключен. Карточка «Фонарик» делает луч длиннее и заряд дольше.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFlashlight : MonoBehaviour, ILightBeam
    {
        [Tooltip("Прожектор луча. Пусто — создаётся сам")]
        [SerializeField] Light beam;
        [Tooltip("Высота фонарика над ногами, м")]
        [SerializeField] float height = 1.2f;
        [Tooltip("Яркость луча (URP: спадает с квадратом расстояния)")]
        [SerializeField] float intensity = 90f;
        [SerializeField] Color color = new(1f, 0.95f, 0.78f);

        PlayerController _player;
        float _charge = 1f;
        float _nextBurn;
        bool _on;

        /// <summary>Фонарик можно включить здесь и сейчас: арена тёмная.</summary>
        public bool Available => DarkArena.Active;
        /// <summary>Заряд: 1 — полный, 0 — пустой.</summary>
        public float Charge01 => _charge;
        public bool IsOn => _on;

        public bool BeamOn => _on;
        public Vector3 BeamOrigin => transform.position;
        public Vector3 BeamDirection
        {
            get
            {
                Vector3 direction = _player && _player.Aim != null ? _player.Aim.Direction : transform.forward;
                direction.y = 0f;
                return direction.sqrMagnitude > 1e-4f ? direction.normalized : transform.forward;
            }
        }
        public float BeamRange => Stats.flashlightRange + (HasCard ? Stats.flashlightCardRange : 0f);
        public float BeamHalfAngle => Stats.flashlightHalfAngle;

        PlayerStats Stats => _player.Stats;
        bool HasCard => _player.Modifiers.LanternRadius > 0f;
        float Battery => Stats.flashlightBattery + (HasCard ? Stats.flashlightCardBattery : 0f);

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            if (beam == null)
            {
                var go = new GameObject("FlashlightBeam");
                go.transform.SetParent(transform, false);
                beam = go.AddComponent<Light>();
                beam.type = LightType.Spot;
                beam.shadows = LightShadows.None;
            }
            beam.color = color;
            beam.enabled = false;
        }

        void OnEnable() => LightBeams.Register(this);

        void OnDisable()
        {
            LightBeams.Unregister(this);
            _on = false;
            if (beam)
                beam.enabled = false;
        }

        void Update()
        {
            if (GameFeel.Paused || _player == null)
                return;
            float dt = Time.deltaTime;
            bool canUse = Available && !_player.IsDead && !_player.IsScripted && GameSession.IsPlayerActive;
            bool wants = canUse && _player.LastIntent.FlashlightHeld && _charge > 0f;
            if (wants != _on)
            {
                _on = wants;
                GameEvents.PlaySound(_on ? SoundCue.UiMove : SoundCue.LampOut, transform.position);
                _nextBurn = Time.time + Stats.flashlightBurnInterval;
            }

            if (_on)
            {
                _charge = Mathf.Max(0f, _charge - dt / Mathf.Max(0.1f, Battery));
                if (_charge <= 0f)
                    _on = false;
                if (Time.time >= _nextBurn)
                {
                    _nextBurn = Time.time + Stats.flashlightBurnInterval;
                    Burn();
                }
            }
            else
            {
                _charge = Mathf.Min(1f, _charge + dt / Mathf.Max(0.1f, Stats.flashlightRecharge));
            }

            if (beam)
            {
                beam.enabled = _on;
                if (_on)
                {
                    Vector3 direction = BeamDirection;
                    beam.transform.SetPositionAndRotation(transform.position + Vector3.up * height,
                        Quaternion.LookRotation(direction + Vector3.down * 0.18f));
                    beam.range = BeamRange + 2f;
                    beam.spotAngle = BeamHalfAngle * 2f;
                    beam.innerSpotAngle = BeamHalfAngle * 1.2f;
                    // Мерцание на исходе заряда.
                    float flicker = _charge < 0.2f ? 0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 25f, 0f) : 1f;
                    beam.intensity = intensity * flicker;
                }
            }
        }

        /// <summary>Тени в луче теряют попадание.</summary>
        void Burn()
        {
            var all = Targetable.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var target = all[i];
                if (target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                if (!target.TryGetComponent(out IBurnable burnable) || !burnable.BurnsInLight)
                    continue;
                if (!LightBeams.Contains(target.Position) || !target.TryGetComponent(out IDamageable damageable))
                    continue;
                Vector3 away = target.Position - transform.position;
                away.y = 0f;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = 1,
                    Point = target.AimPoint,
                    Direction = away.sqrMagnitude > 1e-4f ? away.normalized : transform.forward,
                    Force = 3f,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Burn,
                });
            }
        }
    }
}

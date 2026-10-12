using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerCardFx : MonoBehaviour
    {
        public const int PopupThree = 2;
        public const int PopupBang = 3;
        public const int PopupFreeze = 4;
        public const int PopupHeart = 5;

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [Header("Тамагочи")]
        [Tooltip("Яйцо тамагочи над головой (только у себя)")]
        [SerializeField] Transform tamagotchi;
        [SerializeField] Renderer tamagotchiScreen;
        [Tooltip("Рожицы: голодный, ест, сытый")]
        [SerializeField] Texture2D[] tamagotchiFaces;
        [SerializeField] ParticleBurst pixelBurst;

        [Header("Кольца и надписи")]
        [SerializeField] BillboardPopup chalkPopup;
        [SerializeField] ExpandingRing handRing;
        [SerializeField] ExpandingRing seaRing;

        [Header("Комбо")]
        [Tooltip("«Чехарда»: пыль при отрыве и приземлении")]
        [SerializeField] ParticleBurst leapDust;
        [Tooltip("«Пенальти»: вспышка от удара по мячу")]
        [SerializeField] ParticleBurst kickFlash;
        [Tooltip("«Солнышко»: нитка от руки к мячу — меловая линия")]
        [SerializeField] Material stringMaterial;
        [SerializeField] Color stringColor = new(1f, 1f, 1f, 0.85f);
        [SerializeField, Min(0.005f)] float stringWidth = 0.035f;

        PlayerController _player;
        MaterialPropertyBlock _block;
        int _face = -1;
        float _tamagotchiScale;
        // «Второе дыхание»: меловой счёт «три, два, раз» над выбитым игроком
        float _countStart = -1f;
        float _countLength;
        int _countShown;
        LineRenderer _string;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _block = new MaterialPropertyBlock();
            if (stringMaterial)
            {
                var go = new GameObject("YoyoString", typeof(LineRenderer));
                go.transform.SetParent(transform, false);
                _string = go.GetComponent<LineRenderer>();
                _string.sharedMaterial = stringMaterial;
                _string.useWorldSpace = true;
                _string.positionCount = 2;
                _string.widthMultiplier = stringWidth;
                _string.startColor = _string.endColor = stringColor;
                _string.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _string.enabled = false;
            }
        }

        void OnEnable() => PlayerFx.Played += OnPlayed;

        void OnDisable() => PlayerFx.Played -= OnPlayed;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (tamagotchi)
            {
                float want = _player.IsLocal && _player.HasTamagotchi && !_player.IsDead ? 1f : 0f;
                _tamagotchiScale = Mathf.MoveTowards(_tamagotchiScale, want, dt * 4f);
                bool on = _tamagotchiScale > 0.01f;
                if (tamagotchi.gameObject.activeSelf != on)
                    tamagotchi.gameObject.SetActive(on);
                if (on)
                {
                    tamagotchi.localScale = Vector3.one * _tamagotchiScale;
                    var camera = Camera.main;
                    if (camera)
                        tamagotchi.rotation = Quaternion.LookRotation(camera.transform.forward, Vector3.up);
                    tamagotchi.position = transform.position + Vector3.up * (2.25f + 0.05f * Mathf.Sin(Time.time * 4f));
                    float food = _player.TamagotchiFood01;
                    int face = food >= 1f ? 2 : food > 0f ? 1 : 0;
                    if (face != _face && tamagotchiScreen && tamagotchiFaces != null && face < tamagotchiFaces.Length)
                    {
                        _face = face;
                        tamagotchiScreen.GetPropertyBlock(_block);
                        _block.SetTexture(BaseMapId, tamagotchiFaces[face]);
                        tamagotchiScreen.SetPropertyBlock(_block);
                    }
                }
            }
            UpdateCountdown();
            UpdateString();
        }

        /// <summary>«Солнышко» (йо-йо): пока мяч на нитке летит, от руки к нему тянется нитка.</summary>
        void UpdateString()
        {
            if (_string == null)
                return;
            Ball yoyo = null;
            if (!_player.IsDead)
            {
                var balls = Ball.Active;
                for (int i = 0; i < balls.Count; i++)
                {
                    var ball = balls[i];
                    if (ball.IsYoyoString && ball.Owner == gameObject && ball.State is BallState.Live or BallState.Returning)
                    {
                        yoyo = ball;
                        break;
                    }
                }
            }
            _string.enabled = yoyo != null;
            if (yoyo == null)
                return;
            _string.SetPosition(0, transform.position + Vector3.up * 1.1f + transform.forward * 0.25f);
            _string.SetPosition(1, yoyo.Position + yoyo.VisualOffset);
        }

        void UpdateCountdown()
        {
            if (_countStart < 0f)
                return;
            if (!_player.IsDead)
            {
                _countStart = -1f;
                return;
            }
            // три шага на всё время: «три!», «два», «раз»
            int step = Mathf.FloorToInt((Time.time - _countStart) / Mathf.Max(0.1f, _countLength / 3f));
            if (step >= 3)
            {
                _countStart = -1f;
                return;
            }
            if (step < _countShown)
                return;
            _countShown = step + 1;
            BillboardPopup.Show(chalkPopup, transform.position + Vector3.up * 1.6f, PopupThree - step,
                step == 2 ? new Color(1f, 0.85f, 0.3f) : Color.white);
        }

        void OnPlayed(PlayerController player, PlayerFxKind kind, Vector3 a, Vector3 b)
        {
            if (player != _player)
                return;
            switch (kind)
            {
                case PlayerFxKind.TamagotchiSave:
                    if (pixelBurst)
                        PoolService.Spawn(pixelBurst, a + Vector3.up * 2.2f, Quaternion.identity);
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.4f, PopupHeart, new Color(1f, 0.45f, 0.5f));
                    break;
                case PlayerFxKind.CountThree:
                    int step = Mathf.Max(1, (int)b.x);
                    bool last = step >= (int)b.y;
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.3f, last ? PopupThree : Mathf.Min(step - 1, 1),
                        last ? new Color(1f, 0.85f, 0.3f) : Color.white);
                    break;
                case PlayerFxKind.CircleGuard:
                    Ring(handRing, a, 4f, Color.white);
                    break;
                case PlayerFxKind.SeaFigure:
                    Ring(seaRing, a, b.y > 0f ? b.y : 6f, new Color(0.55f, 0.85f, 1f));
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.4f, PopupFreeze, new Color(0.7f, 0.9f, 1f));
                    break;
                case PlayerFxKind.Whistle:
                    Ring(seaRing, a, b.y > 0f ? b.y : 6f, new Color(0.85f, 0.95f, 1f));
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.4f, PopupFreeze, Color.white);
                    break;
                case PlayerFxKind.SecondWind:
                    _countStart = Time.time;
                    _countLength = b.x > 0f ? b.x : 3f;
                    _countShown = 0;
                    break;
                case PlayerFxKind.Penalty:
                    if (kickFlash)
                        PoolService.Spawn(kickFlash, a + Vector3.up * 0.2f, Quaternion.identity);
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 1.6f, PopupBang, new Color(1f, 0.85f, 0.3f));
                    break;
                case PlayerFxKind.LeapStart:
                    if (leapDust)
                        PoolService.Spawn(leapDust, a + Vector3.up * 0.1f, Quaternion.identity).Play(0.7f);
                    break;
                case PlayerFxKind.LeapLand:
                    if (leapDust)
                        PoolService.Spawn(leapDust, a + Vector3.up * 0.1f, Quaternion.identity).Play(1.2f);
                    Ring(handRing, a, b.x > 0f ? b.x : 2f, new Color(1f, 0.85f, 0.3f));
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.2f, PopupBang, Color.white);
                    break;
                case PlayerFxKind.SecondWindUp:
                    _countStart = -1f;
                    Ring(handRing, a, 2.5f, new Color(1f, 0.85f, 0.3f));
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.4f, PopupHeart, new Color(1f, 0.45f, 0.5f));
                    break;
            }
        }


        static void Ring(ExpandingRing prefab, Vector3 at, float radius, Color color)
        {
            if (prefab == null)
                return;
            var ring = PoolService.Spawn(prefab, at + Vector3.up * 0.06f, Quaternion.identity);
            ring.Tint(color);
            ring.Play(radius);
        }
    }
}

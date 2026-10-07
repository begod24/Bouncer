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

        [Tooltip("Цвета игроков по номерам: кольцо паса — цветом того, кто бросил")]
        [SerializeField] Color[] slotColors =
        {
            new(0.95f, 0.85f, 0.3f), new(0.4f, 0.75f, 1f), new(0.95f, 0.45f, 0.6f), new(0.5f, 0.9f, 0.45f),
        };

        [Header("Жвачный пузырь")]
        [Tooltip("Розовый пузырь перед лицом: виден, пока заряжен")]
        [SerializeField] Transform gumBubble;
        [SerializeField] ParticleBurst gumPop;
        [SerializeField] GumSpot gumSpot;

        [Header("Тамагочи")]
        [Tooltip("Яйцо тамагочи над головой (только у себя)")]
        [SerializeField] Transform tamagotchi;
        [SerializeField] Renderer tamagotchiScreen;
        [Tooltip("Рожицы: голодный, ест, сытый")]
        [SerializeField] Texture2D[] tamagotchiFaces;
        [SerializeField] ParticleBurst pixelBurst;

        [Header("Кольца и надписи")]
        [SerializeField] BillboardPopup chalkPopup;
        [SerializeField] ExpandingRing passRing;
        [SerializeField] ExpandingRing handRing;
        [SerializeField] ExpandingRing seaRing;
        [Tooltip("Ускорение от мелового крестика: след за ногами")]
        [SerializeField] TrailRenderer chalkTrail;

        PlayerController _player;
        MaterialPropertyBlock _block;
        int _face = -1;
        float _bubbleScale;
        Vector3 _bubbleRest;
        float _tamagotchiScale;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _block = new MaterialPropertyBlock();
            if (gumBubble)
                _bubbleRest = gumBubble.localScale;
        }

        void OnEnable() => PlayerFx.Played += OnPlayed;

        void OnDisable() => PlayerFx.Played -= OnPlayed;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (gumBubble)
            {
                float want = _player.ShowsGumBubble ? 1f : 0f;
                _bubbleScale = Mathf.MoveTowards(_bubbleScale, want, dt * (want > _bubbleScale ? 2f : 8f));
                bool on = _bubbleScale > 0.01f;
                if (gumBubble.gameObject.activeSelf != on)
                    gumBubble.gameObject.SetActive(on);
                if (on)
                {
                    float breath = 1f + 0.08f * Mathf.Sin(Time.time * 3.1f);
                    gumBubble.localScale = _bubbleRest * (_bubbleScale * breath);
                }
            }
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
            if (chalkTrail)
                chalkTrail.emitting = _player.Motor.IsBoosted && !_player.IsDead;
        }

        void OnPlayed(PlayerController player, PlayerFxKind kind, Vector3 a, Vector3 b)
        {
            if (player != _player)
                return;
            switch (kind)
            {
                case PlayerFxKind.GumBubblePop:
                    if (gumPop)
                        PoolService.Spawn(gumPop, a + Vector3.up * 1.2f, Quaternion.identity);
                    if (Online.IsHost && gumSpot)
                        GumSpot.Drop(gumSpot, a + Vector3.up * 0.5f);
                    break;
                case PlayerFxKind.TamagotchiSave:
                    if (pixelBurst)
                        PoolService.Spawn(pixelBurst, a + Vector3.up * 2.2f, Quaternion.identity);
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.4f, PopupHeart, new Color(1f, 0.45f, 0.5f));
                    break;
                case PlayerFxKind.PassCatch:
                    Ring(passRing, a, 1.6f, SlotColor((int)b.x));
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
                    Ring(seaRing, a, 6f, new Color(0.55f, 0.85f, 1f));
                    BillboardPopup.Show(chalkPopup, a + Vector3.up * 2.4f, PopupFreeze, new Color(0.7f, 0.9f, 1f));
                    break;
            }
        }

        Color SlotColor(int slot) => slotColors != null && slotColors.Length > 0 ? slotColors[Mathf.Clamp(slot, 0, slotColors.Length - 1)] : Color.white;

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

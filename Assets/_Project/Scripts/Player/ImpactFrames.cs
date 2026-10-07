using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    // «Импакт-кадр»: на 2–3 кадра в точке попадания вспыхивает комиксная звёздочка, плюс эффект идеальной поимки.
    // Висит на префабе игрока. Попадания по всем Health показывает один экземпляр (первый включённый),
    // поимку — каждый игрок свою. По сети не шлётся: у гостя урон приходит через Health.Mirror, звёздочка рисуется там же.
    [RequireComponent(typeof(PlayerController))]
    public sealed class ImpactFrames : MonoBehaviour
    {
        static ImpactFrames s_active;
        static readonly Dictionary<int, float> s_lastHit = new();

        [Header("Попадания")]
        [Tooltip("Звёздочка, когда бьют врага (или что-то ломается)")]
        [SerializeField] ParticleBurst enemyHit;
        [Tooltip("Звёздочка, когда попали по ребёнку")]
        [SerializeField] ParticleBurst playerHit;
        [Tooltip("Насколько крупнее заряженный удар и добивание")]
        [SerializeField, Min(1f)] float strongScale = 1.45f;
        [Tooltip("Не чаще, чем раз в столько секунд по одной цели — площадные удары не рябят")]
        [SerializeField, Min(0f)] float perTargetInterval = 0.07f;
        [Tooltip("Звёздочку выносим из модели к камере, м")]
        [SerializeField, Min(0f)] float towardCamera = 0.35f;

        [Header("Идеальная поимка")]
        [SerializeField] ParticleBurst perfectCatch;
        [SerializeField] ExpandingRing perfectRing;
        [SerializeField] float perfectRingRadius = 1.3f;

        PlayerController _player;
        Camera _camera;

        void Awake() => _player = GetComponent<PlayerController>();

        void OnEnable()
        {
            PlayerFx.Played += OnPlayerFx;
            if (s_active == null)
            {
                s_active = this;
                Health.AnyDamaged += OnAnyDamaged;
            }
        }

        void OnDisable()
        {
            PlayerFx.Played -= OnPlayerFx;
            if (s_active == this)
            {
                Health.AnyDamaged -= OnAnyDamaged;
                s_active = null;
                foreach (var other in FindObjectsByType<ImpactFrames>(FindObjectsSortMode.None))
                {
                    if (other == this || !other.isActiveAndEnabled)
                        continue;
                    s_active = other;
                    Health.AnyDamaged += other.OnAnyDamaged;
                    break;
                }
            }
        }

        void OnAnyDamaged(Health health, HitInfo hit)
        {
            if (health == null)
                return;
            int id = health.GetInstanceID();
            if (s_lastHit.TryGetValue(id, out float last) && Time.time - last < perTargetInterval)
                return;
            s_lastHit[id] = Time.time;
            if (s_lastHit.Count > 256)
                s_lastHit.Clear();

            var target = health.GetComponent<Targetable>();
            bool player = target && target.Team == Team.Player;
            var prefab = player ? playerHit : enemyHit;
            if (prefab == null)
                return;
            Vector3 point = hit.Point != Vector3.zero ? hit.Point : target ? target.AimPoint : health.transform.position + Vector3.up;
            if (!_camera)
                _camera = Camera.main;
            if (_camera)
                point += (_camera.transform.position - point).normalized * towardCamera;
            var burst = PoolService.Spawn(prefab, point, Quaternion.identity);
            bool strong = hit.Has(HitFlags.Charged) || health.IsDead;
            burst.transform.localScale = Vector3.one * (strong ? strongScale : 1f);
        }

        void OnPlayerFx(PlayerController player, PlayerFxKind kind, Vector3 a, Vector3 b)
        {
            if (player != _player || kind != PlayerFxKind.PerfectCatch)
                return;
            if (perfectCatch)
                PoolService.Spawn(perfectCatch, a, Quaternion.identity);
            if (perfectRing)
            {
                var ring = PoolService.Spawn(perfectRing, new Vector3(a.x, 0.05f, a.z), Quaternion.identity);
                ring.Play(perfectRingRadius);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_active = null;
            s_lastHit.Clear();
        }
    }
}

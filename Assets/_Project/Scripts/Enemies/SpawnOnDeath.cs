using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Выбитый враг выпускает других: золотая матрёшка раскрывается и из неё выходят обычные, юла-«спутник»
    /// раскалывается на мини-юлы. Когда арена пройдена и враги исчезают, никто не выходит.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class SpawnOnDeath : MonoBehaviour
    {
        [SerializeField] GameObject prefab;
        [SerializeField, Min(1)] int count = 2;
        [Tooltip("На каком расстоянии в стороны появляются")]
        [SerializeField] float spread = 0.8f;
        [Tooltip("С какой скоростью разлетаются (если у них есть физическое тело)")]
        [SerializeField] float impulse = 4f;
        [Tooltip("Неваляшки после появления сначала качаются")]
        [SerializeField] float stun = 0.6f;

        void Awake() => GetComponent<Health>().Died += OnDied;

        void OnDied(HitInfo hit)
        {
            if (prefab == null || hit.Has(HitFlags.Despawn))
                return;
            Vector3 origin = transform.position;
            Vector3 away = hit.Direction;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-4f)
                away = transform.forward;
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away);
            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0f : i / (count - 1f) * 2f - 1f;
                var child = PoolService.Spawn(prefab, origin + side * (t * spread) + Vector3.up * 0.1f, transform.rotation);
                if (child.TryGetComponent(out Rigidbody body) && !body.isKinematic)
                    body.AddForce((side * t + away * 0.5f).normalized * impulse + Vector3.up * (impulse * 0.4f), ForceMode.VelocityChange);
                if (child.TryGetComponent(out RolyPolyEnemy rolyPoly))
                    rolyPoly.Stun(stun);
                if (child.TryGetComponent(out IDamageable damageable) && child.TryGetComponent(out TopEnemy _))
                {
                    // Мини-юлы разлетаются, как от удара.
                    damageable.ApplyHit(new HitInfo
                    {
                        Damage = 0,
                        Direction = (side * t + away * 0.5f).normalized,
                        SourceTeam = Team.Player,
                        Flags = HitFlags.None,
                    });
                }
            }
            GameEvents.PlaySound(SoundCue.BossSplit, origin);
        }
    }
}

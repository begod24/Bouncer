using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Arena
{
    /// <summary>
    /// Гора коробок на барахолке. Обычный мяч отскакивает от неё, как от стены. Сильный (заряженный) мяч или таран
    /// Трансформера её рушат: коробки валятся в сторону удара, врагов за горой завал бьёт и оглушает. Потом коробки
    /// сжимаются и пропадают — место становится проходимым.
    /// По сети рушит и бьёт завалом хозяин комнаты; у гостей гора валится по его вести (<see cref="BreakEvents"/>).
    /// </summary>
    public sealed class TopplePile : MonoBehaviour, IBallTarget, IBreakable
    {
        [Tooltip("Коробки (каждая — отдельная часть модели)")]
        [SerializeField] Transform[] boxes;
        [SerializeField] Collider[] blockers;
        [SerializeField] NavMeshObstacle obstacle;
        [Tooltip("Завал: центр — столько метров от горы в сторону удара")]
        [SerializeField] float fallDistance = 1.4f;
        [SerializeField] float fallRadius = 2f;
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] float knockback = 7f;
        [SerializeField] float boxMass = 1.5f;
        [SerializeField] float lifetime = 4f;
        [SerializeField] float shrinkTime = 0.6f;

        Rigidbody[] _bodies;
        Vector3[] _scales;
        float _brokenAt;

        public bool IsBroken { get; private set; }

        public BallContactResult OnBallContact(Ball ball, in RaycastHit hit)
        {
            if (IsBroken)
                return BallContactResult.PassThrough;
            if (!ball.Stats.Has(HitFlags.Charged))
                return BallContactResult.Bounce;
            Break(ball.Velocity, 5f);
            return BallContactResult.Bounce;
        }

        public void Break(Vector3 direction, float force)
        {
            if (IsBroken)
                return;
            IsBroken = true;
            _brokenAt = Time.time;
            if (blockers != null)
                foreach (var c in blockers)
                    if (c)
                        c.enabled = false;
            if (obstacle)
                obstacle.enabled = false;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector3.forward;

            _bodies = new Rigidbody[boxes.Length];
            _scales = new Vector3[boxes.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                var box = boxes[i];
                if (!box)
                    continue;
                _scales[i] = box.localScale;
                box.gameObject.layer = Layers.Ragdoll;
                if (box.TryGetComponent(out MeshFilter filter) && filter.sharedMesh && !box.TryGetComponent(out Collider _))
                {
                    var c = box.gameObject.AddComponent<BoxCollider>();
                    c.center = filter.sharedMesh.bounds.center;
                    c.size = filter.sharedMesh.bounds.size;
                }
                var body = box.gameObject.AddComponent<Rigidbody>();
                body.mass = boxMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // Верхние коробки летят дальше.
                float height = Mathf.Clamp01((box.position.y - transform.position.y) / 1.8f);
                body.linearVelocity = direction * (force * (0.5f + height)) + Vector3.up * (1f + 2f * height)
                                      + Random.insideUnitSphere * 0.8f;
                body.angularVelocity = Random.insideUnitSphere * 5f;
                _bodies[i] = body;
            }

            // Завал бьёт тех, кто стоит за горой (по сети — у хозяина: враги там настоящие).
            Vector3 center = transform.position + direction * fallDistance;
            foreach (var t in Targetable.All)
            {
                if (NetHooks.IsGuest)
                    break;
                if (t.Team != Team.Enemy || !t.IsAlive)
                    continue;
                Vector3 delta = t.Position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude > fallRadius * fallRadius || !t.TryGetComponent(out IDamageable damageable))
                    continue;
                damageable.ApplyHit(new HitInfo
                {
                    Damage = damage,
                    Point = t.AimPoint,
                    Direction = direction,
                    Force = knockback,
                    SourceTeam = Team.Player,
                    Source = gameObject,
                    Flags = HitFlags.Area,
                });
            }
            GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
            GameEvents.PlaySound(SoundCue.SackSpill, transform.position);
            GameFeel.Shake(0.35f);
            BreakEvents.Raise(this, direction, force);
        }

        void Update()
        {
            if (!IsBroken || _bodies == null)
                return;
            float age = Time.time - _brokenAt;
            if (age < lifetime)
                return;
            float k = 1f - (age - lifetime) / Mathf.Max(0.01f, shrinkTime);
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (!_bodies[i])
                    continue;
                if (k <= 0f)
                    _bodies[i].gameObject.SetActive(false);
                else
                    _bodies[i].transform.localScale = _scales[i] * k;
            }
            if (k <= 0f)
                enabled = false;
        }
    }
}

using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Arena
{
    /// <summary>
    /// Снос предмета (прилавок барахолки): пока цел — обычное препятствие (коллайдеры, дыра в NavMesh через
    /// NavMeshObstacle). Снесённый — части модели разлетаются кусками с физикой, коллайдеры и препятствие
    /// выключаются, через пару секунд куски сжимаются и пропадают: место становится проходимым.
    /// </summary>
    public sealed class BreakableProp : MonoBehaviour, IBreakable
    {
        [Tooltip("Части модели, которые разлетаются (каждая — кусок)")]
        [SerializeField] Transform[] parts;
        [Tooltip("Что перестаёт мешать после сноса")]
        [SerializeField] Collider[] blockers;
        [SerializeField] NavMeshObstacle obstacle;
        [SerializeField] float pieceMass = 3f;
        [SerializeField] float lifetime = 2.5f;
        [SerializeField] float shrinkTime = 0.5f;
        [Tooltip("Разброс: доля силы, уходящая вверх и в стороны")]
        [SerializeField] float scatter = 0.6f;

        Rigidbody[] _bodies;
        Vector3[] _scales;
        float _brokenAt;

        public bool IsBroken { get; private set; }

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
            _bodies = new Rigidbody[parts.Length];
            _scales = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!part)
                    continue;
                _scales[i] = part.localScale;
                part.gameObject.layer = Layers.Ragdoll;
                if (part.TryGetComponent(out MeshFilter filter) && filter.sharedMesh && !part.TryGetComponent(out Collider _))
                {
                    var box = part.gameObject.AddComponent<BoxCollider>();
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = Vector3.Max(filter.sharedMesh.bounds.size, Vector3.one * 0.08f);
                }
                var body = part.gameObject.AddComponent<Rigidbody>();
                body.mass = pieceMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                Vector3 spread = Random.insideUnitSphere * scatter;
                spread.y = Mathf.Abs(spread.y) + 0.4f;
                body.linearVelocity = (direction + spread) * force;
                body.angularVelocity = Random.insideUnitSphere * 8f;
                _bodies[i] = body;
            }
            GameEvents.PlaySound(SoundCue.AreaThud, transform.position);
            GameEvents.PlaySound(SoundCue.DecoyBurst, transform.position);
            GameFeel.Shake(0.3f);
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

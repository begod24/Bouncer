using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>Разлетающиеся части врага после выбивания. Через пару секунд сжимаются и возвращаются в пул.</summary>
    public sealed class Debris : MonoBehaviour, IPoolable
    {
        [SerializeField] Rigidbody[] pieces;
        [SerializeField] float lifetime = 2.2f;
        [SerializeField] float shrinkTime = 0.4f;

        Vector3[] _localPositions;
        Quaternion[] _localRotations;
        Vector3[] _localScales;
        float _spawnTime;

        void Awake()
        {
            int count = pieces.Length;
            _localPositions = new Vector3[count];
            _localRotations = new Quaternion[count];
            _localScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var t = pieces[i].transform;
                _localPositions[i] = t.localPosition;
                _localRotations[i] = t.localRotation;
                _localScales[i] = t.localScale;
                pieces[i].interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public void OnSpawned()
        {
            _spawnTime = Time.time;
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                var t = piece.transform;
                t.SetLocalPositionAndRotation(_localPositions[i], _localRotations[i]);
                t.localScale = _localScales[i];
                piece.position = t.position;
                piece.rotation = t.rotation;
                piece.linearVelocity = Vector3.zero;
                piece.angularVelocity = Vector3.zero;
            }
        }

        public void OnDespawned() { }

        public void Burst(Vector3 direction, float force, Vector3 inheritedVelocity)
        {
            foreach (var piece in pieces)
            {
                Vector3 spread = Random.insideUnitSphere * 0.5f;
                spread.y = Mathf.Abs(spread.y);
                piece.linearVelocity = inheritedVelocity + (direction + Vector3.up * 0.8f + spread) * force;
                piece.angularVelocity = Random.insideUnitSphere * 12f;
            }
        }

        void Update()
        {
            float age = Time.time - _spawnTime;
            if (age < lifetime)
                return;
            float k = 1f - (age - lifetime) / Mathf.Max(0.01f, shrinkTime);
            if (k <= 0f)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            for (int i = 0; i < pieces.Length; i++)
                pieces[i].transform.localScale = _localScales[i] * k;
        }
    }
}

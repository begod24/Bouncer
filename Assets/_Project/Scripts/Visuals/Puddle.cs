using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Visuals
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(GroundZone))]
    public sealed class Puddle : MonoBehaviour
    {
        const int Points = 36;

        static Mesh[] s_meshes;

        [Tooltip("Лужа «дышит»: то растекается, то подсыхает. Насколько меняется размер, доля")]
        [SerializeField, Range(0f, 0.5f)] float breathe = 0.3f;
        [Tooltip("Примерно столько секунд длится один разлив или усыхание")]
        [SerializeField, Min(1f)] float breathePeriod = 8f;
        [Tooltip("Насколько при этом вытягивается вдоль одной оси, доля")]
        [SerializeField, Range(0f, 0.3f)] float stretch = 0.1f;

        MeshFilter _filter;
        Vector3 _baseScale;
        float _seed;

        void Awake() => _filter = GetComponent<MeshFilter>();

        public void Place(Vector3 position, Vector2 size, int variant, float yaw)
        {
            _filter.sharedMesh = Meshes()[Mathf.Abs(variant) % Meshes().Length];
            transform.SetPositionAndRotation(position + Vector3.up * 0.015f, Quaternion.Euler(0f, yaw, 0f));
            _baseScale = new Vector3(size.x * 0.5f, 1f, size.y * 0.5f);
            _seed = 3.7f + variant * 11.3f + yaw * 0.07f;
            Breathe();
        }

        void Update() => Breathe();

        // Плавный шум без повторов: каждая лужа живёт в своём темпе. Зона замедления (GroundZone) растёт вместе с ней
        void Breathe()
        {
            if (_baseScale == Vector3.zero)
                return;
            float t = Time.time / breathePeriod;
            float size = 1f + breathe * (Mathf.PerlinNoise(_seed, t) * 2f - 1f);
            float along = stretch * (Mathf.PerlinNoise(_seed + 41.3f, t * 0.7f) * 2f - 1f);
            transform.localScale = new Vector3(_baseScale.x * size * (1f + along), 1f, _baseScale.z * size * (1f - along));
        }

        static Mesh[] Meshes()
        {
            if (s_meshes != null && s_meshes[0] != null)
                return s_meshes;
            var random = new System.Random(1407);
            s_meshes = new Mesh[4];
            for (int i = 0; i < s_meshes.Length; i++)
                s_meshes[i] = Build(random, i);
            return s_meshes;
        }

        static Mesh Build(System.Random random, int index)
        {
            var vertices = new Vector3[Points + 1];
            var normals = new Vector3[Points + 1];
            // UV.x: 0 в центре, 1 на краю — по нему шейдер лужи делает мягкий край
            var uvs = new Vector2[Points + 1];
            var triangles = new int[Points * 3];
            normals[0] = Vector3.up;
            // Контур — сумма трёх синусов по кругу: плавная клякса без углов
            float Phase() => (float)random.NextDouble() * Mathf.PI * 2f;
            float a2 = Phase(), a3 = Phase(), a5 = Phase();
            for (int i = 1; i <= Points; i++)
            {
                float angle = (i - 1) * Mathf.PI * 2f / Points;
                float r = 0.92f + 0.08f * Mathf.Sin(2f * angle + a2) + 0.06f * Mathf.Sin(3f * angle + a3) + 0.03f * Mathf.Sin(5f * angle + a5);
                vertices[i] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(1f, 0f);
            }
            for (int i = 0; i < Points; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % Points + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Puddle_" + index, vertices = vertices, normals = normals, uv = uvs, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

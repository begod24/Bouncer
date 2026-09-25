using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Лужа после дождя: неровное блестящее пятно на асфальте. Игровая часть — <see cref="GroundZone"/> на том же
    /// объекте (круглая): в луже катящийся мяч быстро гаснет, а бег замедляется. Форма строится в коде, размер
    /// задаёт тот, кто кладёт лужу.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(GroundZone))]
    public sealed class Puddle : MonoBehaviour
    {
        const int Points = 14;

        static Mesh[] s_meshes;

        MeshFilter _filter;

        void Awake() => _filter = GetComponent<MeshFilter>();

        /// <summary>Лужа такого размера по X и Z, м, одна из нескольких форм.</summary>
        public void Place(Vector3 position, Vector2 size, int variant)
        {
            _filter.sharedMesh = Meshes()[Mathf.Abs(variant) % Meshes().Length];
            transform.SetPositionAndRotation(position + Vector3.up * 0.015f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            // Меш — лужа радиусом около 1, зона — прямоугольник 2×2 с вписанным эллипсом: масштаб растягивает обоих.
            transform.localScale = new Vector3(size.x * 0.5f, 1f, size.y * 0.5f);
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

        /// <summary>Плоская неровная лужа радиусом около 1 в плоскости XZ, лицом вверх.</summary>
        static Mesh Build(System.Random random, int index)
        {
            var vertices = new Vector3[Points + 1];
            var normals = new Vector3[Points + 1];
            var triangles = new int[Points * 3];
            normals[0] = Vector3.up;
            float start = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 1; i <= Points; i++)
            {
                float angle = start + (i - 1) * Mathf.PI * 2f / Points + ((float)random.NextDouble() - 0.5f) * 0.3f;
                float r = Mathf.Lerp(0.8f, 1.05f, (float)random.NextDouble());
                vertices[i] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                normals[i] = Vector3.up;
            }
            for (int i = 0; i < Points; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % Points + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Puddle_" + index, vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

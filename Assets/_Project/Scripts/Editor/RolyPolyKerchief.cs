using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Bouncer.EditorTools
{
    /// <summary>
    /// Платок неваляшек. Красная голова в белый горошек читалась как мяч (отзывы к 1.0), поэтому голова
    /// «повязана» фиолетовым платком в горошек: красные клетки палитры на меше головы перекрашены, под
    /// подбородком узел с двумя концами, на затылке уголок. Меши собираются из FBX и лежат в
    /// Art/Models/Enemies/Kerchief. После переэкспорта неваляшек из Blender меню нужно запустить снова.
    /// </summary>
    public static class RolyPolyKerchief
    {
        const string Folder = "Assets/_Project/Art/Models/Enemies/Kerchief";
        const string ChildName = "Kerchief";
        const float Cell = 1f / 8f;
        /// <summary>Голова большой неваляшки — обычная ×3 (те же пропорции, плюс трещины и брови).</summary>
        const float BossScale = 3f;

        static readonly Vector2Int Red = new(1, 7);
        static readonly Vector2Int DarkRed = new(5, 7);
        static readonly Vector2Int Purple = new(0, 3);
        static readonly Vector2Int Indigo = new(7, 4);

        [MenuItem("Bouncer/Art/Rebuild Roly-Poly Kerchief")]
        public static void Rebuild()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Models/Enemies", "Kerchief");
            var head = SaveMesh(Recolor(LoadMesh("Assets/_Project/Art/Models/Enemies/Enemy_RolyPoly.fbx", "RolyPoly_Head"), false),
                "RolyPoly_Head_Kerchief");
            var bossHead = SaveMesh(Recolor(LoadMesh("Assets/_Project/Art/Models/Enemies/Boss_BigRolyPoly.fbx", "BigRolyPoly_Head"), true),
                "BigRolyPoly_Head_Kerchief");
            var knot = SaveMesh(BuildKnot(), "RolyPoly_KerchiefKnot");
            AssetDatabase.SaveAssets();

            Dress("Assets/_Project/Prefabs/Enemies/RolyPoly.prefab", "Visual/Head", head, knot, 1f);
            Dress("Assets/_Project/Prefabs/Enemies/RolyPolyDebris.prefab", "HeadPiece/Head", head, knot, 1f);
            foreach (var size in new[] { "Big", "Medium", "Small" })
                Dress($"Assets/_Project/Prefabs/Enemies/Boss_RolyPoly_{size}.prefab", "Visual/Boss_BigRolyPoly/BigRolyPoly_Head",
                    bossHead, knot, BossScale);
            AssetDatabase.SaveAssets();
            Debug.Log("[RolyPolyKerchief] Платок собран: 3 меша, 5 префабов.");
        }

        static Mesh LoadMesh(string path, string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Mesh mesh && mesh.name == name)
                    return mesh;
            throw new System.InvalidOperationException($"Нет меша {name} в {path}");
        }

        /// <summary>Копия головы: красное → фиолетовое, у босса тёмно-красные трещины → индиго.</summary>
        static Mesh Recolor(Mesh source, bool boss)
        {
            var mesh = Object.Instantiate(source);
            var uvs = mesh.uv;
            for (int i = 0; i < uvs.Length; i++)
            {
                var cell = new Vector2Int(Mathf.FloorToInt(uvs[i].x * 8f), Mathf.FloorToInt(uvs[i].y * 8f));
                if (cell == Red)
                    uvs[i] += (Vector2)(Purple - Red) * Cell;
                else if (boss && cell == DarkRed)
                    uvs[i] += (Vector2)(Indigo - DarkRed) * Cell;
            }
            mesh.uv = uvs;
            return mesh;
        }

        /// <summary>Узел под подбородком, два конца на груди и уголок на затылке — в координатах обычной головы.</summary>
        static Mesh BuildKnot()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddEllipsoid(vertices, triangles, new Vector3(0f, -0.245f, 0.262f), new Vector3(0.062f, 0.046f, 0.044f), Quaternion.identity);
            foreach (float side in new[] { -1f, 1f })
                AddEllipsoid(vertices, triangles, new Vector3(0.062f * side, -0.31f, 0.318f), new Vector3(0.036f, 0.075f, 0.016f),
                    Quaternion.Euler(-37f, 0f, 25f * side));
            AddCorner(vertices, triangles);

            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            var uv = new Vector2((Purple.x + 0.5f) * Cell, (Purple.y + 0.5f) * Cell);
            var uvs = new List<Vector2>(vertices.Count);
            for (int i = 0; i < vertices.Count; i++)
                uvs.Add(uv);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Гранёный эллипсоид, как вся остальная low-poly графика.</summary>
        static void AddEllipsoid(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector3 radii, Quaternion rotation)
        {
            const int segments = 12, rings = 8;
            Vector3 Point(int ring, int segment)
            {
                float theta = Mathf.PI * ring / rings;
                float phi = 2f * Mathf.PI * segment / segments;
                var p = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                return center + rotation * Vector3.Scale(p, radii);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                    AddQuad(vertices, triangles, Point(r, s), Point(r, s + 1), Point(r + 1, s + 1), Point(r + 1, s));
        }

        /// <summary>
        /// Уголок платка на затылке: треугольник облегает голову, ниже свисает над шеей и ложится на туловище.
        /// Двусторонний, чтобы снизу не просвечивал.
        /// </summary>
        static void AddCorner(List<Vector3> vertices, List<int> triangles)
        {
            const int rows = 6, columns = 6;
            const float top = -0.02f, tip = -0.35f, halfWidth = 0.2f;
            Vector3 Point(int row, int column, float lift)
            {
                float t = (float)row / rows;
                float y = Mathf.Lerp(top, tip, t);
                float x = halfWidth * (1f - t) * (2f * column / columns - 1f);
                float depth = BackDepth(y);
                return new Vector3(x, y, -(Mathf.Sqrt(Mathf.Max(0f, depth * depth - x * x)) + lift));
            }
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                {
                    AddQuad(vertices, triangles, Point(r, c, 0.012f), Point(r, c + 1, 0.012f), Point(r + 1, c + 1, 0.012f), Point(r + 1, c, 0.012f));
                    AddQuad(vertices, triangles, Point(r, c + 1, 0.006f), Point(r, c, 0.006f), Point(r + 1, c, 0.006f), Point(r + 1, c + 1, 0.006f));
                }
        }

        /// <summary>Как далеко от оси спина неваляшки на высоте y (голова, провисание над шеей, туловище).</summary>
        static float BackDepth(float y)
        {
            float[] ys = { -0.02f, -0.08f, -0.12f, -0.18f, -0.26f, -0.35f };
            float[] depths = { 0.31f, 0.30f, 0.293f, 0.30f, 0.318f, 0.345f };
            if (y >= ys[0])
                return depths[0];
            for (int i = 1; i < ys.Length; i++)
                if (y >= ys[i])
                    return Mathf.Lerp(depths[i - 1], depths[i], Mathf.InverseLerp(ys[i - 1], ys[i], y));
            return depths[depths.Length - 1];
        }

        /// <summary>Четырёхугольник по часовой (a — левый верх), вершины не общие — грани плоские.</summary>
        static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTriangle(vertices, triangles, a, b, c);
            AddTriangle(vertices, triangles, a, c, d);
        }

        static void AddTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f)
                return;
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        /// <summary>Меш в ассет; если он уже есть — переписать на месте, чтобы ссылки в префабах не порвались.</summary>
        static Mesh SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name;
            string path = $"{Folder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>Голове — перекрашенный меш, в неё — узел с уголком (у босса ×3).</summary>
        static void Dress(string prefabPath, string headPath, Mesh head, Mesh knot, float scale)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var target = root.transform.Find(headPath);
                if (target == null || !target.TryGetComponent(out MeshFilter filter) || !target.TryGetComponent(out MeshRenderer renderer))
                {
                    Debug.LogError($"[RolyPolyKerchief] {prefabPath}: нет головы {headPath}");
                    return;
                }
                filter.sharedMesh = head;

                var child = target.Find(ChildName);
                if (child == null)
                {
                    child = new GameObject(ChildName).transform;
                    child.SetParent(target, false);
                }
                child.gameObject.layer = target.gameObject.layer;
                child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.localScale = Vector3.one * scale;
                if (!child.TryGetComponent(out MeshFilter knotFilter))
                    knotFilter = child.gameObject.AddComponent<MeshFilter>();
                knotFilter.sharedMesh = knot;
                if (!child.TryGetComponent(out MeshRenderer knotRenderer))
                    knotRenderer = child.gameObject.AddComponent<MeshRenderer>();
                knotRenderer.sharedMaterials = renderer.sharedMaterials;
                knotRenderer.shadowCastingMode = renderer.shadowCastingMode;
                knotRenderer.receiveShadows = renderer.receiveShadows;
                knotRenderer.lightProbeUsage = renderer.lightProbeUsage;
                knotRenderer.reflectionProbeUsage = renderer.reflectionProbeUsage;
                knotRenderer.motionVectorGenerationMode = renderer.motionVectorGenerationMode;
                knotRenderer.renderingLayerMask = renderer.renderingLayerMask;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

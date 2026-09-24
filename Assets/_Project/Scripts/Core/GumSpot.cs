using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Липкое пятно жвачки на асфальте (карточка «Жвачка»): враги в нём вязнут и идут медленнее,
    /// через несколько секунд пятно съёживается и исчезает. Игрока не замедляет.
    /// Как и <see cref="GroundZone"/>, проверяется по реестру, без физических триггеров.
    /// Неровная лепёшка строится в коде, цвет — подкраской шейдера палитры.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GumSpot : MonoBehaviour, IPoolable
    {
        const int MeshVariants = 4;
        const float SurfaceOffset = 0.03f;

        static readonly List<GumSpot> s_all = new();
        static Mesh[] s_meshes;

        [Tooltip("Радиус пятна, м")]
        [SerializeField, Min(0.1f)] float radius = 0.9f;
        [Tooltip("Разброс размера: пятна от (1 − разброс) до (1 + разброс)")]
        [SerializeField, Range(0f, 0.5f)] float sizeJitter = 0.15f;
        [Tooltip("Множитель скорости врага, который идёт по пятну")]
        [SerializeField, Range(0.1f, 1f)] float enemyMoveMultiplier = 0.45f;
        [SerializeField, Min(0.1f)] float lifetime = 4f;
        [Tooltip("Последние секунды жизни пятно съёживается")]
        [SerializeField, Min(0.01f)] float shrinkTime = 0.6f;
        [Tooltip("Больше пятен на арене не бывает: лишние исчезают, начиная со старых")]
        [SerializeField, Min(1)] int maxSpots = 80;
        [SerializeField] Color color = new(0.95f, 0.42f, 0.7f);

        MeshFilter _filter;
        Renderer _renderer;
        MaterialPropertyBlock _block;
        float _age;
        float _size = 1f;

        /// <summary>Радиус сейчас, с учётом размера и съёживания.</summary>
        public float CurrentRadius => transform.localScale.x;

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            if (PaletteShader.Supports(_renderer.sharedMaterial))
                _block.SetColor(PaletteShader.TintColor, PaletteShader.Tint(color, 1f));
            else
                _block.SetColor(PaletteShader.BaseColor, color);
            _renderer.SetPropertyBlock(_block);
        }

        void OnEnable() => s_all.Add(this);

        void OnDisable() => s_all.Remove(this);

        public void OnSpawned()
        {
            _age = 0f;
            _size = Random.Range(1f - sizeJitter, 1f + sizeJitter);
            _filter.sharedMesh = Meshes()[Random.Range(0, MeshVariants)];
            UpdateScale();
            // Список идёт по порядку появления: первым уходит самое старое пятно.
            while (s_all.Count > maxSpots)
                PoolService.Despawn(s_all[0].gameObject);
        }

        public void OnDespawned() { }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                PoolService.Despawn(gameObject);
                return;
            }
            UpdateScale();
        }

        void UpdateScale()
        {
            float shrink = Mathf.Clamp01((lifetime - _age) / shrinkTime);
            transform.localScale = Vector3.one * (radius * _size * shrink);
        }

        /// <summary>Положить пятно на асфальт под точкой (мяч в полёте). Над пропастью или на стене — не кладётся.</summary>
        public static void Drop(GumSpot prefab, Vector3 above)
        {
            if (prefab == null
                || !Physics.Raycast(above + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 8f, Layers.EnvironmentMask,
                    QueryTriggerInteraction.Ignore)
                || hit.normal.y < 0.6f)
                return;
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            PoolService.Spawn(prefab, hit.point + hit.normal * SurfaceOffset, rotation);
        }

        /// <summary>Множитель скорости врага в точке: 1 — жвачки тут нет.</summary>
        public static float EnemyMoveMultiplierAt(Vector3 position)
        {
            float multiplier = 1f;
            foreach (var spot in s_all)
            {
                Vector3 delta = position - spot.transform.position;
                float r = spot.CurrentRadius;
                if (delta.x * delta.x + delta.z * delta.z <= r * r && Mathf.Abs(delta.y) < 1.5f)
                    multiplier = Mathf.Min(multiplier, spot.enemyMoveMultiplier);
            }
            return multiplier;
        }

        // ---------- Лепёшка ----------

        static Mesh[] Meshes()
        {
            if (s_meshes != null && s_meshes[0] != null)
                return s_meshes;
            // Сид постоянный: пятна одни и те же от забега к забегу.
            var random = new System.Random(2409);
            s_meshes = new Mesh[MeshVariants];
            for (int i = 0; i < MeshVariants; i++)
                s_meshes[i] = BuildBlob(random, i);
            return s_meshes;
        }

        /// <summary>Плоская неровная лепёшка радиусом около 1 в плоскости XZ, лицом вверх.</summary>
        static Mesh BuildBlob(System.Random random, int index)
        {
            const int points = 11;
            var vertices = new Vector3[points + 1];
            var normals = new Vector3[points + 1];
            var uv = new Vector2[points + 1];
            var triangles = new int[points * 3];
            float start = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i <= points; i++)
            {
                normals[i] = Vector3.up;
                uv[i] = new Vector2(0.0625f, 0.0625f);
                if (i == 0)
                    continue;
                float angle = start + (i - 1) * Mathf.PI * 2f / points + ((float)random.NextDouble() - 0.5f) * 0.35f;
                float r = Mathf.Lerp(0.75f, 1.1f, (float)random.NextDouble());
                vertices[i] = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            }
            for (int i = 0; i < points; i++)
            {
                // По часовой стрелке при взгляде сверху — у Unity это лицевая сторона.
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % points + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "GumSpot_" + index, vertices = vertices, normals = normals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_all.Clear();
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Жвачка на асфальте: глянцевое пятно с толстым краем и бликом, на нём надуваются и лопаются пузыри.
    /// Враг, ушедший с пятна, тянет за ногами розовые нитки, пока они не порвутся. В конце пятно лопается — «чпок».
    /// Материалы (тело, край, блик) — три подсетки, ставит Bouncer → Art → Build Gum VFX.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GumSpot : MonoBehaviour, IPoolable
    {
        const int MeshVariants = 6;
        const int OutlinePoints = 28;
        const float SurfaceOffset = 0.02f;
        const float CenterHeight = 0.03f;
        const float InnerHeight = 0.04f;
        const float CrestHeight = 0.075f;
        const float AppearTime = 0.15f;
        const float StrandHeight = 0.15f;
        const int StrandLines = 3;
        const int StrandPoints = 6;

        static readonly List<GumSpot> s_all = new();
        static readonly Dictionary<Targetable, GumSpot> s_inside = new();
        static readonly List<Targetable> s_gone = new();
        static readonly List<Strand> s_strands = new();
        static readonly Stack<Strand> s_free = new();
        static Mesh[] s_meshes;
        static Mesh s_bubbleMesh;
        static Transform s_strandRoot;
        static int s_frame = -1;
        static int s_bubbles;
        static float s_nextPopFx;

        sealed class Strand
        {
            public LineRenderer[] Lines;
            public Targetable Target;
            public Vector3 Anchor;
            public Vector3 Side;
        }

        [Tooltip("Радиус пятна, м")]
        [SerializeField, Min(0.1f)] float radius = 0.9f;
        [Tooltip("Разброс размера: пятна от (1 − разброс) до (1 + разброс)")]
        [SerializeField, Range(0f, 0.5f)] float sizeJitter = 0.15f;
        [Tooltip("Множитель скорости врага, который идёт по пятну")]
        [SerializeField, Range(0.1f, 1f)] float enemyMoveMultiplier = 0.45f;
        [SerializeField, Min(0.1f)] float lifetime = 4f;
        [Tooltip("Последние секунды жизни пятно вздувается и лопается")]
        [SerializeField, Min(0.05f)] float popTime = 0.3f;
        [Tooltip("Больше пятен на арене не бывает: лишние исчезают, начиная со старых")]
        [SerializeField, Min(1)] int maxSpots = 80;

        [Header("Пузыри")]
        [SerializeField] Material bubbleMaterial;
        [Tooltip("Пузырь надувается за столько секунд (от и до)")]
        [SerializeField] Vector2 bubbleInflate = new(0.5f, 0.8f);
        [Tooltip("Пауза до следующего пузыря, с (от и до)")]
        [SerializeField] Vector2 bubblePause = new(0.4f, 2.2f);
        [Tooltip("Размер пузыря — доля радиуса пятна (от и до)")]
        [SerializeField] Vector2 bubbleSize = new(0.26f, 0.4f);
        [Tooltip("Больше стольких пузырей сразу на всех пятнах не надувается")]
        [SerializeField, Min(0)] int maxBubbles = 10;
        [Tooltip("Розовые брызги: лопнул пузырь, лопнуло пятно, порвались нитки")]
        [SerializeField] ParticleBurst popBurst;
        [Tooltip("Не чаще одних брызг за столько секунд на всю арену")]
        [SerializeField, Min(0f)] float popFxInterval = 0.08f;

        [Header("Нитки за ногами")]
        [SerializeField] Material strandMaterial;
        [Tooltip("Враг ушёл с пятна — нитки тянутся до стольких метров и рвутся")]
        [SerializeField, Min(0.2f)] float strandLength = 1.6f;
        [SerializeField, Min(0.005f)] float strandWidth = 0.1f;

        MeshFilter _filter;
        Transform _bubble;
        float _age;
        float _size = 1f;
        bool _bubbleOn;
        float _bubbleStart;
        float _bubbleTime;
        float _bubbleScale;
        float _nextBubble;

        public float CurrentRadius => transform.localScale.x;

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            if (bubbleMaterial)
            {
                var go = new GameObject("Bubble", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = BubbleMesh();
                var bubbleRenderer = go.GetComponent<MeshRenderer>();
                bubbleRenderer.sharedMaterial = bubbleMaterial;
                bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bubbleRenderer.receiveShadows = false;
                go.SetActive(false);
                _bubble = go.transform;
            }
        }

        void OnEnable() => s_all.Add(this);

        void OnDisable()
        {
            s_all.Remove(this);
            StopBubble();
        }

        public void OnSpawned()
        {
            _age = 0f;
            _size = Random.Range(1f - sizeJitter, 1f + sizeJitter);
            _filter.sharedMesh = Meshes()[Random.Range(0, MeshVariants)];
            _nextBubble = Random.Range(0.1f, bubblePause.y);
            UpdateScale();
            while (s_all.Count > maxSpots)
                PoolService.Despawn(s_all[0].gameObject);
        }

        public void OnDespawned() => StopBubble();

        void Update()
        {
            if (s_frame != Time.frameCount)
            {
                s_frame = Time.frameCount;
                UpdateStrands();
            }
            _age += Time.deltaTime;
            if (_age >= lifetime)
            {
                PopFx(transform.position + Vector3.up * 0.1f, 0.55f);
                PoolService.Despawn(gameObject);
                return;
            }
            UpdateScale();
            UpdateBubble();
        }

        void UpdateScale()
        {
            float k;
            float left = lifetime - _age;
            if (_age < AppearTime)
            {
                // шлёп: пятно растекается с небольшим перехлёстом
                float t = _age / AppearTime;
                k = 0.55f + 0.45f * Mathf.Sin(t * Mathf.PI * 0.62f) / Mathf.Sin(Mathf.PI * 0.62f);
            }
            else if (left < popTime)
            {
                // «чпок»: вздувается и разом схлопывается
                float t = 1f - left / popTime;
                k = t < 0.4f ? 1f + 0.15f * (t / 0.4f) : 1.15f * (1f - Mathf.Pow((t - 0.4f) / 0.6f, 2f));
            }
            else
            {
                k = 1f + 0.02f * Mathf.Sin((_age + _size * 10f) * 3f);
            }
            transform.localScale = Vector3.one * (radius * _size * Mathf.Max(0f, k));
        }

        // ------------------------------------------------------------------ bubbles
        void UpdateBubble()
        {
            if (_bubble == null)
                return;
            if (!_bubbleOn)
            {
                _nextBubble -= Time.deltaTime;
                if (_nextBubble > 0f || s_bubbles >= maxBubbles || lifetime - _age < popTime + bubbleInflate.y)
                    return;
                _bubbleOn = true;
                s_bubbles++;
                _bubbleStart = Time.time;
                _bubbleTime = Random.Range(bubbleInflate.x, bubbleInflate.y);
                _bubbleScale = Random.Range(bubbleSize.x, bubbleSize.y);
                Vector2 at = Random.insideUnitCircle * 0.45f;
                _bubble.localPosition = new Vector3(at.x, InnerHeight, at.y);
                _bubble.gameObject.SetActive(true);
            }
            float t = (Time.time - _bubbleStart) / _bubbleTime;
            if (t >= 1f)
            {
                PopFx(_bubble.position + Vector3.up * _bubbleScale * transform.localScale.x * 0.5f, 0.3f);
                StopBubble();
                _nextBubble = Random.Range(bubblePause.x, bubblePause.y);
                return;
            }
            // надувается всё быстрее и чуть дрожит перед тем, как лопнуть
            float grow = Mathf.Sqrt(t);
            float wobble = 1f + 0.06f * t * Mathf.Sin(Time.time * 40f);
            float s = _bubbleScale * grow;
            _bubble.localScale = new Vector3(s * wobble, s * 0.8f / wobble, s * wobble);
        }

        void StopBubble()
        {
            if (!_bubbleOn)
                return;
            _bubbleOn = false;
            s_bubbles = Mathf.Max(0, s_bubbles - 1);
            if (_bubble != null)
                _bubble.gameObject.SetActive(false);
        }

        void PopFx(Vector3 at, float scale)
        {
            if (popBurst == null || Time.time < s_nextPopFx)
                return;
            s_nextPopFx = Time.time + popFxInterval;
            PoolService.Spawn(popBurst, at, Quaternion.identity).Play(scale);
        }

        // ------------------------------------------------------------------ strands
        void UpdateStrands()
        {
            if (s_strandRoot == null)
            {
                // сцена сменилась: старые нитки ушли вместе с ней
                s_strands.Clear();
                s_free.Clear();
                s_inside.Clear();
            }
            foreach (var target in Targetable.All)
            {
                if (target == null || target.Team != Team.Enemy || !target.IsAlive)
                    continue;
                var spot = SpotAt(target.Position);
                s_inside.TryGetValue(target, out var was);
                if (spot != null)
                {
                    s_inside[target] = spot;
                    continue;
                }
                if (was == null)
                    continue;
                s_inside.Remove(target);
                if (was.isActiveAndEnabled && strandMaterial)
                    StartStrand(was, target);
            }
            s_gone.Clear();
            foreach (var pair in s_inside)
                if (pair.Key == null || !pair.Key.IsAlive || pair.Value == null || !pair.Value.isActiveAndEnabled)
                    s_gone.Add(pair.Key);
            foreach (var target in s_gone)
                s_inside.Remove(target);

            for (int i = s_strands.Count - 1; i >= 0; i--)
            {
                var strand = s_strands[i];
                if (!UpdateStrand(strand))
                {
                    s_strands.RemoveAt(i);
                    foreach (var line in strand.Lines)
                        line.enabled = false;
                    strand.Target = null;
                    s_free.Push(strand);
                }
            }
        }

        static GumSpot SpotAt(Vector3 position)
        {
            foreach (var spot in s_all)
            {
                Vector3 delta = position - spot.transform.position;
                float r = spot.CurrentRadius;
                if (delta.x * delta.x + delta.z * delta.z <= r * r && Mathf.Abs(delta.y) < 1.5f)
                    return spot;
            }
            return null;
        }

        void StartStrand(GumSpot spot, Targetable target)
        {
            var strand = s_free.Count > 0 ? s_free.Pop() : NewStrand();
            Vector3 center = spot.transform.position;
            Vector3 away = target.Position - center;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            strand.Target = target;
            strand.Anchor = center + away * spot.CurrentRadius * 0.85f + Vector3.up * CrestHeight * spot.CurrentRadius;
            strand.Side = Vector3.Cross(Vector3.up, away);
            foreach (var line in strand.Lines)
                line.enabled = true;
            s_strands.Add(strand);
        }

        Strand NewStrand()
        {
            if (s_strandRoot == null)
                s_strandRoot = new GameObject("GumStrands").transform;
            var strand = new Strand { Lines = new LineRenderer[StrandLines] };
            for (int i = 0; i < StrandLines; i++)
            {
                var line = new GameObject("Strand", typeof(LineRenderer)).GetComponent<LineRenderer>();
                line.transform.SetParent(s_strandRoot, false);
                line.sharedMaterial = strandMaterial;
                line.positionCount = StrandPoints;
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                // тянутая жвачка: толще у концов, тоньше посередине
                line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.5f, 0.35f), new Keyframe(1f, 0.8f));
                line.enabled = false;
                strand.Lines[i] = line;
            }
            return strand;
        }

        bool UpdateStrand(Strand strand)
        {
            var target = strand.Target;
            if (target == null || !target.IsAlive)
                return false;
            Vector3 feet = target.Position + Vector3.up * StrandHeight;
            Vector3 flat = feet - strand.Anchor;
            flat.y = 0f;
            float stretch = flat.magnitude / strandLength;
            if (stretch >= 1f)
            {
                PopFx(Vector3.Lerp(strand.Anchor, feet, 0.6f), 0.35f);
                return false;
            }
            float slack = 1f - stretch;
            for (int k = 0; k < strand.Lines.Length; k++)
            {
                var line = strand.Lines[k];
                float spread = (k - (strand.Lines.Length - 1) * 0.5f) * 0.09f;
                Vector3 from = strand.Anchor + strand.Side * spread;
                Vector3 to = feet + strand.Side * spread * 0.4f;
                for (int p = 0; p < StrandPoints; p++)
                {
                    float t = p / (float)(StrandPoints - 1);
                    Vector3 point = Vector3.Lerp(from, to, t);
                    point.y -= Mathf.Sin(t * Mathf.PI) * 0.08f * slack;
                    point.y = Mathf.Max(point.y, strand.Anchor.y - 0.02f);
                    line.SetPosition(p, point);
                }
                line.widthMultiplier = strandWidth * (0.4f + 0.6f * slack) * (k == 1 ? 1f : 0.75f);
            }
            return true;
        }

        // ------------------------------------------------------------------ drop and slowdown
        public static void Drop(GumSpot prefab, Vector3 above)
        {
            if (prefab == null
                || !Physics.Raycast(above + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 8f, Layers.EnvironmentMask,
                    QueryTriggerInteraction.Ignore)
                || hit.normal.y < 0.6f)
                return;
            // без случайного поворота: блик у всех пятен с одной стороны, как от одного солнца
            var rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            PoolService.Spawn(prefab, hit.point + hit.normal * SurfaceOffset, rotation);
            Dropped?.Invoke(prefab, above);
        }

        public static event System.Action<GumSpot, Vector3> Dropped;

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

        // ------------------------------------------------------------------ meshes
        static Mesh[] Meshes()
        {
            if (s_meshes != null && s_meshes[0] != null)
                return s_meshes;
            var random = new System.Random(2409);
            s_meshes = new Mesh[MeshVariants];
            for (int i = 0; i < MeshVariants; i++)
                s_meshes[i] = BuildBlob(random, i);
            return s_meshes;
        }

        /// <summary>Клякса жвачки: подсетка 0 — тело, 1 — толстый край, 2 — блик (сверху слева, как от солнца).</summary>
        static Mesh BuildBlob(System.Random random, int index)
        {
            const int n = OutlinePoints;
            var outline = new float[n];
            float p1 = (float)random.NextDouble() * Mathf.PI * 2f;
            float p2 = (float)random.NextDouble() * Mathf.PI * 2f;
            float p3 = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                outline[i] = 1f + 0.1f * Mathf.Sin(2f * a + p1) + 0.07f * Mathf.Sin(3f * a + p2) + 0.04f * Mathf.Sin(5f * a + p3);
            }
            Vector3 Ring(int i, float scale, float height)
            {
                float a = (i % n) * Mathf.PI * 2f / n;
                float r = outline[i % n] * scale;
                return new Vector3(Mathf.Cos(a) * r, height, Mathf.Sin(a) * r);
            }

            var vertices = new List<Vector3>();
            var body = new List<int>();
            var rim = new List<int>();
            var gloss = new List<int>();

            // тело: чуть вогнутое к середине
            vertices.Add(new Vector3(0f, CenterHeight, 0f));
            for (int i = 0; i < n; i++)
                vertices.Add(Ring(i, 0.78f, InnerHeight));
            for (int i = 0; i < n; i++)
            {
                body.Add(0);
                body.Add(1 + (i + 1) % n);
                body.Add(1 + i);
            }

            // край: внутреннее кольцо, гребень, внешнее кольцо у асфальта
            int inner = vertices.Count;
            for (int i = 0; i < n; i++)
                vertices.Add(Ring(i, 0.78f, InnerHeight));
            int crest = vertices.Count;
            for (int i = 0; i < n; i++)
                vertices.Add(Ring(i, 0.9f, CrestHeight));
            int outer = vertices.Count;
            for (int i = 0; i < n; i++)
                vertices.Add(Ring(i, 1f, 0.005f));
            void Strip(int a, int b)
            {
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    rim.Add(a + i);
                    rim.Add(a + j);
                    rim.Add(b + i);
                    rim.Add(a + j);
                    rim.Add(b + j);
                    rim.Add(b + i);
                }
            }
            Strip(inner, crest);
            Strip(crest, outer);

            // блик: вытянутый овал и точка, чуть выше тела
            void Ellipse(Vector2 center, Vector2 size, float angle, int segments)
            {
                int first = vertices.Count;
                float height = InnerHeight + 0.006f;
                vertices.Add(new Vector3(center.x, height, center.y));
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float x = Mathf.Cos(a) * size.x, y = Mathf.Sin(a) * size.y;
                    vertices.Add(new Vector3(center.x + x * c - y * s, height, center.y + x * s + y * c));
                }
                for (int i = 0; i < segments; i++)
                {
                    gloss.Add(first);
                    gloss.Add(first + 1 + (i + 1) % segments);
                    gloss.Add(first + 1 + i);
                }
            }
            float jitter = ((float)random.NextDouble() - 0.5f) * 0.08f;
            Ellipse(new Vector2(-0.3f + jitter, 0.28f), new Vector2(0.17f, 0.07f), Mathf.Deg2Rad * -35f, 12);
            Ellipse(new Vector2(-0.06f, 0.46f + jitter), new Vector2(0.045f, 0.045f), 0f, 8);

            var uv = new Vector2[vertices.Count];
            for (int i = 0; i < uv.Length; i++)
                uv[i] = new Vector2(0.0625f, 0.0625f);
            var mesh = new Mesh { name = "GumSpot_" + index, subMeshCount = 3 };
            mesh.SetVertices(vertices);
            mesh.uv = uv;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(rim, 1);
            mesh.SetTriangles(gloss, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Пузырь: низкополигональная сфера диаметром 1, нижняя половина утоплена в пятно.</summary>
        static Mesh BubbleMesh()
        {
            if (s_bubbleMesh != null)
                return s_bubbleMesh;
            const int rings = 6, segments = 10;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments * Mathf.PI * 2f;
                    var normal = new Vector3(Mathf.Sin(v) * Mathf.Cos(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Sin(u));
                    vertices.Add(normal * 0.5f);
                    normals.Add(normal);
                }
            }
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * (segments + 1) + s;
                    int b = a + segments + 1;
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                    triangles.Add(b);
                }
            }
            s_bubbleMesh = new Mesh { name = "GumBubble" };
            s_bubbleMesh.SetVertices(vertices);
            s_bubbleMesh.SetNormals(normals);
            s_bubbleMesh.SetTriangles(triangles, 0);
            s_bubbleMesh.RecalculateBounds();
            return s_bubbleMesh;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_all.Clear();
            s_inside.Clear();
            s_strands.Clear();
            s_free.Clear();
            s_strandRoot = null;
            s_frame = -1;
            s_bubbles = 0;
            s_nextPopFx = 0f;
        }
    }
}

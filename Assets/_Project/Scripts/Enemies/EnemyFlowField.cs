using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Поле направлений к ближайшему игроку для роя (пупсы). Сетка строится по NavMesh один раз,
    /// а расстояния до игроков пересчитываются несколько раз в секунду (Дейкстра сразу от всех игроков).
    /// Каждый враг только читает направление в своей клетке — один расчёт на весь рой вместо пути на каждого.
    /// </summary>
    public sealed class EnemyFlowField : MonoBehaviour
    {
        const float CellSize = 0.5f;
        const float UpdateInterval = 0.25f;
        const float Unreachable = float.MaxValue;

        static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] Dz = { 0, 0, 1, -1, 1, -1, 1, -1 };
        static readonly float[] Cost = { 1f, 1f, 1f, 1f, 1.4142f, 1.4142f, 1.4142f, 1.4142f };

        static EnemyFlowField s_instance;

        readonly MinHeap _heap = new();
        Vector3 _origin;
        int _width;
        int _height;
        bool[] _walkable;
        float[] _distance;
        float _nextUpdate;

        public static EnemyFlowField Instance
        {
            get
            {
                if (s_instance == null)
                {
                    s_instance = new GameObject("[EnemyFlowField]").AddComponent<EnemyFlowField>();
                    s_instance.Build();
                }
                return s_instance;
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        void Build()
        {
            var vertices = NavMesh.CalculateTriangulation().vertices;
            if (vertices.Length == 0)
                return;

            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var v in vertices)
                bounds.Encapsulate(v);
            _origin = new Vector3(bounds.min.x, bounds.center.y, bounds.min.z);
            _width = Mathf.CeilToInt(bounds.size.x / CellSize) + 1;
            _height = Mathf.CeilToInt(bounds.size.z / CellSize) + 1;
            _walkable = new bool[_width * _height];
            _distance = new float[_width * _height];

            float maxOffsetSqr = CellSize * CellSize * 0.36f;
            for (int z = 0; z < _height; z++)
            {
                for (int x = 0; x < _width; x++)
                {
                    Vector3 center = CellCenter(x, z);
                    _walkable[z * _width + x] = NavMesh.SamplePosition(center, out NavMeshHit hit, CellSize * 0.75f, NavMesh.AllAreas)
                                                && Flat(hit.position - center).sqrMagnitude <= maxOffsetSqr;
                }
            }
            Recalculate();
        }

        void Update()
        {
            if (_width == 0 || Time.time < _nextUpdate)
                return;
            _nextUpdate = Time.time + UpdateInterval;
            Recalculate();
        }

        /// <summary>
        /// Направление к ближайшему игроку в обход препятствий.
        /// false — точка вне сетки, игрок недостижим или враг уже в клетке игрока (тогда бежать прямо).
        /// </summary>
        public bool TryGetDirection(Vector3 position, out Vector3 direction)
        {
            direction = default;
            if (_width == 0)
                return false;
            int cell = CellAt(position);
            if (cell < 0)
                return false;

            if (!_walkable[cell])
            {
                // Враг оттолкнули к стене или в препятствие — сначала вернуться на проходимую клетку.
                int nearest = NearestWalkableCell(position, 3);
                if (nearest < 0)
                    return false;
                direction = Flat(CellCenter(nearest % _width, nearest / _width) - position).normalized;
                return direction.sqrMagnitude > 0.5f;
            }

            float own = _distance[cell];
            if (own == Unreachable)
                return false;

            // Сглаженный градиент: взвешенная сумма шагов к соседям, которые ближе к игроку.
            int cx = cell % _width, cz = cell / _width;
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < 8; k++)
            {
                int n = Neighbour(cx, cz, k);
                if (n < 0 || _distance[n] >= own)
                    continue;
                sum += new Vector3(Dx[k], 0f, Dz[k]) * ((own - _distance[n]) / (Cost[k] * Cost[k]));
            }
            if (sum.sqrMagnitude < 1e-8f)
                return false;
            direction = sum.normalized;
            return true;
        }

        void Recalculate()
        {
            for (int i = 0; i < _distance.Length; i++)
                _distance[i] = Unreachable;
            _heap.Clear();

            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                int cell = NearestWalkableCell(target.Position, 4);
                if (cell < 0)
                    continue;
                _distance[cell] = 0f;
                _heap.Push(cell, 0f);
            }

            while (_heap.TryPop(out int cell, out float distance))
            {
                if (distance > _distance[cell])
                    continue;
                int cx = cell % _width, cz = cell / _width;
                for (int k = 0; k < 8; k++)
                {
                    int n = Neighbour(cx, cz, k);
                    if (n < 0)
                        continue;
                    float next = distance + Cost[k] * CellSize;
                    if (next < _distance[n])
                    {
                        _distance[n] = next;
                        _heap.Push(n, next);
                    }
                }
            }
        }

        /// <summary>Проходимый сосед клетки или -1. По диагонали — только если не срезаем угол препятствия.</summary>
        int Neighbour(int cx, int cz, int k)
        {
            int nx = cx + Dx[k], nz = cz + Dz[k];
            if (nx < 0 || nz < 0 || nx >= _width || nz >= _height)
                return -1;
            int n = nz * _width + nx;
            if (!_walkable[n])
                return -1;
            if (k >= 4 && (!_walkable[cz * _width + nx] || !_walkable[nz * _width + cx]))
                return -1;
            return n;
        }

        int CellAt(Vector3 position)
        {
            int x = Mathf.FloorToInt((position.x - _origin.x) / CellSize);
            int z = Mathf.FloorToInt((position.z - _origin.z) / CellSize);
            return x < 0 || z < 0 || x >= _width || z >= _height ? -1 : z * _width + x;
        }

        int NearestWalkableCell(Vector3 position, int maxRing)
        {
            int x0 = Mathf.FloorToInt((position.x - _origin.x) / CellSize);
            int z0 = Mathf.FloorToInt((position.z - _origin.z) / CellSize);
            for (int ring = 0; ring <= maxRing; ring++)
            {
                int best = -1;
                float bestSqr = float.PositiveInfinity;
                for (int z = z0 - ring; z <= z0 + ring; z++)
                {
                    for (int x = x0 - ring; x <= x0 + ring; x++)
                    {
                        if (Mathf.Max(Mathf.Abs(x - x0), Mathf.Abs(z - z0)) != ring)
                            continue;
                        if (x < 0 || z < 0 || x >= _width || z >= _height || !_walkable[z * _width + x])
                            continue;
                        float sqr = Flat(CellCenter(x, z) - position).sqrMagnitude;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            best = z * _width + x;
                        }
                    }
                }
                if (best >= 0)
                    return best;
            }
            return -1;
        }

        Vector3 CellCenter(int x, int z) =>
            new(_origin.x + (x + 0.5f) * CellSize, _origin.y, _origin.z + (z + 0.5f) * CellSize);

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        /// <summary>Двоичная куча клеток по расстоянию (в .NET Standard 2.1 нет PriorityQueue).</summary>
        sealed class MinHeap
        {
            int[] _cells = new int[512];
            float[] _keys = new float[512];
            int _count;

            public void Clear() => _count = 0;

            public void Push(int cell, float key)
            {
                if (_count == _cells.Length)
                {
                    System.Array.Resize(ref _cells, _count * 2);
                    System.Array.Resize(ref _keys, _count * 2);
                }
                int i = _count++;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_keys[parent] <= key)
                        break;
                    _cells[i] = _cells[parent];
                    _keys[i] = _keys[parent];
                    i = parent;
                }
                _cells[i] = cell;
                _keys[i] = key;
            }

            public bool TryPop(out int cell, out float key)
            {
                if (_count == 0)
                {
                    cell = -1;
                    key = 0f;
                    return false;
                }
                cell = _cells[0];
                key = _keys[0];
                int lastCell = _cells[--_count];
                float lastKey = _keys[_count];
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= _count)
                        break;
                    if (child + 1 < _count && _keys[child + 1] < _keys[child])
                        child++;
                    if (_keys[child] >= lastKey)
                        break;
                    _cells[i] = _cells[child];
                    _keys[i] = _keys[child];
                    i = child;
                }
                _cells[i] = lastCell;
                _keys[i] = lastKey;
                return true;
            }
        }
    }
}

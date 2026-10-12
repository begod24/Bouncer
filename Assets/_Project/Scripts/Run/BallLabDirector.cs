using System.Collections.Generic;
using System.Text;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using Bouncer.Waves;
using UnityEngine;

namespace Bouncer.Run
{
    public enum LabFormation
    {
        Single,
        Line,
        Column,
        Crowd,
        Scatter,
    }

    // Панель «Площадки мячей» (IMGUI, только для баланса): выбор мяча, мишени, карточки, счётчик урона.
    public sealed class BallLabDirector : MonoBehaviour
    {
        const float PanelWidth = 440f;
        const float PickerWidth = 360f;
        const float ReferenceHeight = 1080f;
        const float Spacing = 1.7f;
        const float LooseCleanupTime = 1.5f;
        const int HistorySize = 8;
        static readonly Vector2 ArenaHalf = new(19.5f, 12.5f);
        static readonly string[] FormationNames = { "Один", "Линия", "Колонна", "Толпа", "Веер" };

        [SerializeField] WaveSpawner spawner;
        [SerializeField] Bestiary bestiary;
        [Tooltip("Через сколько секунд после гибели всех мишеней ставить их заново")]
        [SerializeField] float respawnDelay = 1.2f;

        sealed class BallOption
        {
            public Ball Prefab;
            public string Name;
        }

        sealed class TargetOption
        {
            public GameObject Prefab;
            public string Name;
            public int Hits;
            public BestiarySection Section;
        }

        sealed class ThrowRecord
        {
            public int Number;
            public string Ball;
            public string Kind;
            public int Damage;
            public int Kills;
            public readonly HashSet<Health> Struck = new();
        }

        static int s_ball;
        static int s_target;
        static LabFormation s_formation = LabFormation.Line;
        static int s_count = 5;
        static float s_distance = 10f;
        static bool s_frozen = true;
        static bool s_respawn = true;
        static bool s_infinite = true;
        static bool s_immortal = true;
        static bool s_panel = true;
        static readonly List<UpgradeCard> s_cards = new();

        readonly List<BallOption> _balls = new();
        readonly List<TargetOption> _targets = new();
        readonly List<UpgradeCard> _deck = new();
        readonly Dictionary<UpgradeCard, string> _cardNames = new();
        readonly List<ThrowRecord> _history = new();
        readonly Dictionary<Health, int> _hpSeen = new();
        readonly Dictionary<Ball, (int life, float since)> _loose = new();
        readonly List<Ball> _drop = new();
        readonly StringBuilder _text = new();

        PlayerController _player;
        PlayerCards _cards;
        ThrowRecord _current;
        int _throws;
        int _damage;
        int _kills;
        float _firstThrowAt = -1f;
        bool _started;
        bool _placed;
        Vector3 _origin;
        Vector3 _forward = Vector3.forward;
        float _respawnAt = -1f;
        float _nextApply;
        bool _wasFrozen;
        bool _showTargets;
        bool _showCards;
        Vector2 _scroll;
        Vector2 _targetScroll;
        Vector2 _cardScroll;
        string[] _ballNames;
        string[] _targetNames;
        Texture2D _shade;
        GUIStyle _box;
        GUIStyle _label;
        GUIStyle _small;
        GUIStyle _header;
        GUIStyle _hp;

        void Awake()
        {
            if (!BallLab.Active)
                enabled = false;
        }

        void Start()
        {
            if (!BallLab.Active)
                return;
            _player = Players.Local;
            if (_player == null)
            {
                enabled = false;
                return;
            }
            if (spawner == null)
                spawner = FindAnyObjectByType<WaveSpawner>();
            _player.TryGetComponent(out _cards);
            BuildBalls();
            BuildTargets();
            BuildDeck();
            _player.Balls.Thrown += OnThrown;
            if (_cards)
                _cards.Discarded += OnDiscarded;
            Health.AnyDamaged += OnDamaged;
            GameEvents.EnemyKilled += OnEnemyKilled;
        }

        void OnDestroy()
        {
            Health.AnyDamaged -= OnDamaged;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            if (_player)
                _player.Balls.Thrown -= OnThrown;
            if (_cards)
                _cards.Discarded -= OnDiscarded;
            if (_shade)
                Destroy(_shade);
            BallLab.PointerOverPanel = false;
        }

        void Update()
        {
            if (_player == null)
                return;
            if (!_started)
            {
                _started = true;
                RestoreCards();
                SelectBall(s_ball);
                Place(keepOrigin: false);
            }

            _player.Health.Floor = s_immortal ? 1 : 0;
            if (s_frozen)
                FreezeTargets();
            else if (_wasFrozen)
                ThawTargets();
            _wasFrozen = s_frozen;
            if (s_infinite)
                CleanupLoose();
            if (Time.time >= _nextApply)
            {
                _nextApply = Time.time + 0.5f;
                var balls = Ball.Active;
                for (int i = 0; i < balls.Count; i++)
                    if (balls[i].Definition)
                        balls[i].ApplyDefinition();
            }
            WatchRespawn();
        }

        void BuildBalls()
        {
            _balls.Clear();
            _balls.Add(new BallOption { Prefab = _player.Balls.BallPrefab, Name = "Обычный" });
            if (_cards && _cards.Deck)
                foreach (var card in _cards.Deck.deck)
                    if (card is BallTypeCard type && type.ballPrefab && _balls.FindIndex(b => b.Prefab == type.ballPrefab) < 0)
                        _balls.Add(new BallOption { Prefab = type.ballPrefab, Name = CardName(card) });
            _ballNames = new string[_balls.Count];
            for (int i = 0; i < _balls.Count; i++)
                _ballNames[i] = $"{i + 1}. {_balls[i].Name}";
        }

        void BuildTargets()
        {
            _targets.Clear();
            if (bestiary)
                foreach (var entry in bestiary.entries)
                {
                    if (entry == null || entry.prefab == null)
                        continue;
                    _targets.Add(new TargetOption
                    {
                        Prefab = entry.prefab,
                        Name = entry.title.IsEmpty ? entry.prefab.name : entry.title.GetLocalizedString(),
                        Hits = entry.hits,
                        Section = entry.section,
                    });
                }
            _targetNames = new string[_targets.Count];
            for (int i = 0; i < _targets.Count; i++)
                _targetNames[i] = TargetLabel(_targets[i]);
            s_target = Mathf.Clamp(s_target, 0, Mathf.Max(0, _targets.Count - 1));
        }

        void BuildDeck()
        {
            _deck.Clear();
            if (_cards == null || _cards.Deck == null)
                return;
            foreach (var card in _cards.Deck.deck)
            {
                if (card == null || card is BallTypeCard || card.coopOnly || card.category == UpgradeCategory.Treat || _deck.Contains(card))
                    continue;
                _deck.Add(card);
                CardName(card);
            }
            _deck.Sort((a, b) => a.category != b.category ? a.category.CompareTo(b.category)
                : string.CompareOrdinal(CardName(a), CardName(b)));
        }

        string CardName(UpgradeCard card)
        {
            if (_cardNames.TryGetValue(card, out string name))
                return name;
            name = card.title.IsEmpty ? card.name : card.title.GetLocalizedString();
            if (string.IsNullOrEmpty(name))
                name = card.name;
            _cardNames[card] = name;
            return name;
        }

        static string TargetLabel(TargetOption target)
        {
            string prefix = target.Section switch
            {
                BestiarySection.Elite => "★ ",
                BestiarySection.Boss => "Босс: ",
                _ => string.Empty,
            };
            return $"{prefix}{target.Name} ({target.Hits} уд.)";
        }

        void RestoreCards()
        {
            if (_cards == null)
                return;
            var cards = new List<UpgradeCard>(s_cards);
            s_cards.Clear();
            foreach (var card in cards)
                TakeCard(card);
        }

        void TakeCard(UpgradeCard card)
        {
            if (_cards == null || card == null)
                return;
            s_cards.Add(card);
            _cards.Take(card);
        }

        void OnDiscarded(UpgradeCard card) => s_cards.Remove(card);

        void SelectBall(int index)
        {
            if (_balls.Count == 0)
                return;
            s_ball = Mathf.Clamp(index, 0, _balls.Count - 1);
            _player.Balls.SetBallPrefab(_balls[s_ball].Prefab);
        }

        string CurrentBallName => _balls.Count > 0 ? _balls[Mathf.Clamp(s_ball, 0, _balls.Count - 1)].Name : "?";

        void Place(bool keepOrigin)
        {
            if (spawner == null || _targets.Count == 0)
                return;
            if (!keepOrigin)
            {
                _origin = _player.transform.position;
                Vector3 forward = _player.transform.forward;
                forward.y = 0f;
                _forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            }
            spawner.DespawnAll();
            _placed = true;
            _respawnAt = -1f;

            var target = _targets[s_target];
            Vector3 anchor = _origin + _forward * s_distance;
            Vector3 right = Vector3.Cross(Vector3.up, _forward);
            int count = s_formation == LabFormation.Single ? 1 : s_count;
            switch (s_formation)
            {
                case LabFormation.Single:
                    Queue(target, anchor);
                    break;
                case LabFormation.Line:
                    for (int i = 0; i < count; i++)
                        Queue(target, anchor + right * ((i - (count - 1) * 0.5f) * Spacing));
                    break;
                case LabFormation.Column:
                    for (int i = 0; i < count; i++)
                        Queue(target, anchor + _forward * (i * Spacing));
                    break;
                case LabFormation.Crowd:
                    spawner.QueueGroupAt(target.Prefab, count, GroupLayout.Cluster, ClampToArena(anchor),
                        elite: target.Section == BestiarySection.Elite);
                    break;
                case LabFormation.Scatter:
                    for (int i = 0; i < count; i++)
                    {
                        float angle = count == 1 ? 0f : Mathf.Lerp(-55f, 55f, i / (count - 1f));
                        float distance = s_distance + (i % 2 == 0 ? -2.5f : 2.5f);
                        Queue(target, _origin + Quaternion.Euler(0f, angle, 0f) * _forward * Mathf.Max(3f, distance));
                    }
                    break;
            }
        }

        void Queue(TargetOption target, Vector3 point) =>
            spawner.QueueGroupAt(target.Prefab, 1, GroupLayout.Line, ClampToArena(point), elite: target.Section == BestiarySection.Elite);

        static Vector3 ClampToArena(Vector3 point) =>
            new(Mathf.Clamp(point.x, -ArenaHalf.x, ArenaHalf.x), point.y, Mathf.Clamp(point.z, -ArenaHalf.y, ArenaHalf.y));

        void ClearTargets()
        {
            if (spawner)
                spawner.DespawnAll();
            _placed = false;
            _respawnAt = -1f;
        }

        void WatchRespawn()
        {
            if (!_placed || !s_respawn || spawner == null)
                return;
            if (Targetable.CountAlive(Team.Enemy) > 0 || spawner.PendingEnemies > 0)
            {
                _respawnAt = -1f;
                return;
            }
            if (_respawnAt < 0f)
                _respawnAt = Time.time + respawnDelay;
            else if (Time.time >= _respawnAt)
                Place(keepOrigin: true);
        }

        static void FreezeTargets()
        {
            var all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var target = all[i];
                if (target.Team == Team.Enemy && target.IsAlive && target.FrozenLeft < 5f)
                    target.FreezeLocal(30f);
            }
        }

        static void ThawTargets()
        {
            var all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Team == Team.Enemy)
                    all[i].Thaw();
        }

        void CleanupLoose()
        {
            _drop.Clear();
            var balls = Ball.Active;
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                if (ball.State != BallState.Loose || ball.Owner != _player.gameObject)
                {
                    _loose.Remove(ball);
                    continue;
                }
                if (!_loose.TryGetValue(ball, out var seen) || seen.life != ball.Life)
                    _loose[ball] = (ball.Life, Time.time);
                else if (Time.time - seen.since >= LooseCleanupTime)
                    _drop.Add(ball);
            }
            foreach (var ball in _drop)
            {
                _loose.Remove(ball);
                ball.Consume();
            }
        }

        void OnThrown(ThrowStats stats)
        {
            _throws++;
            if (_firstThrowAt < 0f)
                _firstThrowAt = Time.time;
            _current = new ThrowRecord
            {
                Number = _throws,
                Ball = CurrentBallName,
                Kind = stats.Has(HitFlags.Candle) ? "свечка" : stats.Has(HitFlags.Charged) ? "заряд" : "обычный",
            };
            _history.Insert(0, _current);
            if (_history.Count > HistorySize)
                _history.RemoveAt(_history.Count - 1);
            if (s_infinite && _player.Balls.Balls < _player.Balls.MaxBalls)
                _player.Balls.GiveBall();
        }

        void OnDamaged(Health health, HitInfo hit)
        {
            if (hit.SourceTeam != Team.Player || health == null || (_player && health == _player.Health))
                return;
            if (!health.TryGetComponent(out Targetable target) || target.Team != Team.Enemy)
                return;
            int before = _hpSeen.TryGetValue(health, out int seen) && seen > health.Current ? seen : health.Max;
            int applied = Mathf.Max(0, before - health.Current);
            if (health.Current <= 0)
                _hpSeen.Remove(health);
            else
                _hpSeen[health] = health.Current;
            _damage += applied;
            if (_current != null)
            {
                _current.Damage += applied;
                _current.Struck.Add(health);
            }
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (hit.Has(HitFlags.Despawn) || hit.SourceTeam != Team.Player)
                return;
            _kills++;
            if (_current != null)
                _current.Kills++;
        }

        void ResetStats()
        {
            _throws = 0;
            _damage = 0;
            _kills = 0;
            _firstThrowAt = -1f;
            _current = null;
            _history.Clear();
        }

        void OnGUI()
        {
            var session = GameSession.Instance;
            var e = Event.current;
            bool playing = session != null && session.State is (SessionState.Playing or SessionState.Cleared) && !GameFeel.Paused;
            if (playing && e.type == EventType.KeyDown)
                HandleKey(e);
            if (!playing || !s_panel || _player == null)
            {
                BallLab.PointerOverPanel = false;
                if (playing && _player != null)
                    DrawCollapsed();
                return;
            }

            EnsureStyles();
            DrawHealthLabels();

            float scale = Mathf.Clamp(Screen.height / ReferenceHeight, 0.6f, 2f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = Screen.height / scale;
            var panel = new Rect(10f, 10f, PanelWidth, height - 20f);
            var picker = new Rect(panel.xMax + 8f, 10f, PickerWidth, Mathf.Min(height - 20f, 760f));
            BallLab.PointerOverPanel = panel.Contains(e.mousePosition) || (_showCards && picker.Contains(e.mousePosition));

            GUI.Box(panel, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("ПЛОЩАДКА МЯЧЕЙ   ·   F1 — спрятать", _header);
            DrawBalls();
            GUILayout.Space(10f);
            DrawTargets();
            GUILayout.Space(10f);
            DrawPlayer(session);
            GUILayout.Space(10f);
            DrawCards(session);
            GUILayout.Space(10f);
            DrawStats();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (_showCards)
                DrawCardPicker(picker);
            GUI.matrix = Matrix4x4.identity;
        }

        void HandleKey(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.F1:
                    s_panel = !s_panel;
                    break;
                case KeyCode.F2:
                    Place(keepOrigin: false);
                    break;
                case KeyCode.F3:
                    ClearTargets();
                    break;
                case KeyCode.F4:
                    ResetStats();
                    break;
                case >= KeyCode.Alpha1 and <= KeyCode.Alpha9:
                    int index = e.keyCode - KeyCode.Alpha1;
                    if (index >= _balls.Count)
                        return;
                    SelectBall(index);
                    break;
                default:
                    return;
            }
            e.Use();
        }

        void DrawCollapsed()
        {
            EnsureStyles();
            GUI.Label(new Rect(12f, Screen.height - 34f, 400f, 26f), "Площадка мячей: F1 — панель", _small);
        }

        void DrawBalls()
        {
            GUILayout.Label("Мяч   (клавиши 1–9)", _header);
            int picked = GUILayout.SelectionGrid(s_ball, _ballNames, 2);
            if (picked != s_ball)
                SelectBall(picked);
            var definition = _player.Balls.BallDefinition;
            if (definition)
                GUILayout.Label(Describe(definition), _small);
#if UNITY_EDITOR
            if (definition && GUILayout.Button("Открыть настройки мяча в инспекторе"))
            {
                UnityEditor.Selection.activeObject = definition;
                UnityEditor.EditorGUIUtility.PingObject(definition);
            }
            GUILayout.Label("Правки в инспекторе применяются сразу и сохраняются в ассете (это не откатится после Play).", _small);
#endif
        }

        void DrawTargets()
        {
            GUILayout.Label("Мишени   (F2 — поставить перед собой, F3 — убрать)", _header);
            if (_targets.Count == 0)
            {
                GUILayout.Label("Нет бестиария — не из чего выбрать.", _label);
                return;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◀", GUILayout.Width(36f)))
                s_target = (s_target + _targets.Count - 1) % _targets.Count;
            if (GUILayout.Button(_targetNames[s_target]))
                _showTargets = !_showTargets;
            if (GUILayout.Button("▶", GUILayout.Width(36f)))
                s_target = (s_target + 1) % _targets.Count;
            GUILayout.EndHorizontal();
            if (_showTargets)
            {
                _targetScroll = GUILayout.BeginScrollView(_targetScroll, GUILayout.Height(220f));
                int picked = GUILayout.SelectionGrid(s_target, _targetNames, 1);
                GUILayout.EndScrollView();
                if (picked != s_target)
                {
                    s_target = picked;
                    _showTargets = false;
                }
            }
            if (_targets[s_target].Section == BestiarySection.Boss)
                GUILayout.Label("Босс вне своей арены может вести себя странно.", _small);

            s_formation = (LabFormation)GUILayout.SelectionGrid((int)s_formation, FormationNames, 5);
            if (s_formation != LabFormation.Single)
            {
                GUILayout.Label($"Сколько: {s_count}", _label);
                s_count = Mathf.RoundToInt(GUILayout.HorizontalSlider(s_count, 1f, 10f));
            }
            GUILayout.Label($"Дистанция: {s_distance:0} м", _label);
            s_distance = Mathf.Round(GUILayout.HorizontalSlider(s_distance, 3f, 20f));
            s_frozen = GUILayout.Toggle(s_frozen, " Стоят на месте (иней, как от «Замри!»)");
            s_respawn = GUILayout.Toggle(s_respawn, " Ставить заново, когда все убиты");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Поставить (F2)"))
                Place(keepOrigin: false);
            if (GUILayout.Button("Убрать (F3)"))
                ClearTargets();
            GUILayout.EndHorizontal();
            GUILayout.Label("Строй встаёт перед игроком по направлению взгляда. Над мишенями — их здоровье.", _small);
        }

        void DrawPlayer(GameSession session)
        {
            GUILayout.Label("Игрок", _header);
            s_infinite = GUILayout.Toggle(s_infinite, " Бесконечные мячи (свои упавшие исчезают)");
            s_immortal = GUILayout.Toggle(s_immortal, " Бессмертие (не меньше 1 сердца)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Начать заново"))
                session.Restart();
            if (GUILayout.Button("Выйти в меню"))
                session.ToTitle();
            GUILayout.EndHorizontal();
        }

        void DrawCards(GameSession session)
        {
            GUILayout.Label("Карточки   (карманы не считаются)", _header);
            if (_cards == null)
            {
                GUILayout.Label("У игрока нет PlayerCards.", _label);
                return;
            }
            if (s_cards.Count == 0)
                GUILayout.Label("Пока нет.", _small);
            else
            {
                _text.Clear();
                foreach (var card in s_cards)
                {
                    if (_text.Length > 0)
                        _text.Append(", ");
                    _text.Append(CardName(card));
                }
                GUILayout.Label(_text.ToString(), _small);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_showCards ? "Закрыть список" : "Добавить карточку…"))
                _showCards = !_showCards;
            if (s_cards.Count > 0 && GUILayout.Button("Сбросить (перезапуск)"))
            {
                s_cards.Clear();
                session.Restart();
            }
            GUILayout.EndHorizontal();
        }

        void DrawCardPicker(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, rect.height - 20f));
            GUILayout.Label("Добавить карточку", _header);
            _cardScroll = GUILayout.BeginScrollView(_cardScroll);
            UpgradeCategory? category = null;
            foreach (var card in _deck)
            {
                if (category != card.category)
                {
                    category = card.category;
                    GUILayout.Label(card.category switch
                    {
                        UpgradeCategory.Modifier => "Броски",
                        UpgradeCategory.Passive => "Пассивки",
                        _ => card.category.ToString(),
                    }, _label);
                }
                int stacks = _cards.StacksOf(card);
                string label = card.IsCombo ? $"{CardName(card)} (комбо)" : CardName(card);
                if (stacks > 0)
                    label += $"  ×{stacks}";
                GUI.enabled = stacks < card.maxStacks;
                if (GUILayout.Button(label))
                    TakeCard(card);
                GUI.enabled = true;
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStats()
        {
            GUILayout.Label("Статистика   (F4 — сбросить)", _header);
            float time = _firstThrowAt >= 0f ? Time.time - _firstThrowAt : 0f;
            float perThrow = _throws > 0 ? (float)_damage / _throws : 0f;
            float perSecond = time > 0.5f ? _damage / time : 0f;
            GUILayout.Label($"Бросков: {_throws}   урон: {_damage}   убито: {_kills}", _label);
            GUILayout.Label($"Урон за бросок: {perThrow:0.00}   урон в секунду: {perSecond:0.00}", _label);
            foreach (var record in _history)
                GUILayout.Label($"#{record.Number} {record.Ball}, {record.Kind}: урон {record.Damage}, " +
                                $"целей {record.Struck.Count}, убито {record.Kills}", _small);
            if (GUILayout.Button("Сбросить статистику (F4)"))
                ResetStats();
        }

        void DrawHealthLabels()
        {
            var camera = Camera.main;
            if (camera == null)
                return;
            GUI.matrix = Matrix4x4.identity;
            var all = Targetable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var target = all[i];
                if (target.Team != Team.Enemy || !target.IsAlive || target.Health == null)
                    continue;
                Vector3 screen = camera.WorldToScreenPoint(target.AimPoint + Vector3.up * 1.1f);
                if (screen.z <= 0f)
                    continue;
                var health = target.Health;
                GUI.Label(new Rect(screen.x - 50f, Screen.height - screen.y - 14f, 100f, 28f), $"{health.Current}/{health.Max}", _hp);
            }
        }

        string Describe(BallDefinition d)
        {
            _text.Clear();
            _text.Append($"Урон {d.damage} / заряд {d.chargedDamage} / свечка {d.candleDamage}   ");
            _text.Append($"отброс {d.knockback:0.#} / {d.chargedKnockback:0.#} / {d.candleKnockback:0.#}\n");
            _text.Append($"Скорость {d.speed:0.#}→{d.chargedSpeed:0.#} м/с, вверх {d.upVelocity:0.#}→{d.chargedUpVelocity:0.#}, ");
            _text.Append($"гравитация {d.liveGravity:0.#}→{d.chargedLiveGravity:0.#}\n");
            _text.Append($"Рикошетов {d.maxRicochets} (скорость ×{d.wallSpeedKeep:0.##}), опасен от {d.minLiveSpeed:0.#} м/с, ");
            _text.Append($"живёт {d.maxLiveTime:0.#} с, радиус {d.radius:0.##}");
            _text.Append($"\nВ руках: {(d.hands > 0 ? d.hands.ToString() : "как у ребёнка")}{(d.fixedHands ? " (карточки «+мяч» не действуют)" : string.Empty)}");
            if (!Mathf.Approximately(d.chargeTimeMultiplier, 1f))
                _text.Append($", заряд ×{d.chargeTimeMultiplier:0.##}");
            if (d.autoFireInterval > 0f)
                _text.Append($", очередь раз в {d.autoFireInterval:0.##} с (без заряда)");
            var p = d.perks;
            if (p.lob)
                _text.Append($"\n• Падает в точку прицела: {d.lobDistance.x:0}–{d.lobTapDistance:0} м, с зарядом до {d.lobDistance.y:0} м");
            if (p.ignoreShields)
                _text.Append("\n• Щит солдата не отбивает");
            if (p.bounceOffTargets)
                _text.Append("\n• Отскакивает от врагов, пока есть рикошеты");
            if (p.groundRoll)
                _text.Append("\n• Лежащий мяч можно пнуть рывком");
            if (p.extraShots > 0)
                _text.Append($"\n• Веер: +{p.extraShots} мяча через {p.spreadAngle:0}° (лишние — фантомы)");
            if (p.chainBounces > 0)
                _text.Append($"\n• Цепочка: ещё {p.chainBounces} врага в пределах {d.chainRange:0.#} м");
            if (p.areaRadius > 0f && p.areaDamage > 0)
                _text.Append($"\n• По площади: {p.areaDamage} урона в {p.areaRadius:0.#} м");
            if (p.lob)
                _text.Append("\n• Навес: бьёт только на излёте, удар по площади при падении");
            if (p.groundRoll)
                _text.Append($"\n• Катится по асфальту, торможение {d.rollDrag:0.#} м/с²");
            if (p.grazeRadius > 0f)
                _text.Append($"\n• Задевает всех в {p.grazeRadius:0.##} м от пути (отброс ×{d.grazeKnockbackScale:0.##}), свечки нет");
            if (p.grazeStun > 0f)
                _text.Append($"\n• Сбивает с ног на {p.grazeStun:0.#} с");
            if (p.bounceAssist > 0f)
                _text.Append($"\n• После рикошета доворачивает к врагу (до {p.bounceAssist:0}°)");
            if (p.homing > 0f)
                _text.Append($"\n• Самонаведение {p.homing:0}°/с");
            if (p.curve != 0f)
                _text.Append($"\n• Кручёный {p.curve:0}°/с");
            if (p.floorBounces > 0)
                _text.Append($"\n• Отскоков от асфальта: {p.floorBounces}");
            return _text.ToString();
        }

        void EnsureStyles()
        {
            if (_box != null)
                return;
            _shade = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _shade.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.08f, 0.82f));
            _shade.Apply();
            _box = new GUIStyle(GUI.skin.box) { normal = { background = _shade } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = Color.white } };
            _small = new GUIStyle(_label) { fontSize = 13, normal = { textColor = new Color(0.82f, 0.86f, 0.9f) } };
            _header = new GUIStyle(_label) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.85f, 0.35f) } };
            _hp = new GUIStyle(_label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.45f, 0.4f) },
            };
        }
    }
}

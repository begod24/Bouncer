using System;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using Bouncer.Waves;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Run
{
    /// <summary>Шаг тренировки во дворе. Ключи заданий в таблице «UI» — tutorial.{шаг в нижнем регистре}.*</summary>
    public enum TutorialStep
    {
        Move,
        Throw,
        Pickup,
        Charge,
        Catch,
        Dash,
        Fight,
        Elite,
        Portfolio,
        Pockets,
        Kiosk,
        Done,
    }

    /// <summary>
    /// Тренировка во дворе (<see cref="Tutorial"/>): задания по шагам на первой арене без волн.
    /// Побегать → выбить неваляшек → подобрать мячи → заряженный бросок → поймать мячи солдатика → рывок →
    /// бой с кучкой врагов → элитка с золотой меткой → портфель и карточка → карманы → ларёк → готово.
    /// Врагов выпускает через <see cref="WaveSpawner"/> (с метками на полу) недалеко от игрока, в сторону середины
    /// арены. Игрока не выбить: жизни не падают ниже одной и восстанавливаются с каждым шагом; ёжиков и свойств
    /// у элиток в обучении нет (<see cref="Danger"/>). Задание и прогресс показывает TutorialPanel (UI), она же
    /// сообщает, что карманы открывали (<see cref="ReportPocketsViewed"/>). Шаг засчитан — пара секунд «Готово!»,
    /// потом следующий. Вне обучения выключен.
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        [SerializeField] WaveSpawner spawner;
        [SerializeField] LootDropper loot;
        [SerializeField] Kiosk kiosk;

        [Header("Враги")]
        [SerializeField] GameObject rolyPoly;
        [SerializeField] GameObject pupsik;
        [SerializeField] GameObject soldier;
        [Tooltip("Элитка шага «Элитка»: из неё выпадает портфель")]
        [SerializeField] GameObject elite;

        [Header("Задания")]
        [SerializeField] float moveDistance = 10f;
        [SerializeField, Min(1)] int throwTargets = 3;
        [SerializeField, Min(1)] int chargedThrows = 2;
        [SerializeField, Min(1)] int catches = 2;
        [SerializeField, Min(1)] int dashes = 2;
        [Tooltip("Бой: солдатиков, пупсов и неваляшек")]
        [SerializeField] Vector3Int fight = new(3, 5, 2);
        [Tooltip("Монетки перед ларьком — чтобы было на что посмотреть")]
        [SerializeField, Min(0)] int kioskCoins = 60;
        [Tooltip("Сколько висит «Готово!» перед следующим шагом, с")]
        [SerializeField] float stepPause = 1.4f;
        [Tooltip("На каком расстоянии от игрока появляются враги, м")]
        [SerializeField] float spawnDistance = 9f;
        [Tooltip("Убитую цель шага (солдатика, элитку) выпустить снова через столько секунд")]
        [SerializeField] float respawnDelay = 1f;

        PlayerController _player;
        PlayerCards _cards;
        Vector3 _center;
        Vector3 _lastPosition;
        float _moved;
        float _lastDash;
        float _nextAt;
        float _respawnAt;
        int _count;
        int _fightTotal;
        bool _eliteDown;
        bool _cardPicked;
        bool _pocketsSeen;
        bool _shopOpened;
        bool _shopClosed;

        /// <summary>Идёт обучение: директор сцены. Вне обучения — null.</summary>
        public static TutorialDirector Instance { get; private set; }
        public TutorialStep Step { get; private set; }
        /// <summary>Номер шага с 1.</summary>
        public int StepNumber => (int)Step + 1;
        /// <summary>Сколько всего заданий (без «Готово»).</summary>
        public static int StepCount => (int)TutorialStep.Done;
        public int Progress { get; private set; }
        public int Target { get; private set; } = 1;
        /// <summary>Шаг засчитан: висит «Готово!», скоро следующий.</summary>
        public bool StepComplete => _nextAt > 0f;
        /// <summary>Все задания пройдены.</summary>
        public bool IsFinished => Step == TutorialStep.Done;

        /// <summary>Сменился шаг или его прогресс — панель переписывает текст.</summary>
        public event Action Changed;

        void Awake()
        {
            if (!Tutorial.Active)
            {
                enabled = false;
                return;
            }
            Instance = this;
        }

        void Start()
        {
            if (!Tutorial.Active)
                return;
            _player = FindFirstObjectByType<PlayerController>();
            if (_player == null)
            {
                enabled = false;
                return;
            }
            _player.TryGetComponent(out _cards);
            _player.Health.Floor = 1;
            _player.Balls.Thrown += OnThrown;
            _player.Balls.Caught += OnCaught;
            if (_cards)
                _cards.Picked += OnCardPicked;
            if (kiosk)
            {
                kiosk.ShopOpened += OnShopOpened;
                kiosk.ShopClosed += OnShopClosed;
            }
            GameEvents.EnemyKilled += OnEnemyKilled;

            _center = ArenaCenter();
            _lastPosition = Flat(_player.transform.position);
            _lastDash = _player.Motor.DashStartTime;
            Enter(TutorialStep.Move);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            if (_player)
            {
                _player.Balls.Thrown -= OnThrown;
                _player.Balls.Caught -= OnCaught;
            }
            if (_cards)
                _cards.Picked -= OnCardPicked;
            if (kiosk)
            {
                kiosk.ShopOpened -= OnShopOpened;
                kiosk.ShopClosed -= OnShopClosed;
            }
        }

        /// <summary>Игрок открыл и закрыл карманы (Tab / Select) — шаг «Карманы» засчитан.</summary>
        public void ReportPocketsViewed() => _pocketsSeen = true;

        void Update()
        {
            var session = GameSession.Instance;
            if (_player == null || session == null || IsFinished)
                return;
            // Пауза, выбор карточки, витрина: задания ждут, бег не считается.
            if (!session.PlayerCanAct)
            {
                _lastPosition = Flat(_player.transform.position);
                return;
            }
            TrackMove();
            TrackDash();
            if (_nextAt > 0f)
            {
                if (Time.time >= _nextAt)
                    Enter(Step + 1);
                return;
            }
            Tick();
        }

        void Tick()
        {
            switch (Step)
            {
                case TutorialStep.Move:
                    if (_moved >= moveDistance)
                        Complete();
                    break;
                case TutorialStep.Throw:
                    SetProgress(_count);
                    if (_count >= throwTargets)
                        Complete();
                    else if (NoEnemies())
                        Spawn(rolyPoly, throwTargets - _count, GroupLayout.Line, 0f);
                    break;
                case TutorialStep.Pickup:
                    Target = _player.Balls.MaxBalls;
                    SetProgress(Mathf.Min(_player.Balls.Balls, Target));
                    if (Progress >= Target)
                        Complete();
                    break;
                case TutorialStep.Charge:
                    SetProgress(_count);
                    if (_count >= chargedThrows)
                        Complete();
                    break;
                case TutorialStep.Catch:
                    SetProgress(_count);
                    if (_count >= catches)
                        Complete();
                    else
                        Respawn(soldier, false);
                    break;
                case TutorialStep.Dash:
                    SetProgress(_count);
                    if (_count >= dashes)
                        Complete();
                    break;
                case TutorialStep.Fight:
                    int left = spawner ? spawner.AliveCount + spawner.PendingEnemies : 0;
                    SetProgress(Mathf.Clamp(_fightTotal - left, 0, _fightTotal));
                    if (left == 0)
                        Complete();
                    break;
                case TutorialStep.Elite:
                    if (_eliteDown)
                        Complete();
                    else
                        Respawn(elite, true);
                    break;
                case TutorialStep.Portfolio:
                    if (_cardPicked)
                        Complete();
                    break;
                case TutorialStep.Pockets:
                    if (_pocketsSeen)
                        Complete();
                    break;
                case TutorialStep.Kiosk:
                    if (_shopClosed)
                        Complete();
                    break;
            }
        }

        void Enter(TutorialStep step)
        {
            Step = step;
            _nextAt = 0f;
            _respawnAt = 0f;
            _count = 0;
            _moved = 0f;
            Progress = 0;
            Target = 1;
            // Каждое задание — с полными сердцами.
            _player.Health.Heal(_player.Health.Max);

            switch (step)
            {
                case TutorialStep.Throw:
                    Target = throwTargets;
                    Spawn(rolyPoly, throwTargets, GroupLayout.Line, 0f);
                    break;
                case TutorialStep.Pickup:
                    Target = _player.Balls.MaxBalls;
                    break;
                case TutorialStep.Charge:
                    Target = chargedThrows;
                    break;
                case TutorialStep.Catch:
                    Target = catches;
                    if (!HasEnemies())
                        Spawn(soldier, 1, GroupLayout.Line, 0f);
                    break;
                case TutorialStep.Dash:
                    Target = dashes;
                    break;
                case TutorialStep.Fight:
                    int before = spawner ? spawner.AliveCount + spawner.PendingEnemies : 0;
                    Spawn(soldier, fight.x, GroupLayout.Line, 0f);
                    Spawn(pupsik, fight.y, GroupLayout.Cluster, 120f);
                    Spawn(rolyPoly, fight.z, GroupLayout.Cluster, -120f);
                    _fightTotal = before + fight.x + fight.y + fight.z;
                    Target = _fightTotal;
                    break;
                case TutorialStep.Elite:
                    _eliteDown = false;
                    Spawn(elite, 1, GroupLayout.Cluster, 0f, true);
                    break;
                case TutorialStep.Portfolio:
                    // Портфель выпал из элитки. Если его нет (исчез, не выпал) — положить рядом с игроком.
                    if (!_cardPicked && loot && FindAnyObjectByType<PortfolioPickup>() == null)
                        loot.PlacePortfolio(PointAround(3f, 0f));
                    break;
                case TutorialStep.Pockets:
                    _pocketsSeen = false;
                    break;
                case TutorialStep.Kiosk:
                    OpenKiosk();
                    break;
                case TutorialStep.Done:
                    // Дальше только экран «Готов гулять!»: игрок стоит, клики по кнопкам не бросают мяч.
                    Tutorial.MarkCompleted();
                    _player.BeginScripted();
                    GameEvents.PlaySound(SoundCue.Victory, Vector3.zero);
                    break;
            }
            Changed?.Invoke();
        }

        /// <summary>Шаг «Ларёк»: остатки врагов уходят, арена «пройдена», монетки на первый раз, окошко загорается.</summary>
        void OpenKiosk()
        {
            _shopOpened = false;
            _shopClosed = false;
            var session = GameSession.Instance;
            if (spawner)
                spawner.DespawnAll();
            if (session)
                session.ClearArena();
            if (loot && kioskCoins > 0)
                loot.DropCoins(kioskCoins, _player.transform.position + Vector3.up * 0.5f);
            CoinPickup.CollectAll();
            if (kiosk && _cards)
                kiosk.Open(_cards, 0);
            // Без ларька шаг не пройти — засчитать сразу.
            if (!kiosk || !_cards)
                _shopClosed = true;
        }

        void Complete()
        {
            _nextAt = Time.time + stepPause;
            Progress = Target;
            GameEvents.PlaySound(SoundCue.CardPick, _player.transform.position);
            Changed?.Invoke();
        }

        void SetProgress(int value)
        {
            if (value == Progress)
                return;
            Progress = value;
            Changed?.Invoke();
        }

        void OnThrown(ThrowStats stats)
        {
            if (Step == TutorialStep.Charge && !StepComplete && stats.Has(HitFlags.Charged))
                _count++;
        }

        void OnCaught(CatchInfo info)
        {
            if (Step == TutorialStep.Catch && !StepComplete && info.EnemyBall)
                _count++;
        }

        void OnEnemyKilled(GameObject enemy, HitInfo hit)
        {
            if (hit.Has(HitFlags.Despawn))
                return;
            if (Step == TutorialStep.Throw && !StepComplete)
                _count++;
            if (enemy && enemy.TryGetComponent(out EnemyReward reward) && reward.Portfolio)
                _eliteDown = true;
        }

        void OnCardPicked(UpgradeCard card) => _cardPicked = true;

        void OnShopOpened() => _shopOpened = true;

        void OnShopClosed()
        {
            if (_shopOpened)
                _shopClosed = true;
        }

        void TrackMove()
        {
            Vector3 position = Flat(_player.transform.position);
            float step = Vector3.Distance(position, _lastPosition);
            // Большой скачок — не бег (телепорт, отброс).
            if (step < 2f)
                _moved += step;
            _lastPosition = position;
        }

        void TrackDash()
        {
            float dash = _player.Motor.DashStartTime;
            if (Mathf.Approximately(dash, _lastDash))
                return;
            _lastDash = dash;
            if (Step == TutorialStep.Dash && !StepComplete)
                _count++;
        }

        bool HasEnemies() => spawner && spawner.AliveCount + spawner.PendingEnemies > 0;

        bool NoEnemies() => !HasEnemies();

        /// <summary>Цель шага выбита раньше, чем задание сделано, — через respawnDelay выпустить новую.</summary>
        void Respawn(GameObject prefab, bool isElite)
        {
            if (HasEnemies())
            {
                _respawnAt = 0f;
                return;
            }
            if (_respawnAt <= 0f)
            {
                _respawnAt = Time.time + respawnDelay;
                return;
            }
            if (Time.time < _respawnAt)
                return;
            _respawnAt = 0f;
            Spawn(prefab, 1, isElite ? GroupLayout.Cluster : GroupLayout.Line, 0f, isElite);
        }

        void Spawn(GameObject prefab, int count, GroupLayout layout, float angle, bool isElite = false)
        {
            if (prefab && spawner && count > 0)
                spawner.QueueGroupAt(prefab, count, layout, PointAround(spawnDistance, angle), elite: isElite);
        }

        /// <summary>
        /// Точка на проходимой части арены примерно в distance от игрока: в сторону середины арены (чтобы не у стены),
        /// повёрнутая на angle; если там не пройти — соседние направления через 30°.
        /// </summary>
        Vector3 PointAround(float distance, float angle)
        {
            Vector3 origin = Flat(_player.transform.position);
            Vector3 toCenter = _center - origin;
            Vector3 direction = toCenter.sqrMagnitude > 9f ? toCenter.normalized : Flat(_player.transform.forward).normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.forward;
            direction = Quaternion.Euler(0f, angle, 0f) * direction;
            for (int i = 0; i < 12; i++)
            {
                float turn = (i % 2 == 0 ? 1f : -1f) * ((i + 1) / 2) * 30f;
                Vector3 candidate = origin + Quaternion.Euler(0f, turn, 0f) * direction * distance;
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                    return hit.position;
            }
            return origin + direction * distance;
        }

        /// <summary>Середина арены — среднее точек спавна (они по краям).</summary>
        Vector3 ArenaCenter()
        {
            var points = spawner ? spawner.SpawnPoints : null;
            if (points == null || points.Length == 0)
                return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var point in points)
                if (point)
                {
                    sum += point.position;
                    count++;
                }
            return count > 0 ? Flat(sum / count) : Vector3.zero;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

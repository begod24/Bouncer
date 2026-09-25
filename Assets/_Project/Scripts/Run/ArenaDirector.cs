using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Upgrades;
using Bouncer.Visuals;
using Bouncer.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Run
{
    /// <summary>
    /// Арена в прогулке. Узнаёт по <see cref="RunState.ArenaIndex"/>, какую арену играет сцена, и настраивает её:
    /// волны, время суток, ларёк, стрелку дальше. Следит за условием победы; когда оно выполнено — оставшиеся враги
    /// исчезают, монетки слетаются к игроку, после босса предлагается карточка, открывается ларёк и появляется
    /// стрелка на следующую арену. На последней арене победа заканчивает прогулку.
    /// Ещё раскладывает на арене найденный портфель.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public sealed class ArenaDirector : MonoBehaviour
    {
        [SerializeField] RunDefinition run;
        [Tooltip("Какую арену прогулки играть, если сцену запустили саму по себе и в прогулке её нет")]
        [SerializeField, Min(0)] int defaultArena;
        [SerializeField] WaveSpawner spawner;
        [SerializeField] TimeOfDayController timeOfDay;
        [Tooltip("Погода арены: пусто — всегда ясно")]
        [SerializeField] WeatherController weather;
        [SerializeField] LootDropper loot;
        [SerializeField] Kiosk kiosk;
        [SerializeField] ArenaExit exit;
        [Tooltip("Где может лежать найденный портфель. Пусто — точки спавна врагов")]
        [SerializeField] Transform[] portfolioSpots;
        [Tooltip("Портфель не кладётся ближе этого к игроку, м")]
        [SerializeField] float portfolioMinDistance = 8f;

        readonly List<float> _portfolioTimes = new();
        PlayerCards _player;
        bool _complete;

        public static ArenaDirector Instance { get; private set; }
        public RunDefinition Run => run;
        public ArenaDefinition Arena { get; private set; }
        public int ArenaIndex { get; private set; }
        public bool IsLastArena => run == null || run.IsLast(ArenaIndex);
        public ArenaDefinition NextArena => run ? run.Get(ArenaIndex + 1) : null;
        /// <summary>Арена пройдена (ларёк и стрелка открыты).</summary>
        public bool IsComplete => _complete;
        /// <summary>Какая погода выпала на эту арену.</summary>
        public WeatherKind Weather { get; private set; }
        public Kiosk Kiosk => kiosk;
        public ArenaExit Exit => exit;

        void Awake()
        {
            Instance = this;
            if (run == null || run.Count == 0)
                return;
            RunState.FirstScene = run.arenas[0].sceneName;

            string scene = SceneManager.GetActiveScene().name;
            int index = RunState.Active ? RunState.ArenaIndex : defaultArena;
            var candidate = run.Get(index);
            if (candidate == null || candidate.sceneName != scene)
            {
                // Сцену запустили саму по себе (в редакторе): прогулка начинается с неё.
                index = run.IndexOfScene(scene);
                if (index < 0)
                    index = Mathf.Clamp(defaultArena, 0, run.Count - 1);
                if (RunState.Active && index != RunState.ArenaIndex)
                    RunState.BeginAt(index);
            }
            ArenaIndex = index;
            Arena = run.Get(index);
            Apply();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable() => GameEvents.BossDefeated += OnBossDefeated;

        void OnDisable() => GameEvents.BossDefeated -= OnBossDefeated;

        void Start()
        {
            var player = FindFirstObjectByType<Bouncer.Player.PlayerController>();
            if (player)
                player.TryGetComponent(out _player);
        }

        void Apply()
        {
            if (Arena == null)
                return;
            if (spawner && Arena.wave)
                spawner.Wave = Arena.wave;
            if (timeOfDay && Arena.timeOfDay != null && Arena.timeOfDay.Length > 0)
                timeOfDay.Configure(Arena.timeOfDay, Arena.wave ? Arena.wave.duration : 0f);
            if (exit)
                exit.Hide();
            Weather = RollWeather();
            if (weather)
                weather.Begin(Weather);

            _portfolioTimes.Clear();
            float duration = Arena.wave ? Arena.wave.duration : 300f;
            for (int i = 0; i < Arena.portfolioFinds; i++)
                _portfolioTimes.Add(Random.Range(Arena.portfolioWindow.x, Arena.portfolioWindow.y) * duration);
            _portfolioTimes.Sort();
        }

        /// <summary>Непогода выпадает с шансом арены, какая именно — случайно из её списка.</summary>
        WeatherKind RollWeather()
        {
            if (weather == null || Arena.weathers == null || Arena.weathers.Length == 0 || Random.value >= Arena.weatherChance)
                return WeatherKind.Clear;
            return Arena.weathers[Random.Range(0, Arena.weathers.Length)];
        }

        void Update()
        {
            var session = GameSession.Instance;
            if (Arena == null || _complete || session == null || session.State != SessionState.Playing)
                return;
            float time = spawner ? spawner.WaveTime : session.SurvivalTime;

            while (_portfolioTimes.Count > 0 && time >= _portfolioTimes[0])
            {
                _portfolioTimes.RemoveAt(0);
                PlaceFoundPortfolio();
            }

            float duration = Arena.wave ? Arena.wave.duration : 0f;
            switch (Arena.goal)
            {
                case ArenaGoal.SurviveAndClear:
                    if (spawner && time >= duration && spawner.BurstsDone && spawner.AliveCount == 0 && spawner.PendingEnemies == 0)
                        Complete(boss: false);
                    break;
                case ArenaGoal.SurviveUntilCall:
                    if (time >= duration)
                        Complete(boss: false);
                    break;
            }
        }

        void OnBossDefeated()
        {
            if (Arena != null && Arena.goal is (ArenaGoal.DefeatBoss or ArenaGoal.SurviveUntilCall))
                Complete(boss: Arena.goal == ArenaGoal.DefeatBoss);
        }

        /// <summary>Условие победы выполнено.</summary>
        void Complete(bool boss)
        {
            var session = GameSession.Instance;
            if (_complete || session == null)
                return;
            _complete = true;

            if (IsLastArena)
            {
                session.Win();
                return;
            }

            session.ClearArena();
            if (spawner)
            {
                spawner.Spawning = false;
                spawner.DespawnAll();
            }
            CoinPickup.CollectAll();
            if (boss && _player)
                _player.QueueOffer(OfferKind.Boss);
            if (kiosk && Arena.kiosk && _player)
                kiosk.Open(_player, ArenaIndex);
            if (exit)
            {
                var next = NextArena;
                string label = next != null && !next.arrivalLabel.IsEmpty ? next.arrivalLabel.GetLocalizedString() : "→";
                exit.Show(label);
            }
        }

        /// <summary>Игрок дошёл до стрелки: следующая арена. false — уйти сейчас нельзя.</summary>
        public bool LeaveArena()
        {
            var session = GameSession.Instance;
            var next = NextArena;
            if (!_complete || session == null || next == null || session.State != SessionState.Cleared)
                return false;
            int lives = _player ? _player.Player.Health.Current : 0;
            session.LeaveArena(next.sceneName, lives);
            return true;
        }

        /// <summary>Портфель где-нибудь у края арены, подальше от игрока.</summary>
        void PlaceFoundPortfolio()
        {
            if (loot == null)
                return;
            var spots = portfolioSpots != null && portfolioSpots.Length > 0 ? portfolioSpots : spawner ? spawner.SpawnPoints : null;
            if (spots == null || spots.Length == 0)
                return;
            Vector3 playerPosition = _player ? _player.transform.position : Vector3.zero;
            Transform best = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var spot = spots[Random.Range(0, spots.Length)];
                if (!spot)
                    continue;
                best = spot;
                Vector3 delta = spot.position - playerPosition;
                delta.y = 0f;
                if (delta.sqrMagnitude >= portfolioMinDistance * portfolioMinDistance)
                    break;
            }
            if (best == null)
                return;
            loot.PlacePortfolio(best.position);
            GameEvents.PlaySound(SoundCue.Portfolio, best.position);
        }
    }
}

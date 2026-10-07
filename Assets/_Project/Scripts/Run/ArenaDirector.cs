using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Upgrades;
using Bouncer.Visuals;
using Bouncer.Waves;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Run
{
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
        [Tooltip("Вторая стрелка — на другую арену развилки (если на следующем этапе она есть)")]
        [SerializeField] ArenaExit forkExit;
        [Tooltip("Где может лежать найденный портфель. Пусто — точки спавна врагов")]
        [SerializeField] Transform[] portfolioSpots;
        [Tooltip("Портфель не кладётся ближе этого к игроку, м")]
        [SerializeField] float portfolioMinDistance = 8f;

        readonly List<float> _portfolioTimes = new();
        bool _complete;
        float _weatherWaitUntil;

        public static ArenaDirector Instance { get; private set; }
        public RunDefinition Run => run;
        public ArenaDefinition Arena { get; private set; }
        public int ArenaIndex { get; private set; }
        public int ArenaVariant { get; private set; }
        public bool IsLastArena => run == null || run.IsLast(ArenaIndex);
        public ArenaDefinition NextArena => run ? run.Get(ArenaIndex + 1) : null;
        public bool IsComplete => _complete;
        public bool ClearedByBoss { get; private set; }
        public WeatherKind Weather { get; private set; }
        public int WeatherSeed { get; private set; }
        public bool WeatherKnown { get; private set; }
        public float ArenaTime => spawner ? spawner.WaveTime : GameSession.Instance ? GameSession.Instance.SurvivalTime : 0f;
        public float ArenaDuration => Arena != null && Arena.wave
            ? Arena.wave.duration + (Arena.goal == ArenaGoal.SurviveUntilCall ? Danger.FinalExtraTime : 0f)
            : 0f;
        public Kiosk Kiosk => kiosk;
        public ArenaExit Exit => exit;

        public static event System.Action<bool, bool> Completed;

        void Awake()
        {
            Instance = this;
            if (run == null || run.Count == 0)
                return;
            RunState.FirstScene = run.arenas[0].sceneName;

            string scene = SceneManager.GetActiveScene().name;
            int index = RunState.Active ? RunState.ArenaIndex : defaultArena;
            int variant = RunState.Active ? RunState.ArenaVariant : 0;
            var candidate = run.Get(index, variant);
            if (candidate == null || candidate.sceneName != scene)
            {
                if (!run.FindScene(scene, out index, out variant))
                {
                    index = Mathf.Clamp(defaultArena, 0, run.Count - 1);
                    variant = 0;
                }
                if (RunState.Active && (index != RunState.ArenaIndex || variant != RunState.ArenaVariant))
                    RunState.BeginAt(index, variant);
            }
            ArenaIndex = index;
            ArenaVariant = variant;
            Arena = run.Get(index, variant);
            Apply();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void OnEnable()
        {
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.RunFinished += OnRunFinished;
        }

        void OnDisable()
        {
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.RunFinished -= OnRunFinished;
        }

        void Apply()
        {
            ArenaMusic.FinalTheme = Arena != null && Arena.finalMusic;
            if (Arena == null)
                return;
            EnemyScaling.ArenaHits = Arena.enemyHitsMultiplier;
            EnemyScaling.ArenaIndex = ArenaIndex;
            EnemyScaling.Players = RunState.Active ? RunState.PlayerCount : 1;
            if (loot)
                loot.ElitePortfolioLimit = Arena.elitePortfolios;
            if (spawner && Arena.wave)
                spawner.Wave = Arena.wave;
            if (timeOfDay && Arena.timeOfDay != null && Arena.timeOfDay.Length > 0)
                timeOfDay.Configure(Arena.timeOfDay, Arena.wave ? Arena.wave.duration : 0f);
            if (exit)
                exit.Hide();
            if (forkExit)
                forkExit.Hide();
            _portfolioTimes.Clear();
            if (Tutorial.Active || NetHooks.IsGuest)
            {
                if (spawner)
                    spawner.Spawning = false;
                if (weather)
                    weather.Begin(WeatherKind.Clear);
                WeatherKnown = Tutorial.Active;
                _weatherWaitUntil = Time.unscaledTime + 2.5f;
                return;
            }
            Weather = RollWeather();
            WeatherSeed = Random.Range(1, int.MaxValue);
            WeatherKnown = true;
            if (weather)
                weather.Begin(Weather, WeatherSeed);

            float duration = Arena.wave ? Arena.wave.duration : 300f;
            for (int i = 0; i < Arena.portfolioFinds; i++)
                _portfolioTimes.Add(Random.Range(Arena.portfolioWindow.x, Arena.portfolioWindow.y) * duration);
            _portfolioTimes.Sort();
        }

        WeatherKind RollWeather()
        {
            if (weather == null || Arena.weathers == null || Arena.weathers.Length == 0 || Random.value >= Arena.weatherChance)
                return WeatherKind.Clear;
            return Arena.weathers[Random.Range(0, Arena.weathers.Length)];
        }

        public void ApplyWeatherFromNetwork(WeatherKind kind, int seed)
        {
            if (WeatherKnown && Weather == kind && WeatherSeed == seed)
                return;
            Weather = kind;
            WeatherSeed = seed;
            WeatherKnown = true;
            if (weather)
                weather.Begin(kind, seed, remote: true);
        }

        void Update()
        {
            if (!WeatherKnown && Time.unscaledTime >= _weatherWaitUntil)
                WeatherKnown = true;
            var session = GameSession.Instance;
            if (Arena == null || _complete || session == null || session.State != SessionState.Playing || NetHooks.IsGuest)
                return;
            float time = spawner ? spawner.WaveTime : session.SurvivalTime;

            while (_portfolioTimes.Count > 0 && time >= _portfolioTimes[0])
            {
                _portfolioTimes.RemoveAt(0);
                PlaceFoundPortfolio();
            }

            float duration = ArenaDuration;
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
            OpenBossPages();
            if (Arena != null && Arena.goal is (ArenaGoal.DefeatBoss or ArenaGoal.SurviveUntilCall))
                Complete(boss: Arena.goal == ArenaGoal.DefeatBoss, bossDefeated: true);
        }

        void OnRunFinished(bool victory)
        {
            if (victory)
                OpenBossPages();
        }

        void OpenBossPages()
        {
            if (Arena == null || Arena.wave == null || Tutorial.Active)
                return;
            foreach (var burst in Arena.wave.bursts)
            {
                if (!burst.boss)
                    continue;
                if (burst.prefab)
                    BestiaryProgress.Open(burst.prefab.name);
                if (burst.variants != null)
                    foreach (var variant in burst.variants)
                        if (variant)
                            BestiaryProgress.Open(variant.name);
            }
        }

        void Complete(bool boss, bool bossDefeated = false)
        {
            var session = GameSession.Instance;
            if (_complete || session == null)
                return;
            _complete = true;
            ClearedByBoss = boss;

            if (IsLastArena)
            {
                var home = HomeCall.Instance;
                if (Arena.goal == ArenaGoal.SurviveUntilCall && home != null && home.IsFinale)
                {
                    home.BeginCall(bossDefeated);
                    return;
                }
                if (spawner)
                    spawner.Spawning = false;
                session.Win();
                Completed?.Invoke(boss, true);
                return;
            }

            session.ClearArena();
            if (spawner)
            {
                spawner.Spawning = false;
                spawner.DespawnAll();
            }
            CoinPickup.CollectAll();
            Completed?.Invoke(boss, false);
            OpenAfterClear(boss);
        }

        void OpenAfterClear(bool boss)
        {
            PlayerCards customer = null;
            foreach (var player in Players.All)
            {
                if (!player.IsLocal || !player.TryGetComponent(out PlayerCards cards))
                    continue;
                if (boss)
                    cards.QueueOffer(OfferKind.Boss);
                cards.OpenWholeBackpack();
                if (player == Players.Local)
                    customer = cards;
            }
            if (kiosk && Arena.kiosk && customer)
                kiosk.Open(customer, ArenaIndex);
            if (exit)
                exit.Show(ArrivalLabel(NextArena), 0);
            if (forkExit)
            {
                var fork = run ? run.Get(ArenaIndex + 1, 1) : null;
                if (fork != null)
                    forkExit.Show(ArrivalLabel(fork), 1);
                else
                    forkExit.Hide();
            }
        }

        public void WinFromHome(bool boss)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.Win();
            Completed?.Invoke(boss, true);
        }

        public void CompleteFromNetwork(bool boss, bool last)
        {
            if (_complete)
                return;
            _complete = true;
            ClearedByBoss = boss;
            if (boss)
                OpenBossPages();
            var session = GameSession.Instance;
            if (session == null)
                return;
            if (last)
            {
                session.Win();
                return;
            }
            session.ClearArena();
            OpenAfterClear(boss);
        }

        public void SyncWaveTime(float time)
        {
            if (spawner && Mathf.Abs(spawner.WaveTime - time) > 0.25f)
                spawner.WaveTime = time;
        }

        static string ArrivalLabel(ArenaDefinition next) =>
            next != null && !next.arrivalLabel.IsEmpty ? next.arrivalLabel.GetLocalizedString() : "→";

        public bool LeaveArena(int variant = 0)
        {
            var session = GameSession.Instance;
            var next = run ? run.Get(ArenaIndex + 1, variant) : null;
            if (!_complete || session == null || next == null || session.State != SessionState.Cleared)
                return false;
            if (Online.Active)
                return Online.IsHost && NetHooks.LeaveArena != null && NetHooks.LeaveArena(variant);
            foreach (var player in Players.All)
                RunState.SetLives(player.Slot, Mathf.Max(1, player.Health.Current));
            RunState.NextVariant = variant;
            session.LeaveArena(next.sceneName);
            return true;
        }

        public string NextSceneName(int variant)
        {
            var next = run ? run.Get(ArenaIndex + 1, variant) : null;
            return next != null ? next.sceneName : null;
        }

        public void LeaveOnline(int variant)
        {
            var session = GameSession.Instance;
            if (session == null || session.State != SessionState.Cleared)
                return;
            foreach (var player in Players.All)
                if (player.IsLocal)
                    RunState.SetLives(player.Slot, Mathf.Max(1, player.Health.Current));
            RunState.NextVariant = variant;
            session.AdvanceOnline();
        }

        static float DistanceToNearestPlayer(Vector3 position)
        {
            float best = float.PositiveInfinity;
            foreach (var player in Players.All)
            {
                Vector3 delta = player.transform.position - position;
                delta.y = 0f;
                best = Mathf.Min(best, delta.magnitude);
            }
            return best;
        }

        void PlaceFoundPortfolio()
        {
            if (loot == null)
                return;
            var spots = portfolioSpots != null && portfolioSpots.Length > 0 ? portfolioSpots : spawner ? spawner.SpawnPoints : null;
            if (spots == null || spots.Length == 0)
                return;
            Transform best = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var spot = spots[Random.Range(0, spots.Length)];
                if (!spot)
                    continue;
                best = spot;
                if (DistanceToNearestPlayer(spot.position) >= portfolioMinDistance)
                    break;
            }
            if (best == null)
                return;
            loot.PlaceFind(best.position, Arena.findLemonade, Arena.findCoins);
            GameEvents.PlaySound(SoundCue.Portfolio, best.position);
        }
    }
}

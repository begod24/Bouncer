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
    /// <summary>
    /// Арена в прогулке. Узнаёт по <see cref="RunState.ArenaIndex"/>, какую арену играет сцена, и настраивает её:
    /// волны, время суток, ларёк, стрелку дальше. Следит за условием победы; когда оно выполнено — оставшиеся враги
    /// исчезают, монетки слетаются к игроку, после босса предлагается карточка, открывается ларёк и появляется
    /// стрелка на следующую арену (на развилке — две, по одной на каждую арену этапа). На последней арене победа
    /// заканчивает прогулку.
    /// Ещё раскладывает на арене найденный портфель и открывает в бестиарии выбитого босса (<see cref="BestiaryProgress"/>).
    /// В обучении (<see cref="Tutorial"/>) арена только ставит время суток: волн, находок и погоды нет —
    /// врагов выпускает <see cref="TutorialDirector"/>.
    /// По сети волны, находки и условие победы ведёт хозяин комнаты; гость узнаёт от него, что арена пройдена
    /// (<see cref="CompleteFromNetwork"/>), и сверяет с ним часы волн. Дальше у каждого своё: карточка за босса,
    /// неоткрытые портфели из рюкзака и своя витрина ларька; стрелки — общие: на следующую арену все идут, когда
    /// все живые встали у одной стрелки (<see cref="ArenaExit"/>, решает хозяин, переход — <see cref="LeaveOnline"/>).
    /// Финал по сети: мама зовёт у всех, а победу объявляет хозяин, когда дома все живые (<see cref="WinFromHome"/>).
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
        /// <summary>Какая арена этапа играется: 0 — основная, 1 и дальше — развилки.</summary>
        public int ArenaVariant { get; private set; }
        /// <summary>Последняя арена прогулки: её победа заканчивает прогулку.</summary>
        public bool IsLastArena => run == null || run.IsLast(ArenaIndex);
        public ArenaDefinition NextArena => run ? run.Get(ArenaIndex + 1) : null;
        /// <summary>Арена пройдена (ларёк и стрелка открыты).</summary>
        public bool IsComplete => _complete;
        /// <summary>Какая погода выпала на эту арену.</summary>
        public WeatherKind Weather { get; private set; }
        /// <summary>Из чего разложены лужи (по сети гостям — чтобы у всех одинаково).</summary>
        public int WeatherSeed { get; private set; }
        /// <summary>Погода уже известна: у гостя — когда её прислал хозяин (или ждать надоело).</summary>
        public bool WeatherKnown { get; private set; }
        /// <summary>Секунды боя на арене — по часам волн.</summary>
        public float ArenaTime => spawner ? spawner.WaveTime : GameSession.Instance ? GameSession.Instance.SurvivalTime : 0f;
        /// <summary>Сколько длится бой по волнам арены, с (финал: время до зова мамы; на 5-й опасности зовёт позже).</summary>
        public float ArenaDuration => Arena != null && Arena.wave
            ? Arena.wave.duration + (Arena.goal == ArenaGoal.SurviveUntilCall ? Danger.FinalExtraTime : 0f)
            : 0f;
        public Kiosk Kiosk => kiosk;
        public ArenaExit Exit => exit;

        /// <summary>
        /// Арена пройдена (у хозяина комнаты — чтобы сказать гостям): выбит ли босс (карточка за него) и последняя ли
        /// это арена (победа).
        /// </summary>
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
                // Сцену запустили саму по себе (в редакторе): прогулка начинается с неё.
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
            // Гость: врагов и находки выпускает хозяин комнаты, погоду он же пришлёт (ApplyWeatherFromNetwork).
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

        /// <summary>Непогода выпадает с шансом арены, какая именно — случайно из её списка.</summary>
        WeatherKind RollWeather()
        {
            if (weather == null || Arena.weathers == null || Arena.weathers.Length == 0 || Random.value >= Arena.weatherChance)
                return WeatherKind.Clear;
            return Arena.weathers[Random.Range(0, Arena.weathers.Length)];
        }

        /// <summary>По сети у гостя: погода, которую выбрал хозяин, и из чего у него разложены лужи.</summary>
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
            // Гость не дождался погоды от хозяина — значит, ясно (надпись арены не ждёт вечно).
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

        /// <summary>Прогулка выиграна: босс финала побеждён, даже если его не выбили, а дождались мамы.</summary>
        void OnRunFinished(bool victory)
        {
            if (victory)
                OpenBossPages();
        }

        /// <summary>Бестиарий: открыть страницу босса этой арены (по имени префаба из его выхода в волнах).</summary>
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

        /// <summary>Условие победы выполнено. boss — предложить карточку за босса.</summary>
        void Complete(bool boss, bool bossDefeated = false)
        {
            var session = GameSession.Instance;
            if (_complete || session == null)
                return;
            _complete = true;

            if (IsLastArena)
            {
                // Финал: мама позвала — но прогулка кончится, только когда игрок добежит до подъезда.
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

        /// <summary>
        /// Арена пройдена — у каждого компьютера своё: карточка за босса (каждому игроку своя), портфели из рюкзака,
        /// витрина ларька для своего игрока; и стрелки дальше (на развилке — две).
        /// </summary>
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
            // Развилка: вторая стрелка ведёт на другую арену следующего этапа.
            if (forkExit)
            {
                var fork = run ? run.Get(ArenaIndex + 1, 1) : null;
                if (fork != null)
                    forkExit.Show(ArrivalLabel(fork), 1);
                else
                    forkExit.Hide();
            }
        }

        /// <summary>
        /// По сети, у хозяина: финал — все живые дома, у подъезда. Прогулка выиграна у всех (boss — босс выбит).
        /// </summary>
        public void WinFromHome(bool boss)
        {
            var session = GameSession.Instance;
            if (session == null)
                return;
            session.Win();
            Completed?.Invoke(boss, true);
        }

        /// <summary>По сети у гостя: хозяин сказал, что арена пройдена (boss — за босса карточка, last — прогулка выиграна).</summary>
        public void CompleteFromNetwork(bool boss, bool last)
        {
            if (_complete)
                return;
            _complete = true;
            // Босс выбит у хозяина — страница бестиария открывается и у гостя.
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

        /// <summary>По сети у гостя: часы волн — как у хозяина (по ним идёт время суток).</summary>
        public void SyncWaveTime(float time)
        {
            if (spawner && Mathf.Abs(spawner.WaveTime - time) > 0.25f)
                spawner.WaveTime = time;
        }

        static string ArrivalLabel(ArenaDefinition next) =>
            next != null && !next.arrivalLabel.IsEmpty ? next.arrivalLabel.GetLocalizedString() : "→";

        /// <summary>
        /// Игрок дошёл до стрелки: следующая арена (variant — по какой стрелке: 0 — основная, 1 — развилка).
        /// false — уйти сейчас нельзя.
        /// </summary>
        public bool LeaveArena(int variant = 0)
        {
            var session = GameSession.Instance;
            var next = run ? run.Get(ArenaIndex + 1, variant) : null;
            if (!_complete || session == null || next == null || session.State != SessionState.Cleared)
                return false;
            // По сети всех ведёт хозяин комнаты.
            if (Online.Active)
                return Online.IsHost && NetHooks.LeaveArena != null && NetHooks.LeaveArena(variant);
            // Выбитый в коопе приходит на следующую арену с одним сердцем.
            foreach (var player in Players.All)
                RunState.SetLives(player.Slot, Mathf.Max(1, player.Health.Current));
            RunState.NextVariant = variant;
            session.LeaveArena(next.sceneName);
            return true;
        }

        /// <summary>Сцена следующей арены по стрелке variant. null — дальше арен нет.</summary>
        public string NextSceneName(int variant)
        {
            var next = run ? run.Get(ArenaIndex + 1, variant) : null;
            return next != null ? next.sceneName : null;
        }

        /// <summary>
        /// По сети, на каждом компьютере: хозяин ведёт всех на следующую арену по стрелке variant. Своему игроку
        /// записываются сердца (выбитый придёт с одним), экран гаснет; сцену грузит сеть.
        /// </summary>
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

        /// <summary>Портфель где-нибудь у края арены, подальше от игроков.</summary>
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

using System.Collections;
using System.Collections.Generic;
using Bouncer.Balls;
using Bouncer.Core;
using Bouncer.Player;
using Bouncer.Visuals;
using Bouncer.Waves;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Bouncer.Trailer
{
    // Постановка кадров для ролика: убрать волны, поставить детей-кукол и врагов, держать камеру.
    public static class TrailerStage
    {
        const string PlayerPrefab = "Assets/_Project/Prefabs/Player.prefab";
        const string EnemyFolder = "Assets/_Project/Prefabs/Enemies/";
        const string TimeOfDayFolder = "Assets/_Project/Data/TimeOfDay/";

        public static readonly List<TrailerPuppet> Kids = new();
        static CinemachineCamera s_cam;

        public static void Prepare()
        {
            Time.captureFramerate = TrailerRecorder.Fps;
            var session = GameSession.Instance;
            if (session != null && session.State == Bouncer.Core.SessionState.Title)
                session.StartRun();
            foreach (var spawner in Object.FindObjectsByType<WaveSpawner>(FindObjectsSortMode.None))
            {
                spawner.Spawning = false;
                spawner.DespawnAll();
            }
            Ball.DespawnAllLoose();
            ClearOffers();
            MuteMusic();
            RemoveKids();
        }

        // Предложения карточек, заработанные в прошлом кадре, переезжают в следующий (RunCards.Carried) и
        // открывают выбор карточки с заморозкой времени — снимаем их.
        public static void ClearOffers()
        {
            for (int slot = 0; slot < RunState.MaxPlayers; slot++)
            {
                RunState.StartCardChosen(slot);
                Bouncer.Upgrades.RunCards.Carried(slot).Clear();
            }
            var session = GameSession.Instance;
            if (session != null && session.State == Bouncer.Core.SessionState.Upgrade)
                session.EndUpgradeChoice();
        }

        public static void MuteMusic()
        {
            var music = GameObject.Find("Music");
            if (music)
                foreach (var source in music.GetComponentsInChildren<AudioSource>(true))
                    source.mute = true;
        }

        public static void RemoveKids()
        {
            for (int i = Players.All.Count - 1; i >= 0; i--)
                Object.Destroy(Players.All[i].gameObject);
            Kids.Clear();
        }

        public static TrailerPuppet SpawnKid(int slot, int kid, Vector3 position, float yaw, int balls = 3)
        {
            ClearOffers();
            var prefab = AssetDatabase.LoadAssetAtPath<PlayerController>(PlayerPrefab);
            var holder = new GameObject("TrailerHold");
            holder.SetActive(false);
            var player = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), holder.transform);
            player.name = "Kid_" + slot;
            Object.DestroyImmediate(player.GetComponent<PlayerInputReader>());
            var puppet = player.gameObject.AddComponent<TrailerPuppet>();
            puppet.Home = position;
            puppet.AimDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            player.transform.SetParent(null, true);
            Object.Destroy(holder);
            player.Setup(slot, true);
            player.Health.Floor = 1;
            var kidView = player.GetComponentInChildren<PlayerKid>();
            if (kidView)
                kidView.ShowKid(kid);
            player.Balls.SetHands(balls);
            // линия прицела — подсказка интерфейса, в ролике она только мешает
            foreach (var aim in player.GetComponentsInChildren<AimIndicator>(true))
            {
                aim.enabled = false;
                foreach (var line in aim.GetComponents<LineRenderer>())
                    line.enabled = false;
            }
            Kids.Add(puppet);
            return puppet;
        }

        public static GameObject Enemy(string prefabName, Vector3 position, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyFolder + prefabName + ".prefab");
            if (prefab == null)
            {
                Debug.LogError("[Trailer] no enemy " + prefabName);
                return null;
            }
            return PoolService.Spawn(prefab, position, Quaternion.Euler(0f, yaw, 0f));
        }

        // Группа как у WaveSpawner: отряд солдатиков, стая — члены группы узнают друг друга.
        public static List<GameObject> Group(string prefabName, IReadOnlyList<Vector3> positions, float yaw, bool elite = false)
        {
            var spawned = new List<GameObject>();
            foreach (var p in positions)
            {
                var go = Enemy(prefabName, p, yaw);
                if (go)
                    spawned.Add(go);
            }
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i].TryGetComponent(out Bouncer.Enemies.IGroupMember member))
                    member.OnGroupSpawned(spawned, i);
            if (elite)
                foreach (var go in spawned)
                    Bouncer.Enemies.EliteAffix.Assign(go, Bouncer.Enemies.AffixKind.None);
            return spawned;
        }

        public static void Card(TrailerPuppet kid, string cardName)
        {
            var card = AssetDatabase.LoadAssetAtPath<Bouncer.Upgrades.UpgradeCard>("Assets/_Project/Data/Upgrades/" + cardName + ".asset");
            if (card == null)
            {
                Debug.LogError("[Trailer] no card " + cardName);
                return;
            }
            card.Apply(kid.Player);
        }

        public static Ball RollBall(Vector3 position, Vector3 velocity, string prefab = "Assets/_Project/Prefabs/Ball.prefab")
        {
            var ballPrefab = AssetDatabase.LoadAssetAtPath<Ball>(prefab);
            var ball = PoolService.Spawn(ballPrefab, position, Quaternion.identity);
            ball.Drop(position, velocity);
            return ball;
        }

        // Метка появления, как у волн: круг на земле и звук, потом сам враг.
        public static IEnumerator SpawnWithMarker(string prefabName, Vector3 position, float yaw, float delay = 0.9f)
        {
            var markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX/SpawnMarker.prefab");
            var marker = markerPrefab ? PoolService.Spawn(markerPrefab, position, Quaternion.identity) : null;
            GameEvents.PlaySound(SoundCue.SpawnWarning, position);
            yield return Wait(delay);
            if (marker)
                PoolService.Despawn(marker);
            Enemy(prefabName, position, yaw);
        }

        public static IEnumerator LoadArena(string scene, int arenaIndex = -1)
        {
            if (arenaIndex > 0)
            {
                // как переход с предыдущей арены: GameSession продолжит забег, а не начнёт новый с первой арены
                RunState.BeginAt(arenaIndex - 1);
                RunState.AdvanceArena(0f, 0);
            }
            else
            {
                RunState.Clear();
            }
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/" + scene + ".unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            yield return null;
            Prepare();
            ClearWeather();
            yield return null;
        }

        public static void ClearWeather()
        {
            foreach (var w in Object.FindObjectsByType<WeatherController>(FindObjectsSortMode.None))
                w.Begin(WeatherKind.Clear);
        }

        // Бег по точкам вне физики (как HomeCall): аниматор видит скорость по смещению.
        public static IEnumerator RunPath(TrailerPuppet kid, IReadOnlyList<Vector3> points, float speed)
        {
            kid.Bot = false;
            kid.Player.BeginScripted();
            var t = kid.transform;
            foreach (var p in points)
            {
                Vector3 target = new(p.x, t.position.y, p.z);
                while ((target - t.position).sqrMagnitude > 0.0004f)
                {
                    Vector3 to = target - t.position;
                    float step = speed * Time.deltaTime;
                    t.position = to.magnitude <= step ? target : t.position + to.normalized * step;
                    if (to.sqrMagnitude > 1e-4f)
                        t.rotation = Quaternion.RotateTowards(t.rotation, Quaternion.LookRotation(to), 720f * Time.deltaTime);
                    yield return null;
                }
            }
        }

        public static void KillEnemies()
        {
            foreach (var spawner in Object.FindObjectsByType<WaveSpawner>(FindObjectsSortMode.None))
                spawner.DespawnAll();
        }

        public static void TimeOfDay(float progress, params string[] profiles)
        {
            var tod = Object.FindFirstObjectByType<TimeOfDayController>();
            if (tod == null)
                return;
            TimeOfDayProfile[] keys = null;
            if (profiles != null && profiles.Length > 0)
            {
                keys = new TimeOfDayProfile[profiles.Length];
                for (int i = 0; i < profiles.Length; i++)
                    keys[i] = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(TimeOfDayFolder + "TimeOfDay_" + profiles[i] + ".asset");
            }
            tod.Configure(keys, 0f);
            tod.Progress = progress;
        }

        // Камера ролика: свой CinemachineCamera с высшим приоритетом, тряска от GameFeel остаётся.
        public static CinemachineCamera Cam
        {
            get
            {
                if (s_cam != null)
                    return s_cam;
                var brain = Object.FindFirstObjectByType<CinemachineBrain>();
                if (brain)
                    brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                var go = new GameObject("TrailerCam");
                s_cam = go.AddComponent<CinemachineCamera>();
                s_cam.Priority = 1000;
                var listener = go.AddComponent<CinemachineImpulseListener>();
                listener.Gain = 0.6f;
                return s_cam;
            }
        }

        public static void Shot(Vector3 position, Vector3 lookAt, float fov)
        {
            var cam = Cam;
            cam.transform.position = position;
            Vector3 d = lookAt - position;
            if (d.sqrMagnitude > 1e-6f)
                cam.transform.rotation = Quaternion.LookRotation(d);
            var lens = cam.Lens;
            lens.FieldOfView = fov;
            cam.Lens = lens;
        }

        public static void Shot(Vector3 position, Quaternion rotation, float fov)
        {
            var cam = Cam;
            cam.transform.SetPositionAndRotation(position, rotation);
            var lens = cam.Lens;
            lens.FieldOfView = fov;
            cam.Lens = lens;
        }

        // Время ролика: кадры / fps. Time.unscaledTime в редакторе идёт по реальным часам, а не по captureFramerate.
        public static float Now => Time.frameCount / (float)TrailerRecorder.Fps;

        public static IEnumerator Wait(float seconds)
        {
            float start = Now;
            while (Now - start < seconds)
                yield return null;
        }

        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // Кадр длительностью seconds: каждый кадр вызывает f(t01) — камера и всё, что двигается.
        public static IEnumerator Over(float seconds, System.Action<float> f)
        {
            float start = Now;
            while (true)
            {
                float t = seconds <= 0f ? 1f : (Now - start) / seconds;
                f(Mathf.Clamp01(t));
                if (t >= 1f)
                    yield break;
                yield return null;
            }
        }
    }
}

using System.Collections;
using Bouncer.Core;
using UnityEngine;
using static Bouncer.Trailer.TrailerStage;

namespace Bouncer.Trailer
{
    // Запуск съёмки из Play Mode: TrailerDirector.Run("open,throw"). Каждый кадр сам ставит сцену, пишет клип и
    // останавливается. Код ролика правится только вне Play Mode: перекомпиляция посреди съёмки её обрывает.
    public sealed partial class TrailerDirector : MonoBehaviour
    {
        static TrailerDirector s_instance;
        static TrailerRunner s_runner;
        public static bool Busy { get; private set; }
        public static string Last { get; private set; }
        public static string Errors { get; private set; } = "";

        public static void Run(string shots)
        {
            Application.runInBackground = true;
            if (s_instance == null)
            {
                var go = new GameObject("TrailerDirector");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<TrailerDirector>();
                s_runner = go.AddComponent<TrailerRunner>();
            }
            // Ошибка в игровом коде ставит редактор на паузу (Error Pause) и съёмка встаёт — снимаем паузу сами.
            UnityEditor.EditorApplication.update -= Unpause;
            UnityEditor.EditorApplication.update += Unpause;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            s_instance.StopAllCoroutines();
            s_runner.StopAllCoroutines();
            if (TrailerRecorder.IsRecording)
                TrailerRecorder.End();
            s_instance.StartCoroutine(s_instance.Play(shots));
        }

        // Корутина текущего кадра: камера, постановка. Останавливается перед следующим кадром.
        static Coroutine Go(IEnumerator routine) => s_runner.StartCoroutine(routine);

        static void Unpause()
        {
            if (Busy && UnityEditor.EditorApplication.isPlaying && UnityEditor.EditorApplication.isPaused)
                UnityEditor.EditorApplication.isPaused = false;
            // выбор карточки посреди кадра (заработали в бою) замораживает время — в ролике его не бывает
            var session = GameSession.Instance;
            if (Busy && session != null && session.State == Bouncer.Core.SessionState.Upgrade)
                session.EndUpgradeChoice();
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception) && Errors.Length < 3000)
                Errors += message.Split('\n')[0] + " | " + (stack ?? "").Split('\n')[0] + "\n";
        }

        IEnumerator Play(string shots)
        {
            Busy = true;
            var log = new System.Text.StringBuilder();
            foreach (var raw in shots.Split(','))
            {
                string shot = raw.Trim();
                Last = log + shot + ": started";
                s_runner.StopAllCoroutines();
                Time.timeScale = 1f;
                int seed = 17;
                foreach (char c in shot)
                    seed = seed * 31 + c;
                Random.InitState(seed);   // повторная съёмка кадра повторяет поведение ботов
                var feel = FindFirstObjectByType<GameFeel>();
                if (feel)
                    feel.enabled = true;
                var routine = Scene(shot);
                if (routine == null)
                {
                    log.Append(shot + ": unknown; ");
                    continue;
                }
                yield return routine;
                if (TrailerRecorder.IsRecording)
                    TrailerRecorder.End();
                log.Append(shot + ": " + TrailerRecorder.Frames + " frames; ");
            }
            s_runner.StopAllCoroutines();
            Time.timeScale = 1f;
            Time.captureFramerate = 0;
            Last = log + "ALL DONE";
            Busy = false;
        }

        // Запись: name — имя клипа, seconds — длина; постановка идёт параллельно через Go().
        static IEnumerator Record(string name, float seconds, float warmup = 0.5f)
        {
            Time.captureFramerate = TrailerRecorder.Fps;
            yield return Wait(warmup);
            TrailerRecorder.Begin(name);
            int frames = Mathf.RoundToInt(seconds * TrailerRecorder.Fps);
            while (TrailerRecorder.IsRecording && TrailerRecorder.Frames < frames)
                yield return null;
            TrailerRecorder.End();
        }

        IEnumerator Scene(string shot) => shot switch
        {
            "open" => Open(),
            "fivemore" => FiveMore(),
            "throw" => ThrowShot(),
            "catch" => CatchShot(),
            "survive" => SurviveShot(),
            "coop" => CoopShot(),
            "rink" => ArenaShot("Rink", "u07_rink", 0.2f, new[] { "Frog", "Frog", "Top", "Lunokhod", "Frog" }, Melkaya, new Vector3(-3f, 5.5f, -7f)),
            "bazaar" => ArenaShot("Bazaar", "u07_bazaar", 0.15f, new[] { "Drummer", "RCCar", "DendyGun", "CryDoll", "RCCar" }, Tolstyak, new Vector3(3f, 5.5f, -7f)),
            "kg" => ArenaShot("Kindergarten", "u07_kg", 0f, new[] { "Ballerina", "Chick", "Bear", "Chick", "CryDoll" }, Otlichnik, new Vector3(-3f, 5f, -6.5f), lantern: true),
            "site" => ArenaShot("Site", "u07_site", 0f, new[] { "Shadow", "Elite_RockingHorse", "Scarecrow", "Shadow", "Mannequin" }, Huligan, new Vector3(3f, 5f, -6.5f), lantern: true),
            "split" => BossSplit(),
            "transformer" => BossTransformer(),
            "babai" => BossBabai(),
            "slowcatch" => SlowCatch(),
            "finale" => Finale(),
            _ => null,
        };
    }
}

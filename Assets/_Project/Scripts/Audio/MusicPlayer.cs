using System;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Audio
{
    /// <summary>Музыкальный трек и то, как он повторяется.</summary>
    [Serializable]
    public sealed class MusicTrack
    {
        public AudioClip clip;
        [Tooltip("Громкость относительно звуков — при ползунке «Музыка» на максимуме")]
        [Range(0f, 1f)] public float volume = 0.5f;
        [Tooltip("Через сколько секунд от начала файла трек запускается снова, а хвост прошлого круга доигрывает поверх. " +
                 "Петля из N тактов 4/4: N × 240 / BPM. 0 — длина файла")]
        [Min(0f)] public float loopLength;

        public double LoopLength => loopLength > 0f ? loopLength : (double)clip.samples / clip.frequency;
    }

    /// <summary>
    /// Музыка: на заставке — тема меню, в забеге — игровая. Смена трека — через затухание; на паузе музыка
    /// тише и глуше, после «Выбит!» и победы затихает. Переживает перезагрузку сцены, поэтому «Ещё раз»
    /// и «В меню» не обрывают звук, а новый забег начинает игровой трек сначала.
    /// Повтор бесшовный: следующий круг запускается по часам звука (PlayScheduled) ровно через длину петли
    /// на втором источнике, а первый в это время доигрывает хвост.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] MusicTrack menuTheme = new();
        [SerializeField] MusicTrack gameTheme = new();

        [Tooltip("За сколько секунд трек затихает перед сменой")]
        [SerializeField, Min(0.05f)] float switchFade = 0.7f;
        [Tooltip("За сколько секунд музыка приглушается на паузе и возвращается")]
        [SerializeField, Min(0.05f)] float moodFade = 0.35f;
        [Tooltip("Громкость на паузе, доля от обычной")]
        [SerializeField, Range(0f, 1f)] float pausedVolume = 0.45f;
        [Tooltip("Громкость после «Выбит!» и победы, доля от обычной")]
        [SerializeField, Range(0f, 1f)] float finishedVolume = 0.25f;
        [Tooltip("На паузе и после «Выбит!» музыка звучит как из-за стены: частота среза, Гц")]
        [SerializeField, Range(200f, 5000f)] float muffledCutoff = 1100f;

        const float OpenCutoff = 22000f;
        /// <summary>Запас до старта по часам звука: запланированный звук не должен опоздать.</summary>
        const double StartDelay = 0.1;

        static MusicPlayer s_instance;

        readonly AudioSource[] _voices = new AudioSource[2];
        readonly AudioLowPassFilter[] _filters = new AudioLowPassFilter[2];
        readonly double[] _startAt = new double[2];
        readonly bool[] _armed = new bool[2];
        int _current;
        bool _waitingForData;

        MusicTrack _playing;
        GameSession _session;
        bool _restart;
        float _fade;
        float _duck = 1f;
        float _muffle;

        void Awake()
        {
            // Копия из перезагруженной сцены: музыка уже играет.
            if (s_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            s_instance = this;
            DontDestroyOnLoad(gameObject);

            for (int i = 0; i < _voices.Length; i++)
            {
                var voice = new GameObject("Voice " + i);
                voice.transform.SetParent(transform, false);
                var source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.priority = 0;
                source.bypassReverbZones = true;
                _voices[i] = source;
                _filters[i] = voice.AddComponent<AudioLowPassFilter>();
                _filters[i].enabled = false;
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        void Update()
        {
            if (s_instance != this)
                return;

            var session = GameSession.Instance;
            if (session != _session)
            {
                // Новая сцена сразу с забегом («Ещё раз», «Заново») — игровой трек начинается сначала.
                // Следующая арена той же прогулки музыку не обрывает.
                _session = session;
                _restart = session != null && session.State != SessionState.Title && RunState.ArenaIndex == 0;
            }

            var wanted = session == null || session.State == SessionState.Title ? menuTheme : gameTheme;
            float dt = Time.unscaledDeltaTime;
            if (wanted != _playing || _restart)
            {
                _fade = Mathf.MoveTowards(_fade, 0f, dt / switchFade);
                if (_fade <= 0f)
                {
                    Play(wanted);
                    _restart = false;
                    _fade = 1f;
                }
            }
            else
            {
                _fade = Mathf.MoveTowards(_fade, 1f, dt / switchFade);
                UpdateMood(session, dt);
            }

            float volume = _playing != null ? _playing.volume * _fade * _duck * GameSettings.MusicGain : 0f;
            float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(muffledCutoff), _muffle));
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i].volume = volume;
                _filters[i].enabled = _muffle > 0.001f;
                _filters[i].cutoffFrequency = cutoff;
            }
            KeepLooping();
        }

        void UpdateMood(GameSession session, float dt)
        {
            if (session == null)
                return;
            // В настройках музыку слышно как в игре — иначе ползунок громкости не подобрать.
            bool paused = session.State == SessionState.Playing && GameFeel.Paused && !session.OverlayOpen;
            float duck = session.IsFinished ? finishedVolume : paused ? pausedVolume : 1f;
            bool muffled = paused || session.State == SessionState.GameOver;
            _duck = Mathf.MoveTowards(_duck, duck, dt / moodFade);
            _muffle = Mathf.MoveTowards(_muffle, muffled ? 1f : 0f, dt / moodFade);
        }

        void Play(MusicTrack track)
        {
            _playing = track;
            foreach (var voice in _voices)
                voice.Stop();
            _armed[0] = _armed[1] = false;
            _waitingForData = track.clip != null;
            if (_waitingForData && track.clip.loadState != AudioDataLoadState.Loaded)
                track.clip.LoadAudioData();
        }

        /// <summary>Держит следующий круг петли запланированным на свободном источнике.</summary>
        void KeepLooping()
        {
            if (_playing == null || _playing.clip == null)
                return;
            double now = AudioSettings.dspTime;
            if (_waitingForData)
            {
                if (_playing.clip.loadState != AudioDataLoadState.Loaded)
                    return;
                _waitingForData = false;
                _current = 1;
                Schedule(0, now + StartDelay);
                return;
            }

            int next = 1 - _current;
            if (_armed[next] && now >= _startAt[next])
            {
                // Начался новый круг. Прошлый источник доигрывает хвост и потом возьмёт следующий круг.
                _armed[next] = false;
                _current = next;
                next = 1 - next;
            }
            // Если отстали (игра стояла), новый круг начнётся сразу, а не в прошлом.
            if (!_armed[next] && !_voices[next].isPlaying)
                Schedule(next, Math.Max(_startAt[_current] + _playing.LoopLength, now + StartDelay));
        }

        void Schedule(int index, double time)
        {
            var voice = _voices[index];
            voice.clip = _playing.clip;
            voice.timeSamples = 0;
            voice.PlayScheduled(time);
            _startAt[index] = time;
            _armed[index] = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;
    }
}

using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Bouncer.Trailer
{
    // Записывает Camera.main покадрово (Time.captureFramerate) в mp4 через ffmpeg.
    // Звук не пишется: AudioRenderer в редакторе отдаёт 0 сэмплов, поэтому рядом с клипом кладётся
    // <name>.sounds.tsv — звуковые события игры с временем кадра, и звук собирается при монтаже.
    // Только для съёмки рекламного ролика, в билд не попадает (asmdef с UNITY_EDITOR).
    public sealed class TrailerRecorder : MonoBehaviour
    {
        public static string Ffmpeg = "/private/tmp/claude-501/-Users-bekbolataldiyarov-Desktop-projects-Game-Projects-Bouncer/b8b81bcd-f3d6-4e07-9f55-2d70d94bc953/scratchpad/venv/lib/python3.9/site-packages/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1";
        public static string OutDir = "/Users/bekbolataldiyarov/Desktop/projects/It's our field main/ItsOurField_Gameplay/clips";
        public static int Fps = 60;
        public static int Width = 1920;
        public static int Height = 1080;
        public static float Supersample = 1.5f;
        // С интерфейсом: пишем весь Game view (3D + HUD), как его видит игрок. Game view должен быть 1920×1080.
        public static bool Hud;

        public static bool IsRecording => s_instance != null && s_instance._recording;
        public static int Frames => s_instance != null ? s_instance._frames : 0;

        static TrailerRecorder s_instance;

        // ScreenCapture на Metal отдаёт кадр уже перевёрнутым или нет — проверено по первому клипу
        public static bool FlipHud;

        bool _recording;
        bool _hud;
        int _frames;
        Process _ff;
        Stream _video;
        byte[] _pixels;
        RenderTexture _rt;
        Camera _camera;
        StreamWriter _sounds;

        public static void Begin(string name)
        {
            if (IsRecording)
                End();
            if (s_instance == null)
            {
                var go = new GameObject("TrailerRecorder");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<TrailerRecorder>();
            }
            s_instance.StartRecording(name);
        }

        public static void End()
        {
            if (s_instance != null)
                s_instance.StopRecording();
        }

        void StartRecording(string name)
        {
            Directory.CreateDirectory(OutDir);
            _hud = Hud;
            if (_hud)
                name += "_hud";
            int w = _hud ? Screen.width : Mathf.RoundToInt(Width * Supersample / 2f) * 2;
            int h = _hud ? Screen.height : Mathf.RoundToInt(Height * Supersample / 2f) * 2;
            _camera = Camera.main;
            _rt = new RenderTexture(w, h, _hud ? 0 : 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            _rt.Create();
            if (!_hud)
                _camera.targetTexture = _rt;
            _pixels = new byte[w * h * 4];

            string mp4 = Path.Combine(OutDir, name + ".mp4");
            var info = new ProcessStartInfo(Ffmpeg,
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {w}x{h} -r {Fps} -i - " +
                $"-vf {(_hud && !FlipHud ? "" : "vflip,")}scale={Width}:{Height}:flags=lanczos -c:v libx264 -preset medium -crf 12 -pix_fmt yuv420p \"{mp4}\"")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            _ff = Process.Start(info);
            _video = _ff.StandardInput.BaseStream;

            _sounds = new StreamWriter(Path.Combine(OutDir, name + ".sounds.tsv"));
            _sounds.WriteLine("time\tcue\tx\ty\tz\tcamx\tcamy\tcamz\trightx\trighty\trightz");
            GameEvents.SoundRequested += OnSound;

            Time.captureFramerate = Fps;
            _frames = 0;
            _recording = true;
            StartCoroutine(Capture());
            Debug.Log($"[Trailer] recording {name} {w}x{h}");
        }

        void OnSound(SoundCue cue, Vector3 position)
        {
            if (!_recording)
                return;
            var c = _camera.transform;
            var ci = CultureInfo.InvariantCulture;
            string F(float v) => v.ToString("F3", ci);
            _sounds.WriteLine(string.Join("\t", F(_frames / (float)Fps), cue.ToString(),
                F(position.x), F(position.y), F(position.z),
                F(c.position.x), F(c.position.y), F(c.position.z),
                F(c.right.x), F(c.right.y), F(c.right.z)));
        }

        IEnumerator Capture()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (_recording)
            {
                yield return endOfFrame;
                if (!_recording)
                    yield break;
                if (_hud)
                    ScreenCapture.CaptureScreenshotIntoRenderTexture(_rt);
                var request = AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32);
                request.WaitForCompletion();
                if (!request.hasError)
                {
                    request.GetData<byte>().CopyTo(_pixels);
                    _video.Write(_pixels, 0, _pixels.Length);
                }
                _frames++;
            }
        }

        void StopRecording()
        {
            if (!_recording)
                return;
            _recording = false;
            GameEvents.SoundRequested -= OnSound;
            if (_camera && !_hud)
                _camera.targetTexture = null;
            if (_rt)
            {
                _rt.Release();
                Destroy(_rt);
            }
            _video.Flush();
            _video.Close();
            _ff.WaitForExit(120000);
            _ff.Dispose();
            _sounds.Close();
            Debug.Log($"[Trailer] recorded {_frames} frames");
        }

        void OnDestroy() => StopRecording();
    }
}

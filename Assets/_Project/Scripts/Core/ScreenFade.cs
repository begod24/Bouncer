using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    public sealed class ScreenFade : MonoBehaviour
    {
        const float FadeOutTime = 0.45f;
        const float FadeInTime = 0.6f;
        const float CoverTimeout = 20f;

        static ScreenFade s_instance;

        float _alpha;

        public static bool IsBusy { get; private set; }

        public static void LoadScene(string sceneName)
        {
            if (IsBusy || string.IsNullOrEmpty(sceneName))
                return;
            Ensure().StartCoroutine(s_instance.Run(sceneName));
        }

        public static void Cover()
        {
            if (IsBusy)
                return;
            Ensure().StartCoroutine(s_instance.RunCover());
        }

        public static void CoverNow()
        {
            var fade = Ensure();
            fade._alpha = 1f;
            if (!IsBusy)
                fade.StartCoroutine(fade.RunCover());
        }

        static ScreenFade Ensure()
        {
            if (s_instance == null)
            {
                var go = new GameObject("[ScreenFade]");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<ScreenFade>();
            }
            return s_instance;
        }

        IEnumerator RunCover()
        {
            IsBusy = true;
            bool loaded = false;
            void OnLoaded(Scene scene, LoadSceneMode mode) => loaded = true;
            SceneManager.sceneLoaded += OnLoaded;
            float until = Time.unscaledTime + CoverTimeout;
            while (_alpha < 1f || (!loaded && Time.unscaledTime < until))
            {
                _alpha = Mathf.MoveTowards(_alpha, 1f, Time.unscaledDeltaTime / FadeOutTime);
                yield return null;
            }
            SceneManager.sceneLoaded -= OnLoaded;
            yield return null;
            while (_alpha > 0f)
            {
                _alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / FadeInTime);
                yield return null;
            }
            IsBusy = false;
        }

        IEnumerator Run(string sceneName)
        {
            IsBusy = true;
            while (_alpha < 1f)
            {
                _alpha = Mathf.MoveTowards(_alpha, 1f, Time.unscaledDeltaTime / FadeOutTime);
                yield return null;
            }
            var load = SceneManager.LoadSceneAsync(sceneName);
            while (load != null && !load.isDone)
                yield return null;
            yield return null;
            while (_alpha > 0f)
            {
                _alpha = Mathf.MoveTowards(_alpha, 0f, Time.unscaledDeltaTime / FadeInTime);
                yield return null;
            }
            IsBusy = false;
        }

        void OnGUI()
        {
            if (_alpha <= 0f)
                return;
            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, _alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            IsBusy = false;
        }
    }
}

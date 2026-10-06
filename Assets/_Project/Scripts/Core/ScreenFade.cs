using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    /// <summary>
    /// Затемнение между аренами: экран гаснет, грузится следующая сцена, экран светлеет.
    /// Рисуется через IMGUI поверх всего интерфейса и переживает смену сцены. Время — реальное:
    /// игра на время перехода стоит. По сети сцену грузит не затемнение, а сеть: <see cref="Cover"/> только
    /// гасит экран и ждёт, пока она загрузится.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        const float FadeOutTime = 0.45f;
        const float FadeInTime = 0.6f;
        /// <summary>Сцена по сети так и не загрузилась — экран всё равно светлеет.</summary>
        const float CoverTimeout = 20f;

        static ScreenFade s_instance;

        float _alpha;

        /// <summary>Идёт переход: экран гаснет или светлеет.</summary>
        public static bool IsBusy { get; private set; }

        /// <summary>Погасить экран, загрузить сцену и плавно показать её.</summary>
        public static void LoadScene(string sceneName)
        {
            if (IsBusy || string.IsNullOrEmpty(sceneName))
                return;
            Ensure().StartCoroutine(s_instance.Run(sceneName));
        }

        /// <summary>Погасить экран и показать снова, когда загрузится следующая сцена (её грузит сеть).</summary>
        public static void Cover()
        {
            if (IsBusy)
                return;
            Ensure().StartCoroutine(s_instance.RunCover());
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
            // Кадр на то, чтобы новая сцена проснулась и расставилась.
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
            // Кадр на то, чтобы новая сцена проснулась и расставилась.
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

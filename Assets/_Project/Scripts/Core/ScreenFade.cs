using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Core
{
    /// <summary>
    /// Затемнение между аренами: экран гаснет, грузится следующая сцена, экран светлеет.
    /// Рисуется через IMGUI поверх всего интерфейса и переживает смену сцены. Время — реальное:
    /// игра на время перехода стоит.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        const float FadeOutTime = 0.45f;
        const float FadeInTime = 0.6f;

        static ScreenFade s_instance;

        float _alpha;

        /// <summary>Идёт переход: экран гаснет или светлеет.</summary>
        public static bool IsBusy { get; private set; }

        /// <summary>Погасить экран, загрузить сцену и плавно показать её.</summary>
        public static void LoadScene(string sceneName)
        {
            if (IsBusy || string.IsNullOrEmpty(sceneName))
                return;
            if (s_instance == null)
            {
                var go = new GameObject("[ScreenFade]");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<ScreenFade>();
            }
            s_instance.StartCoroutine(s_instance.Run(sceneName));
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

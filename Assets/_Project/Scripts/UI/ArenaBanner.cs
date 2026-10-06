using Bouncer.Core;
using Bouncer.Run;
using TMPro;
using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>
    /// Плашка посреди экрана: название арены, когда начинается бой («Двор · утро»), с погодой, если она выпала
    /// («Дождь: в лужах мяч гаснет»), и «Пройдено!» с подсказкой про ларёк и стрелку, когда арена пройдена.
    /// В обучении вместо названия арены — «Тренировка».
    /// </summary>
    public sealed class ArenaBanner : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text hint;
        [SerializeField] float introTime = 2.4f;
        [SerializeField] float clearedTime = 4f;
        [SerializeField] float fadeSpeed = 3f;

        float _hideAt;
        bool _introShown;
        bool _clearedShown;

        void Awake() => group.alpha = 0f;

        void Update()
        {
            var session = GameSession.Instance;
            var director = ArenaDirector.Instance;
            if (session != null && director != null && director.Arena != null)
            {
                if (!_introShown && session.State == SessionState.Playing && Tutorial.Active)
                {
                    _introShown = true;
                    Show(Loc.Get("tutorial.banner"), Loc.Get("tutorial.banner.hint"), introTime + 1.2f);
                }
                // По сети — когда закрыта стартовая карточка и гость узнал от хозяина погоду.
                if (!_introShown && session.State == SessionState.Playing && session.Menu == LocalMenu.None
                    && director.WeatherKnown && !director.Arena.title.IsEmpty)
                {
                    _introShown = true;
                    string weather = WeatherKey(director.Weather);
                    string hintText = weather != null ? Loc.Get(weather)
                        : !director.Arena.introHint.IsEmpty ? director.Arena.introHint.GetLocalizedString()
                        : string.Empty;
                    Show(director.Arena.title.GetLocalizedString(), hintText,
                        hintText.Length > 0 ? introTime + 1.2f : introTime);
                }
                // Сначала выбор карточки за босса, потом плашка — иначе её не видно под экраном выбора.
                if (!_clearedShown && director.IsComplete && session.State == SessionState.Cleared && session.Menu == LocalMenu.None)
                {
                    _clearedShown = true;
                    // По сети дальше идут все вместе: подсказка про общую стрелку.
                    Show(Loc.Get("arena.cleared"), Loc.Get(Online.Active ? "arena.cleared.hint.coop" : "arena.cleared.hint"), clearedTime);
                }
                if (session.IsShopOpen || session.State is SessionState.GameOver or SessionState.Victory)
                    _hideAt = 0f;
            }
            float target = Time.unscaledTime < _hideAt ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
        }

        static string WeatherKey(WeatherKind weather) => weather switch
        {
            WeatherKind.Rain => "weather.rain",
            WeatherKind.Storm => "weather.storm",
            WeatherKind.Fog => "weather.fog",
            _ => null,
        };

        void Show(string titleText, string hintText, float seconds)
        {
            title.text = titleText;
            if (hint)
            {
                hint.text = hintText;
                hint.gameObject.SetActive(!string.IsNullOrEmpty(hintText));
            }
            _hideAt = Time.unscaledTime + seconds;
        }
    }
}

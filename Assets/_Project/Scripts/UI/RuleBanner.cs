using Bouncer.Core;
using TMPro;
using UnityEngine;

namespace Bouncer.UI
{
    /// <summary>
    /// Надпись мелом под полосой босса: правило раунда, которое объявил свистком Физрук («Замри!» и подсказка),
    /// и полоска, сколько правилу осталось. Слушает <see cref="GameEvents.Announced"/>; время — игровое,
    /// на паузе надпись тоже стоит.
    /// </summary>
    public sealed class RuleBanner : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text hint;
        [Tooltip("Полоска оставшегося времени: растягивается по ширине от левого края")]
        [SerializeField] RectTransform timeFill;
        [Tooltip("Что подпрыгивает, когда появляется новое правило")]
        [SerializeField] RectTransform punch;
        [SerializeField] float fadeSpeed = 5f;

        float _start;
        float _until;
        Vector3 _punchScale = Vector3.one;

        void Awake()
        {
            group.alpha = 0f;
            if (punch)
                _punchScale = punch.localScale;
        }

        void OnEnable() => GameEvents.Announced += OnAnnounced;

        void OnDisable() => GameEvents.Announced -= OnAnnounced;

        void OnAnnounced(Announcement announcement)
        {
            title.text = Loc.Get(announcement.Title);
            if (hint)
            {
                string text = string.IsNullOrEmpty(announcement.Hint) ? string.Empty : Loc.Get(announcement.Hint);
                hint.text = text;
                hint.gameObject.SetActive(!string.IsNullOrEmpty(text));
            }
            _start = Time.time;
            _until = Time.time + Mathf.Max(0.5f, announcement.Seconds);
        }

        void Update()
        {
            var session = GameSession.Instance;
            bool hidden = session != null && session.State is not (SessionState.Playing or SessionState.Upgrade);
            float left = _until - Time.time;
            float target = left > 0f && !hidden ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
            if (timeFill)
            {
                float total = Mathf.Max(0.01f, _until - _start);
                timeFill.anchorMax = new Vector2(Mathf.Clamp01(left / total), timeFill.anchorMax.y);
            }
            if (punch)
            {
                float age = Time.time - _start;
                float bump = age < 0.3f ? Mathf.Sin(age / 0.3f * Mathf.PI) * 0.18f : 0f;
                punch.localScale = _punchScale * (1f + bump);
            }
        }
    }
}

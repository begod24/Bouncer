using Bouncer.Core;
using Bouncer.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Bouncer.UI
{
    /// <summary>
    /// Обучение на экране: карточка задания слева (номер шага, что сделать, как это сделать, прогресс или «Готово!»)
    /// и в конце экран «Готов гулять!» с кнопками «Гулять!» (заставка сразу открывает выбор ребёнка) и «В меню».
    /// Читает <see cref="TutorialDirector"/>, сама сообщает ему, что карманы открыли и закрыли. Карточка прячется
    /// под паузой, выбором карточки и витриной. Вне обучения панели не видно.
    /// </summary>
    public sealed class TutorialPanel : MonoBehaviour
    {
        [Header("Задание")]
        [SerializeField] CanvasGroup taskGroup;
        [Tooltip("Карточка задания: растёт по тексту, подпрыгивает, когда шаг засчитан")]
        [SerializeField] RectTransform card;
        [SerializeField] TMP_Text stepLabel;
        [SerializeField] TMP_Text titleLabel;
        [SerializeField] TMP_Text hintLabel;
        [SerializeField] TMP_Text progressLabel;
        [SerializeField] Color progressColor = new(0.96f, 0.95f, 0.93f, 0.85f);
        [SerializeField] Color doneColor = new(0.49f, 0.73f, 0.31f);

        [Header("Конец")]
        [SerializeField] CanvasGroup doneScreen;
        [SerializeField] UnityEngine.UI.Button playButton;
        [SerializeField] UnityEngine.UI.Button menuButton;
        [SerializeField] float fadeSpeed = 6f;

        TutorialDirector _director;
        bool _dirty;
        bool _pocketsWereOpen;
        bool _doneShown;
        bool _wasComplete;
        float _bump;

        void Awake()
        {
            Hide(taskGroup);
            Hide(doneScreen);
            playButton.onClick.AddListener(Play);
            menuButton.onClick.AddListener(ToMenu);
        }

        void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            if (_director != null)
                _director.Changed -= MarkDirty;
            _director = null;
        }

        void OnLocaleChanged(Locale locale) => _dirty = true;

        void MarkDirty() => _dirty = true;

        void Update()
        {
            if (_director == null && Tutorial.Active && TutorialDirector.Instance != null)
            {
                _director = TutorialDirector.Instance;
                _director.Changed += MarkDirty;
                _dirty = true;
            }
            if (_director == null)
            {
                Hide(taskGroup);
                Hide(doneScreen);
                return;
            }

            TrackPockets();
            var session = GameSession.Instance;
            bool finished = _director.IsFinished;
            bool showTask = !finished && session != null && session.State is (SessionState.Playing or SessionState.Cleared)
                            && !GameFeel.Paused;
            Fade(taskGroup, showTask);
            if (_dirty)
                Refresh();
            if (card)
            {
                _bump = Mathf.MoveTowards(_bump, 0f, Time.unscaledDeltaTime * 3f);
                card.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(_bump * Mathf.PI));
            }
            UpdateDone(finished, session);
        }

        void Refresh()
        {
            _dirty = false;
            string key = "tutorial." + _director.Step.ToString().ToLowerInvariant();
            if (_director.IsFinished)
                return;
            stepLabel.text = Loc.Format("tutorial.step", _director.StepNumber, TutorialDirector.StepCount);
            titleLabel.text = Loc.Get(key + ".title");
            hintLabel.text = Loc.Get(key + ".hint");

            string progress = string.Empty;
            if (_director.StepComplete)
            {
                progress = Loc.Get("tutorial.step_done");
                progressLabel.color = doneColor;
                if (!_wasComplete)
                    _bump = 1f;
            }
            else if (_director.Target > 1)
            {
                progress = Loc.Format("tutorial.progress", _director.Progress, _director.Target);
                progressLabel.color = progressColor;
            }
            _wasComplete = _director.StepComplete;
            progressLabel.text = progress;
            progressLabel.gameObject.SetActive(progress.Length > 0);
            if (card)
                UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(card);
        }

        /// <summary>Шаг «Карманы»: карманы открыли (Tab, Select, пауза) и закрыли — засчитать.</summary>
        void TrackPockets()
        {
            var pockets = PocketsPanel.Instance;
            bool open = pockets != null && pockets.IsOpen;
            if (_pocketsWereOpen && !open && _director.Step == TutorialStep.Pockets)
                _director.ReportPocketsViewed();
            _pocketsWereOpen = open;
        }

        void UpdateDone(bool finished, GameSession session)
        {
            Fade(doneScreen, finished);
            bool active = finished && doneScreen.alpha > 0.5f;
            doneScreen.interactable = active;
            doneScreen.blocksRaycasts = active;
            if (finished && !_doneShown)
            {
                _doneShown = true;
                // Esc больше не открывает паузу поверх: отсюда только «Гулять!» или «В меню».
                if (session != null)
                    session.OverlayOpen = true;
            }
            var events = EventSystem.current;
            if (active && events != null && (events.currentSelectedGameObject == null
                                             || !events.currentSelectedGameObject.transform.IsChildOf(doneScreen.transform)))
                events.SetSelectedGameObject(playButton.gameObject);
        }

        void Play()
        {
            Tutorial.OpenKidsOnTitle = true;
            ToMenu();
        }

        static void ToMenu()
        {
            if (GameSession.Instance != null)
                GameSession.Instance.ToTitle();
        }

        void Fade(CanvasGroup group, bool visible)
        {
            if (group)
                group.alpha = Mathf.MoveTowards(group.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * fadeSpeed);
        }

        static void Hide(CanvasGroup group)
        {
            if (group == null)
                return;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }
}

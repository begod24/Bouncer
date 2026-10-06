using System;
using Bouncer.Core;
using Bouncer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// «Кто выходит гулять?» — после «Играть» на заставке. Четверо детей в своих позах (<see cref="KidStage"/>
    /// в RenderTexture), под каждым кнопка с именем: какая кнопка выбрана (мышью, стрелками, геймпадом), тот ребёнок
    /// и в позе, нажал — прогулка начинается с ним. Статы у всех одинаковые, поэтому на экране только имя и характер.
    /// Справа вверху — уровень опасности прогулки (открытые победами, <see cref="Danger"/>) и что он добавляет.
    /// Открывает и закрывает его <see cref="RunScreens"/>.
    /// </summary>
    public sealed class KidSelectScreen : MonoBehaviour
    {
        [SerializeField] KidRoster roster;
        [SerializeField] KidStage stagePrefab;
        [SerializeField] RawImage view;
        [Tooltip("Кнопки с именами в порядке KidRoster; их ставит под детей по картинке")]
        [SerializeField] Button[] kidButtons;
        [SerializeField] TMP_Text tagline;
        [Tooltip("Где стоит сцена с детьми — подальше от арены")]
        [SerializeField] Vector3 stagePosition = new(0f, -500f, 0f);
        [Tooltip("Уровень опасности прогулки")]
        [SerializeField] ChalkStepper dangerStepper;
        [Tooltip("Что добавляет выбранный уровень опасности")]
        [SerializeField] TMP_Text dangerHint;

        KidStage _stage;
        bool _closing;
        int _highlighted = -1;

        /// <summary>Игрок выбрал ребёнка (номер в KidRoster) — пора начинать прогулку.</summary>
        public event Action<int> Chosen;

        public KidRoster Roster => roster;

        /// <summary>Кнопка ребёнка, с которым гуляли в прошлый раз, — её выбрать при открытии.</summary>
        public Selectable FirstButton => kidButtons.Length > 0 ? kidButtons[Mathf.Clamp(GameSettings.Kid, 0, kidButtons.Length - 1)] : null;

        void Awake()
        {
            for (int i = 0; i < kidButtons.Length; i++)
            {
                int index = i;
                kidButtons[i].onClick.AddListener(() => Choose(index));
                if (!kidButtons[i].TryGetComponent(out KidSelectButton hover))
                    hover = kidButtons[i].gameObject.AddComponent<KidSelectButton>();
                hover.Bind(this, index);
            }
            if (dangerStepper)
                dangerStepper.Changed += index =>
                {
                    Danger.Selected = index + 1;
                    ShowDangerHint();
                };
        }

        void RefreshDanger()
        {
            if (!dangerStepper)
                return;
            int unlocked = Danger.Unlocked;
            var options = new string[unlocked];
            for (int i = 0; i < unlocked; i++)
                options[i] = Loc.Format("danger.level", i + 1);
            dangerStepper.SetOptions(options, Danger.Selected - 1);
            ShowDangerHint();
        }

        void ShowDangerHint()
        {
            if (!dangerHint)
                return;
            int level = Danger.Selected;
            string hint = Loc.Get($"danger.{level}.hint");
            if (Danger.Unlocked < Danger.Max && level == Danger.Unlocked)
                hint += "  ·  " + Loc.Format("danger.next", level + 1);
            dangerHint.text = hint;
        }

        public void Open()
        {
            _closing = false;
            if (_stage == null)
            {
                _stage = Instantiate(stagePrefab, stagePosition, Quaternion.identity);
                var rect = view.rectTransform.rect;
                float scale = view.canvas != null ? view.canvas.scaleFactor : 1f;
                _stage.Build(Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale));
                view.texture = _stage.Texture;
                PlaceButtons(rect);
            }
            _highlighted = -1;
            Highlight(roster.Clamp(GameSettings.Kid));
            RefreshDanger();
        }

        /// <summary>
        /// Убрать сцену с детьми. Экран гаснет плавно, а RawImage без текстуры мелькнул бы белым
        /// прямоугольником: пока экран видно, дети гаснут вместе с ним, а сцена уходит, когда он погас.
        /// </summary>
        public void Close()
        {
            _closing = _stage != null;
            if (!UiVisibility.IsShown(view))
                DestroyStage();
        }

        void Update()
        {
            if (_closing && !UiVisibility.IsShown(view))
                DestroyStage();
        }

        void OnDisable()
        {
            if (_closing)
                DestroyStage();
        }

        void DestroyStage()
        {
            _closing = false;
            if (_stage != null)
                Destroy(_stage.gameObject);
            _stage = null;
            view.texture = null;
        }

        /// <summary>На этом ребёнке выделение: он в позе, под ним круг, внизу его характер.</summary>
        public void Highlight(int index)
        {
            index = roster.Clamp(index);
            if (index == _highlighted)
                return;
            _highlighted = index;
            if (_stage != null)
                _stage.Select(index);
            var kid = roster[index];
            if (tagline)
                tagline.text = kid != null && !kid.tagline.IsEmpty ? kid.tagline.GetLocalizedString() : "";
        }

        void Choose(int index)
        {
            index = roster.Clamp(index);
            GameSettings.Kid = index;
            GameSettings.Save();
            var player = Players.Local ? Players.Local.GetComponent<PlayerKid>() : null;
            if (player != null)
                player.Show(roster[index]);
            Chosen?.Invoke(index);
        }

        /// <summary>Кнопки с именами — ровно под детьми на картинке (кнопки лежат внутри RawImage).</summary>
        void PlaceButtons(Rect rect)
        {
            for (int i = 0; i < kidButtons.Length; i++)
            {
                var button = (RectTransform)kidButtons[i].transform;
                var position = button.anchoredPosition;
                position.x = (_stage.ViewportX(i) - 0.5f) * rect.width;
                button.anchoredPosition = position;
            }
        }
    }
}

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

        KidStage _stage;
        int _highlighted = -1;

        /// <summary>Игрок выбрал ребёнка (номер в KidRoster) — пора начинать прогулку.</summary>
        public event Action<int> Chosen;

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
        }

        public void Open()
        {
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
        }

        public void Close()
        {
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
            var player = FindFirstObjectByType<PlayerKid>();
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

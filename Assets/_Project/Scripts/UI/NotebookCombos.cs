using System.Collections.Generic;
using Bouncer.Core;
using Bouncer.Upgrades;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Тетрадка → «Комбо»: все комбо колоды карточками. Собранное хоть раз — целиком;
    /// несобранное — серое, но видно, из каких двух карточек оно складывается.
    /// </summary>
    public sealed class NotebookCombos : MonoBehaviour
    {
        [SerializeField] CardDeck deck;
        [Tooltip("Выключенный образец карточки (копия карточки выбора)")]
        [SerializeField] UpgradeCardView template;
        [SerializeField] RectTransform grid;
        [SerializeField, Min(1)] int columns = 6;
        [SerializeField] Vector2 spacing = new(236f, 336f);
        [SerializeField, Range(0.3f, 1f)] float cardScale = 0.6f;
        [Tooltip("Карточки появляются по очереди с такой паузой, с")]
        [SerializeField] float stagger = 0.03f;

        readonly List<UpgradeCard> _combos = new();
        readonly List<UpgradeCardView> _views = new();

        public bool HasNew
        {
            get
            {
                if (deck == null)
                    return false;
                foreach (var card in deck.deck)
                    if (card && card.IsCombo && BestiaryProgress.IsNew(PlayerCards.ComboId(card)))
                        return true;
                return false;
            }
        }

        void Awake()
        {
            if (template)
                template.gameObject.SetActive(false);
        }

        public Selectable Open()
        {
            if (deck == null || template == null)
                return null;
            if (CardDeck.Active == null)
                CardDeck.Active = deck;
            _combos.Clear();
            foreach (var card in deck.deck)
                if (card && card.IsCombo && !_combos.Contains(card))
                    _combos.Add(card);
            while (_views.Count < _combos.Count)
            {
                var view = Instantiate(template, grid, false);
                view.name = "Combo_" + _views.Count;
                _views.Add(view);
            }
            int rows = Mathf.CeilToInt(_combos.Count / (float)columns);
            for (int i = 0; i < _views.Count; i++)
            {
                var view = _views[i];
                bool used = i < _combos.Count;
                view.gameObject.SetActive(used);
                if (!used)
                    continue;
                var card = _combos[i];
                int row = i / columns;
                int inRow = Mathf.Min(columns, _combos.Count - row * columns);
                var rect = (RectTransform)view.transform;
                rect.localScale = Vector3.one * cardScale;
                rect.anchoredPosition = new Vector2(((i % columns) - (inRow - 1) * 0.5f) * spacing.x, ((rows - 1) * 0.5f - row) * spacing.y);
                view.Show(card, 0, 0, i * stagger);
                string id = PlayerCards.ComboId(card);
                if (!BestiaryProgress.IsOpen(id))
                    view.ShowLocked();
                BestiaryProgress.MarkSeen(id);
                var navigation = view.Button.navigation;
                navigation.mode = Navigation.Mode.Automatic;
                view.Button.navigation = navigation;
            }
            return _combos.Count > 0 ? _views[0].Button : null;
        }
    }
}

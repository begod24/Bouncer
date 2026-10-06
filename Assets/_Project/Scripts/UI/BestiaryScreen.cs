using System.Collections.Generic;
using System.Text;
using Bouncer.Core;
using Bouncer.Run;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bouncer.UI
{
    public sealed class BestiaryScreen : MonoBehaviour
    {
        [SerializeField] Bestiary book;
        [SerializeField] BestiaryStage stagePrefab;
        [SerializeField] RawImage view;
        [Tooltip("Где стоит сцена бестиария — подальше от арены и от сцены выбора ребёнка")]
        [SerializeField] Vector3 stagePosition = new(0f, -600f, 0f);
        [Tooltip("Вкладки по порядку разделов: враги, элитки, боссы")]
        [SerializeField] ChalkTabs tabs;

        [Header("Список")]
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform listContent;
        [Tooltip("Выключенный образец строки: кнопка с надписью Label")]
        [SerializeField] Button itemTemplate;
        [SerializeField] Color itemColor = new(0.96f, 0.95f, 0.93f, 1f);
        [SerializeField] Color lockedColor = new(0.96f, 0.95f, 0.93f, 0.4f);
        [Tooltip("Сколько боссов открыто — под списком, только во вкладке «Боссы»")]
        [SerializeField] TMP_Text countLabel;
        [Tooltip("«новое!» у вкладки «Боссы»")]
        [SerializeField] GameObject bossTabNew;

        [Header("Страница")]
        [SerializeField] RectTransform page;
        [SerializeField] TMP_Text nameLabel;
        [SerializeField] TMP_Text statsLabel;
        [SerializeField] TMP_Text whereLabel;
        [SerializeField] TMP_Text bodyLabel;
        [SerializeField] TMP_Text tipTitle;
        [SerializeField] TMP_Text tipLabel;

        readonly List<Bestiary.Entry> _entries = new();
        readonly List<Button> _items = new();
        readonly List<ArenaDefinition> _arenas = new();
        readonly Vector3[] _corners = new Vector3[4];
        BestiaryStage _stage;
        bool _closing;
        Selectable _tabsSelectable;
        int _section;
        int _shown = -1;

        public Selectable UpTarget { get; set; }

        public bool HasNew => book != null && book.AnyNew();

        public Selectable SectionTabs => _tabsSelectable;

        void Awake()
        {
            itemTemplate.gameObject.SetActive(false);
            if (tabs)
            {
                tabs.TryGetComponent(out _tabsSelectable);
                tabs.Changed += OnTabChanged;
            }
        }

        public Selectable Open()
        {
            _closing = false;
            if (_stage == null && stagePrefab != null)
            {
                _stage = Instantiate(stagePrefab, stagePosition, Quaternion.identity);
                var rect = view.rectTransform.rect;
                float scale = view.canvas != null ? view.canvas.scaleFactor : 1f;
                _stage.Build(Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale));
                view.texture = _stage.Texture;
            }
            _section = HasNew ? (int)BestiarySection.Boss : 0;
            if (tabs)
                tabs.Select(_section);
            BuildList();
            int start = Mathf.Max(0, _entries.FindIndex(entry => entry.IsNew));
            _shown = -1;
            ShowEntry(start, null);
            return _entries.Count > 0 ? _items[start] : _tabsSelectable;
        }

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

        Selectable FirstItem => _entries.Count > 0 ? _items[0] : _tabsSelectable;

        public void StepSection(int delta)
        {
            if (tabs == null)
                return;
            tabs.Step(delta);
            if (EventSystem.current != null && FirstItem != null)
                EventSystem.current.SetSelectedGameObject(FirstItem.gameObject);
        }

        void OnTabChanged(int index)
        {
            _section = index;
            BuildList();
            _shown = -1;
            ShowEntry(0, null);
        }

        void BuildList()
        {
            if (book == null)
                return;
            var section = (BestiarySection)Mathf.Clamp(_section, 0, (int)BestiarySection.Boss);
            book.Collect(section, _entries);
            while (_items.Count < _entries.Count)
            {
                var item = Instantiate(itemTemplate, listContent, false);
                if (!item.TryGetComponent(out BestiaryEntryButton handler))
                    handler = item.gameObject.AddComponent<BestiaryEntryButton>();
                handler.Bind(this, _items.Count);
                _items.Add(item);
            }
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                bool used = i < _entries.Count;
                item.gameObject.SetActive(used);
                if (!used)
                    continue;
                var entry = _entries[i];
                SetLabel(i);
                var navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? _items[i - 1] : _tabsSelectable,
                    selectOnDown = i < _entries.Count - 1 ? _items[i + 1] : null,
                };
                item.navigation = navigation;
            }
            if (_tabsSelectable)
                _tabsSelectable.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = UpTarget,
                    selectOnDown = _entries.Count > 0 ? _items[0] : null,
                };
            if (bossTabNew)
                bossTabNew.SetActive(HasNew);
            if (countLabel)
            {
                bool bosses = section == BestiarySection.Boss;
                countLabel.gameObject.SetActive(bosses);
                if (bosses)
                    countLabel.text = Loc.Format("bestiary.bosses.count", book.CountOpen(section, out int total), total);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            listContent.anchoredPosition = new Vector2(listContent.anchoredPosition.x, 0f);
            if (scroll)
                scroll.StopMovement();
        }

        void SetLabel(int index)
        {
            var entry = _entries[index];
            var label = _items[index].GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                return;
            bool open = entry.IsOpen;
            string text = open && !entry.title.IsEmpty ? entry.title.GetLocalizedString() : Loc.Get("bestiary.locked.name");
            if (entry.IsNew)
                text += "  <size=70%><color=#F7D13D>" + Loc.Get("notebook.new") + "</color></size>";
            label.text = text;
            label.color = open ? itemColor : lockedColor;
        }

        public void ShowEntry(int index, RectTransform item)
        {
            if (item != null)
                Reveal(item);
            if (index == _shown || index < 0 || index >= _entries.Count)
            {
                if (_entries.Count == 0)
                    Clear();
                return;
            }
            _shown = index;
            var entry = _entries[index];
            bool open = entry.IsOpen;
            if (entry.IsNew)
                BestiaryProgress.MarkSeen(entry.Id);

            nameLabel.text = open && !entry.title.IsEmpty ? entry.title.GetLocalizedString() : Loc.Get("bestiary.locked.name");
            statsLabel.text = !open ? Loc.Get("bestiary.locked.stats") : entry.section switch
            {
                BestiarySection.Elite => Loc.Format("bestiary.hits.elite", entry.hits),
                BestiarySection.Boss => Loc.Format("bestiary.hits.boss", entry.hits),
                _ => Loc.Format("bestiary.hits", entry.hits),
            };

            book.ArenasOf(entry, _arenas);
            if (_arenas.Count > 0)
            {
                var names = new StringBuilder();
                foreach (var arena in _arenas)
                {
                    if (arena.title.IsEmpty)
                        continue;
                    if (names.Length > 0)
                        names.Append(", ");
                    names.Append(arena.title.GetLocalizedString());
                }
                whereLabel.text = Loc.Format("bestiary.where", names.ToString());
            }
            whereLabel.gameObject.SetActive(_arenas.Count > 0);

            bodyLabel.text = !open ? Loc.Get("bestiary.locked.body")
                : !entry.description.IsEmpty ? entry.description.GetLocalizedString() : string.Empty;
            bool tip = open && !entry.tip.IsEmpty;
            tipTitle.gameObject.SetActive(tip);
            tipLabel.gameObject.SetActive(tip);
            if (tip)
                tipLabel.text = entry.tip.GetLocalizedString();
            if (page)
                LayoutRebuilder.MarkLayoutForRebuild(page);

            if (_stage != null)
                _stage.Show(entry.prefab, !open);
        }

        void Clear()
        {
            _shown = -1;
            nameLabel.text = statsLabel.text = bodyLabel.text = string.Empty;
            whereLabel.gameObject.SetActive(false);
            tipTitle.gameObject.SetActive(false);
            tipLabel.gameObject.SetActive(false);
            if (_stage != null)
                _stage.Show(null, false);
        }

        void Reveal(RectTransform item)
        {
            var viewport = scroll != null && scroll.viewport != null ? scroll.viewport : listContent.parent as RectTransform;
            if (viewport == null)
                return;
            item.GetWorldCorners(_corners);
            float bottom = viewport.InverseTransformPoint(_corners[0]).y;
            float top = viewport.InverseTransformPoint(_corners[1]).y;
            var view = viewport.rect;
            float delta = top > view.yMax ? top - view.yMax : bottom < view.yMin ? bottom - view.yMin : 0f;
            if (Mathf.Approximately(delta, 0f))
                return;
            float max = Mathf.Max(0f, listContent.rect.height - view.height);
            var position = listContent.anchoredPosition;
            position.y = Mathf.Clamp(position.y - delta, 0f, max);
            listContent.anchoredPosition = position;
            if (scroll)
                scroll.StopMovement();
        }
    }
}

using System;
using System.Text;
using Bouncer.Core;
using Bouncer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bouncer.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class NotebookScreen : MonoBehaviour
    {
        [Serializable]
        sealed class Page
        {
            public GameObject panel;
            [Tooltip("Подсказка управления внизу — строка таблицы «UI»")]
            public string hintKey;
            [Tooltip("Что выбрать на странице клавиатурой и геймпадом. Пусто — остаться на вкладках")]
            public Selectable first;
            [Tooltip("«новое!» на вкладке страницы")]
            public GameObject newMark;
        }

        const int BestiaryPage = 0;
        const int RecordsPage = 1;

        [Tooltip("Вкладки по порядку страниц: бестиарий, рекорды, как играть")]
        [SerializeField] ChalkTabs tabs;
        [SerializeField] Page[] pages;
        [SerializeField] BestiaryScreen bestiary;
        [SerializeField] TMP_Text recordsText;
        [Tooltip("Дети — чтобы подписать рекорд именем ребёнка")]
        [SerializeField] KidRoster roster;
        [SerializeField] TMP_Text hint;

        CanvasGroup _group;
        Selectable _tabsSelectable;
        int _page = -1;
        bool _open;

        public bool HasNew => (bestiary != null && bestiary.HasNew) || RunRecords.HasUnseen;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            tabs.TryGetComponent(out _tabsSelectable);
            tabs.Changed += index => ShowPage(index);
            if (bestiary != null)
                bestiary.UpTarget = _tabsSelectable;
        }

        public Selectable Open()
        {
            _open = true;
            bool newBoss = bestiary != null && bestiary.HasNew;
            int page = RunRecords.HasUnseen && !newBoss ? RecordsPage : BestiaryPage;
            tabs.Select(page);
            _page = -1;
            return ShowPage(page);
        }

        public void Close()
        {
            _open = false;
            _page = -1;
            if (bestiary != null)
                bestiary.Close();
        }

        void Update()
        {
            if (!_open || !_group.interactable)
                return;
            int step = TabStepPressed();
            if (step != 0)
                tabs.Step(step);
        }

        Selectable ShowPage(int index)
        {
            index = Mathf.Clamp(index, 0, pages.Length - 1);
            var page = pages[index];
            if (index == _page)
                return page.first != null ? page.first : _tabsSelectable;
            _page = index;
            for (int i = 0; i < pages.Length; i++)
                pages[i].panel.SetActive(i == index);
            if (hint)
                hint.text = string.IsNullOrEmpty(page.hintKey) ? string.Empty : Loc.Get(page.hintKey);

            Selectable first = page.first;
            Selectable below = first;
            if (index == BestiaryPage && bestiary != null)
            {
                first = bestiary.Open();
                below = bestiary.SectionTabs != null ? bestiary.SectionTabs : first;
            }
            else
            {
                if (bestiary != null)
                    bestiary.Close();
                if (first != null)
                {
                    var navigation = first.navigation;
                    navigation.mode = Navigation.Mode.Explicit;
                    navigation.selectOnUp = _tabsSelectable;
                    first.navigation = navigation;
                }
            }
            if (_tabsSelectable)
                _tabsSelectable.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = below };

            if (index == RecordsPage)
            {
                RefreshRecords();
                RunRecords.MarkSeen();
            }
            RefreshMarks();

            var target = first != null ? first : _tabsSelectable;
            var events = EventSystem.current;
            var selected = events != null ? events.currentSelectedGameObject : null;
            if (events != null && target != null && selected != null && !selected.activeInHierarchy)
                events.SetSelectedGameObject(target.gameObject);
            return target;
        }

        void RefreshMarks()
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i].newMark == null)
                    continue;
                bool isNew = i switch
                {
                    BestiaryPage => bestiary != null && bestiary.HasNew,
                    RecordsPage => RunRecords.HasUnseen,
                    _ => false,
                };
                pages[i].newMark.SetActive(isNew);
            }
        }

        void RefreshRecords()
        {
            if (recordsText == null)
                return;
            var lines = new StringBuilder();
            for (int level = Danger.Max; level >= 1; level--)
            {
                var best = RunRecords.Best(level);
                if (best == null)
                    continue;
                string kid = best.kid;
                if (roster != null)
                    for (int i = 0; i < roster.Count; i++)
                        if (roster[i] != null && roster[i].name == best.kid && !roster[i].displayName.IsEmpty)
                            kid = roster[i].displayName.GetLocalizedString();
                lines.AppendLine(Loc.Format("records.line", level, RunRecords.FormatTime(best.time), kid,
                    best.cards != null ? best.cards.Length : 0));
            }
            bool coopHeader = false;
            for (int level = Danger.Max; level >= 1; level--)
            {
                var best = RunRecords.Best(level, coop: true);
                if (best == null)
                    continue;
                if (!coopHeader)
                {
                    coopHeader = true;
                    if (lines.Length > 0)
                        lines.AppendLine();
                    lines.AppendLine(Loc.Get("records.coop"));
                }
                lines.AppendLine(Loc.Format("records.line.coop", level, RunRecords.FormatTime(best.time), best.players));
            }
            recordsText.text = lines.Length > 0 ? lines.ToString() : Loc.Get("records.empty");
        }

        static int TabStepPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.qKey.wasPressedThisFrame) || (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame))
                return -1;
            if ((keyboard != null && keyboard.eKey.wasPressedThisFrame) || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame))
                return 1;
            return 0;
        }
    }
}

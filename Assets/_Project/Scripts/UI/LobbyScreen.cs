using System;
using Bouncer.Core;
using Bouncer.Net;
using Bouncer.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    /// <summary>
    /// Комната: код (или IP хозяина в локальной сети) с кнопкой «Скопировать», список игроков их цветами
    /// (имя, ребёнок, хозяин / готов), выбор ребёнка (занятых не взять), опасность (выбирает хозяин из открытых
    /// у себя), «Готов» у гостей и «Гулять!» у хозяина — когда все гости готовы. «Выйти» (и Esc) — уйти из комнаты.
    /// Данные берёт из <see cref="NetRoom"/>, сам ничего не решает.
    /// </summary>
    public sealed class LobbyScreen : MonoBehaviour
    {
        [SerializeField] KidRoster roster;
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text code;
        [SerializeField] Button copyButton;
        [Tooltip("Строки игроков по номерам: 1-й, 2-й, 3-й, 4-й")]
        [SerializeField] TMP_Text[] rows = Array.Empty<TMP_Text>();
        [Tooltip("Кнопки детей в порядке KidRoster")]
        [SerializeField] Button[] kidButtons = Array.Empty<Button>();
        [SerializeField] ChalkStepper dangerStepper;
        [SerializeField] Button readyButton;
        [SerializeField] TMP_Text readyLabel;
        [SerializeField] Button startButton;
        [SerializeField] Button leaveButton;
        [SerializeField] TMP_Text status;
        [Tooltip("Цвета игроков по номеру — те же, что у имён над головой")]
        [SerializeField] Color[] slotColors =
        {
            new(0.98f, 0.85f, 0.36f),
            new(0.45f, 0.78f, 0.96f),
            new(0.96f, 0.56f, 0.71f),
            new(0.56f, 0.86f, 0.49f),
        };
        [SerializeField] Color freeKidColor = Color.white;
        [SerializeField] Color takenKidColor = new(1f, 1f, 1f, 0.3f);

        bool _open;
        float _copiedUntil;
        int _dangerShown = -1;

        /// <summary>Игрок ушёл из комнаты (кнопкой) — экран закрыть.</summary>
        public event Action Left;

        void Awake()
        {
            for (int i = 0; i < kidButtons.Length; i++)
            {
                int kid = i;
                kidButtons[i].onClick.AddListener(() => WithRoom(room => room.PickKid(kid)));
            }
            readyButton.onClick.AddListener(ToggleReady);
            startButton.onClick.AddListener(() => WithRoom(room => room.StartGame()));
            leaveButton.onClick.AddListener(Leave);
            copyButton.onClick.AddListener(Copy);
            if (dangerStepper)
                dangerStepper.Changed += index => WithRoom(room => room.SetDanger(index + 1));
        }

        /// <summary>Открыть экран. Возвращает, что выбрать клавиатурой и геймпадом.</summary>
        public Selectable Open()
        {
            if (!_open)
            {
                _open = true;
                NetRoom.Changed += Refresh;
            }
            _dangerShown = -1;
            Refresh();
            return IsHost ? startButton : readyButton;
        }

        public void Close()
        {
            if (!_open)
                return;
            _open = false;
            NetRoom.Changed -= Refresh;
        }

        void OnDestroy() => Close();

        void Update()
        {
            // «Скопировано!» гаснет само, а без событий комнаты экран обновлять незачем.
            if (_open && _copiedUntil > 0f && Time.unscaledTime >= _copiedUntil)
            {
                _copiedUntil = 0f;
                Refresh();
            }
        }

        static bool IsHost => NetSession.Instance != null && NetSession.Instance.IsHost;

        void Refresh()
        {
            if (!_open || this == null)
                return;
            var room = NetRoom.Current;
            var session = NetSession.Instance;
            bool host = IsHost;
            readyButton.gameObject.SetActive(!host);
            startButton.gameObject.SetActive(host);
            if (room == null || session == null)
            {
                code.text = "";
                foreach (var row in rows)
                    row.text = "";
                SetStatus("lobby.connecting");
                startButton.interactable = false;
                readyButton.interactable = false;
                return;
            }

            title.text = Loc.Get(room.Mode == NetMode.Versus ? "lobby.title.versus" : "lobby.title.coop");
            code.text = Loc.Format(session.IsLan ? "lobby.ip" : "lobby.code", session.Code);

            bool haveMe = room.TryGetLocal(out var me);
            int max = NetSession.MaxPlayers(room.Mode);
            for (int slot = 0; slot < rows.Length; slot++)
                ShowRow(rows[slot], slot, slot < max, room);

            ulong myId = haveMe ? me.ClientId : ulong.MaxValue;
            for (int kid = 0; kid < kidButtons.Length; kid++)
            {
                bool mine = haveMe && me.Kid == kid;
                bool taken = room.KidTaken(kid, myId);
                kidButtons[kid].interactable = !taken && !room.Started;
                var label = kidButtons[kid].GetComponentInChildren<TMP_Text>();
                if (label)
                {
                    label.text = KidName(kid);
                    label.color = mine ? SlotColor(me.Slot) : taken ? takenKidColor : freeKidColor;
                }
            }

            ShowDanger(room, host);
            readyLabel.text = Loc.Get(haveMe && me.Ready ? "lobby.unready" : "lobby.ready");
            readyButton.interactable = haveMe && me.Kid >= 0 && !room.Started;
            startButton.interactable = room.CanStart;

            if (Time.unscaledTime < _copiedUntil)
                SetStatus("lobby.copied");
            else if (host)
                SetStatus(room.CanStart ? "lobby.can.start" : "lobby.wait.ready");
            else
                SetStatus(haveMe && me.Ready ? "lobby.wait.host" : "lobby.pick.ready");
        }

        void ShowRow(TMP_Text row, int slot, bool open, NetRoom room)
        {
            if (row == null)
                return;
            row.gameObject.SetActive(open);
            if (!open)
                return;
            for (int i = 0; i < room.Count; i++)
            {
                var member = room[i];
                if (member.Slot != slot)
                    continue;
                string kid = member.Kid >= 0 ? KidName(member.Kid) : "…";
                string mark = member.ClientId == Unity.Netcode.NetworkManager.ServerClientId ? Loc.Get("lobby.host")
                    : member.Ready ? Loc.Get("lobby.ready.mark") : Loc.Get("lobby.notready.mark");
                row.text = $"{slot + 1}. {member.Name}  —  {kid}  ·  {mark}";
                row.color = SlotColor(slot);
                return;
            }
            row.text = $"{slot + 1}. {Loc.Get("lobby.empty")}";
            row.color = takenKidColor;
        }

        /// <summary>Опасность: хозяин листает открытые у себя уровни, гости видят выбранный.</summary>
        void ShowDanger(NetRoom room, bool host)
        {
            if (!dangerStepper)
                return;
            dangerStepper.GetComponent<Selectable>().interactable = host && !room.Started;
            int key = host ? room.Danger * 10 + Danger.Unlocked : room.Danger;
            if (key == _dangerShown)
                return;
            _dangerShown = key;
            if (host)
            {
                int unlocked = Danger.Unlocked;
                var options = new string[unlocked];
                for (int i = 0; i < unlocked; i++)
                    options[i] = Loc.Format("danger.level", i + 1);
                dangerStepper.SetOptions(options, room.Danger - 1);
            }
            else
            {
                dangerStepper.SetOptions(new[] { Loc.Format("danger.level", room.Danger) }, 0);
            }
        }

        void ToggleReady()
        {
            var room = NetRoom.Current;
            if (room != null && room.TryGetLocal(out var me))
                room.SetReady(!me.Ready);
        }

        void Copy()
        {
            var session = NetSession.Instance;
            if (session == null || string.IsNullOrEmpty(session.Code))
                return;
            GUIUtility.systemCopyBuffer = session.Code;
            _copiedUntil = Time.unscaledTime + 2f;
            Refresh();
        }

        void Leave()
        {
            if (NetSession.Instance != null)
                NetSession.Instance.Leave();
            Left?.Invoke();
        }

        static void WithRoom(Action<NetRoom> action)
        {
            if (NetRoom.Current != null)
                action(NetRoom.Current);
        }

        string KidName(int kid)
        {
            var definition = roster != null ? roster[kid] : null;
            return definition != null && !definition.displayName.IsEmpty ? definition.displayName.GetLocalizedString() : (kid + 1).ToString();
        }

        Color SlotColor(int slot) => slotColors.Length > 0 ? slotColors[Mathf.Abs(slot) % slotColors.Length] : Color.white;

        void SetStatus(string key)
        {
            if (status)
                status.text = string.IsNullOrEmpty(key) ? "" : Loc.Get(key);
        }
    }
}

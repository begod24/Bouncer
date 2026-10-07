using System;
using Bouncer.Core;
using Bouncer.Net;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Bouncer.UI
{
    public sealed class OnlineScreen : MonoBehaviour
    {
        [SerializeField] NetSession sessionPrefab;
        [SerializeField] TMP_InputField nameField;
        [SerializeField] TMP_InputField codeField;
        [SerializeField] Button createButton;
        [SerializeField] Button joinButton;
        [Tooltip("Комната по локальной сети (без интернета и сервисов)")]
        [SerializeField] Button lanButton;
        [SerializeField] TMP_Text status;

        NetMode _mode;
        bool _busy;

        public event Action RoomEntered;

        void Awake()
        {
            createButton.onClick.AddListener(() => Create(lan: false));
            lanButton.onClick.AddListener(() => Create(lan: true));
            joinButton.onClick.AddListener(Join);
            codeField.onSubmit.AddListener(_ => Join());
            nameField.characterLimit = GameSettings.PlayerNameLength;
            codeField.characterLimit = 15;
        }

        public Selectable Open(NetMode mode, string notice)
        {
            _mode = mode;
            nameField.text = GameSettings.PlayerName;
            if (codeField.text.Length == 0 && !string.IsNullOrEmpty(NetSession.LastJoinCode))
                codeField.text = NetSession.LastJoinCode;
            SetBusy(false);
            SetStatus(notice);
            return createButton;
        }

        async void Create(bool lan)
        {
            if (_busy)
                return;
            SaveName();
            SetBusy(true);
            SetStatus("online.busy");
            var session = NetSession.Ensure(sessionPrefab);
            bool ok = session != null && await session.CreateRoom(_mode, lan);
            Done(ok, session);
        }

        async void Join()
        {
            if (_busy)
                return;
            string code = codeField.text.Trim();
            if (code.Length == 0)
            {
                SetStatus("online.code.empty");
                return;
            }
            SaveName();
            SetBusy(true);
            SetStatus("online.busy");
            var session = NetSession.Ensure(sessionPrefab);
            bool ok = session != null && await session.JoinRoom(code);
            Done(ok, session);
        }

        void Done(bool ok, NetSession session)
        {
            if (this == null)
                return;
            SetBusy(false);
            if (ok)
            {
                SetStatus(null);
                RoomEntered?.Invoke();
            }
            else
            {
                SetStatus(session != null && !string.IsNullOrEmpty(session.ErrorKey) ? session.ErrorKey : "net.error.join");
            }
        }

        void SaveName()
        {
            GameSettings.PlayerName = nameField.text.Trim();
            GameSettings.Save();
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            createButton.interactable = !busy;
            joinButton.interactable = !busy;
            lanButton.interactable = !busy;
            nameField.interactable = !busy;
            codeField.interactable = !busy;
        }

        void SetStatus(string key)
        {
            if (status)
                status.text = string.IsNullOrEmpty(key) ? "" : Loc.Get(key);
        }
    }
}

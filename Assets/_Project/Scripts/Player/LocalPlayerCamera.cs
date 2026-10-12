using Bouncer.Core;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.Player
{
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class LocalPlayerCamera : MonoBehaviour
    {
        CinemachineCamera _camera;
        PlayerController _spectated;
        float _noticeAt;

        void Awake() => _camera = GetComponent<CinemachineCamera>();

        void OnEnable()
        {
            Players.LocalChanged += Follow;
            Follow(Players.Local);
        }

        void OnDisable() => Players.LocalChanged -= Follow;

        void Follow(PlayerController player)
        {
            if (player == null)
                return;
            _spectated = null;
            _camera.Target.TrackingTarget = player.Targetable ? player.Targetable.AimTransform : player.transform;
            _camera.PreviousStateIsValid = false;
        }

        /// <summary>Спектатор: пока тебя сбили (кооп), камера у живого напарника; ← → / A D / плечевые / клик — следующий.</summary>
        void LateUpdate()
        {
            var local = Players.Local;
            if (local == null)
                return;
            bool spectate = Online.Active && local.IsDead;
            if (!spectate || !HasCandidate(local))
            {
                if (_spectated != null)
                {
                    _spectated = null;
                    SetTarget(local, false);
                }
                return;
            }

            int step = StepPressed();
            if (_spectated == null || !IsCandidate(_spectated, local))
            {
                _spectated = Nearest(local);
                SetTarget(_spectated, true);
            }
            else if (step != 0)
            {
                _spectated = Next(local, _spectated, step);
                SetTarget(_spectated, true);
                Announce(_spectated);
            }
        }

        void SetTarget(PlayerController player, bool smooth)
        {
            if (player == null)
                return;
            _camera.Target.TrackingTarget = player.Targetable ? player.Targetable.AimTransform : player.transform;
            if (!smooth)
                _camera.PreviousStateIsValid = true;
        }

        void Announce(PlayerController player)
        {
            if (player == null || Time.unscaledTime < _noticeAt)
                return;
            _noticeAt = Time.unscaledTime + 0.4f;
            GameEvents.AnnounceLocal(new Announcement
            {
                Title = "net.spectate.title",
                Hint = "net.spectate.hint",
                Seconds = 3f,
                Arg = string.IsNullOrEmpty(player.DisplayName) ? $"#{player.Slot + 1}" : player.DisplayName,
            });
        }

        static bool IsCandidate(PlayerController player, PlayerController local) =>
            player != null && player != local && !player.IsLocal && !player.IsDead && !player.IsHome && player.isActiveAndEnabled;

        static bool HasCandidate(PlayerController local)
        {
            foreach (var player in Players.All)
                if (IsCandidate(player, local))
                    return true;
            return false;
        }

        static PlayerController Nearest(PlayerController local)
        {
            PlayerController best = null;
            float bestSqr = float.MaxValue;
            foreach (var player in Players.All)
            {
                if (!IsCandidate(player, local))
                    continue;
                float sqr = (player.transform.position - local.transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = player;
                }
            }
            return best;
        }

        static PlayerController Next(PlayerController local, PlayerController current, int step)
        {
            var all = Players.All;
            int start = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i] == current)
                    start = i;
            for (int offset = 1; offset <= all.Count; offset++)
            {
                var player = all[((start + offset * step) % all.Count + all.Count) % all.Count];
                if (IsCandidate(player, local))
                    return player;
            }
            return current;
        }

        static int StepPressed()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            var mouse = Mouse.current;
            bool right = (keyboard != null && (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame))
                         || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame)
                         || (mouse != null && mouse.leftButton.wasPressedThisFrame);
            bool left = (keyboard != null && (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame))
                        || (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame);
            return right ? 1 : left ? -1 : 0;
        }
    }
}

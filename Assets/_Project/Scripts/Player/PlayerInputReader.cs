using Bouncer.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bouncer.Player
{
    /// <summary>Локальный ввод (клавиатура с мышью или геймпад) → PlayerIntent.</summary>
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour, IPlayerIntentSource
    {
        const string GamepadScheme = "Gamepad";

        PlayerInput _playerInput;
        PlayerStats _stats;
        Camera _camera;
        InputAction _move;
        InputAction _aimPointer;
        InputAction _aimStick;
        InputAction _throw;
        InputAction _catch;
        InputAction _dash;
        InputAction _pause;

        void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
            _stats = GetComponent<PlayerController>().Stats;

            var actions = _playerInput.actions;
            _move = actions.FindAction("Player/Move", true);
            _aimPointer = actions.FindAction("Player/AimPointer", true);
            _aimStick = actions.FindAction("Player/AimStick", true);
            _throw = actions.FindAction("Player/Throw", true);
            _catch = actions.FindAction("Player/Catch", true);
            _dash = actions.FindAction("Player/Dash", true);
            _pause = actions.FindAction("Player/Pause", true);
        }

        public PlayerIntent ReadIntent()
        {
            if (_camera == null)
                _camera = Camera.main;

            bool gamepad = _playerInput.currentControlScheme == GamepadScheme;
            var intent = new PlayerIntent
            {
                UsingGamepad = gamepad,
                Move = ToWorld(_move.ReadValue<Vector2>()),
            };

            if (gamepad)
            {
                Vector2 stick = _aimStick.ReadValue<Vector2>();
                if (stick.sqrMagnitude > _stats.stickDeadzone * _stats.stickDeadzone)
                    intent.Aim = ToWorld(stick).normalized;
            }
            else if (_camera != null)
            {
                Ray ray = _camera.ScreenPointToRay(_aimPointer.ReadValue<Vector2>());
                var plane = new Plane(Vector3.up, new Vector3(0f, _stats.aimPlaneHeight, 0f));
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 delta = ray.GetPoint(enter) - transform.position;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > 0.04f)
                    {
                        intent.Aim = delta.normalized;
                        intent.AimFromPointer = true;
                    }
                }
            }

            // Клики по отладочному окну не должны бросать мяч.
            bool blocked = !gamepad && GameSettings.PointerOverDebugUI;
            intent.ThrowHeld = _throw.IsPressed() && !blocked;
            intent.ThrowPressed = _throw.WasPressedThisFrame() && !blocked;
            intent.ThrowReleased = _throw.WasReleasedThisFrame();
            intent.CatchPressed = _catch.WasPressedThisFrame() && !blocked;
            intent.DashPressed = _dash.WasPressedThisFrame();
            intent.PausePressed = _pause.WasPressedThisFrame();
            return intent;
        }

        /// <summary>Вектор ввода относительно камеры → мировая плоскость XZ.</summary>
        Vector3 ToWorld(Vector2 input)
        {
            if (_camera == null)
                return Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
            Vector3 forward = _camera.transform.forward;
            forward.y = 0f;
            Vector3 right = _camera.transform.right;
            right.y = 0f;
            return Vector3.ClampMagnitude(right.normalized * input.x + forward.normalized * input.y, 1f);
        }
    }
}

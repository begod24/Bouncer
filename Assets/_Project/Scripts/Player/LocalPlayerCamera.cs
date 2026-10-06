using Unity.Cinemachine;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Камера арены следит за своим игроком (<see cref="Players.Local"/>): игрока нет в сцене заранее,
    /// он появляется при старте — и камера сразу встаёт над ним, без подлёта.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class LocalPlayerCamera : MonoBehaviour
    {
        CinemachineCamera _camera;

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
            _camera.Target.TrackingTarget = player.Targetable ? player.Targetable.AimTransform : player.transform;
            _camera.PreviousStateIsValid = false;
        }
    }
}

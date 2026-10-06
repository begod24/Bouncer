using Unity.Cinemachine;
using UnityEngine;

namespace Bouncer.Player
{
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

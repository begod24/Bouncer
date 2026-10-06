using System;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bouncer.Player
{
    [DefaultExecutionOrder(-60)]
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] PlayerController playerPrefab;
        [Tooltip("Точки старта по номерам игроков: 1-й, 2-й, 3-й, 4-й. Синяя ось — куда смотрит игрок")]
        [SerializeField] Transform[] starts = Array.Empty<Transform>();

        public static PlayerSpawner Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            if (!Online.Active)
                Spawn(0, isLocal: true);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public PlayerController Spawn(int slot, bool isLocal)
        {
            var pose = StartPose(slot);
            var player = Instantiate(playerPrefab, pose.position, pose.rotation);
            player.name = playerPrefab.name;
            if (player.gameObject.scene != gameObject.scene)
                SceneManager.MoveGameObjectToScene(player.gameObject, gameObject.scene);
            player.Setup(slot, isLocal);
            return player;
        }

        public Pose StartPose(int slot)
        {
            Transform start = null;
            for (int i = Mathf.Min(slot, starts.Length - 1); i >= 0 && start == null; i--)
                start = starts[i];
            if (start == null)
                start = transform;
            Vector3 forward = start.forward;
            forward.y = 0f;
            var rotation = forward.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(forward) : Quaternion.identity;
            return new Pose(start.position, rotation);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f);
            for (int i = 0; i < starts.Length; i++)
            {
                var start = starts[i];
                if (!start)
                    continue;
                Vector3 p = start.position;
                Gizmos.DrawWireSphere(p + Vector3.up * 0.7f, 0.35f);
                Gizmos.DrawLine(p, p + Vector3.up * 1.4f);
                Gizmos.DrawLine(p + Vector3.up * 0.7f, p + Vector3.up * 0.7f + start.forward * 0.8f);
            }
        }
    }
}

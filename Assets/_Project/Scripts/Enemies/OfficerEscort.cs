using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Офицер с саблей выходит не один: рядом появляется шеренга обычных солдатиков, и он ею командует —
    /// строй стреляет залпами по расписанию офицера (<see cref="SoldierSquad.Leader"/>).
    /// Свиту зовёт кадром позже появления: к этому времени спавнер уже собрал группу из самого офицера.
    /// </summary>
    [RequireComponent(typeof(TinSoldierEnemy))]
    public sealed class OfficerEscort : MonoBehaviour, IPoolable
    {
        [SerializeField] TinSoldierEnemy escortPrefab;
        [SerializeField, Min(0)] int escorts = 3;
        [Tooltip("Расстояние между солдатиками свиты")]
        [SerializeField] float spacing = 1.4f;

        TinSoldierEnemy _officer;
        bool _pending;

        void Awake() => _officer = GetComponent<TinSoldierEnemy>();

        public void OnSpawned() => _pending = true;

        public void OnDespawned() => _pending = false;

        void Update()
        {
            if (!_pending)
                return;
            _pending = false;
            var squad = new SoldierSquad { Leader = _officer };
            _officer.JoinSquad(squad);
            if (escortPrefab == null)
                return;
            Vector3 right = transform.right;
            for (int i = 0; i < escorts; i++)
            {
                float offset = (i + 1) * spacing * (i % 2 == 0 ? 1f : -1f) * 0.75f;
                Vector3 position = transform.position + right * offset - transform.forward * 0.6f;
                if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    position = hit.position;
                var soldier = PoolService.Spawn(escortPrefab, position, transform.rotation);
                soldier.JoinSquad(squad);
            }
        }
    }
}

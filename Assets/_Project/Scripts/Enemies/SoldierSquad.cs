using System.Collections.Generic;
using Bouncer.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Строй оловянных солдатиков: общая позиция шеренги на дистанции от игрока и расписание залпов.
    /// Залп: весь строй замахивается разом, потом бросает по очереди слева направо.
    /// Обычный объект без MonoBehaviour — его обновляет первый солдатик, дошедший до Update в этом кадре.
    /// </summary>
    public sealed class SoldierSquad
    {
        readonly List<TinSoldierEnemy> _members = new();
        int _tickFrame = -1;
        bool _hasAnchor;
        Vector3 _anchor;
        Vector3 _facing = Vector3.forward;
        float _nextRegroup;
        float _nextVolley;

        public Targetable Target { get; private set; }
        public int VolleyId { get; private set; }
        public float VolleyStart { get; private set; } = float.NegativeInfinity;
        public int Count => _members.Count;

        public void Add(TinSoldierEnemy soldier, TinSoldierDefinition definition)
        {
            if (_members.Contains(soldier))
                return;
            _members.Add(soldier);
            _hasAnchor = false;
            _nextVolley = Mathf.Max(_nextVolley, Time.time + definition.firstVolleyDelay);
        }

        public void Remove(TinSoldierEnemy soldier)
        {
            if (_members.Remove(soldier))
                _hasAnchor = false;
        }

        /// <summary>Сейчас идёт залп: от начала замаха до броска последнего в шеренге.</summary>
        public bool IsVolleyActive(TinSoldierDefinition d) =>
            Time.time >= VolleyStart && Time.time <= VolleyStart + d.aimTime + _members.Count * d.volleyStagger;

        public float ThrowTimeFor(TinSoldierEnemy soldier, TinSoldierDefinition d) =>
            VolleyStart + d.aimTime + Mathf.Max(0, _members.IndexOf(soldier)) * d.volleyStagger;

        /// <summary>Место солдатика в шеренге, повёрнутой лицом к игроку.</summary>
        public Vector3 SlotFor(TinSoldierEnemy soldier, TinSoldierDefinition d)
        {
            int index = Mathf.Max(0, _members.IndexOf(soldier));
            Vector3 right = Vector3.Cross(Vector3.up, _facing);
            return _anchor + right * ((index - (_members.Count - 1) * 0.5f) * d.slotSpacing);
        }

        public void Tick(TinSoldierDefinition d)
        {
            if (_tickFrame == Time.frameCount || _members.Count == 0)
                return;
            _tickFrame = Time.frameCount;

            Vector3 centroid = Vector3.zero;
            foreach (var member in _members)
                centroid += member.transform.position;
            centroid /= _members.Count;

            Target = Targetable.FindNearest(centroid, Team.Player);
            if (Target == null)
                return;

            Vector3 fromTarget = Flat(centroid - Target.Position);
            float distance = fromTarget.magnitude;
            if (!_hasAnchor || Time.time >= _nextRegroup)
            {
                _nextRegroup = Time.time + d.regroupInterval;
                Vector3 direction = distance > 0.1f ? fromTarget / distance : Vector3.forward;
                Vector3 anchor = Target.Position + direction * d.preferredDistance;
                _anchor = NavMesh.SamplePosition(anchor, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : centroid;
                Vector3 facing = Flat(Target.Position - _anchor);
                _facing = facing.sqrMagnitude > 0.01f ? facing.normalized : -direction;
                _hasAnchor = true;
            }

            if (Time.time >= _nextVolley && distance <= d.maxThrowRange)
            {
                VolleyId++;
                VolleyStart = Time.time;
                _nextVolley = Time.time + d.aimTime + _members.Count * d.volleyStagger + d.reloadTime;
            }
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}

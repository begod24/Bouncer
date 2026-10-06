using Bouncer.Core;
using Bouncer.Player;
using TMPro;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Стрелка мелом на следующую арену («В коробку →»). Появляется, когда арена пройдена; игрок заходит на неё —
    /// и прогулка идёт дальше (<see cref="ArenaDirector.LeaveArena"/>). На развилке стрелок две: каждая помнит,
    /// на какую арену этапа ведёт.
    /// По сети команда «голосует ногами»: дальше идут, когда все живые игроки стоят у одной стрелки (решает
    /// хозяин комнаты); под надписью — сколько уже стоят, «2/3».
    /// </summary>
    public sealed class ArenaExit : MonoBehaviour
    {
        [Tooltip("Стрелка и надпись: видны только на пройденной арене")]
        [SerializeField] GameObject visual;
        [SerializeField] TMP_Text label;
        [Tooltip("Что покачивается, чтобы стрелку было видно издалека")]
        [SerializeField] Transform pulse;
        [SerializeField] float radius = 1.6f;
        [Tooltip("По сети: сколько все должны простоять у стрелки, прежде чем идти дальше, с")]
        [SerializeField] float gatherTime = 0.6f;

        Vector3 _pulseScale = Vector3.one;
        bool _shown;
        bool _left;
        int _variant;
        string _text;
        int _shownHere = -1;
        int _shownAlive = -1;
        float _gatheredAt = -1f;

        public bool IsShown => _shown;

        void Awake()
        {
            if (pulse)
                _pulseScale = pulse.localScale;
            SetShown(false);
        }

        /// <summary>Показать стрелку; variant — на какую арену следующего этапа она ведёт (0 — основная).</summary>
        public void Show(string text, int variant = 0)
        {
            _text = text;
            _shownHere = _shownAlive = -1;
            if (label)
                label.text = text;
            _variant = variant;
            SetShown(true);
        }

        public void Hide() => SetShown(false);

        void SetShown(bool shown)
        {
            _shown = shown;
            if (visual)
                visual.SetActive(shown);
        }

        void Update()
        {
            if (!_shown)
                return;
            if (pulse)
                pulse.localScale = _pulseScale * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f));

            var session = GameSession.Instance;
            if (_left || session == null || session.State != SessionState.Cleared)
                return;
            if (Online.Active)
            {
                UpdateOnline();
                return;
            }
            if (!GameSession.IsPlayerActive)
                return;
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > radius * radius)
                    continue;
                if (ArenaDirector.Instance != null && ArenaDirector.Instance.LeaveArena(_variant))
                    _left = true;
                return;
            }
        }

        /// <summary>По сети: сколько живых у стрелки; все у неё — хозяин ведёт всех дальше.</summary>
        void UpdateOnline()
        {
            int alive = 0;
            int here = 0;
            foreach (var player in Players.All)
            {
                if (player.IsDead)
                    continue;
                alive++;
                Vector3 delta = player.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radius * radius)
                    here++;
            }
            if (label && (here != _shownHere || alive != _shownAlive))
            {
                _shownHere = here;
                _shownAlive = alive;
                label.text = here > 0 ? $"{_text}\n{here}/{alive}" : _text;
            }
            if (alive == 0 || here < alive)
            {
                _gatheredAt = -1f;
                return;
            }
            if (_gatheredAt < 0f)
                _gatheredAt = Time.time;
            if (!Online.IsHost || Time.time - _gatheredAt < gatherTime)
                return;
            if (ArenaDirector.Instance != null && ArenaDirector.Instance.LeaveArena(_variant))
                _left = true;
        }
    }
}

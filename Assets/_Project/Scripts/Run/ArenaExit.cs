using Bouncer.Core;
using TMPro;
using UnityEngine;

namespace Bouncer.Run
{
    /// <summary>
    /// Стрелка мелом на следующую арену («В коробку →»). Появляется, когда арена пройдена; игрок заходит на неё —
    /// и прогулка идёт дальше (<see cref="ArenaDirector.LeaveArena"/>).
    /// </summary>
    public sealed class ArenaExit : MonoBehaviour
    {
        [Tooltip("Стрелка и надпись: видны только на пройденной арене")]
        [SerializeField] GameObject visual;
        [SerializeField] TMP_Text label;
        [Tooltip("Что покачивается, чтобы стрелку было видно издалека")]
        [SerializeField] Transform pulse;
        [SerializeField] float radius = 1.6f;

        Vector3 _pulseScale = Vector3.one;
        bool _shown;
        bool _left;

        public bool IsShown => _shown;

        void Awake()
        {
            if (pulse)
                _pulseScale = pulse.localScale;
            SetShown(false);
        }

        public void Show(string text)
        {
            if (label)
                label.text = text;
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
            if (_left || session == null || session.State != SessionState.Cleared || !GameSession.IsPlayerActive)
                return;
            foreach (var target in Targetable.All)
            {
                if (target.Team != Team.Player || !target.IsAlive)
                    continue;
                Vector3 delta = target.Position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > radius * radius)
                    continue;
                if (ArenaDirector.Instance != null && ArenaDirector.Instance.LeaveArena())
                    _left = true;
                return;
            }
        }
    }
}

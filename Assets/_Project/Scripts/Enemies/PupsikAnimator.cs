using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Процедурная анимация пупса без скелета: ножки качаются в бёдрах, тельце переваливается и подпрыгивает,
    /// вытянутые ручки болтаются. Присед перед прыжком, в прыжке ручки вверх. Только визуал.
    /// </summary>
    [RequireComponent(typeof(PupsikEnemy))]
    public sealed class PupsikAnimator : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        [Header("Бег")]
        [Tooltip("Шагов в секунду на полной скорости")]
        [SerializeField] float stepRate = 8f;
        [SerializeField] float legSwing = 40f;
        [SerializeField] float bodyBob = 0.06f;
        [SerializeField] float bodyRoll = 9f;
        [SerializeField] float armFlap = 12f;

        [Header("Прыжок")]
        [SerializeField] float squatDepth = 0.08f;
        [SerializeField] float hopArmRaise = 70f;
        [SerializeField] float hopLegTuck = 35f;

        PupsikEnemy _pupsik;
        Vector3 _bodyRest;
        float _phase;
        float _pose;

        void Awake()
        {
            _pupsik = GetComponent<PupsikEnemy>();
            _bodyRest = body.localPosition;
        }

        void OnEnable() => _phase = Random.value * Mathf.PI * 2f;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var state = _pupsik.CurrentState;
            float speed01 = Mathf.Clamp01(_pupsik.PlanarVelocity.magnitude / Mathf.Max(0.1f, _pupsik.Definition.moveSpeed));
            bool running = state == PupsikEnemy.State.Chase;
            if (running)
                _phase += dt * stepRate * Mathf.Lerp(0.4f, 1f, speed01) * Mathf.PI;

            // 0 — бег, 1 — присед/прыжок: плавно переходим между позами.
            float targetPose = state is PupsikEnemy.State.Windup or PupsikEnemy.State.Hop ? 1f : 0f;
            _pose = Mathf.MoveTowards(_pose, targetPose, dt * 8f);

            float sin = Mathf.Sin(_phase);
            float run = running ? speed01 : 0f;
            float squat = state == PupsikEnemy.State.Windup ? Mathf.Clamp01(_pupsik.StateTime / 0.12f) : 0f;

            body.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(sin) * bodyBob * run - squat * squatDepth);
            body.localRotation = Quaternion.Euler(0f, 0f, sin * bodyRoll * run);

            float legs = sin * legSwing * run * (1f - _pose);
            float tuck = -hopLegTuck * _pose;
            legL.localRotation = Quaternion.Euler(legs + tuck, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-legs + tuck, 0f, 0f);

            float flap = Mathf.Sin(_phase * 2f) * armFlap * run;
            float raise = -hopArmRaise * _pose;
            armL.localRotation = Quaternion.Euler(flap + raise, 0f, 0f);
            armR.localRotation = Quaternion.Euler(-flap + raise, 0f, 0f);
        }
    }
}

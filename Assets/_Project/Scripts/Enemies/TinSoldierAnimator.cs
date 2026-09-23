using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Процедурная анимация оловянного солдатика без скелета: марш на прямых ногах, отмашка левой рукой,
    /// замах правой за голову и бросок, покачивание всей фигуркой от попадания. Только визуал.
    /// </summary>
    [RequireComponent(typeof(TinSoldierEnemy))]
    public sealed class TinSoldierAnimator : MonoBehaviour
    {
        [Tooltip("Корень модели: наклоняется целиком от попадания (пивот у ног)")]
        [SerializeField] Transform visual;
        [SerializeField] Transform body;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;
        [Tooltip("Мяч в руке: скрыт, пока солдатик «перезаряжается»")]
        [SerializeField] GameObject handBall;

        [Header("Марш")]
        [Tooltip("Шагов в секунду на полной скорости")]
        [SerializeField] float stepRate = 3.5f;
        [SerializeField] float legSwing = 28f;
        [SerializeField] float armSwing = 30f;
        [SerializeField] float bodyBob = 0.03f;

        [Header("Бросок")]
        [Tooltip("Рука за головой на замахе, градусы вокруг оси X плеча")]
        [SerializeField] float windupAngle = 150f;
        [Tooltip("Рука после броска — вперёд и вниз")]
        [SerializeField] float followThroughAngle = -70f;

        [Header("Попадание")]
        [SerializeField] float staggerTilt = 22f;
        [SerializeField] float staggerWobble = 16f;
        [SerializeField] float staggerDamping = 5f;

        TinSoldierEnemy _soldier;
        Vector3 _bodyRest;
        float _phase;
        float _march;
        float _armR;

        void Awake()
        {
            _soldier = GetComponent<TinSoldierEnemy>();
            _bodyRest = body.localPosition;
        }

        void OnEnable()
        {
            _phase = 0f;
            _march = 0f;
            _armR = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var state = _soldier.CurrentState;
            float speed01 = Mathf.Clamp01(_soldier.PlanarVelocity.magnitude / Mathf.Max(0.1f, _soldier.Definition.moveSpeed));
            _march = Mathf.MoveTowards(_march, state == TinSoldierEnemy.State.March ? speed01 : 0f, dt * 4f);
            _phase += dt * stepRate * Mathf.PI * Mathf.Max(_march, 0.001f);

            float sin = Mathf.Sin(_phase);
            body.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(sin) * bodyBob * _march);
            legL.localRotation = Quaternion.Euler(sin * legSwing * _march, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-sin * legSwing * _march, 0f, 0f);
            armL.localRotation = Quaternion.Euler(-sin * armSwing * _march, 0f, 0f);

            // Правая рука: держит мяч перед собой, на замахе уходит за голову, после броска — вперёд.
            float targetArm = state switch
            {
                TinSoldierEnemy.State.Aim => windupAngle * Ease(_soldier.AimProgress),
                TinSoldierEnemy.State.Throw => followThroughAngle,
                _ => 0f,
            };
            float armSpeed = state == TinSoldierEnemy.State.Throw ? 1400f : 360f;
            _armR = Mathf.MoveTowards(_armR, targetArm, armSpeed * dt);
            armR.localRotation = Quaternion.Euler(_armR, 0f, 0f);

            if (handBall && handBall.activeSelf != _soldier.HasBall)
                handBall.SetActive(_soldier.HasBall);

            // Попадание: негнущаяся фигурка качается целиком, как игрушка на подставке.
            Quaternion tilt = Quaternion.identity;
            if (state == TinSoldierEnemy.State.Stagger)
            {
                float t = _soldier.StateTime;
                float angle = staggerTilt * Mathf.Exp(-staggerDamping * t) * Mathf.Cos(t * staggerWobble);
                Vector3 axis = Vector3.Cross(Vector3.up, _soldier.LastHitDirection);
                if (axis.sqrMagnitude > 1e-4f)
                    tilt = Quaternion.AngleAxis(angle, axis.normalized);
            }
            visual.rotation = tilt * transform.rotation;
        }

        static float Ease(float t) => t * t * (3f - 2f * t);
    }
}

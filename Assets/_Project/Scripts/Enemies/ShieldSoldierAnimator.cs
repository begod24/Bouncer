using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Процедурная анимация солдатика с крышкой: марш на прямых ногах, крышка перед грудью (вздрагивает от мяча,
    /// выбитая — уходит в сторону), замах половником и удар крышкой, покачивание от попадания. Только визуал.
    /// </summary>
    [RequireComponent(typeof(ShieldSoldierEnemy))]
    public sealed class ShieldSoldierAnimator : MonoBehaviour
    {
        [Tooltip("Корень модели: наклоняется целиком от попадания (пивот у ног)")]
        [SerializeField] Transform visual;
        [SerializeField] Transform body;
        [Tooltip("Левая рука держит крышку (крышка — её дочерний объект)")]
        [SerializeField] Transform armL;
        [Tooltip("Правая рука с половником")]
        [SerializeField] Transform armR;
        [SerializeField] Transform lid;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;

        [Header("Марш")]
        [SerializeField] float stepRate = 3.2f;
        [SerializeField] float legSwing = 26f;
        [SerializeField] float bodyBob = 0.03f;

        [Header("Крышка и половник")]
        [Tooltip("Отбитый мяч отталкивает крышку назад, градусы")]
        [SerializeField] float blockKickAngle = 25f;
        [Tooltip("Выбитая крышка: рука уходит в сторону, градусы")]
        [SerializeField] float brokenArmAngle = 75f;
        [Tooltip("Замах половником за голову, градусы вокруг оси X плеча")]
        [SerializeField] float windupAngle = 140f;
        [Tooltip("Удар половником: рука вперёд и вниз")]
        [SerializeField] float strikeAngle = -45f;
        [Tooltip("Удар: рука с крышкой выбрасывается вперёд")]
        [SerializeField] float bashArmAngle = -35f;

        [Header("Попадание")]
        [SerializeField] float staggerTilt = 18f;
        [SerializeField] float staggerWobble = 16f;
        [SerializeField] float staggerDamping = 5f;

        ShieldSoldierEnemy _soldier;
        Vector3 _bodyRest;
        Quaternion _lidRest;
        float _phase;
        float _march;
        float _armR;
        float _armLForward;
        float _armLSide;

        void Awake()
        {
            _soldier = GetComponent<ShieldSoldierEnemy>();
            _bodyRest = body.localPosition;
            _lidRest = lid ? lid.localRotation : Quaternion.identity;
        }

        void OnEnable()
        {
            _phase = 0f;
            _march = 0f;
            _armR = 0f;
            _armLForward = 0f;
            _armLSide = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            var state = _soldier.CurrentState;
            float speed01 = Mathf.Clamp01(_soldier.PlanarVelocity.magnitude / Mathf.Max(0.1f, _soldier.Definition.moveSpeed));
            _march = Mathf.MoveTowards(_march, state == ShieldSoldierEnemy.State.Advance ? speed01 : 0f, dt * 4f);
            _phase += dt * stepRate * Mathf.PI * Mathf.Max(_march, 0.001f);

            float sin = Mathf.Sin(_phase);
            body.localPosition = _bodyRest + Vector3.up * (Mathf.Abs(sin) * bodyBob * _march);
            legL.localRotation = Quaternion.Euler(sin * legSwing * _march, 0f, 0f);
            legR.localRotation = Quaternion.Euler(-sin * legSwing * _march, 0f, 0f);

            // Половник: замах над головой и удар.
            float targetR = state switch
            {
                ShieldSoldierEnemy.State.Windup => windupAngle,
                ShieldSoldierEnemy.State.Bash => strikeAngle,
                _ => sin * 12f * _march,
            };
            _armR = Mathf.MoveTowards(_armR, targetR, (state == ShieldSoldierEnemy.State.Bash ? 1500f : 420f) * dt);
            armR.localRotation = Quaternion.Euler(_armR, 0f, 0f);

            // Крышка: вперёд при ударе, в сторону — если её выбили.
            float targetForward = state == ShieldSoldierEnemy.State.Bash ? bashArmAngle : 0f;
            float targetSide = _soldier.GuardUp ? 0f : brokenArmAngle;
            _armLForward = Mathf.MoveTowards(_armLForward, targetForward, 900f * dt);
            _armLSide = Mathf.MoveTowards(_armLSide, targetSide, (_soldier.GuardUp ? 240f : 900f) * dt);
            // Левая рука солдатика — на -X в Unity: отвести наружу = поворот вокруг Z в минус.
            armL.localRotation = Quaternion.Euler(_armLForward, 0f, -_armLSide);
            if (lid)
                lid.localRotation = _lidRest * Quaternion.Euler(-_soldier.BlockKick * blockKickAngle, 0f, 0f);

            Quaternion tilt = Quaternion.identity;
            if (state == ShieldSoldierEnemy.State.Stagger)
            {
                float t = _soldier.StateTime;
                float angle = staggerTilt * Mathf.Exp(-staggerDamping * t) * Mathf.Cos(t * staggerWobble);
                Vector3 axis = Vector3.Cross(Vector3.up, _soldier.LastHitDirection);
                if (axis.sqrMagnitude > 1e-4f)
                    tilt = Quaternion.AngleAxis(angle, axis.normalized);
            }
            visual.rotation = tilt * transform.rotation;
        }
    }
}

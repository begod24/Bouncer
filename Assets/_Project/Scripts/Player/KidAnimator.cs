using Bouncer.Balls;
using Bouncer.Core;
using UnityEngine;

namespace Bouncer.Player
{
    /// <summary>
    /// Передаёт состояние игрока аниматору ребёнка (AC_Kid, его собирает Bouncer → Build Kid Animator):
    /// бег по направлению относительно взгляда, рывок и подкат, замах и бросок, ловля, удар, выбывание,
    /// радость в конце арены и после победы. Ребёнок всегда смотрит туда, куда целится игрок (ловят только спереди),
    /// поэтому бег вбок и назад — свои клипы, а не поворот модели. Только в рывке модель разворачивается по нему.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(PlayerKid))]
    public sealed class KidAnimator : MonoBehaviour
    {
        static readonly int MoveX = Animator.StringToHash("MoveX");
        static readonly int MoveZ = Animator.StringToHash("MoveZ");
        static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
        static readonly int Dashing = Animator.StringToHash("Dashing");
        static readonly int Sliding = Animator.StringToHash("Sliding");
        static readonly int Down = Animator.StringToHash("Down");
        static readonly int Cheer = Animator.StringToHash("Cheer");
        static readonly int Charging = Animator.StringToHash("Charging");
        static readonly int Charge = Animator.StringToHash("Charge");
        static readonly int Catching = Animator.StringToHash("Catching");
        static readonly int Throw = Animator.StringToHash("Throw");
        static readonly int Caught = Animator.StringToHash("Caught");
        static readonly int Hurt = Animator.StringToHash("Hurt");
        const int UpperBodyLayer = 1;

        [SerializeField] PlayerController player;
        [Tooltip("Скорость бега, при которой клип бега идёт с обычной скоростью (ноги не скользят), м/с")]
        [SerializeField] float naturalRunSpeed = 4.5f;
        [Tooltip("Скорость клипа бега: не медленнее и не быстрее, чем во столько раз")]
        [SerializeField] Vector2 runSpeedRange = new(0.7f, 1.9f);
        [Tooltip("Сглаживание направления бега, с")]
        [SerializeField] float moveDamp = 0.06f;
        [Tooltip("Как быстро модель разворачивается по рывку и обратно, °/с")]
        [SerializeField] float dashTurnSpeed = 1800f;
        [Tooltip("Сколько радоваться, когда арена пройдена, с")]
        [SerializeField] float clearedCheer = 1.8f;

        PlayerKid _kid;
        Vector3 _lastPosition;
        float _cheerUntil;
        SessionState _lastState;
        float _upperWeight = 1f;

        void Awake() => _kid = GetComponent<PlayerKid>();

        void Start()
        {
            _lastPosition = transform.position;
            player.Hurt += OnHurt;
            player.Balls.Thrown += OnThrown;
            player.Balls.Caught += OnCaught;
            player.Balls.Fumbled += OnFumbled;
        }

        void OnDestroy()
        {
            if (player == null || player.Balls == null)
                return;
            player.Hurt -= OnHurt;
            player.Balls.Thrown -= OnThrown;
            player.Balls.Caught -= OnCaught;
            player.Balls.Fumbled -= OnFumbled;
        }

        void Update()
        {
            var animator = _kid.Animator;
            float dt = Time.deltaTime;
            Vector3 position = transform.position;
            Vector3 delta = position - _lastPosition;
            _lastPosition = position;
            if (animator == null || !animator.isActiveAndEnabled || dt <= 0f)
                return;

            // В сценке (дорога домой) контроллер выключен и игрока двигают снаружи — скорость по смещению.
            Vector3 velocity = player.IsScripted ? delta / dt : player.Motor.Velocity;
            velocity.y = 0f;
            float maxSpeed = Mathf.Max(0.1f, player.Stats.moveSpeed * player.Modifiers.MoveSpeed);
            Vector3 local = Quaternion.Inverse(_kid.Slot.rotation) * velocity / maxSpeed;
            Vector2 move = Vector2.ClampMagnitude(new Vector2(local.x, local.z), 1f);
            animator.SetFloat(MoveX, move.x, moveDamp, dt);
            animator.SetFloat(MoveZ, move.y, moveDamp, dt);
            float speed = velocity.magnitude;
            animator.SetFloat(RunSpeed, speed > 0.3f
                ? Mathf.Clamp(speed / naturalRunSpeed, runSpeedRange.x, runSpeedRange.y) : 1f);

            var motor = player.Motor;
            bool sliding = motor.IsDashing && player.Modifiers.TackleDamage > 0;
            bool dashing = motor.IsDashing && !sliding;
            bool down = player.IsDead;
            animator.SetBool(Dashing, dashing);
            animator.SetBool(Sliding, sliding);
            animator.SetBool(Down, down);
            animator.SetBool(Charging, player.Balls.IsCharging);
            animator.SetFloat(Charge, player.Balls.Charge01);
            animator.SetBool(Catching, player.Balls.IsCatching);

            var session = GameSession.Instance;
            var state = session != null ? session.State : SessionState.Playing;
            if (state == SessionState.Cleared && _lastState == SessionState.Playing)
                _cheerUntil = Time.time + clearedCheer;
            _lastState = state;
            bool cheer = !down && (state == SessionState.Victory || Time.time < _cheerUntil);
            animator.SetBool(Cheer, cheer);

            // Руки и корпус слушают замах и ловлю, только когда всё тело не занято своим клипом.
            float upperTarget = down || dashing || sliding || cheer ? 0f : 1f;
            _upperWeight = Mathf.MoveTowards(_upperWeight, upperTarget, dt * 8f);
            animator.SetLayerWeight(UpperBodyLayer, _upperWeight);

            // Рывок: модель ныряет туда, куда рвётся игрок, потом возвращается к взгляду.
            Quaternion target = motor.IsDashing && motor.DashDirection.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(motor.DashDirection)
                : transform.rotation;
            _kid.Slot.rotation = Quaternion.RotateTowards(_kid.Slot.rotation, target, dashTurnSpeed * dt);
        }

        void OnHurt(HitInfo hit) => SetTrigger(Hurt);

        void OnFumbled() => SetTrigger(Hurt);

        void OnThrown(ThrowStats stats) => SetTrigger(Throw);

        void OnCaught(CatchInfo info) => SetTrigger(Caught);

        void SetTrigger(int id)
        {
            var animator = _kid.Animator;
            if (animator != null && animator.isActiveAndEnabled)
                animator.SetTrigger(id);
        }
    }
}

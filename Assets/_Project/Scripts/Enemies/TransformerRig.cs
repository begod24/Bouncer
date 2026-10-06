using UnityEngine;

namespace Bouncer.Enemies
{
    /// <summary>
    /// Две позы Трансформера из ларька. Модель в префабе — робот (Boss_Transformer.fbx), поза машины снята с
    /// Boss_Transformer_Car.fbx (та же иерархия частей) инструментом редактора и лежит здесь массивами.
    /// Превращение: каждая часть едет от одной позы к другой в своё окно общего хода (сначала ноги, потом грудь,
    /// руки, крыша, двери, голова). Поверх позы — шаг робота (ноги, руки, покачивание таза) и колёса машины.
    /// Только вид; логика — в <see cref="TransformerBoss"/>.
    /// </summary>
    public sealed class TransformerRig : MonoBehaviour
    {
        [Tooltip("Части модели в том же порядке, что позы")]
        [SerializeField] Transform[] parts;
        [SerializeField] Vector3[] robotPositions;
        [SerializeField] Quaternion[] robotRotations;
        [SerializeField] Vector3[] carPositions;
        [SerializeField] Quaternion[] carRotations;
        [Tooltip("С какой доли общего хода превращения начинает двигаться часть")]
        [SerializeField] float[] delays;
        [Tooltip("Сколько общего хода занимает движение одной части")]
        [SerializeField] float partSpan = 0.45f;

        [Header("Шаг робота и колёса")]
        [SerializeField] Transform pelvis;
        [SerializeField] Transform legL;
        [SerializeField] Transform legR;
        [SerializeField] Transform armL;
        [SerializeField] Transform armR;
        [SerializeField] Transform[] wheels;
        [SerializeField] float legSwing = 22f;
        [SerializeField] float armSwing = 18f;
        [SerializeField] float stepRate = 1.6f;

        [Header("Коробка попаданий: робот ↔ машина")]
        [SerializeField] BoxCollider hitBox;
        [SerializeField] Vector3 robotBoxCenter = new(0f, 1.85f, -0.1f);
        [SerializeField] Vector3 robotBoxSize = new(2.2f, 3.7f, 1.5f);
        [SerializeField] Vector3 carBoxCenter = new(0f, 0.75f, 0f);
        [SerializeField] Vector3 carBoxSize = new(1.8f, 1.5f, 4.2f);
        [Tooltip("Точка прицела: у робота — грудь, у машины — капот")]
        [SerializeField] Transform aimPoint;
        [SerializeField] float robotAimHeight = 2.2f;
        [SerializeField] float carAimHeight = 0.8f;

        float _from;
        float _to;
        float _progress = 1f;
        float _walkPhase;
        float _wheelAngle;

        /// <summary>0 — робот, 1 — машина (с учётом начатого превращения).</summary>
        public float CarAmount { get; private set; }
        /// <summary>0..1: насколько быстро идёт робот (размах шага).</summary>
        public float Walk { get; set; }
        /// <summary>Скорость машины, м/с: крутит колёса.</summary>
        public float DriveSpeed { get; set; }
        /// <summary>Рука с пушкой поднята на игрока (0..1).</summary>
        public float AimCannon { get; set; }
        /// <summary>Левая рука замахнулась бросить батарейку (0..1).</summary>
        public float WindThrow { get; set; }
        public float WheelRadius { get; set; } = 0.3f;

        public bool IsTransforming => _progress < 1f;
        public bool IsCar => !IsTransforming && _to >= 0.5f;
        /// <summary>Во что превращается (или уже превратился): true — в машину.</summary>
        public bool TargetIsCar => _to >= 0.5f;
        /// <summary>Ход превращения 0..1 (1 — превращение закончено).</summary>
        public float Progress => _progress;

        public int PartCount => parts?.Length ?? 0;

        /// <summary>Сразу встать в позу (true — машина).</summary>
        public void SetMode(bool car)
        {
            _from = _to = car ? 1f : 0f;
            _progress = 1f;
            Apply();
        }

        /// <summary>Начать превращение в машину (car = true) или в робота.</summary>
        public void BeginTransform(bool car)
        {
            _from = CarAmount;
            _to = car ? 1f : 0f;
            _progress = 0f;
        }

        /// <summary>Ход превращения 0..1 (его ведёт босс по своему таймеру).</summary>
        public void SetProgress(float progress) => _progress = Mathf.Clamp01(progress);

        /// <summary>По сети у гостя: превращается в машину (car) или в робота и прошёл progress хода, как у хозяина.</summary>
        public void SetNet(bool car, float progress)
        {
            float to = car ? 1f : 0f;
            if (!Mathf.Approximately(_to, to))
            {
                _from = 1f - to;
                _to = to;
            }
            _progress = Mathf.Clamp01(progress);
        }

        /// <summary>Инструмент редактора: записать обе позы и порядок.</summary>
        public void SetPoses(Transform[] partList, Vector3[] robotPos, Quaternion[] robotRot, Vector3[] carPos, Quaternion[] carRot, float[] delayList)
        {
            parts = partList;
            robotPositions = robotPos;
            robotRotations = robotRot;
            carPositions = carPos;
            carRotations = carRot;
            delays = delayList;
        }

        void LateUpdate()
        {
            Apply();
            float dt = Time.deltaTime;

            // Робот шагает: ноги и руки вперёд-назад, таз покачивается.
            float robot = 1f - CarAmount;
            _walkPhase += dt * stepRate * Mathf.PI * 2f * Mathf.Max(Walk, 0.05f);
            float swing = Mathf.Sin(_walkPhase) * Walk * robot;
            if (legL)
                legL.localRotation *= Quaternion.Euler(swing * legSwing, 0f, 0f);
            if (legR)
                legR.localRotation *= Quaternion.Euler(-swing * legSwing, 0f, 0f);
            if (armL)
                armL.localRotation *= Quaternion.Euler(-swing * armSwing - 110f * WindThrow * robot, 0f, 0f);
            if (armR)
                armR.localRotation *= Quaternion.Euler(swing * armSwing - 75f * AimCannon * robot, 0f, 0f);
            if (pelvis)
                pelvis.localPosition += Vector3.up * (Mathf.Abs(Mathf.Sin(_walkPhase)) * 0.06f * Walk * robot);

            // Машина едет: колёса крутятся.
            _wheelAngle += DriveSpeed / Mathf.Max(0.05f, WheelRadius) * Mathf.Rad2Deg * dt;
            if (wheels != null)
                foreach (var w in wheels)
                    if (w)
                        w.localRotation *= Quaternion.Euler(_wheelAngle, 0f, 0f);
        }

        void Apply()
        {
            if (parts == null || robotPositions == null || carPositions == null || parts.Length == 0)
                return;
            float ease = Mathf.SmoothStep(0f, 1f, _progress);
            CarAmount = Mathf.Lerp(_from, _to, ease);
            if (hitBox)
            {
                hitBox.center = Vector3.Lerp(robotBoxCenter, carBoxCenter, CarAmount);
                hitBox.size = Vector3.Lerp(robotBoxSize, carBoxSize, CarAmount);
            }
            if (aimPoint)
                aimPoint.localPosition = Vector3.up * Mathf.Lerp(robotAimHeight, carAimHeight, CarAmount);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (!part || i >= robotPositions.Length || i >= carPositions.Length)
                    continue;
                // Окно части: задержки — для сборки робота (ноги первыми); машина собирается в обратном порядке.
                float delay = delays != null && i < delays.Length ? delays[i] : 0f;
                if (_to > _from)
                    delay = Mathf.Max(0f, 1f - partSpan - delay);
                float local = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_progress - delay) / Mathf.Max(0.05f, partSpan)));
                float car = Mathf.Lerp(_from, _to, local);
                part.localPosition = Vector3.Lerp(robotPositions[i], carPositions[i], car);
                part.localRotation = Quaternion.Slerp(robotRotations[i], carRotations[i], car);
            }
        }
    }
}

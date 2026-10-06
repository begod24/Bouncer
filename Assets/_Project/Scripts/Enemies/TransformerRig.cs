using UnityEngine;

namespace Bouncer.Enemies
{
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

        public float CarAmount { get; private set; }
        public float Walk { get; set; }
        public float DriveSpeed { get; set; }
        public float AimCannon { get; set; }
        public float WindThrow { get; set; }
        public float WheelRadius { get; set; } = 0.3f;

        public bool IsTransforming => _progress < 1f;
        public bool IsCar => !IsTransforming && _to >= 0.5f;
        public bool TargetIsCar => _to >= 0.5f;
        public float Progress => _progress;

        public int PartCount => parts?.Length ?? 0;

        public void SetMode(bool car)
        {
            _from = _to = car ? 1f : 0f;
            _progress = 1f;
            Apply();
        }

        public void BeginTransform(bool car)
        {
            _from = CarAmount;
            _to = car ? 1f : 0f;
            _progress = 0f;
        }

        public void SetProgress(float progress) => _progress = Mathf.Clamp01(progress);

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

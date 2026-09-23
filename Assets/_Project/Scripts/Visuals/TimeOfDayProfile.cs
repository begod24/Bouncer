using UnityEngine;
using UnityEngine.Rendering;

namespace Bouncer.Visuals
{
    /// <summary>
    /// Одно время суток: палитра моделей, солнце, окружающий свет, туман, цвет неба и цветокоррекция.
    /// Между профилями плавно переходит <see cref="TimeOfDayController"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Bouncer/Time Of Day Profile", fileName = "TimeOfDay_")]
    public sealed class TimeOfDayProfile : ScriptableObject
    {
        [Header("Палитра")]
        [Tooltip("Текстура-палитра 8×8 с той же раскладкой ячеек, что T_Palette_Day")]
        public Texture2D palette;
        [Tooltip("Сила свечения окон и фонарей (текстура свечения задана в M_Palette)")]
        [Min(0f)] public float emissionStrength;

        [Header("Солнце")]
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1.2f;
        [Tooltip("Высота солнца над горизонтом, градусы")]
        [Range(0f, 90f)] public float sunElevation = 50f;
        [Tooltip("Азимут, градусы: 0 — свет падает на север (+Z), 90 — на восток")]
        [Range(0f, 360f)] public float sunAzimuth = 330f;
        [Range(0f, 1f)] public float shadowStrength = 0.75f;

        [Header("Окружающий свет (Trilight)")]
        public Color ambientSky = new(0.55f, 0.62f, 0.72f);
        public Color ambientEquator = new(0.45f, 0.47f, 0.48f);
        public Color ambientGround = new(0.3f, 0.29f, 0.26f);

        [Header("Туман и небо")]
        public Color fogColor = new(0.7f, 0.78f, 0.86f);
        [Min(0f)] public float fogStart = 35f;
        [Min(0f)] public float fogEnd = 110f;
        [Tooltip("Цвет фона камеры")]
        public Color skyColor = new(0.62f, 0.75f, 0.9f);

        [Header("Постобработка")]
        [Tooltip("Цветокоррекция этого времени суток (баланс белого, насыщенность, экспозиция)")]
        public VolumeProfile volumeProfile;
    }
}

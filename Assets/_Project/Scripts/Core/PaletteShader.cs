using UnityEngine;

namespace Bouncer.Core
{
    /// <summary>
    /// Свойства шейдера Bouncer/PaletteLit (Art/Shaders/PaletteLit.shader) — общий шейдер моделей из Blender.
    /// Цвет берётся из палитры, поэтому подкраска идёт не через _BaseColor (умножение на текстуру),
    /// а через _TintColor (a — сила) и _FlashColor (a — сила, поверх освещения).
    /// </summary>
    public static class PaletteShader
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int TintColor = Shader.PropertyToID("_TintColor");
        public static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

        // Глобальные значения, их выставляет контроллер времени суток.
        public static readonly int GlobalPaletteA = Shader.PropertyToID("_Bouncer_PaletteA");
        public static readonly int GlobalPaletteB = Shader.PropertyToID("_Bouncer_PaletteB");
        public static readonly int GlobalPaletteBlend = Shader.PropertyToID("_Bouncer_PaletteBlend");
        public static readonly int GlobalPaletteActive = Shader.PropertyToID("_Bouncer_PaletteActive");
        public static readonly int GlobalEmissionStrength = Shader.PropertyToID("_Bouncer_EmissionStrength");

        /// <summary>Материал на шейдере палитры (есть _TintColor/_FlashColor).</summary>
        public static bool Supports(Material material) => material && material.HasProperty(FlashColor);

        /// <summary>Цвет подкраски с силой в альфе — так её читает шейдер.</summary>
        public static Color Tint(Color color, float amount) => new(color.r, color.g, color.b, Mathf.Clamp01(amount));
    }
}

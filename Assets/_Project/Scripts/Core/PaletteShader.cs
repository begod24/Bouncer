using UnityEngine;

namespace Bouncer.Core
{
    public static class PaletteShader
    {
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int TintColor = Shader.PropertyToID("_TintColor");
        public static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
        public static readonly int FrostAmount = Shader.PropertyToID("_FrostAmount");
        public static readonly int GlowColor = Shader.PropertyToID("_GlowColor");

        public static readonly int GlobalPaletteA = Shader.PropertyToID("_Bouncer_PaletteA");
        public static readonly int GlobalPaletteB = Shader.PropertyToID("_Bouncer_PaletteB");
        public static readonly int GlobalPaletteBlend = Shader.PropertyToID("_Bouncer_PaletteBlend");
        public static readonly int GlobalPaletteActive = Shader.PropertyToID("_Bouncer_PaletteActive");
        public static readonly int GlobalEmissionStrength = Shader.PropertyToID("_Bouncer_EmissionStrength");

        public static bool Supports(Material material) => material && material.HasProperty(FlashColor);

        public static Color Tint(Color color, float amount) => new(color.r, color.g, color.b, Mathf.Clamp01(amount));
    }
}

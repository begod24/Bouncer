using UnityEngine;

namespace Bouncer.Core
{
    // Глобальные параметры шейдеров эффектов: контровой свет персонажей и мячей, тон частиц, мокрый асфальт.
    // Здесь значения по умолчанию (меню, выбор ребёнка, редактор); на аренах их каждый кадр уточняет TimeOfDayController.
    public static class FxGlobals
    {
        // Бит слоя отрисовки «Rim» (Project Settings → Tags and Layers → Rendering Layers, 7)
        public const int RimLayerBit = 7;
        public const uint RimLayerMask = 1u << RimLayerBit;

        public static readonly int RimStrength = Shader.PropertyToID("_Bouncer_RimStrength");
        public static readonly int RimColor = Shader.PropertyToID("_Bouncer_RimColor");
        public static readonly int FxTint = Shader.PropertyToID("_Bouncer_FxTint");
        public static readonly int Wet = Shader.PropertyToID("_Bouncer_Wet");

        public const float DefaultRimStrength = 0.32f;
        public static readonly Color DefaultRimColor = new(1f, 0.97f, 0.9f, 1f);

        public static void ResetDefaults()
        {
            Shader.SetGlobalFloat(RimStrength, DefaultRimStrength);
            Shader.SetGlobalColor(RimColor, DefaultRimColor);
            Shader.SetGlobalColor(FxTint, new Color(1f, 1f, 1f, 0f));
            Shader.SetGlobalFloat(Wet, 0f);
        }

        // Включает или выключает контровой свет у всех рендереров объекта
        public static void MarkRim(GameObject root, bool on = true)
        {
            if (root == null)
                return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                SetRim(renderer, on);
        }

        public static void SetRim(Renderer renderer, bool on)
        {
            if (renderer == null)
                return;
            uint mask = renderer.renderingLayerMask;
            uint want = on ? mask | RimLayerMask : mask & ~RimLayerMask;
            if (want != mask)
                renderer.renderingLayerMask = want;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RuntimeInit() => ResetDefaults();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void EditorInit() => ResetDefaults();
#endif
    }
}

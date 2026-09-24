// Надписи и рисунки на моделях из Blender (материал M_Decals): вывески, цифры на монетках, граффити.
// Цвет — из той же палитры, что у PaletteLit (UV0 → ячейка, глобальная смена времени суток работает),
// форма — из атласа-маски T_Decals (UV1). Теней не отбрасывает; подкраска и вспышка — как у PaletteLit.
Shader "Bouncer/PaletteDecal"
{
    Properties
    {
        [MainTexture] _BaseMap("Palette", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _EmissionMap("Emission Palette", 2D) = "black" {}
        _EmissionStrength("Emission Strength", Range(0, 8)) = 0
        [NoScaleOffset] _DecalMap("Decal Atlas (mask, R)", 2D) = "black" {}
        _Cutoff("Mask Cutoff", Range(0, 1)) = 0.5
        _TintColor("Tint (RGB) / Amount (A)", Color) = (1, 1, 1, 0)
        _FlashColor("Flash (RGB) / Amount (A)", Color) = (1, 1, 1, 0)

        [HideInInspector] _Surface("__surface", Float) = 0
        [HideInInspector] _Cull("__cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PaletteDecalVertex
            #pragma fragment PaletteDecalFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_IRRADIANCE
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "PaletteLitInput.hlsl"
            #include "PaletteDecalPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PaletteDecalDepthVertex
            #pragma fragment PaletteDecalDepthOnlyFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing

            #include "PaletteLitInput.hlsl"
            #include "PaletteDecalPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex PaletteDecalDepthVertex
            #pragma fragment PaletteDecalDepthNormalsFragment
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing

            #include "PaletteLitInput.hlsl"
            #include "PaletteDecalPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

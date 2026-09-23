// PS1-обработка кадра для Full Screen Pass Renderer Feature (After Rendering Post Processing):
// квантование цвета до _ColorLevels уровней на канал с упорядоченным дизерингом Байера 4×4.
// Пикселизацию даёт Render Scale URP-ассета с Upscaling Filter = Point, поэтому дизер ложится
// на «крупные» пиксели. Квантуем в sRGB, иначе в тенях будут грубые ступени.
Shader "Hidden/Bouncer/PS1Post"
{
    Properties
    {
        _ColorLevels("Color Levels", Range(4, 256)) = 32
        _DitherStrength("Dither Strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "PS1Dither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ColorLevels;
            float _DitherStrength;

            static const float kBayer4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                uint2 cell = uint2(input.positionCS.xy) & 3u;
                float threshold = (kBayer4[cell.y * 4u + cell.x] + 0.5) / 16.0 - 0.5;

                float3 display = LinearToSRGB(saturate(color.rgb));
                display = floor(display * _ColorLevels + 0.5 + threshold * _DitherStrength) / _ColorLevels;
                color.rgb = SRGBToLinear(saturate(display));
                return color;
            }
            ENDHLSL
        }
    }
}

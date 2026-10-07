Shader "Bouncer/Glow"
{
    // Светящееся пятно на квадрате (Quad): огонёк мигалки (повёрнут к камере) или отсвет на земле (лежит плашмя).
    // Ступенчатое мультяшное сияние или мягкое. Складывается со сценой. Цвет и яркость (A) — _BaseColor,
    // их меняет скрипт через MaterialPropertyBlock (SirenLights).
    Properties
    {
        [MainColor] _BaseColor("Color (A — brightness)", Color) = (1, 1, 1, 1)
        [Toggle] _Billboard("Face Camera", Float) = 1
        [Toggle] _Soft("Soft (no steps)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BouncerFx.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Billboard;
                half _Soft;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS;
                if (_Billboard > 0.5)
                {
                    float3 center = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                    float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = UNITY_MATRIX_V[1].xyz;
                    positionWS = center + (right * input.positionOS.x + up * input.positionOS.y) * scale;
                }
                else
                {
                    positionWS = TransformObjectToWorld(input.positionOS.xyz);
                }
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv - 0.5) * 2.0;
                half stepped = (1.0h - BouncerStep(0.95, r)) * 0.25h + (1.0h - BouncerStep(0.6, r)) * 0.35h
                             + (1.0h - BouncerStep(0.25, r)) * 0.4h;
                half soft = (half)pow(saturate(1.0 - r), 2.0);
                half glow = lerp(stepped, soft, _Soft) * _BaseColor.a;
                half3 color = BouncerFxFog(_BaseColor.rgb * glow, input.fogCoord, input.positionWS, 1.0h);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}

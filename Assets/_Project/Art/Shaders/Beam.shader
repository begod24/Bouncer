Shader "Bouncer/Beam"
{
    // Луч на LineRenderer (Texture Mode — Stretch): белое ядро, две ступени свечения цвета линии,
    // вдоль луча бегут яркие сгустки. U — вдоль луча, V — поперёк. Цвет и прозрачность — из цвета линии.
    Properties
    {
        _Core("Core Width", Range(0.05, 1)) = 0.3
        _CoreWhite("Core Whiteness", Range(0, 1)) = 0.85
        _Glow("Glow Opacity", Range(0, 1)) = 0.55
        _Repeat("Pulses along Beam", Float) = 10
        _Speed("Pulse Speed", Float) = 16
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Beam"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
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
                half _Core;
                half _CoreWhite;
                half _Glow;
                float _Repeat;
                float _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.color = input.color;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float v = abs(input.uv.y - 0.5) * 2.0;
                half core = 1.0h - BouncerStep(_Core, v);
                half inner = 1.0h - BouncerStep(lerp(_Core, 1.0, 0.45), v);
                half outer = 1.0h - BouncerStep(0.96, v);
                float pulse = BouncerNoise(float2(input.uv.x * _Repeat - _Time.y * _Speed, 0.5));
                half glow = (inner * 0.6h + (outer - inner) * 0.3h) * _Glow * (0.7h + 0.6h * (half)pulse);
                half alpha = max(core, glow) * input.color.a * saturate(input.uv.x * 40.0);
                half3 color = lerp(input.color.rgb, half3(1.0h, 1.0h, 1.0h), core * _CoreWhite);
                color = BouncerFxFog(color, input.fogCoord, input.positionWS, 0.0h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

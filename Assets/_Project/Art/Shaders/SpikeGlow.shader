Shader "Bouncer/SpikeGlow"
{
    Properties
    {
        _BaseColor("Base (at the ball)", Color) = (0.35, 0.03, 0.05, 1)
        _TipColor("Tip (hot)", Color) = (1, 0.82, 0.25, 1)
        _MidColor("Middle", Color) = (1, 0.25, 0.05, 1)
        _Inner("Inner Radius (object space)", Float) = 0.5
        _Outer("Tip Radius (object space)", Float) = 0.8
        _Pulse("Pulse Speed", Float) = 9
        _Glow("Glow Strength", Range(0, 3)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SpikeGlow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half4 _MidColor;
                float _Inner;
                float _Outer;
                float _Pulse;
                half _Glow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float radius : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.radius = length(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half tip = saturate((input.radius - _Inner) / max(1e-4, _Outer - _Inner));
                half wave = 0.5 + 0.5 * sin(_Time.y * _Pulse - input.radius * 18 + input.positionOS.y * 6);
                half3 color = lerp(_BaseColor.rgb, _MidColor.rgb, saturate(tip * 1.6));
                color = lerp(color, _TipColor.rgb, saturate(tip * tip * (0.6 + 0.6 * wave)));
                color *= 1 + _Glow * tip * tip * (0.5 + 0.5 * wave);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}

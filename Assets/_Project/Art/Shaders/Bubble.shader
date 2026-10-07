Shader "Bouncer/Bubble"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.5
        _RimAlpha("Rim Alpha", Range(0, 1)) = 0.85
        _CoreAlpha("Core Alpha", Range(0, 1)) = 0.08
        _Iridescence("Iridescence", Range(0, 1)) = 0.8
        _Swirl("Swirl Speed", Float) = 0.6
        _Highlight("Highlight", Range(0, 2)) = 1
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
            Name "Bubble"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _RimPower;
                half _RimAlpha;
                half _CoreAlpha;
                half _Iridescence;
                half _Swirl;
                half _Highlight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float3 positionOS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceNormalizeViewDir(position.positionWS);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half3 Rainbow(half t)
            {
                return saturate(half3(abs(t * 6 - 3) - 1, 2 - abs(t * 6 - 2), 2 - abs(t * 6 - 4)));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize(input.normalWS);
                float3 v = normalize(input.viewWS);
                half facing = saturate(dot(n, v));
                half rim = pow(1 - facing, _RimPower);
                half swirl = frac(facing * 1.7 + input.positionOS.y * 1.3 + _Time.y * _Swirl);
                half3 film = lerp(half3(1, 1, 1), Rainbow(swirl), _Iridescence);
                float3 lightDir = normalize(float3(-0.4, 0.8, -0.45));
                half spec = pow(saturate(dot(reflect(-v, n), lightDir)), 40) * _Highlight;
                half3 color = film * _BaseColor.rgb + spec;
                half alpha = saturate(lerp(_CoreAlpha, _RimAlpha, rim) + spec) * _BaseColor.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

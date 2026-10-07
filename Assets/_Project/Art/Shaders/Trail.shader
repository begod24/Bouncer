Shader "Bouncer/Trail"
{
    // След мяча: мультяшные «линии скорости» из T_TrailChalk. Прозрачность гаснет ступеньками, а не плавно.
    Properties
    {
        [MainTexture] _BaseMap("Strokes (U along the trail)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _Steps("Fade Steps", Range(1, 8)) = 3
        _Core("White Core", Range(0, 1)) = 0.35
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
            Name "Trail"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BouncerFx.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Steps;
                half _Core;
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
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half fade = ceil(saturate(input.color.a) * _Steps) / _Steps;
                // светлая сердцевина у самого мяча
                half core = (1.0h - smoothstep(0.0h, 0.35h, input.uv.x)) * (1.0h - smoothstep(0.08h, 0.2h, abs(input.uv.y - 0.5h)));
                half3 color = lerp(input.color.rgb * _BaseColor.rgb, half3(1.0h, 1.0h, 1.0h), core * _Core);
                half alpha = tex.a * fade * _BaseColor.a * step(0.001h, input.color.a);
                color = BouncerFxFog(color, input.fogCoord, input.positionWS, 0.0h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

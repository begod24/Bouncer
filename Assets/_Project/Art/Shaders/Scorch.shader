Shader "Bouncer/Scorch"
{
    // Выжженное пятно под огнём: чёрная клякса с рваным краем, по ней тлеют угли (сетка трещин).
    // Сначала середина раскалена, потом остывает, к концу пятно тает. Где светятся угли — там жжётся.
    // Частица лежит на земле (Horizontal Billboard), потоки UV + AgePercent + StableRandom.x (как у ToonFire).
    Properties
    {
        _SootColor("Soot (A — opacity)", Color) = (0.09, 0.06, 0.05, 0.7)
        _EmberColor("Embers", Color) = (1, 0.45, 0.1, 1)
        _HotColor("Hot Core", Color) = (1, 0.8, 0.32, 1)
        _CellScale("Ember Cells", Float) = 4.5
        _CoolBy("Cold at Age", Range(0.1, 1)) = 0.75
        _Lit("Time Of Day Tint (soot)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Scorch"
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
                half4 _SootColor;
                half4 _EmberColor;
                half4 _HotColor;
                float _CellScale;
                half _CoolBy;
                half _Lit;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 uv : TEXCOORD0;
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
                float2 p = (input.uv.xy - 0.5) * 2.0;
                float age = saturate(input.uv.z);
                float seed = input.uv.w * 50.0;
                float r = length(p);

                float edge = 0.7 + 0.24 * BouncerNoise(p * 1.7 + seed);
                half soot = BouncerStep(0.0, edge - r);

                // угли: клетки Вороного, светятся щели между ними
                float2 g = p * _CellScale + seed;
                float2 id = floor(g);
                float2 f = g - id;
                float d1 = 8.0;
                float d2 = 8.0;
                float2 nearest = 0;
                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 o = float2(i, j);
                        float2 c = o + float2(BouncerHash21(id + o), BouncerHash21(id + o + 17.1));
                        float d = length(c - f);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                            nearest = id + o;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                }
                float crack = d2 - d1;

                float heat = 1.0 - smoothstep(0.0, _CoolBy, age);
                heat *= 0.8 + 0.2 * sin(_Time.y * 7.0 + seed + nearest.x * 1.7 + nearest.y * 2.3);
                float inner = saturate(1.0 - r / max(edge, 1e-3));
                half ember = BouncerStep(0.0, (0.1 + 0.2 * heat) * inner - crack) * BouncerStep(0.08, heat) * soot;
                half hot = BouncerStep(0.6, inner * heat * 1.35) * soot;

                half3 color = _SootColor.rgb * BouncerFxTint(_Lit);
                color = lerp(color, _EmberColor.rgb, ember);
                color = lerp(color, _HotColor.rgb, hot);
                half glow = max(ember, hot);
                half alpha = soot * lerp(_SootColor.a, 1.0h, glow) * (1.0h - smoothstep(0.78, 1.0, age)) * input.color.a;
                color = BouncerFxFog(color * input.color.rgb, input.fogCoord, input.positionWS, 0.0h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}

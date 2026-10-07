Shader "Bouncer/Puddle"
{
    // Лужа в дождь: гладкая тёмная вода с бликами, по ней изредка бегут круги от капель, край мягкий и «дышит»:
    // разные бока лужи медленно то подтекают, то подсыхают. UV.x меша — 0 в центре, 1 на краю (строит Visuals/Puddle).
    // Сила кругов — глобальная _Bouncer_Wet.
    Properties
    {
        [MainColor] _BaseColor("Water", Color) = (0.09, 0.11, 0.15, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        _EdgeSoftness("Edge Softness", Range(0.01, 1)) = 0.3
        _RippleScale("Ripple Cell, m", Float) = 0.95
        _RippleSpeed("Ripples per Second", Float) = 0.75
        _RippleDensity("Cells with Drops", Range(0, 1)) = 0.45
        _RippleStrength("Ripple Bend", Range(0, 2)) = 0.4
        _RippleTint("Ripple Rings", Color) = (0.55, 0.65, 0.8, 0.14)
        _EdgeWobble("Edge Wobble", Range(0, 0.5)) = 0.2
        _EdgeWobbleSpeed("Edge Wobble Speed", Float) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Puddle"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BouncerFx.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _EdgeSoftness;
                float _RippleScale;
                float _RippleSpeed;
                half _RippleDensity;
                half _RippleStrength;
                half4 _RippleTint;
                half _EdgeWobble;
                float _EdgeWobbleSpeed;
            CBUFFER_END

            float _Bouncer_Wet;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
                float2 positionOS : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.positionOS = input.positionOS.xz;
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float3 Hash3(float2 p)
            {
                float3 q = float3(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)), dot(p, float2(419.2, 371.9)));
                return frac(sin(q) * 43758.5453);
            }

            // Круги от капель: в каждой клетке сетки по одному кругу со своим моментом.
            // Возвращает наклон поверхности (xz) и яркость колец.
            float2 Ripples(float2 xz, float time, out float rings)
            {
                float2 g = xz / _RippleScale;
                float2 id = floor(g);
                float2 f = g - id;
                float2 tilt = 0;
                rings = 0;
                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 cell = float2(i, j);
                        float3 h = Hash3(id + cell);
                        // капает не в каждую клетку: остальные стоят гладкими
                        float alive = step(frac(h.x * 13.1 + h.y * 7.7), _RippleDensity);
                        float phase = frac(time * _RippleSpeed * (0.7 + 0.6 * h.z) + h.x * 7.0);
                        float2 v = f - (cell + 0.2 + 0.6 * h.xy);
                        float d = length(v);
                        float x = (d - phase * 0.9) / 0.07;
                        float life = (1.0 - phase) * alive;
                        float bump = exp(-x * x);
                        tilt += v / max(d, 1e-3) * (sin(x * 2.2) * bump * life);
                        rings += bump * life * life;
                    }
                }
                return tilt;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float wet = saturate(_Bouncer_Wet);
                float rings;
                float2 tilt = Ripples(input.positionWS.xz, _Time.y, rings) * (_RippleStrength * wet);
                half3 normalWS = normalize(half3(-tilt.x, 1.0h, -tilt.y));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogCoord;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = _BaseColor.rgb;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1.0h;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb += _RippleTint.rgb * (saturate(rings) * _RippleTint.a * wet);
                color.rgb = BouncerFxFog(color.rgb, input.fogCoord, input.positionWS, 0.0h);
                // Край: три медленные волны по окружности, у каждой лужи свой сдвиг (от её места на арене)
                float2 origin = float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m23);
                float seed = dot(origin, float2(0.37, 0.71));
                float angle = atan2(input.positionOS.y, input.positionOS.x);
                float t = _Time.y * _EdgeWobbleSpeed;
                float lobes = sin(2.0 * angle + t * 1.3 + seed) * 0.5
                            + sin(3.0 * angle - t * 0.9 + seed * 2.1) * 0.3
                            + sin(5.0 * angle + t * 1.7 + seed * 0.7) * 0.2;
                float reach = 1.0 - _EdgeWobble * (0.5 + 0.5 * lobes);
                half edge = 1.0h - smoothstep(1.0h - _EdgeSoftness, 1.0h, (half)(input.uv.x / reach));
                return half4(color.rgb, edge * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}

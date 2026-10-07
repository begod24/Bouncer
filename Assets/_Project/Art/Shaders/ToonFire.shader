Shader "Bouncer/ToonFire"
{
    // Мультяшный язык пламени на частице: капля с острым верхом, край колышется, три ступени цвета
    // (красный край, оранжевая середина, жёлтое ядро). Без текстур. Частица отдаёт потоки UV, AgePercent
    // и StableRandom.x (их ставит EnemyFxBuilder). К концу жизни язык съёживается, а не тает.
    Properties
    {
        _OuterColor("Outer", Color) = (0.95, 0.24, 0.07, 1)
        _MidColor("Middle", Color) = (1, 0.56, 0.1, 1)
        _CoreColor("Core", Color) = (1, 0.92, 0.48, 1)
        _Wobble("Edge Wobble", Range(0, 1)) = 0.4
        _Speed("Flicker Speed", Float) = 5
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
            Name "ToonFire"
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
                half4 _OuterColor;
                half4 _MidColor;
                half4 _CoreColor;
                half _Wobble;
                float _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // xy — UV, z — возраст частицы 0..1, w — её случайное число
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

            // Капля пламени: больше нуля внутри, ноль на краю. Низ круглый, верх острый.
            // Без деления на ширину — у острия и у краёв квадрата нет разрывов, а с ними и полосок от сглаживания
            float Drop(float x, float y)
            {
                const float bottom = 0.24;
                float body = y < bottom
                    ? sqrt(saturate(1.0 - pow((bottom - y) / bottom, 2.0)))
                    : pow(saturate((1.0 - y) / (1.0 - bottom)), 0.85);
                // запас 0,04 — над остриём (ширина 0) на самой оси поле не дотягивает до края
                return 0.96 * body - abs(x) - 0.04;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float age = saturate(input.uv.z);
                float seed = input.uv.w * 37.0;
                float t = _Time.y * _Speed + seed;
                float y = input.uv.y;
                float x = (input.uv.x - 0.5) * 2.0;

                // колышется сильнее к верху, край в зубцах
                x += (BouncerNoise(float2(y * 2.5 - t, seed)) * 2.0 - 1.0) * _Wobble * y * 0.8;
                x *= 1.0 + 0.2 * (BouncerNoise(float2(y * 6.0 - t * 1.7, seed + 3.1)) - 0.5);

                // гаснущий язык съёживается: ниже и уже
                float height = lerp(1.0, 0.45, age);
                float width = 1.0 - age * age * 0.6;
                float outer = Drop(x / width, y / height);
                float mid = Drop(x / (width * 0.66), (y - 0.03) / (height * 0.74));
                float core = Drop(x / (width * 0.38), (y - 0.05) / (height * 0.46));

                half3 color = lerp(_OuterColor.rgb, _MidColor.rgb, BouncerStep(0.0, mid));
                color = lerp(color, _CoreColor.rgb, BouncerStep(0.0, core)) * input.color.rgb;
                color = BouncerFxFog(color, input.fogCoord, input.positionWS, 0.0h);
                return half4(color, BouncerStep(0.0, outer) * input.color.a);
            }
            ENDHLSL
        }
    }
}

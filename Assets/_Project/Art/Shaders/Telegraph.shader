Shader "Bouncer/Telegraph"
{
    // Заливка предупреждения на земле: к моменту удара заполняется до края.
    // Круг — от центра к краю, полоса — от начала (UV.y = 0) к концу. Всё задаёт GroundMarker через MaterialPropertyBlock.
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 0.32, 0.22, 0.9)
        _Progress("Progress", Range(0, 1)) = 0.5
        [Enum(Circle, 0, Line, 1)] _Mode("Shape", Float) = 0
        _Size("Radius (circle) / Width (line), m", Float) = 2
        _Length("Length (line), m", Float) = 4
        _Edge("Edge Width, m", Float) = 0.12
        _Fill("Fill Alpha", Range(0, 1)) = 0.3
        _Base("Base Alpha (not filled yet)", Range(0, 1)) = 0.1
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
            Name "Telegraph"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Progress;
                float _Mode;
                float _Size;
                float _Length;
                float _Edge;
                half _Fill;
                half _Base;
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
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                return output;
            }

            // 1 внутри [from, to] с мягким краем шириной aa
            float Band(float x, float from, float to, float aa)
            {
                return smoothstep(from - aa, from, x) * (1.0 - smoothstep(to, to + aa, x));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate(_Progress);
                float time = _Time.y;
                float inside, filled, front, edge, pattern;
                if (_Mode < 0.5)
                {
                    // круг: d в метрах от центра
                    float2 p = (input.uv * 2.0 - 1.0) * _Size;
                    float d = length(p);
                    float aa = max(fwidth(d), 1e-4);
                    float r = _Size;
                    inside = 1.0 - smoothstep(r - aa, r, d);
                    float fr = r * t;
                    filled = 1.0 - smoothstep(fr - aa, fr, d);
                    front = Band(d, fr - _Edge * 0.7, fr, aa) * step(0.02, t);
                    edge = Band(d, r - _Edge, r, aa);
                    // концентрические полоски бегут к центру — «стягивается»
                    pattern = step(0.5, frac(d / 0.55 + time * 1.3));
                }
                else
                {
                    // полоса: x поперёк (м от оси), y вдоль (м от начала)
                    float x = (input.uv.x - 0.5) * _Size;
                    float y = input.uv.y * _Length;
                    float ax = abs(x);
                    float half_w = _Size * 0.5;
                    float aa = max(fwidth(y), 1e-4);
                    float aax = max(fwidth(ax), 1e-4);
                    inside = (1.0 - smoothstep(half_w - aax, half_w, ax)) * Band(y, 0.0, _Length, aa);
                    float fy = _Length * t;
                    filled = 1.0 - smoothstep(fy - aa, fy, y);
                    front = Band(y, fy - _Edge * 0.8, fy, aa) * step(0.02, t);
                    edge = max(Band(ax, half_w - _Edge, half_w, aax), Band(y, _Length - _Edge, _Length, aa) * step(ax, half_w));
                    // шевроны бегут в сторону удара
                    pattern = step(0.5, frac((y - ax * 0.8) / 0.7 - time * 1.6));
                }

                half alpha = inside * (_Base * (0.6 + 0.4 * pattern));
                alpha = max(alpha, filled * inside * (_Fill * (0.75 + 0.25 * pattern)));
                alpha = max(alpha, front * inside * 0.85);
                alpha = max(alpha, edge * 0.95);
                // перед самым ударом — частое мигание
                float late = smoothstep(0.82, 0.9, t);
                alpha *= 1.0 + late * 0.45 * sin(time * 38.0);
                half3 color = _BaseColor.rgb + (front + edge * 0.35) * 0.25;
                return half4(color, saturate(alpha) * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}

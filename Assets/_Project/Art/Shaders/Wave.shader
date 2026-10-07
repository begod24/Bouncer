Shader "Bouncer/Wave"
{
    // Объёмная волна на земле: толстое кольцо с ярким фронтом и ступенчатым хвостом.
    // Лежит на квадрате (Quad, повёрнут лицом вверх); радиус фронта, толщину, прозрачность и цвет
    // каждый кадр ставит ExpandingRing через MaterialPropertyBlock (в долях половины квадрата).
    // Узор: 0 — ударная волна (пыльная), 1 — крик (волнистый фронт и эхо), 2 — барабан (два кольца, штрихи),
    // 3 — заморозка (зубцы-кристаллы и блёстки), 4 — взрыв (рваный огненный фронт, дым в хвосте).
    Properties
    {
        [Enum(Shock,0,Cry,1,Drum,2,Freeze,3,Blast,4)] _Pattern("Pattern", Float) = 0
        _FrontWhite("Front Whiteness", Range(0, 1)) = 0.6
        _TailShade("Tail Shade", Range(0, 1.5)) = 0.85
        _SmokeColor("Blast Smoke", Color) = (0.25, 0.2, 0.18, 1)
        _Lit("Time Of Day Tint", Range(0, 1)) = 0.4

        [HideInInspector] _Front("Front", Float) = 0.5
        [HideInInspector] _Thickness("Thickness", Float) = 0.2
        [HideInInspector] _Fade("Fade", Float) = 1
        [HideInInspector] _Tint("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-5"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "Wave"
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
                half _Pattern;
                half _FrontWhite;
                half _TailShade;
                half4 _SmokeColor;
                half _Lit;
                float _Front;
                float _Thickness;
                half _Fade;
                half4 _Tint;
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
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float r = length(p);
                float2 dir = p / max(r, 1e-4);
                float a = atan2(p.y, p.x) / 6.2831853;
                float th = max(_Thickness, 1e-3);
                int pattern = (int)round(_Pattern);

                // неровный фронт у каждого узора свой (функции угла с целым числом повторов — без шва)
                float rr = r;
                if (pattern == 0)
                    rr += (BouncerNoise(dir * 4.0 + 3.0) - 0.5) * th * 0.3;
                else if (pattern == 1)
                    rr += sin(a * 6.2831853 * 14.0 + _Time.y * 9.0) * th * 0.12;
                else if (pattern == 3)
                    rr += (abs(frac(a * 16.0) - 0.5) * 2.0 - 0.5) * th * 0.6;
                else if (pattern == 4)
                    rr += (BouncerNoise(dir * 3.0 + _Front * 5.0) - 0.5) * th * 0.8;

                // 0 — фронт, 1 — конец хвоста
                float s = (_Front - rr) / th;
                half band = BouncerStep(0.0, s) * (1.0h - BouncerStep(1.0, s));
                half front = 1.0h - BouncerStep(0.2, s);
                half tail = 0.85h - 0.35h * BouncerStep(0.45, s) - 0.28h * BouncerStep(0.75, s);
                half alpha = band * max(front, tail);

                half3 tint = _Tint.rgb;
                half3 tailColor = tint * _TailShade;
                half3 frontColor = lerp(tint, half3(1.0h, 1.0h, 1.0h), _FrontWhite);

                if (pattern == 0)
                {
                    // пыль: к хвосту рвётся на клочья
                    half keep = BouncerStep(s * 0.75, BouncerNoise(p * 7.0 + _Front * 3.0));
                    alpha *= max(front, keep);
                }
                else if (pattern == 1)
                {
                    // крик: тонкий фронт и два эха, между ними едва видно
                    half echo1 = BouncerStep(0.4, s) * (1.0h - BouncerStep(0.52, s));
                    half echo2 = BouncerStep(0.8, s) * (1.0h - BouncerStep(0.9, s));
                    half lines = max(1.0h - BouncerStep(0.14, s), max(echo1 * 0.75h, echo2 * 0.45h));
                    alpha = band * max(lines, 0.12h);
                }
                else if (pattern == 2)
                {
                    // барабан: сплошное кольцо и пунктир за ним
                    half ring = 1.0h - BouncerStep(0.3, s);
                    half dashes = BouncerStep(0.0, sin((a * 22.0 + _Front * 1.5) * 6.2831853));
                    half second = BouncerStep(0.55, s) * (1.0h - BouncerStep(0.8, s)) * dashes;
                    alpha = band * max(ring, second * 0.8h);
                }
                else if (pattern == 3)
                {
                    // заморозка: бледная заливка и блёстки
                    float2 cell = floor(p * 26.0);
                    half sparkle = step(0.93, BouncerHash21(cell)) * step(0.5, frac(_Time.y * 3.0 + BouncerHash21(cell + 5.3)));
                    alpha = band * max(front, max(tail * 0.6h, sparkle));
                    tailColor = lerp(tailColor, half3(1.0h, 1.0h, 1.0h), sparkle);
                }
                else
                {
                    // взрыв: жёлтый фронт, оранжевая середина, дым в хвосте
                    half smoke = BouncerStep(0.6, s);
                    tailColor = lerp(tailColor, _SmokeColor.rgb, smoke);
                    half keep = BouncerStep(s * 0.65, BouncerNoise(p * 5.0 - _Front * 2.0));
                    alpha *= max(front, keep);
                }

                half3 color = lerp(tailColor, frontColor, front) * BouncerFxTint(_Lit);
                color = BouncerFxFog(color, input.fogCoord, input.positionWS, 0.0h);
                return half4(color, saturate(alpha) * _Tint.a * _Fade);
            }
            ENDHLSL
        }
    }
}

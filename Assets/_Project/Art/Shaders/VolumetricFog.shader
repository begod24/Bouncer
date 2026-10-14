Shader "Hidden/Bouncer/VolumetricFog"
{
    // Вариант B тумана: рэймарч от камеры до поверхности по тому же полю плотности, что и вариант A (BouncerFogCore.hlsl).
    // Плотность накапливается вдоль луча, поэтому клубы имеют толщину, а фонари светят объёмными ореолами.
    Properties
    {
        _Sigma("Extinction (1/m per unit density)", Range(0.2, 6)) = 1.0
        _MaxDistance("Max Ray Distance, m", Range(10, 120)) = 55
        _Steps("Steps", Range(8, 48)) = 32
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Blend Off
        Cull Off

        Pass
        {
            Name "VolumetricFog"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "BouncerFogCore.hlsl"

            float _Sigma;
            float _MaxDistance;
            float _Steps;

            float InterleavedGradient(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                if (_Bouncer_VolFog < 0.5 || _Bouncer_FogParams.z <= 0.001)
                    return scene;

                float depth = SampleSceneDepth(uv);
                float3 world = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
                float3 cam = _WorldSpaceCameraPos;
                float3 delta = world - cam;
                float dist = length(delta);
                float3 dir = delta / max(dist, 1e-4);
                #if UNITY_REVERSED_Z
                    if (depth <= 1e-5) dist = _MaxDistance;
                #else
                    if (depth >= 1.0 - 1e-5) dist = _MaxDistance;
                #endif
                dist = min(dist, _MaxDistance);

                int steps = (int)_Steps;
                float dt = dist / steps;
                float jitter = InterleavedGradient(input.positionCS.xy);
                float transmittance = 1.0;
                float3 scatter = 0.0;
                for (int i = 0; i < steps; i++)
                {
                    float3 p = cam + dir * ((i + jitter) * dt);
                    half4 fog = BouncerFogSampleRaw(p, 0.0h);
                    float extinction = fog.a * _Sigma;
                    float step = exp(-extinction * dt);
                    scatter += transmittance * (1.0 - step) * fog.rgb;
                    transmittance *= step;
                }

                // потолок: даже в самой гуще виден хотя бы (1 - cap) сцены, как и в варианте A
                float cap = saturate(_Bouncer_FogShape.z * _Bouncer_FogParams.z);
                float keep = max(transmittance, 1.0 - cap);
                float gain = (1.0 - keep) / max(1.0 - transmittance, 1e-4);
                half3 color = scene.rgb * keep + scatter * gain;
                return half4(color, scene.a);
            }
            ENDHLSL
        }
    }
}

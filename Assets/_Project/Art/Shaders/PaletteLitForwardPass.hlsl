#ifndef BOUNCER_PALETTE_LIT_FORWARD_PASS_INCLUDED
#define BOUNCER_PALETTE_LIT_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS         : POSITION;
    float3 normalOS           : NORMAL;
    float2 texcoord           : TEXCOORD0;
    float2 staticLightmapUV   : TEXCOORD1;
    float2 dynamicLightmapUV  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                  : TEXCOORD0;
    float3 positionWS          : TEXCOORD1;
    half3  normalWS            : TEXCOORD2;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight : TEXCOORD5;
#else
    half  fogFactor               : TEXCOORD5;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord         : TEXCOORD6;
#endif

    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);

#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV   : TEXCOORD8;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion      : TEXCOORD9;
#endif

    float4 positionCS          : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings input, out InputData inputData)
{
    inputData = (InputData)0;

    inputData.positionWS = input.positionWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif

    inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
    inputData.viewDirectionWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(inputData.positionWS));

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

#if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
    inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
    #endif
    #if defined(LIGHTMAP_ON)
    inputData.staticLightmapUV = input.staticLightmapUV;
    #else
    inputData.vertexSH = input.vertexSH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
    inputData.probeOcclusion = input.probeOcclusion;
    #endif
#endif
}

void InitializeBakedGIData(Varyings input, inout InputData inputData)
{
#if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, input.positionCS.xy);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

Varyings PaletteLitVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

#if defined(_FOG_FRAGMENT)
    half fogFactor = 0;
#else
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    return output;
}

float FrostHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float FrostNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(FrostHash(i), FrostHash(i + float3(1, 0, 0)), f.x),
                     lerp(FrostHash(i + float3(0, 1, 0)), FrostHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(FrostHash(i + float3(0, 0, 1)), FrostHash(i + float3(1, 0, 1)), f.x),
                     lerp(FrostHash(i + float3(0, 1, 1)), FrostHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

// Ледяная корка: голубые грани трёх оттенков с белыми трещинами, иней сверху, холодный ободок и блёстки
half3 ApplyFrost(half3 color, float3 positionWS, half3 n, half ndv, half amount)
{
    float ice = FrostNoise(positionWS * 4.5) * 3.0;
    half facet = (half)(floor(ice) / 3.0);
    float edge = min(frac(ice), 1.0 - frac(ice));
    half crack = (half)(1.0 - saturate(edge / max(fwidth(ice) * 1.2, 1e-4)));
    // своя окраска врага просвечивает сквозь лёд — его можно узнать
    half3 iceColor = half3(0.45h, 0.74h, 1.0h) * (0.7h + 0.4h * facet);
    half3 frozen = lerp(color, color * 0.5h + iceColor * 0.55h, 0.75h);
    frozen += half3(0.8h, 0.92h, 1.0h) * (saturate(n.y * 1.6h - 0.4h) * 0.22h);
    frozen = lerp(frozen, half3(0.92h, 0.97h, 1.0h), crack * 0.5h);
    frozen += half3(0.5h, 0.82h, 1.0h) * ((half)pow(1.0h - ndv, 2.5h) * 0.7h);
    float3 cell = floor(positionWS * 14.0);
    float h = FrostHash(cell);
    float3 local = frac(positionWS * 14.0) - 0.5;
    half sparkle = (half)(step(0.92, h) * step(length(local), 0.22) * step(0.4, sin(_Time.y * 6.0 + h * 40.0)));
    frozen += sparkle * 0.9h;
    return lerp(color, frozen, amount);
}

void PaletteLitFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    half3 albedo = SamplePalette(input.uv) * _BaseColor.rgb;
    albedo = lerp(albedo, _TintColor.rgb, _TintColor.a);

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.alpha = 1.0h;
    surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
    surfaceData.occlusion = 1.0h;
    surfaceData.emission = SamplePaletteEmission(input.uv);

    InputData inputData;
    InitializeInputData(input, inputData);
    SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));
    InitializeBakedGIData(input, inputData);

    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
    half3 n = inputData.normalWS;
    half ndv = saturate(dot(n, inputData.viewDirectionWS));
    // ребята, враги и мячи (слой Rim) в тумане остаются читаемыми
    half fogResist = 0.0h;
    if ((GetMeshRenderingLayer() & BOUNCER_RIM_LAYER) != 0u)
    {
        fogResist = (half)_Bouncer_FogShape.w;
        // мультяшный ободок по краю силуэта, чуть сильнее сверху
        half rim = smoothstep(0.6h, 0.85h, 1.0h - ndv) * saturate(n.y * 0.5h + 0.75h);
        color.rgb += _Bouncer_RimColor.rgb * (rim * (half)_Bouncer_RimStrength);
    }
    else if (_Bouncer_Wet > 0.0)
    {
        // мокрые горизонтальные поверхности: темнее, блик солнца и отсвет неба по краю
        half wet = (half)_Bouncer_Wet * saturate(n.y * 3.0h - 2.0h);
        Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
        half3 h = SafeNormalize(mainLight.direction + inputData.viewDirectionWS);
        half spec = pow(saturate(dot(n, h)), 48.0h) * mainLight.shadowAttenuation;
        half sheen = pow(1.0h - ndv, 4.0h);
        color.rgb *= 1.0h - 0.28h * wet;
        color.rgb += wet * (mainLight.color * (spec * 0.55h) + _GlossyEnvironmentColor.rgb * (sheen * 0.35h));
    }
    if (_FrostAmount > 0.001h)
        color.rgb = ApplyFrost(color.rgb, input.positionWS, n, ndv, _FrostAmount);
    if (_GlowColor.a > 0.001h)
    {
        // элита: цветной пульсирующий контур по краю силуэта
        half contour = smoothstep(0.3h, 0.75h, 1.0h - ndv);
        half pulse = 0.7h + 0.3h * (half)sin(_Time.y * 5.0);
        color.rgb = lerp(color.rgb, _GlowColor.rgb * 1.6h, saturate(contour * pulse * _GlowColor.a));
    }
    color.rgb = lerp(color.rgb, _FlashColor.rgb, _FlashColor.a);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.rgb = MixRadialFog(color.rgb, input.positionWS, fogResist);
    color.a = 1.0h;
    outColor = color;

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif

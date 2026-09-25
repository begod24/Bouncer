#ifndef BOUNCER_PALETTE_LIT_INPUT_INCLUDED
#define BOUNCER_PALETTE_LIT_INPUT_INCLUDED

// Входные данные Bouncer/PaletteLit. Один и тот же UnityPerMaterial во всех проходах — для SRP Batcher.
// Имена _BaseMap/_BaseColor/_Cutoff совпадают с URP, поэтому тени и глубина берутся из стандартных проходов URP.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    half4 _TintColor;     // rgb — цвет, a — сила подкраски (серия попаданий, состояние мяча)
    half4 _FlashColor;    // rgb — цвет, a — сила вспышки поверх освещения
    half _EmissionStrength;
    half _Cutoff;
    half _Surface;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

// Глобальная палитра от TimeOfDayController. Пока контроллер выключен (_Bouncer_PaletteActive = 0),
// используется палитра материала — модели корректно выглядят и в сценах без смены времени суток.
TEXTURE2D(_Bouncer_PaletteA);
TEXTURE2D(_Bouncer_PaletteB);
float _Bouncer_PaletteActive;
float _Bouncer_PaletteBlend;
float _Bouncer_EmissionStrength;

// Каждая грань лежит UV внутри одной ячейки палитры, поэтому только точечная выборка.
half3 SamplePalette(float2 uv)
{
    half3 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_PointClamp, uv).rgb;
    if (_Bouncer_PaletteActive > 0.5)
    {
        half3 a = SAMPLE_TEXTURE2D(_Bouncer_PaletteA, sampler_PointClamp, uv).rgb;
        half3 b = SAMPLE_TEXTURE2D(_Bouncer_PaletteB, sampler_PointClamp, uv).rgb;
        color = lerp(a, b, (half)_Bouncer_PaletteBlend);
    }
    return color;
}

half3 SamplePaletteEmission(float2 uv)
{
    return SAMPLE_TEXTURE2D(_EmissionMap, sampler_PointClamp, uv).rgb * (half)(_EmissionStrength + _Bouncer_EmissionStrength);
}

// Туман вокруг игрока (погода «Туман»): густеет с расстоянием от игрока по земле, а не от камеры —
// камера висит в двадцати метрах, и обычный туман закрывал бы не то. Задаёт WeatherController;
// при силе 0 ничего не делает. xyz центра — позиция игрока; параметры: x — начало, y — конец, z — сила.
float4 _Bouncer_FogCenter;
float4 _Bouncer_FogParams;
half4 _Bouncer_FogColor;

half3 MixRadialFog(half3 color, float3 positionWS)
{
    if (_Bouncer_FogParams.z <= 0.0)
        return color;
    float distance = length(positionWS.xz - _Bouncer_FogCenter.xz);
    float fog = smoothstep(_Bouncer_FogParams.x, _Bouncer_FogParams.y, distance) * _Bouncer_FogParams.z;
    return lerp(color, _Bouncer_FogColor.rgb, (half)fog);
}

#endif

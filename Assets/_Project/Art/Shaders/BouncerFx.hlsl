#ifndef BOUNCER_FX_INCLUDED
#define BOUNCER_FX_INCLUDED

// Общее для эффектов (частицы, след мяча): туман вокруг игрока и тон времени суток.
// Глобальные переменные те же, что у PaletteLit; ставят WeatherController и TimeOfDayController.

float4 _Bouncer_FogCenter;
float4 _Bouncer_FogParams;
half4 _Bouncer_FogColor;
// RGB — во что красить «освещённые» эффекты (ночью темнее и синее), A — насколько. 0 — без тона
half4 _Bouncer_FxTint;

half BouncerRadialFog(float3 positionWS)
{
    if (_Bouncer_FogParams.z <= 0.0)
        return 0.0h;
    float distance = length(positionWS.xz - _Bouncer_FogCenter.xz);
    return (half)(smoothstep(_Bouncer_FogParams.x, _Bouncer_FogParams.y, distance) * _Bouncer_FogParams.z);
}

half3 BouncerFxTint(half lit)
{
    half3 tint = lerp(half3(1.0h, 1.0h, 1.0h), _Bouncer_FxTint.rgb, _Bouncer_FxTint.a);
    return lerp(half3(1.0h, 1.0h, 1.0h), tint, lit);
}

// Процедурный шум для огня, углей и волн: хэш и сглаженный шум по сетке
float BouncerHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float BouncerNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = BouncerHash21(i);
    float b = BouncerHash21(i + float2(1.0, 0.0));
    float c = BouncerHash21(i + float2(0.0, 1.0));
    float d = BouncerHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Ступенька с мягким краем в один пиксель — мультяшная граница без лесенки
half BouncerStep(float edge, float x)
{
    float w = max(fwidth(x), 1e-4);
    return (half)saturate((x - edge) / w + 0.5);
}

// Обычная прозрачность: в туман уходим цветом тумана. Аддитивная: просто гаснем.
half3 BouncerFxFog(half3 color, float fogCoord, float3 positionWS, half additive)
{
    half3 fogged = MixFog(color, fogCoord);
    half3 dimmed = MixFogColor(color, half3(0.0h, 0.0h, 0.0h), fogCoord);
    color = lerp(fogged, dimmed, additive);
    half radial = BouncerRadialFog(positionWS);
    return lerp(color, lerp(_Bouncer_FogColor.rgb, half3(0.0h, 0.0h, 0.0h), additive), radial);
}

#endif

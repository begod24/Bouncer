#ifndef BOUNCER_FX_INCLUDED
#define BOUNCER_FX_INCLUDED

// Общее для эффектов (частицы, след мяча): туман погоды и тон времени суток.
// Туман — BouncerFogCore.hlsl (там же шум); глобальные переменные ставят WeatherController и TimeOfDayController.
#include "BouncerFogCore.hlsl"

// RGB — во что красить «освещённые» эффекты (ночью темнее и синее), A — насколько. 0 — без тона
half4 _Bouncer_FxTint;

// Частицы, следы и вспышки тонут в тумане слабее окружения, чтобы удары и телеграфы читались
#define BOUNCER_FX_FOG_RESIST 0.45h

half3 BouncerFxTint(half lit)
{
    half3 tint = lerp(half3(1.0h, 1.0h, 1.0h), _Bouncer_FxTint.rgb, _Bouncer_FxTint.a);
    return lerp(half3(1.0h, 1.0h, 1.0h), tint, lit);
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
    half4 fog = BouncerFogSample(positionWS, BOUNCER_FX_FOG_RESIST);
    return lerp(color, lerp(fog.rgb, half3(0.0h, 0.0h, 0.0h), additive), fog.a);
}

#endif

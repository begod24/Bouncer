#ifndef BOUNCER_FOG_CORE_INCLUDED
#define BOUNCER_FOG_CORE_INCLUDED

// Туман погоды «Туман»: стелется по земле, клубится и плывёт, вокруг игрока чище, ребят и мячи почти не скрывает.
// Один расчёт на все шейдеры (палитра, декали, частицы, следы). Глобальные переменные ставит WeatherController.

float4 _Bouncer_FogCenter;   // xyz — центр чистого пузыря (игрок)
float4 _Bouncer_FogParams;   // x, y — радиус чистого пузыря и где он кончается, м; z — сила тумана 0..1
half4 _Bouncer_FogColor;     // цвет тумана (от арены и времени суток)
float4 _Bouncer_FogShape;    // x — спад плотности с высотой, 1/м; y — масштаб шума, 1/м; z — потолок плотности 0..1; w — насколько туман не берёт актёров 0..1
float4 _Bouncer_FogWind;     // xy — дрейф большого слоя, zw — малого (в единицах шума)

#define BOUNCER_FOG_LAMPS 6
float4 _Bouncer_FogLamps[BOUNCER_FOG_LAMPS];     // xyz — голова фонаря, w — радиус ореола, м
half4 _Bouncer_FogLampColors[BOUNCER_FOG_LAMPS]; // rgb — цвет ореола с учётом яркости фонаря, a — насколько ореол сгущает туман
float _Bouncer_FogLampCount;

// Процедурный шум: хэш и сглаженный шум по сетке (огонь, угли, волны, туман)
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

// Клубы 0..1: большой слой плывёт в одну сторону, малый (повёрнут и вытянут в космы) в другую,
// а медленный шум-«ветер» сминает оба — клубы рвутся, сливаются и тянутся. Края чёткие, как в мультике: между клубами просветы
float BouncerFogBanks(float2 xz)
{
    float2 p = xz * _Bouncer_FogShape.y;
    float swirl = BouncerNoise(p * 0.6 + _Bouncer_FogWind.zw + 17.3) - 0.5;
    float big = BouncerNoise(p + _Bouncer_FogWind.xy + swirl * 1.8);
    float2 q = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8) * float2(1.5, 2.8) + _Bouncer_FogWind.zw;
    float small = BouncerNoise(q - swirl * 1.2);
    return smoothstep(0.3, 0.7, big * 0.62 + small * 0.38);
}

// rgb — цвет тумана в этой точке (со светом фонарей), a — сколько тумана между камерой и точкой (0 — чисто).
// resist: 0 — окружение (тонет полностью), 0..1 — актёры и эффекты (туман берёт их на столько слабее)
half4 BouncerFogSample(float3 positionWS, half resist)
{
    half4 fog = half4(_Bouncer_FogColor.rgb, 0.0h);
    if (_Bouncer_FogParams.z <= 0.001)
        return fog;

    float banks = BouncerFogBanks(positionWS.xz);
    // экспоненциальный туман по высоте: у асфальта густо, на уровне головы вдвое реже, над крышами нет совсем
    float height = exp(-max(positionWS.y, 0.0) * _Bouncer_FogShape.x);
    float distance = length(positionWS.xz - _Bouncer_FogCenter.xz);
    // вокруг игрока почти чисто (но не стерильно), дальше туман густеет до потолка, а не до сплошной стены
    float bubble = lerp(0.18, 1.0, smoothstep(_Bouncer_FogParams.x, _Bouncer_FogParams.y, distance));
    float amount = lerp(0.12, 1.0, banks) * height * bubble;

    // у клубов светлая тёплая сердцевина, а редкая дымка между ними холоднее и темнее: туман читается объёмом, а не серой плёнкой
    half3 color = _Bouncer_FogColor.rgb * lerp(half3(0.74h, 0.8h, 0.9h), half3(1.18h, 1.14h, 1.08h), (half)banks);
    float glow = 0.0;
    for (int i = 0; i < (int)_Bouncer_FogLampCount; i++)
    {
        float3 delta = positionWS - _Bouncer_FogLamps[i].xyz;
        delta.y *= 0.6;
        float k = saturate(1.0 - length(delta) / _Bouncer_FogLamps[i].w);
        k *= k;
        color += _Bouncer_FogLampColors[i].rgb * (half)k;
        glow += k * _Bouncer_FogLampColors[i].a;
    }
    // у фонаря туман видно и там, где клубы редкие: ореол
    amount = saturate(amount + glow * height);

    fog.rgb = color;
    fog.a = (half)(amount * _Bouncer_FogShape.z * _Bouncer_FogParams.z * (1.0h - resist));
    return fog;
}

half3 BouncerFogMix(half3 color, float3 positionWS, half resist)
{
    half4 fog = BouncerFogSample(positionWS, resist);
    return lerp(color, fog.rgb, fog.a);
}

#endif

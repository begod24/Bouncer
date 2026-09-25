#!/usr/bin/env python3
"""
Bouncer: синтез звуковых эффектов (без сэмплов — только генераторы, шум и фильтры).

Запуск из корня репозитория:  python3 Tools/sfx.py
Нужен numpy. Пишет WAV 44.1 кГц, 16 бит, моно в Assets/_Project/Audio/SFX.
Сиды фиксированы: повторный запуск даёт те же звуки. Громкость и разброс высоты — в Unity (SoundBank).
"""
import os
import wave

import numpy as np

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Project", "Audio", "SFX")
RNG = np.random.default_rng(7)


# ---------------------------------------------------------------- генераторы

def osc(freq, n, shape="sine"):
    f = np.full(n, freq, dtype=np.float64) if np.isscalar(freq) else freq[:n]
    ph = 2 * np.pi * np.cumsum(f) / SR
    if shape == "sine":
        return np.sin(ph)
    if shape == "square":
        return np.sign(np.sin(ph))
    if shape == "saw":
        return 2.0 * ((ph / (2 * np.pi)) % 1.0) - 1.0
    if shape == "triangle":
        return 2.0 * np.abs(2.0 * ((ph / (2 * np.pi)) % 1.0) - 1.0) - 1.0
    raise ValueError(shape)


def sweep(f0, f1, n, curve="exp"):
    x = np.linspace(0.0, 1.0, n)
    if curve == "exp":
        return f0 * (f1 / f0) ** x
    return f0 + (f1 - f0) * x


def noise(n):
    return RNG.uniform(-1.0, 1.0, n)


def decay(n, seconds):
    return np.exp(-np.arange(n) / SR / max(1e-4, seconds))


def attack_decay(n, attack, seconds):
    t = np.arange(n) / SR
    rise = np.clip(t / max(1e-4, attack), 0.0, 1.0)
    return rise * np.exp(-np.maximum(0.0, t - attack) / max(1e-4, seconds))


def spectral(x, lo=None, hi=None, soft=0.15):
    """Фильтр через FFT: пропускает полосу lo..hi Гц с мягкими краями."""
    spec = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(len(x), 1.0 / SR)
    gain = np.ones_like(freqs)
    if lo:
        gain *= 1.0 / (1.0 + np.exp(-(freqs - lo) / (lo * soft)))
    if hi:
        gain *= 1.0 / (1.0 + np.exp((freqs - hi) / (hi * soft)))
    return np.fft.irfft(spec * gain, len(x))


def band_noise(n, center, width):
    """Узкополосный шум вокруг center (может меняться во времени): НЧ-шум, умноженный на несущую."""
    base = spectral(noise(n), hi=width)
    base /= max(1e-6, np.abs(base).max())
    return base * osc(center, n)


def bell(freq, duration, bright=1.0):
    """Колокольчик: негармонические обертоны с разным затуханием."""
    n = int(SR * duration)
    out = np.zeros(n)
    for ratio, amp, dec in ((1.0, 1.0, 0.9), (2.76, 0.45 * bright, 0.35), (5.4, 0.25 * bright, 0.18), (8.93, 0.12 * bright, 0.08)):
        out += amp * osc(freq * ratio, n) * decay(n, duration * dec)
    return out * attack_decay(n, 0.002, duration)


def pluck(freq, duration, shape="triangle"):
    n = int(SR * duration)
    return osc(freq, n, shape) * attack_decay(n, 0.003, duration * 0.35)


def place(dest, src, at):
    i = int(at * SR)
    end = min(len(dest), i + len(src))
    dest[i:end] += src[: end - i]
    return dest


def pad(duration):
    return np.zeros(int(SR * duration))


def note(name):
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + (octave - 4) * 12) / 12)


def finish(x, peak=0.89, fade=0.01):
    x = x - np.mean(x)
    f = int(SR * fade)
    if f > 0 and len(x) > f:
        x[-f:] *= np.linspace(1.0, 0.0, f)
    m = np.abs(x).max()
    return x * (peak / m) if m > 0 else x


def write(name, x):
    os.makedirs(OUT, exist_ok=True)
    data = (np.clip(finish(x), -1.0, 1.0) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", name, f"{len(data) / SR:.2f}s")


# ---------------------------------------------------------------- игрок и мяч

def whoosh(duration, f0, f1, width=900.0, swell=0.4):
    n = int(SR * duration)
    t = np.linspace(0, 1, n)
    env = np.sin(np.pi * np.clip(t / swell, 0, 1) * 0.5) * np.exp(-np.maximum(0, t - swell) * 7.0)
    return band_noise(n, sweep(f0, f1, n), width) * env


def thump(duration, f0, f1, dec):
    n = int(SR * duration)
    return osc(sweep(f0, f1, n), n) * attack_decay(n, 0.002, dec)


def click(duration=0.012, lo=2000):
    n = int(SR * duration)
    return spectral(noise(n), lo=lo) * decay(n, duration * 0.3)


def sfx_player():
    write("Throw", whoosh(0.22, 500, 2200))
    write("ThrowCharged", whoosh(0.3, 350, 2600, 1200, 0.3) + 0.8 * place(pad(0.3), thump(0.12, 110, 60, 0.05), 0))
    candle = whoosh(0.35, 400, 2400, 1000, 0.3)
    for i, f in enumerate((1760, 2349, 2794)):
        place(candle, 0.35 * pluck(f, 0.15, "sine"), 0.04 + i * 0.05)
    write("ThrowCandle", candle)

    n = int(SR * 0.12)
    slap = spectral(noise(n), lo=300, hi=3500) * attack_decay(n, 0.001, 0.025) + 0.7 * thump(0.12, 190, 120, 0.03)
    write("Catch", slap)
    candle_catch = pad(0.45)
    place(candle_catch, slap, 0)
    for i, name in enumerate(("C6", "E6", "G6")):
        place(candle_catch, 0.5 * bell(note(name), 0.3, 0.6), 0.05 + i * 0.07)
    write("CatchCandle", candle_catch)

    miss = whoosh(0.15, 1500, 800, 1500, 0.2) * 0.6
    write("CatchMiss", miss)

    n = int(SR * 0.1)
    blip = osc(sweep(480, 980, n), n, "triangle") * attack_decay(n, 0.002, 0.03)
    write("Pickup", blip + 0.25 * place(pad(0.1), click(0.008, 3000), 0))

    write("Dash", whoosh(0.18, 900, 3500, 2000, 0.15))

    n = int(SR * 0.35)
    oof = spectral(osc(sweep(230, 130, n), n, "saw"), hi=900) * attack_decay(n, 0.01, 0.12)
    oof += 0.6 * place(pad(0.35), thump(0.15, 140, 70, 0.05), 0)
    write("PlayerHurt", oof)

    n = int(SR * 0.9)
    slide = osc(sweep(950, 180, n) * (1 + 0.02 * np.sin(np.arange(n) / SR * 2 * np.pi * 7)), n) * attack_decay(n, 0.01, 0.5)
    write("PlayerKnockedOut", slide + 0.5 * place(pad(0.9), thump(0.2, 120, 50, 0.08), 0.65))

    boing = thump(0.14, 320, 190, 0.05)
    write("BallWall", boing + 0.35 * place(pad(0.14), click(0.01, 1500), 0))

    n = int(SR * 0.45)
    thud = thump(0.45, 75, 42, 0.14) + 0.6 * spectral(noise(n), hi=500) * attack_decay(n, 0.002, 0.07)
    write("AreaThud", thud)

    n = int(SR * 0.4)
    whack = spectral(noise(n), lo=150, hi=2200) * attack_decay(n, 0.001, 0.04) + 0.8 * thump(0.4, 420, 380, 0.06)
    creak = osc(700 + 60 * np.sin(np.arange(n) / SR * 2 * np.pi * 23), n, "saw") * attack_decay(n, 0.05, 0.12)
    write("SwingBat", whack + 0.25 * spectral(creak, lo=400, hi=2500))


# ---------------------------------------------------------------- враги

def sfx_enemies():
    write("EnemyHit", thump(0.14, 250, 160, 0.045) + 0.4 * place(pad(0.14), click(0.01, 1800), 0))
    n = int(SR * 0.22)
    write("EnemyHitStrong", thump(0.22, 180, 85, 0.07) + 0.5 * spectral(noise(n), hi=1500) * attack_decay(n, 0.001, 0.03))

    # Неваляшка: мягкий звон «динь-дон», как у настоящей игрушки, когда она качается.
    chime = pad(1.0)
    place(chime, bell(note("A5"), 0.8, 0.8), 0.0)
    place(chime, 0.8 * bell(note("E5"), 0.8, 0.8), 0.14)
    write("RolyPolyChime", chime)

    pop = pad(0.6)
    n = int(SR * 0.2)
    place(pop, spectral(noise(n), lo=200, hi=4000) * attack_decay(n, 0.001, 0.03), 0)
    place(pop, thump(0.25, 420, 80, 0.08), 0)
    place(pop, 0.5 * bell(note("D#5"), 0.5, 1.2) + 0.4 * bell(note("A5"), 0.5, 1.2), 0.03)
    write("RolyPolyPop", pop)

    # Пупс: писк резиновой игрушки — носовой тон с изгибом высоты.
    n = int(SR * 0.2)
    t = np.linspace(0, 1, n)
    freq = 1300 + 700 * np.sin(np.pi * t) + 40 * np.sin(np.arange(n) / SR * 2 * np.pi * 35)
    squeak = spectral(osc(freq, n, "square"), lo=700, hi=4200) * attack_decay(n, 0.01, 0.1)
    write("PupsikSqueak", squeak)
    n = int(SR * 0.3)
    t = np.linspace(0, 1, n)
    squeal = spectral(osc(1800 * (1 - 0.6 * t), n, "square"), lo=500, hi=4000) * attack_decay(n, 0.005, 0.12)
    write("PupsikPop", squeal + 0.8 * place(pad(0.3), thump(0.12, 380, 120, 0.04), 0))

    # Оловянный солдатик: жестяной «дзынь» и взмах.
    def tin(freq, duration):
        m = int(SR * duration)
        mod = osc(freq * 1.41, m) * 3.0 * decay(m, duration * 0.4)
        return np.sin(2 * np.pi * np.cumsum(np.full(m, freq)) / SR + mod) * attack_decay(m, 0.001, duration * 0.3)

    write("SoldierThrow", 0.6 * tin(2600, 0.25) + 0.7 * whoosh(0.25, 600, 2000, 800, 0.35))
    clatter = pad(0.6)
    for i in range(7):
        place(clatter, 0.5 * tin(RNG.uniform(1800, 3600), 0.2), RNG.uniform(0, 0.35))
    n = int(SR * 0.15)
    place(clatter, spectral(noise(n), lo=800) * attack_decay(n, 0.001, 0.04), 0)
    write("SoldierPop", clatter)

    boss = pad(1.2)
    place(boss, bell(note("A2"), 1.1, 1.3), 0)
    place(boss, 0.6 * bell(note("E3"), 1.0, 1.0), 0.02)
    n = int(SR * 0.3)
    place(boss, spectral(noise(n), lo=300, hi=6000) * attack_decay(n, 0.001, 0.06), 0)
    place(boss, 0.4 * bell(note("A5"), 0.7, 0.8), 0.18)
    write("BossSplit", boss)

    n = int(SR * 0.35)
    warn = osc(sweep(300, 620, n), n, "triangle") * (0.6 + 0.4 * np.sin(np.arange(n) / SR * 2 * np.pi * 18)) * attack_decay(n, 0.08, 0.15)
    write("SpawnWarning", warn)


# ---------------------------------------------------------------- забег и интерфейс

def square_note(freq, duration, duty_soft=0.35):
    n = int(SR * duration)
    tone = spectral(osc(freq, n, "square"), hi=freq * 8) * attack_decay(n, 0.004, duration * duty_soft)
    return tone


def sfx_run():
    up = pad(0.8)
    for i, name in enumerate(("C5", "E5", "G5", "C6")):
        place(up, square_note(note(name), 0.16), i * 0.08)
    place(up, 0.6 * bell(note("C6"), 0.5, 0.5), 0.32)
    write("LevelUp", up)

    n = int(SR * 0.3)
    paper = spectral(noise(n), lo=2500, hi=7000) * attack_decay(n, 0.005, 0.05)
    write("CardPick", paper + place(pad(0.3), 0.7 * pluck(note("E5"), 0.25) + 0.5 * pluck(note("B5"), 0.25), 0.03))

    n = int(SR * 0.06)
    write("UiMove", osc(1250, n) * attack_decay(n, 0.001, 0.012) + 0.4 * place(pad(0.06), click(0.006, 3000), 0))

    fanfare = pad(1.8)
    melody = (("G4", 0.0, 0.12), ("C5", 0.12, 0.12), ("E5", 0.24, 0.12), ("G5", 0.36, 0.24),
              ("E5", 0.62, 0.1), ("G5", 0.72, 0.1), ("C6", 0.84, 0.8))
    for name, at, dur in melody:
        place(fanfare, square_note(note(name), dur + 0.06, 0.6), at)
    for name, at in (("C3", 0.0), ("G3", 0.36), ("C3", 0.84)):
        place(fanfare, 0.6 * pluck(note(name), 0.5, "triangle"), at)
    place(fanfare, 0.5 * bell(note("C6"), 0.9, 0.6), 0.84)
    write("Victory", fanfare)

    sad = pad(1.6)
    for i, (name, dur) in enumerate((("G3", 0.28), ("F#3", 0.28), ("F3", 0.28), ("E3", 0.7))):
        n = int(SR * dur)
        vibrato = 1 + (0.012 * np.sin(np.arange(n) / SR * 2 * np.pi * 6) if i == 3 else 0)
        tone = osc(note(name) * vibrato, n, "saw")
        cutoff = 600 + 900 * np.sin(np.linspace(0, np.pi, n))
        tone = spectral(tone, hi=float(np.mean(cutoff))) * attack_decay(n, 0.02, dur * 0.6)
        place(sad, tone, i * 0.3)
    write("GameOver", sad)


# ---------------------------------------------------------------- коробка, стройка, погода

def write_loop(name, x, crossfade=0.25):
    """Бесшовная петля: хвост плавно накладывается на начало, без затухания в конце."""
    f = int(SR * crossfade)
    head, tail = x[:f], x[-f:]
    ramp = np.linspace(0.0, 1.0, f)
    body = x[f:-f].copy()
    x = np.concatenate([head * ramp + tail * (1.0 - ramp), body])
    x = x - np.mean(x)
    m = np.abs(x).max()
    x = x * (0.7 / m) if m > 0 else x
    os.makedirs(OUT, exist_ok=True)
    data = (np.clip(x, -1.0, 1.0) * 32767).astype(np.int16)
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print("wrote", name, f"{len(data) / SR:.2f}s (loop)")


def sfx_dusk():
    # Свисток физрука: высокий тон с трелью горошины, в начале — выдох.
    n = int(SR * 0.75)
    t = np.arange(n) / SR
    trill = 1.0 + 0.045 * np.sign(np.sin(2 * np.pi * 34 * t)) * (0.6 + 0.4 * np.sin(2 * np.pi * 3 * t))
    tone = osc(2750.0 * trill, n) + 0.35 * osc(5500.0 * trill, n)
    breath = band_noise(n, 2900.0, 900.0) * 0.5
    env = attack_decay(n, 0.02, 0.9) * np.clip((0.75 - t) / 0.08, 0.0, 1.0)
    write("Whistle", (tone * 0.8 + breath) * env)

    # Гром: треск и долгий низкий раскат, который перекатывается.
    n = int(SR * 3.2)
    t = np.arange(n) / SR
    crack = spectral(noise(n), lo=400, hi=5000) * attack_decay(n, 0.002, 0.08)
    rumble = spectral(noise(n), hi=180) * attack_decay(n, 0.15, 1.3)
    roll = 0.6 + 0.4 * np.sin(2 * np.pi * (1.3 + 0.6 * t) * t)
    write("Thunder", 0.5 * crack + 2.2 * rumble * roll)

    # Манекен встал в позу: сухой пластиковый щелчок суставов, два подряд.
    clack = pad(0.2)
    for at, freq, amp in ((0.0, 1150, 1.0), (0.055, 820, 0.7)):
        m = int(SR * 0.06)
        knock = osc(freq, m, "triangle") * attack_decay(m, 0.001, 0.012)
        place(knock, 0.6 * click(0.01, 2500), 0.0)
        place(clack, amp * knock, at)
    write("MannequinPose", clack)

    # Тень: шипящий выдох, уходящий вниз, под ним глухой гул.
    n = int(SR * 0.7)
    hiss = band_noise(n, sweep(5200, 2200, n), 1800.0) * attack_decay(n, 0.12, 0.25)
    hum = osc(sweep(95, 60, n), n) * attack_decay(n, 0.1, 0.3) * 0.5
    write("ShadowHiss", hiss + hum)

    # Чучело поймало мяч: шорох соломы и мягкий хлопок.
    n = int(SR * 0.3)
    rustle = spectral(noise(n), lo=1200, hi=6000) * attack_decay(n, 0.004, 0.07)
    write("ScarecrowCatch", rustle + 0.9 * place(pad(0.3), thump(0.12, 220, 120, 0.04), 0.0))

    # Фонарь погас: электрический треск и затихающий гул.
    n = int(SR * 0.6)
    crackle = np.zeros(n)
    rng = np.random.default_rng(11)
    for at in np.sort(rng.uniform(0.0, 0.3, 14)):
        place(crackle, rng.uniform(0.4, 1.0) * click(0.006, 2000), at)
    buzz = osc(100.0, n, "saw") * attack_decay(n, 0.005, 0.18) * 0.35
    write("LampOut", crackle + spectral(buzz, hi=1500))

    # «Второе дыхание»: удар сердца и восходящий перезвон.
    breath = pad(1.0)
    place(breath, thump(0.18, 90, 50, 0.06), 0.0)
    place(breath, 0.7 * thump(0.16, 80, 45, 0.05), 0.22)
    for i, name in enumerate(("C5", "G5", "C6")):
        place(breath, 0.5 * bell(note(name), 0.6, 0.7), 0.3 + i * 0.09)
    write("SecondWind", breath)

    # Дождь: ровный шум капель с редкими звонкими каплями — петля для погоды.
    n = int(SR * 4.5)
    rain = spectral(noise(n), lo=500, hi=7000) * 0.6 + spectral(noise(n), hi=400) * 0.35
    rng = np.random.default_rng(12)
    for at in rng.uniform(0.0, 4.4, 60):
        m = int(SR * 0.03)
        drop = osc(rng.uniform(1800, 3600), m) * attack_decay(m, 0.001, 0.006) * rng.uniform(0.1, 0.35)
        place(rain, drop, at)
    write_loop("RainLoop", rain)


def reverb(x, seconds=0.9, mix=0.35, seed=21):
    """Простое эхо двора: свёртка с затухающим шумом (через FFT)."""
    rng = np.random.default_rng(seed)
    n = int(SR * seconds)
    ir = rng.uniform(-1.0, 1.0, n) * np.exp(-np.arange(n) / SR / (seconds * 0.3))
    ir = spectral(ir, hi=4000)
    ir /= max(1e-6, np.abs(ir).sum() ** 0.5)
    size = len(x) + n
    wet = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)
    dry = np.concatenate([x, np.zeros(n)])
    wet *= np.abs(dry).max() / max(1e-6, np.abs(wet).max())
    return dry * (1.0 - mix) + wet * mix


def vowel(source, formants):
    """Грубый голос: полосы вокруг формант, formants = [(частота, ширина, вес)]."""
    return sum(w * spectral(source, lo=f - bw, hi=f + bw) for f, bw, w in formants)


def sfx_final():
    # Смешок Бабая: глухое «хе-хе-хе» с хрипом, каждое ниже предыдущего.
    laugh = pad(1.0)
    for i, (at, f0) in enumerate(((0.0, 118.0), (0.17, 108.0), (0.34, 98.0), (0.53, 90.0))):
        m = int(SR * 0.16)
        glottal = osc(sweep(f0, f0 * 0.85, m), m, "saw")
        voice = vowel(glottal, [(420.0, 140.0, 1.0), (900.0, 220.0, 0.55), (2400.0, 400.0, 0.12)])
        breath = band_noise(m, 1400.0, 900.0) * 0.35
        env = attack_decay(m, 0.012, 0.05)
        place(laugh, (voice + breath) * env * (1.0 - 0.12 * i), at)
    write("BabaiLaugh", reverb(laugh, 0.7, 0.3))

    # «Считалочка»: деревянный стук и низкий колокол на каждый счёт.
    n = int(SR * 0.7)
    knock = thump(0.12, 190, 120, 0.035) + 0.5 * place(pad(0.12), click(0.01, 1400), 0)
    tick = pad(0.7)
    place(tick, knock, 0.0)
    place(tick, 0.35 * bell(note("A3"), 0.65, 0.5), 0.0)
    write("CountTick", reverb(tick, 0.5, 0.25))

    # «Я иду искать!»: тритон колоколами и свист, уходящий вверх.
    sting = pad(1.3)
    place(sting, 0.6 * bell(note("C4"), 1.2, 0.8), 0.0)
    place(sting, 0.5 * bell(note("F#4"), 1.2, 0.8), 0.02)
    place(sting, 0.7 * whoosh(0.5, 300, 2600, 900, 0.7), 0.0)
    place(sting, 0.6 * thump(0.2, 80, 45, 0.07), 0.0)
    write("FoundYou", reverb(sting, 0.8, 0.3))

    # Приземление после прыжка на шесте: тяжёлый удар, пыль и стук клюки.
    n = int(SR * 0.7)
    dust = spectral(noise(n), lo=150, hi=1800) * attack_decay(n, 0.004, 0.18)
    land = 1.4 * place(pad(0.7), thump(0.3, 95, 38, 0.1), 0) + 0.5 * dust
    place(land, 0.4 * (osc(620, int(SR * 0.08), "triangle") * attack_decay(int(SR * 0.08), 0.001, 0.02)), 0.01)
    write("PoleLand", land)

    # «Карусель»: воющий вихрь, который то накатывает, то отпускает — фигура кружится.
    n = int(SR * 1.8)
    t = np.arange(n) / SR
    whirl = band_noise(n, sweep(380, 1300, n), 700.0) * (0.55 + 0.45 * np.sin(2 * np.pi * 6.0 * t))
    env = np.clip(t / 0.25, 0.0, 1.0) * np.clip((1.8 - t) / 0.4, 0.0, 1.0)
    write("CarouselSpin", whirl * env + 0.3 * osc(sweep(90, 140, n), n) * env)

    # Ворона: хриплое «кра-а», тон сползает вниз.
    n = int(SR * 0.42)
    t = np.arange(n) / SR
    source = osc(sweep(640, 470, n), n, "saw") * (1.0 + 0.3 * np.sign(np.sin(2 * np.pi * 55 * t)))
    caw = vowel(source, [(900.0, 250.0, 1.0), (1500.0, 300.0, 0.8), (2700.0, 400.0, 0.3)])
    caw += 0.35 * band_noise(n, 1800.0, 1400.0)
    write("CrowCaw", caw * attack_decay(n, 0.015, 0.16))

    # Хлопанье крыльев: несколько глухих взмахов.
    flap = pad(0.5)
    for i, at in enumerate((0.0, 0.09, 0.18, 0.28)):
        m = int(SR * 0.07)
        place(flap, (0.9 - 0.15 * i) * spectral(noise(m), lo=250, hi=2200) * attack_decay(m, 0.006, 0.02), at)
    write("CrowFlap", flap)

    # Ложное чучело рассыпалось: шорох соломы и мягкий хлопок.
    n = int(SR * 0.7)
    straw = spectral(noise(n), lo=1500, hi=8000) * attack_decay(n, 0.003, 0.12)
    straw += 0.5 * spectral(noise(n), lo=400, hi=2000) * attack_decay(n, 0.01, 0.2)
    write("DecoyBurst", straw + 0.8 * place(pad(0.7), thump(0.15, 160, 70, 0.05), 0))

    # Мячи улетели в мешок: глухое «фух» и шорох мешковины.
    n = int(SR * 0.45)
    stuff = 0.9 * place(pad(0.45), thump(0.18, 140, 70, 0.06), 0.03)
    stuff += 0.45 * spectral(noise(n), lo=600, hi=3500) * attack_decay(n, 0.02, 0.1)
    write("SackStuff", stuff)

    # Мешок высыпался: мячи скачут по асфальту в разные стороны.
    spill = pad(1.0)
    rng = np.random.default_rng(31)
    for at in np.sort(rng.uniform(0.0, 0.6, 7)):
        f = rng.uniform(260, 420)
        m = int(SR * 0.12)
        boing = osc(sweep(f * 1.3, f, m), m) * attack_decay(m, 0.002, 0.04)
        place(spill, rng.uniform(0.4, 0.8) * boing, at)
    n = int(SR * 1.0)
    spill += 0.3 * spectral(noise(n), lo=600, hi=3500) * attack_decay(n, 0.01, 0.15)
    write("SackSpill", spill)

    # Свет погас во всём дворе: глухой провал, шипение вниз и треск умирающих ламп.
    n = int(SR * 1.6)
    t = np.arange(n) / SR
    whump = osc(sweep(62, 32, n), n) * attack_decay(n, 0.02, 0.5)
    hiss = band_noise(n, sweep(3200, 280, n), 1300.0) * attack_decay(n, 0.05, 0.45) * 0.5
    dark = 1.3 * whump + hiss
    rng = np.random.default_rng(41)
    for at in np.sort(rng.uniform(0.05, 0.6, 10)):
        place(dark, rng.uniform(0.2, 0.5) * click(0.006, 2200), at)
    write("DarkWave", dark)

    # Мама зовёт из окна: скрип форточки и далёкое напевное «До-мо-ой!».
    call = pad(2.4)
    n = int(SR * 0.3)
    creak = osc(sweep(900, 600, n) * (1.0 + 0.06 * noise(n)), n, "saw")
    place(call, 0.18 * spectral(creak, lo=500, hi=3000) * attack_decay(n, 0.01, 0.12), 0.0)
    for at, f0, length in ((0.35, note("G4"), 0.32), (0.66, note("E4"), 0.3), (0.95, note("D4"), 0.9)):
        m = int(SR * length)
        t = np.arange(m) / SR
        vib = 1.0 + 0.012 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t / 0.2, 0, 1)
        source = osc(f0 * vib, m, "saw")
        voice = vowel(source, [(600.0, 160.0, 1.0), (1000.0, 200.0, 0.7), (2600.0, 350.0, 0.15)])
        env = np.clip(t / 0.06, 0, 1) * np.clip((length - t) / 0.12, 0, 1)
        place(call, voice * env, at)
    write("MomCall", reverb(call, 1.2, 0.45))

    # Дверь подъезда: скрип петель, пружина и тяжёлый хлопок железной двери.
    door = pad(1.4)
    n = int(SR * 0.5)
    t = np.arange(n) / SR
    hinge = osc(sweep(420, 260, n) * (1.0 + 0.04 * np.sin(2 * np.pi * 23 * t)), n, "saw")
    place(door, 0.25 * spectral(hinge, lo=300, hi=2500) * attack_decay(n, 0.05, 0.25), 0.0)
    m = int(SR * 0.35)
    spring = osc(sweep(900, 700, m), m, "triangle") * attack_decay(m, 0.002, 0.12) * (1 + 0.5 * np.sin(2 * np.pi * 30 * np.arange(m) / SR))
    place(door, 0.3 * spring, 0.5)
    place(door, 1.2 * thump(0.3, 110, 45, 0.09), 0.62)
    m = int(SR * 0.7)
    ring = sum(a * osc(f, m) * decay(m, d) for f, a, d in ((410, 0.4, 0.25), (1030, 0.25, 0.15), (1720, 0.15, 0.1)))
    place(door, 0.5 * ring, 0.62)
    write("DoorOpen", reverb(door, 0.6, 0.25))


if __name__ == "__main__":
    sfx_player()
    sfx_enemies()
    sfx_run()
    sfx_dusk()
    sfx_final()

"""Neon Yurt studio splash: neon sign flickers on, glows, fades. 1920x1080 @60, ~2.8 s + neon hum wav."""
import os, subprocess, wave
import numpy as np
from PIL import Image, ImageFilter
import imageio_ffmpeg

ROOT = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer"
OUT = os.path.dirname(os.path.abspath(__file__)) + "/out"
os.makedirs(OUT, exist_ok=True)
FPS, W, H = 60, 1920, 1080
DUR = 2.8
N = int(DUR * FPS)

src = Image.open(ROOT + "/Assets/_Project/Art/UI/Branding/NeonYurtBanner.png").convert("RGB")
# fit by height with a little margin so the zoom has room
base_h = int(H * 0.92)
base_w = int(src.width * base_h / src.height)
big = src.resize((base_w, base_h), Image.LANCZOS)
canvas = Image.new("RGB", (W, H))
canvas.paste(big, ((W - base_w) // 2, (H - base_h) // 2))
sign = np.asarray(canvas).astype(np.float32) / 255.0
glow1 = np.asarray(canvas.filter(ImageFilter.GaussianBlur(14))).astype(np.float32) / 255.0
glow2 = np.asarray(canvas.filter(ImageFilter.GaussianBlur(48))).astype(np.float32) / 255.0

# flicker key: (time, level). Neon tube catching: stutters, then holds.
keys = [(0.00, 0), (0.30, 0), (0.33, .9), (0.37, 0), (0.46, 0), (0.48, 1), (0.53, .15),
        (0.60, .2), (0.62, 1), (0.66, .4), (0.70, 1), (0.74, .55), (0.78, 1), (2.25, 1), (2.75, 0), (DUR, 0)]
def level(t):
    for (t0, a), (t1, b) in zip(keys, keys[1:]):
        if t0 <= t <= t1:
            # steps during the flicker, smooth fades later
            if t1 <= 0.8: return a
            u = (t - t0) / max(t1 - t0, 1e-6)
            return a + (b - a) * u
    return 0.0

rng = np.random.default_rng(7)
grain = [rng.normal(0, 0.012, (H // 4, W // 4)).astype(np.float32) for _ in range(8)]

ff = imageio_ffmpeg.get_ffmpeg_exe()
out = OUT + "/00_splash.mp4"
p = subprocess.Popen([ff, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                      "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-crf", "14", "-preset", "slow",
                      "-pix_fmt", "yuv420p", out], stdin=subprocess.PIPE)
for i in range(N):
    t = i / FPS
    L = level(t)
    pulse = 1 + 0.04 * np.sin(t * 23) * (t > 0.8)
    s = 1.0 + 0.035 * (t / DUR)  # slow push-in
    glow = glow1 * 0.3 + glow2 * 0.45
    lit = sign * (0.15 + 0.85 * L) * (L > 0)
    img = 1 - (1 - lit) * (1 - glow * L * pulse)  # screen blend keeps the sign's texture
    if abs(s - 1) > 1e-4:
        im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
        cw, ch = int(W / s), int(H / s)
        im = im.crop(((W - cw) // 2, (H - ch) // 2, (W + cw) // 2, (H + ch) // 2)).resize((W, H), Image.BILINEAR)
        img = np.asarray(im).astype(np.float32) / 255.0
    g = np.kron(grain[i % 8], np.ones((4, 4), np.float32))[..., None]
    img = np.clip(img + g * (0.3 + L), 0, 1)
    p.stdin.write((img * 255).astype(np.uint8).tobytes())
p.stdin.close(); p.wait()

# neon hum: 100 Hz buzz + harmonics, crackle on each flicker step, follows the light level
SR = 48000
t = np.arange(int(DUR * SR)) / SR
lv = np.array([level(x) for x in t[::48]]).repeat(48)[: len(t)]
hum = sum(np.sin(2 * np.pi * 100 * k * t) / k ** 1.3 for k in range(1, 9))
hum = np.tanh(hum * 1.6) * 0.16 * lv
crack = np.zeros_like(t)
for (t0, a), (t1, b) in zip(keys, keys[1:]):
    if t1 <= 0.8 and a != b:
        n0 = int(t1 * SR); n = int(0.025 * SR)
        crack[n0:n0 + n] += rng.normal(0, 1, n) * np.exp(-np.arange(n) / (0.005 * SR)) * 0.45
# soft "thoom" when it settles
n0 = int(0.78 * SR); n = int(0.6 * SR)
crack[n0:n0 + n] += np.sin(2 * np.pi * 55 * np.arange(n) / SR) * np.exp(-np.arange(n) / (0.15 * SR)) * 0.35
sig = np.clip(hum + crack, -1, 1)
st = np.stack([sig, sig], 1)
with wave.open(OUT + "/00_splash.wav", "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
    w.writeframes((st * 32767).astype(np.int16).tobytes())
print("ok", out)

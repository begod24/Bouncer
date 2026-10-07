#!/usr/bin/env python3
"""
Bouncer: маленькие текстуры для мира (не интерфейс): тень вороны на асфальте, мама в окне в финале,
эффекты карточек 2026-10 (меловые надписи и знаки, снимок «Смены», рожицы тамагочи, полосы VHS, резинка).

Запуск из корня репозитория:  python3 Tools/vfx_art.py
Нужны numpy и Pillow. Белая форма на прозрачном фоне — цвет задаёт материал в Unity.
Пишет в Assets/_Project/Art/VFX.
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "VFX")


def save(name, alpha):
    os.makedirs(OUT, exist_ok=True)
    a = np.clip(alpha * 255.0 + 0.5, 0, 255).astype(np.uint8)
    rgba = np.zeros(a.shape + (4,), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = a
    Image.fromarray(rgba).save(os.path.join(OUT, name))
    print("wrote", name)


def blob_shadow(size=64):
    """Мягкое пятно тени: плотное в середине, тает к краю."""
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32) + 0.5
    r = np.hypot(xs - size / 2, ys - size / 2) / (size / 2)
    return np.clip(1.0 - r, 0.0, 1.0) ** 1.4


def mom_silhouette(size=128):
    """Мама в окне: голова с пучком, плечи, рука у рта — зовёт домой."""
    img = Image.new("L", (size * 4, size * 4), 0)
    d = ImageDraw.Draw(img)
    s = 4

    def circle(cx, cy, r):
        d.ellipse([(cx - r) * s, (cy - r) * s, (cx + r) * s, (cy + r) * s], fill=255)

    circle(64, 22, 10)
    circle(64, 46, 19)
    d.rectangle([54 * s, 56 * s, 74 * s, 80 * s], fill=255)
    d.polygon([(40 * s, 74 * s), (88 * s, 74 * s), (112 * s, 128 * s), (16 * s, 128 * s)], fill=255)
    circle(40, 80, 12)
    circle(88, 80, 12)
    d.line([(90 * s, 84 * s), (96 * s, 66 * s), (82 * s, 52 * s)], fill=255, width=12 * s, joint="curve")
    circle(80, 52, 8)
    img = img.filter(ImageFilter.GaussianBlur(3)).resize((size, size), Image.LANCZOS)
    return np.asarray(img, np.float32) / 255.0


FONT = os.path.join(ROOT, "Assets", "_Project", "Art", "Fonts", "Caveat.ttf")


def chalk(alpha, seed):
    """Меловая фактура: зерно и пропуски поверх формы."""
    rng = np.random.default_rng(seed)
    h, w = alpha.shape
    fine = np.asarray(Image.fromarray((rng.random((h, w)) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)),
                      np.float32) / 255.0
    fine = (fine - fine.min()) / max(1e-6, fine.max() - fine.min())
    a = alpha * np.clip(0.45 + 0.75 * fine, 0.0, 1.0)
    return np.where(rng.random((h, w)) < 0.06, a * 0.35, a)


def word(text, size=(512, 256), font_px=170, seed=1, outline=True):
    """Меловое слово (Caveat), белое на прозрачном."""
    from PIL import ImageFont
    img = Image.new("L", size, 0)
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(FONT, font_px)
    box = d.textbbox((0, 0), text, font=font)
    x = (size[0] - (box[2] - box[0])) / 2 - box[0]
    y = (size[1] - (box[3] - box[1])) / 2 - box[1]
    if outline:
        d.text((x, y), text, font=font, fill=255, stroke_width=6, stroke_fill=255)
    else:
        d.text((x, y), text, font=font, fill=255)
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(0.8)), np.float32) / 255.0
    return chalk(a, seed)


def strokes(size, lines, width, seed, closed=False):
    img = Image.new("L", (size * 4, size * 4), 0)
    d = ImageDraw.Draw(img)
    for pts in lines:
        d.line([(x * 4, y * 4) for x, y in pts], fill=255, width=int(width * 4), joint="curve")
    img = img.filter(ImageFilter.GaussianBlur(3)).resize((size, size), Image.LANCZOS)
    return chalk(np.asarray(img, np.float32) / 255.0, seed)


def chalk_cross(size=128):
    return strokes(size, [[(24, 26), (104, 100)], [(102, 24), (26, 104)]], 13, 101)


def chalk_spiral(size=256):
    pts = []
    for i in range(300):
        t = i / 299
        a = t * 3.2 * 2 * math.pi
        r = 10 + 108 * t
        pts.append((128 + r * math.cos(a), 128 + r * math.sin(a)))
    return strokes(size, [pts], 7, 102)


def heart(size=(256, 256)):
    img = Image.new("L", size, 0)
    d = ImageDraw.Draw(img)
    pts = []
    for k in range(120):
        t = 2 * math.pi * k / 120
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((128 + x * 6.5, 120 - y * 6.5))
    d.line(pts + [pts[0]], fill=255, width=16, joint="curve")
    a = np.asarray(img.filter(ImageFilter.GaussianBlur(1.0)), np.float32) / 255.0
    return chalk(a, 103)


def radio_waves(size=256):
    lines = []
    for r in (40, 75, 110):
        for side in (-1, 1):
            lines.append([(128 + side * r * math.cos(math.radians(a)), 150 - r * math.sin(math.radians(a)))
                          for a in np.linspace(-35, 35, 24)] if side > 0 else
                         [(128 - r * math.cos(math.radians(a)), 150 - r * math.sin(math.radians(a))) for a in np.linspace(-35, 35, 24)])
    return strokes(size, lines, 10, 104)


def handprints(w=512, h=128):
    """Полоса из ладошек (для кольца «Круговой поруки»): тянется вдоль линии."""
    img = Image.new("L", (w * 2, h * 2), 0)
    d = ImageDraw.Draw(img)
    for k in range(4):
        cx, cy = (64 + k * 128) * 2, 64 * 2
        d.ellipse([cx - 34, cy - 20, cx + 34, cy + 56], fill=255)
        for i, (dx, ln) in enumerate(((-30, 40), (-12, 52), (6, 54), (24, 46))):
            x = cx + dx
            d.rounded_rectangle([x - 8, cy - 18 - ln, x + 8, cy - 4], radius=8, fill=255)
        d.rounded_rectangle([cx + 30, cy - 4, cx + 62, cy + 12], radius=8, fill=255)
    img = img.filter(ImageFilter.GaussianBlur(1.5)).resize((w, h), Image.LANCZOS)
    return chalk(np.asarray(img, np.float32) / 255.0, 105)


def vhs(w=64, h=256):
    y = np.arange(h, dtype=np.float32)[:, None]
    lines = (np.sin(y / h * 2 * math.pi * 40) > 0.55).astype(np.float32) * 0.5
    band = np.exp(-((y - h * 0.3) / 10.0) ** 2) * 0.9 + np.exp(-((y - h * 0.75) / 5.0) ** 2) * 0.6
    rng = np.random.default_rng(106)
    noise = (rng.random((h, w)) < 0.02).astype(np.float32) * 0.8
    return np.clip(np.broadcast_to(lines + band, (h, w)) + noise, 0, 1)


def spiky_warning(size=256):
    """Предупреждение под ёжиком: кольцо с зубцами внутрь, как колючки."""
    img = Image.new("L", (size * 2, size * 2), 0)
    d = ImageDraw.Draw(img)
    c = size
    r_out, r_in = size * 0.92, size * 0.78
    d.ellipse([c - r_out, c - r_out, c + r_out, c + r_out], fill=255)
    d.ellipse([c - r_in, c - r_in, c + r_in, c + r_in], fill=0)
    for k in range(14):
        a = 2 * math.pi * k / 14
        b = math.pi / 14
        p0 = (c + r_in * math.cos(a - b), c + r_in * math.sin(a - b))
        p1 = (c + r_in * math.cos(a + b), c + r_in * math.sin(a + b))
        tip = (c + size * 0.5 * math.cos(a), c + size * 0.5 * math.sin(a))
        d.polygon([p0, p1, tip], fill=255)
    img = img.filter(ImageFilter.GaussianBlur(2)).resize((size, size), Image.LANCZOS)
    return np.asarray(img, np.float32) / 255.0


def save_rgba(name, rgba):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgba).save(os.path.join(OUT, name))
    print("wrote", name)


def photo(w=160, h=192):
    """Снимок «Смены»: белая рамка, выцветший двор, тёмный силуэт игрушки."""
    img = Image.new("RGBA", (w, h), (246, 242, 230, 255))
    d = ImageDraw.Draw(img)
    x0, y0, x1, y1 = 12, 12, w - 12, h - 44
    for i in range(y1 - y0):
        t = i / (y1 - y0)
        c = (int(170 + 40 * t), int(190 + 20 * t), int(196 - 30 * t), 255)
        d.line([(x0, y0 + i), (x1, y0 + i)], fill=c)
    d.rectangle([x0, y1 - 30, x1, y1], fill=(120, 116, 104, 255))
    cx, base = (x0 + x1) // 2, y1 - 22
    sil = (40, 38, 44, 255)
    d.ellipse([cx - 26, base - 70, cx + 26, base - 20], fill=sil)
    d.ellipse([cx - 20, base - 100, cx + 20, base - 62], fill=sil)
    d.ellipse([cx - 22, base - 108, cx - 8, base - 92], fill=sil)
    d.ellipse([cx + 8, base - 108, cx + 22, base - 92], fill=sil)
    d.ellipse([cx - 12, base - 88, cx - 4, base - 80], fill=(250, 250, 240, 255))
    d.ellipse([cx + 4, base - 88, cx + 12, base - 80], fill=(250, 250, 240, 255))
    for k in range(3):
        d.line([(x0 + 10 + k * 22, h - 30), (x0 + 24 + k * 22, h - 22)], fill=(150, 140, 120, 255), width=2)
    rgba = np.asarray(img).copy()
    rng = np.random.default_rng(107)
    grain = (rng.normal(0, 7, rgba.shape[:2])).astype(np.int16)
    for c in range(3):
        rgba[..., c] = np.clip(rgba[..., c].astype(np.int16) + grain, 0, 255).astype(np.uint8)
    return rgba


FACES = {
    0: ["..........", ".XX....XX.", ".X......X.", "..........", "...XXXX...", "..X....X..", ".........."],
    1: ["..........", ".XX....XX.", ".XX....XX.", "..........", "..X....X..", "...XXXX...", ".........."],
    2: ["..........", ".X.X..X.X.", "..X....X..", "..........", ".X......X.", "..XXXXXX..", ".........."],
}


def tamagotchi_face(kind, w=80, h=64):
    """Жидкокристаллический экран: рожица из квадратных пикселей. 0 — голодный, 1 — ест, 2 — сытый."""
    lcd = np.zeros((h, w, 4), np.uint8)
    lcd[...] = (170, 196, 120, 255)
    rows = FACES[kind]
    cell = 6
    ox = (w - len(rows[0]) * cell) // 2
    oy = (h - len(rows) * cell) // 2
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch == "X":
                lcd[oy + j * cell + 1: oy + (j + 1) * cell, ox + i * cell + 1: ox + (i + 1) * cell] = (40, 52, 36, 255)
    filled = {0: 1, 1: 3, 2: 5}[kind]
    for k in range(5):
        x = 8 + k * 13
        color = (40, 52, 36, 255) if k < filled else (130, 150, 96, 255)
        lcd[h - 9: h - 4, x: x + 9] = color
    return lcd


def elastic(w=256, h=32):
    """Резинка: полоски по длине, как у резиночки для прыжков."""
    rgba = np.zeros((h, w, 4), np.uint8)
    colors = [(235, 90, 110), (250, 205, 70), (90, 170, 235)]
    for x in range(w):
        rgba[:, x] = colors[(x // 22) % 3] + (255,)
    rgba[:3] = (255, 255, 255, 120)
    rgba[-3:] = (60, 40, 50, 160)
    return rgba


# ---------------------------------------------------------------- атлас частиц (VFX-проход 2026-10)
# Мультяшные формы вместо стандартного размытого кружка. A — форма, RGB — светотень в сером
# (свет сверху-слева, тень снизу-справа), цвет даёт частица. Сетка 8×8 по 128 px, ряды:
#   0 клубок дыма/пыли, 8 кадров: растёт, потом рассыпается
#   1 искры и звёздочки, 8 вариантов
#   2 бумажки (0–5) и квадратики-пиксели (6–7)
#   3 перья, 4 соломинки, 5 клочки ваты
#   6 щепки (0–3) и капли (4–7)
#   7 штрих вдоль Y (0), штрих вдоль X (1), всплеск 4 кадра (2–5), мягкое пятно (6), кольцо (7)
# Ряды и столбцы зашиты в Scripts/Editor/VfxPassBuilder.cs — менять вместе.

FX_CELL = 128
FX_SS = 4
FX_SHADE = 178
FX_LIGHT = (-0.07, -0.09)


class FxCell:
    """Одна клетка атласа, рисуется в 4× и уменьшается. Координаты в долях: -1..1, y вниз."""

    def __init__(self):
        n = FX_CELL * FX_SS
        self.n = n
        self.alpha = Image.new("L", (n, n), 0)
        self.lit = Image.new("L", (n, n), 0)
        self.dark = Image.new("L", (n, n), 0)
        self.a = ImageDraw.Draw(self.alpha)
        self.l = ImageDraw.Draw(self.lit)
        self.d = ImageDraw.Draw(self.dark)

    def p(self, x, y):
        h = self.n / 2
        return (h + x * h, h + y * h)

    def s(self, r):
        return r * self.n / 2

    def circle(self, x, y, r, shade=True, cut=False):
        box = [*self.p(x - r, y - r), *self.p(x + r, y + r)]
        self.a.ellipse(box, fill=0 if cut else 255)
        if cut:
            self.l.ellipse(box, fill=0)
            return
        if shade:
            lx, ly = FX_LIGHT
            rr = r * 0.9
            self.l.ellipse([*self.p(x + lx * r * 2 - rr, y + ly * r * 2 - rr), *self.p(x + lx * r * 2 + rr, y + ly * r * 2 + rr)], fill=255)
        else:
            self.l.ellipse(box, fill=255)

    def poly(self, pts, lit=True, cut=False):
        pp = [self.p(x, y) for x, y in pts]
        self.a.polygon(pp, fill=0 if cut else 255)
        if cut:
            self.l.polygon(pp, fill=0)
        elif lit:
            self.l.polygon(pp, fill=255)

    def line(self, pts, width, lit=True, dark=False):
        pp = [self.p(x, y) for x, y in pts]
        w = max(1, int(self.s(width)))
        self.a.line(pp, fill=255, width=w, joint="curve")
        for x, y in (pts[0], pts[-1]):
            r = self.s(width) / 2
            cx, cy = self.p(x, y)
            self.a.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
        if dark:
            self.d.line(pp, fill=255, width=w, joint="curve")
        elif lit:
            self.l.line(pp, fill=255, width=w, joint="curve")

    def shade_poly(self, pts):
        """Тень поверх: всё внутри многоугольника темнее."""
        self.d.polygon([self.p(x, y) for x, y in pts], fill=255)

    def done(self, soft=0.0):
        a = self.alpha
        if soft > 0:
            a = a.filter(ImageFilter.GaussianBlur(soft * FX_SS))
        size = (FX_CELL, FX_CELL)
        alpha = np.asarray(a.resize(size, Image.LANCZOS), np.float32) / 255.0
        lit = np.asarray(self.lit.filter(ImageFilter.GaussianBlur(FX_SS * 0.6)).resize(size, Image.LANCZOS), np.float32) / 255.0
        dark = np.asarray(self.dark.filter(ImageFilter.GaussianBlur(FX_SS * 0.4)).resize(size, Image.LANCZOS), np.float32) / 255.0
        shade = FX_SHADE / 255.0 + (1.0 - FX_SHADE / 255.0) * lit
        shade = shade * (1.0 - 0.3 * dark)
        rgba = np.zeros((FX_CELL, FX_CELL, 4), np.uint8)
        rgba[..., :3] = np.clip(shade * 255.0 + 0.5, 0, 255).astype(np.uint8)[..., None]
        rgba[..., 3] = np.clip(alpha * 255.0 + 0.5, 0, 255).astype(np.uint8)
        return rgba


def fx_puff(frame):
    rng = np.random.default_rng(300)
    blobs = [(0.0, 0.05, 0.42), (-0.32, 0.15, 0.3), (0.33, 0.12, 0.32), (-0.12, -0.25, 0.32), (0.2, -0.22, 0.28),
             (-0.38, -0.08, 0.22), (0.42, -0.06, 0.2), (0.05, 0.32, 0.26)]
    grow = min(1.0, 0.5 + 0.17 * frame)
    late = max(0, frame - 3)
    spread = 1.0 + late * 0.16
    shrink = 1.0 - late * 0.19
    c = FxCell()
    for i, (x, y, r) in enumerate(blobs):
        k = shrink * (1.0 - 0.12 * late * (i % 3 == 0))
        rr = r * grow * max(0.08, k)
        if rr < 0.03:
            continue
        jx, jy = rng.normal(0, 0.03, 2)
        c.circle((x + jx) * grow * spread, (y + jy) * grow * spread, rr)
    if late >= 2:
        for k in range(late):
            a = rng.uniform(0, 2 * math.pi)
            c.circle(0.3 * math.cos(a) * grow, 0.3 * math.sin(a) * grow, 0.06 + 0.03 * k, cut=True)
    return c.done()


def star_points(n, outer, inner, rot=0.0):
    pts = []
    for k in range(n * 2):
        a = rot + math.pi * k / n - math.pi / 2
        r = outer if k % 2 == 0 else inner
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def fx_star(variant):
    c = FxCell()
    shapes = {
        0: (4, 0.92, 0.17, 0.0), 1: (5, 0.85, 0.38, 0.0), 2: (4, 0.7, 0.24, math.pi / 4), 3: (6, 0.8, 0.36, 0.0),
        6: (8, 0.9, 0.48, 0.0), 7: (4, 0.82, 0.5, 0.0),
    }
    if variant in shapes:
        c.poly(star_points(*shapes[variant]))
    elif variant == 4:
        c.poly([(-0.16, -0.75), (0.16, -0.75), (0.16, -0.16), (0.75, -0.16), (0.75, 0.16), (0.16, 0.16), (0.16, 0.75),
                (-0.16, 0.75), (-0.16, 0.16), (-0.75, 0.16), (-0.75, -0.16), (-0.16, -0.16)])
    else:
        c.circle(0, 0, 0.5, shade=False)
    return c.done()


def fx_paper(variant):
    rng = np.random.default_rng(310 + variant)
    c = FxCell()
    if variant >= 6:
        s = 0.62
        c.poly([(-s, -s), (s, -s), (s, s), (-s, s)])
        c.shade_poly([(-s, s * 0.6), (s, s * 0.6), (s, s), (-s, s)])
        c.shade_poly([(s * 0.6, -s), (s, -s), (s, s), (s * 0.6, s)])
        return c.done()
    n = 3 + variant % 3
    rot = rng.uniform(0, 2 * math.pi)
    pts = []
    for k in range(n):
        a = rot + 2 * math.pi * k / n + rng.uniform(-0.3, 0.3)
        r = rng.uniform(0.5, 0.78)
        pts.append((r * math.cos(a), r * 0.75 * math.sin(a)))
    c.poly(pts)
    # сгиб: половина по одну сторону линии через центр — в тени
    a = rng.uniform(0, math.pi)
    nx, ny = math.cos(a), math.sin(a)
    c.shade_poly([(ny * 3, -nx * 3), (-ny * 3, nx * 3), (nx * 3 - ny * 3, ny * 3 + nx * 3), (nx * 3 + ny * 3, ny * 3 - nx * 3)])
    return c.done()


def bezier(p0, p1, p2, t):
    u = 1 - t
    return (u * u * p0[0] + 2 * u * t * p1[0] + t * t * p2[0], u * u * p0[1] + 2 * u * t * p1[1] + t * t * p2[1])


def fx_feather(variant):
    rng = np.random.default_rng(320 + variant)
    c = FxCell()
    rot = rng.uniform(-0.6, 0.6) + (math.pi / 2 if variant % 2 else 0)
    bend = rng.uniform(0.15, 0.4) * (1 if variant % 3 else -1)

    def R(x, y):
        return (x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot))

    p0, p1, p2 = (-0.8, 0.1), (0.0, -bend), (0.8, 0.05)
    left, right, spine = [], [], []
    steps = 24
    for i in range(steps + 1):
        t = i / steps
        x, y = bezier(p0, p1, p2, t)
        tx, ty = bezier(p0, p1, p2, min(1, t + 0.01))
        dx, dy = tx - x, ty - y
        ln = math.hypot(dx, dy) or 1
        nx, ny = -dy / ln, dx / ln
        w = 0.3 * math.sin(math.pi * min(1, t * 1.15)) ** 0.7 if t > 0.12 else 0.0
        left.append(R(x + nx * w, y + ny * w))
        right.append(R(x - nx * w * 0.85, y - ny * w * 0.85))
        spine.append(R(x, y))
    c.poly(left + right[::-1])
    c.shade_poly(right + spine[::-1])
    for k in range(2):
        t = rng.uniform(0.35, 0.8)
        i = int(t * steps)
        side = left if k == 0 else right
        x, y = side[i]
        sx, sy = spine[i]
        c.poly([(x, y), (sx * 0.4 + x * 0.6, sy * 0.4 + y * 0.6), side[min(steps, i + 2)]], cut=True)
    c.line(spine[:4], 0.05, lit=False, dark=True)
    c.line(spine[3:], 0.03, lit=False, dark=True)
    return c.done()


def fx_straw(variant):
    rng = np.random.default_rng(330 + variant)
    c = FxCell()
    rot = rng.uniform(0, math.pi)
    bend = rng.uniform(-0.25, 0.25)
    length = rng.uniform(0.8, 0.95)

    def R(x, y):
        return (x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot))

    pts = [R(*bezier((-length, 0), (0, bend), (length, 0), i / 12)) for i in range(13)]
    c.line(pts, 0.14)
    c.line([(x + 0.02, y + 0.03) for x, y in pts[1:-1]], 0.04, lit=False, dark=True)
    if variant % 3 == 0:
        x, y = pts[8]
        tip = R(*bezier((-length, 0), (0, bend), (length, 0), 0.62))
        c.line([(x, y), (tip[0] + 0.25 * math.cos(rot + 0.6), tip[1] + 0.25 * math.sin(rot + 0.6))], 0.07)
    nx, ny = pts[5]
    c.circle(nx, ny, 0.075, shade=False)
    return c.done()


def fx_fluff(variant):
    rng = np.random.default_rng(340 + variant)
    c = FxCell()
    for _ in range(rng.integers(5, 9)):
        r = rng.uniform(0.14, 0.3)
        a = rng.uniform(0, 2 * math.pi)
        d = rng.uniform(0, 0.35)
        c.circle(d * math.cos(a), d * math.sin(a), r)
    for _ in range(3):
        a = rng.uniform(0, 2 * math.pi)
        x0, y0 = 0.35 * math.cos(a), 0.35 * math.sin(a)
        x1, y1 = 0.78 * math.cos(a + 0.4), 0.78 * math.sin(a + 0.4)
        c.line([(x0, y0), ((x0 + x1) / 2 + 0.1, (y0 + y1) / 2), (x1, y1)], 0.06)
    return c.done()


def fx_splinter(variant):
    rng = np.random.default_rng(350 + variant)
    c = FxCell()
    rot = rng.uniform(0, math.pi)
    w = rng.uniform(0.1, 0.17)
    ln = rng.uniform(0.75, 0.92)

    def R(x, y):
        return (x * math.cos(rot) - y * math.sin(rot), x * math.sin(rot) + y * math.cos(rot))

    pts = [(-ln, rng.uniform(-0.05, 0.05)), (-ln * 0.6, -w), (ln * 0.3, -w * 1.1), (ln, rng.uniform(-0.08, 0.02)),
           (ln * 0.7, w * 0.6), (ln * 0.75, w * 1.1), (-ln * 0.5, w)]
    c.poly([R(*p) for p in pts])
    c.shade_poly([R(-ln, 0.0), R(ln, 0.0), R(ln * 0.75, w * 1.2), R(-ln * 0.5, w * 1.1)])
    c.line([R(-ln * 0.5, -w * 0.3), R(ln * 0.5, -w * 0.4)], 0.025, lit=False, dark=True)
    return c.done()


def fx_drop(variant):
    c = FxCell()
    if variant == 0:
        c.circle(0, 0.18, 0.5)
        c.poly([(-0.43, 0.0), (0.0, -0.78), (0.43, 0.0)])
    elif variant == 1:
        c.circle(0, 0, 0.55)
    elif variant == 2:
        c.circle(-0.25, 0.15, 0.36)
        c.circle(0.35, -0.3, 0.22)
    else:
        c.circle(0, 0, 0.42)
        c.circle(0.3, 0.2, 0.25)
        c.circle(-0.28, 0.22, 0.2)
    c.shade_poly([(-1, 0.35), (1, 0.0), (1, 1), (-1, 1)])
    hx, hy = (-0.16, -0.05) if variant == 0 else (-0.2, -0.2)
    c.d.ellipse([*c.p(hx - 0.1, hy - 0.14), *c.p(hx + 0.1, hy + 0.14)], fill=0)
    return c.done()


def fx_streak(vertical):
    c = FxCell()
    if vertical:
        c.poly([(0, -0.98), (0.16, -0.5), (0.2, 0.3), (0, 0.98), (-0.2, 0.3), (-0.16, -0.5)])
    else:
        c.poly([(-0.98, 0), (-0.5, -0.16), (0.3, -0.2), (0.98, 0), (0.3, 0.2), (-0.5, 0.16)])
    return c.done(soft=0.6)


def fx_splash(frame):
    """Всплеск капли: дно на нижнем крае клетки. 0 — бугорок, 1–2 — две дуги воды и брызги, 3 — круг и капли."""
    c = FxCell()
    base = 0.8
    if frame == 0:
        c.poly([(-0.3, base), (-0.14, base - 0.22), (0.0, base - 0.3), (0.14, base - 0.22), (0.3, base)])
        for x, y in ((-0.18, base - 0.45), (0.04, base - 0.55), (0.22, base - 0.42)):
            c.circle(x, y, 0.06, shade=False)
    elif frame in (1, 2):
        wide = 0.5 if frame == 1 else 0.7
        tall = 0.55 if frame == 1 else 0.7
        width = 0.13 if frame == 1 else 0.08
        for side in (-1, 1):
            arc = [(side * (0.08 + wide * t), base - tall * math.sin(t * math.pi * 0.62)) for t in np.linspace(0, 1, 10)]
            c.line(arc, width)
        c.line([(0, base), (0, base - tall * 0.55)], width * 0.9)
        c.d.ellipse([*c.p(-wide * 0.6, base - 0.06), *c.p(wide * 0.6, base + 0.06)], fill=255)
        c.poly([(-wide - 0.1, base), (wide + 0.1, base), (wide * 0.6, base - 0.1), (-wide * 0.6, base - 0.1)])
        lift = 0.12 if frame == 1 else 0.3
        for x, y in ((-wide * 1.1, tall + lift * 0.6), (-0.15, tall * 0.8 + lift), (0.0, tall * 0.6 + lift * 1.4),
                     (0.18, tall * 0.85 + lift), (wide * 1.1, tall + lift * 0.5)):
            c.circle(x, base - y, 0.055, shade=False)
    else:
        c.a.ellipse([*c.p(-0.75, base - 0.12), *c.p(0.75, base + 0.12)], outline=255, width=int(c.s(0.06)))
        c.l.ellipse([*c.p(-0.75, base - 0.12), *c.p(0.75, base + 0.12)], outline=255, width=int(c.s(0.06)))
        for x, y in ((-0.5, base - 0.6), (0.1, base - 0.75), (0.45, base - 0.5)):
            c.circle(x, y, 0.045, shade=False)
    return c.done()


def fx_soft():
    ys, xs = np.mgrid[0:FX_CELL, 0:FX_CELL].astype(np.float32) + 0.5
    r = np.hypot(xs - FX_CELL / 2, ys - FX_CELL / 2) / (FX_CELL / 2)
    t = np.clip((1.0 - r) / 0.55, 0.0, 1.0)
    a = t * t * (3 - 2 * t)
    rgba = np.zeros((FX_CELL, FX_CELL, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)
    return rgba


def fx_ring():
    c = FxCell()
    c.a.ellipse([*c.p(-0.9, -0.9), *c.p(0.9, 0.9)], outline=255, width=int(c.s(0.12)))
    c.l.ellipse([*c.p(-0.9, -0.9), *c.p(0.9, 0.9)], outline=255, width=int(c.s(0.12)))
    return c.done()


def fx_atlas():
    rows = [
        [fx_puff(f) for f in range(8)],
        [fx_star(v) for v in range(8)],
        [fx_paper(v) for v in range(8)],
        [fx_feather(v) for v in range(8)],
        [fx_straw(v) for v in range(8)],
        [fx_fluff(v) for v in range(8)],
        [fx_splinter(v) for v in range(4)] + [fx_drop(v) for v in range(4)],
        [fx_streak(True), fx_streak(False)] + [fx_splash(f) for f in range(4)] + [fx_soft(), fx_ring()],
    ]
    atlas = np.zeros((FX_CELL * 8, FX_CELL * 8, 4), np.uint8)
    for j, row in enumerate(rows):
        for i, cell in enumerate(row):
            atlas[j * FX_CELL:(j + 1) * FX_CELL, i * FX_CELL:(i + 1) * FX_CELL] = cell
    return atlas


def impact_frame(frame, size=256):
    """Комиксная звёздочка удара, 4 кадра: вспышка, большая звезда, рваный контур, разлёт штрихов."""
    s = 4
    n = size * s
    img = Image.new("L", (n, n), 0)
    shade = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(img)
    ds = ImageDraw.Draw(shade)
    rng = np.random.default_rng(360)
    spikes = 9
    jitter = rng.uniform(0.82, 1.12, spikes)

    def burst(outer, inner):
        pts = []
        for k in range(spikes * 2):
            a = math.pi * k / spikes - math.pi / 2 + 0.12
            r = outer * jitter[k // 2] if k % 2 == 0 else inner
            pts.append((n / 2 + r * n / 2 * math.cos(a), n / 2 + r * n / 2 * math.sin(a)))
        return pts

    if frame == 0:
        d.polygon(burst(0.55, 0.26), fill=255)
    elif frame == 1:
        d.polygon(burst(0.95, 0.5), fill=255)
        ds.polygon(burst(0.95, 0.5), fill=255)
        ds.polygon(burst(0.72, 0.36), fill=0)
    elif frame == 2:
        d.polygon(burst(0.98, 0.56), fill=255)
        d.polygon(burst(0.78, 0.44), fill=0)
        ds.polygon(burst(0.98, 0.56), fill=120)
    else:
        for k in range(spikes):
            a = math.pi * 2 * k / spikes - math.pi / 2 + 0.12
            r0, r1 = 0.68 * jitter[k], 0.95 * jitter[k]
            d.line([(n / 2 + r0 * n / 2 * math.cos(a), n / 2 + r0 * n / 2 * math.sin(a)),
                    (n / 2 + min(0.99, r1) * n / 2 * math.cos(a), n / 2 + min(0.99, r1) * n / 2 * math.sin(a))],
                   fill=255, width=int(0.045 * n))
    alpha = np.asarray(img.resize((size, size), Image.LANCZOS), np.float32) / 255.0
    sh = np.asarray(shade.filter(ImageFilter.GaussianBlur(2)).resize((size, size), Image.LANCZOS), np.float32) / 255.0
    rgba = np.zeros((size, size, 4), np.uint8)
    rgba[..., :3] = np.clip((1.0 - 0.42 * sh) * 255 + 0.5, 0, 255).astype(np.uint8)[..., None]
    rgba[..., 3] = np.clip(alpha * 255 + 0.5, 0, 255).astype(np.uint8)
    return rgba


def impact_sheet():
    return np.concatenate([impact_frame(f) for f in range(4)], axis=1)


def trail_chalk(w=256, h=64):
    """След мяча: плотная середина и две штриховые полоски по краям, к хвосту штрихи редеют.
    U — вдоль следа (0 у мяча), V — поперёк."""
    img = Image.new("L", (w * 4, h * 4), 0)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, h * 4 * 0.32, w * 4, h * 4 * 0.68], radius=h * 4 * 0.18, fill=255)
    rng = np.random.default_rng(370)
    for y in (0.12, 0.88):
        x = 0.0
        while x < w * 4:
            t = x / (w * 4)
            dash = (0.22 - 0.14 * t) * w * 4
            gap = (0.03 + 0.12 * t) * w * 4 * rng.uniform(0.7, 1.3)
            d.rounded_rectangle([x, h * 4 * (y - 0.06), min(w * 4, x + dash), h * 4 * (y + 0.06)], radius=h * 4 * 0.06, fill=255)
            x += dash + gap
    a = np.asarray(img.resize((w, h), Image.LANCZOS), np.float32) / 255.0
    return a


def ground_cracks(variant, size=256):
    """Трещины в асфальте от удара Физрука: светлая вмятина посередине и ломаные трещины во все стороны,
    к концам тоньше, с короткими отростками."""
    n = size * 4
    img = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(380 + variant)
    c = n / 2
    dent_r = n * 0.13
    dent = [(c + dent_r * rng.uniform(0.75, 1.1) * math.cos(t), c + dent_r * rng.uniform(0.75, 1.1) * math.sin(t))
            for t in np.linspace(0, 2 * math.pi, 11, endpoint=False)]
    d.polygon(dent, fill=95)

    def crack(x, y, ang, length, width, branches):
        steps = 7
        base = ang
        pts = [(x, y)]
        for k in range(steps):
            # ломаная: держит общее направление, на изломах резко дёргается в сторону
            ang = base + rng.normal(0.0, 0.08) + (rng.choice([-1, 1]) * rng.uniform(0.25, 0.5) if k % 2 else 0.0)
            seg = length / steps * rng.uniform(0.7, 1.3)
            x += math.cos(ang) * seg * n / 2
            y += math.sin(ang) * seg * n / 2
            pts.append((x, y))
        for k in range(steps):
            w = max(1, int(width * n * (1.0 - k / steps) + 1))
            d.line([pts[k], pts[k + 1]], fill=255, width=w)
        for _ in range(branches):
            k = int(rng.integers(2, steps - 2))
            side = 1 if rng.random() < 0.5 else -1
            bx, by = pts[k]
            crack(bx, by, base + side * rng.uniform(0.5, 0.9), length * rng.uniform(0.25, 0.4), width * (1.0 - k / steps) * 0.75, 0)

    count = 6 + variant % 3
    for i in range(count):
        ang = 2 * math.pi * i / count + rng.normal(0.0, 0.22)
        crack(c + math.cos(ang) * dent_r * 0.35, c + math.sin(ang) * dent_r * 0.35, ang,
              rng.uniform(0.55, 0.9), 0.02, int(rng.integers(1, 3)))
    a = np.asarray(img.resize((size, size), Image.LANCZOS), np.float32) / 255.0
    rgba = np.zeros((size, size, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = np.clip(a * 255 + 0.5, 0, 255).astype(np.uint8)
    return rgba


def cracks_sheet():
    """2×2 варианта трещин; частица берёт случайный кадр."""
    top = np.concatenate([ground_cracks(0), ground_cracks(1)], axis=1)
    bottom = np.concatenate([ground_cracks(2), ground_cracks(3)], axis=1)
    return np.concatenate([top, bottom], axis=0)


def write_fx():
    save_rgba("T_FxAtlas.png", fx_atlas())
    save_rgba("T_Impact.png", impact_sheet())
    save("T_TrailChalk.png", trail_chalk())
    save_rgba("T_Cracks.png", cracks_sheet())


if __name__ == "__main__":
    import sys
    if len(sys.argv) > 1 and sys.argv[1] == "fx":
        write_fx()
        sys.exit(0)
    save("T_BlobShadow.png", blob_shadow())
    save("T_MomSilhouette.png", mom_silhouette())
    save("T_ChalkCross.png", chalk_cross())
    save("T_ChalkSpiral.png", chalk_spiral())
    for i, text in enumerate(("раз", "два", "три!", "БАХ!", "ЗАМРИ!")):
        save(f"T_Popup_{i}.png", word(text, seed=110 + i))
    save("T_Popup_5.png", heart())
    save("T_RadioWaves.png", radio_waves())
    save("T_Handprints.png", handprints())
    save("T_VHS.png", vhs())
    save_rgba("T_Photo.png", photo())
    for k in range(3):
        save_rgba(f"T_Tamagotchi_{k}.png", tamagotchi_face(k))
    save_rgba("T_Elastic.png", elastic())
    save("T_SpikyWarning.png", spiky_warning())
    write_fx()

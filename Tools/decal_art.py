#!/usr/bin/env python3
"""
Bouncer: атлас надписей и рисунков для моделей (T_Decals) — вывески, цифры на монетках, граффити.

В моделях из Blender надпись — это прямоугольник из двух треугольников с материалом M_Decals:
UV0 указывает на ячейку палитры (цвет и смена времени суток, как у всей модели), UV1 — на место в этом атласе.
Атлас — только маска (белое на чёрном), цвет даёт палитра. Шейдер Bouncer/PaletteDecal отрезает всё вне маски.

Запуск из корня репозитория:  python3 Tools/decal_art.py
Пишет Assets/_Project/Art/Decals/T_Decals.png и Tools/decal_atlas.json (где что лежит, для ba_lib в Bouncer.blend).
Новые надписи добавляй в конец ENTRIES: раскладка идёт по порядку, и старые места в атласе не сдвинутся.
Нужен Pillow. Шрифты: Caveat из проекта (почерк), PT Sans из macOS (вывески, свободная лицензия ParaType).
"""
import json
import math
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "Assets", "_Project", "Art", "Decals", "T_Decals.png")
OUT_JSON = os.path.join(ROOT, "Tools", "decal_atlas.json")

W = 2048
PAD = 16                       # поля между надписями: мип-уровни не должны смешивать соседей
PT_SANS = "/System/Library/Fonts/Supplemental/PTSans.ttc"
CAVEAT = os.path.join(ROOT, "Assets", "_Project", "Art", "Fonts", "Caveat.ttf")


def sign_font(size):
    return ImageFont.truetype(PT_SANS, size, index=2)          # PT Sans Narrow Bold


def bold_font(size):
    return ImageFont.truetype(PT_SANS, size, index=7)          # PT Sans Bold


def hand_font(size):
    return ImageFont.truetype(CAVEAT, size)


# ================================================================ рисовалки (маска 'L': 255 — надпись)

def text_img(text, font, spacing=0.0, line_gap=0.12):
    lines = text.split("\n")
    boxes = [font.getbbox(line) for line in lines]
    widths = [b[2] - b[0] + spacing * max(0, len(line) - 1) for b, line in zip(boxes, lines)]
    asc, desc = font.getmetrics()
    lh = asc + desc
    w = int(max(widths)) + 8
    h = int(lh * len(lines) + lh * line_gap * (len(lines) - 1)) + 8
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    y = 4
    for line, lw in zip(lines, widths):
        x = (w - lw) / 2
        if spacing:
            for ch in line:
                d.text((x, y), ch, font=font, fill=255)
                x += font.getlength(ch) + spacing
        else:
            d.text((x, y), line, font=font, fill=255)
        y += lh * (1 + line_gap)
    return img.crop(img.getbbox())


def star_pts(cx, cy, r, inner=0.42, rot=-90):
    pts = []
    for k in range(10):
        a = math.radians(rot + k * 36)
        rr = r if k % 2 == 0 else r * inner
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    return pts


def heart_pts(cx, cy, s, n=60):
    pts = []
    for k in range(n):
        t = 2 * math.pi * k / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((cx + x * s, cy - y * s))
    return pts


def stroke(d, pts, width, closed=False):
    pts = list(pts) + ([pts[0]] if closed else [])
    d.line(pts, fill=255, width=width, joint="curve")
    r = width / 2
    for x, y in (pts[0], pts[-1]):
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)


def wheat(d, cx, cy, radius, grain, side):
    """Колос вдоль дуги сбоку от номинала: зёрна-эллипсы, повёрнутые по касательной."""
    for k in range(6):
        a = math.radians(-50 + k * 22)
        x = cx + side * radius * math.cos(a)
        y = cy - radius * math.sin(a)
        tilt = -side * a
        pts = []
        for j in range(12):
            t = 2 * math.pi * j / 12
            ex, ey = grain * 0.42 * math.cos(t), grain * math.sin(t)
            pts.append((x + ex * math.cos(tilt) - ey * math.sin(tilt), y + ex * math.sin(tilt) + ey * math.cos(tilt)))
        d.polygon(pts, fill=255)


def coin_front(numeral, word, size=384):
    img = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(img)
    c = size / 2
    num = text_img(numeral, bold_font(int(size * 0.5)))
    img.paste(num, (int(c - num.width / 2), int(c - num.height / 2 - size * 0.06)), num)
    wd = text_img(word, sign_font(int(size * 0.12)), spacing=4)
    img.paste(wd, (int(c - wd.width / 2), int(c + size * 0.2)), wd)
    for side in (-1, 1):
        wheat(d, c, c + size * 0.02, size * 0.36, size * 0.045, side)
    return img


def coin_back(size=384):
    img = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(img)
    c = size / 2
    d.polygon(star_pts(c, c + size * 0.02, size * 0.2), fill=255)
    for k in range(18):
        a = 2 * math.pi * k / 18
        x, y = c + size * 0.36 * math.cos(a), c + size * 0.36 * math.sin(a)
        r = size * 0.018
        d.ellipse((x - r, y - r, x + r, y + r), fill=255)
    return img


def shape(size, draw):
    img = Image.new("L", size, 0)
    draw(ImageDraw.Draw(img), size)
    return img.crop(img.getbbox())


def _heart_arrow(d, s):
    w, h = s
    stroke(d, heart_pts(w * 0.5, h * 0.45, w / 44), 12, closed=True)
    stroke(d, [(w * 0.1, h * 0.8), (w * 0.9, h * 0.15)], 9)
    tip = (w * 0.9, h * 0.15)
    stroke(d, [tip, (tip[0] - 40, tip[1] + 4)], 9)
    stroke(d, [tip, (tip[0] - 10, tip[1] + 38)], 9)


def _scribble(d, s):
    w, h = s
    pts = [(20 + i * (w - 40) / 60, h / 2 + h * 0.3 * math.sin(i * 0.55) * (0.6 + 0.4 * math.sin(i * 0.13))) for i in range(61)]
    stroke(d, pts, 14)


def _arrow(d, s):
    w, h = s
    stroke(d, [(20, h * 0.6), (w * 0.45, h * 0.45), (w - 40, h * 0.5)], 14)
    tip = (w - 30, h * 0.5)
    stroke(d, [tip, (tip[0] - 60, tip[1] - 45)], 14)
    stroke(d, [tip, (tip[0] - 60, tip[1] + 45)], 14)


def _smiley(d, s):
    w, h = s
    c, r = (w / 2, h / 2), min(w, h) * 0.42
    stroke(d, [(c[0] + r * math.cos(a), c[1] + r * math.sin(a)) for a in [2 * math.pi * k / 48 for k in range(48)]], 12, closed=True)
    for sx in (-1, 1):
        d.ellipse((c[0] + sx * r * 0.35 - 12, c[1] - r * 0.3 - 16, c[0] + sx * r * 0.35 + 12, c[1] - r * 0.3 + 16), fill=255)
    stroke(d, [(c[0] + r * 0.55 * math.cos(a), c[1] + r * 0.55 * math.sin(a)) for a in
               [math.radians(20 + 140 * k / 16) for k in range(17)]], 11)


def _star(d, s):
    w, h = s
    d.polygon(star_pts(w / 2, h / 2 + h * 0.04, min(w, h) * 0.48), fill=255)


def _bolt(d, s):
    w, h = s
    pts = [(0.55, 0.0), (0.12, 0.56), (0.42, 0.56), (0.3, 1.0), (0.88, 0.38), (0.56, 0.38), (0.78, 0.0)]
    d.polygon([(x * w, y * h) for x, y in pts], fill=255)


# ================================================================ содержимое атласа (новое — только в конец!)

def entries():
    return [
        ("sign_soyuzpechat", text_img("СОЮЗПЕЧАТЬ", sign_font(170), spacing=6)),
        ("sign_gazety", text_img("ГАЗЕТЫ", sign_font(140), spacing=4)),
        ("sign_zhurnaly", text_img("ЖУРНАЛЫ", sign_font(140), spacing=4)),
        ("sign_no_entry", text_img("ВХОД\nВОСПРЕЩЁН", sign_font(96))),
        ("gum_bum", text_img("БУМ", sign_font(150), spacing=6)),
        ("label_lemonade", text_img("ЛИМОНАД", sign_font(110))),
        ("coin_1_tiyn", coin_front("1", "ТИЫН")),
        ("coin_5_tiyn", coin_front("5", "ТИЫН")),
        ("coin_1_tenge", coin_front("1", "ТЕНГЕ")),
        ("coin_back", coin_back()),
        ("tag_vitya_lena", text_img("Витя + Лена", hand_font(120))),
        ("tag_dvor", text_img("Двор — чемпион!", hand_font(120))),
        ("tag_hockey", text_img("ХОККЕЙ", hand_font(130))),
        ("tag_5b", text_img("5 «Б»", hand_font(130))),
        ("tag_my_tut", text_img("Мы тут были", hand_font(120))),
        ("tag_seryoga", text_img("Серёга", hand_font(130))),
        ("tag_tut_byl", text_img("Тут был Я", hand_font(120))),
        ("doodle_heart", shape((360, 300), _heart_arrow)),
        ("doodle_scribble", shape((520, 180), _scribble)),
        ("doodle_arrow", shape((420, 180), _arrow)),
        ("doodle_smiley", shape((260, 260), _smiley)),
        ("shape_star", shape((200, 200), _star)),
        ("shape_bolt", shape((140, 240), _bolt)),
    ]


def pack(items):
    """Полки слева направо в порядке списка. Возвращает место каждой картинки и высоту атласа."""
    x, y, row_h = PAD, PAD, 0
    places = []
    for name, img in items:
        if x + img.width + PAD > W:
            x, y, row_h = PAD, y + row_h + PAD, 0
        places.append((name, img, x, y))
        x += img.width + PAD
        row_h = max(row_h, img.height)
    height = y + row_h + PAD
    h = 256
    while h < height:
        h *= 2
    return places, h


def main():
    places, H = pack(entries())
    atlas = Image.new("L", (W, H), 0)
    layout = {}
    for name, img, x, y in places:
        atlas.paste(img, (x, y))
        # UV: начало координат внизу слева, как в Blender и Unity.
        layout[name] = {"uv": [x / W, 1 - (y + img.height) / H, (x + img.width) / W, 1 - y / H],
                        "aspect": round(img.width / img.height, 4)}
    os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
    atlas.save(OUT_PNG, optimize=True)
    with open(OUT_JSON, "w", encoding="utf-8") as f:
        json.dump({"size": [W, H], "entries": layout}, f, ensure_ascii=False, indent=1)
    print(f"wrote {OUT_PNG} ({W}x{H}), {len(layout)} entries")


if __name__ == "__main__":
    main()

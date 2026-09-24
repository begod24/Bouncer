#!/usr/bin/env python3
"""
Bouncer: сплэш-скрин и иконка приложения из 3D-рендеров.

Картинки рендерит Blender из Bouncer.blend (скрипт ba_branding.py там же):
  сцена Splash — 3840×2160, сцена Icon — 2048×2048 на прозрачном фоне.
Этот скрипт собирает из них PNG в Art/UI/Branding (там их импортирует ArtImportPostprocessor):
  splash: Splash_Background.png — картинка без текста, 1920×1080, с виньеткой;
          Splash_Logo_EN/RU.png — название на прозрачном фоне;
          Splash_EN/RU.png      — картинка с названием, готовый сплэш (EN стоит в Player Settings).
          Название — как на титульном экране игры: Neucha, цвет мела F4F2EC, мягкая тень 55 %.
  icon:   App_Icon.png — 1024×1024 по сетке иконок macOS: плашка 824 px с непрерывными углами,
          закатный градиент, мягкая тень. Без этой формы macOS 26+ кладёт иконку в серую плашку.

Запуск из корня репозитория:
  python3 Tools/branding_art.py splash путь/к/рендеру_Splash.png
  python3 Tools/branding_art.py icon путь/к/рендеру_Icon.png
Нужны numpy и Pillow.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Branding")
FONTS = os.path.join(ROOT, "Assets", "_Project", "Art", "Fonts")
W, H = 1920, 1080
CHALK = (244, 242, 236)                 # «white» из палитры, цвет заголовка в игре

TITLES = {
    "EN": "IT'S OUR FIELD",
    "RU": "ВЫШИБАЛЫ",
}

ICON = 1024
ICON_BOX = (100, 100, 924, 924)         # сетка macOS (Big Sur и новее): плашка 824 px в холсте 1024
ICON_CORNER = 5.0                       # степень суперэллипса: углы плавные, как у системных иконок
ICON_SKY = [(0.0, "3E4FA6"), (0.45, "7E7DCB"), (0.8, "D6A6C8"), (1.0, "F6BFAE")]
ICON_GLOW = ((0.49, 0.36), 0.5, "FFC7A0", 0.55)   # тёплое свечение за головой: центр, радиус, цвет, сила


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def hex_rgb(h):
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], np.float32)


# ================================================================ splash

def grade(img):
    """Виньетка и лёгкое затемнение неба под заголовком."""
    a = np.asarray(img, np.float32) / 255.0
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    nx, ny = (xx - W / 2) / (W / 2), (yy - H / 2) / (H / 2)
    r = np.sqrt(nx * nx * 0.85 + ny * ny) / np.sqrt(1.85)
    k = 1.0 - 0.28 * smoothstep(0.35, 1.0, r)
    k *= 1.0 - 0.10 * smoothstep(0.34, 0.0, yy / H)      # верх чуть темнее: белому тексту нужен контраст
    a = a * k[..., None]
    return Image.fromarray(np.clip(a * 255.0 + 0.5, 0, 255).astype(np.uint8))


def title_layer(text, size=210, center=(W // 2, 150), ss=2):
    """RGBA-слой с надписью и тенью на весь кадр. Рисуем в ss раз крупнее и уменьшаем — края мягче."""
    font = ImageFont.truetype(os.path.join(FONTS, "Neucha.ttf"), size * ss)
    x0, y0, x1, y1 = font.getbbox(text)
    cx, cy = center[0] * ss, center[1] * ss
    pos = (cx - (x0 + x1) / 2, cy - (y0 + y1) / 2)
    mask = Image.new("L", (W * ss, H * ss), 0)
    ImageDraw.Draw(mask).text(pos, text, font=font, fill=255)
    # тень как у TMP-материала «Neucha SDF - Shadow»: чуть толще, размыта, сдвинута вправо-вниз, 55 %
    sh = mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(7 * ss))
    sh = ImageChops.offset(sh, 9 * ss, 9 * ss)
    sh = sh.point(lambda v: int(v * 0.55))
    layer = Image.new("RGBA", (W * ss, H * ss), (0, 0, 0, 0))
    layer.putalpha(sh)
    face = Image.new("RGBA", (W * ss, H * ss), CHALK + (0,))
    face.putalpha(mask)
    layer = Image.alpha_composite(layer, face)
    return layer.resize((W, H), Image.LANCZOS)


def crop_alpha(img, margin=24):
    x0, y0, x1, y1 = img.getchannel("A").getbbox()
    return img.crop((max(0, x0 - margin), max(0, y0 - margin), min(img.width, x1 + margin),
                     min(img.height, y1 + margin)))


def splash(render_path):
    src = Image.open(render_path).convert("RGB")
    bg = grade(src.resize((W, H), Image.LANCZOS))
    bg.save(os.path.join(OUT, "Splash_Background.png"))
    print("wrote Splash_Background.png")
    for lang, text in TITLES.items():
        layer = title_layer(text)
        crop_alpha(layer).save(os.path.join(OUT, f"Splash_Logo_{lang}.png"))
        Image.alpha_composite(bg.convert("RGBA"), layer).convert("RGB").save(os.path.join(OUT, f"Splash_{lang}.png"))
        print(f"wrote Splash_Logo_{lang}.png, Splash_{lang}.png")


# ================================================================ icon

def superellipse_mask(size, box, n=ICON_CORNER, ss=4):
    x0, y0, x1, y1 = [c * ss for c in box]
    ys, xs = np.mgrid[0:size * ss, 0:size * ss].astype(np.float32) + 0.5
    u = np.abs((xs - (x0 + x1) / 2) / ((x1 - x0) / 2))
    v = np.abs((ys - (y0 + y1) / 2) / ((y1 - y0) / 2))
    m = ((u ** n + v ** n) <= 1.0).astype(np.uint8) * 255
    return Image.fromarray(m).resize((size, size), Image.LANCZOS)


def icon_background(w):
    """Закатное небо как на сплэше: вертикальный градиент и тёплое свечение за головой босса."""
    ys, xs = np.mgrid[0:w, 0:w].astype(np.float32) / w
    col = np.zeros((w, w, 3), np.float32)
    for (p0, c0), (p1, c1) in zip(ICON_SKY, ICON_SKY[1:]):
        t = np.clip((ys - p0) / (p1 - p0), 0, 1)[..., None]
        inside = ((ys >= p0) & (ys <= p1))[..., None]
        col = np.where(inside, hex_rgb(c0) * (1 - t) + hex_rgb(c1) * t, col)
    (gx, gy), radius, color, amount = ICON_GLOW
    g = np.clip(1 - np.sqrt((xs - gx) ** 2 + (ys - gy) ** 2) / radius, 0, 1) ** 2 * amount
    col = col * (1 - g[..., None]) + hex_rgb(color) * g[..., None]
    return Image.fromarray(np.clip(col + 0.5, 0, 255).astype(np.uint8)).convert("RGBA")


def icon(render_path):
    w = ICON_BOX[2] - ICON_BOX[0]
    art = Image.open(render_path).convert("RGBA").resize((w, w), Image.LANCZOS)
    body = Image.alpha_composite(icon_background(w), art)
    mask = superellipse_mask(ICON, ICON_BOX)
    canvas = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))     # мягкая тень под плашкой, как в шаблоне macOS
    shadow.putalpha(mask.point(lambda v: int(v * 0.32)))
    canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(14)), (0, 10))
    tile = Image.new("RGBA", (ICON, ICON), (0, 0, 0, 0))
    tile.paste(body, ICON_BOX[:2])
    tile.putalpha(ImageChops.multiply(tile.getchannel("A"), mask))
    canvas.alpha_composite(tile)
    canvas.save(os.path.join(OUT, "App_Icon.png"))
    print("wrote App_Icon.png")


if __name__ == "__main__":
    if len(sys.argv) < 3 or sys.argv[1] not in ("splash", "icon"):
        sys.exit(__doc__)
    if len(sys.argv) > 3:
        OUT = sys.argv[3]
    os.makedirs(OUT, exist_ok=True)
    (splash if sys.argv[1] == "splash" else icon)(sys.argv[2])

#!/usr/bin/env python3
"""
Bouncer: маленькие текстуры для мира (не интерфейс): тень вороны на асфальте, мама в окне в финале.

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

    circle(64, 22, 10)                      # пучок
    circle(64, 46, 19)                      # голова
    d.rectangle([54 * s, 56 * s, 74 * s, 80 * s], fill=255)                    # шея
    d.polygon([(40 * s, 74 * s), (88 * s, 74 * s), (112 * s, 128 * s), (16 * s, 128 * s)], fill=255)   # плечи
    circle(40, 80, 12)
    circle(88, 80, 12)
    d.line([(90 * s, 84 * s), (96 * s, 66 * s), (82 * s, 52 * s)], fill=255, width=12 * s, joint="curve")  # рука
    circle(80, 52, 8)                       # ладонь у рта
    img = img.filter(ImageFilter.GaussianBlur(3)).resize((size, size), Image.LANCZOS)
    return np.asarray(img, np.float32) / 255.0


if __name__ == "__main__":
    save("T_BlobShadow.png", blob_shadow())
    save("T_MomSilhouette.png", mom_silhouette())

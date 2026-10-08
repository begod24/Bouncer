"""Chalk-style titles for the trailer (RGBA overlays): a hand font, grainy chalk texture, a small 'boil'
(3 hand-drawn variants alternating), write-on reveal from the left. Used by edit.py."""
import math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

FONTS = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer/Assets/_Project/Art/Fonts/"
NEUCHA = FONTS + "Neucha.ttf"
CAVEAT = FONTS + "Caveat.ttf"
W, H = 1920, 1080

_cache = {}


def _font(path, size):
    key = (path, size)
    if key not in _cache:
        _cache[key] = ImageFont.truetype(path, size)
    return _cache[key]


def _grain(w, h, seed):
    rng = np.random.default_rng(seed)
    g = rng.random((h // 2 + 1, w // 2 + 1)).astype(np.float32)
    g = np.kron(g, np.ones((2, 2), np.float32))[:h, :w]
    # streaks along a slight diagonal like chalk dragged on asphalt
    s = rng.random((h // 6 + 1, w // 24 + 1)).astype(np.float32)
    s = np.kron(s, np.ones((6, 24), np.float32))[:h, :w]
    return np.clip(0.55 + 0.6 * g * (0.6 + 0.4 * s), 0, 1)


def text_layer(lines, seed=0, color=(250, 248, 240), shadow=True):
    """lines: list of (text, font_path, size, (cx, cy), rotation_deg). Returns a list of 3 'boil' RGBA frames."""
    frames = []
    for variant in range(3):
        rng = random.Random(seed * 31 + variant)
        mask = Image.new("L", (W, H), 0)
        for text, path, size, (cx, cy), rot in lines:
            f = _font(path, size)
            tmp = Image.new("L", (W, H), 0)
            d = ImageDraw.Draw(tmp)
            bbox = d.textbbox((0, 0), text, font=f, stroke_width=max(1, size // 40))
            tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
            x = cx - tw / 2 - bbox[0] + rng.uniform(-2, 2)
            y = cy - th / 2 - bbox[1] + rng.uniform(-2, 2)
            d.text((x, y), text, font=f, fill=255, stroke_width=max(1, size // 40), stroke_fill=255)
            tmp = tmp.rotate(rot + rng.uniform(-0.4, 0.4), resample=Image.BICUBIC, center=(cx, cy))
            mask = Image.fromarray(np.maximum(np.asarray(mask), np.asarray(tmp)))
        m = np.asarray(mask.filter(ImageFilter.GaussianBlur(0.8))).astype(np.float32) / 255.0
        m = m * _grain(W, H, seed * 7 + variant)
        m = np.clip((m - 0.18) * 1.5, 0, 1)
        rgba = np.zeros((H, W, 4), np.float32)
        rgba[..., :3] = np.array(color, np.float32) / 255.0
        rgba[..., 3] = m
        if shadow:
            sh = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(9))
            sh = np.asarray(sh).astype(np.float32) / 255.0 * 0.55
            sh = np.roll(np.roll(sh, 4, 0), 3, 1)
            a = rgba[..., 3]
            out_a = a + sh * (1 - a)
            rgb = (rgba[..., :3] * a[..., None]) / np.maximum(out_a[..., None], 1e-4)
            rgba[..., :3] = rgb
            rgba[..., 3] = out_a
        frames.append(rgba)
    return frames


def reveal(frame, t01, x0=0, x1=W, soft=60):
    """Write-on from the left: alpha multiplied by a soft edge moving from x0 to x1."""
    if t01 >= 1:
        return frame
    edge = x0 + (x1 - x0 + soft) * t01
    xs = np.arange(W, dtype=np.float32)
    k = np.clip((edge - xs) / soft, 0, 1)
    out = frame.copy()
    out[..., 3] *= k[None, :]
    return out


def bounds(lines):
    """Horizontal extent of the text, to time the reveal."""
    xs = []
    for text, path, size, (cx, cy), rot in lines:
        f = _font(path, size)
        d = ImageDraw.Draw(Image.new("L", (8, 8)))
        b = d.textbbox((0, 0), text, font=f)
        xs += [cx - (b[2] - b[0]) / 2, cx + (b[2] - b[0]) / 2]
    return int(min(xs)) - 20, int(max(xs)) + 20

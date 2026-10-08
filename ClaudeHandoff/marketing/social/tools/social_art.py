"""The game's look for social posts: everything lies on the yard asphalt. Chalk drawings and titles (fonts of
the game, Neucha + Caveat), the yellow wavy underline of the HUD, gum-wrapper cards (вкладыши), notebook pages,
film-camera prints with the orange date stamp. Pure PIL + numpy; every function takes a seed, so reruns match."""
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

REPO = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer/"
FONTS = REPO + "Assets/_Project/Art/Fonts/"
NEUCHA = FONTS + "Neucha.ttf"
CAVEAT = FONTS + "Caveat.ttf"
BRAND = REPO + "Assets/_Project/Art/UI/Branding/"

CHALK = (247, 245, 236)
YELLOW = (246, 206, 66)          # the HUD's wavy underline
PINK = (244, 150, 170)
SKY = (140, 200, 245)
INK = (34, 56, 150)              # ballpoint
RED_PEN = (205, 40, 45)
ASPHALT = (54, 54, 59)

_fonts = {}


def font(path, size):
    k = (path, size)
    if k not in _fonts:
        _fonts[k] = ImageFont.truetype(path, size)
    return _fonts[k]


def vnoise(w, h, cx, cy=None, rng=None):
    """Value noise in 0..1 with cells of cx x cy pixels (bicubic)."""
    rng = rng or np.random.default_rng(0)
    cy = cy or cx
    g = (rng.random((int(h / cy) + 3, int(w / cx) + 3)) * 255).astype(np.uint8)
    im = Image.fromarray(g).resize((int(w / cx + 3) * cx, int(h / cy + 3) * cy), Image.BICUBIC)
    return np.asarray(im, np.float32)[cy:cy + h, cx:cx + w] / 255.0


# ---------------------------------------------------------------- surfaces
def asphalt(w, h, seed=1, base=ASPHALT):
    """Yard asphalt: blotchy, grainy, light pebbles, a few hairline cracks. Float RGB 0..1."""
    rng = np.random.default_rng(seed)
    lum = 1 + (vnoise(w, h, 220, rng=rng) - 0.5) * 0.22 + (vnoise(w, h, 45, rng=rng) - 0.5) * 0.12
    lum += (rng.random((h, w)).astype(np.float32) - 0.5) * 0.16
    peb = (rng.random((h, w)) < 0.006).astype(np.float32) * rng.uniform(0.25, 0.7, (h, w)).astype(np.float32)
    peb = np.asarray(Image.fromarray((peb * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.7)),
                     np.float32) / 255 * 2.2
    dark = (rng.random((h, w)) < 0.01).astype(np.float32) * 0.25
    lum = lum + peb - dark
    img = np.ones((h, w, 3), np.float32) * (np.array(base, np.float32) / 255) * lum[..., None]
    cr = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(cr)
    r = random.Random(seed)
    for _ in range(2):
        x, y = r.uniform(0, w), r.uniform(0, h)
        a = r.uniform(0, math.pi * 2)
        pts = [(x, y)]
        for _ in range(8):
            a += r.uniform(-0.7, 0.7)
            x += math.cos(a) * r.uniform(8, 18)
            y += math.sin(a) * r.uniform(8, 18)
            pts.append((x, y))
        d.line(pts, fill=255, width=2)
    c = np.asarray(cr.filter(ImageFilter.GaussianBlur(1.0)), np.float32) / 255
    img = img * (1 - 0.25 * c[..., None])
    return np.clip(img, 0, 1)


def vignette(img, amount=0.35):
    h, w = img.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    r = np.hypot((xs - w / 2) / (w / 2), (ys - h / 2) / (h / 2)) / 1.414
    return img * (1 - amount * np.clip(r, 0, 1) ** 2.2)[..., None]


# ---------------------------------------------------------------- masks (L images drawn white on black)
class Mask:
    """A drawing layer; everything is drawn in white, then turned into chalk, ink or print."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.im = Image.new("L", (w, h), 0)
        self.d = ImageDraw.Draw(self.im)

    def text(self, text, path, size, center, rot=0.0, anchor="mm", stroke=0, spacing=0.0):
        """Text centred at `center` (anchor of the text box), rotated by rot degrees (counter-clockwise)."""
        f = font(path, size)
        tmp = Image.new("L", (1, 1))
        b = ImageDraw.Draw(tmp).textbbox((0, 0), text, font=f, anchor="lt", stroke_width=stroke)
        pad = size
        lw, lh = b[2] - b[0] + pad * 2, b[3] - b[1] + pad * 2
        layer = Image.new("L", (lw, lh), 0)
        ld = ImageDraw.Draw(layer)
        if spacing:
            x = pad
            for ch in text:
                ld.text((x, pad - b[1]), ch, font=f, fill=255, anchor="lt", stroke_width=stroke, stroke_fill=255)
                x += f.getlength(ch) + spacing
        else:
            ld.text((pad - b[0], pad - b[1]), text, font=f, fill=255, anchor="lt", stroke_width=stroke,
                    stroke_fill=255)
        ax = {"l": pad, "m": lw / 2, "r": lw - pad}[anchor[0]]
        ay = {"t": pad, "m": lh / 2, "b": lh - pad}[anchor[1]]
        # rotate around the anchor point
        big = Image.new("L", (lw * 3, lh * 3), 0)
        big.paste(layer, (int(lw * 1.5 - ax), int(lh * 1.5 - ay)))
        big = big.rotate(rot, resample=Image.BICUBIC, center=(lw * 1.5, lh * 1.5))
        self.paste(big, (int(center[0] - lw * 1.5), int(center[1] - lh * 1.5)))
        return f.getlength(text) + spacing * len(text)

    def paste(self, layer, xy):
        tmp = Image.new("L", (self.w, self.h), 0)
        tmp.paste(layer, xy)
        self.im = ImageChops.lighter(self.im, tmp)
        self.d = ImageDraw.Draw(self.im)

    def line(self, pts, width, seed=0, wobble=1.5):
        r = random.Random(seed)
        pts = [(x + r.uniform(-wobble, wobble), y + r.uniform(-wobble, wobble)) for x, y in pts]
        self.d.line(pts, fill=255, width=int(width), joint="curve")
        for p in (pts[0], pts[-1]):
            self.d.ellipse((p[0] - width / 2, p[1] - width / 2, p[0] + width / 2, p[1] + width / 2), fill=255)

    def wave(self, x0, x1, y, width=8, amp=7, period=34, seed=0, rot=0.0):
        """The HUD's wavy underline."""
        n = max(8, int((x1 - x0) / 4))
        pts = [(x0 + (x1 - x0) * i / n, y + amp * math.sin((x1 - x0) * i / n / period * 2 * math.pi)) for i in range(n + 1)]
        if rot:
            cx, cy = (x0 + x1) / 2, y
            a = math.radians(-rot)
            pts = [(cx + (x - cx) * math.cos(a) - (yy - cy) * math.sin(a), cy + (x - cx) * math.sin(a) + (yy - cy) * math.cos(a))
                   for x, yy in pts]
        self.line(pts, width, seed, wobble=0.8)

    def arrow(self, pts, width=8, head=34, seed=0):
        """Curved chalk arrow through pts (quadratic spline), head at the end."""
        if len(pts) == 3:
            (x0, y0), (cx, cy), (x1, y1) = pts
            pts = [((1 - t) ** 2 * x0 + 2 * (1 - t) * t * cx + t * t * x1, (1 - t) ** 2 * y0 + 2 * (1 - t) * t * cy + t * t * y1)
                   for t in np.linspace(0, 1, 30)]
        self.line(pts, width, seed)
        (ax, ay), (bx, by) = pts[-4], pts[-1]
        a = math.atan2(by - ay, bx - ax)
        for s in (-1, 1):
            b = a + math.pi + s * math.radians(28)
            self.line([(bx, by), (bx + head * math.cos(b), by + head * math.sin(b))], width, seed + s)

    def star(self, cx, cy, r, width=6, seed=0, fill=False, rot=0.0):
        pts = []
        for i in range(11):
            rr = r if i % 2 == 0 else r * 0.45
            a = math.radians(-90 + i * 36 + rot)
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        if fill:
            self.d.polygon(pts, fill=255)
        self.line(pts, width, seed, wobble=1.0)

    def circle(self, cx, cy, rx, ry=None, width=6, turns=1.12, seed=0):
        ry = ry or rx
        r = random.Random(seed)
        a0 = r.uniform(0, 6.28)
        pts = []
        n = int(60 * turns)
        for i in range(n + 1):
            t = a0 + turns * 2 * math.pi * i / n
            k = 1 + 0.05 * math.sin(i * 0.37) + 0.04 * (i / n)
            pts.append((cx + rx * k * math.cos(t), cy + ry * k * math.sin(t)))
        self.line(pts, width, seed, wobble=0.6)

    def ball(self, cx, cy, r, width=6, seed=0):
        self.circle(cx, cy, r, width=width, turns=1.02, seed=seed)
        self.line([(cx - r * 0.95, cy - r * 0.2), (cx + r * 0.95, cy + r * 0.25)], width * 0.8, seed + 1)
        self.line([(cx - r * 0.3, cy - r * 0.92), (cx - r * 0.05, cy + r * 0.95)], width * 0.8, seed + 2)
        for k in range(3):   # speed lines
            y = cy - r * 0.5 + k * r * 0.5
            self.line([(cx - r * 1.3 - k * 6, y), (cx - r * 2.2 - k * 12, y + 4)], width * 0.7, seed + 3 + k)

    def hopscotch(self, x, y, cell, width=7, seed=0):
        """Классики: 1, 2, 3|4, 5, 6|7, 8, then the rounded «небо» (x, y = bottom centre, upward)."""
        c = cell
        rows = [1, 1, 2, 1, 2, 1]
        k, yy = 1, y
        for n in rows:
            if n == 1:
                self.line([(x - c / 2, yy), (x + c / 2, yy), (x + c / 2, yy - c), (x - c / 2, yy - c), (x - c / 2, yy)], width, seed + k)
                self.text(str(k), NEUCHA, int(c * 0.5), (x, yy - c / 2))
                k += 1
            else:
                self.line([(x - c, yy), (x + c, yy), (x + c, yy - c), (x - c, yy - c), (x - c, yy)], width, seed + k)
                self.line([(x, yy), (x, yy - c)], width, seed + k + 50)
                self.text(str(k), NEUCHA, int(c * 0.5), (x - c / 2, yy - c / 2))
                self.text(str(k + 1), NEUCHA, int(c * 0.5), (x + c / 2, yy - c / 2))
                k += 2
            yy -= c
        pts = [(x + c / 2 * math.cos(a), yy - c * 0.1 - c * 0.75 * math.sin(a)) for a in np.linspace(0, math.pi, 24)]
        self.line(pts, width, seed + 99)
        self.text("НЕБО", NEUCHA, int(c * 0.28), (x, yy - c * 0.38))

    def array(self):
        return np.asarray(self.im, np.float32) / 255.0


# ---------------------------------------------------------------- materials for masks
def chalk(m, seed=0, grain=0.5, pits=0.07):
    """Mask (0..1) -> chalk coverage: soft edge, grain, streaks dragged along the asphalt, pits."""
    h, w = m.shape
    rng = np.random.default_rng(seed + 1000)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.9)), np.float32) / 255
    fine = rng.random((h, w)).astype(np.float32)
    fine = np.asarray(Image.fromarray((fine * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255
    fine = (fine - fine.min()) / max(1e-6, fine.max() - fine.min())
    streak = vnoise(w, h, 3, 14, rng)
    tex = 0.6 * fine + 0.4 * streak
    m = m * np.clip(1 - grain + grain * tex * 1.55, 0, 1)
    m = np.where(rng.random((h, w)) < pits, m * 0.3, m)
    return np.clip((m - 0.1) * 1.3, 0, 1)


def ink(m, seed=0):
    """Ballpoint: a hair softer, a little uneven pressure."""
    h, w = m.shape
    rng = np.random.default_rng(seed + 2000)
    m = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255
    return np.clip(m * (0.78 + 0.22 * vnoise(w, h, 30, rng=rng)), 0, 1)


def put(img, cover, color, opacity=1.0, shadow=0.0, shadow_blur=6, shadow_off=(4, 5)):
    """Composite a solid colour through a coverage map; optional soft drop shadow."""
    if shadow:
        sh = np.asarray(Image.fromarray((cover * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(shadow_blur)),
                        np.float32) / 255 * shadow
        sh = np.roll(np.roll(sh, shadow_off[1], 0), shadow_off[0], 1)
        img = img * (1 - sh[..., None])
    a = (cover * opacity)[..., None]
    return img * (1 - a) + (np.array(color, np.float32) / 255) * a


def chalk_put(img, mask, color=CHALK, seed=0, opacity=0.95, grain=0.5):
    return put(img, chalk(mask.array(), seed, grain), color, opacity, shadow=0.35)


# ---------------------------------------------------------------- images
def to_f(im):
    return np.asarray(im.convert("RGB"), np.float32) / 255.0


def to_im(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8))


def paste_rgba(img, layer, xy, shadow=0.5, blur=14, off=(10, 14)):
    """Paste an RGBA PIL layer onto a float image at xy (top-left), with a soft shadow on the ground."""
    h, w = img.shape[:2]
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.paste(layer, xy, layer)
    al = np.asarray(canvas.split()[3], np.float32) / 255
    if shadow:
        sh = np.asarray(canvas.split()[3].filter(ImageFilter.GaussianBlur(blur)), np.float32) / 255 * shadow
        sh = np.roll(np.roll(sh, off[1], 0), off[0], 1)
        img = img * (1 - sh[..., None])
    rgb = np.asarray(canvas.convert("RGB"), np.float32) / 255
    return img * (1 - al[..., None]) + rgb * al[..., None]


def cover_crop(im, w, h, fx=0.5, fy=0.5, zoom=1.0):
    """Scale to cover w x h (times zoom) and crop around (fx, fy)."""
    s = max(w / im.width, h / im.height) * zoom
    r = im.resize((max(w, int(im.width * s + 0.5)), max(h, int(im.height * s + 0.5))), Image.LANCZOS)
    x = int((r.width - w) * fx)
    y = int((r.height - h) * fy)
    return r.crop((x, y, x + w, y + h))


def rounded(im, r):
    m = Image.new("L", im.size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, im.width - 1, im.height - 1), r, fill=255)
    out = im.convert("RGBA")
    out.putalpha(ImageChops.multiply(out.split()[3], m))
    return out


def logo(width, lang="en", color=None):
    im = Image.open(BRAND + ("Splash_Logo_EN.png" if lang == "en" else "Splash_Logo_RU.png")).convert("RGBA")
    im = im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)
    if color:
        solid = Image.new("RGBA", im.size, color + (255,))
        solid.putalpha(im.split()[3])
        im = solid
    return im


def zigzag_paper(w, h, tooth=14, color=(247, 241, 228), seed=0):
    """Paper with pinked top and bottom edges like the game's Card_Paper; RGBA."""
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    pts = [(0, tooth)]
    n = int(w / tooth)
    for i in range(n + 1):
        pts.append((i * w / n, tooth if i % 2 == 0 else 0))
    pts += [(w, tooth), (w, h - tooth)]
    for i in range(n, -1, -1):
        pts.append((i * w / n, h - tooth if i % 2 == 0 else h))
    pts += [(0, h - tooth)]
    d.polygon(pts, fill=255)
    rng = np.random.default_rng(seed)
    base = np.ones((h, w, 3), np.float32) * np.array(color, np.float32) / 255
    base *= (0.965 + 0.035 * vnoise(w, h, 60, rng=rng))[..., None]
    base *= (0.97 + 0.03 * rng.random((h, w)).astype(np.float32))[..., None]
    out = to_im(base).convert("RGBA")
    out.putalpha(m)
    return out


def halftone(w, h, cell=7, angle=15):
    """0..1 dot screen, for the cheap-print look of the wrappers."""
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    a = math.radians(angle)
    u = (xs * math.cos(a) + ys * math.sin(a)) / cell
    v = (-xs * math.sin(a) + ys * math.cos(a)) / cell
    d = np.hypot(u - np.round(u), v - np.round(v))
    return np.clip((0.36 - d) * 6, 0, 1)


def creases(layer, seed=0, n=2):
    """Faint fold lines on a paper RGBA layer."""
    r = random.Random(seed)
    w, h = layer.size
    a = np.asarray(layer, np.float32)
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    for _ in range(n):
        x0, y0 = r.uniform(0, w), 0
        x1, y1 = x0 + r.uniform(-w * 0.4, w * 0.4), h
        if r.random() < 0.5:
            x0, y0, x1, y1 = 0, r.uniform(0, h), w, r.uniform(0, h)
        d.line((x0, y0, x1, y1), fill=255, width=2)
    lm = np.asarray(m.filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255
    dm = np.roll(lm, 2, 1)
    a[..., :3] = np.clip(a[..., :3] * (1 + 0.10 * lm[..., None]) * (1 - 0.10 * dm[..., None]), 0, 255)
    return Image.fromarray(a.astype(np.uint8), "RGBA")


def tape(w, h, seed=0):
    r = random.Random(seed)
    m = Image.new("L", (w, h), 0)
    pts = [(0, 0)]
    for i in range(1, 7):
        pts.append((r.uniform(-3, 5), h * i / 7))
    pts = [(x, y) for x, y in pts] + [(0, h)]
    right = [(w - r.uniform(-3, 5), h * i / 7) for i in range(7, -1, -1)]
    ImageDraw.Draw(m).polygon([(0, 0)] + [(x, y) for x, y in pts[1:-1]] + [(0, h)] + right, fill=165)
    t = Image.new("RGBA", (w, h), (250, 244, 214, 0))
    t.putalpha(m)
    return t


# ---------------------------------------------------------------- the film date stamp (7-segment, orange)
SEG = {"0": "abcdef", "1": "bc", "2": "abged", "3": "abgcd", "4": "fgbc", "5": "afgcd", "6": "afgedc", "7": "abc",
       "8": "abcdefg", "9": "abcdfg", " ": "", "'": "", "-": "g"}


def date_stamp(text, h=44):
    """Orange glowing 7-segment date like the 90s point-and-shoot cameras; returns RGBA."""
    w_digit, gap = h * 0.55, h * 0.22
    W = int(len(text) * (w_digit + gap) + h)
    m = Image.new("L", (W, int(h * 1.6)), 0)
    d = ImageDraw.Draw(m)
    t = h * 0.12
    x = h * 0.4
    y0 = h * 0.3
    for ch in text:
        if ch == "'":
            d.line((x + 2, y0, x - 2, y0 + h * 0.25), fill=255, width=int(t))
            x += gap * 1.2
            continue
        sk = math.tan(math.radians(8))
        def P(px, py):
            return (x + px + (h - py) * sk * 0.6, y0 + py)
        segs = {"a": ((0, 0), (w_digit, 0)), "b": ((w_digit, 0), (w_digit, h / 2)), "c": ((w_digit, h / 2), (w_digit, h)),
                "d": ((0, h), (w_digit, h)), "e": ((0, h / 2), (0, h)), "f": ((0, 0), (0, h / 2)), "g": ((0, h / 2), (w_digit, h / 2))}
        for s in SEG.get(ch, ""):
            (ax, ay), (bx, by) = segs[s]
            d.line((P(ax, ay), P(bx, by)), fill=255, width=int(t))
        x += w_digit + gap
    core = np.asarray(m.filter(ImageFilter.GaussianBlur(0.8)), np.float32) / 255
    glow = np.asarray(m.filter(ImageFilter.GaussianBlur(h * 0.18)), np.float32) / 255
    rgb = np.zeros(core.shape + (3,), np.float32)
    rgb[...] = np.array((255, 150, 50), np.float32) / 255
    rgb = rgb * (1 - core[..., None] * 0.5) + np.array((255, 225, 150), np.float32) / 255 * core[..., None] * 0.5
    a = np.clip(core + glow * 0.9, 0, 1)
    out = Image.fromarray((np.dstack([rgb, a]) * 255).astype(np.uint8), "RGBA")
    return out


def film_print(photo, w, date, seed=0, border=26):
    """A 10x15 print: 3:2 crop, warm faded colours, grain, white border, the date in the corner. RGBA."""
    ph = int(w * 2 / 3)
    im = cover_crop(photo, w, ph)
    a = to_f(im)
    a = 0.06 + a * 0.92                                    # lifted blacks
    a[..., 0] = a[..., 0] * 1.04 + 0.01
    a[..., 2] = a[..., 2] * 0.94
    rng = np.random.default_rng(seed)
    a += (rng.standard_normal(a.shape[:2]).astype(np.float32) * 0.025)[..., None]
    ys, xs = np.mgrid[0:ph, 0:w].astype(np.float32)
    rr = np.hypot((xs - w / 2) / (w / 2), (ys - ph / 2) / (ph / 2))
    a *= (1 - 0.22 * np.clip(rr - 0.5, 0, 1))[..., None]
    pim = to_im(a).convert("RGBA")
    ds = date_stamp(date, h=int(w * 0.035))
    pim.alpha_composite(ds, (w - ds.width - int(w * 0.03), ph - ds.height - int(w * 0.02)))
    card = Image.new("RGBA", (w + border * 2, ph + border * 2), (246, 244, 238, 255))
    card.paste(pim, (border, border))
    return creases(card, seed, 1)


def notebook(w, h, cell=36, margin_right=110, seed=0):
    """A squared school notebook page (клетка) with the red margin line on the right; float RGB."""
    rng = np.random.default_rng(seed)
    img = np.ones((h, w, 3), np.float32) * np.array((250, 250, 245), np.float32) / 255
    img *= (0.975 + 0.025 * vnoise(w, h, 80, rng=rng))[..., None]
    grid = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(grid)
    off = cell // 2
    for x in range(off, w, cell):
        d.line((x, 0, x, h), fill=255, width=2)
    for y in range(off, h, cell):
        d.line((0, y, w, y), fill=255, width=2)
    g = np.asarray(grid.filter(ImageFilter.GaussianBlur(0.5)), np.float32) / 255
    img = put(img, g, (150, 185, 225), 0.55)
    mr = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mr).line((w - margin_right, 0, w - margin_right, h), fill=255, width=3)
    img = put(img, np.asarray(mr.filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255, (225, 80, 90), 0.8)
    return img


def wrap(text, path, size, width):
    f = font(path, size)
    lines, cur = [], ""
    for word in text.split():
        t = (cur + " " + word).strip()
        if f.getlength(t) <= width:
            cur = t
        else:
            lines.append(cur)
            cur = word
    if cur:
        lines.append(cur)
    return lines


def rotate_rgba(im, deg):
    return im.rotate(deg, resample=Image.BICUBIC, expand=True)

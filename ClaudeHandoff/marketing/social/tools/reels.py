"""Vertical reels (1080x1920, 30 fps) for Instagram Reels / TikTok / YouTube Shorts in the game's look:
gameplay sits in a chalk-framed window on the yard asphalt, Blender shots go full screen, chalk titles write on.
Sound: the game's SFX rebuilt from the trailer clips' sound logs + «Space Cadet Training Montage» (CC0),
cut on its beat grid. Every reel is written twice: with music, and SFX only (to add a trending sound in the app).
Run: python reels.py <trailer|kids|bosses|pov|all> <ru|en|all> [--fast]
Env: SOC_RENDERS = folder with the Blender renders (kids_v/, key_v/, boss_*.png)."""
import os, sys, math, random, subprocess, wave
import numpy as np
from PIL import Image
import social_art as A
from social_art import Mask, NEUCHA, CAVEAT, CHALK, YELLOW

HERE = os.path.dirname(os.path.abspath(__file__))
TRAILER = "/Users/bekbolataldiyarov/Desktop/projects/ItsOurField_Trailer"
CLIPS = os.path.join(TRAILER, "clips")
RENDERS = os.environ.get("SOC_RENDERS", os.path.join(HERE, "..", "src", "renders"))
OUT = os.path.join(HERE, "..", "reels")
W, H, FPS = 1080, 1920, 30
FAST = "--fast" in sys.argv

# the trailer's sound tools (sound bank, sound-event logs, music decoding)
os.environ["TRAILER_ROOT"] = TRAILER
sys.path.insert(0, os.path.join(TRAILER, "tools"))
_argv = sys.argv
sys.argv = ["edit.py", CLIPS, os.devnull, "--music=spacecadet"]
import edit as E
sys.argv = _argv
SR = E.SR

MUSIC = E.MUSICS["spacecadet"]["path"]
MUSIC_GAIN = E.MUSICS["spacecadet"]["gain"]
BEAT = 60 / 130.0
BAR = BEAT * 4
DROP_SRC, PIECE1_END, CLIMAX, HIT, TAIL = 6.665, 21.434, 58.357, 66.447, 70.8

# gameplay window: a 5:6 crop of the 16:9 clip, framed in chalk
WX, WY, WW, WH = 40, 390, 1000, 1200
TOP_Y = 205            # main title line
LOGO_Y = 1622          # small logo under the window

TX = {
    "home": ("— Саша, домой!", "— Sasha, come home!"),
    "five": ("— Ещё 5 минуточек!", "— Five more minutes!"),
    "throw": ("КИДАЙ!", "THROW!"),
    "catch": ("ЛОВИ!", "CATCH!"),
    "survive": ("ВЫЖИВАЙ!", "SURVIVE!"),
    "coop": ("ВМЕСТЕ: 1–4 ИГРОКА", "CO-OP: 1–4 PLAYERS"),
    "yards": ("НОВЫЕ ДВОРЫ", "NEW YARDS"),
    "dusk": ("ТОТ, КТО В СУМЕРКАХ", "THE ONE IN THE DUSK"),
    "mom": ("МАМА ЗОВЁТ ДОМОЙ!", "MOM'S CALLING YOU HOME!"),
    "soon": ("СКОРО", "COMING SOON"),
    "follow": ("подписывайся, чтобы не пропустить", "follow so you don't miss it"),
    "who": ("КТО ТЫ ИЗ ДВОРА?", "WHO WERE YOU IN THE YARD?"),
    "comments": ("пиши в комментах", "tell us in the comments"),
    "can": ("СМОЖЕШЬ ВЫБИТЬ ВСЕХ?", "CAN YOU KNOCK THEM ALL OUT?"),
    "you": ("А ТЫ СМОЖЕШЬ?", "COULD YOU?"),
    "pov": ("POV: тебе 9, на часах 21:00", "POV: you're 9 and it's 9 PM"),
    "later": ("5 минут спустя:", "5 minutes later:"),
    "dark": ("...уже стемнело", "...it got dark"),
}
KIDS = [("Kid_Otlichnik", "ОТЛИЧНИК", "BRAINIAC", "Любит книги, немного заучка", "Loves books, a bit of a know-it-all"),
        ("Kid_Tolstyak", "ТОЛСТЯК", "CHUBS", "Добрый, всегда голодный, лучший друг", "Kind, always hungry, your best friend"),
        ("Kid_Melkaya", "МЕЛКАЯ", "SHORTY", "Быстрая, смелая, всегда в движении", "Fast, fearless, never sits still"),
        ("Kid_Huligan", "ХУЛИГАН", "HOOLIGAN", "Крутой драчун, главный во дворе", "Tough scrapper, boss of the yard")]
BOSSES = {"roly": ("БОЛЬШАЯ НЕВАЛЯШКА", "BIG ROLY-POLY", "двор", "the yard"),
          "fizruk": ("ФИЗРУК-МАНЕКЕН", "PE TEACHER MANNEQUIN", "хоккейная коробка", "the hockey box"),
          "transformer": ("ТРАНСФОРМЕР ИЗ ЛАРЬКА", "KIOSK TRANSFORMER", "барахолка", "the flea market"),
          "hare": ("БОЛЬШОЙ ПЛЮШЕВЫЙ ЗАЯЦ", "BIG PLUSH BUNNY", "детсад", "the kindergarten"),
          "dusk": ("ТОТ, КТО В СУМЕРКАХ", "THE ONE IN THE DUSK", "финал", "the finale")}


def tx(key, lang):
    return TX[key][0 if lang == "ru" else 1]


# ---------------------------------------------------------------- titles
def T(text, at, dur, y=TOP_Y, size=148, font=NEUCHA, color=CHALK, rot=-2.0, wave=False, sub=None, sub_size=75,
      fade_out=True, x=W // 2):
    """A chalk title on the timeline; sub = a second line in Caveat under it."""
    if sub and y == TOP_Y:
        y -= 50                      # room for the second line above the window
    return dict(text=text, at=at, dur=dur, y=y, size=size, font=font, color=color, rot=rot, wave=wave, sub=sub,
                sub_size=sub_size, fade_out=fade_out, x=x)


def fit(text, font, size, width=980):
    f = A.font(font, size)
    while f.getlength(text) > width and size > 30:
        size -= 4
        f = A.font(font, size)
    return size


class Layer:
    """Pre-rendered chalk title: 3 'boil' variants, cropped to their box, colour + alpha with a soft shadow."""

    def __init__(self, ti, seed):
        self.ti = ti
        self.var = []
        for v in range(3):
            r = random.Random(seed * 31 + v)
            m = Mask(W, H)
            size = fit(ti["text"], ti["font"], ti["size"])
            jx, jy = r.uniform(-2, 2), r.uniform(-2, 2)
            m.text(ti["text"], ti["font"], size, (ti["x"] + jx, ti["y"] + jy), ti["rot"] + r.uniform(-0.4, 0.4))
            if ti["wave"]:
                tw = min(980, A.font(ti["font"], size).getlength(ti["text"]))
                m.wave(ti["x"] - tw / 2, ti["x"] + tw / 2, ti["y"] + size * 0.62, 8, 7, 34, seed=seed + v, rot=ti["rot"])
            cov = A.chalk(m.array(), seed * 7 + v)
            subcov = None
            if ti["sub"]:
                ms = Mask(W, H)
                ssize = fit(ti["sub"], CAVEAT, ti["sub_size"])
                ms.text(ti["sub"], CAVEAT, ssize, (ti["x"] + 10 + jx, ti["y"] + size * 0.62 + (60 if ti["wave"] else 30) + ssize * 0.5),
                        ti["rot"] + 1)
                subcov = A.chalk(ms.array(), seed * 7 + v + 100)
            self.var.append(self._pack(cov, subcov, ti["color"]))
        ys, xs = [], []
        for (x0, y0, rgb, a) in self.var:
            xs += [x0, x0 + a.shape[1]]
        self.x0, self.x1 = min(xs), max(xs)

    @staticmethod
    def _pack(cov, subcov, color):
        rgb = np.zeros((H, W, 3), np.float32)
        rgb[...] = np.array(color, np.float32) / 255
        a = cov.copy()
        if subcov is not None:
            rgb = np.where((subcov > cov)[..., None], np.array(CHALK, np.float32) / 255, rgb)
            a = np.maximum(a, subcov)
        sh = np.asarray(Image.fromarray((a * 255).astype(np.uint8)).filter(
            __import__("PIL.ImageFilter", fromlist=["GaussianBlur"]).GaussianBlur(7)), np.float32) / 255 * 0.6
        sh = np.roll(np.roll(sh, 5, 0), 4, 1)
        out_a = a + sh * (1 - a)
        out_rgb = rgb * (a / np.maximum(out_a, 1e-4))[..., None]
        ys, xs = np.nonzero(out_a > 0.004)
        if len(ys) == 0:
            return (0, 0, np.zeros((1, 1, 3), np.float32), np.zeros((1, 1), np.float32))
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        return (x0, y0, out_rgb[y0:y1, x0:x1].copy(), (out_a * 0.97)[y0:y1, x0:x1].copy())

    def draw(self, img, t):
        ti = self.ti
        lt = t - ti["at"]
        if lt < 0 or lt > ti["dur"]:
            return
        x0, y0, rgb, a = self.var[int(lt * 12) % 3]
        k = 1.0
        if ti["fade_out"] and ti["dur"] - lt < 0.12:
            k = (ti["dur"] - lt) / 0.12
        u = min(1.0, lt / 0.24)
        if u < 1:
            edge = self.x0 + (self.x1 - self.x0 + 60) * u
            xs = np.arange(x0, x0 + a.shape[1], dtype=np.float32)
            a = a * np.clip((edge - xs) / 60, 0, 1)[None, :]
        a = (a * k)[..., None]
        reg = img[y0:y0 + rgb.shape[0], x0:x0 + rgb.shape[1]]
        img[y0:y0 + rgb.shape[0], x0:x0 + rgb.shape[1]] = reg * (1 - a) + rgb * a


# ---------------------------------------------------------------- backgrounds
def game_background(seed):
    """Asphalt with chalk scraps, the chalk frame round the window, a small logo under it."""
    img = A.asphalt(W, H, seed).astype(np.float32)
    m = Mask(W, H)
    r = random.Random(seed)
    m.star(110, 1650, 34, 6, r.randint(0, 99), rot=12)
    m.ball(960, 1690, 34, 6, r.randint(0, 99))
    m.star(980, 120, 26, 5, r.randint(0, 99))
    m.circle(90, 110, 36, 30, 5, 1.1, r.randint(0, 99))
    img = A.put(img, A.chalk(m.array(), seed, 0.6), CHALK, 0.45)
    f = Mask(W, H)
    x0, y0, x1, y1 = WX - 12, WY - 12, WX + WW + 12, WY + WH + 12
    f.line([(x0 - 26, y0), (x1 + 18, y0 + 2)], 7, seed + 1)
    f.line([(x1, y0 - 22), (x1 - 2, y1 + 26)], 7, seed + 2)
    f.line([(x1 + 22, y1), (x0 - 16, y1 - 2)], 7, seed + 3)
    f.line([(x0, y1 + 20), (x0 + 2, y0 - 24)], 7, seed + 4)
    img = A.put(img, A.chalk(f.array(), seed + 9, 0.5), CHALK, 0.85, shadow=0.3)
    sh = np.zeros((H, W), np.float32)
    sh[WY + 10:WY + WH + 16, WX + 8:WX + WW + 14] = 1
    sh = np.asarray(Image.fromarray((sh * 255).astype(np.uint8)).filter(
        __import__("PIL.ImageFilter", fromlist=["GaussianBlur"]).GaussianBlur(14)), np.float32) / 255
    img = img * (1 - 0.5 * sh[..., None])
    lg = A.logo(400)
    img = A.paste_rgba(img, lg, ((W - lg.width) // 2, LOGO_Y), shadow=0.4, blur=6, off=(3, 4))
    return img.astype(np.float32)


# ---------------------------------------------------------------- sources
class Game:
    def __init__(self, s):
        ch = int(1080 / s.get("zoom", 1.0))
        c = int(ch * WW / WH)
        cx = int(min(max(s.get("cx", 0.5) * 1920 - c / 2, 0), 1920 - c))
        cy = int(min(max(s.get("cy", 0.5) * 1080 - ch / 2, 0), 1080 - ch))
        self.proc = subprocess.Popen([E.FF, "-loglevel", "quiet", "-ss", f"{s['t_in']:.4f}", "-i",
                                      os.path.join(CLIPS, s["src"] + ".mp4"), "-t", f"{s['dur'] + 0.3:.4f}", "-vf",
                                      f"crop={c}:{ch}:{cx}:{cy},scale={WW}:{WH}:flags=lanczos,fps={FPS}",
                                      "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE)
        self.last = np.zeros((WH, WW, 3), np.uint8)
        self.s = s

    def frame(self, i, base):
        raw = self.proc.stdout.read(WW * WH * 3)
        if len(raw) == WW * WH * 3:
            self.last = np.frombuffer(raw, np.uint8).reshape(WH, WW, 3)
        img = base.copy()
        img[WY:WY + WH, WX:WX + WW] = E.grade(self.last, self.s.get("grade"))
        return img

    def close(self):
        self.proc.stdout.close()
        self.proc.wait()


class Seq:
    """Blender PNG sequence (30 fps, 1080x1920), full screen; holds the last frame."""

    def __init__(self, s):
        d = os.path.join(RENDERS, s["src"])
        self.files = sorted(os.path.join(d, f) for f in os.listdir(d) if f.endswith(".png"))
        self.s = s

    def frame(self, i, base):
        k = int(round((self.s["t_in"] + i / FPS) * 30))
        im = Image.open(self.files[min(k, len(self.files) - 1)]).convert("RGB")
        if im.size != (W, H):
            im = A.cover_crop(im, W, H)
        return np.asarray(im, np.float32) / 255

    def close(self):
        pass


class Poster:
    """A still render pushed in slowly (full screen)."""

    def __init__(self, s):
        self.im = Image.open(os.path.join(RENDERS, s["src"])).convert("RGB")
        self.s = s

    def frame(self, i, base):
        u = i / max(1, self.s["dur"] * FPS)
        z = 1.0 + 0.09 * (1 - (1 - u) ** 2)
        return np.asarray(A.cover_crop(self.im, W, H, 0.5, self.s.get("fy", 0.45), z), np.float32) / 255

    def close(self):
        pass


_logo_cache = {}


def end_overlay(img, t, s, lang):
    """Logo pops in with an overshoot; «СКОРО» and a line in chalk under it."""
    at = s.get("logo_at", 0.1)
    if t < at:
        return img
    if "logo" not in _logo_cache:
        _logo_cache["logo"] = A.logo(920)
    lg = _logo_cache["logo"]
    u = min(1.0, (t - at) / 0.35)
    sc = 1 + 0.18 * math.exp(-6 * u) * math.cos(9 * u) - 0.18 * (1 - u) ** 3
    a = min(1.0, (t - at) / 0.12)
    im = lg.resize((max(1, int(lg.width * sc)), max(1, int(lg.height * sc))), Image.BILINEAR)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    canvas.alpha_composite(im, (W // 2 - im.width // 2, 330 - im.height // 2))
    arr = np.asarray(canvas, np.float32) / 255
    al = arr[..., 3:4] * a
    return img * (1 - al) + arr[..., :3] * al


# ---------------------------------------------------------------- reels
def kid_titles(at, dur, k, lang, y=210):
    """The kid's name in the sky above them, the tagline under it (the bottom of the screen is the app's)."""
    name = k[1] if lang == "ru" else k[2]
    tag = k[3] if lang == "ru" else k[4]
    return [T(name, at + 0.04, dur - 0.04, y=y, size=180, rot=-3, wave=True, sub=tag, sub_size=70, fade_out=False)]


def reel_trailer(lang):
    S, t = [], 0.0

    def add(**k):
        nonlocal t
        k.setdefault("t_in", 0.0)
        k["at"] = t
        S.append(k)
        t += k["dur"]

    titles = []
    add(kind="game", src="u01_open", t_in=0.6, dur=2.3, cx=0.56)
    titles.append(T(tx("home", lang), 0.5, 1.8, size=140))
    add(kind="game", src="u02_fivemore", t_in=0.7, dur=2.3, cx=0.5)
    titles.append(T(tx("five", lang), 2.45, 2.1, size=140))
    for i, k in enumerate(KIDS):     # the drop: 3 beats per kid
        add(kind="seq", src=f"kids_v/{k[0]}", t_in=0.45, dur=BEAT * 3, flash=i == 0, extra=[(0.02, "ChalkScribble", 0.5)])
        titles += kid_titles(S[-1]["at"], BEAT * 3, k, lang)
    th = E.ev("u03_throw", "ThrowCharged", default=1.2)
    add(kind="game", src="u03_throw", t_in=max(0.0, th - 0.95), dur=BAR, cx=0.38)
    titles.append(T(tx("throw", lang), S[-1]["at"] + 0.1, BAR - 0.15, size=200, rot=-4, wave=True))
    ca = E.ev("u04_catch", "CatchCandle", default=E.ev("u04_catch", "Catch", default=3.9))
    add(kind="game", src="u04_catch", t_in=max(0.0, ca - 0.85), dur=BEAT * 2, cx=0.5)
    titles.append(T(tx("catch", lang), S[-1]["at"] + 0.1, BAR - 0.15, size=200, rot=3, wave=True))
    back = E.ev("u04_catch", "Throw", default=ca + 0.7)
    add(kind="game", src="u04_catch", t_in=back - 0.1, dur=BEAT * 2, cx=0.5)
    add(kind="game", src="u05_survive", t_in=1.0, dur=BAR, cx=0.45)
    titles.append(T(tx("survive", lang), S[-1]["at"] + 0.1, BAR - 0.15, size=200, rot=-3, wave=True))
    add(kind="game", src="u06_coop", t_in=1.2, dur=BAR, cx=0.5)
    titles.append(T(tx("coop", lang), S[-1]["at"] + 0.1, BAR - 0.15, size=140, rot=-2, wave=True))
    for i, (clip, t_in, lift) in enumerate([("u07_rink", 1.0, 0), ("u07_bazaar", 0.9, 0), ("u07_site", 2.0, 0.12), ("u07_kg", 1.0, 0.12)]):
        add(kind="game", src=clip, t_in=t_in, dur=BEAT, cx=0.5, grade=dict(lift=lift))
        if i == 0:
            titles.append(T(tx("yards", lang), S[-1]["at"] + 0.05, BAR - 0.1, size=175, rot=3, wave=True))
    # the climax piece of the track starts with the bosses
    add(kind="game", src="u08_transformer", t_in=4.38, dur=BEAT * 2, cx=0.45)
    add(kind="game", src="u08_babai", t_in=1.8, dur=BAR, cx=0.62, grade=dict(lift=0.16))
    titles.append(T(tx("dusk", lang), S[-1]["at"] + 0.15, BAR - 0.2, size=135, rot=-2, wave=True))
    sc = E.ev("u08_slowcatch", "CatchCandle", default=E.ev("u08_slowcatch", "Catch", default=1.5))
    add(kind="game", src="u08_slowcatch", t_in=max(0.0, sc - 1.1), dur=BAR, cx=0.36, grade=dict(lift=0.08), slow_music=True)
    t_hit = (PIECE1_END - 2.065) + (HIT - CLIMAX)
    add(kind="game", src="u09_finale", t_in=0.75, dur=t_hit - t, cx=0.5, grade=dict(lift=0.14), mute=("MomCall",))
    titles.append(T(tx("mom", lang), S[-1]["at"] + 0.3, S[-1]["dur"] - 0.35, size=140, rot=-2, wave=True))
    t_tail = (PIECE1_END - 2.065) + (TAIL - CLIMAX)
    add(kind="end", src="key_v", t_in=0.0, dur=t_tail - t, logo_at=0.02,
        extra=[(0.02, "Catch", 0.6), (0.67, "ChalkScribble", 0.6)])
    titles += [T(tx("soon", lang), S[-1]["at"] + 0.67, S[-1]["dur"] - 0.67, y=560, size=162, color=YELLOW, rot=-2, wave=True,
                 sub=tx("follow", lang), sub_size=72, fade_out=False)]
    music = [(2.065, PIECE1_END, 0.0), (CLIMAX, None, PIECE1_END - 2.065)]
    return S, titles, music, t


def reel_kids(lang):
    S, titles, t = [], [], 0.0
    d = (HIT - CLIMAX) / 4
    for i, k in enumerate(KIDS):
        S.append(dict(kind="seq", src=f"kids_v/{k[0]}", t_in=0.0, dur=d, at=t, flash=i == 0, extra=[(0.03, "ChalkScribble", 0.5)]))
        titles += kid_titles(t, d, k, lang, y=330)
        t += d
    titles.insert(0, T(tx("who", lang), 0.15, t - 0.15, y=120, size=100, color=YELLOW, rot=-2, fade_out=False))
    end = TAIL - CLIMAX - t
    S.append(dict(kind="end", src="key_v", t_in=0.0, dur=end, at=t, logo_at=0.02,
                  extra=[(0.02, "Catch", 0.6), (0.67, "ChalkScribble", 0.6)]))
    titles.append(T(tx("comments", lang) + " ↓", t + 0.67, end - 0.67, y=560, size=113, color=YELLOW, rot=-2, wave=True,
                    font=CAVEAT, sub=tx("soon", lang).lower() if lang == "ru" else "coming soon", sub_size=81, fade_out=False))
    return S, titles, [(CLIMAX, None, 0.0)], t + end


def reel_bosses(lang):
    S, titles, t = [], [], 0.0
    start = 5.5
    drop = DROP_SRC - start

    def add(**k):
        nonlocal t
        k.setdefault("t_in", 0.0)
        k["at"] = t
        S.append(k)
        t += k["dur"]

    def name(key, at, dur):
        b = BOSSES[key]
        titles.append(T(b[0] if lang == "ru" else b[1], at + 0.05, dur - 0.08, size=135, rot=-2, wave=True,
                        sub=b[2] if lang == "ru" else b[3], sub_size=75))

    add(kind="game", src="u08_babai", t_in=0.6, dur=drop, cx=0.62, grade=dict(lift=0.16))
    titles.append(T(tx("can", lang), 0.1, drop - 0.12, size=135, color=YELLOW, rot=-2, wave=True))
    sp = E.ev("u08_split", "BossSplit", default=2.0)
    add(kind="game", src="u08_split", t_in=max(0.0, sp - 0.9), dur=BAR, cx=0.47, flash=True)
    name("roly", S[-1]["at"], BAR)
    add(kind="poster", src="boss_Boss_Fizruk.png", dur=BAR, fy=0.4, extra=[(0.02, "ChalkScribble", 0.5)])
    name("fizruk", S[-1]["at"], BAR)
    add(kind="game", src="u08_transformer", t_in=3.9, dur=BAR, cx=0.47)
    name("transformer", S[-1]["at"], BAR)
    add(kind="poster", src="boss_Boss_Hare.png", dur=BAR, fy=0.4, extra=[(0.02, "ChalkScribble", 0.5)])
    name("hare", S[-1]["at"], BAR)
    add(kind="game", src="u08_babai", t_in=2.5, dur=BAR, cx=0.62, grade=dict(lift=0.16))
    name("dusk", S[-1]["at"], BAR)
    sc = E.ev("u08_slowcatch", "CatchCandle", default=E.ev("u08_slowcatch", "Catch", default=1.5))
    add(kind="game", src="u08_slowcatch", t_in=max(0.0, sc - 1.1), dur=BAR, cx=0.36, grade=dict(lift=0.08), slow_music=True)
    titles.append(T(tx("you", lang), S[-1]["at"] + 0.1, BAR - 0.15, size=162, color=YELLOW, rot=3, wave=True))
    add(kind="end", src="key_v", t_in=0.0, dur=2.8, logo_at=0.02, extra=[(0.02, "Catch", 0.6), (0.67, "ChalkScribble", 0.6)])
    titles.append(T(tx("soon", lang), S[-1]["at"] + 0.67, 2.8 - 0.67, y=560, size=162, color=YELLOW, rot=-2, wave=True,
                    sub=tx("follow", lang), sub_size=72, fade_out=False))
    return S, titles, [(start, PIECE1_END, 0.0)], t


def reel_pov(lang):
    S, titles, t = [], [], 0.0
    drop = DROP_SRC - 2.065

    def add(**k):
        nonlocal t
        k.setdefault("t_in", 0.0)
        k["at"] = t
        S.append(k)
        t += k["dur"]

    add(kind="game", src="u01_open", t_in=0.4, dur=2.5, cx=0.56)
    add(kind="game", src="u02_fivemore", t_in=0.7, dur=drop - 2.5, cx=0.5)
    titles.append(T(tx("pov", lang), 0.1, drop - 0.12, y=150, size=104, color=YELLOW, rot=-2, font=NEUCHA))
    titles.append(T(tx("home", lang), 0.6, 1.85, y=290, size=100, rot=-1, font=CAVEAT))
    titles.append(T(tx("five", lang), 2.55, drop - 2.6, y=290, size=100, rot=1, font=CAVEAT))
    add(kind="game", src="u05_survive", t_in=0.6, dur=BAR, cx=0.45, flash=True)
    add(kind="game", src="u06_coop", t_in=2.0, dur=BAR, cx=0.5)
    add(kind="game", src="u07_site", t_in=1.6, dur=BEAT * 2, cx=0.5, grade=dict(lift=0.12))
    titles.append(T(tx("later", lang), drop + 0.05, BAR * 2 + BEAT * 2 - 0.1, size=135, rot=-2, wave=True))
    add(kind="game", src="u08_babai", t_in=1.8, dur=BAR, cx=0.62, grade=dict(lift=0.16))
    titles.append(T(tx("dark", lang), S[-1]["at"] + 0.1, BAR - 0.15, size=129, rot=-2, font=CAVEAT))
    add(kind="game", src="u09_finale", t_in=0.75, dur=2.6, cx=0.5, grade=dict(lift=0.14), mute=("MomCall",))
    titles.append(T(tx("mom", lang), S[-1]["at"] + 0.2, 2.35, size=140, rot=-2, wave=True))
    add(kind="end", src="key_v", t_in=0.0, dur=2.8, logo_at=0.02, extra=[(0.02, "Catch", 0.6), (0.67, "ChalkScribble", 0.6)])
    titles.append(T(tx("soon", lang), S[-1]["at"] + 0.67, 2.8 - 0.67, y=560, size=162, color=YELLOW, rot=-2, wave=True,
                    sub=tx("follow", lang), sub_size=72, fade_out=False))
    return S, titles, [(2.065, PIECE1_END, 0.0)], t


REELS = {"trailer": reel_trailer, "kids": reel_kids, "bosses": reel_bosses, "pov": reel_pov}
NAMES = {"trailer": "R1_mama_zovet_domoy", "kids": "R2_kto_ty_iz_dvora", "bosses": "R3_bossy_dvora", "pov": "R4_pov_21_00"}


# ---------------------------------------------------------------- audio
def mix(segs, music, total, with_music=True):
    n = int(total * SR) + SR
    sfx = np.zeros((n, 2), np.float32)
    rng = random.Random(4)
    for s in segs:
        for (dt, cue, g) in s.get("extra", []):
            y, v = E.cue_sample(cue, rng)
            if y is not None:
                E.place(sfx, s["at"] + dt, y[: int(SR * 0.9)], v * g * 0.8)
        if s["kind"] != "game":
            continue
        last = {}
        for (t, cue, x, y_, z, cx, cy, cz, rx, ry, rz) in E.events(s["src"]):
            if not (s["t_in"] <= t < s["t_in"] + s["dur"]) or cue in s.get("mute", ()):
                continue
            b = E.BANK.get(cue, {})
            if t - last.get(cue, -9) < max(0.04, b.get("interval", 0.04)):
                continue
            last[cue] = t
            smp, v = E.cue_sample(cue, rng)
            if smp is None:
                continue
            d = np.array([x - cx, y_ - cy, z - cz])
            dist = float(np.linalg.norm(d))
            pan = 0.0 if b.get("ui") else float(np.clip(np.dot(d, [rx, ry, rz]) / max(dist, 1e-3), -1, 1)) * 0.5
            att = float(np.clip(1 - (dist - 12) / 48, 0, 1))
            E.place(sfx, s["at"] + (t - s["t_in"]), smp, v * (0.55 + 0.45 * att), pan)
    sfx = np.tanh(sfx * 1.1) * 0.85
    out = sfx * 0.8
    if with_music:
        src = E.decode(MUSIC) * MUSIC_GAIN
        mus = np.zeros((n, 2), np.float32)
        xf = int(0.03 * SR)
        for k, (a, b, at) in enumerate(music):
            piece = src[int(round(a * SR)): None if b is None else int(round(b * SR))].copy()
            cur = int(round(at * SR))
            if k > 0 and len(piece) > xf:
                piece[:xf] *= np.sin(np.linspace(0, math.pi / 2, xf))[:, None].astype(np.float32)
                mus[cur - xf:cur] *= np.cos(np.linspace(0, math.pi / 2, xf))[:, None].astype(np.float32)
                cur -= xf
            piece = piece[: max(0, n - cur)]
            mus[cur:cur + len(piece)] += piece
        env = np.ones(n, np.float32) * 0.62
        fade = int(max(0.0, total - 0.6) * SR)
        env[fade:] *= np.linspace(1, 0, n - fade) ** 1.5
        for s in segs:
            if s.get("slow_music"):
                a, b = int(s["at"] * SR), int((s["at"] + s["dur"]) * SR)
                lp = mus[a:b].copy()
                k = 1 - math.exp(-2 * math.pi * 500 / SR)
                for ch in range(2):
                    acc, col = 0.0, lp[:, ch]
                    o = np.empty_like(col)
                    for j in range(len(col)):
                        acc += k * (col[j] - acc)
                        o[j] = acc
                    lp[:, ch] = o
                ramp = np.ones(b - a, np.float32)
                r = int(0.08 * SR)
                ramp[:r] = np.linspace(0, 1, r)
                ramp[-r:] = np.linspace(1, 0, r)
                mus[a:b] = mus[a:b] * (1 - ramp[:, None]) + lp * 1.6 * ramp[:, None]
        out = out + mus * env[:, None]
    out = np.tanh(out) * 0.95
    return out[: int(total * SR)]


def write_wav(path, x):
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())


# ---------------------------------------------------------------- render
def render(reel, lang):
    segs, titles, music, total = REELS[reel](lang)
    print(f"== {reel} {lang}: {total:.2f} s, {len(segs)} shots", flush=True)
    os.makedirs(OUT, exist_ok=True)
    base = game_background(11 + len(reel))
    layers = [Layer(ti, k * 13 + 1 + (0 if lang == "ru" else 500)) for k, ti in enumerate(titles)]
    name = f"{NAMES[reel]}_{lang}"
    tmp = os.path.join(OUT, f".{name}_video.mp4")
    enc = subprocess.Popen([E.FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                            "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "veryfast" if FAST else "slow",
                            "-crf", "24" if FAST else "17", "-pix_fmt", "yuv420p", tmp], stdin=subprocess.PIPE)
    for s in segs:
        src = {"game": Game, "seq": Seq, "end": Seq, "poster": Poster}[s["kind"]](s)
        n = int(round((s["at"] + s["dur"]) * FPS)) - int(round(s["at"] * FPS))
        for i in range(n):
            t = s["at"] + i / FPS
            img = src.frame(i, base)
            if s["kind"] == "end":
                img = end_overlay(img, i / FPS, s, lang)
            for L in layers:
                L.draw(img, t)
            if s.get("flash") and i < 4:
                img = img + (1 - img) * (1 - i / 4) * 0.8
            if s["kind"] == "end" and s["dur"] - i / FPS < 0.4:
                img = img * max(0.0, (s["dur"] - i / FPS) / 0.4)
            enc.stdin.write((np.clip(img, 0, 1) * 255).astype(np.uint8).tobytes())
        src.close()
    enc.stdin.close()
    enc.wait()
    for with_music, suffix in ((True, ""), (False, "_sfx_only")):
        wav = os.path.join(OUT, f".{name}{suffix}.wav")
        write_wav(wav, mix(segs, music, total, with_music))
        out = os.path.join(OUT, f"{name}{suffix}.mp4")
        subprocess.run([E.FF, "-y", "-loglevel", "error", "-i", tmp, "-i", wav, "-c:v", "copy", "-c:a", "aac", "-b:a", "192k",
                        "-shortest", "-movflags", "+faststart", out], check=True)
        os.remove(wav)
        print("OK", out, flush=True)
    os.remove(tmp)


if __name__ == "__main__":
    which = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else "all"
    langs = sys.argv[2] if len(sys.argv) > 2 and not sys.argv[2].startswith("--") else "all"
    for r in (REELS if which == "all" else [which]):
        for lang in (("ru", "en") if langs == "all" else (langs,)):
            render(r, lang)

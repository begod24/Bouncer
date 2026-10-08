"""Edits the trailer «Мама зовёт домой»: clips cut on the music's beat grid, chalk titles, game SFX rebuilt
from the recorded sound-event logs, music, end card.
Usage: python edit.py [clips_dir] [out.mp4] [--fast] [--music=game|spacecadet]
  game        v1: SFX_Music4 from the game (106.67 BPM, drop at 5.995 s)
  spacecadet  v2: «Space Cadet Training Montage» by Zane Little Music, CC0 (opengameart.org/node/138918), 130 BPM;
              the track is cut drop phrase -> last half-phrase (climax) -> ending, its final hit lands on the logo."""
import os, sys, math, random, subprocess, wave
import numpy as np
from PIL import Image
import imageio_ffmpeg
import chalk

# ROOT holds out/00_splash.*, blender/kids/<Kid>/, blender/endcard/, soundbank.tsv (default: next to this script)
HERE = os.environ.get("TRAILER_ROOT", os.path.dirname(os.path.abspath(__file__)))
REPO = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer/"
SFX = REPO + "Assets/_Project/Audio/SFX/"
BRAND = REPO + "Assets/_Project/Art/UI/Branding/"
FF = imageio_ffmpeg.get_ffmpeg_exe()
FPS, W, H, SR = 60, 1920, 1080, 48000

args = [a for a in sys.argv[1:] if not a.startswith("--")]
CLIPS = args[0] if len(args) > 0 else os.path.join(HERE, "clips")
OUT = args[1] if len(args) > 1 else os.path.join(HERE, "out", "trailer.mp4")
FAST = "--fast" in sys.argv

# ---------------------------------------------------------------- music
# plan: pieces of the source track (start, end|None) played back to back; drop_src: the first hit after the
# build-up in source time, placed on the kids' entrance (DROP); hit_src: the final hit (cut to the logo)
MUSICS = {
    "game": dict(path=REPO + "Assets/_Project/Audio/Music/SFX_Music4.mp3", bpm=106.6667, drop_src=5.995,
                 plan=[(0.0, None)], gain=1.0, end_fade=2.6),
    "spacecadet": dict(path=os.path.join(HERE, "music", "space_cadet_training_montage.wav"), bpm=130.0, drop_src=6.665,
                       plan=[(0.0, 21.434), (58.357, None)], gain=1.75, end_fade=0.3, hit_src=66.447, tail_src=70.8),
}
VARIANT = next((a.split("=", 1)[1] for a in sys.argv if a.startswith("--music=")), "game")
M = MUSICS[VARIANT]
DROP = 8.6                         # trailer time of the drop: splash 2.6 + sunset yard 3.5 + «5 минуточек» 2.5
MUSIC_AT = DROP - M["drop_src"]    # trailer time where the track starts
BEAT = 60 / M["bpm"]
BAR = BEAT * 4


def music_to_trailer(m):
    """Trailer time of source-track time m, through the plan's cuts."""
    t = MUSIC_AT
    for a, b in M["plan"]:
        if b is None or m < b:
            return t + (m - a)
        t += b - a
    return t


def events(clip):
    path = os.path.join(CLIPS, clip + ".sounds.tsv")
    out = []
    if os.path.exists(path):
        for line in open(path).read().splitlines()[1:]:
            p = line.split("\t")
            if len(p) >= 11:
                out.append((float(p[0]), p[1], *map(float, p[2:11])))
    return out


def ev(clip, cue, n=0, default=0.0):
    ts = [e[0] for e in events(clip) if e[1] == cue]
    return ts[n] if len(ts) > n else default


# ---------------------------------------------------------------- titles
N, C = chalk.NEUCHA, chalk.CAVEAT


def title(ru, en, cx, cy, size=170, rot=-3, en_size=None):
    en_size = en_size or int(size * 0.5)
    return [(ru, N, size, (cx, cy), rot), (en, C, en_size, (cx + 20, cy + int(size * 0.78)), rot + 1)]


# ---------------------------------------------------------------- the cut
# src: clip name (mp4 in CLIPS) | ('png', dir, fps) | ('file', path). t_in: source start, dur: length on the timeline.
def build_segments():
    S = []
    t = 0.0

    def add(**k):
        nonlocal t
        k.setdefault("t_in", 0.0)
        k["at"] = t
        S.append(k)
        t += k["dur"]

    add(name="splash", src=("file", os.path.join(HERE, "out", "00_splash.mp4")), dur=2.6, audio=os.path.join(HERE, "out", "00_splash.wav"))
    # quiet sunset yard, the call from the window
    add(name="open", src="u01_open", t_in=0.4, dur=3.5,
        titles=[dict(t=0.7, dur=2.6, lines=title("— Саша, домой!", "— Sasha, come home!", 1300, 250, 120, -2))])
    add(name="fivemore", src="u02_fivemore", t_in=0.7, dur=DROP - t, zoom=(1.12, 1.2, 0.42),
        titles=[dict(t=0.15, dur=DROP - t - 0.2, lines=title("— Ещё 5 минуточек!", "— Five more minutes!", 960, 200, 130, -2))])
    # DROP: the four kids (Blender), two beats each
    kids = [("Kid_Otlichnik", "ОТЛИЧНИК", "BRAINIAC", 520), ("Kid_Tolstyak", "ТОЛСТЯК", "CHUBS", 1400),
            ("Kid_Melkaya", "МЕЛКАЯ", "SHORTY", 520), ("Kid_Huligan", "ХУЛИГАН", "HOOLIGAN", 1400)]
    for i, (k, ru, en, x) in enumerate(kids):
        kb = 2 if VARIANT == "game" else 3
        add(name="kid_" + k, src=("png", os.path.join(HERE, "blender", "kids", k), 60), t_in=0.2 if kb == 2 else 0.0,
            dur=BEAT * kb, flash=i == 0, extra=[(0.02, "ChalkScribble", 0.5)],
            titles=[dict(t=0.04, dur=BEAT * kb - 0.04, lines=title(ru, en, x, 760, 150, -4 if x < 960 else 3), fade_out=False)])
    # gameplay on the bar
    th = ev("u03_throw", "ThrowCharged", default=1.2)
    add(name="throw", src="u03_throw", t_in=max(0.0, th - 0.95), dur=BAR,
        titles=[dict(t=0.1, dur=BAR - 0.15, lines=title("КИДАЙ!", "THROW!", 470, 790, 180, -4))])
    ca = ev("u04_catch", "CatchCandle", default=ev("u04_catch", "Catch", default=3.9))
    # two beats of the catch, a jump cut over the block blink, two beats of the answer and the hot-potato blast
    add(name="catch", src="u04_catch", t_in=max(0.0, ca - 0.85), dur=BEAT * 2,
        titles=[dict(t=0.1, dur=BAR - 0.15, lines=title("ЛОВИ!", "CATCH!", 1450, 790, 180, 3))])
    back = ev("u04_catch", "Throw", default=ca + 0.7)
    add(name="catch_back", src="u04_catch", t_in=back - 0.1, dur=BEAT * 2)
    add(name="survive", src="u05_survive", t_in=1.0, dur=BAR,
        titles=[dict(t=0.1, dur=BAR - 0.15, lines=title("ВЫЖИВАЙ!", "SURVIVE!", 520, 800, 170, -3))])
    add(name="coop", src="u06_coop", t_in=1.2, dur=BAR,
        titles=[dict(t=0.1, dur=BAR - 0.15, lines=title("ВМЕСТЕ: 1–4 ИГРОКА", "CO-OP FOR 1–4 PLAYERS", 960, 150, 120, -2))])
    # new arenas, one beat each
    for i, (clip, t_in) in enumerate([("u07_rink", 1.0), ("u07_bazaar", 0.9), ("u07_site", 2.0), ("u07_kg", 1.0)]):
        add(name=clip, src=clip, t_in=t_in, dur=BEAT, grade=dict(lift=0.12 if clip in ("u07_site", "u07_kg") else 0.0),
            titles=[dict(t=0.05, dur=BAR - 0.1, lines=title("НОВЫЕ ДВОРЫ", "NEW YARDS", 1380, 820, 140, 3))] if i == 0 else [])
    # bosses
    if VARIANT == "game":
        sp = ev("u08_split", "BossSplit", default=2.0)
        add(name="split", src="u08_split", t_in=max(0.0, sp - 0.7), dur=BEAT * 2, zoom=(1.3, 1.38, 0.5, 0.42))
    add(name="transformer", src="u08_transformer", t_in=4.38, dur=BEAT * 2)   # the ram through the stalls
    add(name="babai", src="u08_babai", t_in=1.8, dur=BAR, grade=dict(lift=0.16),
        titles=[dict(t=0.15, dur=BAR - 0.2, lines=title("ТОТ, КТО В СУМЕРКАХ", "THE ONE IN THE DUSK", 960, 160, 110, -2))])
    sc = ev("u08_slowcatch", "CatchCandle", default=ev("u08_slowcatch", "Catch", default=1.5))
    add(name="slowcatch", src="u08_slowcatch", t_in=max(0.0, sc - (1.3 if VARIANT == "game" else 1.1)), dur=BAR,
        slow_music=True, grade=dict(lift=0.08))
    # the finale runs up to the track's final hit (v2) — the cut to the logo lands on it
    fin = BAR * 1.75 if "hit_src" not in M else music_to_trailer(M["hit_src"]) - t
    add(name="finale", src="u09_finale", t_in=0.75, dur=fin, grade=dict(lift=0.14), mute_cues=("MomCall",),
        titles=[dict(t=0.3, dur=fin - 0.35, lines=title("МАМА ЗОВЁТ ДОМОЙ!", "MOM'S CALLING YOU HOME!", 960, 170, 130, -2))])
    logo_at = 0.25 if "hit_src" not in M else 0.02
    end = 4.0 if "tail_src" not in M else music_to_trailer(M["tail_src"]) - t
    add(name="end", src=("png", os.path.join(HERE, "blender", "endcard"), 30), dur=end, endcard=True, logo_at=logo_at,
        extra=[(logo_at, "Catch", 1.0 if logo_at > 0.1 else 0.6), (logo_at + 0.65, "ChalkScribble", 0.6)])
    return S, t


# ---------------------------------------------------------------- video sources
class Source:
    def __init__(self, seg):
        self.seg = seg
        src = seg["src"]
        if isinstance(src, tuple) and src[0] == "png" and not os.path.isdir(src[1]) and os.path.exists(src[1] + ".mp4"):
            src = ("file", src[1] + ".mp4")      # packed Blender render instead of the PNG frames
        n = int(round(seg["dur"] * FPS))
        if isinstance(src, tuple) and src[0] == "png":
            d, fps = src[1], src[2]
            files = sorted(f for f in os.listdir(d) if f.endswith(".png")) if os.path.isdir(d) else []
            self.frames = []
            for i in range(n):
                k = int((seg["t_in"] + i / FPS) * fps)
                self.frames.append(os.path.join(d, files[min(k, len(files) - 1)]) if files else None)
            self.proc = None
        else:
            path = src[1] if isinstance(src, tuple) else os.path.join(CLIPS, src + ".mp4")
            self.proc = subprocess.Popen([FF, "-loglevel", "quiet", "-ss", f"{seg['t_in']:.4f}", "-i", path,
                                          "-t", f"{seg['dur'] + 0.2:.4f}", "-vf", f"scale={W}:{H}:flags=lanczos,fps={FPS}",
                                          "-f", "rawvideo", "-pix_fmt", "rgb24", "-"], stdout=subprocess.PIPE)
            self.last = np.zeros((H, W, 3), np.uint8)
            self.frames = None

    def frame(self, i):
        if self.frames is not None:
            p = self.frames[min(i, len(self.frames) - 1)]
            if p is None:
                return np.zeros((H, W, 3), np.uint8)
            im = Image.open(p).convert("RGB")
            if im.size != (W, H):
                im = im.resize((W, H), Image.LANCZOS)
            return np.asarray(im)
        raw = self.proc.stdout.read(W * H * 3)
        if len(raw) == W * H * 3:
            self.last = np.frombuffer(raw, np.uint8).reshape(H, W, 3)
        return self.last

    def close(self):
        if self.proc:
            self.proc.stdout.close()
            self.proc.wait()


def zoom(img, z, cy=0.5, cx=0.5):
    """Center crop by z (>1) with the crop centre at height cy (0 top .. 1 bottom), scaled back to full frame."""
    if not z or z <= 1.0:
        return img
    h, w = img.shape[:2]
    ch, cw = int(h / z), int(w / z)
    y0 = int(min(max(cy * h - ch / 2, 0), h - ch))
    x0 = int(min(max(cx * w - cw / 2, 0), w - cw))
    return np.asarray(Image.fromarray(img[y0:y0 + ch, x0:x0 + cw]).resize((w, h), Image.LANCZOS))


def grade(img, g):
    x = img.astype(np.float32) / 255.0
    lift = g.get("lift", 0.0) if g else 0.0
    if lift:
        x = 1 - (1 - x) * (1 - lift)          # screen-lift the shadows of dark arenas
        x = np.power(x, 0.92)
    x = (x - 0.5) * 1.05 + 0.5                # a touch of contrast
    lum = (x * np.array([0.299, 0.587, 0.114], np.float32)).sum(-1, keepdims=True)
    x = lum + (x - lum) * 1.08                # and saturation
    return np.clip(x, 0, 1)


# ---------------------------------------------------------------- end card logo
_logo = None


def logo_layer(t, at=0.25):
    """IT'S OUR FIELD logo pops in (overshoot), then 'СКОРО · COMING SOON' in chalk."""
    global _logo
    if _logo is None:
        im = Image.open(BRAND + "Splash_Logo_EN.png").convert("RGBA")
        s = 1150 / im.width
        _logo = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    if t < at:
        return None
    u = min(1.0, (t - at) / 0.35)
    scale = 1 + 0.18 * math.exp(-6 * u) * math.cos(9 * u) - 0.18 * (1 - u) ** 3
    a = min(1.0, (t - at) / 0.12)
    lg = _logo.resize((max(1, int(_logo.width * scale)), max(1, int(_logo.height * scale))), Image.LANCZOS)
    if a < 1:
        r, g, b, al = lg.split()
        al = al.point(lambda v: int(v * a))
        lg = Image.merge("RGBA", (r, g, b, al))
    canvas.alpha_composite(lg, (W // 2 - lg.width // 2, 215 - lg.height // 2))
    return np.asarray(canvas).astype(np.float32) / 255.0


# ---------------------------------------------------------------- audio
def load_wav(path):
    w = wave.open(path)
    x = np.frombuffer(w.readframes(w.getnframes()), np.int16).astype(np.float32) / 32768.0
    if w.getnchannels() == 2:
        x = x.reshape(-1, 2).mean(1)
    return x, w.getframerate()


def decode(path, channels=2):
    raw = subprocess.run([FF, "-loglevel", "error", "-i", path, "-ac", str(channels), "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, channels).copy()


BANK = {}
for line in open(os.path.join(HERE, "soundbank.tsv")).read().splitlines()[1:]:
    p = line.split("\t")
    BANK[p[0]] = dict(volume=float(p[1]), jitter=float(p[2]), interval=float(p[3]), ui=p[4] == "1",
                      paths=[REPO + q for q in p[5].split("|") if q])
MASTER = 0.9
_wavs = {}


def cue_sample(cue, rng):
    b = BANK.get(cue)
    if not b or not b["paths"]:
        p = SFX + cue + ".wav"
        if not os.path.exists(p):
            return None, 0
        b = dict(volume=0.8, jitter=0.0, paths=[p], ui=True)
    path = rng.choice(b["paths"])
    if path not in _wavs:
        _wavs[path] = load_wav(path)
    x, sr = _wavs[path]
    pitch = 1 + rng.uniform(-b["jitter"], b["jitter"])
    n = int(len(x) * SR / sr / pitch)
    y = np.interp(np.arange(n) * sr * pitch / SR, np.arange(len(x)), x).astype(np.float32)
    return y, b["volume"] * MASTER


def place(bus, at, y, gain, pan=0.0):
    i = int(at * SR)
    if i >= len(bus) or i + len(y) <= 0:
        return
    if i < 0:
        y = y[-i:]
        i = 0
    y = y[: len(bus) - i]
    l, r = math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)
    bus[i:i + len(y), 0] += y * gain * l * 1.41
    bus[i:i + len(y), 1] += y * gain * r * 1.41


def mix_audio(segs, total):
    n = int(total * SR) + SR
    sfx = np.zeros((n, 2), np.float32)
    rng = random.Random(4)
    for s in segs:
        if s.get("audio"):
            a = decode(s["audio"])
            sfx[int(s["at"] * SR):int(s["at"] * SR) + len(a)] += a[: n - int(s["at"] * SR)] * 0.9
        for (dt, cue, g) in s.get("extra", []):
            y, v = cue_sample(cue, rng)
            if y is not None:
                place(sfx, s["at"] + dt, y[: int(SR * 0.9)], v * g * 0.8)
        src = s["src"]
        if isinstance(src, tuple):
            continue
        last = {}
        for (t, cue, x, y_, z, cx, cy, cz, rx, ry, rz) in events(src):
            if not (s["t_in"] <= t < s["t_in"] + s["dur"]):
                continue
            if cue in s.get("mute_cues", ()):
                continue                         # the user asked to drop the game's mom-call sound
            b = BANK.get(cue, {})
            if t - last.get(cue, -9) < max(0.04, b.get("interval", 0.04)):
                continue
            last[cue] = t
            smp, v = cue_sample(cue, rng)
            if smp is None:
                continue
            d = np.array([x - cx, y_ - cy, z - cz])
            dist = float(np.linalg.norm(d))
            pan = 0.0 if b.get("ui") else float(np.clip(np.dot(d, [rx, ry, rz]) / max(dist, 1e-3), -1, 1)) * 0.6
            att = float(np.clip(1 - (dist - 12) / 48, 0, 1))
            gain = v * (0.55 + 0.45 * att)
            place(sfx, s["at"] + (t - s["t_in"]), smp, gain, pan)
    sfx = np.tanh(sfx * 1.1) * 0.85

    music = decode(M["path"]) * M["gain"]
    mus = np.zeros((n, 2), np.float32)
    cur = int(round(MUSIC_AT * SR))
    xf = int(0.03 * SR)                                   # 30 ms equal-power crossfade at the plan's cuts
    for k, (a, b) in enumerate(M["plan"]):
        piece = music[int(round(a * SR)): None if b is None else int(round(b * SR))].copy()
        if k > 0 and len(piece) > xf:
            ramp = np.sin(np.linspace(0, math.pi / 2, xf))[:, None].astype(np.float32)
            piece[:xf] *= ramp
            mus[cur - xf:cur] *= np.cos(np.linspace(0, math.pi / 2, xf))[:, None].astype(np.float32)
            cur -= xf
        if cur < 0:
            piece, cur = piece[-cur:], 0
        piece = piece[: max(0, n - cur)]
        mus[cur:cur + len(piece)] += piece
        cur += len(piece)
    env = np.ones(n, np.float32) * 0.62
    end_fade = int((total - M["end_fade"]) * SR)
    env[end_fade:] *= np.linspace(1, 0, n - end_fade) ** 1.5
    for s in segs:
        if s.get("slow_music"):
            a, b = int(s["at"] * SR), int((s["at"] + s["dur"]) * SR)
            # muffled music during the slow-motion catch
            lp = mus[a:b].copy()
            k = 1 - math.exp(-2 * math.pi * 500 / SR)
            for ch in range(2):
                acc = 0.0
                col = lp[:, ch]
                out = np.empty_like(col)
                for j in range(len(col)):
                    acc += k * (col[j] - acc)
                    out[j] = acc
                lp[:, ch] = out
            ramp = np.ones(b - a, np.float32)
            r = int(0.08 * SR)
            ramp[:r] = np.linspace(0, 1, r)
            ramp[-r:] = np.linspace(1, 0, r)
            mus[a:b] = mus[a:b] * (1 - ramp[:, None]) + lp * 1.6 * ramp[:, None]
    mus *= env[:, None]
    out = np.tanh((mus + sfx * 0.8) * 1.0) * 0.95
    out = out[: int(total * SR)]
    path = os.path.join(HERE, "out", "trailer_audio.wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(2); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((np.clip(out, -1, 1) * 32767).astype(np.int16).tobytes())
    return path


# ---------------------------------------------------------------- render
def main():
    segs, total = build_segments()
    for s in segs:
        print(f"{s['at']:6.2f}  {s['dur']:5.2f}  {s['name']}  in={s['t_in']:.2f}")
    print("total", round(total, 2))
    audio = mix_audio(segs, total)
    tmp = os.path.join(HERE, "out", "trailer_video.mp4")
    enc = subprocess.Popen([FF, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                            "-r", str(FPS), "-i", "-", "-c:v", "libx264", "-preset", "veryfast" if FAST else "slow",
                            "-crf", "23" if FAST else "16", "-pix_fmt", "yuv420p", tmp], stdin=subprocess.PIPE)
    # titles live on the timeline, not in a clip: one can span several short cuts
    layers = []
    for k, s in enumerate(segs):
        for ti in s.get("titles", []):
            fr = chalk.text_layer(ti["lines"], seed=k * 13 + 1)
            x0, x1 = chalk.bounds(ti["lines"])
            layers.append((dict(ti, t=s["at"] + ti["t"]), fr, x0, x1))
    for s in segs:
        src = Source(s)
        n = int(round((s["at"] + s["dur"]) * FPS)) - int(round(s["at"] * FPS))
        for i in range(n):
            t = i / FPS
            raw = src.frame(i)
            if s.get("zoom"):
                z0, z1, cy, *cx = s["zoom"]
                raw = zoom(raw, z0 + (z1 - z0) * i / max(1, n - 1), cy, cx[0] if cx else 0.5)
            img = raw.astype(np.float32) / 255.0 if isinstance(s["src"], tuple) else grade(raw, s.get("grade"))
            for ti, fr, x0, x1 in layers:
                lt = s["at"] + t - ti["t"]
                if lt < 0 or lt > ti["dur"]:
                    continue
                f = fr[int(lt * 12) % 3]
                f = chalk.reveal(f, min(1.0, lt / 0.22), x0, x1)
                a = f[..., 3:4]
                if ti.get("fade_out", True) and ti["dur"] - lt < 0.15:
                    a = a * ((ti["dur"] - lt) / 0.15)
                img = img * (1 - a) + f[..., :3] * a
            if s.get("endcard"):
                lg = logo_layer(t, s.get("logo_at", 0.25))
                if lg is not None:
                    a = lg[..., 3:4]
                    img = img * (1 - a) + lg[..., :3] * a
                soon_at = s.get("logo_at", 0.25) + 0.65
                if t > soon_at:
                    if "soon" not in s:
                        s["soon"] = chalk.text_layer([("СКОРО", N, 110, (960, 395), -2), ("COMING SOON", C, 60, (975, 475), -1)], seed=77)
                        s["soon_x"] = chalk.bounds([("СКОРО", N, 110, (960, 395), -2), ("COMING SOON", C, 60, (975, 475), -1)])
                    f = chalk.reveal(s["soon"][int(t * 12) % 3], min(1, (t - soon_at) / 0.3), *s["soon_x"])
                    a = f[..., 3:4]
                    img = img * (1 - a) + f[..., :3] * a
                fade = max(0.0, (t - (s["dur"] - 0.5)) / 0.5)
                img = img * (1 - fade)
            if s.get("flash") and i < 4:
                img = img + (1 - img) * (1 - i / 4) * 0.85
            enc.stdin.write((np.clip(img, 0, 1) * 255).astype(np.uint8).tobytes())
        src.close()
        print("rendered", s["name"], flush=True)
    enc.stdin.close()
    enc.wait()
    subprocess.run([FF, "-y", "-loglevel", "error", "-i", tmp, "-i", audio, "-c:v", "copy", "-c:a", "aac", "-b:a", "256k",
                    "-shortest", "-movflags", "+faststart", OUT], check=True)
    print("OK", OUT)


if __name__ == "__main__":
    main()

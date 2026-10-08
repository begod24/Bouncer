"""Instagram / TikTok photo posts in the game's look: everything lies on the yard asphalt.
Run: python photos.py <renders_dir> <frames_dir> [out_dir]   (renders from blender/social.py, frames from the trailer clips)
Writes <out>/ru/... and <out>/en/...: key art (4:5 + 9:16 story), kid gum-wrapper cards (cover + 4),
boss notebook pages (cover + 5), film photos of the yards (cover + 6), avatar."""
import math, os, sys, random
import numpy as np
from PIL import Image, ImageDraw
import social_art as A
from social_art import Mask, NEUCHA, CAVEAT, CHALK, YELLOW, INK, RED_PEN

R = sys.argv[1]
FR = sys.argv[2]
OUT = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "photos")
W, H = 1080, 1350
WS, HS = 1080, 1920

KIDS = [
    dict(id="Kid_Otlichnik", n=1, ru="ОТЛИЧНИК", en="BRAINIAC", tag_ru="Любит книги, немного заучка",
         tag_en="Loves books, a bit of a know-it-all", color=(42, 150, 96)),
    dict(id="Kid_Tolstyak", n=2, ru="ТОЛСТЯК", en="CHUBS", tag_ru="Добрый, всегда голодный, лучший друг",
         tag_en="Kind, always hungry, your best friend", color=(38, 120, 205)),
    dict(id="Kid_Melkaya", n=3, ru="МЕЛКАЯ", en="SHORTY", tag_ru="Быстрая, смелая, всегда в движении",
         tag_en="Fast, fearless, never sits still", color=(222, 62, 84)),
    dict(id="Kid_Huligan", n=4, ru="ХУЛИГАН", en="HOOLIGAN", tag_ru="Крутой драчун, главный во дворе",
         tag_en="Tough scrapper, boss of the yard", color=(240, 150, 28)),
]

BOSSES = [
    dict(id="Boss_BigRolyPoly", ru="Большая неваляшка", en="Big Roly-Poly", where_ru="Двор", where_en="The Yard", danger=2,
         desc_ru="Хозяйка двора: идёт напролом, а выбитая раскалывается на две поменьше.",
         desc_en="Queen of the yard: plows ahead, and when knocked out she splits in two.",
         tip_ru="Держи дистанцию. Когда половинок четыре — выбивай по одной.",
         tip_en="Keep your distance. When there are four, take them one at a time."),
    dict(id="Boss_Fizruk", ru="Физрук-манекен", en="PE Teacher Mannequin", where_ru="Хоккейная коробка",
         where_en="The hockey box", danger=3,
         desc_ru="Свистит новое правило: «Замри!», «Штрафной!», «Мяч в игре!», «Замена!»",
         desc_en="Whistles a new rule: Freeze!, Penalty!, Ball in play!, Substitution!",
         tip_ru="Читай правило на экране. В «Замри!» не беги!",
         tip_en="Read the rule on screen. Don't run during Freeze!"),
    dict(id="Boss_Transformer", ru="Трансформер из ларька", en="Kiosk Transformer", where_ru="Барахолка",
         where_en="The flea market", danger=4,
         desc_ru="Милицейские «Жигули», которые превращаются в робота и сносят прилавки.",
         desc_en="A police Zhiguli that turns into a robot and smashes the stalls.",
         tip_ru="Заставь его врезаться в стену — оглушённый, он открыт.",
         tip_en="Make him crash into a wall — stunned, he's wide open."),
    dict(id="Boss_Hare", ru="Большой плюшевый заяц", en="Big Plush Bunny", where_ru="Детсад", where_en="The kindergarten",
         danger=4,
         desc_ru="Скачет за тобой, бьёт приземлением и кидает морковку-бумеранг.",
         desc_en="Hops after you, slams down in a circle and throws a carrot boomerang.",
         tip_ru="Уходи из круга до приземления — после прыжка он открыт.",
         tip_en="Leave the circle before he lands — after a jump he's open."),
    dict(id="Boss_Dusk", ru="Тот, кто в сумерках", en="The One in the Dusk", where_ru="Когда стемнеет",
         where_en="When it gets dark", danger=5,
         desc_ru="Бабай с мешком. Ловит все мячи спереди и гасит фонари.",
         desc_en="The Bogeyman with a sack. Catches every ball from the front and puts out the lights.",
         tip_ru="Бей сбоку и сзади. Или продержись, пока мама не позовёт домой.",
         tip_en="Hit him from the side or back. Or hold out until mom calls you home."),
]

YARDS = [
    dict(frame="u03_throw_1.3.png", ru="ДВОР", en="THE YARD", date="'97 6 1",
         sub_ru="неваляшки, пупсы и оловянные солдатики", sub_en="roly-polys, baby dolls and tin soldiers", fy=0.5),
    dict(frame="u07_rink_1.2.png", ru="ХОККЕЙНАЯ КОРОБКА", en="THE HOCKEY BOX", date="'97 6 18",
         sub_ru="физрук свистит новые правила", sub_en="the PE teacher whistles new rules", fy=0.5),
    dict(frame="u08_transformer_4.6.png", ru="БАРАХОЛКА", en="THE FLEA MARKET", date="'97 7 5",
         sub_ru="Трансформер сносит прилавки", sub_en="the Transformer smashes the stalls", fy=0.55),
    dict(frame="u07_site_2.3.png", ru="СТРОЙКА", en="THE BUILDING SITE", date="'97 7 23",
         sub_ru="огненная лошадка и темнота", sub_en="a fire horse and the dark", fy=0.5),
    dict(frame="u07_kg_0.5.png", ru="ДЕТСАД", en="THE KINDERGARTEN", date="'97 8 9",
         sub_ru="ночью тут оживают игрушки", sub_en="at night the toys wake up", fy=0.5),
    dict(frame="u09_finale_4.2.png", ru="МАМА ЗОВЁТ ДОМОЙ", en="MOM'S CALLING YOU HOME", date="'97 8 31",
         sub_ru="успей добежать до подъезда", sub_en="make it to your door in time", fy=0.6),
]

T = {
    "soon": ("СКОРО", "COMING SOON"),
    "tagline": ("вышибалы во дворе против оживших игрушек", "backyard dodgeball against toys come to life"),
    "who": ("КТО ТЫ ИЗ ДВОРА?", "WHO WERE YOU IN THE YARD?"),
    "who_sub": ("пиши в комментах", "tell us in the comments"),
    "collect": ("собери все 4 вкладыша", "collect all 4 cards"),
    "swipe": ("листай", "swipe"),
    "wrapper": ("вкладыш", "card"),
    "of": ("из", "of"),
    "bosses_page": ("Боссы двора", "Yard bosses"),
    "where": ("Где:", "Where:"),
    "danger": ("Опасность:", "Danger:"),
    "how": ("Как победить:", "How to win:"),
    "boss": ("БОСС!", "BOSS!"),
    "summer": ("ОДНО ЛЕТО — ШЕСТЬ ДВОРОВ", "ONE SUMMER — SIX YARDS"),
}


def t(key, lang):
    return T[key][0 if lang == "ru" else 1]


def save(img, lang, name):
    d = os.path.join(OUT, lang)
    os.makedirs(d, exist_ok=True)
    p = os.path.join(d, name)
    (img if isinstance(img, Image.Image) else A.to_im(img)).convert("RGB").save(p, quality=95)
    print("wrote", p, flush=True)


def coeffs(dst, src):
    m = []
    for (x, y), (u, v) in zip(dst, src):
        m.append([x, y, 1, 0, 0, 0, -u * x, -u * y])
        m.append([0, 0, 0, x, y, 1, -v * x, -v * y])
    return np.linalg.solve(np.array(m, float), np.array(src, float).reshape(8))


def ground(mask_flat, w, h, quad):
    """Warp a flat mask (Mask) onto a ground quad (tl, tr, br, bl) of a w x h image -> Mask."""
    fw, fh = mask_flat.w, mask_flat.h
    c = coeffs(quad, [(0, 0), (fw, 0), (fw, fh), (0, fh)])
    out = Mask(w, h)
    out.im = mask_flat.im.transform((w, h), Image.PERSPECTIVE, tuple(c), Image.BICUBIC)
    return out


def swipe_hint(img, lang, x, y, seed):
    m = Mask(img.shape[1], img.shape[0])
    m.text(t("swipe", lang), CAVEAT, 54, (x - 30, y), 2, anchor="rm")
    m.arrow([(x, y + 6), (x + 55, y - 12), (x + 115, y + 4)], 6, 26, seed)
    return A.chalk_put(img, m, CHALK, seed)


# ---------------------------------------------------------------- key art
def keyart(lang):
    for res, src, name in (((W, H), "key_4x5.png", "01_keyart_4x5.jpg"), ((WS, HS), "key_9x16.png", "01_keyart_story_9x16.jpg")):
        w, h = res
        img = A.to_f(Image.open(os.path.join(R, src)).resize(res, Image.LANCZOS))
        # a little darker at the top for the logo, a little at the bottom for the chalk
        ys = np.linspace(0, 1, h)[:, None, None]
        img = img * (1 - 0.28 * np.clip(1 - ys / 0.28, 0, 1)) * (1 - 0.18 * np.clip((ys - 0.8) / 0.2, 0, 1))
        lg = A.logo(int(w * 0.84))
        ly = int(h * (0.075 if h == H else 0.11))
        img = A.paste_rgba(img, lg, ((w - lg.width) // 2, ly), shadow=0.55, blur=10, off=(4, 7))
        sub = Mask(w, h)
        sub.text(t("tagline", lang), CAVEAT, int(w * 0.05), (w // 2, ly + lg.height + int(w * 0.055)), 0)
        sub.wave(w * 0.28, w * 0.72, ly + lg.height + int(w * 0.1), 7, 6, 30, seed=3)
        img = A.chalk_put(img, sub, CHALK, 11)
        # «СКОРО» chalked on the court in front of the kids, in perspective
        fl = Mask(1600, 520)
        fl.text(t("soon", lang), NEUCHA, 330 if lang == "ru" else 210, (800, 250), 0, spacing=18 if lang == "ru" else 10)
        fl.wave(330, 1270, 470, 16, 12, 60, seed=5)
        if h == H:
            quad = [(250, 1115), (830, 1115), (1110, 1335), (-30, 1335)]
        else:
            quad = [(210, 1480), (870, 1480), (1150, 1820), (-70, 1820)]
        g = ground(fl, w, h, quad)
        img = A.put(img, A.chalk(g.array(), 21, 0.55), CHALK, 0.92)
        save(img, lang, name)


# ---------------------------------------------------------------- gum-wrapper kid cards
def wrapper(k, lang, seed=0):
    cw, ch = 800, 1100
    card = A.zigzag_paper(cw, ch, 14, seed=seed)
    col = np.array(k["color"], np.float32) / 255
    f = np.asarray(card, np.float32) / 255
    rgb, al = f[..., :3], f[..., 3]
    x0, y0, x1, y1 = 26, 40, cw - 26, 880
    ys, xs = np.mgrid[0:ch, 0:cw].astype(np.float32)
    inside = (xs >= x0) & (xs < x1) & (ys >= y0) & (ys < y1)
    ang = np.arctan2(ys - 480, xs - cw / 2)
    rays = (np.sin(ang * 14) > 0).astype(np.float32)
    band = col[None, None, :] * (0.88 + 0.16 * rays[..., None])
    dots = A.halftone(cw, ch, 7, 15)
    band = band * (1 - 0.14 * dots[..., None])
    rgb = np.where(inside[..., None], band, rgb)
    card = Image.fromarray((np.dstack([rgb, al]) * 255).astype(np.uint8), "RGBA")
    # the picture window
    pic = A.cover_crop(Image.open(os.path.join(R, f"kid_card_{k['id']}.png")).convert("RGB"), 684, 650, 0.5, 0.36, 1.22)
    frame = Image.new("RGBA", (684 + 20, 650 + 20), (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle((0, 0, 703, 669), 34, fill=(255, 253, 245, 255))
    frame.alpha_composite(A.rounded(pic, 26), (10, 10))
    card.alpha_composite(frame, (48, 160))
    # brand on top, the number badge
    lg = A.logo(430, color=(255, 252, 240))
    sh = A.logo(430, color=(20, 26, 50))
    card.alpha_composite(sh, ((cw - lg.width) // 2 + 3, 66 + 4))
    card.alpha_composite(lg, ((cw - lg.width) // 2, 66))
    d = ImageDraw.Draw(card)
    d.ellipse((40, 52, 148, 160), fill=YELLOW + (255,), outline=(30, 34, 60, 255), width=5)
    d.text((94, 108), f"№{k['n']}", font=A.font(NEUCHA, 50), fill=(30, 34, 60), anchor="mm")
    # name ribbon over the bottom of the window
    rib = Image.new("RGBA", (cw, 150), (0, 0, 0, 0))
    rd = ImageDraw.Draw(rib)
    rd.polygon([(40, 30), (cw - 40, 30), (cw - 70, 75), (cw - 40, 120), (40, 120), (70, 75)], fill=(28, 34, 68, 255))
    rd.polygon([(52, 38), (cw - 52, 38), (cw - 80, 75), (cw - 52, 112), (52, 112), (80, 75)], fill=YELLOW + (255,))
    name = k["ru"] if lang == "ru" else k["en"]
    rd.text((cw // 2, 76), name, font=A.font(NEUCHA, 82), fill=(28, 34, 68), anchor="mm")
    rib = rib.rotate(2.0, resample=Image.BICUBIC)
    card.alpha_composite(rib, (0, 770))
    # tagline on the paper
    d = ImageDraw.Draw(card)
    tag = k["tag_ru"] if lang == "ru" else k["tag_en"]
    lines = A.wrap(tag, CAVEAT, 50, 660)
    for i, ln in enumerate(lines):
        d.text((cw // 2, 948 + i * 52 - (len(lines) - 1) * 26), ln, font=A.font(CAVEAT, 50), fill=(40, 44, 70), anchor="mm")
    d.text((cw // 2, ch - 46), f"{t('wrapper', lang)} {k['n']} {t('of', lang)} 4 · IT'S OUR FIELD", font=A.font(CAVEAT, 30),
           fill=(120, 110, 100), anchor="mm")
    return A.creases(card, seed + 3, 2)


def doodles(img, seed, spots):
    """Chalk scraps on the asphalt around the main thing: stars, a ball, hopscotch bits."""
    m = Mask(img.shape[1], img.shape[0])
    r = random.Random(seed)
    for kind, x, y, s in spots:
        if kind == "star":
            m.star(x, y, s, 6, r.randint(0, 99), rot=r.uniform(-20, 20))
        elif kind == "ball":
            m.ball(x, y, s, 6, r.randint(0, 99))
        elif kind == "hop":
            m.hopscotch(x, y, s, 7, r.randint(0, 99))
        elif kind == "heart":
            pts = []
            for a in np.linspace(0, 2 * math.pi, 50):
                hx = 16 * math.sin(a) ** 3
                hy = 13 * math.cos(a) - 5 * math.cos(2 * a) - 2 * math.cos(3 * a) - math.cos(4 * a)
                pts.append((x + hx * s / 16, y - hy * s / 16))
            m.line(pts, 6, r.randint(0, 99))
    return A.put(img, A.chalk(m.array(), seed, 0.6), CHALK, 0.55)


def kids_posts(lang):
    cards = [wrapper(k, lang, i * 7 + 1) for i, k in enumerate(KIDS)]
    # cover: the four cards dropped on the asphalt, two by two
    img = A.asphalt(W, H, 3)
    img = doodles(img, 4, [("star", 70, 1190, 34), ("ball", 1000, 1100, 30), ("star", 1010, 250, 28), ("heart", 70, 260, 28)])
    spots = [(-6, 290, 500), (5, 790, 480), (4, 285, 905), (-5, 795, 920)]
    for (rot, cx, cy), c in zip(spots, cards):
        cs = c.resize((int(c.width * 0.43), int(c.height * 0.43)), Image.LANCZOS)
        cr = A.rotate_rgba(cs, rot)
        img = A.paste_rgba(img, cr, (cx - cr.width // 2, cy - cr.height // 2), 0.6, 14, (8, 12))
    m = Mask(W, H)
    m.text(t("who", lang), NEUCHA, 92 if lang == "ru" else 80, (W // 2, 140), 2)
    m.wave(220, 860, 210, 9, 8, 34, seed=2, rot=2)
    img = A.put(img, A.chalk(m.array(), 5), YELLOW, 0.95, shadow=0.35)
    m = Mask(W, H)
    m.text(t("collect", lang) + " · " + t("who_sub", lang), CAVEAT, 50, (W // 2, 1205), 1)
    img = A.chalk_put(img, m, CHALK, 6)
    img = swipe_hint(img, lang, 900, 1290, 8)
    save(A.vignette(img, 0.3), lang, "02_kids_0_cover.jpg")
    for i, (k, c) in enumerate(zip(KIDS, cards)):
        img = A.asphalt(W, H, 10 + i)
        img = doodles(img, 20 + i, [("star", 90, 260 + i * 40, 36), ("ball", 1000, 1150 - i * 50, 38),
                                    ("heart", 980, 330, 34), ("star", 120, 1180, 28)])
        cs = c.resize((int(c.width * 0.86), int(c.height * 0.86)), Image.LANCZOS)
        cr = A.rotate_rgba(cs, [-3, 2.5, -2, 3][i])
        img = A.paste_rgba(img, cr, ((W - cr.width) // 2, 655 - cr.height // 2 + 40), 0.6, 18, (12, 18))
        m = Mask(W, H)
        m.text(f"{k['n']}/4", NEUCHA, 64, (110, 120), 4)
        img = A.chalk_put(img, m, CHALK, 30 + i)
        if i < 3:
            img = swipe_hint(img, lang, 900, 1300, 40 + i)
        else:
            m = Mask(W, H)
            m.text(t("who", lang), NEUCHA, 58 if lang == "ru" else 50, (W // 2, 1300), 1)
            img = A.put(img, A.chalk(m.array(), 50), YELLOW, 0.95, shadow=0.35)
        save(A.vignette(img, 0.3), lang, f"02_kids_{i + 1}_{k['id'][4:].lower()}.jpg")


# ---------------------------------------------------------------- boss notebook pages
def photo_print(path, w, h, border=18, fx=0.5, fy=0.4, seed=0):
    im = A.cover_crop(Image.open(path).convert("RGB"), w, h, fx, fy)
    card = Image.new("RGBA", (w + border * 2, h + border * 2), (250, 249, 244, 255))
    card.paste(im, (border, border))
    return A.creases(card, seed, 1)


def notebook_cover(lang, seed=0):
    pw, ph = 900, 1190
    col = np.array((92, 158, 120), np.float32) / 255
    rng = np.random.default_rng(seed)
    base = np.ones((ph, pw, 3), np.float32) * col * (0.95 + 0.05 * A.vnoise(pw, ph, 50, rng=rng))[..., None]
    base *= (0.97 + 0.03 * rng.random((ph, pw)).astype(np.float32))[..., None]
    pm = Mask(pw, ph)
    pm.d.rectangle((50, 50, pw - 50, ph - 50), outline=255, width=6)
    pm.d.rectangle((66, 66, pw - 66, ph - 66), outline=255, width=2)
    word = "ТЕТРАДЬ" if lang == "ru" else "NOTEBOOK"
    pm.text(word, NEUCHA, 120, (pw // 2, 250), 0, spacing=14)
    labels = (["для", "по", "ученика", "класса", "школы"] if lang == "ru" else ["for", "subject", "name", "class", "school"])
    for i, lb in enumerate(labels):
        y = 470 + i * 120
        pm.text(lb, NEUCHA, 46, (110, y), 0, anchor="lm")
        pm.d.line((110 + A.font(NEUCHA, 46).getlength(lb) + 20, y + 26, pw - 110, y + 26), fill=255, width=3)
    print_ink = (30, 70, 50)
    base = A.put(base, pm.array(), print_ink, 0.85)
    hand = Mask(pw, ph)
    fills = (["боссов двора", "вышибалам", "Отличника", "3 «Б»", "№ 17"] if lang == "ru"
             else ["yard bosses", "dodgeball", "Brainiac", "3B", "No. 17"])
    for i, txt in enumerate(fills):
        y = 470 + i * 120
        x = 110 + A.font(NEUCHA, 46).getlength(labels[i]) + 50
        hand.text(txt, CAVEAT, 76, (x, y - 4), 1.5, anchor="lm")
    base = A.put(base, A.ink(hand.array(), seed), (24, 40, 120), 0.95)
    stick = Mask(pw, ph)
    stick.circle(pw - 190, ph - 190, 100, 70, 7, 1.2, seed)
    stick.text(t("boss", lang), CAVEAT, 64, (pw - 190, ph - 190), 12)
    base = A.put(base, A.ink(stick.array(), seed + 1), RED_PEN, 0.95)
    cover = A.to_im(base).convert("RGBA")
    return A.creases(cover, seed + 2, 2)


def boss_page(b, i, lang):
    pw, ph = 1000, 1290
    page = A.notebook(pw, ph, 36, 110, seed=i)
    # photo taped in
    pr = photo_print(os.path.join(R, f"boss_{b['id']}.png"), 520, 650, 18, 0.5, 0.3, seed=i)
    pr = A.rotate_rgba(pr, [3, -2.5, 2, -3, 2.5][i])
    page_im = A.to_im(page).convert("RGBA")
    shadow_layer = A.paste_rgba(page, pr, (48, 236), 0.35, 8, (5, 7))
    page_im = A.to_im(shadow_layer).convert("RGBA")
    for (tx, ty, rot) in ((48 + 40, 236 - 6, 35), (48 + pr.width - 170, 236 - 6, -30)):
        tp = A.rotate_rgba(A.tape(150, 50, i + tx), rot)
        page_im.alpha_composite(tp, (int(tx), int(ty)))
    page = A.to_f(page_im)
    blue, red = Mask(pw, ph), Mask(pw, ph)
    blue.text(f"{t('bosses_page', lang)} · №{i + 1}", CAVEAT, 48, (54, 70), 0, anchor="lm")
    name = b["ru"] if lang == "ru" else b["en"]
    size = 96 if len(name) < 18 else 82
    blue.text(name, CAVEAT, size, (54, 162), 1, anchor="lm", stroke=1)
    wlen = min(860, A.font(CAVEAT, size).getlength(name))
    blue.wave(54, 54 + wlen, 214, 4, 5, 26, seed=i)
    blue.wave(60, 60 + wlen * 0.9, 228, 3, 4, 22, seed=i + 9)
    # right column
    cx = 640
    blue.text(t("where", lang), CAVEAT, 46, (cx, 300), 0, anchor="lm")
    where = b["where_ru"] if lang == "ru" else b["where_en"]
    for k, ln in enumerate(A.wrap(where, CAVEAT, 50, 230)):
        blue.text(ln, CAVEAT, 50, (cx + 10, 356 + k * 46), 0, anchor="lm")
    blue.text(t("danger", lang), CAVEAT, 46, (cx, 500), 0, anchor="lm")
    for s in range(5):
        x, y = cx + 26 + s * 52, 562
        (red if s < b["danger"] else blue).star(x, y, 22, 4, i * 10 + s, fill=s < b["danger"], rot=s * 7)
    red.circle(cx + 120, 700, 110, 62, 5, 1.25, i + 3)
    red.text(t("boss", lang), CAVEAT, 72, (cx + 120, 700), 8)
    red.arrow([(cx + 20, 780), (cx - 40, 830), (cx - 80, 900)], 5, 24, i + 5)
    # text under the photo, on the grid lines (two cells per line)
    desc = b["desc_ru"] if lang == "ru" else b["desc_en"]
    y = 990
    for ln in A.wrap(desc, CAVEAT, 44, 830):
        blue.text(ln, CAVEAT, 44, (54, y), 0, anchor="lm")
        y += 54
    y += 14
    red.text(t("how", lang), CAVEAT, 50, (54, y), 0, anchor="lm")
    red.wave(54, 54 + A.font(CAVEAT, 50).getlength(t("how", lang)), y + 30, 3, 3, 18, seed=i + 11)
    y += 62
    tip = b["tip_ru"] if lang == "ru" else b["tip_en"]
    for ln in A.wrap(tip, CAVEAT, 46, 830):
        blue.text(ln, CAVEAT, 46, (54, y), 0, anchor="lm")
        y += 54
    page = A.put(page, A.ink(blue.array(), i), INK, 0.92)
    page = A.put(page, A.ink(red.array(), i + 50), RED_PEN, 0.9)
    return A.creases(A.to_im(page).convert("RGBA"), i + 21, 1)


def boss_posts(lang):
    img = A.asphalt(W, H, 60)
    img = doodles(img, 61, [("hop", 120, 1330, 100), ("ball", 960, 1240, 44), ("star", 1000, 120, 30)])
    cv = notebook_cover(lang, 3)
    cv = A.rotate_rgba(cv.resize((int(cv.width * 0.92), int(cv.height * 0.92)), Image.LANCZOS), -3)
    img = A.paste_rgba(img, cv, ((W - cv.width) // 2, (H - cv.height) // 2 - 10), 0.6, 18, (12, 18))
    img = swipe_hint(img, lang, 900, 1300, 62)
    save(A.vignette(img, 0.3), lang, "03_bosses_0_cover.jpg")
    for i, b in enumerate(BOSSES):
        img = A.asphalt(W, H, 70 + i)
        pg = A.rotate_rgba(boss_page(b, i, lang), [-1.5, 1.2, -1.0, 1.5, -1.2][i])
        img = A.paste_rgba(img, pg, ((W - pg.width) // 2, (H - pg.height) // 2), 0.6, 16, (10, 16))
        save(A.vignette(img, 0.25), lang, f"03_bosses_{i + 1}_{b['id'][5:].lower()}.jpg")


# ---------------------------------------------------------------- film photos of the yards
def yard_posts(lang):
    prints = [A.film_print(Image.open(os.path.join(FR, y["frame"])), 900, y["date"], seed=i) for i, y in enumerate(YARDS)]
    # cover: the whole summer in a pile
    img = A.asphalt(W, H, 90)
    # the first day of summer ends up on top of the pile
    pile = [(4, 560, 610), (7, 800, 420), (-4, 290, 800), (10, 790, 850), (-7, 420, 1110), (-9, 330, 410)]
    for (rot, cx, cy), p in reversed(list(zip(pile, prints))):
        ps = A.rotate_rgba(p.resize((int(p.width * 0.52), int(p.height * 0.52)), Image.LANCZOS), rot)
        img = A.paste_rgba(img, ps, (cx - ps.width // 2, cy - ps.height // 2), 0.55, 12, (8, 12))
    m = Mask(W, H)
    m.text(t("summer", lang), NEUCHA, 74 if lang == "ru" else 70, (W // 2, 120), 2)
    m.wave(170, 910, 182, 9, 8, 34, seed=91, rot=2)
    img = A.put(img, A.chalk(m.array(), 92), YELLOW, 0.95, shadow=0.35)
    img = swipe_hint(img, lang, 900, 1300, 93)
    save(A.vignette(img, 0.3), lang, "04_yards_0_cover.jpg")
    for i, (y, p) in enumerate(zip(YARDS, prints)):
        img = A.asphalt(W, H, 100 + i)
        img = doodles(img, 110 + i, [("star", 100, 1200, 30), ("ball", 990, 140, 36)])
        pr = A.rotate_rgba(p, [-3, 2.5, -2, 3, -2.5, 2][i])
        img = A.paste_rgba(img, pr, ((W - pr.width) // 2, 560 - pr.height // 2), 0.6, 16, (10, 16))
        m = Mask(W, H)
        title = y["ru"] if lang == "ru" else y["en"]
        size = 92 if len(title) < 12 else 70
        m.text(title, NEUCHA, size, (W // 2, 1000), 1.5)
        m.text(y["sub_ru"] if lang == "ru" else y["sub_en"], CAVEAT, 54, (W // 2, 1110), 0)
        img = A.chalk_put(img, m, CHALK, 120 + i)
        m = Mask(W, H)
        tw = A.font(NEUCHA, size).getlength(title)
        m.wave(W / 2 - tw / 2, W / 2 + tw / 2, 1058, 8, 7, 32, seed=130 + i, rot=1.5)
        img = A.put(img, A.chalk(m.array(), 140 + i), YELLOW, 0.95, shadow=0.3)
        m = Mask(W, H)
        m.text(f"{i + 1}/6", NEUCHA, 56, (100, 110), 4)
        img = A.chalk_put(img, m, CHALK, 150 + i)
        if i < 5:
            img = swipe_hint(img, lang, 900, 1290, 160 + i)
        save(A.vignette(img, 0.3), lang, f"04_yards_{i + 1}.jpg")


if __name__ == "__main__":
    only = os.environ.get("ONLY", "")
    for lang in ("ru", "en"):
        if not only or "key" in only:
            keyart(lang)
        if not only or "kids" in only:
            kids_posts(lang)
        if not only or "boss" in only:
            boss_posts(lang)
        if not only or "yard" in only:
            yard_posts(lang)

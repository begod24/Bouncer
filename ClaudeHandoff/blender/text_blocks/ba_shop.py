"""Shop: the «Союзпечать» newsstand (the between-rounds kiosk) and what it sells:
gum with wrapper cards in three rarities, lemonade and a sandwich (healing).
The items are modelled at an exaggerated 'item' size for icons and pickups; the kiosk shows tiny copies."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

UP = Vector((0, 0, 1))


def facing(normal, origin):
    """Frame for text/decals on a vertical face with the given outward normal: local X reads left to
    right for someone looking at the face, local Y is up, local Z points out of the face."""
    n = Vector(normal).normalized()
    r = UP.cross(n)
    m = Matrix((r, UP, n)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def ccw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return pts if area > 0 else list(reversed(pts))


def plate(b, m, w, h, color, depth=0.01, x=0.0, y=0.0):
    """Thin rectangle standing proud of a face (m = facing(...)): centre (x, y) in the face plane."""
    c = m @ Vector((x, y, depth / 2))
    rot = m.to_3x3()
    mm = rot.to_4x4()
    mm.translation = c
    return b.box_m(mm, (w, h, depth), color)


# ================================================================ kiosk «Союзпечать»
MAGAZINES = ["red", "yellow", "green_light", "pink", "sky", "orange", "lavender", "teal", "coral", "blue_light"]


def _magazine(b, m, x, y, w, h, rnd, depth):
    col = rnd.choice(MAGAZINES)
    plate(b, m, w, h, col, depth, x, y)
    plate(b, m, w * 0.8, h * 0.14, "white", depth + 0.006, x, y + h * 0.32)          # title
    if rnd.random() < 0.7:                                                            # cover picture
        plate(b, m, w * 0.55, h * 0.36, rnd.choice(["cream", "yellow", "sky", "white", "pink_light"]),
              depth + 0.006, x + rnd.uniform(-0.03, 0.03), y - h * 0.08)


def _newspaper(b, m, x, y, w, h, depth):
    plate(b, m, w, h, "cream", depth, x, y)
    plate(b, m, w * 0.86, h * 0.12, "grey_dark", depth + 0.005, x, y + h * 0.36)     # masthead
    for k in range(4):
        plate(b, m, w * 0.36, 0.012, "grey", depth + 0.005, x - w * 0.22, y + h * (0.16 - 0.12 * k))
    plate(b, m, w * 0.36, h * 0.42, "grey_light", depth + 0.005, x + w * 0.2, y - h * 0.06)   # photo


def _gum_row(b, m, x0, y, n, depth, rnd, size=(0.07, 0.035)):
    for k in range(n):
        col = rnd.choice(["sky", "pink", "yellow", "lavender", "lime", "orange"])
        plate(b, m, size[0], size[1], col, depth, x0 + k * (size[0] + 0.012), y)
        plate(b, m, size[0] * 0.5, size[1] * 0.3, "white", depth + 0.004, x0 + k * (size[0] + 0.012), y)


def build_kiosk(col, location, name="Prop_Kiosk"):
    """Newsstand ~2.4 x 1.7 m, 3 m to the top of the sign. Front (-Y): the sales window with a
    counter, magazines and newspapers behind the glass, the sign «СОЮЗПЕЧАТЬ»; sides say
    «ГАЗЕТЫ» and «ЖУРНАЛЫ»; the door is at the back. The sales window glows at dusk (window_lit)."""
    L.remove_tree(name)
    rnd = random.Random(21)
    W, D = 2.4, 1.7
    Z0, Z1, Z2, Z3 = 0.1, 0.95, 2.2, 2.3            # plinth top, glass bottom, glass top, roof bottom
    paint, frame, glass = "blue_light", "white", "window"
    b = Builder()
    # plinth and the solid lower part with a trim strip
    b.box((0, 0, Z0 / 2), (W + 0.12, D + 0.12, Z0), "concrete")
    b.box((0, 0, (Z0 + Z1) / 2), (W, D, Z1 - Z0), paint)
    b.box((0, 0, Z1), (W + 0.03, D + 0.03, 0.05), frame)
    for sx in (-1, 1):                                  # vertical seams of the metal sheets
        b.box((sx * W / 6, -D / 2 - 0.003, (Z0 + Z1) / 2), (0.02, 0.01, Z1 - Z0 - 0.06), "blue")
    for k in range(5):                                  # a bit of rust along the bottom
        b.box((rnd.uniform(-W / 2 + 0.2, W / 2 - 0.2), -D / 2 - 0.004, Z0 + rnd.uniform(0.04, 0.16)),
              (rnd.uniform(0.05, 0.14), 0.01, rnd.uniform(0.03, 0.07)), "rust")
    # glass band: dark panes behind a white frame
    b.box((0, 0, (Z1 + Z2) / 2), (W - 0.04, D - 0.04, Z2 - Z1), glass)
    b.box((0, 0, Z2 + (Z3 - Z2) / 2), (W + 0.02, D + 0.02, Z3 - Z2), frame)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * (W / 2 - 0.03), sy * (D / 2 - 0.03), (Z1 + Z2) / 2), (0.08, 0.08, Z2 - Z1), frame)
    bay = 0.8                                          # front: magazines | sales window | newspapers
    for sx in (-1, 1):
        b.box((sx * (W / 2 - bay), -D / 2 + 0.01, (Z1 + Z2) / 2), (0.06, 0.06, Z2 - Z1), frame)
    # ---- front (-Y)
    fm = facing((0, -1, 0), (0, -D / 2 + 0.02, 0))
    zc = (Z1 + Z2) / 2
    # left bay: magazines 2 x 3
    for i in range(2):
        for j in range(3):
            _magazine(b, fm, -W / 2 + 0.23 + i * 0.34, Z1 + 0.22 + j * 0.4, 0.28, 0.36, rnd, 0.012)
    # right bay: newspapers on top, gum packs below
    for i in range(2):
        _newspaper(b, fm, W / 2 - 0.23 - i * 0.34, Z2 - 0.28, 0.3, 0.4, 0.012)
        _newspaper(b, fm, W / 2 - 0.23 - i * 0.34, Z2 - 0.72, 0.3, 0.4, 0.012)
    _gum_row(b, fm, W / 2 - bay + 0.1, Z1 + 0.12, 7, 0.012, rnd)
    _gum_row(b, fm, W / 2 - bay + 0.1, Z1 + 0.2, 7, 0.012, rnd)
    # centre bay: the sales window (glowing recess), counter, lamp, price list above
    ww, wz0, wz1 = 0.56, 1.05, 1.5
    b.box((0, -D / 2 + 0.012, (wz0 + wz1) / 2), (ww, 0.012, wz1 - wz0), "window_lit")
    b.box((0, -D / 2 - 0.005, wz1 + 0.025), (ww + 0.1, 0.05, 0.05), frame)
    b.box((0, -D / 2 - 0.005, wz0 - 0.025), (ww + 0.1, 0.05, 0.05), frame)
    for sx in (-1, 1):
        b.box((sx * (ww / 2 + 0.025), -D / 2 - 0.005, (wz0 + wz1) / 2), (0.05, 0.05, wz1 - wz0 + 0.1), frame)
    b.box((0, -D / 2 - 0.14, wz0 - 0.07), (ww + 0.34, 0.3, 0.04), "wood")                # counter shelf
    for sx in (-1, 1):
        b.beam((sx * (ww / 2 + 0.1), -D / 2 - 0.01, wz0 - 0.35), (sx * (ww / 2 + 0.1), -D / 2 - 0.26, wz0 - 0.09),
               0.03, 0.03, "grey")                                                    # brackets
    cm = facing((0, -1, 0), (0, -D / 2 + 0.005, 0))                                     # goods inside the window
    _gum_row(b, cm, -0.2, wz0 + 0.08, 6, 0.01, rnd, size=(0.06, 0.03))
    _gum_row(b, cm, -0.2, wz0 + 0.14, 6, 0.01, rnd, size=(0.06, 0.03))
    for k in range(3):                                                               # packs on the counter
        b.box((-0.2 + k * 0.1, -D / 2 - 0.16, wz0 - 0.035), (0.07, 0.035, 0.03),
              ["sky", "pink", "yellow"][k], rot=(0, 0, rnd.uniform(-0.3, 0.3)))
    b.cyl((0, -D / 2 - 0.08, wz1 + 0.2), (0, -D / 2 - 0.17, wz1 + 0.2), 0.02, 0.02, segs=6, color="grey")
    b.cyl((0, -D / 2 - 0.17, wz1 + 0.22), (0, -D / 2 - 0.17, wz1 + 0.14), 0.04, 0.055, segs=8,
          color=lambda f: "lamp" if f.normal.z < -0.9 else "grey")                        # lamp over the window
    plate(b, fm, 0.5, 0.42, "cream", 0.012, 0, Z2 - 0.3)                               # price list
    for k in range(5):
        plate(b, fm, 0.36, 0.022, "grey_dark", 0.018, -0.02, Z2 - 0.15 - k * 0.065)
        plate(b, fm, 0.06, 0.022, "red", 0.018, 0.18, Z2 - 0.15 - k * 0.065)
    # ---- sides (-X: newspapers, +X: magazines and a poster)
    for sx, kind in ((-1, "papers"), (1, "mags")):
        sm = facing((sx, 0, 0), (sx * (W / 2 - 0.02), 0, 0))
        for i in range(3):
            for j in range(2):
                x = -D / 2 + 0.32 + i * 0.52
                y = Z1 + 0.38 + j * 0.56
                if kind == "papers":
                    _newspaper(b, sm, x, y, 0.4, 0.48, 0.012)
                else:
                    _magazine(b, sm, x, y, 0.36, 0.46, rnd, 0.012)
        for i in range(2):
            b.box((sx * (W / 2 - 0.01), -D / 2 + 0.58 + i * 0.52, zc), (0.05, 0.05, Z2 - Z1), frame)
    # ---- back (+Y): painted, with the door
    b.box((0, D / 2 - 0.015, zc), (W - 0.06, 0.02, Z2 - Z1), paint)
    bm_ = facing((0, 1, 0), (0, D / 2, 0))
    plate(b, bm_, 0.7, 1.95, "blue", 0.02, 0.5, Z0 + 1.0)
    plate(b, bm_, 0.42, 0.5, glass, 0.03, 0.5, Z0 + 1.5)
    plate(b, bm_, 0.05, 0.14, "tin", 0.05, 0.25, Z0 + 1.0)
    # ---- roof slab, then the signs
    b.box((0, 0, Z3 + 0.06), (W + 0.36, D + 0.36, 0.12), lambda f: "grey" if f.normal.z > 0.9 else frame)
    for y in (-0.25, 0.35):                                                           # roofing seams
        b.box((0, y, Z3 + 0.122), (W + 0.3, 0.05, 0.006), "grey_dark")
    b.box((0.55, 0.35, Z3 + 0.24), (0.28, 0.28, 0.24), "grey_light")                    # vent
    b.box((0.55, 0.35, Z3 + 0.37), (0.36, 0.36, 0.04), "grey_dark")
    zs = Z3 + 0.12 + 0.3
    b.box((0, -D / 2 - 0.08, zs), (W + 0.16, 0.08, 0.56), frame)                      # front sign board
    b.box((0, -D / 2 - 0.08, zs - 0.3), (W + 0.2, 0.1, 0.04), "red")
    b.box((0, -D / 2 - 0.08, zs + 0.3), (W + 0.2, 0.1, 0.04), "red")
    for sx in (-1, 1):
        b.beam((sx * 0.9, -D / 2 - 0.04, Z3 + 0.12), (sx * 0.9, -D / 2 + 0.3, zs + 0.2), 0.04, 0.04, "grey")
    b.decal("sign_soyuzpechat", facing((0, -1, 0), (0, -D / 2 - 0.12, zs - 0.01)), "red", width=2.3)
    for sx, word, width in ((-1, "sign_gazety", 1.0), (1, "sign_zhurnaly", 1.15)):
        b.box((sx * (W / 2 + 0.1), 0, Z3 + 0.12 + 0.2), (0.06, D * 0.8, 0.36), frame)
        b.decal(word, facing((sx, 0, 0), (sx * (W / 2 + 0.13), 0, Z3 + 0.32)), "blue", width=width)
    return L.mesh_root(b, name, col, location=location, pivot=(0, 0, 0))


# ================================================================ gum with a wrapper card
# rarity: (wrapper, band, accent, name)
GUMS = {
    "Item_Gum_Common": ("sky", "white", "blue", "gum_bum"),
    "Item_Gum_Rare": ("purple", "pink_light", "purple", "gum_bum"),
    "Item_Gum_Gold": ("gold", "red", "yellow", "gum_bum"),
}


def build_gum(col, name, location):
    """Stick of bubble gum in a waxed wrapper, 0.4 m wide at item size, standing, print to the front.
    The rarity is the wrapper colour; rare gets stars, gold gets a star and rays."""
    L.remove_tree(name)
    wrap, band, accent, word = GUMS[name]
    W, H, T = 0.4, 0.17, 0.06
    b = Builder()
    b.box((0, 0, H / 2), (W, T, H), wrap)
    for sx in (-1, 1):                                        # folded, slightly pinched ends
        b.prism([(0, 0), (0.035, 0.012), (0.035, H - 0.012), (0, H)], T * 0.8,
                wrap if name != "Item_Gum_Gold" else "ochre",
                matrix=Matrix.Translation((sx * W / 2, T * 0.4, 0)) @ Matrix.Rotation(math.pi / 2, 4, "X")
                @ Matrix.Diagonal((sx, 1, 1, 1)))
    fm = facing((0, -1, 0), (0, -T / 2, 0))
    plate(b, fm, W * 0.9, H * 0.42, band, 0.006, 0, H * 0.5)                            # logo band
    b.decal(word, fm @ Matrix.Translation((0.025, H * 0.5, 0.006)), accent, height=H * 0.36)
    b.decal("shape_bolt", fm @ Matrix.Translation((-W * 0.33, H * 0.5, 0.006)), accent if name != "Item_Gum_Common" else "yellow",
            height=0.13)
    stars = {"Item_Gum_Common": [], "Item_Gum_Rare": [(0.15, 0.14), (-0.16, 0.035), (0.17, 0.03)],
             "Item_Gum_Gold": [(0.16, 0.135), (-0.16, 0.13), (-0.15, 0.035), (0.16, 0.035)]}[name]
    for x, y in stars:
        b.decal("shape_star", fm @ Matrix.Translation((x, y, 0.0)), "white" if name == "Item_Gum_Rare" else "yellow",
                height=0.045)
    # the wrapper card peeking out of the top
    b.box((0.06, 0.0, H + 0.03), (0.13, 0.012, 0.08), "white", rot=(0, 0.18, 0))
    b.box((0.06, -0.007, H + 0.035), (0.09, 0.004, 0.04), accent if name != "Item_Gum_Common" else "red",
          rot=(0, 0.18, 0))
    return L.mesh_root(b, name, col, location=location, pivot=(0, 0, 0))


# ================================================================ lemonade
def build_lemonade(col, location, name="Item_Lemonade"):
    """Half-litre lemonade bottle, 0.56 m at item size: green glass, crown cap, yellow label «ЛИМОНАД»."""
    L.remove_tree(name)
    s = 1.85
    prof = [(0.0, 0.0), (0.036, 0.0), (0.04, 0.012), (0.041, 0.16), (0.038, 0.19), (0.026, 0.225),
            (0.015, 0.25), (0.0135, 0.285), (0.016, 0.29), (0.016, 0.298), (0.0, 0.3)]
    prof = [(r * s, z * s) for r, z in prof]
    b = Builder()
    bottle = Lathe(prof, 12)
    b.lathe(bottle, lambda f: "gold" if f.calc_center_median().z > 0.286 * s else "green_light")
    for z in (0.02, 0.15):                                    # glass rings
        b.cyl((0, 0, z * s), (0, 0, (z + 0.006) * s), 0.0415 * s, 0.0415 * s, segs=12, color="green")
    # label band around the front half, facet by facet (same angles as the bottle's lathe)
    step = math.pi / 6
    phase = -math.pi / 2 - step / 2
    for k in range(-2, 3):
        a0 = phase + k * step
        a1 = a0 + step
        mid = (math.cos((a0 + a1) / 2), math.sin((a0 + a1) / 2), 0)
        for z0, z1, r, colr in ((0.055, 0.13, 0.0425, "yellow"), (0.058, 0.064, 0.0433, "orange"),
                                (0.121, 0.127, 0.0433, "orange")):
            pts = [(r * s * math.cos(a), r * s * math.sin(a), z * s) for a, z in ((a0, z0), (a1, z0), (a1, z1), (a0, z1))]
            b.quad(pts, colr, want=mid)
    # lemon slice and a leaf on the label
    b.cyl((0, -0.040 * s, 0.093 * s), (0, -0.0445 * s, 0.093 * s), 0.019 * s, 0.019 * s, segs=10,
          color=lambda f: "cream" if f.normal.y < -0.9 else "yellow")
    for k in range(6):
        a = k * math.pi / 3
        b.box((0.0095 * s * math.cos(a), -0.0448 * s, 0.093 * s + 0.0095 * s * math.sin(a)),
              (0.012 * s, 0.001, 0.003 * s), "yellow", rot=(0, -a, 0))
    b.box((0.022 * s, -0.0435 * s, 0.108 * s), (0.02 * s, 0.002, 0.008 * s), "green", rot=(0, -0.6, 0))
    return L.mesh_root(b, name, col, location=location, pivot=(0, 0, 0))


# ================================================================ sandwich
def build_sandwich(col, location, name="Item_Sandwich"):
    """Slice of bread with butter and two rounds of sausage, lying flat, 0.36 m wide at item size."""
    L.remove_tree(name)
    b = Builder()
    w, d, t = 0.36, 0.3, 0.045
    # bread slice outline in XY: flat bottom, domed top (the crust of a brick loaf)
    pts = [(-w / 2, -d / 2), (w / 2, -d / 2), (w / 2, d * 0.15)]
    for k in range(1, 8):
        a = k * math.pi / 8
        pts.append((w / 2 * math.cos(a), d * 0.15 + d * 0.35 * math.sin(a)))
    pts.append((-w / 2, d * 0.15))
    b.prism(pts, t, lambda f: "sand" if abs(f.normal.z) > 0.9 else "wood_light")
    # crust ring on top
    b.prism(pts, 0.004, "ochre", matrix=Matrix.Translation((0, 0, t)) @ Matrix.Diagonal((1.0, 1.0, 1, 1)))
    inner = [(x * 0.88, y * 0.88 - 0.004) for x, y in pts]
    b.prism(inner, 0.008, "sand", matrix=Matrix.Translation((0, 0, t)))
    # butter
    butter = [(x * 0.8, y * 0.8 - 0.006) for x, y in pts]
    b.prism(butter, 0.012, "cream", matrix=Matrix.Translation((0, 0, t + 0.004)))
    # two rounds of sausage, slightly overlapping
    for x, y, zr in ((-0.055, -0.01, 0.0), (0.06, 0.02, 0.012)):
        b.cyl((x, y, t + 0.014 + zr), (x, y, t + 0.03 + zr), 0.085, 0.085, segs=12,
              color=lambda f: "pink" if abs(f.normal.z) > 0.9 else "red_light")
    for x, y, r in ((0.03, 0.0, 0.6), (0.09, 0.05, -0.4), (0.07, -0.03, 1.4)):          # dill
        b.box((x, y, t + 0.044), (0.032, 0.008, 0.004), "green", rot=(0, 0, r))
    return L.mesh_root(b, name, col, location=location, pivot=(0, 0, 0))


def build_all(col_parent):
    col = L.collection("Shop", col_parent)
    out = [build_kiosk(col, (-12.0, 0.0, 0.0))]
    x = -9.5
    for name in GUMS:
        out.append(build_gum(col, name, (x, -3.0, 0.0)))
        x += 0.6
    out.append(build_lemonade(col, (x + 0.2, -3.0, 0.0)))
    out.append(build_sandwich(col, (x + 0.9, -3.0, 0.0)))
    return out

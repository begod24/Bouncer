"""Arena «Барахолка» (evening; the fork alternative to the hockey box): a 90s flea market on a vacant lot.
Rows of stalls (cover; balls bounce off them; the transformer's ram smashes them), carts with goods (a ball pushes
them and they roll into enemies), piles of cardboard boxes (a strong ball topples them), commercial kiosks
(«комки»; the toy kiosk is where the transformer boss drives out of), shipping containers, strings of light bulbs,
the market gate «РЫНОК» and the ground. Breakable props keep their pieces as separate child parts (Unity turns
them into debris). Lamps and lit kiosk windows use the lamp / window_lit cells, so they glow in the evening.
Props: origin on the ground, front to -Y. Env_BazaarGround is in arena coordinates (x east, y north)."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

BAZ_W, BAZ_D = 44.0, 30.0
FACE_FRONT = (math.pi / 2, 0.0, 0.0)          # decal facing -Y


def ccw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return pts if area > 0 else list(reversed(pts))


def _part(builder, name, col, parent, pivot):
    return builder.to_object(name, col, pivot=pivot, parent=parent)


# ================================================================ small goods
def _plaid_bag(b, c, size, rnd, colors=("red", "blue", "white")):
    """The checked «челночная» bag: a white box with red and blue bands, two handles."""
    c, (sx, sy, sz) = Vector(c), size
    b.box(c, size, colors[2])
    for k in range(3):
        x = c.x - sx / 2 + sx * (k + 0.5) / 3
        b.box((x, c.y, c.z), (sx * 0.12, sy + 0.012, sz + 0.012), colors[0])
    for k in range(2):
        z = c.z - sz / 2 + sz * (k + 1) / 3
        b.box((c.x, c.y, z), (sx + 0.014, sy + 0.014, sz * 0.1), colors[1])
    for dy in (-sy * 0.25, sy * 0.25):
        b.beam((c.x - sx * 0.2, c.y + dy, c.z + sz / 2), (c.x - sx * 0.1, c.y + dy, c.z + sz / 2 + 0.1), 0.02, 0.02, "black")
        b.beam((c.x - sx * 0.1, c.y + dy, c.z + sz / 2 + 0.1), (c.x + sx * 0.1, c.y + dy, c.z + sz / 2 + 0.1), 0.02, 0.02, "black")
        b.beam((c.x + sx * 0.1, c.y + dy, c.z + sz / 2 + 0.1), (c.x + sx * 0.2, c.y + dy, c.z + sz / 2), 0.02, 0.02, "black")


def _cardboard(b, c, size, rnd, label=None):
    """A cardboard box: tape across the top, sometimes a printed label or «this side up» arrows."""
    c = Vector(c)
    col = rnd.choice(("sand_dark", "ochre", "sand"))
    b.box(c, size, col)
    b.box((c.x, c.y, c.z + size[2] / 2 + 0.003), (size[0] + 0.004, size[1] * 0.22, 0.006), "wood_light")
    b.box((c.x, c.y - size[1] / 2 - 0.002, c.z + size[2] * 0.3), (size[0] * 0.22, 0.004, size[2] * 0.4), "wood_light")
    if label:
        b.decal(label, L.frame((c.x, c.y - size[1] / 2, c.z - size[2] * 0.12), FACE_FRONT), "red",
                width=min(size[0] * 0.8, 0.5), lift=0.004)
    return col


def _toy_goods(g, rnd, w, top):
    """Boxed toys, dolls, toy cars, balls and a stack of Brick Games on the table."""
    x = -w / 2 + 0.2
    while x < w / 2 - 0.2:
        kind = rnd.random()
        if kind < 0.35:                                                              # boxed toy with a window
            h = rnd.uniform(0.25, 0.42)
            c = rnd.choice(("blue", "red", "yellow", "green", "purple"))
            g.box((x, 0.05, top + h / 2), (0.22, 0.14, h), c)
            g.box((x, -0.022, top + h * 0.55), (0.15, 0.01, h * 0.5), "blue_pale")
            x += 0.28
        elif kind < 0.55:                                                            # doll
            g.cyl((x, -0.1, top), (x, -0.1, top + 0.16), 0.05, 0.035, segs=6, color=rnd.choice(("pink", "sky", "lavender")))
            g.sphere((x, -0.1, top + 0.2), 0.045, 6, 4, "rubber")
            g.sphere((x, -0.08, top + 0.23), 0.035, 6, 4, rnd.choice(("ochre", "wood")))
            x += 0.16
        elif kind < 0.75:                                                            # toy car
            c = rnd.choice(("red", "yellow", "blue", "white"))
            g.box((x, -0.12, top + 0.05), (0.2, 0.1, 0.06), c)
            g.box((x - 0.01, -0.12, top + 0.1), (0.1, 0.09, 0.05), "window")
            for dx in (-0.06, 0.06):
                g.cyl((x + dx, -0.18, top + 0.03), (x + dx, -0.06, top + 0.03), 0.03, 0.03, segs=6, color="black")
            x += 0.26
        elif kind < 0.88:                                                            # ball
            g.sphere((x, -0.08, top + 0.1), 0.1, 8, 5, rnd.choice(("red", "yellow", "cyan")))
            x += 0.24
        else:                                                                         # Brick Games
            for k in range(3):
                g.box((x, 0.0, top + 0.02 + 0.035 * k), (0.12, 0.2, 0.03), "grey")
            g.box((x, -0.03, top + 0.11), (0.08, 0.07, 0.004), "lime")
            x += 0.2


def _cassette_goods(g, rnd, w, top):
    """Rows of cassettes, VHS tapes and a boombox."""
    g.box((-w / 2 + 0.35, 0.1, top + 0.17), (0.5, 0.18, 0.34), "grey")                  # boombox
    for dx in (-0.12, 0.12):
        g.cyl((-w / 2 + 0.35 + dx, 0.0, top + 0.15), (-w / 2 + 0.35 + dx, -0.015, top + 0.15), 0.09, 0.09,
              segs=10, color="black")
    g.box((-w / 2 + 0.35, 0.0, top + 0.28), (0.18, 0.02, 0.05), "window")
    g.beam((-w / 2 + 0.17, 0.1, top + 0.34), (-w / 2 + 0.53, 0.1, top + 0.34), 0.03, 0.02, "grey_dark")
    for row in range(3):
        y = -0.25 + row * 0.16
        for k in range(8):
            x = -w / 2 + 0.75 + k * 0.14
            if x > w / 2 - 0.1:
                break
            col = rnd.choice(("black", "black", "white", "red", "blue"))
            g.box((x, y, top + 0.012), (0.11, 0.07, 0.024), col)
            g.box((x, y - 0.036, top + 0.013), (0.08, 0.004, 0.014), "cream")
    for k in range(4):                                                               # VHS stack
        g.box((w / 2 - 0.3, 0.2, top + 0.015 + 0.03 * k), (0.2, 0.11, 0.028), "black")
        g.box((w / 2 - 0.3, 0.144, top + 0.015 + 0.03 * k), (0.14, 0.004, 0.02), rnd.choice(("white", "red", "yellow")))


def _clothes_goods(g, rnd, w, top):
    """Folded jeans, sneakers in pairs, caps."""
    for k in range(3):                                                                # jeans stacks
        x = -w / 2 + 0.3 + k * 0.32
        for j in range(rnd.randint(3, 6)):
            g.box((x, 0.05, top + 0.02 + 0.04 * j), (0.28, 0.32, 0.036), rnd.choice(("blue", "navy", "blue_light")))
    for k in range(3):                                                                # sneakers
        x = 0.2 + k * 0.3
        for dx in (-0.06, 0.06):
            g.box((x + dx, -0.15, top + 0.04), (0.09, 0.24, 0.08), "white")
            g.box((x + dx, -0.15, top + 0.012), (0.1, 0.25, 0.024), rnd.choice(("blue", "red", "black")))
    g.sphere((w / 2 - 0.2, 0.1, top + 0.02), 0.1, 8, 4, "red", scale=(1, 1, 0.6))       # cap
    g.box((w / 2 - 0.2, -0.02, top + 0.02), (0.12, 0.1, 0.01), "red")


def _kitchen_goods(g, rnd, w, top):
    """Enamel pots with blue rims, lids, a kettle, ladles, a colander: the shield soldier's armoury."""
    x = -w / 2 + 0.25
    for k in range(4):
        r = rnd.uniform(0.1, 0.15)
        h = rnd.uniform(0.14, 0.24)
        body = rnd.choice(("white", "red", "cream", "green_light"))
        g.cyl((x, 0.05, top), (x, 0.05, top + h), r, r, segs=10, color=body)
        g.cyl((x, 0.05, top + h), (x, 0.05, top + h + 0.015), r + 0.008, r + 0.008, segs=10, color="blue")
        g.lathe(Lathe([(0.0, 0.0), (r, 0.0), (r * 0.6, 0.025), (0.0, 0.03)], 10,
                      Matrix.Translation((x, 0.05, top + h + 0.015))), body)
        g.sphere((x, 0.05, top + h + 0.05), 0.02, 5, 3, "black")
        x += 2 * r + 0.1
    g.lathe(Lathe([(0.0, 0.0), (0.13, 0.0), (0.12, 0.12), (0.06, 0.2), (0.0, 0.21)], 10,
                  Matrix.Translation((w / 2 - 0.3, -0.12, top))), "tin")                   # kettle
    g.cyl((w / 2 - 0.42, -0.12, top + 0.08), (w / 2 - 0.52, -0.12, top + 0.16), 0.02, 0.012, segs=5, color="tin")
    g.lathe(Lathe([(0.0, 0.0), (0.06, 0.005), (0.11, 0.06), (0.12, 0.1), (0.0, 0.1)], 10,
                  Matrix.Translation((w / 2 - 0.62, 0.12, top))), "tin")                   # colander
    for k in range(3):                                                                # lids lying flat
        g.cyl((0.3 + k * 0.08, -0.2, top + 0.01 + 0.012 * k), (0.3 + k * 0.08, -0.2, top + 0.022 + 0.012 * k),
              0.14, 0.14, segs=10, color=("white", "red", "blue")[k])


GOODS = {"toys": _toy_goods, "cassettes": _cassette_goods, "clothes": _clothes_goods, "kitchen": _kitchen_goods}
CLOTH = {"toys": "yellow", "cassettes": "navy", "clothes": "red", "kitchen": "green"}
SIGN = {"toys": "sign_igrushki", "cassettes": "sign_kassety", "clothes": "sign_dzhinsy", "kitchen": None}


# ================================================================ stalls
def stall(col, loc, name="Bazaar_Stall_Toys", kind="toys", seed=101, awning=("red", "white")):
    """Folding market table (2.1 x 0.8 m) with goods, a tablecloth, a rack of goods behind it up to 1.95 m and a
    striped awning on two poles. Cover: balls bounce off it; the transformer's ram smashes it, so it is four parts
    under an empty root: Table, Rack, Awning, Goods (named <Kind>_...)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    tag = name.replace("Bazaar_", "").replace("_", "")
    root = L.empty_root(name, col, loc, size=0.5)
    W, D, TH = 2.1, 0.8, 0.82

    t = Builder()
    t.box((0, 0, TH), (W, D, 0.04), "wood_light")
    for sx in (-1, 1):
        for sy in (-1, 1):
            t.box((sx * (W / 2 - 0.08), sy * (D / 2 - 0.08), TH / 2), (0.04, 0.04, TH), "grey")
        t.beam((sx * (W / 2 - 0.08), -D / 2 + 0.08, 0.15), (sx * (W / 2 - 0.08), D / 2 - 0.08, 0.15), 0.03, 0.03, "grey")
    cloth = CLOTH[kind]
    t.box((0, -D / 2 - 0.006, TH - 0.28), (W + 0.02, 0.012, 0.56), cloth)                    # tablecloth
    t.box((0, -D / 2 - 0.013, TH - 0.5), (W + 0.02, 0.004, 0.06), "white")
    for sx in (-1, 1):
        t.box((sx * (W / 2 + 0.006), 0, TH - 0.28), (0.012, D, 0.56), cloth)
    sign = rnd.choice(("tag_nedorogo", "sign_vse_po"))
    sm = Matrix.Translation((0.55, -D / 2 - 0.03, TH - 0.2)) @ Matrix.Rotation(0.04, 4, "Z") @ \
        Matrix.Rotation(-0.05, 4, "X")                                                       # cardboard sign, askew
    t.box_m(sm, (0.62, 0.02, 0.26), "cream")
    t.decal(sign, sm @ Matrix.Translation((0, -0.011, 0)) @ Matrix.Rotation(math.pi / 2, 4, "X"), "black",
            width=0.52, lift=0.002)

    r = Builder()
    ry = D / 2 + 0.18
    for sx in (-1, 1):
        r.box((sx * (W / 2 - 0.05), ry, 0.98), (0.05, 0.05, 1.96), "grey_dark")
        r.box((sx * (W / 2 - 0.05), ry, 0.02), (0.08, 0.4, 0.04), "grey_dark")
    r.box((0, ry, 1.93), (W, 0.05, 0.05), "grey_dark")
    if kind == "clothes":                                                              # hanging jeans
        for k in range(6):
            x = -W / 2 + 0.28 + k * 0.31
            c = rnd.choice(("blue", "navy", "blue_light"))
            r.box((x, ry, 1.45), (0.26, 0.03, 0.9), c)
            r.box((x, ry - 0.016, 1.45 - 0.1), (0.005, 0.004, 0.6), "navy")
    elif kind == "toys":                                                               # a hanging carpet with toys on hooks
        r.box((0, ry + 0.03, 1.2), (W - 0.2, 0.02, 1.4), "maroon")
        r.box((0, ry + 0.015, 1.2), (W - 0.5, 0.02, 1.0), "red_dark")
        for k in range(5):
            x = -W / 2 + 0.4 + k * 0.33
            r.sphere((x, ry, 1.5), 0.08, 6, 4, rnd.choice(("yellow", "red", "cyan", "lime")))
            r.box((x, ry - 0.02, 1.05), (0.18, 0.06, 0.26), rnd.choice(("blue", "green", "purple")))
    else:                                                                             # a shelf of goods
        for z in (1.0, 1.45):
            r.box((0, ry, z), (W - 0.1, 0.3, 0.03), "wood")
            x = -W / 2 + 0.2
            while x < W / 2 - 0.2:
                h = rnd.uniform(0.15, 0.35)
                if kind == "kitchen":
                    rr = rnd.uniform(0.08, 0.13)
                    r.cyl((x, ry, z + 0.015), (x, ry, z + 0.015 + h * 0.7), rr, rr, segs=8,
                          color=rnd.choice(("white", "red", "cream", "blue")))
                    x += 2 * rr + 0.06
                else:
                    r.box((x, ry, z + 0.015 + h / 2), (0.12, 0.2, h), rnd.choice(("black", "grey_dark", "red", "blue")))
                    x += 0.16
        r.box((0, ry + 0.16, 1.2), (W - 0.1, 0.02, 1.2), "grey")
    parts = [(t, "Table", (0, 0, TH / 2)), (r, "Rack", (0, ry, 1.0))]

    a = Builder()
    zb, zf = 2.3, 2.02
    for sx in (-1, 1):
        a.cyl((sx * (W / 2 + 0.05), ry + 0.08, 0.0), (sx * (W / 2 + 0.05), ry + 0.08, zb), 0.025, 0.025, segs=6, color="grey")
        a.cyl((sx * (W / 2 + 0.05), -D / 2 - 0.35, 0.0), (sx * (W / 2 + 0.05), -D / 2 - 0.35, zf), 0.022, 0.022,
              segs=6, color="grey")
    n = 7
    for k in range(n):                                                                # striped canopy
        x0 = -W / 2 - 0.1 + (W + 0.2) * k / n
        x1 = -W / 2 - 0.1 + (W + 0.2) * (k + 1) / n
        a.quad([(x0, ry + 0.1, zb), (x1, ry + 0.1, zb), (x1, -D / 2 - 0.4, zf), (x0, -D / 2 - 0.4, zf)],
               awning[k % 2], want=(0, 0, 1))
        a.quad([(x0, ry + 0.1, zb - 0.01), (x1, ry + 0.1, zb - 0.01), (x1, -D / 2 - 0.4, zf - 0.01),
                (x0, -D / 2 - 0.4, zf - 0.01)], awning[k % 2], want=(0, 0, -1))
        a.quad([(x0, -D / 2 - 0.4, zf), (x1, -D / 2 - 0.4, zf), (x1, -D / 2 - 0.42, zf - 0.22),
                (x0, -D / 2 - 0.42, zf - 0.22)], awning[(k + 1) % 2], want=(0, -1, 0))         # valance
    if SIGN[kind]:
        a.box((0, -D / 2 - 0.43, zf - 0.11), (1.1, 0.02, 0.2), "white")
        a.decal(SIGN[kind], L.frame((0, -D / 2 - 0.442, zf - 0.11), FACE_FRONT), "red", height=0.14, lift=0.002)
    parts.append((a, "Awning", (0, 0, 2.15)))

    g = Builder()
    GOODS[kind](g, rnd, W, TH + 0.02)
    parts.append((g, "Goods", (0, 0, TH + 0.1)))

    for bld, suffix, pv in parts:
        _part(bld, tag + "_" + suffix, col, root, pv)
    return root


# ================================================================ kiosks and containers
def kiosk_90s(col, loc, name="Bazaar_Kiosk", sign="sign_kassety", body="blue", seed=111, door=False):
    """Commercial kiosk («комок») 2.4 x 1.8 x 2.5 m: metal booth, big lit window behind a grille full of bottles
    and boxes, a serving hatch, a sign board on the roof. door=True: a side door (Kiosk_Door part, hinge at its
    back edge) — the transformer drives out of the toy kiosk."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    tag = name.replace("Bazaar_", "").replace("_", "")
    root = L.empty_root(name, col, loc, size=0.5)
    b = Builder()
    W, D, H = 2.4, 1.8, 2.3
    b.box((0, 0, 0.1), (W + 0.1, D + 0.1, 0.2), "concrete")
    b.box((0, 0.1, 0.2 + H / 2), (W, D - 0.2, H), body)
    b.box((0, 0, 0.2 + H + 0.06), (W + 0.3, D + 0.3, 0.12), lambda f: "roof" if f.normal.z > 0.9 else "tin_dark")
    wz0, wz1 = 0.95, 2.25
    b.box((0, -D / 2 + 0.08, 0.2 + 0.4), (W, 0.16, 0.8), body)                                # under the window
    b.box((0, -D / 2 + 0.12, (wz0 + wz1) / 2), (W - 0.1, 0.05, wz1 - wz0), "window_lit")    # lit goods wall
    for k in range(3):                                                                # shelves with goods
        z = wz0 + 0.12 + k * 0.4
        b.box((0, -D / 2 + 0.06, z), (W - 0.2, 0.05, 0.02), "grey_dark")
        x = -W / 2 + 0.2
        while x < W / 2 - 0.2:
            h = rnd.uniform(0.12, 0.3)
            if rnd.random() < 0.5:
                b.cyl((x, -D / 2 + 0.05, z + 0.01), (x, -D / 2 + 0.05, z + 0.01 + h), 0.035, 0.03, segs=6,
                      color=rnd.choice(("green_dark", "bottle", "red", "orange", "yellow")))
                x += 0.1
            else:
                b.box((x, -D / 2 + 0.05, z + 0.01 + h / 2), (0.14, 0.04, h), rnd.choice(("red", "blue", "yellow", "white", "purple")))
                x += 0.18
    for k in range(11):                                                               # grille
        x = -W / 2 + 0.1 + k * (W - 0.2) / 10
        b.box((x, -D / 2 + 0.01, (wz0 + wz1) / 2), (0.025, 0.025, wz1 - wz0), "grey_dark")
    for z in (wz0, (wz0 + wz1) / 2, wz1):
        b.box((0, -D / 2 + 0.01, z), (W, 0.03, 0.03), "grey_dark")
    b.box((0.6, -D / 2 - 0.02, wz0 + 0.12), (0.5, 0.1, 0.36), "tin_dark")                     # serving hatch
    b.box((0.6, -D / 2 - 0.08, wz0 - 0.02), (0.6, 0.2, 0.03), "tin")
    b.box((0, -D / 2 + 0.2, 0.2 + H + 0.42), (W - 0.2, 0.1, 0.5), "red")                      # sign board
    b.box((0, -D / 2 + 0.2, 0.2 + H + 0.2), (0.08, 0.08, 0.2), "grey_dark")
    if sign:
        b.decal(sign, L.frame((0, -D / 2 + 0.148, 0.2 + H + 0.42), FACE_FRONT), "white", height=0.3, lift=0.004)
    for _ in range(5):                                                                # rust
        x = rnd.uniform(-1.1, 1.1)
        b.box((x, D / 2 - 0.09, 0.2 + rnd.uniform(0.4, 1.8)), (rnd.uniform(0.05, 0.1), 0.01, rnd.uniform(0.2, 0.5)), "rust")
    b.cyl((W / 2 - 0.2, -D / 2 - 0.05, 0.2 + H - 0.1), (W / 2 - 0.2, -D / 2 - 0.3, 0.2 + H - 0.1), 0.02, 0.02,
          segs=5, color="grey_dark")
    b.cyl((W / 2 - 0.2, -D / 2 - 0.3, 0.2 + H - 0.08), (W / 2 - 0.2, -D / 2 - 0.3, 0.2 + H - 0.2), 0.05, 0.1,
          segs=8, color=lambda f: "lamp" if f.normal.z < -0.9 else "grey_dark")
    body_ob = _part(b, tag + "_Body", col, root, (0, 0, 1.2))
    if door:
        d = Builder()
        x = W / 2 + 0.02
        d.box((x, 0.1, 0.2 + 1.0), (0.05, 0.9, 1.95), "tin_dark")
        d.box((x + 0.035, -0.25, 0.2 + 1.0), (0.03, 0.05, 0.15), "tin")
        d.box((x + 0.03, 0.1, 0.2 + 1.6), (0.01, 0.6, 0.3), "window_lit")
        _part(d, tag + "_Door", col, root, (x, 0.55, 1.2))
    return root


def container(col, loc, name="Bazaar_Container", color="rust", seed=121, open_doors=False):
    """Shipping container 6.0 x 2.4 x 2.6 m, ribbed walls, doors with locking bars at +X. open_doors=True: the doors
    stand open and boxes and rolled carpets show inside. Background and boundary piece."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    Lx, Dy, H = 6.0, 2.4, 2.6
    b.box((0, 0, H / 2), (Lx, Dy, H), color)
    for sy in (-1, 1):                                                                # ribs
        for k in range(20):
            x = -Lx / 2 + 0.2 + k * (Lx - 0.4) / 19
            b.box((x, sy * (Dy / 2 + 0.015), H / 2), (0.08, 0.03, H - 0.25), color)
    for z in (0.06, H - 0.06):                                                        # frame rails
        for sy in (-1, 1):
            b.box((0, sy * (Dy / 2 + 0.02), z), (Lx + 0.02, 0.06, 0.12), "grey_dark")
    for sx in (-1, 1):
        for sy in (-1, 1):
            for z in (0.08, H - 0.08):
                b.box((sx * (Lx / 2 - 0.08), sy * (Dy / 2 - 0.08), z), (0.18, 0.18, 0.16), "grey_dark")
    if open_doors:
        b.box((Lx / 2 + 0.005, 0, H / 2), (0.02, Dy - 0.2, H - 0.2), "grey_dark")              # the dark inside
        for sy in (-1, 1):                                                            # doors swung open 100°
            hinge = Vector((Lx / 2 + 0.03, sy * (Dy / 2 - 0.05), 0.0))
            d = Vector((math.sin(math.radians(100)), -sy * math.cos(math.radians(100)), 0.0))   # out and a bit sideways
            c = hinge + d * 0.55 + Vector((0, 0, H / 2))
            b.box(c, (1.1, 0.05, H - 0.2), color, rot=(0, 0, math.atan2(d.y, d.x)))
        for k in range(3):                                                            # goods being unloaded
            _cardboard(b, (Lx / 2 + 0.45 + 0.1 * k, -0.55 + 0.55 * k, 0.28 + (0.52 if k == 1 else 0.0)),
                       (0.55, 0.5, 0.52), rnd)
        _plaid_bag(b, (Lx / 2 + 0.5, 0.75, 0.3), (0.5, 0.45, 0.6), rnd)
        b.cyl((Lx / 2 + 1.2, -0.9, 0.2), (Lx / 2 + 1.25, 0.6, 0.2), 0.2, 0.2, segs=8, color="maroon")   # carpet roll
    else:
        b.box((Lx / 2 + 0.01, 0, H / 2), (0.03, Dy - 0.1, H - 0.2), color)
        for k in range(4):
            y = -Dy / 2 + 0.35 + k * (Dy - 0.7) / 3
            b.cyl((Lx / 2 + 0.05, y, 0.2), (Lx / 2 + 0.05, y, H - 0.2), 0.025, 0.025, segs=5, color="tin")
    for _ in range(8):                                                                # rust streaks
        x = rnd.uniform(-2.8, 2.8)
        sy = rnd.choice((-1, 1))
        b.box((x, sy * (Dy / 2 + 0.035), H - rnd.uniform(0.4, 0.9)), (rnd.uniform(0.06, 0.14), 0.01, rnd.uniform(0.3, 0.8)),
              "rust_light" if color == "rust" else "rust")
    return L.mesh_root(b, name, col, loc)


# ================================================================ carts and box piles
def cart(col, loc, name="Bazaar_Cart", seed=131):
    """Four-wheel hand cart ~1.25 m loaded with a checked bag, boxes and a rolled carpet. A ball pushes it and it
    rolls into enemies (physics in Unity). Front = -Y, the push handle at +Y."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    b.box((0, 0, 0.3), (0.72, 1.2, 0.05), "wood")
    for sx in (-1, 1):
        b.beam((sx * 0.34, -0.6, 0.27), (sx * 0.34, 0.6, 0.27), 0.04, 0.04, "grey_dark")
        for sy in (-1, 1):
            c = Vector((sx * 0.3, sy * 0.45, 0.12))
            b.cyl(c + Vector((sx * -0.04, 0, 0)), c + Vector((sx * 0.04, 0, 0)), 0.12, 0.12, segs=8, color="black")
            b.cyl(c + Vector((sx * 0.04, 0, 0)), c + Vector((sx * 0.05, 0, 0)), 0.05, 0.05, segs=6, color="tin")
            b.beam(c, c + Vector((0, 0, 0.16)), 0.03, 0.03, "grey_dark")
    for sx in (-1, 1):                                                                # push handle
        b.beam((sx * 0.3, 0.6, 0.3), (sx * 0.3, 0.72, 1.0), 0.03, 0.03, "grey")
    b.beam((-0.32, 0.72, 1.0), (0.32, 0.72, 1.0), 0.035, 0.035, "black")
    _plaid_bag(b, (-0.12, 0.2, 0.62), (0.44, 0.6, 0.6), rnd)
    _cardboard(b, (0.17, -0.3, 0.55), (0.34, 0.5, 0.46), rnd)
    _cardboard(b, (-0.14, -0.35, 0.5), (0.36, 0.4, 0.36), rnd)
    b.cyl((-0.33, -0.2, 1.02), (0.33, -0.1, 1.0), 0.1, 0.1, segs=8, color="purple")          # rolled carpet
    b.cyl((-0.34, -0.2, 1.02), (-0.33, -0.2, 1.02), 0.1, 0.1, segs=8, color="yellow")
    return L.mesh_root(b, name, col, loc)


def box_pile(col, loc, name="Bazaar_BoxPile", seed=141):
    """A pile of cardboard boxes ~2.3 m wide and ~1.9 m tall: 3 + 2 + 1 boxes and a leaning one. Every box is its
    own part (pivot at its centre): a strong ball topples the pile, the boxes fall and stun whoever they hit."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    tag = name.replace("Bazaar_", "").replace("_", "")
    root = L.empty_root(name, col, loc, size=0.5)
    boxes = []
    for k in range(3):
        s = rnd.uniform(0.66, 0.78)
        boxes.append(((-0.76 + k * 0.76 + rnd.uniform(-0.04, 0.04), rnd.uniform(-0.06, 0.06), s / 2), (s, s * 0.9, s)))
    for k in range(2):
        s = rnd.uniform(0.6, 0.7)
        boxes.append(((-0.38 + k * 0.76 + rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05), 0.72 + s / 2), (s, s * 0.9, s)))
    s = 0.56
    boxes.append(((rnd.uniform(-0.1, 0.1), 0.0, 1.38 + s / 2), (s, s * 0.9, s)))
    labels = ["sign_igrushki", None, "sign_kassety", None, None, "sign_igrushki"]
    for k, (c, size) in enumerate(boxes):
        bb = Builder()
        _cardboard(bb, c, size, rnd, labels[k] if rnd.random() < 0.7 else None)
        _part(bb, tag + "_Box" + str(k + 1), col, root, c)
    return root


# ================================================================ lights, gate, ground
def light_pole(col, loc, name="Bazaar_LightPole", span=6.0):
    """Pipe pole 3.6 m with a string of bulbs sagging towards +X for `span` metres (poles stand `span` apart, so
    the strings join up). The bulbs are lamp cells: they glow in the evening."""
    L.remove_tree(name)
    b = Builder()
    b.box((0, 0, 0.15), (0.4, 0.4, 0.3), "concrete")
    b.cyl((0, 0, 0.3), (0, 0, 3.6), 0.05, 0.045, segs=6, color="grey_dark")
    n = 10
    pts = []
    for k in range(n + 1):
        t = k / n
        pts.append(Vector((span * t, 0.0, 3.5 - 0.55 * math.sin(math.pi * t))))
    b.tube_path(pts, 0.01, "black", segs=4)
    for k in range(1, n):
        p = pts[k]
        b.cyl(p, p + Vector((0, 0, -0.1)), 0.012, 0.012, segs=4, color="black")
        b.sphere(p + Vector((0, 0, -0.15)), 0.055, 6, 4, "lamp")
    return L.mesh_root(b, name, col, loc)


def market_gate(col, loc, name="Bazaar_Gate"):
    """Entrance arch 6 m wide: two posts with a red board «РЫНОК» between them and little flags."""
    L.remove_tree(name)
    b = Builder()
    for sx in (-1, 1):
        b.box((sx * 3.0, 0, 2.1), (0.25, 0.25, 4.2), "grey")
        b.box((sx * 3.0, 0, 0.1), (0.5, 0.5, 0.2), "concrete")
    b.box((0, 0, 3.75), (5.6, 0.12, 0.8), "red")
    b.box((0, 0, 4.2), (6.2, 0.16, 0.1), "gold")
    b.box((0, 0, 3.3), (6.2, 0.16, 0.1), "gold")
    b.decal("sign_rynok", L.frame((0, -0.061, 3.75), FACE_FRONT), "white", height=0.55, lift=0.003)
    for k in range(9):                                                                # flags on a cord
        x = -2.8 + k * 0.7
        c = ("yellow", "blue", "red")[k % 3]
        b.prism([(-0.14, 0.0), (0.14, 0.0), (0.0, -0.28)], 0.01, c,
                matrix=L.frame((x, -0.1, 4.55 - 0.18 * math.sin(math.pi * k / 8)), FACE_FRONT))
    b.tube_path([(-3.0, -0.1, 4.6)] + [(-2.8 + k * 0.7, -0.1, 4.55 - 0.18 * math.sin(math.pi * k / 8)) for k in range(9)] +
                [(3.0, -0.1, 4.6)], 0.008, "black", segs=4)
    return L.mesh_root(b, name, col, loc)


def bazaar_ground(col, loc, name="Env_BazaarGround", seed=151):
    """Market square in arena coordinates: patched asphalt with faded parking lines, flattened cardboard,
    manholes, puddles, trodden dirt and weeds around. Visual only."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    base = b.grid(110, 90, 44, 36, "olive", matrix=L.frame((0, 8, -0.02)))
    b.paint([f for f in base if rnd.random() < 0.3], "wood")
    b.paint([f for f in base if rnd.random() < 0.12], "green_dark")
    b.paint([f for f in base if rnd.random() < 0.08], "sand_dark")
    ground = b.grid(BAZ_W + 6, BAZ_D + 6, int((BAZ_W + 6) / 2), int((BAZ_D + 6) / 2), "asphalt", matrix=L.frame((0, 0, 0)))
    b.paint([f for f in ground if rnd.random() < 0.06], "grey_dark")
    b.paint([f for f in ground if rnd.random() < 0.015], "grey")
    for _ in range(7):                                                                # asphalt patches
        cx, cy, w, d = rnd.uniform(-18, 18), rnd.uniform(-12, 12), rnd.uniform(1.5, 4), rnd.uniform(1, 3)
        b.grid(w, d, 1, 1, "grey_dark", matrix=L.frame((cx, cy, 0.006), (0, 0, rnd.uniform(-0.3, 0.3))))
    for row_y in (-8.0, 8.0):                                                         # faded parking lines
        for k in range(-8, 9):
            if rnd.random() < 0.25:
                continue
            b.grid(0.12, 4.4, 1, 2, "grey_light", matrix=L.frame((k * 2.6, row_y, 0.008)))
    for _ in range(10):                                                               # flattened cardboard
        cx, cy = rnd.uniform(-19, 19), rnd.uniform(-13, 13)
        b.grid(rnd.uniform(0.6, 1.1), rnd.uniform(0.5, 0.9), 1, 1, rnd.choice(("sand_dark", "ochre")),
               matrix=L.frame((cx, cy, 0.01), (0, 0, rnd.uniform(0, math.pi))))
    for cx, cy in ((-6.0, 3.5), (12.0, -5.0)):                                        # manholes
        b.prism([(cx + 0.35 * math.cos(2 * math.pi * k / 10), cy + 0.35 * math.sin(2 * math.pi * k / 10)) for k in range(10)],
                0.004, "grey_dark", matrix=Matrix.Translation((0, 0, 0.008)))
    for _ in range(6):                                                                # puddles
        cx, cy = rnd.uniform(-18, 18), rnd.uniform(-12, 12)
        pts = [(cx + r * math.cos(a), cy + r * 0.7 * math.sin(a)) for a, r in
               [(2 * math.pi * k / 10, rnd.uniform(0.6, 1.3)) for k in range(10)]]
        b.prism(ccw(pts), 0.002, "steel", matrix=Matrix.Translation((0, 0, 0.012)))
    for _ in range(120):                                                              # sunflower-seed husks, litter
        cx, cy = rnd.uniform(-21, 21), rnd.uniform(-14, 14)
        b.grid(0.06, 0.04, 1, 1, rnd.choice(("black", "cream", "red")), matrix=L.frame((cx, cy, 0.014), (0, 0, rnd.uniform(0, 3))))
    for v in b.bm.verts:
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return L.mesh_root(b, name, col, loc)


def build_all(col_parent):
    col = L.collection("Bazaar", col_parent)
    X, Y = -280.0, 0.0
    out = [bazaar_ground(col, (X, Y, 0))]
    out.append(stall(col, (X - 36, Y - 30, 0), "Bazaar_Stall_Toys", "toys", 101, ("red", "white")))
    out.append(stall(col, (X - 32, Y - 30, 0), "Bazaar_Stall_Cassettes", "cassettes", 102, ("blue", "white")))
    out.append(stall(col, (X - 28, Y - 30, 0), "Bazaar_Stall_Clothes", "clothes", 103, ("green", "yellow")))
    out.append(stall(col, (X - 24, Y - 30, 0), "Bazaar_Stall_Kitchen", "kitchen", 104, ("orange", "white")))
    out.append(kiosk_90s(col, (X - 36, Y - 36, 0), "Bazaar_Kiosk_Toys", "sign_igrushki", "green_dark", 111, door=True))
    out.append(kiosk_90s(col, (X - 32, Y - 36, 0), "Bazaar_Kiosk_Cassettes", "sign_kassety", "blue", 112))
    out.append(kiosk_90s(col, (X - 28, Y - 36, 0), "Bazaar_Kiosk_Exchange", "sign_dzhinsy", "maroon", 113))
    out.append(container(col, (X - 20, Y - 36, 0), "Bazaar_Container", "rust", 121))
    out.append(container(col, (X - 12, Y - 36, 0), "Bazaar_Container_Blue", "blue", 122))
    out.append(container(col, (X - 4, Y - 36, 0), "Bazaar_Container_Open", "green_dark", 123, open_doors=True))
    out.append(cart(col, (X - 36, Y - 42, 0)))
    out.append(box_pile(col, (X - 32, Y - 42, 0)))
    out.append(box_pile(col, (X - 28, Y - 42, 0), "Bazaar_BoxPile_B", 142))
    out.append(light_pole(col, (X - 24, Y - 42, 0)))
    out.append(market_gate(col, (X - 12, Y - 44, 0)))
    return out

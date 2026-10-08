"""Arena «Детсад» (dusk, semi-dark; the fork alternative to the construction site): the yard of a Soviet
kindergarten «Солнышко». The spinning carousel (its Deck is a separate part: it turns and carries the player,
enemies and balls), the rocket slide (tall cover), verandas with a lamp under the roof (cover from candles, a light
zone), a playhouse, tyre swans and tyre flower beds, park lamps, the rainbow picket fence, the two-storey building
with the sun mosaic and lit windows, the ground with chalk drawings. Lamps and lit windows use the lamp /
window_lit cells, so they glow at dusk. Props: origin on the ground, front to -Y. Env_KindergartenGround is in
arena coordinates (x east, y north)."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

KG_W, KG_D = 44.0, 30.0
FACE_FRONT = (math.pi / 2, 0.0, 0.0)          # decal facing -Y
RAINBOW = ("red", "orange", "yellow", "green", "sky", "blue", "purple")


def ccw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return pts if area > 0 else list(reversed(pts))


def _sphere_profile(r, z, rings=8, squash=1.0):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((0.0 if i in (0, rings) else r * math.cos(a), z + r * squash * math.sin(a)))
    return prof


def _torus(b, center, R, r, color, segs=10, tube=5, flat=True):
    """Tyre: a ring of tube segments (lying flat when flat=True, else standing in the XZ plane)."""
    c = Vector(center)
    pts = []
    for k in range(segs + 1):
        a = 2 * math.pi * k / segs
        pts.append(c + (Vector((R * math.cos(a), R * math.sin(a), 0.0)) if flat else
                        Vector((R * math.cos(a), 0.0, R * math.sin(a)))))
    faces = []
    for p0, p1 in zip(pts, pts[1:]):
        faces += b.cyl(p0, p1, r, r, segs=tube, color=color)
    return faces


# ================================================================ building and fence
def kg_building(col, loc, name="Kg_Building", seed=201):
    """Two-storey kindergarten 26 x 12 x 7.4 m: whitewashed walls with light-blue bands, rows of big windows (some
    lit at dusk), the entrance porch with a canopy, the sign «ДЕТСКИЙ САД «СОЛНЫШКО»», a sun mosaic, flat roof.
    Background piece on the north side of the arena."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    W, D, H = 26.0, 12.0, 7.4
    b.box((0, 0, H / 2), (W, D, H), "panel_beige")
    b.box((0, 0, 0.3), (W + 0.1, D + 0.1, 0.6), "concrete")                                 # plinth
    for z in (3.35, H - 0.2):
        b.box((0, 0, z), (W + 0.06, D + 0.06, 0.3), "sky")                                  # bands
    b.box((0, 0, H + 0.25), (W + 0.3, D + 0.3, 0.5), lambda f: "roof" if f.normal.z > 0.9 else "concrete")
    for x in (-8.0, 3.0, 9.0):
        b.box((x, 2.0, H + 0.8), (1.0, 1.0, 0.6), "concrete")                                 # vents

    def window(x, z, y, lit, facing=-1):
        b.box((x, y, z), (1.9, 0.08, 1.6), "white")
        b.box((x, y + facing * 0.03, z), (1.7, 0.04, 1.4), "window_lit" if lit else "window")
        b.box((x, y + facing * 0.055, z), (0.08, 0.02, 1.4), "white")
        b.box((x + 0.45, y + facing * 0.055, z + 0.25), (0.8, 0.02, 0.06), "white")
        b.box((x, y + facing * 0.07, z - 0.85), (2.0, 0.14, 0.06), "concrete_light")         # sill
    xs = [-11.5 + k * 2.55 for k in range(10)]
    for x in xs:
        for z in (1.9, 5.3):
            if abs(x) < 1.6 and z < 3:
                continue                                                               # the entrance
            if -11.8 < x < -6.5 and z > 3:
                continue                                                               # the mosaic
            window(x, z, -D / 2 - 0.02, rnd.random() < 0.4)
    for sx in (-1, 1):                                                                # side windows
        for y in (-3.0, 3.0):
            for z in (1.9, 5.3):
                b.box((sx * (W / 2 + 0.02), y, z), (0.08, 1.9, 1.6), "white")
                b.box((sx * (W / 2 + 0.05), y, z), (0.04, 1.7, 1.4), "window_lit" if rnd.random() < 0.3 else "window")
    # entrance: porch, canopy on two pillars, double door, the sign
    b.box((0, -D / 2 - 1.2, 0.15), (4.0, 2.4, 0.3), "concrete")
    b.box((0, -D / 2 - 2.55, 0.08), (3.0, 0.4, 0.16), "concrete_light")
    b.box((0, -D / 2 - 1.3, 3.0), (4.6, 2.8, 0.2), "white")
    for sx in (-1, 1):
        b.cyl((sx * 2.0, -D / 2 - 2.4, 0.3), (sx * 2.0, -D / 2 - 2.4, 2.9), 0.1, 0.1, segs=8, color="white")
    b.box((0, -D / 2 - 0.03, 1.35), (1.8, 0.06, 2.1), "wood")
    b.box((0, -D / 2 - 0.07, 1.35), (0.04, 0.02, 2.0), "brown")
    for sx in (-1, 1):
        b.box((sx * 0.45, -D / 2 - 0.07, 1.8), (0.6, 0.02, 0.7), "window_lit")
        b.box((sx * 0.15, -D / 2 - 0.09, 1.2), (0.05, 0.03, 0.2), "tin")
    b.box((0, -D / 2 - 0.05, 3.55), (8.4, 0.1, 0.62), "white")
    b.decal("sign_detsad", L.frame((0, -D / 2 - 0.101, 3.55), FACE_FRONT), "red", width=8.0, lift=0.003)
    # sun mosaic between the first-floor windows and the roof band on the left
    b.box((-9.2, -D / 2 - 0.03, 5.3), (5.2, 0.06, 3.4), "sky")
    b.decal("doodle_sun", L.frame((-9.2, -D / 2 - 0.061, 5.3), FACE_FRONT), "yellow", height=2.9, lift=0.003)
    for k, (x, z, c) in enumerate(((-11.2, 4.1, "red"), (-7.2, 4.1, "pink"), (-11.3, 6.4, "white"), (-7.1, 6.5, "orange"))):
        b.decal("doodle_flower", L.frame((x, -D / 2 - 0.061, z), FACE_FRONT), c, height=0.7, lift=0.004)
    return L.mesh_root(b, name, col, loc)


def kg_fence(col, loc, name="Kg_Fence", seed=202, length=4.0):
    """Painted metal picket fence section `length` m long, 1.3 m high: a post at +X, two rails, flat pickets in
    rainbow colours with rounded tops."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    b.box((length / 2, 0, 0.7), (0.1, 0.1, 1.4), "grey_dark")
    b.sphere((length / 2, 0, 1.42), 0.07, 6, 4, "grey_dark")
    for z in (0.3, 1.05):
        b.box((0, 0.04, z), (length, 0.04, 0.06), "grey_dark")
    n = int(length / 0.2)
    for k in range(n):
        x = -length / 2 + 0.1 + k * (length - 0.2) / (n - 1)
        c = RAINBOW[k % len(RAINBOW)]
        b.box((x, 0.0, 0.62), (0.1, 0.02, 1.1), c)
        b.cyl((x, -0.01, 1.17), (x, 0.01, 1.17), 0.05, 0.05, segs=6, color=c)
    return L.mesh_root(b, name, col, loc)


# ================================================================ cover: verandas, playhouse, rocket slide
def veranda(col, loc, name="Kg_Veranda", wall="green", roof="red_dark", seed=203):
    """Wooden veranda 6.2 x 3.2 m: raised floor, plank walls at the back and sides, a railing in front with a gap in
    the middle, a single-pitch roof, a lamp under the front beam (lamp cell: the light zone at dusk), a bench
    inside and a few toys. Balls bounce off the walls; the roof keeps candles off."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    W, D = 6.2, 3.2
    b.box((0, 0, 0.12), (W, D, 0.24), "wood")
    for k in range(int(W / 0.3)):                                                     # floor boards
        b.box((-W / 2 + 0.15 + k * 0.3, 0, 0.245), (0.02, D - 0.1, 0.01), "wood_dark")
    zf, zb = 2.55, 2.95                                                               # roof height front / back
    for k in range(int(W / 0.25)):                                                    # back wall planks
        x = -W / 2 + 0.125 + k * 0.25
        b.box((x, D / 2 - 0.05, 0.24 + (zb - 0.24) / 2), (0.23, 0.08, zb - 0.24), wall if k % 2 else "white")
    for sx in (-1, 1):                                                                # side walls
        for k in range(int(D / 0.25)):
            y = -D / 2 + 0.125 + k * 0.25
            h = zf + (zb - zf) * (y + D / 2) / D
            b.box((sx * (W / 2 - 0.05), y, 0.24 + (h - 0.24) / 2), (0.08, 0.23, h - 0.24), wall if k % 2 else "white")
    for sx in (-1, 1):                                                                # front posts and railing
        for x in (sx * (W / 2 - 0.1), sx * 0.8):
            b.box((x, -D / 2 + 0.08, (0.24 + zf) / 2), (0.12, 0.12, zf - 0.24), "wood_dark")
        x0, x1 = sx * 0.8, sx * (W / 2 - 0.1)
        b.beam((x0, -D / 2 + 0.08, 0.95), (x1, -D / 2 + 0.08, 0.95), 0.08, 0.06, "wood_dark")
        for k in range(1, 12):
            x = x0 + (x1 - x0) * k / 12
            b.box((x, -D / 2 + 0.08, 0.6), (0.05, 0.05, 0.7), wall)
    b.beam((-W / 2, -D / 2 + 0.08, zf), (W / 2, -D / 2 + 0.08, zf), 0.14, 0.16, "wood_dark")
    rw = Vector((0, D + 0.6, zb - zf)).length                                         # roof sheet
    rm = Matrix.Translation((0, 0.0, (zf + zb) / 2 + 0.08)) @ Matrix.Rotation(math.atan2(zb - zf, D + 0.6), 4, "X")
    b.box_m(rm, (W + 0.5, rw, 0.08), lambda f: roof if f.normal.z > 0.3 else "wood_dark")
    lamp = Vector((0, -D / 2 + 0.3, zf - 0.12))                                       # lamp under the roof
    b.cyl(lamp + Vector((0, 0, 0.12)), lamp, 0.02, 0.02, segs=4, color="black")
    b.cyl(lamp, lamp + Vector((0, 0, -0.12)), 0.08, 0.2, segs=10, color=lambda f: "lamp" if f.normal.z < -0.9 else "grey_dark")
    b.sphere(lamp + Vector((0, 0, -0.1)), 0.07, 6, 4, "lamp")
    b.box((0, D / 2 - 0.35, 0.5), (W - 0.6, 0.35, 0.05), "wood_light")                       # bench
    for x in (-2.5, 0.0, 2.5):
        b.box((x, D / 2 - 0.35, 0.37), (0.06, 0.3, 0.26), "wood_dark")
    b.cyl((-1.8, 0.2, 0.25), (-1.8, 0.2, 0.47), 0.12, 0.15, segs=8, color="red")              # bucket
    b.sphere((1.6, -0.4, 0.4), 0.16, 8, 5, "yellow")
    b.beam((1.0, 0.6, 0.27), (1.35, 0.9, 0.27), 0.05, 0.02, "blue")                        # spade
    b.decal("doodle_flower", L.frame((0, D / 2 - 0.1, 1.9), FACE_FRONT), "yellow", height=0.8, lift=0.003)
    return L.mesh_root(b, name, col, loc)


def playhouse(col, loc, name="Kg_Playhouse", seed=204):
    """Wooden playhouse 2.2 x 2 m with a pitched roof, a round window and a doorway: small cover."""
    L.remove_tree(name)
    b = Builder()
    W, D, H = 2.2, 2.0, 1.7
    b.box((0, 0, H / 2), (W, D, H), "yellow")
    b.box((0, -D / 2 - 0.01, 0.65), (0.7, 0.03, 1.2), "brown")                               # doorway
    b.cyl((0.7, -D / 2 - 0.01, 1.15), (0.7, -D / 2 - 0.03, 1.15), 0.2, 0.2, segs=10, color="window")
    b.cyl((0.7, -D / 2 - 0.02, 1.15), (0.7, -D / 2 - 0.04, 1.15), 0.24, 0.24, segs=10, color="white")
    for sx in (-1, 1):                                                                # roof slopes
        m = Matrix.Translation((sx * W / 4, 0, H + 0.45)) @ Matrix.Rotation(sx * 0.69, 4, "Y")
        b.box_m(m, (W * 0.72, D + 0.4, 0.08), lambda f: "red" if f.normal.z > 0.2 else "wood_dark")
    b.prism([(-W / 2, 0.0), (W / 2, 0.0), (0.0, 0.9)], 0.02, "yellow",
            matrix=Matrix.Translation((0, -D / 2 + 0.01, H)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    b.prism([(-W / 2, 0.0), (W / 2, 0.0), (0.0, 0.9)], 0.02, "yellow",
            matrix=Matrix.Translation((0, D / 2 + 0.01, H)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    b.decal("doodle_sun", L.frame((-0.55, -D / 2 - 0.03, 1.2), FACE_FRONT), "orange", height=0.55, lift=0.003)
    return L.mesh_root(b, name, col, loc)


def rocket_slide(col, loc, name="Kg_RocketSlide", seed=205):
    """Rocket slide ~5.7 m: white body with red bands and portholes on three red fins, a red nose cone, a hatch at
    the bottom and a tin chute curving down from the upper hatch towards +X. Tall cover."""
    L.remove_tree(name)
    b = Builder()
    body = Lathe([(0.0, 0.5), (0.78, 0.5), (0.8, 1.2), (0.8, 3.6), (0.72, 4.1), (0.0, 4.15)], 12)

    def paint(f):
        z = f.calc_center_median().z
        return "red" if (1.1 < z < 1.4 or 3.0 < z < 3.3) else "white"
    b.lathe(body, paint)
    b.lathe(Lathe([(0.72, 4.1), (0.6, 4.6), (0.35, 5.2), (0.12, 5.6), (0.0, 5.7)], 12), "red")
    b.cyl((0, 0, 5.6), (0, 0, 6.0), 0.03, 0.02, segs=5, color="tin")
    for k in range(3):                                                                # fins
        a = math.pi / 2 + 2 * math.pi * k / 3
        m = Matrix.Rotation(a, 4, "Z") @ Matrix.Translation((0.75, 0.0, 0.0)) @ Matrix.Rotation(math.pi / 2, 4, "X")
        b.prism([(0.0, 0.0), (0.9, 0.0), (0.2, 1.8), (0.0, 1.8)], 0.08, "red", matrix=m @ Matrix.Translation((0, 0, -0.04)))
    for z, a in ((2.2, -math.pi / 2), (3.7, -math.pi / 2 + 0.6), (2.9, math.pi / 2)):      # portholes
        n = Vector((math.cos(a), math.sin(a), 0))
        c = n * 0.8 + Vector((0, 0, z))
        b.cyl(c, c + n * 0.05, 0.2, 0.2, segs=10, color="tin")
        b.cyl(c + n * 0.05, c + n * 0.06, 0.15, 0.15, segs=10, color="sky")
    b.box((0.0, -0.8, 0.95), (0.7, 0.06, 0.9), "grey_dark")                                 # lower hatch
    hatch = Vector((0.8, 0.0, 2.55))                                                  # upper hatch and the chute
    b.box(hatch + Vector((0.01, 0, 0.35)), (0.06, 0.7, 0.8), "grey_dark")
    pts = []
    for k in range(9):
        t = k / 8
        pts.append(Vector((0.8 + 3.0 * t, 0.0, 2.35 * (1 - t) ** 1.6 + 0.2)))
    for p0, p1 in zip(pts, pts[1:]):
        b.beam(p0, p1, 0.6, 0.04, "tin")
        for sy in (-1, 1):
            b.beam(p0 + Vector((0, sy * 0.3, 0.1)), p1 + Vector((0, sy * 0.3, 0.1)), 0.05, 0.22, "red")
    for sy in (-1, 1):
        b.cyl(pts[-2] + Vector((0, sy * 0.3, -0.05)), (pts[-2].x, sy * 0.3, 0.0), 0.04, 0.04, segs=5, color="grey_dark")
        b.cyl(pts[3] + Vector((0, sy * 0.3, -0.05)), (pts[3].x, sy * 0.3, 0.0), 0.04, 0.04, segs=5, color="grey_dark")
    return L.mesh_root(b, name, col, loc)


# ================================================================ the carousel
def carousel(col, loc, name="Kg_Carousel"):
    """The spinning carousel: Carousel_Base (concrete pad and the pedestal, still) and Carousel_Deck (pivot on the
    axis: a painted disc r 2 m, handrails from the hub to the rim and a ring rail) that turns in Unity."""
    L.remove_tree(name)
    root = L.empty_root(name, col, loc, size=0.6)
    base = Builder()
    base.cyl((0, 0, 0.0), (0, 0, 0.06), 2.35, 2.35, segs=20, color="concrete")
    base.cyl((0, 0, 0.06), (0, 0, 0.2), 0.35, 0.3, segs=10, color="grey_dark")
    base.to_object(name.replace("Kg_", "") + "_Base", col, pivot=(0, 0, 0), parent=root)
    d = Builder()
    colors = ("red", "yellow", "blue", "green")
    n = 8
    for k in range(n):                                                                # painted sectors
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        pts = [(0.0, 0.0)] + [(2.0 * math.cos(a0 + (a1 - a0) * j / 3), 2.0 * math.sin(a0 + (a1 - a0) * j / 3)) for j in range(4)]
        d.prism(ccw(pts), 0.08, colors[k % 4], matrix=Matrix.Translation((0, 0, 0.2)))
    d.cyl((0, 0, 0.2), (0, 0, 0.3), 2.04, 2.04, segs=24, color="tin", caps=False)             # rim
    d.cyl((0, 0, 0.28), (0, 0, 1.15), 0.09, 0.09, segs=8, color="tin")                        # hub column
    d.cyl((0, 0, 1.15), (0, 0, 1.22), 0.2, 0.2, segs=10, color="red")
    ring = []
    for k in range(6):                                                                # handrails
        a = 2 * math.pi * k / 6 + math.pi / 6
        rim = Vector((1.8 * math.cos(a), 1.8 * math.sin(a), 0.28))
        top = Vector((1.8 * math.cos(a), 1.8 * math.sin(a), 0.85))
        d.cyl(rim, top, 0.035, 0.035, segs=6, color="blue")
        d.tube_path([top, Vector((0.9 * math.cos(a), 0.9 * math.sin(a), 1.05)), Vector((0.12 * math.cos(a), 0.12 * math.sin(a), 1.12))],
                    0.03, "red" if k % 2 else "blue", segs=5)
        ring.append(top)
    ring.append(ring[0])
    d.tube_path(ring, 0.03, "yellow", segs=5)
    d.to_object(name.replace("Kg_", "") + "_Deck", col, pivot=(0, 0, 0.25), parent=root)
    return root


# ================================================================ decoration
def tyre_swan(col, loc, name="Kg_TyreSwan"):
    """A swan cut out of an old tyre, painted white: the body ring half dug in, a curved neck, a red beak."""
    L.remove_tree(name)
    b = Builder()
    _torus(b, (0, 0, 0.12), 0.38, 0.13, "white", segs=10)
    for sx in (-1, 1):                                                                # wings: tyre flaps
        b.box((sx * 0.3, 0.1, 0.3), (0.08, 0.5, 0.3), "white", rot=(0.4, 0, sx * 0.3))
    b.tube_path([(0, -0.3, 0.2), (0, -0.42, 0.55), (0, -0.3, 0.85), (0, -0.4, 0.95)], 0.06, "white", segs=6)
    b.sphere((0, -0.44, 0.98), 0.09, 8, 5, "white")
    b.lathe(Lathe([(0.04, 0.0), (0.0, 0.12)], 6, Matrix.Translation((0, -0.5, 0.97)) @ Matrix.Rotation(math.pi / 2, 4, "X")),
            "orange_red")
    for sx in (-1, 1):
        b.sphere((sx * 0.06, -0.47, 1.01), 0.018, 4, 3, "black")
    return L.mesh_root(b, name, col, loc)


def tyre_bed(col, loc, name="Kg_TyreBed", seed=206):
    """A tyre flower bed: a big tyre lying flat, painted in three colours, soil and flowers inside."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    for k, c in enumerate(("red", "yellow", "blue")):
        pts = []
        for j in range(5):
            a = 2 * math.pi * (k + j / 4) / 3
            pts.append(Vector((0.6 * math.cos(a), 0.6 * math.sin(a), 0.14)))
        for p0, p1 in zip(pts, pts[1:]):
            b.cyl(p0, p1, 0.15, 0.15, segs=6, color=c)
    b.cyl((0, 0, 0.0), (0, 0, 0.22), 0.5, 0.5, segs=12, color="brown")
    for k in range(9):
        a, r = rnd.uniform(0, 2 * math.pi), rnd.uniform(0, 0.35)
        p = Vector((r * math.cos(a), r * math.sin(a), 0.22))
        h = rnd.uniform(0.2, 0.4)
        b.cyl(p, p + Vector((0, 0, h)), 0.012, 0.012, segs=4, color="green")
        b.sphere(p + Vector((0, 0, h)), rnd.uniform(0.05, 0.08), 6, 4, rnd.choice(("red", "yellow", "white", "pink", "orange")))
    return L.mesh_root(b, name, col, loc)


def kg_lamp(col, loc, name="Kg_Lamp"):
    """Park lamp 3.4 m: a green cast pole on a fluted base with a round glass globe (lamp cell: glows at dusk)."""
    L.remove_tree(name)
    b = Builder()
    b.lathe(Lathe([(0.0, 0.0), (0.22, 0.0), (0.2, 0.25), (0.12, 0.4), (0.09, 0.8), (0.0, 0.8)], 8), "green_dark")
    b.cyl((0, 0, 0.8), (0, 0, 3.0), 0.065, 0.05, segs=8, color="green_dark")
    b.cyl((0, 0, 3.0), (0, 0, 3.08), 0.14, 0.14, segs=10, color="green_dark")
    b.lathe(Lathe(_sphere_profile(0.26, 3.34, 8), 12), "lamp")
    b.cyl((0, 0, 3.58), (0, 0, 3.66), 0.08, 0.04, segs=8, color="green_dark")
    return L.mesh_root(b, name, col, loc)


def kg_bench(col, loc, name="Kg_Bench"):
    """Low child-size bench 1.8 m, painted slats."""
    L.remove_tree(name)
    b = Builder()
    for k, c in enumerate(("blue", "yellow", "red")):
        b.box((0, -0.1 + k * 0.1, 0.34), (1.8, 0.09, 0.04), c)
    for x in (-0.75, 0.75):
        b.box((x, 0.0, 0.16), (0.06, 0.28, 0.32), "grey_dark")
    return L.mesh_root(b, name, col, loc)


# ================================================================ ground
def kg_ground(col, loc, name="Env_KindergartenGround", seed=207):
    """Kindergarten yard in arena coordinates: grass all round, the play area of trodden earth, an asphalt path
    loop, a round asphalt pad for the carousel in the middle, sand under the slide, chalk drawings (hopscotch,
    suns, flowers). Visual only."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    base = b.grid(110, 90, 44, 36, "green", matrix=L.frame((0, 8, -0.02)))
    b.paint([f for f in base if rnd.random() < 0.2], "green_light")
    b.paint([f for f in base if rnd.random() < 0.06], "olive")
    yard = b.grid(KG_W + 2, KG_D + 2, int(KG_W / 2) + 1, int(KG_D / 2) + 1, "green", matrix=L.frame((0, 0, 0)))
    b.paint([f for f in yard if rnd.random() < 0.14], "green_light")
    b.paint([f for f in yard if rnd.random() < 0.07], "olive")

    for _ in range(9):                                                                # trodden earth
        cx, cy = rnd.uniform(-17, 17), rnd.uniform(-11, 10)
        pts = [(cx + r * math.cos(a), cy + r * 0.75 * math.sin(a)) for a, r in
               [(2 * math.pi * k / 12, rnd.uniform(1.4, 2.6)) for k in range(12)]]
        b.prism(ccw(pts), 0.002, "sand_dark", matrix=Matrix.Translation((0, 0, 0.004)))

    def path(pts, w):
        for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
            dv = Vector((xb - xa, yb - ya))
            b.grid(dv.length + w, w, max(1, int(dv.length)), 1, "asphalt",
                   matrix=L.frame(((xa + xb) / 2, (ya + yb) / 2, 0.006), (0, 0, math.atan2(dv.y, dv.x))))
    path([(-19, -12), (19, -12), (19, 11), (-19, 11), (-19, -12)], 2.0)                # the loop
    path([(0, 11), (0, 16)], 2.4)                                                      # to the entrance
    pad = [(3.2 * math.cos(2 * math.pi * k / 24), 3.2 * math.sin(2 * math.pi * k / 24)) for k in range(24)]
    b.prism(ccw(pad), 0.004, "asphalt", matrix=Matrix.Translation((0, 0, 0.006)))
    sand = [(12 + 2.6 * math.cos(2 * math.pi * k / 12), -4 + 2.0 * math.sin(2 * math.pi * k / 12)) for k in range(12)]
    b.prism(ccw(sand), 0.004, "sand", matrix=Matrix.Translation((0, 0, 0.008)))
    chalk = [("chalk_hopscotch", (-9.0, -12.0), 3.0, 0.0, "white"), ("doodle_sun", (6.0, -12.3), 1.4, 0.2, "yellow"),
             ("doodle_flower", (-15.0, 11.2), 1.0, 0.0, "pink"), ("doodle_smiley", (15.5, 10.8), 1.0, 0.3, "white"),
             ("doodle_heart", (-3.0, 11.0), 1.0, -0.2, "red_light"), ("chalk_hopscotch", (18.8, -3.0), 3.0, 1.57, "cream")]
    for name_, (x, y), h, rot, c in chalk:
        b.decal(name_, L.frame((x, y, 0.012), (0, 0, rot)), c, height=h, lift=0.0)
    for v in b.bm.verts:
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return L.mesh_root(b, name, col, loc)


def build_all(col_parent):
    col = L.collection("Kindergarten", col_parent)
    X, Y = -360.0, 0.0
    out = [kg_ground(col, (X, Y, 0))]
    out.append(kg_building(col, (X, Y + 40, 0)))
    out.append(kg_fence(col, (X - 36, Y - 30, 0)))
    out.append(veranda(col, (X - 30, Y - 30, 0), "Kg_Veranda", "green", "red_dark", 203))
    out.append(veranda(col, (X - 22, Y - 30, 0), "Kg_Veranda_B", "sky", "blue", 213))
    out.append(playhouse(col, (X - 15, Y - 30, 0)))
    out.append(rocket_slide(col, (X - 10, Y - 30, 0)))
    out.append(carousel(col, (X - 2, Y - 30, 0)))
    out.append(tyre_swan(col, (X + 4, Y - 30, 0)))
    out.append(tyre_bed(col, (X + 6, Y - 30, 0)))
    out.append(kg_lamp(col, (X + 8, Y - 30, 0)))
    out.append(kg_bench(col, (X + 10, Y - 30, 0)))
    return out

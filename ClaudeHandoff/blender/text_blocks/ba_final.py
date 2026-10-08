"""The final of the walk, «Мама зовёт домой»: the boss «Тот, кто в сумерках» as Бабай with a sack and a crooked
staff (user's pick on 2026-09-25, replaces the scarecrow x3 of ba_bosses) and the crow of his flock.
Parts are separate objects so Unity animates them by code (no skeleton):
Boss_Dusk -> Dusk_Body -> Dusk_Head -> Dusk_Eyes; Dusk_ArmL; Dusk_ArmR -> Dusk_Staff; Dusk_Sack; Dusk_CrowL/R.
Enemy_Crow -> Crow_Body -> Crow_WingL/R (origin = body centre: it flies)."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe
import ba_dusk as D

FRONT = -math.pi / 2

# long coat from the ground to the shoulders: very tall and thin, flared hem
COAT = [(0.0, 0.03), (0.6, 0.03), (0.55, 0.45), (0.46, 1.2), (0.38, 2.1), (0.33, 2.85), (0.34, 3.3), (0.4, 3.58),
        (0.36, 3.74), (0.18, 3.83), (0.0, 3.85)]
HEAD = [(0.0, 3.8), (0.17, 3.83), (0.27, 3.93), (0.31, 4.07), (0.3, 4.22), (0.25, 4.36), (0.15, 4.45), (0.0, 4.48)]
SACK = [(0.0, 0.0), (0.36, 0.04), (0.52, 0.3), (0.55, 0.62), (0.49, 0.92), (0.33, 1.14), (0.14, 1.26), (0.08, 1.33),
        (0.13, 1.41), (0.0, 1.44)]


def _crow(b, c, facing=-1, s=1.0):
    """A crow sitting on a shoulder, looking forward (facing -1 = -Y)."""
    b.sphere(c, 0.12 * s, 8, 5, "black", scale=(0.75, 1.25, 0.85))
    b.sphere(c + Vector((0, 0.13 * facing, 0.1)) * s, 0.075 * s, 8, 5, "black")
    b.cyl(c + Vector((0, 0.19 * facing, 0.1)) * s, c + Vector((0, 0.3 * facing, 0.085)) * s, 0.024 * s, 0.0,
          segs=4, color="orange")
    for sx in (-1, 1):
        b.sphere(c + Vector((sx * 0.042, 0.17 * facing, 0.125)) * s, 0.014 * s, 4, 3, "lamp")
        b.box(c + Vector((sx * 0.1, 0.0, 0.02)) * s, (0.03 * s, 0.2 * s, 0.1 * s), "black", rot=(0.25, 0, sx * 0.1))
    b.box(c + Vector((0, -0.18 * facing, -0.02)) * s, (0.1 * s, 0.16 * s, 0.025 * s), "black", rot=(0.35, 0, 0))
    for sx in (-1, 1):
        b.cyl(c + Vector((sx * 0.035, 0.0, -0.08)) * s, c + Vector((sx * 0.035, -0.02, -0.14)) * s, 0.01 * s,
              0.008 * s, segs=4, color="grey_dark")


def _fingers(a, wrist, sgn, rnd, grip=False):
    """Long knobbly branch fingers; a grip curls them round the staff."""
    for k in range(5):
        spread = (k - 2) * 0.35
        base = wrist + Vector((sgn * 0.02, -0.03 + 0.02 * math.sin(spread), -0.05 + 0.03 * math.cos(spread)))
        if grip:
            mid = base + Vector((-sgn * 0.08, -0.1 + 0.04 * k, -0.05))
            tip = mid + Vector((-sgn * 0.08, 0.06, 0.0))
        else:
            mid = base + Vector((sgn * 0.05 * math.sin(spread), -0.06 * math.cos(spread) - 0.03, -0.2))
            tip = mid + Vector((sgn * 0.04 * math.sin(spread), -0.04, -0.2 + rnd.uniform(-0.03, 0.03)))
        a.cyl(base, mid, 0.024, 0.018, segs=5, color="wood_dark")
        a.sphere(mid, 0.022, 5, 3, "wood_dark")
        a.cyl(mid, tip, 0.018, 0.004, segs=5, color="wood_dark")


def build_babai(col, location=(0, 0, 0), name="Boss_Dusk", seed=61):
    """«Тот, кто в сумерках» ~5 m: Бабай. A very tall thin figure in a long black coat to the ground, a burlap-sack
    head with a stitched grin and glowing eyes (Dusk_Eyes, separate: the fake scarecrows of «Прятки» have none)
    under a wide hat, long arms with branch fingers, a crooked staff in the right hand, a big patched sack
    on his back (Dusk_Sack: grows with the balls inside) and a crow on each shoulder (fly off as the flock)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    root = L.empty_root(name, col, location, size=1.2)
    parts = {}

    # ---------------------------------------------------------------- body: coat, collar, rope, sack strap
    b = Builder()
    coat = Lathe(COAT, 12, Matrix.Diagonal((1.0, 0.78, 1.0, 1.0)))
    b.lathe(coat, "black")
    for k in range(14):                                                                   # ragged hem
        a = FRONT + k * 2 * math.pi / 14 + rnd.uniform(-0.1, 0.1)
        r0 = COAT[1][0]
        p = Vector((r0 * math.cos(a), 0.78 * r0 * math.sin(a), 0.06))
        drop = rnd.uniform(0.08, 0.2)
        t = Vector((-math.sin(a), 0.78 * math.cos(a), 0)).normalized()
        n = Vector((math.cos(a), 0.78 * math.sin(a), 0)).normalized()
        b.quad([p - t * 0.12, p + t * 0.12, p + n * 0.03 + Vector((0, 0, -drop))], "black", want=n)
        b.quad([p - t * 0.12, p + n * 0.03 + Vector((0, 0, -drop)), p + t * 0.12], "black", want=-n)
    for k in range(8):                                                                    # straw under the hem
        a = rnd.uniform(0, 2 * math.pi)
        p = Vector((0.4 * math.cos(a), 0.3 * math.sin(a), 0.1))
        tip = p + Vector((0.18 * math.cos(a), 0.14 * math.sin(a), -rnd.uniform(0.08, 0.14)))
        b.cyl(p, tip, 0.03, 0.005, segs=4, color=rnd.choice(["sand", "yellow", "ochre"]))
    # front edge of the coat, lapels, buttons, patches
    b.decal_strip(coat, [(FRONT + 0.04, coat.s_at_z(3.55)), (FRONT + 0.04, coat.s_at_z(0.1))], 0.035, "grey_dark", off=0.006)
    for sx in (-1, 1):
        b.decal_strip(coat, [(FRONT + sx * 0.05, coat.s_at_z(3.6)), (FRONT + sx * 0.42, coat.s_at_z(3.2)),
                             (FRONT + sx * 0.08, coat.s_at_z(2.8))], 0.05, "grey_dark", off=0.007)
    for z in (2.65, 2.35, 2.05, 1.6, 1.2):
        b.decal_disc(coat, FRONT + 0.1, coat.s_at_z(z), 0.035, 0.035, "grey", segs=6, off=0.008)
    for phi, z, col_ in ((FRONT - 0.7, 1.0, "grey_dark"), (FRONT + 0.9, 1.9, "maroon"), (FRONT + 2.4, 1.4, "grey_dark"),
                         (FRONT + 3.6, 2.6, "grey_dark")):
        b.decal_disc(coat, phi, coat.s_at_z(z), 0.13, 0.11, col_, segs=4, off=0.006, rot=rnd.uniform(0, 1))
    for phi in (FRONT - 0.5, FRONT + 1.3):                                                 # seams
        b.decal_strip(coat, [(phi, coat.s_at_z(2.3)), (phi + 0.05, coat.s_at_z(0.4))], 0.012, "grey_dark", off=0.005)
    # rope belt with a knot and dangling ends
    belt = Lathe([(coat.radius_at(coat.s_at_z(2.02)) + 0.018, 1.99), (coat.radius_at(coat.s_at_z(2.02)) + 0.018, 2.06)],
                 12, Matrix.Diagonal((1.0, 0.78, 1.0, 1.0)))
    b.lathe(belt, "wood_light", cap_bottom=False, cap_top=False)
    knot = coat.point(FRONT + 0.35, coat.s_at_z(2.02), 0.03)
    b.sphere(knot, 0.045, 6, 4, "wood_light")
    b.cyl(knot, knot + Vector((0.02, -0.02, -0.32)), 0.018, 0.012, segs=4, color="wood_light")
    b.cyl(knot, knot + Vector((0.07, -0.01, -0.26)), 0.018, 0.012, segs=4, color="wood_light")
    # tall upturned collar
    for k in range(10):
        a = FRONT + math.pi * 0.25 + k * math.pi * 1.5 / 9
        p = Vector((0.24 * math.cos(a), 0.2 * math.sin(a), 3.74))
        out = Vector((math.cos(a), 0.8 * math.sin(a), 0)).normalized()
        b.box(p + out * 0.02 + Vector((0, 0, 0.1)), (0.16, 0.035, 0.28), "black",
              rot=(0.0, 0.0, math.atan2(out.y, out.x) + math.pi / 2))
    # sack strap: left shoulder -> across the chest -> round the right hip to the sack neck on the back
    strap = [Vector((0.3, -0.18, 3.62)), Vector((0.12, -0.33, 3.2)), Vector((-0.2, -0.33, 2.55)),
             Vector((-0.36, -0.12, 2.3)), Vector((-0.2, 0.3, 2.3)), Vector((0.1, 0.46, 2.36))]
    for p0, p1 in zip(strap, strap[1:]):
        b.beam(p0, p1, 0.05, 0.035, "wood_light", up=(0, 0, 1))
    shoulder_strap = [Vector((0.3, -0.18, 3.62)), Vector((0.36, 0.1, 3.7)), Vector((0.25, 0.36, 3.2)),
                      Vector((0.14, 0.46, 2.4))]
    for p0, p1 in zip(shoulder_strap, shoulder_strap[1:]):
        b.beam(p0, p1, 0.05, 0.035, "wood_light", up=(0, 0, 1))
    for k in range(6):                                                                    # straw from the collar
        a = FRONT + math.pi * (0.3 + 0.28 * k)
        p = Vector((0.18 * math.cos(a), 0.14 * math.sin(a), 3.8))
        b.cyl(p, p + Vector((0.16 * math.cos(a), 0.12 * math.sin(a), 0.1)), 0.025, 0.004, segs=4, color="yellow")
    parts["body"] = b

    # ---------------------------------------------------------------- head: burlap sack, grin, hat
    h = Builder()
    head = Lathe(HEAD, 12, Matrix.Diagonal((1.0, 0.92, 1.0, 1.0)))
    h.lathe(head, "sand_dark")
    h.cyl((0, 0, 3.83), (0, 0, 3.9), 0.2, 0.19, segs=10, color="wood_light")              # rope round the neck
    for sx, dx in ((0.12, 0.05), (0.16, 0.12)):
        h.cyl((sx, -0.17, 3.86), (sx + dx, -0.2, 3.62), 0.016, 0.01, segs=4, color="wood_light")
    ez = 4.13
    for sx in (-1, 1):                                                                    # eye holes (the glow is Dusk_Eyes)
        h.decal_disc(head, FRONT + sx * 0.36, head.s_at_z(ez), 0.07, 0.055, "black", segs=8, off=0.004)
    # stitched grin from ear to ear, curling up at the ends
    mouth = [(FRONT + (k - 6) * 0.13, head.s_at_z(3.99 + 0.005 * (k - 6) ** 2)) for k in range(13)]
    h.decal_strip(head, mouth, 0.02, "black", off=0.005)
    for k in range(1, 12):
        phi, s = mouth[k]
        h.decal_strip(head, [(phi - 0.02, s - 0.045), (phi + 0.02, s + 0.045)], 0.012, "black", off=0.007)
    # seam over the skull and a patch
    h.decal_strip(head, [(FRONT + 1.3, head.s_at_z(3.9)), (FRONT + 1.2, head.s_at_z(4.35))], 0.014, "wood_dark", off=0.004)
    h.decal_disc(head, FRONT - 0.95, head.s_at_z(4.25), 0.07, 0.06, "sand", segs=4, off=0.005, rot=0.4)
    # wide black hat tilted back so the face shows from above
    hat = Matrix.Translation((0, 0.03, 4.38)) @ Matrix.Rotation(-0.22, 4, "X") @ Matrix.Rotation(0.12, 4, "Y")
    h.lathe(Lathe([(0.0, 0.0), (0.44, 0.0), (0.46, 0.035), (0.0, 0.035)], 14, hat), "black")
    h.lathe(Lathe([(0.27, 0.03), (0.25, 0.26), (0.23, 0.36), (0.0, 0.37)], 12, hat), "black")
    h.lathe(Lathe([(0.272, 0.04), (0.265, 0.11)], 12, hat), "maroon", cap_bottom=False, cap_top=False)
    for k in range(5):                                                                    # straw from under the hat
        a = FRONT + math.pi * (0.55 + 0.25 * k)
        p = Vector((0.27 * math.cos(a), 0.25 * math.sin(a), 4.3))
        h.cyl(p, p + Vector((0.12 * math.cos(a), 0.1 * math.sin(a), -0.14)), 0.02, 0.004, segs=4, color="yellow")
    parts["head"] = h

    # ---------------------------------------------------------------- glowing eyes (separate)
    e = Builder()
    for sx in (-1, 1):
        p = head.point(FRONT + sx * 0.36, head.s_at_z(ez), 0.012)
        e.sphere(p, 0.045, 8, 4, "lamp", scale=(1.25, 0.55, 0.85))
    parts["eyes"] = e

    # ---------------------------------------------------------------- arms: long sleeves, branch fingers
    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.43, 0.0, 3.6))
        elbow = Vector((sgn * 0.56, -0.04, 2.62))
        wrist = Vector((sgn * 0.58, -0.14, 1.8))
        a.sphere(sh, 0.13, 8, 5, "black")
        a.cyl(sh, elbow, 0.115, 0.1, segs=8, color="black")
        a.sphere(elbow, 0.1, 8, 5, "black")
        a.cyl(elbow, wrist + (elbow - wrist).normalized() * 0.05, 0.1, 0.12, segs=8, color="black")
        cuff = wrist + (elbow - wrist).normalized() * 0.05
        for k in range(6):                                                                 # ragged cuff
            ang = k * math.pi / 3
            p = cuff + Vector((0.11 * math.cos(ang), 0.11 * math.sin(ang), 0.0))
            a.quad([p + Vector((-0.04, 0, 0)), p + Vector((0.04, 0, 0)), p + Vector((0, 0, -rnd.uniform(0.07, 0.14)))],
                   "black", want=Vector((math.cos(ang), math.sin(ang), 0)))
        a.sphere(wrist, 0.05, 6, 4, "wood_dark")
        _fingers(a, wrist, sgn, rnd, grip=(side == "R"))
        parts["arm" + side] = a

    # ---------------------------------------------------------------- crooked staff in the right hand
    s = Builder()
    grip = Vector((-0.58, -0.22, 1.72))
    pole = [Vector((-0.57, -0.3, 0.02)), Vector((-0.58, -0.26, 0.9)), grip, Vector((-0.6, -0.2, 2.9)),
            Vector((-0.62, -0.22, 3.9)), Vector((-0.6, -0.36, 4.18)), Vector((-0.56, -0.54, 4.24)),
            Vector((-0.53, -0.66, 4.12)), Vector((-0.53, -0.66, 3.98))]
    radii = [0.045, 0.05, 0.05, 0.048, 0.045, 0.042, 0.04, 0.036, 0.03]
    for (p0, r0), (p1, r1) in zip(zip(pole, radii), zip(pole[1:], radii[1:])):
        s.cyl(p0, p1, r0, r1, segs=6, color="wood_dark")
        s.sphere(p1, r1, 6, 3, "wood_dark")
    for z in (0.5, 1.3, 2.4, 3.3):                                                        # knots
        t = Vector((-0.58, -0.25, z))
        s.sphere(t + Vector((0.03, 0, 0)), 0.035, 5, 3, "wood")
    rag = Vector((-0.6, -0.3, 4.05))                                                      # a rag tied under the crook
    s.cyl(rag, rag + Vector((0, 0, 0.06)), 0.06, 0.06, segs=6, color="maroon")
    s.quad([rag + Vector((-0.05, -0.02, 0)), rag + Vector((0.05, -0.02, 0)), rag + Vector((0.08, -0.05, -0.3))],
           "maroon", want=(0, -1, 0))
    s.quad([rag + Vector((-0.05, 0.02, 0)), rag + Vector((0.02, 0.05, -0.24)), rag + Vector((0.05, 0.02, 0))],
           "maroon", want=(0, 1, 0))
    parts["staff"] = s

    # ---------------------------------------------------------------- the sack on his back
    k = Builder()
    sack_m = Matrix.Translation((0.12, 0.58, 0.92)) @ Matrix.Diagonal((1.0, 0.82, 1.0, 1.0))
    sack = Lathe(SACK, 12, sack_m)
    k.lathe(sack, "sand_dark")
    for phi, z, col_, r in ((FRONT + math.pi, 0.5, "sand", 0.14), (FRONT + math.pi + 1.1, 0.8, "wood_light", 0.1),
                            (FRONT + math.pi - 0.9, 0.3, "sand", 0.09)):
        k.decal_disc(sack, phi, sack.s_at_z(z), r, r * 0.8, col_, segs=4, off=0.006, rot=0.5)
    for phi in (FRONT + math.pi - 0.3, FRONT + math.pi + 0.6):                            # seams with stitches
        k.decal_strip(sack, [(phi, sack.s_at_z(0.1)), (phi + 0.1, sack.s_at_z(1.0))], 0.012, "wood_dark", off=0.005)
    tie = sack_m @ Vector((0, 0, 1.3))
    k.cyl(tie - Vector((0, 0, 0.04)), tie + Vector((0, 0, 0.04)), 0.11, 0.1, segs=8, color="wood_light")
    for dx in (-0.06, 0.07):                                                               # ears of the tied top
        k.cyl(tie + Vector((0, 0, 0.05)), tie + Vector((dx, 0.02, 0.2)), 0.05, 0.015, segs=5, color="sand_dark")
    for j in range(4):                                                                     # lumps: balls inside
        a = FRONT + math.pi + rnd.uniform(-1.2, 1.2)
        p = sack.point(a, sack.s_at_z(rnd.uniform(0.25, 0.8)), -0.08)
        k.sphere(p, 0.16, 6, 4, "sand_dark")
    parts["sack"] = k

    # ---------------------------------------------------------------- crows on the shoulders
    for sgn, side in ((1, "L"), (-1, "R")):
        c = Builder()
        _crow(c, Vector((sgn * 0.45, 0.03, 3.86)), facing=-1, s=1.0)
        parts["crow" + side] = c

    D._finish(parts, [("body", "Dusk_Body", (0, 0, 0.6), None),
                      ("head", "Dusk_Head", (0, 0, 3.84), "body"),
                      ("eyes", "Dusk_Eyes", (0, -0.31, ez), "head"),
                      ("armL", "Dusk_ArmL", (0.43, 0, 3.6), "body"),
                      ("armR", "Dusk_ArmR", (-0.43, 0, 3.6), "body"),
                      ("staff", "Dusk_Staff", tuple(grip), "armR"),
                      ("sack", "Dusk_Sack", (0.12, 0.52, 2.36), "body"),
                      ("crowL", "Dusk_CrowL", (0.45, 0.03, 3.74), "body"),
                      ("crowR", "Dusk_CrowR", (-0.45, 0.03, 3.74), "body")], col, root, 1.0)
    return root


# ================================================================ crow of the flock
def build_crow(col, location=(0, 0, 0), name="Enemy_Crow", seed=67):
    """Crow of the flock ~1.2 m wingspan (bigger than a real one so it reads from the camera): black with a
    grey sheen, an orange beak and glowing eyes. Wings are separate (flap around their root, local Y axis).
    Origin = body centre: the crow flies, the game positions it in the air."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    root = L.empty_root(name, col, location, size=0.3)
    parts = {}

    b = Builder()

    def sheen(f):
        return "grey_dark" if f.normal.z > 0.55 else "black"

    b.sphere((0, 0, 0), 0.17, 10, 6, sheen, scale=(0.72, 1.35, 0.72))
    b.sphere((0, -0.24, 0.07), 0.1, 8, 5, sheen)
    b.cyl((0, -0.31, 0.065), (0, -0.45, 0.04), 0.035, 0.0, segs=5, color="orange")
    for sx in (-1, 1):
        b.sphere((sx * 0.055, -0.3, 0.1), 0.02, 5, 3, "lamp")
    for k in range(5):                                                                     # fan tail
        a = (k - 2) * 0.22
        p0 = Vector((0, 0.18, 0.0))
        p1 = p0 + Vector((math.sin(a) * 0.12, 0.28, -0.03))
        b.beam(p0, p1, 0.07, 0.018, "black", up=(0, 0, 1))
    for sx in (-1, 1):                                                                     # legs tucked under
        b.cyl((sx * 0.05, 0.02, -0.1), (sx * 0.05, 0.08, -0.2), 0.014, 0.01, segs=4, color="grey_dark")
        for k in range(3):
            b.cyl((sx * 0.05, 0.08, -0.2), (sx * 0.05 + (k - 1) * 0.03, 0.02, -0.22), 0.008, 0.005, segs=3,
                  color="grey_dark")
    parts["body"] = b

    for sgn, side in ((1, "L"), (-1, "R")):
        w = Builder()
        # inner panel of the wing: a thin slab swept back, then the long primaries splayed like fingers
        panel = [(0.08, -0.12), (0.42, -0.07), (0.47, 0.1), (0.08, 0.12)]
        pts = [(sgn * x, y) for x, y in panel]
        w.prism(D.ccw(pts), 0.03, "black", matrix=Matrix.Translation((0, 0, 0.035)))
        for k in range(4):
            p0 = Vector((sgn * (0.4 + k * 0.02), -0.06 + k * 0.05, 0.05))
            p1 = Vector((sgn * (0.74 - k * 0.05), -0.02 + k * 0.09, 0.05 + rnd.uniform(-0.01, 0.02)))
            w.beam(p0, p1, 0.06, 0.016, "grey_dark" if k == 3 else "black", up=(0, 0, 1))
        parts["wing" + side] = w

    D._finish(parts, [("body", "Crow_Body", (0, 0, 0), None),
                      ("wingL", "Crow_WingL", (0.09, -0.04, 0.05), "body"),
                      ("wingR", "Crow_WingR", (-0.09, -0.04, 0.05), "body")], col, root, 1.0)
    return root


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    return [
        build_babai(col, (5.0, -22.0, 0.0)),
        build_crow(col, (8.5, -22.0, 1.0)),
    ]

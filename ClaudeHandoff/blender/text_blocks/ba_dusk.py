"""Dusk enemies: the mannequin (moves when nobody looks), the scarecrow (catches balls in flight) and the
shadow (only vulnerable in the light). Rigid parts under an empty root, pivots at the joints, animated in
code. Builders take `scale` and an `extras(parts)` hook so the bosses can reuse them.
Glowing bits use the lamp / window_lit cells: they light up with the time-of-day emission at dusk."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2


def _finish(parts, spec, col, root, scale):
    """Turn builders into objects. spec = [(key, name, pivot, parent_key or None)] in creation order."""
    obs = {}
    for key, name, pivot, parent in spec:
        par = root if parent is None else obs[parent]
        obs[key] = parts[key].to_object(name, col, pivot=pivot, parent=par, scale=scale)
    return obs


def ccw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return pts if area > 0 else list(reversed(pts))


def _ring(b, p0, p1, r, color="grey"):
    """Joint gap ring between two limb segments."""
    b.cyl(p0, p1, r, r, segs=10, color=color)


# ================================================================ mannequin
MANNEQUIN_TORSO = [(0.0, 0.88), (0.12, 0.89), (0.17, 0.95), (0.18, 1.02), (0.15, 1.12), (0.14, 1.2),
                   (0.17, 1.3), (0.19, 1.38), (0.2, 1.44), (0.16, 1.5), (0.07, 1.54), (0.0, 1.55)]
MANNEQUIN_HEAD = [(0.0, 1.62), (0.07, 1.635), (0.1, 1.68), (0.108, 1.74), (0.1, 1.8), (0.075, 1.85),
                  (0.04, 1.875), (0.0, 1.88)]


def build_mannequin(col, location=(0, 0, 0), name="Enemy_Mannequin", scale=1.0, plastic="skin_light",
                    seed=31, tag=True, extras=None):
    """Faceless shop mannequin ~1.9 m with jointed limbs: Body -> Head, ArmL/R, LegL/R. Cracks and chips
    in the plastic, a price tag on the right wrist. extras(parts) may dress it (the PE teacher boss)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.split("_", 1)[1] if "_" in name else name
    root = L.empty_root(name, col, location, size=0.4 * scale)
    parts = {}

    b = Builder()
    torso = Lathe(MANNEQUIN_TORSO, 14, Matrix.Diagonal((1.0, 0.62, 1.0, 1.0)))
    b.lathe(torso, plastic)
    parts["torso_lathe"] = torso
    b.cyl((0, 0, 1.5), (0, 0, 1.62), 0.05, 0.048, segs=10, color=plastic)                  # neck
    _ring(b, (0, 0, 1.585), (0, 0, 1.597), 0.053)
    for _ in range(3):                                                                    # cracks
        pts = [(rnd.uniform(0, 2 * math.pi), torso.s_at_z(rnd.uniform(1.0, 1.4)))]
        for _k in range(5):
            phi, s = pts[-1]
            pts.append((phi + rnd.uniform(-0.25, 0.25), s + rnd.uniform(-0.06, 0.06)))
        b.decal_strip(torso, pts, 0.01, "grey_dark", off=0.004)
    for _ in range(6):                                                                    # chipped paint
        b.decal_disc(torso, rnd.uniform(0, 2 * math.pi), torso.s_at_z(rnd.uniform(0.95, 1.45)),
                     rnd.uniform(0.015, 0.03), rnd.uniform(0.012, 0.025), "concrete", segs=5, off=0.003,
                     rot=rnd.uniform(0, 3))
    parts["body"] = b

    h = Builder()
    head = Lathe(MANNEQUIN_HEAD, 12)
    h.lathe(head, plastic)
    parts["head_lathe"] = head
    h.decal_strip(head, [(FRONT + 0.6, head.s_at_z(1.84)), (FRONT + 0.45, head.s_at_z(1.79)),
                         (FRONT + 0.55, head.s_at_z(1.75)), (FRONT + 0.4, head.s_at_z(1.7))], 0.008, "grey_dark", off=0.004)
    h.decal_disc(head, FRONT, head.s_at_z(1.74), 0.03, 0.05, plastic if plastic != "skin_light" else "cream",
                 segs=6, off=0.002)                                                         # faint nose bump
    parts["head"] = h

    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.2, 0.0, 1.42))
        elbow = Vector((sgn * 0.25, 0.01, 1.13))
        wrist = Vector((sgn * 0.26, -0.06, 0.87))
        a.sphere(sh, 0.065, 10, 6, plastic)
        a.cyl(sh + Vector((sgn * 0.02, 0, -0.03)), elbow, 0.055, 0.046, segs=10, color=plastic)
        a.sphere(elbow, 0.046, 8, 5, plastic)
        _ring(a, elbow + Vector((0, 0, 0.006)), elbow - Vector((0, 0, 0.006)), 0.049)
        a.cyl(elbow, wrist, 0.043, 0.032, segs=10, color=plastic)
        _ring(a, wrist + (elbow - wrist).normalized() * 0.01, wrist, 0.035)
        a.sphere(wrist + Vector((0.004 * sgn, -0.02, -0.08)), 0.09, 8, 5, plastic, scale=(0.35, 0.5, 1.0))  # hand
        if tag and side == "R":
            a.beam(wrist, wrist + Vector((-0.02, -0.03, -0.12)), 0.004, 0.004, "white")
            a.box(wrist + Vector((-0.025, -0.035, -0.16)), (0.05, 0.004, 0.07), "cream", rot=(0, 0.2, 0))
            a.box(wrist + Vector((-0.025, -0.038, -0.15)), (0.036, 0.003, 0.01), "red", rot=(0, 0.2, 0))
        parts["arm" + side] = a
        g = Builder()
        hip = Vector((sgn * 0.09, 0.0, 0.92))
        knee = Vector((sgn * 0.1, 0.0, 0.5))
        ankle = Vector((sgn * 0.1, 0.01, 0.1))
        g.sphere(hip, 0.075, 10, 6, plastic)
        g.cyl(hip, knee, 0.075, 0.055, segs=10, color=plastic)
        g.sphere(knee, 0.055, 8, 5, plastic)
        _ring(g, knee + Vector((0, 0, 0.007)), knee - Vector((0, 0, 0.007)), 0.058)
        g.cyl(knee, ankle, 0.052, 0.036, segs=10, color=plastic)
        g.sphere(ankle + Vector((0, -0.06, -0.055)), 0.13, 8, 5, plastic, scale=(0.36, 1.0, 0.35))        # foot
        parts["leg" + side] = g

    if extras:
        extras(parts)
    obs = _finish(parts, [("body", prefix + "_Body", (0, 0, 0.95), None),
                          ("head", prefix + "_Head", (0, 0, 1.6), "body"),
                          ("armL", prefix + "_ArmL", (0.2, 0, 1.42), "body"),
                          ("armR", prefix + "_ArmR", (-0.2, 0, 1.42), "body"),
                          ("legL", prefix + "_LegL", (0.09, 0, 0.92), "body"),
                          ("legR", prefix + "_LegR", (-0.09, 0, 0.92), "body")], col, root, scale)
    return root


# ================================================================ scarecrow
SCARECROW_COAT = [(0.0, 0.72), (0.36, 0.72), (0.33, 0.9), (0.27, 1.15), (0.24, 1.38), (0.23, 1.5),
                  (0.13, 1.56), (0.0, 1.57)]


def build_scarecrow(col, location=(0, 0, 0), name="Enemy_Scarecrow", scale=1.0, coat="olive", coat_profile=None,
                    glowing_eyes=False, hat="felt", crow=True, seed=37, extras=None):
    """Garden scarecrow ~2 m on a single pole: an old coat with patches, straw everywhere, a stitched sack
    head under a felt hat. Arms are the cross-pole in sleeves, spread wide to catch balls.
    Parts: Body (pole + coat) -> Head, ArmL/R."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.split("_", 1)[1] if "_" in name else name
    root = L.empty_root(name, col, location, size=0.4 * scale)
    prof = coat_profile or SCARECROW_COAT
    bottom = prof[0][1]
    parts = {}

    b = Builder()
    b.beam((0, 0, 0.0), (0, 0, 1.62), 0.08, 0.08, "wood")                                 # the pole
    coat_l = Lathe(prof, 12, Matrix.Diagonal((1.0, 0.8, 1.0, 1.0)))
    b.lathe(coat_l, coat)
    parts["coat_lathe"] = coat_l
    for k in range(12):                                                                  # ragged hem
        a = FRONT + k * math.pi / 6 + rnd.uniform(-0.1, 0.1)
        r0 = prof[1][0]
        p = Vector((r0 * math.cos(a), 0.8 * r0 * math.sin(a), bottom + 0.02))
        drop = rnd.uniform(0.06, 0.14)
        t = Vector((-math.sin(a), 0.8 * math.cos(a), 0)).normalized()
        n = Vector((math.cos(a), 0.8 * math.sin(a), 0)).normalized()
        b.quad([p - t * 0.09, p + t * 0.09, p + n * 0.02 + Vector((0, 0, -drop))], coat, want=n)
        b.quad([p - t * 0.09, p + n * 0.02 + Vector((0, 0, -drop)), p + t * 0.09], coat, want=-n)
    for k in range(9):                                                                   # straw under the hem
        a = rnd.uniform(0, 2 * math.pi)
        p = Vector((0.2 * math.cos(a), 0.16 * math.sin(a), bottom + 0.02))
        tip = p + Vector((0.12 * math.cos(a), 0.1 * math.sin(a), -rnd.uniform(0.12, 0.22)))
        b.cyl(p, tip, 0.022, 0.004, segs=4, color=rnd.choice(["sand", "yellow", "ochre"]))
    for phi, z, col_ in ((FRONT - 0.55, 1.05, "brown"), (FRONT + 0.8, 1.3, "blue"), (FRONT + 2.6, 0.95, "wood_dark")):
        b.decal_disc(coat_l, phi, coat_l.s_at_z(z), 0.09, 0.08, col_, segs=4, off=0.006, rot=0.3)     # patches
    for z in (1.4, 1.22, 1.04):                                                          # buttons
        b.decal_disc(coat_l, FRONT + 0.12, coat_l.s_at_z(z), 0.025, 0.025, "black", segs=6, off=0.006)
    b.decal_strip(coat_l, [(FRONT + 0.02, coat_l.s_at_z(1.5)), (FRONT + 0.02, coat_l.s_at_z(bottom + 0.05))],
                  0.02, "grey_dark", off=0.005)                                           # coat flap edge
    for k in range(5):                                                                   # straw from the collar
        a = FRONT + math.pi * (0.2 + 0.4 * k)
        p = Vector((0.1 * math.cos(a), 0.08 * math.sin(a), 1.55))
        b.cyl(p, p + Vector((0.12 * math.cos(a), 0.1 * math.sin(a), 0.1)), 0.02, 0.004, segs=4, color="yellow")
    parts["body"] = b

    h = Builder()
    hz = 1.73
    head = Lathe([(0.0, 1.56), (0.1, 1.575), (0.16, 1.63), (0.175, 1.72), (0.165, 1.8), (0.13, 1.86), (0.07, 1.9),
                  (0.0, 1.91)], 12)
    h.lathe(head, "sand_dark")
    parts["head_lathe"] = head
    h.cyl((0, 0, 1.6), (0, 0, 1.64), 0.13, 0.12, segs=10, color="wood_light")              # rope round the neck
    for k in range(6):
        a = FRONT + k * math.pi / 3
        p = Vector((0.12 * math.cos(a), 0.12 * math.sin(a), 1.6))
        h.cyl(p, p + Vector((0.08 * math.cos(a), 0.08 * math.sin(a), -0.08)), 0.018, 0.004, segs=4, color="yellow")
    r_face = 0.175
    for sx in (-1, 1):
        if glowing_eyes:
            h.sphere((sx * 0.065, -0.16, hz + 0.02), 0.035, 8, 5, "lamp", scale=(1.2, 0.5, 0.8))
        else:
            size = 0.042 if sx > 0 else 0.032                                              # mismatched buttons
            h.cyl((sx * 0.065, -0.15, hz + 0.02), (sx * 0.065, -0.185, hz + 0.02), size, size, segs=8, color="black")
            h.box((sx * 0.065, -0.187, hz + 0.02), (size * 1.1, 0.004, 0.006), "grey")
    mouth = [(FRONT + (k - 4) * 0.075, head.s_at_z(hz - 0.07 - 0.012 * math.cos((k - 4) * 0.5))) for k in range(9)]
    h.decal_strip(head, mouth, 0.012, "black", off=0.004)
    for k in range(1, 8):                                                                # stitches across the mouth
        phi, s = mouth[k]
        h.decal_strip(head, [(phi, s - 0.025), (phi, s + 0.025)], 0.008, "black", off=0.006)
    h.decal_strip(head, [(FRONT + 0.9, head.s_at_z(1.62)), (FRONT + 0.9, head.s_at_z(1.88))], 0.01, "wood_dark", off=0.004)
    if hat == "felt":
        h.cyl((0, 0.01, 1.86), (0, 0.015, 1.88), 0.27, 0.27, segs=14, color="wood_dark")        # brim
        h.cyl((0, 0.015, 1.88), (0.0, 0.03, 2.03), 0.14, 0.12, segs=12, color="wood_dark")      # crown
        h.cyl((0, 0.016, 1.885), (0, 0.018, 1.93), 0.143, 0.14, segs=12, color="black")          # band
        h.box((0.08, -0.12, 1.96), (0.06, 0.02, 0.05), "brown", rot=(0.3, 0, 0.4))               # patch
    elif hat == "tall":
        h.cyl((0, 0.01, 1.86), (0, 0.015, 1.885), 0.34, 0.33, segs=14, color="black")
        h.lathe(Lathe([(0.16, 0.0), (0.12, 0.25), (0.06, 0.48), (0.02, 0.62), (0.0, 0.66)], 10,
                      Matrix.Translation((0, 0.03, 1.88)) @ Matrix.Rotation(-0.35, 4, "X")), "black")
        h.cyl((0, 0.02, 1.885), (0, 0.03, 1.95), 0.165, 0.15, segs=10, color="maroon")
    parts["head"] = h

    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.16, 0.0, 1.45))
        end = Vector((sgn * 0.88, 0.0, 1.45))
        cuff = Vector((sgn * 0.7, 0.0, 1.44))
        a.beam(sh, end, 0.06, 0.06, "wood")
        a.cyl(sh, cuff, 0.1, 0.085, segs=8, color=coat)
        for k in range(6):                                                               # straw hands
            ang = k * math.pi / 3 + 0.3
            dirn = Vector((sgn * 1.0, 0.5 * math.cos(ang), 0.5 * math.sin(ang))).normalized()
            p = cuff + Vector((0, 0.05 * math.cos(ang), 0.05 * math.sin(ang)))
            a.cyl(p, p + dirn * rnd.uniform(0.13, 0.2), 0.02, 0.004, segs=4, color=rnd.choice(["sand", "yellow"]))
        if crow and side == "L":
            c = end + Vector((-0.05 * sgn, 0.0, 0.1))
            a.sphere(c, 0.075, 8, 5, "black", scale=(0.8, 1.2, 0.85))
            a.sphere(c + Vector((0, -0.09, 0.06)), 0.045, 8, 5, "black")
            a.cyl(c + Vector((0, -0.13, 0.06)), c + Vector((0, -0.19, 0.05)), 0.014, 0.0, segs=4, color="orange")
            a.sphere(c + Vector((0.025, -0.115, 0.075)), 0.009, 4, 3, "white")
            a.box(c + Vector((0, 0.1, 0.0)), (0.06, 0.1, 0.02), "black", rot=(0.3, 0, 0))
        parts["arm" + side] = a

    if extras:
        extras(parts)
    _finish(parts, [("body", prefix + "_Body", (0, 0, 0.8), None),
                    ("head", prefix + "_Head", (0, 0, 1.58), "body"),
                    ("armL", prefix + "_ArmL", (0.16, 0, 1.45), "body"),
                    ("armR", prefix + "_ArmR", (-0.16, 0, 1.45), "body")], col, root, scale)
    return root


# ================================================================ shadow
def build_shadow(col, location=(0, 0, 0), name="Enemy_Shadow", scale=1.0, seed=41, extras=None):
    """A hunched shape of dark smoke, ~1.9 m, trailing into wisps instead of legs; long clawed arms,
    glowing eyes and grin (lamp / window_lit: they light up at dusk). Upward faces get an indigo sheen.
    Parts: Body -> Head, ArmL/R. The smoke itself is VFX in Unity."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.split("_", 1)[1] if "_" in name else name
    root = L.empty_root(name, col, location, size=0.4 * scale)
    parts = {}

    def sheen(f):
        return "indigo" if f.normal.z > 0.45 else "black"

    b = Builder()
    lean = Matrix.Rotation(0.15, 4, "X")                                                  # top leans forward (-Y)
    body = Lathe([(0.0, 0.15), (0.07, 0.35), (0.16, 0.7), (0.23, 1.05), (0.28, 1.35), (0.3, 1.5),
                  (0.2, 1.6), (0.0, 1.64)], 12, lean @ Matrix.Diagonal((1.0, 0.72, 1.0, 1.0)))
    b.lathe(body, sheen)
    for k in range(10):                                                                   # wisps
        a = rnd.uniform(0, 2 * math.pi)
        z = rnd.uniform(0.3, 1.0)
        s = body.s_at_z(z)
        p = body.point(a, s, -0.02)
        out = Vector((math.cos(a), math.sin(a) * 0.7, 0)).normalized()
        tip = p + out * rnd.uniform(0.15, 0.3) + Vector((0, 0.1, -rnd.uniform(0.15, 0.35)))
        b.cyl(p, tip, rnd.uniform(0.04, 0.07), 0.0, segs=5, color=rnd.choice(["black", "navy"]))
    for k in range(3):                                                                    # trailing tail
        p = Vector((rnd.uniform(-0.05, 0.05), 0.02, 0.3))
        b.cyl(p, p + Vector((rnd.uniform(-0.15, 0.15), 0.25, -0.28)), 0.05, 0.0, segs=5, color="black")
    parts["body"] = b

    h = Builder()
    hz, hy = 1.74, -0.3                                     # head hangs forward, above the leaning shoulders
    head = Lathe([(0.0, 1.58), (0.1, 1.6), (0.15, 1.68), (0.155, 1.76), (0.13, 1.84), (0.07, 1.89), (0.0, 1.9)], 10,
                 Matrix.Translation((0, hy, 0)))
    h.lathe(head, sheen)
    for sx in (-1, 1):                                                                    # eyes, slanted
        h.sphere((sx * 0.06, hy - 0.14, hz + 0.02), 0.03, 8, 5, "lamp", scale=(1.5, 0.6, 0.7),
                 rot=(0, sx * 0.35, 0))
    for k in range(7):                                                                    # jagged grin
        x = (k - 3) * 0.028
        z = hz - 0.06 + 0.012 * abs(k - 3) ** 1.2
        tooth = [(-0.014, 0.0), (0.014, 0.0), (0.0, -0.035)] if k % 2 == 0 else [(-0.014, 0.0), (0.014, 0.0), (0.0, 0.03)]
        h.prism(ccw(tooth), 0.01, "window_lit",
                matrix=Matrix.Translation((x, hy - 0.142 + abs(k - 3) * 0.012, z)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    for k in range(6):                                                                    # spiky smoke on the head
        a = FRONT + math.pi * (0.3 + 0.28 * k)
        p = Vector((0.1 * math.cos(a), hy + 0.1 * math.sin(a), hz + 0.1))
        h.cyl(p, p + Vector((0.12 * math.cos(a), 0.12 * math.sin(a) + 0.08, 0.18)), 0.04, 0.0, segs=5, color="black")
    parts["head"] = h

    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.27, -0.2, 1.42))
        elbow = Vector((sgn * 0.42, -0.36, 1.08))
        hand = Vector((sgn * 0.46, -0.58, 0.8))
        a.sphere(sh, 0.08, 8, 5, "black")
        a.cyl(sh, elbow, 0.07, 0.05, segs=7, color=sheen)
        a.cyl(elbow, hand, 0.05, 0.035, segs=7, color=sheen)
        for k in range(4):                                                                # claws
            spread = (k - 1.5) * 0.35
            tip = hand + Vector((sgn * 0.05 * spread, -0.16, -0.14 + 0.03 * abs(spread)))
            a.cyl(hand, tip, 0.022, 0.0, segs=4, color="navy" if k % 2 else "black")
        parts["arm" + side] = a

    if extras:
        extras(parts)
    _finish(parts, [("body", prefix + "_Body", (0, 0, 0.9), None),
                    ("head", prefix + "_Head", (0, -0.26, 1.58), "body"),
                    ("armL", prefix + "_ArmL", (0.27, -0.2, 1.42), "body"),
                    ("armR", prefix + "_ArmR", (-0.27, -0.2, 1.42), "body")], col, root, scale)
    return root


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    y = -16.0
    return [
        build_mannequin(col, (0.0, y, 0.0)),
        build_scarecrow(col, (2.2, y, 0.0)),
        build_shadow(col, (4.4, y, 0.0)),
    ]

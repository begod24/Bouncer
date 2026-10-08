"""New day toys for the yard: plush bear (tank), wind-up tin chick (kamikaze), spinning top, rocking horse.
Same rules as ba_enemies: rigid parts parented under an empty root, pivots at the joints, procedural
animation in code. Characters face -Y; character left = +X."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2


def _empty(name, col, parent, location, size=0.08):
    """Attachment point (e.g. where a stuck ball sits). Exported to Unity as an empty child transform."""
    import bpy
    ob = bpy.data.objects.get(name)
    if ob is not None:
        bpy.data.objects.remove(ob, do_unlink=True)
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "SPHERE"
    ob.empty_display_size = size
    col.objects.link(ob)
    ob.parent = parent
    ob.location = Vector(location) - Vector(parent.get("pivot", (0, 0, 0)))
    ob["pivot"] = tuple(location)
    return ob


def _sphere_profile(r, z, rings=8, squash=1.0):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((0.0 if i in (0, rings) else r * math.cos(a), z + r * squash * math.sin(a)))
    return prof


def _star2d(r_out, r_in, n=5, rot=math.pi / 2):
    pts = []
    for k in range(2 * n):
        a = rot + k * math.pi / n
        r = r_out if k % 2 == 0 else r_in
        pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


# ================================================================ plush bear (tank)
BEAR_FUR, BEAR_DARK, BEAR_LIGHT = "wood_light", "wood", "sand"


def build_bear(col, location=(0, 0, 0), name="Enemy_Bear", seed=5, fur=BEAR_FUR, dark=BEAR_DARK,
               light=BEAR_LIGHT, extras=None):
    """Worn plush teddy ~1.8 m: mismatched button eyes, stitched seams, a checked patch, a red bow and a
    torn hole in the belly where balls get stuck (empty Bear_BallSocket). Parts: Body -> Head, ArmL/R, LegL/R.
    extras(parts) can add details to the builders before they are turned into objects (elite version)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.5)
    parts = {}

    # ---------- body: pear-shaped torso, belly, patch, torn hole, bow
    b = Builder()
    torso = Lathe([(0.0, 0.4), (0.3, 0.42), (0.41, 0.53), (0.45, 0.7), (0.42, 0.88), (0.35, 1.02),
                   (0.25, 1.12), (0.16, 1.18), (0.0, 1.2)], 14, Matrix.Diagonal((1.0, 0.86, 1.0, 1.0)))
    b.lathe(torso, fur)
    parts["torso_lathe"] = torso
    s_belly = torso.s_at_z(0.72)
    b.decal_disc(torso, FRONT, s_belly, 0.27, 0.3, light, segs=14, off=0.004)
    # the torn hole where a ball gets stuck, stuffing around its edge
    s_hole = torso.s_at_z(0.74)
    b.decal_disc(torso, FRONT, s_hole, 0.13, 0.13, "brown", segs=12, off=0.008)
    b.decal_disc(torso, FRONT, s_hole, 0.1, 0.1, "leather_dark", segs=10, off=0.011)
    for k in range(7):
        a = k * 2 * math.pi / 7 + 0.3
        p = torso.point(FRONT + 0.14 * math.cos(a) / 0.45, s_hole + 0.14 * math.sin(a), 0.01)
        b.ico(p, rnd.uniform(0.035, 0.05), "cream", subdiv=1, jitter=0.01, seed=seed + k)
    # stitched seam down the middle and across the sides
    b.decal_strip(torso, [(FRONT, torso.s_at_z(1.16)), (FRONT, torso.s_at_z(0.9))], 0.014, dark, off=0.006)
    b.decal_strip(torso, [(FRONT, torso.s_at_z(0.57)), (FRONT, torso.s_at_z(0.45))], 0.014, dark, off=0.006)
    for side in (-1, 1):
        b.decal_strip(torso, [(FRONT + side * math.pi / 2 + 0.2 * side, torso.s_at_z(z))
                              for z in (1.1, 0.9, 0.7, 0.5)], 0.012, dark, off=0.005)
    # checked patch on the right side of the tummy, with stitches round it
    phi_p, s_p = FRONT - 0.95, torso.s_at_z(0.6)
    b.decal_disc(torso, phi_p, s_p, 0.13, 0.13, "blue", segs=4, off=0.006, rot=math.pi / 4)
    for d in (-0.045, 0.045):
        b.decal_strip(torso, [(phi_p + d / 0.45, s_p - 0.08), (phi_p + d / 0.45, s_p + 0.08)], 0.018, "white", off=0.009)
        b.decal_strip(torso, [(phi_p - 0.08 / 0.45, s_p + d), (phi_p + 0.08 / 0.45, s_p + d)], 0.018, "white", off=0.01)
    # red bow at the neck
    for sx in (-1, 1):
        b.prism([(0, 0), (0.17, -0.08), (0.17, 0.08)] if sx > 0 else [(0, 0), (-0.17, 0.08), (-0.17, -0.08)],
                0.05, "red", matrix=L.frame((0, -0.2, 1.14), (math.pi / 2, 0, 0)))
    b.sphere((0, -0.235, 1.14), 0.05, 8, 5, "red_dark", scale=(1, 0.8, 1))
    b.sphere((0, 0.37, 0.52), 0.09, 8, 5, fur)                                               # tail
    parts["body"] = b

    # ---------- head: round head, muzzle, mismatched button eyes, ears
    h = Builder()
    hz = 1.44
    head = Lathe(_sphere_profile(0.29, hz, 8, 0.92), 14)
    h.lathe(head, fur)
    parts["head_lathe"] = head
    h.decal_strip(head, [(FRONT, head.s_at_z(hz + 0.12)), (FRONT, head.s_at_z(hz + 0.245))], 0.014, dark, off=0.006)
    h.decal_strip(head, [(FRONT + math.pi, head.s_at_z(hz + 0.245)), (FRONT + math.pi, head.s_at_z(hz - 0.15))],
                  0.014, dark, off=0.006)
    muzzle = Lathe(_sphere_profile(0.14, 0.0, 6, 0.85), 12,
                   Matrix.Translation((0, -0.24, hz - 0.08)) @ Matrix.Rotation(math.pi / 2, 4, "X")
                   @ Matrix.Diagonal((1.15, 0.9, 1.0, 1.0)))
    h.lathe(muzzle, light)
    h.sphere((0, -0.36, hz - 0.035), 0.05, 8, 5, "black", scale=(1.3, 0.8, 0.8))            # nose
    h.beam((0, -0.362, hz - 0.075), (0, -0.352, hz - 0.13), 0.014, 0.014, "black")           # mouth
    for sx in (-1, 1):
        h.beam((0, -0.352, hz - 0.13), (sx * 0.06, -0.333, hz - 0.15), 0.014, 0.014, "black")
    # eyes: a small black button and a bigger blue one sewn on instead of the lost eye
    h.cyl((0.1, -0.24, hz + 0.07), (0.11, -0.3, hz + 0.075), 0.045, 0.045, segs=8, color="black")
    h.cyl((-0.11, -0.235, hz + 0.08), (-0.12, -0.3, hz + 0.085), 0.06, 0.06, segs=8,
          color=lambda f: "blue" if f.normal.y < -0.8 else "navy")
    for dx in (-0.018, 0.018):
        h.box((-0.12 + dx, -0.303, hz + 0.085), (0.014, 0.008, 0.014), "navy")
    h.box((-0.12, -0.305, hz + 0.085), (0.036, 0.004, 0.008), "cream")                           # thread
    for sx in (-1, 1):                                                                         # ears
        h.sphere((sx * 0.22, 0.02, hz + 0.22), 0.12, 10, 6, fur, scale=(1.0, 0.5, 0.95))
        h.sphere((sx * 0.215, -0.03, hz + 0.21), 0.075, 8, 5, light, scale=(1.0, 0.3, 0.9))
    parts["head"] = h

    # ---------- arms (pivot in the shoulder) and legs (pivot in the hip)
    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.34, -0.02, 1.02))
        hand = Vector((sgn * 0.52, -0.2, 0.66))
        a.cyl(sh, hand, 0.12, 0.11, segs=10, color=fur)
        a.sphere(sh, 0.12, 10, 6, fur)
        a.sphere(hand, 0.125, 10, 6, fur)
        a.sphere(hand + Vector((0, -0.105, -0.03)), 0.075, 8, 5, light, scale=(1.0, 0.45, 1.0))   # paw pad
        parts["arm" + side] = (a, sh)
        g = Builder()
        hip = Vector((sgn * 0.21, 0.0, 0.5))
        g.cyl(hip, (sgn * 0.23, -0.02, 0.14), 0.16, 0.15, segs=10, color=fur)
        g.sphere((sgn * 0.23, -0.1, 0.13), 0.16, 10, 6, fur, scale=(1.0, 1.35, 0.8))
        g.sphere((sgn * 0.23, -0.3, 0.14), 0.1, 8, 5, light, scale=(1.0, 0.35, 0.9))            # sole pad
        parts["leg" + side] = (g, hip)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.55), parent=root)
    parts["head"].to_object(prefix + "_Head", col, pivot=(0, 0, 1.2), parent=body_ob)
    for side in ("L", "R"):
        bld, pv = parts["arm" + side]
        bld.to_object(prefix + "_Arm" + side, col, pivot=pv, parent=body_ob)
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "_Leg" + side, col, pivot=pv, parent=body_ob)
    _empty(prefix + "_BallSocket", col, body_ob, (0, -0.44, 0.74))
    return root


# ================================================================ wind-up tin chick (kamikaze)
def build_chick(col, location=(0, 0, 0), name="Enemy_Chick", seed=9, main="yellow", wing="orange",
                beak="orange", legs=None, extras=None):
    """Tin wind-up chick ~0.8 m: two tin halves joined by a seam, painted wings, a big wind-up key on
    the back (Chick_Key spins round its axle, +Y). Parts: Body -> Head, Key, LegL/R. The parts double as
    the debris of the explosion."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    parts = {}

    b = Builder()
    bz = 0.4
    body = Lathe(_sphere_profile(0.26, bz, 8, 0.9), 14, Matrix.Diagonal((0.92, 1.05, 1.0, 1.0)))
    b.lathe(body, main)
    parts["body_lathe"] = body
    b.decal_strip(body, [(FRONT + 2 * math.pi * i / 16, body.s_at_z(bz)) for i in range(17)], 0.018, "ochre", off=0.004)
    for side in (-1, 1):                                                                 # painted wings
        phi = FRONT + side * 1.75
        b.decal_disc(body, phi, body.s_at_z(bz + 0.03), 0.16, 0.11, wing, segs=12, off=0.006, rot=side * 0.35)
        for k in range(3):
            b.decal_strip(body, [(phi + side * (0.05 - 0.2 * k), body.s_at_z(bz + 0.1)),
                                 (phi + side * (0.2 - 0.2 * k), body.s_at_z(bz - 0.04))], 0.014, "red", off=0.01)
    # tail feathers: three tin flaps at the back
    for k, (dx, tilt) in enumerate(((-0.07, -0.35), (0.0, 0.0), (0.07, 0.35))):
        b.prism([(-0.04, 0.0), (0.04, 0.0), (0.0, 0.16)], 0.02, main if k != 1 else wing,
                matrix=L.frame((dx, 0.24, bz + 0.1), (0.9, 0, tilt)))
    for _ in range(6):                                                                   # rust
        b.decal_disc(body, rnd.uniform(0, 2 * math.pi), rnd.uniform(0.1, 0.7) * body.s[-1],
                     rnd.uniform(0.012, 0.03), rnd.uniform(0.01, 0.025), "rust", segs=5, off=0.005,
                     rot=rnd.uniform(0, 3))
    b.cyl((0, 0.22, bz - 0.03), (0, 0.3, bz - 0.03), 0.03, 0.03, segs=6, color="tin_dark")      # key socket
    parts["body"] = b

    h = Builder()
    hz = 0.72
    head = Lathe(_sphere_profile(0.17, hz, 8, 0.95), 12, Matrix.Translation((0, -0.08, 0)))
    h.lathe(head, main)
    parts["head_lathe"] = head
    rf = 0.17
    for sx in (-1, 1):
        h.decal_disc(head, FRONT + sx * 0.075 / rf, head.s_at_z(hz + 0.04), 0.034, 0.04, "white", segs=8, off=0.004)
        h.decal_disc(head, FRONT + sx * 0.08 / rf, head.s_at_z(hz + 0.035), 0.022, 0.028, "black", segs=8, off=0.008)
        h.decal_disc(head, FRONT + sx * 0.075 / rf - sx * 0.008 / rf, head.s_at_z(hz + 0.05), 0.007, 0.007,
                     "white", segs=5, off=0.012)
        h.decal_disc(head, FRONT + sx * 0.13 / rf, head.s_at_z(hz - 0.03), 0.026, 0.018, "pink", segs=7, off=0.004)
    beak_lathe = Lathe([(0.075, 0.0), (0.045, 0.07), (0.0, 0.13)], 6,
                 Matrix.Translation((0, -0.22, hz - 0.02)) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    h.lathe(beak_lathe, beak, cap_bottom=True)
    for k, (y, r) in enumerate(((-0.12, 0.045), (-0.06, 0.055), (0.0, 0.05), (0.05, 0.04))):   # comb
        h.sphere((0, y - 0.05, hz + 0.16 + 0.015 * math.sin(k * 1.4)), r, 8, 5, "red", scale=(0.55, 1, 1))
    parts["head"] = h

    k_ = Builder()
    axle0, axle1 = Vector((0, 0.28, bz - 0.03)), Vector((0, 0.42, bz - 0.03))
    k_.cyl(axle0, axle1, 0.022, 0.022, segs=6, color="tin")
    for sx in (-1, 1):                                                                   # butterfly handle
        k_.prism([(0, -0.035), (0.13, -0.08), (0.16, 0.0), (0.13, 0.08), (0, 0.035)] if sx > 0 else
                 [(0, 0.035), (-0.13, 0.08), (-0.16, 0.0), (-0.13, -0.08), (0, -0.035)],
                 0.025, "tin", matrix=Matrix.Translation(axle1 + Vector((0, 0.0, 0.0))) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    k_.cyl(axle1 + Vector((0, -0.01, 0)), axle1 + Vector((0, 0.03, 0)), 0.04, 0.04, segs=8, color="tin_dark")
    parts["key"] = (k_, axle0)

    legs = legs or wing
    for sgn, side in ((1, "L"), (-1, "R")):
        g = Builder()
        hip = Vector((sgn * 0.09, 0.0, 0.2))
        foot = Vector((sgn * 0.1, -0.02, 0.03))
        g.cyl(hip, foot, 0.022, 0.02, segs=6, color=legs)
        g.sphere(hip, 0.035, 6, 4, legs)
        for a in (-0.5, 0.0, 0.5):                                                       # three toes
            tip = foot + Vector((math.sin(a) * 0.1, -math.cos(a) * 0.1, -0.01))
            g.beam(foot, tip, 0.03, 0.02, legs)
        parts["leg" + side] = (g, hip)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, bz), parent=root)
    parts["head"].to_object(prefix + "_Head", col, pivot=(0, -0.08, 0.6), parent=body_ob)
    bld, pv = parts["key"]
    bld.to_object(prefix + "_Key", col, pivot=pv, parent=body_ob)
    for side in ("L", "R"):
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "_Leg" + side, col, pivot=pv, parent=body_ob)
    return root


# ================================================================ spinning top
TOP_PROFILE = [(0.0, 0.0), (0.045, 0.035), (0.13, 0.11), (0.34, 0.3), (0.49, 0.44), (0.52, 0.5),
               (0.5, 0.56), (0.41, 0.67), (0.27, 0.77), (0.12, 0.84), (0.0, 0.86)]


def build_top(col, location=(0, 0, 0), name="Enemy_Top", colors=("red", "white", "blue", "yellow"),
              extras=None):
    """Tin spinning top ~1.05 m wide, 1.25 m with the plunger: red lower cone, a yellow belt with a tin
    rim, the upper cone painted in wedges, stars. Top_Body spins round +Z about the tip at the origin;
    Top_Handle (plunger rod + knob) can pump up and down."""
    L.remove_tree(name)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.4)
    lower, wedge_a, wedge_b, belt = colors
    parts = {}

    b = Builder()
    top = Lathe(TOP_PROFILE, 16)

    def paint(f):
        c = f.calc_center_median()
        if c.z < 0.06:
            return "tin_dark"
        if c.z < 0.46:
            return lower
        if c.z < 0.56:
            return belt
        k = int(((math.atan2(c.y, c.x) - top.phase) % (2 * math.pi)) / top.step)
        return wedge_a if (k // 2) % 2 == 0 else wedge_b
    b.lathe(top, paint)
    parts["lathe"] = top
    b.cyl((0, 0, 0.49), (0, 0, 0.53), 0.53, 0.53, segs=16, color="tin")                      # rim seam
    for k in range(4):                                                                    # stars on the lower cone
        phi = FRONT + k * math.pi / 2
        s = top.s_at_z(0.3)
        _, _, nr, nz = top.at(s)
        c = top.point(phi, s, 0.004)
        m = _surface_frame(c, Vector((nr * math.cos(phi), nr * math.sin(phi), nz)))
        b.prism(_star2d(0.075, 0.032), 0.012, "yellow" if lower != "yellow" else "white", matrix=m)
    parts["body"] = b

    h = Builder()
    h.cyl((0, 0, 0.8), (0, 0, 1.12), 0.03, 0.03, segs=6, color="tin")
    for z in (0.9, 0.96, 1.02, 1.08):                                                    # spiral grooves
        h.cyl((0, 0, z), (0, 0, z + 0.018), 0.036, 0.036, segs=6, color="tin_dark")
    h.sphere((0, 0, 1.17), 0.075, 10, 6, "red_dark" if wedge_a == "red" else "red")
    parts["handle"] = h

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0), parent=root)
    parts["handle"].to_object(prefix + "_Handle", col, pivot=(0, 0, 0.86), parent=body_ob)
    return root


def _surface_frame(center, normal):
    """Local frame on a surface point: local Z = normal, local Y as close to world up as possible."""
    n = Vector(normal).normalized()
    up = Vector((0, 0, 1))
    x = up.cross(n)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    y = n.cross(x)
    m = Matrix((x, y, n)).transposed().to_4x4()
    m.translation = center
    return m


# ================================================================ rocking horse
def _arc(y0, y1, radius, n):
    """Points of a rocker arc lying on the ground at y = 0: z = R - sqrt(R^2 - y^2)."""
    pts = []
    for k in range(n + 1):
        y = y0 + (y1 - y0) * k / n
        pts.append((y, radius - math.sqrt(radius * radius - y * y)))
    return pts


def build_rocking_horse(col, location=(0, 0, 0), name="Enemy_RockingHorse", coat="white", spots="grey_light",
                        mane="wood_dark", saddle="red", rocker="red", muzzle="grey_light", extras=None):
    """Painted rocking horse ~1.5 m long on two red rockers. One rigid part (Horse_Body) with the pivot
    at the middle of the rockers on the ground: rocking = rotation round X, the rockers roll on the arc."""
    L.remove_tree(name)
    rnd = random.Random(17)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.4)
    b = Builder()
    R = 1.7
    # rockers with a stripe, a footboard between them and scrolls at the ends
    for sx in (-1, 1):
        pts = _arc(-0.78, 0.78, R, 10)
        for (ya, za), (yb, zb) in zip(pts, pts[1:]):
            b.beam((sx * 0.24, ya, za + 0.045), (sx * 0.24, yb, zb + 0.045), 0.06, 0.09, rocker)
            b.beam((sx * (0.24 + 0.031), ya, za + 0.05), (sx * (0.24 + 0.031), yb, zb + 0.05), 0.004, 0.025, "yellow")
        for y in (-0.8, 0.8):
            z = R - math.sqrt(R * R - 0.78 * 0.78) + 0.07
            b.cyl((sx * 0.21, y, z), (sx * 0.27, y, z), 0.06, 0.06, segs=8, color=rocker)
    for y in (-0.42, 0.42):
        z = R - math.sqrt(R * R - y * y) + 0.1
        b.beam((-0.24, y, z), (0.24, y, z), 0.05, 0.04, "wood")
    # legs splayed front and back, hooves on the rockers
    for sx in (-1, 1):
        for y_top, y_bot in ((-0.24, -0.5), (0.26, 0.52)):
            z_bot = R - math.sqrt(R * R - y_bot * y_bot) + 0.1
            top = Vector((sx * 0.12, y_top, 0.62))
            bot = Vector((sx * 0.22, y_bot, z_bot + 0.06))
            b.cyl(top, bot, 0.06, 0.05, segs=8, color=coat)
            b.cyl(bot, bot + Vector((0, 0, -0.08)), 0.058, 0.062, segs=8, color="black")
    # body along Y
    body = Lathe([(0.0, -0.44), (0.12, -0.42), (0.2, -0.33), (0.23, -0.15), (0.235, 0.05), (0.22, 0.25),
                  (0.17, 0.39), (0.08, 0.45), (0.0, 0.46)], 12,
                 Matrix.Translation((0, 0, 0.72)) @ Matrix.Rotation(-math.pi / 2, 4, "X"))
    b.lathe(body, coat)
    for _ in range(9):                                                                      # dapples
        b.decal_disc(body, rnd.uniform(-math.pi * 0.9, math.pi * 0.9) + math.pi / 2, rnd.uniform(0.15, 0.75) * body.s[-1],
                     rnd.uniform(0.035, 0.06), rnd.uniform(0.03, 0.05), spots, segs=7, off=0.005, rot=rnd.uniform(0, 3))
    # neck and head
    b.cyl((0, -0.3, 0.8), (0, -0.46, 1.12), 0.13, 0.1, segs=10, color=coat)
    b.sphere((0, -0.47, 1.14), 0.11, 10, 6, coat)
    b.cyl((0, -0.48, 1.16), (0, -0.72, 1.02), 0.1, 0.075, segs=10, color=coat)
    b.sphere((0, -0.73, 1.02), 0.08, 10, 6, muzzle, scale=(1.0, 1.1, 0.9))
    for sx in (-1, 1):
        b.sphere((sx * 0.083, -0.56, 1.16), 0.036, 8, 5, "black")                               # eyes
        b.sphere((sx * 0.095, -0.575, 1.175), 0.012, 4, 3, "white")
        b.cyl((sx * 0.035, -0.8, 1.0), (sx * 0.035, -0.81, 1.0), 0.014, 0.014, segs=5, color="black")   # nostrils
        b.lathe(Lathe([(0.035, 0.0), (0.0, 0.1)], 5, Matrix.Translation((sx * 0.055, -0.44, 1.21))
                      @ Matrix.Rotation(0.25 * sx, 4, "Y") @ Matrix.Rotation(-0.25, 4, "X")), coat)      # ears
    # bridle and the handle peg through the head
    b.cyl((0, -0.64, 1.05), (0, -0.66, 1.04), 0.098, 0.098, segs=10, color=saddle)
    b.cyl((-0.16, -0.52, 1.13), (0.16, -0.52, 1.13), 0.022, 0.022, segs=6, color="wood")
    for sx in (-1, 1):
        b.sphere((sx * 0.16, -0.52, 1.13), 0.035, 6, 4, "wood_dark")
    # mane along the neck, forelock, tail
    back = Vector((0, 0.894, 0.447))                   # perpendicular to the neck axis, up and back
    for k in range(8):
        t = 0.02 + k / 7
        axis = Vector((0, -0.3 - 0.16 * t, 0.8 + 0.32 * t))
        p = axis + back * (0.12 - 0.03 * t)
        b.box(p, (0.05, 0.07, 0.12), mane, rot=(-0.6, 0, 0))
    b.box((0, -0.55, 1.24), (0.06, 0.06, 0.1), mane, rot=(0.5, 0, 0))
    for k, dx in enumerate((-0.04, 0.0, 0.04)):
        b.cyl((dx, 0.42, 0.8), (dx * 2.2, 0.66, 0.5 - 0.04 * k), 0.035, 0.015, segs=6, color=mane)
    # saddle on a blanket, with stirrups
    b.box((0, 0.02, 0.945), (0.34, 0.36, 0.02), "blue")
    b.box((0, 0.02, 0.97), (0.26, 0.26, 0.05), saddle)
    b.box((0, -0.11, 1.0), (0.12, 0.05, 0.06), saddle)
    b.box((0, 0.02, 0.972), (0.27, 0.27, 0.01), "yellow")
    for sx in (-1, 1):
        b.beam((sx * 0.2, 0.02, 0.93), (sx * 0.22, 0.02, 0.72), 0.015, 0.02, "leather_dark")
        b.box((sx * 0.22, 0.02, 0.7), (0.03, 0.08, 0.02), "tin")
    if extras:
        extras(b)
    b.to_object(prefix + "_Body", col, pivot=(0, 0, 0), parent=root)
    return root


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    return [
        build_bear(col, (0.0, -8.0, 0.0)),
        build_chick(col, (1.6, -8.0, 0.0)),
        build_top(col, (3.0, -8.0, 0.0)),
        build_rocking_horse(col, (5.0, -8.0, 0.0)),
    ]

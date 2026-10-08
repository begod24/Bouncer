"""Enemies: Roly-Poly (+ boss variant), Pupsik, Tin Soldier."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2


# ================================================================ Roly-Poly
ROLY_BODY = [(0.0, 0.0), (0.2, 0.025), (0.34, 0.1), (0.425, 0.22), (0.455, 0.36), (0.45, 0.48),
             (0.415, 0.6), (0.35, 0.7), (0.27, 0.78), (0.225, 0.83)]
ROLY_HEAD = [(0.225, 0.83), (0.265, 0.87), (0.298, 0.95), (0.312, 1.04), (0.302, 1.13),
             (0.266, 1.21), (0.2, 1.28), (0.11, 1.325), (0.0, 1.34)]


def _crack(b, lat, rnd, s0, phi0, steps, step_len, width, color):
    pts = [(phi0, s0)]
    ang = rnd.uniform(0, 2 * math.pi)
    for _ in range(steps):
        ang += rnd.uniform(-0.9, 0.9)
        phi, s = pts[-1]
        r = lat.radius_at(s)
        s2 = min(lat.s[-1] * 0.93, max(lat.s[-1] * 0.05, s + math.sin(ang) * step_len))
        pts.append((phi + math.cos(ang) * step_len / r, s2))
    b.decal_strip(lat, pts, width, color, off=0.006)


def build_roly_poly(col, location=(0, 0, 0), name="Enemy_RolyPoly", boss=False, scale=1.0, seed=3):
    L.remove_tree(name)
    rnd = random.Random(seed)
    root = L.empty_root(name, col, location, size=0.4 * scale)
    S = Matrix.Diagonal((scale, scale, scale, 1.0))

    # ---------- body (bottom half, weighted)
    b = Builder()
    body = Lathe(ROLY_BODY, 16, S)
    b.lathe(body, "red", cap_bottom=False, cap_top=True)
    s_ap = body.s_at_z(0.42)
    b.decal_disc(body, FRONT, s_ap, 0.25, 0.27, "white", segs=14, off=0.004)
    # flower on the apron
    s_fl = body.s_at_z(0.46)
    r_fl = body.radius_at(s_fl)
    for i in range(5):
        a = math.pi / 2 + 2 * math.pi * i / 5
        du, ds = 0.062 * math.cos(a), 0.062 * math.sin(a)
        b.decal_disc(body, FRONT + du / r_fl, s_fl + ds, 0.042, 0.042, "red_light" if i % 2 else "pink",
                     segs=7, off=0.008, rot=a)
    b.decal_disc(body, FRONT, s_fl, 0.036, 0.036, "yellow", segs=7, off=0.012)
    for sgn in (-1, 1):
        b.decal_disc(body, FRONT + sgn * 0.085 / r_fl, s_fl - 0.12, 0.05, 0.022, "green", segs=6,
                     off=0.008, rot=sgn * 0.5)
    b.decal_strip(body, [(FRONT, s_fl - 0.05), (FRONT + 0.01, s_fl - 0.16)], 0.016, "green_dark", off=0.007)
    if boss:
        b.decal_strip(body, [(FRONT + 0.55, body.s_at_z(0.78)), (FRONT + 0.42, body.s_at_z(0.7)),
                             (FRONT + 0.47, body.s_at_z(0.63)), (FRONT + 0.3, body.s_at_z(0.56)),
                             (FRONT + 0.34, body.s_at_z(0.5))], 0.016, "maroon", off=0.009)
        for _ in range(7):
            _crack(b, body, rnd, rnd.uniform(0.15, 0.95), rnd.uniform(0, 2 * math.pi), 7, 0.07, 0.014, "maroon")
        for _ in range(14):
            s = rnd.uniform(0.2, 1.0)
            b.decal_disc(body, rnd.uniform(0, 2 * math.pi), s, rnd.uniform(0.02, 0.045),
                         rnd.uniform(0.015, 0.035), "cream", segs=5, off=0.005, rot=rnd.uniform(0, 3))
    body_ob = b.to_object(name.replace("Enemy_", "").replace("Boss_", "") + "_Body", col,
                          pivot=(0, 0, 0.45 * scale), parent=root)

    # ---------- head (top half, kerchief + painted face)
    h = Builder()
    head = Lathe(ROLY_HEAD, 16, S)
    h.lathe(head, "red", cap_bottom=True, cap_top=False)
    s_face = head.s_at_z(1.03)
    r_face = head.radius_at(s_face)
    h.decal_disc(head, FRONT, s_face, 0.205, 0.2, "skin_light", segs=14, off=0.004)
    # fringe of hair under the kerchief
    s_hair = head.s_at_z(1.175)
    h.decal_strip(head, [(FRONT - 0.42, s_hair - 0.035), (FRONT - 0.2, s_hair - 0.005),
                         (FRONT, s_hair + 0.005), (FRONT + 0.2, s_hair - 0.005),
                         (FRONT + 0.42, s_hair - 0.035)], 0.05, "ochre", off=0.008)
    # eyes: big, staring (small pupils) = slightly creepy
    s_eye = head.s_at_z(1.06)
    for sgn in (-1, 1):
        ph = FRONT + sgn * 0.088 / r_face
        h.decal_disc(head, ph, s_eye, 0.054, 0.064, "white", segs=10, off=0.008)
        h.decal_disc(head, ph, s_eye - 0.004, 0.037, 0.045, "eye_blue", segs=10, off=0.012)
        h.decal_disc(head, ph, s_eye - 0.004, 0.014, 0.016, "black", segs=6, off=0.016)
        h.decal_disc(head, ph + sgn * 0.3 * 0.03 / r_face - 0.012 / r_face, s_eye + 0.018, 0.008, 0.008,
                     "white", segs=5, off=0.02)
        # lashes / brows
        if boss:   # angry brows: inner ends pulled down
            h.decal_strip(head, [(ph - sgn * 0.065 / r_face, s_eye + 0.05), (ph, s_eye + 0.08),
                                 (ph + sgn * 0.06 / r_face, s_eye + 0.095)], 0.018, "black", off=0.01)
        else:
            h.decal_strip(head, [(ph - sgn * 0.06 / r_face, s_eye + 0.07), (ph, s_eye + 0.088),
                                 (ph + sgn * 0.055 / r_face, s_eye + 0.078)], 0.012, "brown", off=0.01)
        # cheeks
        h.decal_disc(head, FRONT + sgn * 0.128 / r_face, s_eye - 0.088, 0.036, 0.028, "pink", segs=8, off=0.008)
    # smile
    s_m = head.s_at_z(0.955)
    w = 0.075 if boss else 0.05
    pts = []
    for i in range(7):
        t = -1 + 2 * i / 6
        pts.append((FRONT + t * w / r_face, s_m + 0.02 * t * t - (0.006 if boss else 0.0)))
    h.decal_strip(head, pts, 0.026 if boss else 0.018, "lips", off=0.008)
    # polka dots on the kerchief (kept away from the face)
    for z, count, start in ((0.93, 7, 0.0), (1.09, 6, 0.5), (1.23, 6, 0.0)):
        s = head.s_at_z(z)
        for k in range(count):
            ang = math.pi + (k - (count - 1) / 2) * (2 * math.pi * 0.62 / count) + start * 0.3
            phi = FRONT + ang
            rel = math.atan2(math.sin(phi - FRONT), math.cos(phi - FRONT))
            if z < 1.2 and abs(rel) < math.radians(62):
                continue
            h.decal_disc(head, phi, s, 0.03, 0.03, "white", segs=7, off=0.005)
    if boss:
        h.decal_strip(head, [(FRONT - 0.75, head.s_at_z(1.3)), (FRONT - 0.55, head.s_at_z(1.25)),
                             (FRONT - 0.6, head.s_at_z(1.2)), (FRONT - 0.42, head.s_at_z(1.16))],
                      0.014, "maroon", off=0.01)
        for _ in range(4):
            _crack(h, head, rnd, rnd.uniform(0.15, 0.4), FRONT + math.pi + rnd.uniform(-1.8, 1.8), 6, 0.05,
                   0.012, "maroon")
        for _ in range(6):
            h.decal_disc(head, FRONT + math.pi + rnd.uniform(-1.6, 1.6), rnd.uniform(0.1, 0.45),
                         rnd.uniform(0.015, 0.03), rnd.uniform(0.012, 0.025), "cream", segs=5, off=0.006)
    head_ob = h.to_object(name.replace("Enemy_", "").replace("Boss_", "") + "_Head", col,
                          pivot=(0, 0, 1.05 * scale), parent=root)
    return root


# ================================================================ Pupsik
def build_pupsik(col, location=(0, 0, 0), name="Enemy_Pupsik", scale=1.3):
    """~0.8 m tall: modelled at 0.62 m, scaled up so the swarm reads from the game camera."""
    L.remove_tree(name)
    root = L.empty_root(name, col, location, size=0.25)
    skin, dark = "rubber", "rubber_dark"

    # ---------- body = torso + head
    b = Builder()
    torso = Lathe([(0.0, 0.13), (0.1, 0.145), (0.135, 0.19), (0.142, 0.24), (0.13, 0.29),
                   (0.105, 0.33), (0.07, 0.355), (0.0, 0.37)], 12)
    b.lathe(torso, skin)
    b.decal_disc(torso, FRONT, torso.s_at_z(0.215), 0.012, 0.012, dark, segs=5, off=0.003)  # belly button
    hc = 0.465
    prof = []
    for i in range(9):
        a = -math.pi / 2 + math.pi * i / 8
        r = 0.162 * math.cos(a) * (1.04 if -0.6 < a < 0.2 else 1.0)
        prof.append((0.0 if i in (0, 8) else r, hc + 0.155 * math.sin(a)))
    head = Lathe(prof, 12)
    b.lathe(head, skin)
    rf = 0.162
    s_eye = head.s_at_z(0.475)
    for sgn, look in ((-1, 0.004), (1, 0.009)):   # pupils look slightly different ways
        ph = FRONT + sgn * 0.058 / rf
        b.decal_disc(head, ph, s_eye, 0.03, 0.034, "white", segs=8, off=0.004)
        b.decal_disc(head, ph + look / rf, s_eye - 0.003, 0.02, 0.024, "eye_blue", segs=8, off=0.007)
        b.decal_disc(head, ph + look / rf, s_eye - 0.003, 0.009, 0.01, "black", segs=6, off=0.01)
        b.decal_strip(head, [(ph - sgn * 0.03 / rf, s_eye + 0.05), (ph + sgn * 0.025 / rf, s_eye + 0.058)],
                      0.008, dark, off=0.005)
        b.decal_disc(head, FRONT + sgn * 0.088 / rf, head.s_at_z(0.418), 0.026, 0.02, "pink_light", segs=7, off=0.004)
    b.decal_disc(head, FRONT, head.s_at_z(0.385), 0.018, 0.015, "lips", segs=7, off=0.004)  # little "o" mouth
    b.decal_disc(head, FRONT, head.s_at_z(0.385), 0.008, 0.007, "maroon", segs=5, off=0.007)
    b.sphere((0, -0.162, 0.43), 0.02, 6, 4, skin, scale=(1.1, 0.8, 0.9))                   # nose
    for sgn in (-1, 1):                                                                    # ears
        b.sphere((sgn * 0.158, 0.0, 0.462), 0.036, 6, 4, skin, scale=(0.45, 0.8, 1.1))
    # painted curl on the forehead
    s_c = head.s_at_z(0.585)
    curl = []
    for i in range(9):
        t = i / 8
        a = t * 4.2
        rr = 0.028 * (1 - 0.6 * t)
        curl.append((FRONT + (0.01 + rr * math.cos(a)) / rf * 1.6, s_c + rr * math.sin(a)))
    b.decal_strip(head, curl, 0.011, "brown", off=0.005)
    body_ob = b.to_object("Pupsik_Body", col, pivot=(0, 0, 0.2), parent=root, scale=scale)

    # ---------- limbs (separate for procedural waddle)
    for sgn, side in ((1, "L"), (-1, "R")):   # Blender convention: character left = +X
        g = Builder()
        hip = Vector((sgn * 0.07, 0.0, 0.19))
        g.cyl(hip, (sgn * 0.075, -0.005, 0.05), 0.058, 0.05, segs=8, color=skin)
        g.sphere((sgn * 0.078, -0.03, 0.04), 0.058, 8, 5, skin, scale=(0.9, 1.35, 0.65))
        g.to_object("Pupsik_Leg" + side, col, pivot=hip, parent=body_ob, scale=scale)

        a = Builder()
        sh = Vector((sgn * 0.115, 0.0, 0.315))
        hand = Vector((sgn * 0.17, -0.2, 0.37))
        a.cyl(sh, hand, 0.05, 0.043, segs=8, color=skin)
        a.sphere(hand, 0.05, 8, 5, skin, scale=(1.0, 1.1, 0.9))
        a.to_object("Pupsik_Arm" + side, col, pivot=sh, parent=body_ob, scale=scale)
    return root


# ================================================================ Tin Soldier
def _chips(b, lat, rnd, n, s_lo, s_hi, color="tin"):
    for _ in range(n):
        b.decal_disc(lat, rnd.uniform(0, 2 * math.pi), rnd.uniform(s_lo, s_hi),
                     rnd.uniform(0.012, 0.026), rnd.uniform(0.01, 0.02), color, segs=5, off=0.003,
                     rot=rnd.uniform(0, 3))


def build_tin_soldier(col, location=(0, 0, 0), name="Enemy_TinSoldier", seed=11):
    L.remove_tree(name)
    rnd = random.Random(seed)
    root = L.empty_root(name, col, location, size=0.3)
    coat, trim, belts, gold = "blue", "red", "white", "gold"

    # ---------- body: coat + head + shako
    b = Builder()
    torso = Lathe([(0.0, 0.5), (0.175, 0.5), (0.168, 0.56), (0.148, 0.62), (0.152, 0.7),
                   (0.162, 0.78), (0.158, 0.85), (0.12, 0.9), (0.06, 0.915), (0.0, 0.92)], 8)
    b.lathe(torso, coat)
    s_belt = torso.s_at_z(0.615)
    loop = [(FRONT + 2 * math.pi * i / 16, s_belt) for i in range(17)]
    b.decal_strip(torso, loop, 0.045, belts, off=0.004)
    b.decal_disc(torso, FRONT, s_belt, 0.03, 0.026, gold, segs=6, off=0.008)                 # buckle
    # crossbelts (X over the chest)
    for sgn in (-1, 1):
        pts = [(FRONT + sgn * math.radians(70), torso.s_at_z(0.87)),
               (FRONT + sgn * math.radians(10), torso.s_at_z(0.75)),
               (FRONT - sgn * math.radians(45), torso.s_at_z(0.64))]
        b.decal_strip(torso, pts, 0.036, belts, off=0.005 + (0.002 if sgn > 0 else 0))
    for z in (0.69, 0.8):                                                                    # buttons
        b.decal_disc(torso, FRONT, torso.s_at_z(z), 0.013, 0.013, gold, segs=5, off=0.009)
    s_col = torso.s_at_z(0.895)
    b.decal_strip(torso, [(FRONT + 2 * math.pi * i / 12, s_col) for i in range(13)], 0.03, trim, off=0.004)
    s_hem = torso.s_at_z(0.52)
    b.decal_strip(torso, [(FRONT + 2 * math.pi * i / 16, s_hem) for i in range(17)], 0.03, trim, off=0.004)
    _chips(b, torso, rnd, 10, torso.s_at_z(0.52), torso.s_at_z(0.86))
    # epaulettes
    for sgn in (-1, 1):
        b.box((sgn * 0.155, 0, 0.885), (0.1, 0.12, 0.03), gold, rot=(0, sgn * 0.35, 0))
        b.box((sgn * 0.2, 0, 0.87), (0.02, 0.13, 0.04), gold, rot=(0, sgn * 0.35, 0))
    # head
    hz = 1.0
    prof = []
    for i in range(7):
        a = -math.pi / 2 + math.pi * i / 6
        prof.append((0.0 if i in (0, 6) else 0.095 * math.cos(a), hz + 0.1 * math.sin(a)))
    head = Lathe(prof, 8)
    b.lathe(head, "skin")
    rf = 0.095
    s_eye = head.s_at_z(1.0)
    for sgn in (-1, 1):
        b.decal_disc(head, FRONT + sgn * 0.034 / rf, s_eye, 0.011, 0.014, "black", segs=6, off=0.004)
        b.decal_disc(head, FRONT + sgn * 0.058 / rf, head.s_at_z(0.965), 0.018, 0.014, "pink", segs=6, off=0.004)
    s_m = head.s_at_z(0.955)
    b.decal_strip(head, [(FRONT - 0.05 / rf, s_m + 0.012), (FRONT - 0.02 / rf, s_m), (FRONT, s_m + 0.004),
                         (FRONT + 0.02 / rf, s_m), (FRONT + 0.05 / rf, s_m + 0.012)], 0.016, "black", off=0.005)
    b.sphere((0, -0.098, 0.975), 0.016, 5, 3, "skin")                                         # nose
    # shako
    shako = Lathe([(0.0, 1.04), (0.098, 1.04), (0.1, 1.07), (0.112, 1.29), (0.0, 1.29)], 8)
    b.lathe(shako, "black")
    b.decal_disc(shako, FRONT, shako.s_at_z(1.17), 0.045, 0.05, gold, segs=6, off=0.004)
    b.decal_strip(shako, [(FRONT + 2 * math.pi * i / 12, shako.s_at_z(1.265)) for i in range(13)], 0.025,
                  trim, off=0.004)
    _chips(b, shako, rnd, 4, shako.s_at_z(1.08), shako.s_at_z(1.25), "tin_dark")
    b.prism([(-0.085, 0.0), (0.085, 0.0), (0.06, -0.075), (-0.06, -0.075)], 0.012, "black",
            matrix=L.frame((0, -0.07, 1.045), (0.25, 0, 0)))                                  # visor
    b.cyl((0, -0.035, 1.28), (0, -0.045, 1.37), 0.018, 0.018, segs=5, color=trim)
    b.sphere((0, -0.045, 1.39), 0.042, 7, 4, trim, scale=(1, 1, 1.25))                        # plume
    body_ob = b.to_object("TinSoldier_Body", col, pivot=(0, 0, 0.55), parent=root)

    # ---------- legs
    for sgn, side in ((1, "L"), (-1, "R")):   # Blender convention: character left = +X
        g = Builder()
        hip = Vector((sgn * 0.075, 0.0, 0.54))
        leg = Lathe([(0.0, 0.2), (0.058, 0.2), (0.062, 0.36), (0.068, 0.54), (0.0, 0.54)], 6,
                    Matrix.Translation((hip.x, 0, 0)))
        g.lathe(leg, "white")
        _chips(g, leg, rnd, 2, 0.02, 0.3)
        g.cyl((hip.x, 0, 0.0), (hip.x, 0, 0.24), 0.066, 0.064, segs=6, color="black")          # boot
        g.box((hip.x, -0.055, 0.035), (0.11, 0.2, 0.07), "black")
        g.to_object("TinSoldier_Leg" + side, col, pivot=hip, parent=body_ob)

    # ---------- arms (left hangs, right holds the ball). Character left = +X.
    a = Builder()
    sh = Vector((0.19, 0.0, 0.86))
    a.cyl(sh, (0.215, 0.01, 0.6), 0.05, 0.046, segs=6, color=coat)
    a.cyl((0.215, 0.01, 0.62), (0.218, 0.012, 0.56), 0.052, 0.052, segs=6, color=trim)
    a.sphere((0.22, 0.012, 0.53), 0.04, 6, 4, "skin")
    a.to_object("TinSoldier_ArmL", col, pivot=sh, parent=body_ob)

    a = Builder()
    sh = Vector((-0.19, 0.0, 0.86))
    elbow = Vector((-0.215, 0.02, 0.66))
    hand = Vector((-0.2, -0.19, 0.7))
    a.cyl(sh, elbow, 0.05, 0.047, segs=6, color=coat)
    a.sphere(elbow, 0.047, 6, 4, coat)
    a.cyl(elbow, hand, 0.047, 0.044, segs=6, color=coat)
    a.cyl(hand + (elbow - hand).normalized() * 0.06, hand, 0.05, 0.05, segs=6, color=trim)
    a.sphere(hand + Vector((0, -0.01, 0)), 0.04, 6, 4, "skin")
    arm_r = a.to_object("TinSoldier_ArmR", col, pivot=sh, parent=body_ob)

    ball = Builder()
    bc = Vector((-0.2, -0.27, 0.74))
    ball.lathe(Lathe(_ball_profile(0.085, 9), 10, Matrix.Translation(bc)),
               lambda f: "white" if abs(f.calc_center_median().z - bc.z) < 0.018 else "red")
    ball.to_object("TinSoldier_Ball", col, pivot=bc, parent=arm_r)
    return root


def _ball_profile(r, rings):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((0.0 if i in (0, rings) else r * math.cos(a), r * math.sin(a)))
    return prof


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    out = [
        build_roly_poly(col, (0, 0, 0)),
        build_pupsik(col, (1.4, 0, 0)),
        build_tin_soldier(col, (2.6, 0, 0)),
        build_roly_poly(col, (6.0, 0, 0), name="Boss_BigRolyPoly", boss=True, scale=3.0, seed=7),
    ]
    return out

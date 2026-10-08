"""Stage-3 enemies (update after 1.0.1): shield soldier, pioneer drummer and wind-up frog for the hockey box,
music-box ballerina and lunokhod for the construction site, RC car and Dendy light gun for the bazaar,
crying doll for the kindergarten. Same rules as ba_enemies / ba_toys: rigid parts
parented under an empty root, pivots at the joints, procedural animation in Unity. Characters face -Y;
character left = +X."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2
FACE_FRONT = (math.pi / 2, 0.0, 0.0)      # L.frame rotation: a decal facing -Y, reading along +X


def _sphere_profile(r, z, rings=8, squash=1.0):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((0.0 if i in (0, rings) else r * math.cos(a), z + r * squash * math.sin(a)))
    return prof


def _empty(name, col, parent, location, size=0.08):
    """Attachment point exported as an empty child (where a ball leaves the launcher, etc.)."""
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


def _chips(b, lat, rnd, n, s_lo, s_hi, color="tin"):
    for _ in range(n):
        b.decal_disc(lat, rnd.uniform(0, 2 * math.pi), rnd.uniform(s_lo, s_hi),
                     rnd.uniform(0.012, 0.026), rnd.uniform(0.01, 0.02), color, segs=5, off=0.003,
                     rot=rnd.uniform(0, 3))


# ================================================================ shield soldier (hockey box)
def build_shield_soldier(col, location=(0, 0, 0), name="Enemy_ShieldSoldier", seed=21,
                         coat="green", trim="red", extras=None):
    """Tin soldier ~1.45 m in a green coat with a kitchen for armour: an enamel pot lid as a shield (a separate
    part in the left hand: ShieldSoldier_Lid, pivot at the grip behind it), a colander for a helmet and a ladle
    for a club. Parts: Body -> ArmL -> Lid, ArmR (with the ladle), LegL/R."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    belts, gold = "white", "gold"
    parts = {}

    # ---------- body: coat, belt, one crossbelt, head, colander
    b = Builder()
    torso = Lathe([(0.0, 0.5), (0.175, 0.5), (0.168, 0.56), (0.148, 0.62), (0.152, 0.7),
                   (0.162, 0.78), (0.158, 0.85), (0.12, 0.9), (0.06, 0.915), (0.0, 0.92)], 8)
    b.lathe(torso, coat)
    s_belt = torso.s_at_z(0.615)
    b.decal_strip(torso, [(FRONT + 2 * math.pi * i / 16, s_belt) for i in range(17)], 0.045, belts, off=0.004)
    b.decal_disc(torso, FRONT, s_belt, 0.03, 0.026, gold, segs=6, off=0.008)
    b.decal_strip(torso, [(FRONT + math.radians(70), torso.s_at_z(0.87)), (FRONT + math.radians(10), torso.s_at_z(0.75)),
                          (FRONT - math.radians(45), torso.s_at_z(0.64))], 0.036, belts, off=0.005)
    for z in (0.69, 0.8):
        b.decal_disc(torso, FRONT, torso.s_at_z(z), 0.013, 0.013, gold, segs=5, off=0.009)
    b.decal_strip(torso, [(FRONT + 2 * math.pi * i / 12, torso.s_at_z(0.895)) for i in range(13)], 0.03, trim, off=0.004)
    b.decal_strip(torso, [(FRONT + 2 * math.pi * i / 16, torso.s_at_z(0.52)) for i in range(17)], 0.03, trim, off=0.004)
    _chips(b, torso, rnd, 10, torso.s_at_z(0.52), torso.s_at_z(0.86))
    for sgn in (-1, 1):                                                                       # epaulettes
        b.box((sgn * 0.155, 0, 0.885), (0.1, 0.12, 0.03), gold, rot=(0, sgn * 0.35, 0))
    hz = 1.0
    head = Lathe(_sphere_profile(0.1, hz, 6), 8)
    b.lathe(head, "skin")
    s_eye = head.s_at_z(0.995)
    for sgn in (-1, 1):
        b.decal_disc(head, FRONT + sgn * 0.034 / 0.095, s_eye, 0.011, 0.013, "black", segs=6, off=0.004)
        b.decal_disc(head, FRONT + sgn * 0.058 / 0.095, head.s_at_z(0.962), 0.018, 0.014, "pink", segs=6, off=0.004)
        b.decal_strip(head, [(FRONT + sgn * 0.1, head.s_at_z(1.028)), (FRONT + sgn * 0.5, head.s_at_z(1.018))],
                      0.012, "black", off=0.005)                                              # frowning brows
    s_m = head.s_at_z(0.953)
    b.decal_strip(head, [(FRONT - 0.5, s_m + 0.012), (FRONT - 0.2, s_m), (FRONT, s_m + 0.004),
                         (FRONT + 0.2, s_m), (FRONT + 0.5, s_m + 0.012)], 0.016, "black", off=0.005)
    b.sphere((0, -0.098, 0.975), 0.016, 5, 3, "skin")                                          # nose
    # colander helmet: an upturned aluminium bowl with rows of holes, a foot ring and two side handles
    colander = Lathe([(0.118, 1.02), (0.122, 1.05), (0.116, 1.095), (0.1, 1.135), (0.072, 1.165),
                      (0.04, 1.18), (0.0, 1.185)], 12)
    b.lathe(colander, "tin")
    for z, n, ph in ((1.06, 14, 0.0), (1.1, 12, 0.25), (1.137, 9, 0.0), (1.163, 6, 0.3)):
        for k in range(n):
            b.decal_disc(colander, FRONT + ph + 2 * math.pi * k / n, colander.s_at_z(z), 0.009, 0.009,
                         "grey_dark", segs=5, off=0.003)
    b.cyl((0, 0, 1.01), (0, 0, 1.028), 0.134, 0.134, segs=12, color="tin_dark")                  # rim
    b.cyl((0, 0, 1.178), (0, 0, 1.205), 0.052, 0.056, segs=10, color="tin_dark")                 # foot ring
    for sgn in (-1, 1):
        for y in (-0.028, 0.028):
            b.beam((sgn * 0.118, y, 1.07), (sgn * 0.158, y, 1.07), 0.012, 0.012, "tin_dark")
        b.beam((sgn * 0.158, -0.034, 1.07), (sgn * 0.158, 0.034, 1.07), 0.012, 0.012, "tin_dark")
    parts["body"] = b

    # ---------- legs
    for sgn, side in ((1, "L"), (-1, "R")):
        g = Builder()
        hip = Vector((sgn * 0.075, 0.0, 0.54))
        leg = Lathe([(0.0, 0.2), (0.058, 0.2), (0.062, 0.36), (0.068, 0.54), (0.0, 0.54)], 6,
                    Matrix.Translation((hip.x, 0, 0)))
        g.lathe(leg, "white")
        _chips(g, leg, rnd, 2, 0.02, 0.3)
        g.cyl((hip.x, 0, 0.0), (hip.x, 0, 0.24), 0.066, 0.064, segs=6, color="black")
        g.box((hip.x, -0.055, 0.035), (0.11, 0.2, 0.07), "black")
        parts["leg" + side] = (g, hip)

    # ---------- left arm holds the lid out in front of the chest
    a = Builder()
    sh = Vector((0.19, 0.0, 0.86))
    elbow = Vector((0.25, -0.1, 0.66))
    hand = Vector((0.12, -0.27, 0.7))
    a.cyl(sh, elbow, 0.05, 0.047, segs=6, color=coat)
    a.sphere(elbow, 0.047, 6, 4, coat)
    a.cyl(elbow, hand, 0.047, 0.044, segs=6, color=coat)
    a.cyl(hand + (elbow - hand).normalized() * 0.06, hand, 0.05, 0.05, segs=6, color=trim)
    a.sphere(hand, 0.042, 6, 4, "skin")
    parts["armL"] = (a, sh)

    # the lid: white enamel dome with a blue rim, a painted flower, chipped spots and a black knob
    lid_c = Vector((0.04, -0.33, 0.7))
    m = Matrix.Translation(lid_c) @ Matrix.Rotation(math.pi / 2, 4, "X")   # local +Z -> world -Y
    dome = Lathe([(0.0, 0.0), (0.27, 0.0), (0.285, 0.012), (0.272, 0.03), (0.21, 0.052), (0.11, 0.07),
                  (0.0, 0.078)], 16, m)
    lid = Builder()

    def enamel(f):
        c = f.calc_center_median() - lid_c
        r = math.hypot(c.x, c.z)
        return "blue" if r > 0.24 else "white"
    lid.lathe(dome, enamel)
    s_fl = dome.s_at_z(0.066)
    r_fl = dome.radius_at(s_fl)
    phi0 = math.pi * 0.2
    for i in range(5):                                                                        # flower
        ang = 2 * math.pi * i / 5
        lid.decal_disc(dome, phi0 + 0.05 * math.cos(ang) / r_fl, s_fl + 0.05 * math.sin(ang), 0.036, 0.036,
                       "red", segs=7, off=0.006, rot=ang)
    lid.decal_disc(dome, phi0, s_fl, 0.026, 0.026, "yellow", segs=7, off=0.01)
    for d in (-1, 1):
        lid.decal_disc(dome, phi0 + d * 0.11 / r_fl, s_fl - 0.06, 0.04, 0.018, "green", segs=6, off=0.006, rot=d * 0.6)
    for _ in range(5):                                                                        # chipped enamel
        lid.decal_disc(dome, rnd.uniform(0, 2 * math.pi), rnd.uniform(0.05, 0.27), rnd.uniform(0.012, 0.022),
                       rnd.uniform(0.01, 0.018), "black", segs=5, off=0.005, rot=rnd.uniform(0, 3))
    knob0 = lid_c + Vector((0, -0.075, 0))
    lid.cyl(knob0, knob0 + Vector((0, -0.035, 0)), 0.018, 0.018, segs=6, color="black")
    lid.sphere(knob0 + Vector((0, -0.05, 0)), 0.035, 8, 4, "black", scale=(1, 0.6, 1))
    parts["lid"] = (lid, lid_c)

    # ---------- right arm swings a ladle like a mace
    a = Builder()
    sh = Vector((-0.19, 0.0, 0.86))
    elbow = Vector((-0.23, 0.03, 0.66))
    hand = Vector((-0.24, -0.15, 0.6))
    a.cyl(sh, elbow, 0.05, 0.047, segs=6, color=coat)
    a.sphere(elbow, 0.047, 6, 4, coat)
    a.cyl(elbow, hand, 0.047, 0.044, segs=6, color=coat)
    a.cyl(hand + (elbow - hand).normalized() * 0.06, hand, 0.05, 0.05, segs=6, color=trim)
    a.sphere(hand, 0.04, 6, 4, "skin")
    grip = hand + Vector((0, 0.07, -0.07))
    tip = hand + Vector((0, -0.2, 0.34))
    a.beam(grip, tip, 0.024, 0.012, "tin")
    a.beam(grip, grip + Vector((0, 0.03, -0.05)), 0.024, 0.012, "tin")                          # hook
    bowl = tip + Vector((0, -0.05, 0.03))
    a.sphere(bowl, 0.075, 8, 5, "tin", scale=(1.0, 1.0, 0.62))
    a.cyl(bowl + Vector((0, 0, 0.03)), bowl + Vector((0, 0, 0.05)), 0.07, 0.07, segs=8, color="tin_dark")
    parts["armR"] = (a, sh)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.55), parent=root)
    for side in ("L", "R"):
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "_Leg" + side, col, pivot=pv, parent=body_ob)
    bld, pv = parts["armL"]
    arm_l = bld.to_object(prefix + "_ArmL", col, pivot=pv, parent=body_ob)
    bld, pv = parts["lid"]
    bld.to_object(prefix + "_Lid", col, pivot=pv, parent=arm_l)
    bld, pv = parts["armR"]
    bld.to_object(prefix + "_ArmR", col, pivot=pv, parent=body_ob)
    return root


# ================================================================ pioneer drummer (hockey box)
def build_drummer(col, location=(0, 0, 0), name="Enemy_Drummer", seed=23, extras=None):
    """Painted toy pioneer ~1.35 m: white shirt, red scarf and pilotka, navy shorts, knee socks, a red snare drum
    on a strap. Parts: Body -> ArmL/ArmR (each with a drumstick, pivot in the shoulder), Drum (pivot at the
    strap), LegL/R. The beat = arms swinging down in turns, the drum jiggles."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    parts = {}

    b = Builder()
    torso = Lathe([(0.0, 0.56), (0.158, 0.56), (0.164, 0.62), (0.152, 0.72), (0.158, 0.82), (0.152, 0.9),
                   (0.112, 0.95), (0.058, 0.965), (0.0, 0.97)], 10)
    b.lathe(torso, "white")
    shorts = Lathe([(0.0, 0.4), (0.168, 0.4), (0.174, 0.46), (0.168, 0.55), (0.158, 0.6), (0.0, 0.6)], 10)
    b.lathe(shorts, "navy")
    s_belt = shorts.s_at_z(0.57)
    b.decal_strip(shorts, [(FRONT + 2 * math.pi * i / 16, s_belt) for i in range(17)], 0.04, "leather", off=0.004)
    b.box((0, -0.168, 0.57), (0.06, 0.012, 0.045), "gold")
    for z in (0.66, 0.74, 0.82):                                                              # shirt buttons
        b.decal_disc(torso, FRONT, torso.s_at_z(z), 0.011, 0.011, "grey_light", segs=5, off=0.004)
    for sgn in (-1, 1):                                                                       # pockets
        b.decal_disc(torso, FRONT + sgn * 0.55, torso.s_at_z(0.84), 0.045, 0.035, "blue_pale", segs=4, off=0.004,
                     rot=math.pi / 4)
    b.decal(  # pioneer badge: a red star on the left breast
        "shape_star", L.frame((0.07, -0.16, 0.855), FACE_FRONT), "red", height=0.06, lift=0.006)
    # red scarf: the knot under the collar, two tails over the shirt, the triangle on the back
    b.box((0, -0.15, 0.925), (0.075, 0.05, 0.055), "red", rot=(0.3, 0, 0.785))
    for sgn in (-1, 1):
        b.prism([(0.0, 0.0), (0.05, 0.0), (0.038, -0.2), (0.012, -0.2)] if sgn > 0 else
                [(-0.05, 0.0), (0.0, 0.0), (-0.012, -0.2), (-0.038, -0.2)],
                0.012, "red", matrix=L.frame((sgn * 0.012, -0.158, 0.915), (math.pi / 2 - 0.22, sgn * 0.3, 0)))
    b.prism([(-0.13, 0.0), (0.13, 0.0), (0.0, -0.17)], 0.01, "red",
            matrix=L.frame((0, 0.15, 0.95), (math.pi / 2 + 0.22, 0, math.pi)))
    b.cyl((0, 0, 0.93), (0, 0, 0.985), 0.14, 0.12, segs=10, color="red")                       # scarf round the neck
    # head: big toy head, shouting face with freckles, a fringe and a pilotka
    hz = 1.1
    head = Lathe(_sphere_profile(0.15, hz, 8), 12)
    b.lathe(head, "skin")
    rf = 0.15
    s_eye = head.s_at_z(hz + 0.005)
    for sgn in (-1, 1):
        ph = FRONT + sgn * 0.36
        b.decal_disc(head, ph, s_eye, 0.02, 0.026, "black", segs=6, off=0.004)
        b.decal_disc(head, ph - sgn * 0.02, s_eye + 0.009, 0.006, 0.006, "white", segs=4, off=0.007)
        b.decal_disc(head, FRONT + sgn * 0.62, head.s_at_z(hz - 0.05), 0.03, 0.022, "pink", segs=6, off=0.004)
        for dx, dz in ((0.46, -0.025), (0.56, -0.015), (0.52, -0.04)):
            b.decal_disc(head, FRONT + sgn * dx, head.s_at_z(hz + dz), 0.006, 0.006, "rust_light", segs=4, off=0.006)
        b.decal_strip(head, [(ph - sgn * 0.12, head.s_at_z(hz + 0.05)), (ph + sgn * 0.12, head.s_at_z(hz + 0.058))],
                      0.011, "wood", off=0.005)
    b.decal_disc(head, FRONT, head.s_at_z(hz - 0.07), 0.034, 0.03, "maroon", segs=8, off=0.005)   # «Будь готов!»
    b.decal_disc(head, FRONT, head.s_at_z(hz - 0.078), 0.02, 0.012, "lips", segs=6, off=0.008)
    b.sphere((0, -0.152, hz - 0.02), 0.022, 5, 3, "skin")                                     # nose
    for sgn in (-1, 1):                                                                       # ears
        b.sphere((sgn * 0.148, 0.0, hz), 0.035, 6, 4, "skin", scale=(0.45, 0.8, 1.1))
    b.decal_disc(head, FRONT + math.pi, head.s_at_z(hz + 0.04), 0.2, 0.13, "wood", segs=12, off=0.004)   # hair
    b.decal_strip(head, [(FRONT - 0.85, head.s_at_z(hz + 0.1)), (FRONT - 0.3, head.s_at_z(hz + 0.115)),
                         (FRONT + 0.1, head.s_at_z(hz + 0.1)), (FRONT + 0.5, head.s_at_z(hz + 0.12)),
                         (FRONT + 0.85, head.s_at_z(hz + 0.1))], 0.05, "wood", off=0.005)
    cap = Matrix(((0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0))).to_4x4()               # prism X->Y, Y->Z, Z->X
    cap = L.frame((-0.105, 0.0, hz + 0.105), (0, 0.12, 0)) @ cap
    b.prism([(-0.19, 0.0), (0.19, 0.0), (0.16, 0.1), (0.0, 0.075), (-0.16, 0.1)], 0.21, "red", matrix=cap)
    b.decal("shape_star", L.frame((0.0, -0.18, hz + 0.16), (math.pi / 2 - 0.1, 0.12, 0)), "gold", height=0.04,
            lift=0.004)
    parts["body"] = b

    # ---------- the drum: red shell with white cords, white hoops, a cream head, tilted to the drummer
    d = Builder()
    dc = Vector((0.0, -0.3, 0.6))
    tilt = Matrix.Rotation(-0.35, 4, "X")                              # top leans back towards the drummer
    dm = Matrix.Translation(dc) @ tilt
    shell = Lathe([(0.19, -0.1), (0.19, 0.1)], 14, dm)
    d.lathe(shell, "red", cap_bottom=False, cap_top=False)
    zig = []
    for k in range(15):
        zig.append((2 * math.pi * k / 14, 0.025 if k % 2 == 0 else 0.175))
    d.decal_strip(shell, zig, 0.014, "white", off=0.005)
    for z0, z1 in ((-0.11, -0.07), (0.07, 0.11)):
        d.lathe(Lathe([(0.205, z0), (0.205, z1)], 14, dm), "white")
    d.lathe(Lathe([(0.0, 0.085), (0.186, 0.085), (0.186, 0.09), (0.0, 0.09)], 14, dm), "cream")
    d.lathe(Lathe([(0.0, -0.09), (0.186, -0.09), (0.186, -0.085), (0.0, -0.085)], 14, dm), "cream")
    strap_a = dm @ Vector((0.19, 0.0, 0.0))
    d.beam(strap_a, Vector((-0.12, -0.14, 0.92)), 0.04, 0.012, "white")                     # strap over the chest
    parts["drum"] = (d, Vector((0.0, -0.2, 0.68)))
    head_c = dm @ Vector((0, 0, 0.09))

    # ---------- arms: short white sleeves, sticks down to the drum head
    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.19, 0.0, 0.9))
        elbow = Vector((sgn * 0.25, -0.06, 0.74))
        hand = Vector((sgn * 0.17, -0.24, 0.82))
        a.sphere(sh, 0.058, 6, 4, "white")
        a.cyl(sh, sh.lerp(elbow, 0.55), 0.058, 0.052, segs=6, color="white")
        a.cyl(sh.lerp(elbow, 0.5), elbow, 0.045, 0.043, segs=6, color="skin")
        a.sphere(elbow, 0.043, 6, 4, "skin")
        a.cyl(elbow, hand, 0.043, 0.04, segs=6, color="skin")
        a.sphere(hand, 0.045, 6, 4, "skin")
        tip = head_c + Vector((sgn * 0.06, -0.02, 0.012))
        a.cyl(hand + (hand - tip).normalized() * 0.06, tip, 0.012, 0.01, segs=5, color="wood_light")
        a.sphere(tip, 0.02, 5, 3, "wood_light")
        parts["arm" + side] = (a, sh)
        g = Builder()
        hip = Vector((sgn * 0.08, 0.0, 0.45))
        g.cyl(hip, (sgn * 0.08, 0.0, 0.27), 0.052, 0.048, segs=6, color="skin")
        g.sphere((sgn * 0.08, 0.0, 0.3), 0.05, 6, 4, "skin")
        g.cyl((sgn * 0.08, 0.0, 0.29), (sgn * 0.08, 0.0, 0.06), 0.05, 0.046, segs=6, color="white")   # knee socks
        g.box((sgn * 0.08, -0.04, 0.035), (0.1, 0.19, 0.07), "leather_dark")
        parts["leg" + side] = (g, hip)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.6), parent=root)
    bld, pv = parts["drum"]
    bld.to_object(prefix + "_Drum", col, pivot=pv, parent=body_ob)
    for side in ("L", "R"):
        bld, pv = parts["arm" + side]
        bld.to_object(prefix + "_Arm" + side, col, pivot=pv, parent=body_ob)
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "_Leg" + side, col, pivot=pv, parent=body_ob)
    return root


# ================================================================ wind-up tin frog (hockey box)
def build_frog(col, location=(0, 0, 0), name="Enemy_Frog", seed=25, top="green_light", spots="green",
               belly="yellow", extras=None):
    """Tin wind-up frog ~0.95 m long: lithographed green back with dark spots, yellow belly, goggle eyes, a wide
    grin, folded hind legs and a wind-up key on the back. Parts: Body -> Key (spins round its axle, +Y),
    ArmL/R (front legs, pivot in the shoulder), LegL/R (hind legs, pivot in the hip: they kick back on a jump)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    parts = {}

    b = Builder()
    bz = 0.33
    body = Lathe(_sphere_profile(0.3, 0.0, 8), 16, L.frame((0, 0.02, bz), (0.12, 0, 0), (1.12, 1.45, 0.82)))

    def tin(f):
        c = f.calc_center_median()
        return belly if c.z < bz - 0.08 else top
    b.lathe(body, tin)
    s_mid = body.s_at_z(bz - 0.06)
    b.decal_strip(body, [(FRONT + 2 * math.pi * i / 20, s_mid) for i in range(21)], 0.022, "ochre", off=0.004)
    for _ in range(9):
        phi = FRONT + math.pi + rnd.uniform(-2.2, 2.2)
        b.decal_disc(body, phi, rnd.uniform(0.62, 0.9) * body.s[-1], rnd.uniform(0.035, 0.06),
                     rnd.uniform(0.03, 0.05), spots, segs=7, off=0.005, rot=rnd.uniform(0, 3))
    b.decal_strip(body, [(FRONT + math.pi, body.s[-1] * 0.98), (FRONT + math.pi, body.s[-1] * 0.55)], 0.03,
                  "lime", off=0.006)                                                          # back stripe
    grin = []
    for i in range(9):                                                                        # wide grin
        t = -1 + 2 * i / 8
        grin.append((FRONT + t * 0.75, body.s_at_z(bz - 0.02) + 0.03 * (1 - t * t)))
    b.decal_strip(body, grin, 0.022, "red_dark", off=0.006)
    for _ in range(5):                                                                        # rust
        b.decal_disc(body, rnd.uniform(0, 2 * math.pi), rnd.uniform(0.2, 0.8) * body.s[-1],
                     rnd.uniform(0.012, 0.025), rnd.uniform(0.01, 0.02), "rust", segs=5, off=0.005)
    for sgn in (-1, 1):                                                                       # goggle eyes
        e = Vector((sgn * 0.15, -0.24, 0.58))
        b.cyl(e + Vector((0, 0.02, -0.1)), e, 0.085, 0.09, segs=10, color=top)
        b.sphere(e, 0.1, 10, 6, "white")
        b.cyl(e + Vector((0, 0.02, -0.075)), e + Vector((0, 0.02, -0.055)), 0.085, 0.09, segs=10, color="gold")
        b.sphere(e + Vector((sgn * 0.012, -0.072, 0.012)), 0.052, 8, 5, "black", scale=(1, 0.55, 1.05))
        b.sphere(e + Vector((sgn * 0.02, -0.105, 0.035)), 0.016, 4, 3, "white")
        b.decal_disc(body, FRONT + sgn * 0.28, body.s_at_z(bz + 0.1), 0.01, 0.01, "black", segs=4, off=0.006)
    b.cyl((0, 0.36, bz + 0.07), (0, 0.44, bz + 0.07), 0.03, 0.03, segs=6, color="tin_dark")     # key socket
    parts["body"] = b

    k_ = Builder()
    axle0, axle1 = Vector((0, 0.42, bz + 0.07)), Vector((0, 0.56, bz + 0.07))
    k_.cyl(axle0, axle1, 0.022, 0.022, segs=6, color="tin")
    for sx in (-1, 1):
        k_.prism([(0, -0.035), (0.13, -0.08), (0.16, 0.0), (0.13, 0.08), (0, 0.035)] if sx > 0 else
                 [(0, 0.035), (-0.13, 0.08), (-0.16, 0.0), (-0.13, -0.08), (0, -0.035)],
                 0.025, "tin", matrix=Matrix.Translation(axle1) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    k_.cyl(axle1 + Vector((0, -0.01, 0)), axle1 + Vector((0, 0.03, 0)), 0.04, 0.04, segs=8, color="tin_dark")
    parts["key"] = (k_, axle0)

    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()                                                                         # front leg
        sh = Vector((sgn * 0.2, -0.2, 0.24))
        wrist = Vector((sgn * 0.27, -0.34, 0.04))
        a.cyl(sh, wrist, 0.045, 0.035, segs=6, color=top)
        a.sphere(sh, 0.05, 6, 4, top)
        for ang in (-0.5, 0.0, 0.5):
            tip = wrist + Vector((math.sin(ang) * 0.09 * sgn, -math.cos(ang) * 0.09, -0.02))
            a.beam(wrist, tip, 0.035, 0.018, top)
        parts["arm" + side] = (a, sh)

        g = Builder()                                                                         # folded hind leg
        hip = Vector((sgn * 0.27, 0.2, 0.27))
        knee = Vector((sgn * 0.44, -0.02, 0.22))
        ankle = Vector((sgn * 0.4, 0.3, 0.07))
        g.cyl(hip, knee, 0.085, 0.06, segs=8, color=top)
        g.sphere(hip, 0.085, 8, 5, top)
        g.sphere(knee, 0.06, 8, 5, top)
        g.cyl(knee, ankle, 0.06, 0.04, segs=8, color=top)
        g.sphere(ankle, 0.04, 6, 4, top)
        for ang in (-0.45, 0.0, 0.45):                                                        # webbed toes forward
            tip = ankle + Vector((sgn * math.sin(ang) * 0.2, -math.cos(ang) * 0.24, -0.04))
            g.beam(ankle, tip, 0.05, 0.02, belly if ang == 0.0 else top)
        parts["leg" + side] = (g, hip)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, bz), parent=root)
    bld, pv = parts["key"]
    bld.to_object(prefix + "_Key", col, pivot=pv, parent=body_ob)
    for side in ("L", "R"):
        bld, pv = parts["arm" + side]
        bld.to_object(prefix + "_Arm" + side, col, pivot=pv, parent=body_ob)
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "_Leg" + side, col, pivot=pv, parent=body_ob)
    return root


# ================================================================ music-box ballerina (construction site)
def build_ballerina(col, location=(0, 0, 0), name="Enemy_Ballerina", seed=27, extras=None):
    """Porcelain music-box ballerina ~1.85 m en pointe on the brass plate of her box: pink bodice, a white and pink
    tutu, arms up in a crown, a bun and a hairline crack across the face. Parts: Base (brass plate with a mirror
    and the spring, pivot on the ground) and Body (pivot on the spring: turns round Z for the pirouette)
    -> ArmL/ArmR (pivot in the shoulder)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.35)
    parts = {}

    base = Builder()
    plate = Lathe([(0.0, 0.0), (0.42, 0.0), (0.445, 0.03), (0.43, 0.07), (0.39, 0.09), (0.0, 0.09)], 18)
    base.lathe(plate, "gold")
    s_band = plate.s_at_z(0.05)
    for k in range(12):                                                                       # engraved dots
        base.decal_disc(plate, 2 * math.pi * k / 12, s_band, 0.012, 0.012, "ochre", segs=5, off=0.003)
    base.cyl((0, 0, 0.09), (0, 0, 0.096), 0.36, 0.36, segs=18, color="blue_pale")             # mirror
    base.cyl((0, 0, 0.09), (0, 0, 0.26), 0.012, 0.012, segs=6, color="gold")                  # rod
    helix = []
    for k in range(37):
        t = k / 36
        a = t * 3 * 2 * math.pi
        helix.append((0.035 * math.cos(a), 0.035 * math.sin(a), 0.1 + 0.12 * t))
    base.tube_path(helix, 0.008, "tin", segs=4)
    parts["base"] = base

    b = Builder()
    tights, shoe = "pink_light", "pink"
    # supporting leg straight up from the pointe; the other leg in passé
    b.lathe(Lathe([(0.0, 0.25), (0.03, 0.26), (0.04, 0.3), (0.042, 0.34), (0.0, 0.345)], 8), shoe)
    b.cyl((0.0, 0.0, 0.33), (0.02, 0.0, 0.62), 0.036, 0.048, segs=8, color=tights)
    b.sphere((0.02, 0.0, 0.62), 0.048, 8, 5, tights)
    b.cyl((0.02, 0.0, 0.62), (0.05, 0.0, 0.94), 0.048, 0.066, segs=8, color=tights)
    for k in range(3):                                                                        # ribbons
        z = 0.3 + 0.03 * k
        b.cyl((0, 0, z), (0, 0, z + 0.012), 0.043, 0.043, segs=8, color="pink_light")
    hip2, knee2, foot2 = Vector((-0.05, 0.0, 0.93)), Vector((-0.3, -0.05, 0.72)), Vector((-0.02, -0.02, 0.6))
    b.cyl(hip2, knee2, 0.062, 0.048, segs=8, color=tights)
    b.sphere(knee2, 0.048, 8, 5, tights)
    b.cyl(knee2, foot2, 0.048, 0.036, segs=8, color=tights)
    b.sphere(foot2, 0.04, 6, 4, shoe, scale=(1.4, 1, 0.8))
    # tutu: two stiff frilled layers
    for (r_in, r_out, z0, z1, col_a, col_b) in ((0.1, 0.48, 0.92, 0.99, "white", "blue_pale"),
                                                (0.1, 0.4, 0.96, 1.03, "pink_light", "white")):
        tutu = Lathe([(r_in, z0), (r_out, z0 + 0.03), (r_out + 0.01, (z0 + z1) / 2 + 0.01), (r_out - 0.02, z1),
                      (r_in, z1 + 0.01)], 20)

        def frill(f, tutu=tutu, a=col_a, c=col_b):
            p = f.calc_center_median()
            if math.hypot(p.x, p.y) < 0.2:
                return a
            k = int(((math.atan2(p.y, p.x) - tutu.phase) % (2 * math.pi)) / tutu.step)
            return a if k % 2 == 0 else c
        b.lathe(tutu, frill)
    bodice = Lathe([(0.0, 0.98), (0.1, 0.99), (0.092, 1.08), (0.112, 1.2), (0.12, 1.28), (0.098, 1.34),
                    (0.05, 1.37), (0.0, 1.38)], 12)
    b.lathe(bodice, "pink")
    b.decal_disc(bodice, FRONT, bodice.s_at_z(1.24), 0.03, 0.03, "red", segs=7, off=0.005)     # rose
    for _ in range(10):                                                                       # sequins
        b.decal_disc(bodice, rnd.uniform(0, 2 * math.pi), rnd.uniform(0.2, 0.8) * bodice.s[-1], 0.008, 0.008,
                     "white", segs=4, off=0.004)
    b.cyl((0, 0, 1.36), (0, 0, 1.43), 0.034, 0.034, segs=8, color="skin_light")
    hz, rf = 1.52, 0.108
    head = Lathe(_sphere_profile(rf, hz, 8, 1.06), 12)
    b.lathe(head, "skin_light")
    s_eye = head.s_at_z(hz + 0.005)
    for sgn in (-1, 1):
        ph = FRONT + sgn * 0.36
        b.decal_disc(head, ph, s_eye, 0.024, 0.02, "white", segs=8, off=0.004)
        b.decal_disc(head, ph, s_eye - 0.002, 0.014, 0.016, "eye_blue", segs=8, off=0.007)
        b.decal_disc(head, ph, s_eye - 0.002, 0.006, 0.007, "black", segs=5, off=0.01)
        b.decal_strip(head, [(ph - sgn * 0.26, s_eye + 0.012), (ph, s_eye + 0.026), (ph + sgn * 0.3, s_eye + 0.03)],
                      0.007, "black", off=0.008)                                             # lashes
        b.decal_disc(head, FRONT + sgn * 0.6, head.s_at_z(hz - 0.04), 0.024, 0.018, "pink", segs=7, off=0.004)
    b.decal_disc(head, FRONT, head.s_at_z(hz - 0.06), 0.018, 0.009, "lips", segs=6, off=0.004)
    crack = [(FRONT - 0.15, head.s_at_z(hz + 0.1)), (FRONT - 0.2, head.s_at_z(hz + 0.06)),
             (FRONT - 0.12, head.s_at_z(hz + 0.035)), (FRONT - 0.24, head.s_at_z(hz - 0.01)),
             (FRONT - 0.18, head.s_at_z(hz - 0.05)), (FRONT - 0.3, head.s_at_z(hz - 0.08))]
    b.decal_strip(head, crack, 0.004, "grey_dark", off=0.006)
    b.decal_disc(head, FRONT + math.pi, head.s_at_z(hz + 0.02), 0.22, 0.16, "wood_dark", segs=12, off=0.003)
    b.decal_strip(head, [(FRONT - 1.1, head.s_at_z(hz + 0.03)), (FRONT - 0.5, head.s_at_z(hz + 0.075)),
                         (FRONT, head.s_at_z(hz + 0.085)), (FRONT + 0.5, head.s_at_z(hz + 0.075)),
                         (FRONT + 1.1, head.s_at_z(hz + 0.03))], 0.05, "wood_dark", off=0.004)
    b.sphere((0, 0.035, hz + 0.12), 0.06, 8, 5, "wood_dark")                                   # bun
    b.decal_strip(head, [(FRONT - 0.7, head.s_at_z(hz + 0.095)), (FRONT, head.s_at_z(hz + 0.112)),
                         (FRONT + 0.7, head.s_at_z(hz + 0.095))], 0.012, "gold", off=0.007)  # tiara
    b.sphere((0, -0.075, hz + 0.105), 0.016, 5, 3, "gold")
    parts["body"] = b

    for sgn, side in ((1, "L"), (-1, "R")):                                                   # arms en couronne
        a = Builder()
        sh = Vector((sgn * 0.12, 0.0, 1.33))
        elbow = Vector((sgn * 0.25, -0.03, 1.55))
        hand = Vector((sgn * 0.07, -0.05, 1.8))
        a.sphere(sh, 0.036, 6, 4, "skin_light")
        a.cyl(sh, elbow, 0.034, 0.028, segs=6, color="skin_light")
        a.sphere(elbow, 0.028, 6, 4, "skin_light")
        a.cyl(elbow, hand, 0.028, 0.022, segs=6, color="skin_light")
        a.sphere(hand, 0.028, 6, 4, "skin_light", scale=(1, 0.6, 1.3))
        parts["arm" + side] = (a, sh)

    if extras:
        extras(parts)

    base_ob = parts["base"].to_object(prefix + "_Base", col, pivot=(0, 0, 0), parent=root)
    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.26), parent=base_ob)
    for side in ("L", "R"):
        bld, pv = parts["arm" + side]
        bld.to_object(prefix + "_Arm" + side, col, pivot=pv, parent=body_ob)
    return root


# ================================================================ lunokhod (construction site)
def build_lunokhod(col, location=(0, 0, 0), name="Enemy_Lunokhod", seed=29, extras=None):
    """Tin lunokhod ~1.75 m long on eight wire wheels: a tub with bands, «СССР» flag, two camera eyes and two
    headlights (lamp cells glow at dusk), antennas; the domed lid is a solar panel inside and opens backwards
    to fire (Lunokhod_Lid, pivot on the rear hinge). Lunokhod_Muzzle marks where balls leave the launcher.
    Parts: Body -> Lid, WheelL1..L4 / WheelR1..R4 (spin round X, pivot at the hub), Muzzle."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.5)
    parts = {}

    b = Builder()
    oct_ = [(0.62, -0.82), (0.7, -0.6), (0.7, 0.6), (0.62, 0.82), (-0.62, 0.82), (-0.7, 0.6), (-0.7, -0.6),
            (-0.62, -0.82)]
    b.prism(oct_, 0.08, "grey_dark", matrix=Matrix.Translation((0, 0, 0.34)))                  # chassis
    for sgn in (-1, 1):                                                                       # wheel beams
        b.beam((sgn * 0.7, -0.7, 0.3), (sgn * 0.7, 0.7, 0.3), 0.06, 0.06, "grey")
    tub_m = Matrix.Diagonal((0.86, 1.0, 1.0, 1.0))
    tub = Lathe([(0.5, 0.42), (0.58, 0.58), (0.66, 0.82), (0.7, 0.95), (0.0, 0.96)], 16, tub_m)
    b.lathe(tub, "tin")
    for z in (0.52, 0.74):
        b.decal_strip(tub, [(FRONT + 2 * math.pi * i / 24, tub.s_at_z(z)) for i in range(25)], 0.028, "tin_dark",
                      off=0.004)
    _chips(b, tub, rnd, 8, tub.s_at_z(0.45), tub.s_at_z(0.9), "grey")
    for sgn in (-1, 1):                                                                       # flags on the sides
        fm = Matrix.Translation((sgn * 0.6, 0.2, 0.66)) @ Matrix.Rotation(sgn * math.pi / 2, 4, "Z") @ \
            Matrix.Rotation(math.pi / 2, 4, "X")
        b.box_m(fm @ Matrix.Translation((0, 0, 0.004)), (0.16, 0.1, 0.008), "red")
        b.box_m(fm @ Matrix.Translation((-0.055, 0.03, 0.009)), (0.022, 0.022, 0.004), "gold")
    # face: two camera eyes, two headlights, a cone antenna and a whip antenna at the front corners
    for sgn in (-1, 1):
        e0 = Vector((sgn * 0.17, -0.63, 0.8))
        e1 = e0 + Vector((0, -0.14, 0))
        b.box((sgn * 0.17, -0.63, 0.8), (0.14, 0.12, 0.12), "tin_dark")
        b.cyl(e0 + Vector((0, -0.05, 0)), e1, 0.05, 0.055, segs=10, color="tin")
        b.cyl(e1, e1 + Vector((0, -0.01, 0)), 0.042, 0.042, segs=10, color="black")
        b.sphere(e1 + Vector((sgn * 0.012, -0.01, 0.015)), 0.01, 4, 3, "white")
        h0 = Vector((sgn * 0.42, -0.78, 0.45))
        b.cyl(h0 + Vector((0, 0.06, 0)), h0, 0.06, 0.06, segs=10, color="grey_dark")
        b.cyl(h0, h0 + Vector((0, -0.012, 0)), 0.05, 0.05, segs=10, color="lamp")
    b.lathe(Lathe([(0.07, 0.0), (0.0, 0.32)], 8, Matrix.Translation((-0.58, -0.66, 0.42))), "tin")
    b.cyl((-0.58, -0.66, 0.4), (-0.58, -0.66, 0.44), 0.08, 0.08, segs=8, color="tin_dark")
    b.cyl((0.58, -0.66, 0.42), (0.62, -0.7, 1.55), 0.014, 0.007, segs=5, color="tin_dark")
    b.sphere((0.62, -0.7, 1.56), 0.03, 6, 4, "red")
    b.cyl((0.58, -0.66, 0.4), (0.58, -0.66, 0.46), 0.05, 0.05, segs=8, color="tin_dark")
    # launcher inside the tub (seen when the lid opens): a red spring cup
    b.cyl((0, -0.1, 0.95), (0, -0.1, 1.0), 0.16, 0.16, segs=12, color="grey_dark")
    b.lathe(Lathe([(0.0, 1.0), (0.14, 1.02), (0.2, 1.1), (0.18, 1.11), (0.0, 1.04)], 12,
                  Matrix.Translation((0, -0.1, 0))), "red")
    hinge = Vector((0.0, 0.7, 0.97))
    for sgn in (-1, 1):
        b.cyl(hinge + Vector((sgn * 0.12, 0, 0)), hinge + Vector((sgn * 0.3, 0, 0)), 0.03, 0.03, segs=6,
              color="tin_dark")
    parts["body"] = b

    lid = Builder()
    lm = Matrix.Translation((0, 0, 0.97)) @ Matrix.Diagonal((0.86, 1.0, 1.0, 1.0))
    dome = Lathe([(0.0, 0.0), (0.74, 0.0), (0.75, 0.025), (0.62, 0.075), (0.38, 0.105), (0.0, 0.115)], 18, lm)

    def panel(f):
        c = f.calc_center_median()
        if f.normal.z < -0.5:
            return "navy"
        return "steel" if c.z < 1.01 else "tin"
    lid.lathe(dome, panel)
    for k in range(-3, 4):                                                                    # solar cells inside
        lid.box((k * 0.16, 0.0, 0.965), (0.012, 1.3 - abs(k) * 0.12, 0.004), "blue")
    for k in range(-4, 5):
        lid.box((0.0, k * 0.15, 0.965), (1.05 - abs(k) * 0.08, 0.012, 0.004), "blue")
    lid.cyl(hinge + Vector((-0.1, 0, 0)), hinge + Vector((0.1, 0, 0)), 0.032, 0.032, segs=6, color="tin")
    lid.decal_disc(dome, FRONT + math.pi, dome.s_at_z(1.06), 0.1, 0.1, "red", segs=10, off=0.004)
    parts["lid"] = (lid, hinge)

    wheels = []
    for sgn, side in ((1, "L"), (-1, "R")):
        for k, y in enumerate((-0.6, -0.2, 0.2, 0.6)):
            w = Builder()
            c = Vector((sgn * 0.78, y, 0.2))
            ax = Vector((sgn * 0.055, 0, 0))
            w.cyl(c - ax, c + ax, 0.2, 0.2, segs=12, color="tin_dark", caps=False)
            w.cyl(c - ax * 0.8, c + ax * 0.8, 0.185, 0.185, segs=12, color="grey_dark")
            w.cyl(c - ax * 1.2, c + ax * 1.2, 0.055, 0.055, segs=8, color="tin")
            for s in range(6):                                                                # spokes
                a = 2 * math.pi * s / 6 + k * 0.3
                p = c + Vector((ax.x * 1.05, math.cos(a) * 0.17, math.sin(a) * 0.17))
                w.beam(c + Vector((ax.x * 1.05, 0, 0)), p, 0.02, 0.02, "tin")
            for s in range(12):                                                               # grousers
                a = 2 * math.pi * s / 12
                p = c + Vector((0, math.cos(a) * 0.203, math.sin(a) * 0.203))
                w.box_m(Matrix.Translation(p) @ Matrix.Rotation(a, 4, "X"), (0.12, 0.016, 0.02), "grey")
            wheels.append((w, c, side + str(k + 1)))

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.4), parent=root)
    bld, pv = parts["lid"]
    bld.to_object(prefix + "_Lid", col, pivot=pv, parent=body_ob)
    for w, c, tag in wheels:
        w.to_object(prefix + "_Wheel" + tag, col, pivot=c, parent=body_ob)
    _empty(prefix + "_Muzzle", col, body_ob, (0.0, -0.1, 1.14))
    return root


# ================================================================ RC car (bazaar)
def build_rc_car(col, location=(0, 0, 0), name="Enemy_RCCar", seed=31, shell="yellow", stripe="red", extras=None):
    """90s radio-controlled buggy ~1.1 m long: yellow shell with red lightning bolts, a red roll cage with a tiny
    driver, knobby tyres, a rear wing and a whip antenna. Parts: Body -> WheelFL/FR/RL/RR (spin round X; the front
    ones also steer round Z, pivot at the hub), Antenna (wobbles, pivot at its base)."""
    L.remove_tree(name)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    parts = {}

    b = Builder()
    b.box((0, 0.0, 0.16), (0.46, 0.96, 0.08), "grey_dark")                                     # chassis tub
    b.box((0, -0.52, 0.17), (0.5, 0.08, 0.1), "black")                                        # front bumper
    b.box((0, 0.5, 0.18), (0.44, 0.06, 0.1), "black")
    # shell: wedge nose, side pods, engine cover
    b.prism([(-0.2, -0.46), (0.2, -0.46), (0.23, -0.1), (0.23, 0.44), (-0.23, 0.44), (-0.23, -0.1)], 0.08, shell,
            matrix=Matrix.Translation((0, 0, 0.2)))
    b.box((0, -0.3, 0.3), (0.34, 0.3, 0.08), shell, rot=(-0.28, 0, 0), taper=(0.8, 0.6))       # nose
    b.box((0, 0.26, 0.33), (0.36, 0.34, 0.14), shell, taper=(0.8, 0.8))                         # engine cover
    for sgn in (-1, 1):
        b.decal("shape_bolt", L.frame((sgn * 0.231, 0.08, 0.24), (math.pi / 2, 0, sgn * math.pi / 2)), stripe,
                height=0.12, lift=0.004)
    b.decal("shape_star", L.frame((0, -0.32, 0.345), (-0.28, 0, 0)), stripe, height=0.12, lift=0.004)
    for sgn in (-1, 1):                                                                       # headlights
        b.cyl((sgn * 0.13, -0.47, 0.24), (sgn * 0.13, -0.5, 0.24), 0.035, 0.035, segs=8, color="lamp")
    # roll cage, driver, rear wing, exhausts
    for sgn in (-1, 1):
        b.tube_path([(sgn * 0.16, -0.18, 0.24), (sgn * 0.14, -0.06, 0.5), (sgn * 0.14, 0.12, 0.5),
                     (sgn * 0.16, 0.2, 0.3)], 0.018, stripe, segs=5)
    b.beam((-0.14, -0.06, 0.5), (0.14, -0.06, 0.5), 0.03, 0.03, stripe)
    b.beam((-0.14, 0.12, 0.5), (0.14, 0.12, 0.5), 0.03, 0.03, stripe)
    b.sphere((0, 0.02, 0.36), 0.085, 8, 5, "white")                                           # driver's helmet
    b.box((0, -0.06, 0.37), (0.12, 0.03, 0.05), "black")
    b.box((0, 0.05, 0.27), (0.18, 0.16, 0.08), "blue")
    for sgn in (-1, 1):
        b.beam((sgn * 0.14, 0.42, 0.34), (sgn * 0.14, 0.48, 0.46), 0.025, 0.02, "grey_dark")
    b.box((0, 0.5, 0.47), (0.56, 0.16, 0.025), stripe, rot=(0.12, 0, 0))                      # rear wing
    for sgn in (-1, 1):
        b.box((sgn * 0.28, 0.5, 0.44), (0.02, 0.18, 0.08), shell)
        b.cyl((sgn * 0.08, 0.48, 0.27), (sgn * 0.09, 0.56, 0.3), 0.025, 0.02, segs=6, color="tin")   # exhausts
    parts["body"] = b

    wheels = []
    for tag, x, y in (("FL", 1, -0.34), ("FR", -1, -0.34), ("RL", 1, 0.34), ("RR", -1, 0.34)):
        w = Builder()
        c = Vector((x * 0.32, y, 0.17))
        ax = Vector((x * 0.07, 0, 0))
        w.cyl(c - ax, c + ax, 0.17, 0.17, segs=12, color="black")
        w.cyl(c - ax * 1.05, c + ax * 1.05, 0.08, 0.08, segs=8, color=shell)
        w.cyl(c + ax * 1.05, c + ax * 1.2, 0.03, 0.03, segs=6, color="tin")
        for s in range(12):                                                                   # knobs
            a = 2 * math.pi * s / 12 + (0.26 if s % 2 else 0.0)
            p = c + Vector((0, math.cos(a) * 0.172, math.sin(a) * 0.172))
            w.box_m(Matrix.Translation(p) @ Matrix.Rotation(a, 4, "X"), (0.15, 0.03, 0.03), "grey_dark")
        wheels.append((w, c, tag))

    ant = Builder()
    a0 = Vector((-0.17, 0.44, 0.28))
    ant.cyl(a0, a0 + Vector((0, 0, 0.06)), 0.03, 0.025, segs=6, color="black")
    ant.cyl(a0, a0 + Vector((0, 0.06, 0.95)), 0.01, 0.005, segs=4, color="black")
    ant.sphere(a0 + Vector((0, 0.06, 0.97)), 0.03, 6, 4, "red")
    parts["antenna"] = (ant, a0)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.25), parent=root)
    for w, c, tag in wheels:
        w.to_object(prefix + "_Wheel" + tag, col, pivot=c, parent=body_ob)
    bld, pv = parts["antenna"]
    bld.to_object(prefix + "_Antenna", col, pivot=pv, parent=body_ob)
    return root


# ================================================================ Dendy light gun (bazaar)
def build_dendy_gun(col, location=(0, 0, 0), name="Enemy_DendyGun", seed=33, extras=None):
    """The light gun of a Dendy console grown to ~1 m and hopping on its grip: grey body, black grip with
    grooves, orange muzzle with the black light sensor, a red trigger, the cable trailing behind.
    Parts: Body (pivot at the bottom of the grip: it hops and tips to aim) -> Trigger (pivot at its hinge),
    Muzzle (empty at the barrel tip: the laser and the shot start there)."""
    L.remove_tree(name)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    parts = {}

    b = Builder()
    bz = 0.78
    b.box((0, -0.18, bz), (0.14, 0.62, 0.15), "grey")                                          # barrel
    b.box((0, -0.18, bz + 0.085), (0.1, 0.62, 0.02), "grey_dark")                              # rib on top
    b.box((0, 0.2, bz - 0.01), (0.19, 0.3, 0.27), "grey_light")                                # receiver
    b.box((0, 0.2, bz + 0.14), (0.12, 0.22, 0.03), "grey_dark")
    b.box((0, 0.08, bz + 0.16), (0.03, 0.03, 0.05), "grey_dark")                              # rear sight
    b.box((0, -0.46, bz + 0.1), (0.025, 0.03, 0.05), "orange")                                # front sight
    b.cyl((0, -0.49, bz), (0, -0.54, bz), 0.1, 0.1, segs=12, color="orange")                  # muzzle
    b.cyl((0, -0.54, bz), (0, -0.545, bz), 0.06, 0.06, segs=12, color="black")                # light sensor
    b.cyl((0, -0.546, bz), (0, -0.548, bz), 0.022, 0.022, segs=8, color="red_light")
    for sgn in (-1, 1):                                                                       # screws and seam
        b.box((sgn * 0.096, 0.2, bz - 0.01), (0.004, 0.26, 0.004), "grey_dark")
        for y in (0.1, 0.3):
            b.cyl((sgn * 0.095, y, bz + 0.06), (sgn * 0.1, y, bz + 0.06), 0.012, 0.012, segs=6, color="grey_dark")
    # grip, raked back, black with grooves; the cable leaves its bottom and trails behind
    g0, g1 = Vector((0, 0.24, bz - 0.12)), Vector((0, 0.36, 0.05))
    b.beam(g0, g1, 0.16, 0.2, "black")
    d = (g1 - g0).normalized()
    for k in range(4):
        p = g0.lerp(g1, 0.25 + 0.16 * k) + Vector((0, -0.105, 0.0))
        b.beam(p, p + d * 0.05, 0.165, 0.012, "grey_dark")
    b.box((0, 0.37, 0.03), (0.17, 0.24, 0.06), "grey_dark")                                   # foot
    b.tube_path([(0, 0.46, 0.04), (0.05, 0.62, 0.03), (0.2, 0.75, 0.03), (0.3, 0.95, 0.03), (0.15, 1.12, 0.03),
                 (-0.1, 1.2, 0.03)], 0.025, "black", segs=5)
    b.tube_path([(0.0, 0.1, bz - 0.14), (0.0, -0.05, bz - 0.2), (0.0, -0.12, bz - 0.19), (0.0, -0.12, bz - 0.08)],
                0.016, "grey_dark", segs=5)                                                   # trigger guard
    parts["body"] = b

    tr = Builder()
    t0 = Vector((0, 0.02, bz - 0.08))
    tr.box((0, -0.01, bz - 0.14), (0.04, 0.035, 0.1), "red", rot=(0.35, 0, 0))
    parts["trigger"] = (tr, t0)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0.36, 0.0), parent=root)
    bld, pv = parts["trigger"]
    bld.to_object(prefix + "_Trigger", col, pivot=pv, parent=body_ob)
    _empty(prefix + "_Muzzle", col, body_ob, (0.0, -0.55, bz))
    return root


# ================================================================ crying doll (kindergarten)
def build_cry_doll(col, location=(0, 0, 0), name="Enemy_CryDoll", seed=35, dress="sky", dots="white",
                   bow="pink", hair="ochre", extras=None):
    """Big vinyl baby doll ~1.25 m that never stops crying: curly hair with a bow, a polka-dot dress with a white
    collar, knee socks and red shoes, eyes squeezed shut, tears down the cheeks, mouth wide open, fists at the eyes.
    Parts: Body -> Head (pivot in the neck: shakes when she wails), ArmL/ArmR (pivot in the shoulder), LegL/R."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Enemy_", "")
    root = L.empty_root(name, col, location, size=0.3)
    skin = "rubber"
    parts = {}

    b = Builder()
    frock = Lathe([(0.0, 0.34), (0.29, 0.34), (0.285, 0.39), (0.21, 0.55), (0.165, 0.67), (0.155, 0.75),
                   (0.105, 0.8), (0.0, 0.81)], 14)
    b.lathe(frock, dress)
    for _ in range(26):
        s = rnd.uniform(0.04, 0.85) * frock.s[-1]
        b.decal_disc(frock, rnd.uniform(0, 2 * math.pi), s, 0.018, 0.018, dots, segs=6, off=0.004)
    b.decal_strip(frock, [(FRONT + 2 * math.pi * i / 20, frock.s_at_z(0.365)) for i in range(21)], 0.03, "white",
                  off=0.005)
    b.cyl((0, 0, 0.77), (0, 0, 0.81), 0.15, 0.12, segs=12, color="white")                    # collar
    for sgn in (-1, 1):
        b.box((sgn * 0.06, -0.13, 0.785), (0.1, 0.06, 0.012), "white", rot=(0.4, 0, sgn * 0.35))
    b.lathe(Lathe([(0.0, 0.3), (0.16, 0.3), (0.17, 0.36), (0.0, 0.36)], 10), "white")          # bloomers
    parts["body"] = b

    h = Builder()
    hz, rf = 0.99, 0.2
    head = Lathe(_sphere_profile(rf, hz, 8, 0.97), 14)
    h.lathe(head, skin)
    s_eye = head.s_at_z(hz + 0.01)
    for sgn in (-1, 1):
        ph = FRONT + sgn * 0.34
        arc = [(ph - 0.12, s_eye - 0.004), (ph - 0.04, s_eye + 0.012), (ph + 0.04, s_eye + 0.012), (ph + 0.12, s_eye - 0.004)]
        h.decal_strip(head, arc, 0.012, "black", off=0.005)                                   # squeezed shut
        h.decal_strip(head, [(ph + sgn * 0.02, s_eye - 0.03), (ph + sgn * 0.08, s_eye - 0.07),
                             (ph + sgn * 0.13, s_eye - 0.13), (ph + sgn * 0.12, s_eye - 0.19)], 0.02, "cyan",
                      off=0.006)                                                              # tears
        h.decal_disc(head, ph + sgn * 0.13, s_eye - 0.2, 0.014, 0.018, "cyan", segs=6, off=0.007)
        h.decal_strip(head, [(ph - sgn * 0.1, s_eye + 0.05), (ph + sgn * 0.08, s_eye + 0.035)], 0.01, "wood",
                      off=0.005)                                                              # sad brows
        h.decal_disc(head, FRONT + sgn * 0.62, head.s_at_z(hz - 0.05), 0.04, 0.03, "pink", segs=7, off=0.004)
    s_m = head.s_at_z(hz - 0.1)
    h.decal_disc(head, FRONT, s_m, 0.05, 0.04, "lips", segs=10, off=0.004)                     # wailing mouth
    h.decal_disc(head, FRONT, s_m - 0.004, 0.038, 0.028, "maroon", segs=10, off=0.007)
    h.decal_disc(head, FRONT, s_m - 0.018, 0.02, 0.01, "red_light", segs=6, off=0.01)
    h.sphere((0, -0.2, hz - 0.035), 0.022, 6, 4, skin, scale=(1.1, 0.8, 0.9))                 # nose
    for k in range(16):                                                                       # curls
        a = 2 * math.pi * k / 16
        if abs(math.atan2(math.sin(a - FRONT), math.cos(a - FRONT))) < 0.7:
            continue
        p = Vector((math.cos(a) * 0.17, math.sin(a) * 0.17, hz + 0.05 + 0.04 * math.sin(k * 1.7)))
        h.ico(p, 0.075, hair, subdiv=1, jitter=0.008, seed=seed + k)
    for k in range(7):
        a = 2 * math.pi * k / 7
        p = Vector((math.cos(a) * 0.09, math.sin(a) * 0.09 + 0.02, hz + 0.16))
        h.ico(p, 0.08, hair, subdiv=1, jitter=0.008, seed=seed + 40 + k)
    for sgn in (-1, 1):                                                                       # fringe curls
        h.ico((sgn * 0.08, -0.14, hz + 0.13), 0.06, hair, subdiv=1, jitter=0.006, seed=seed + 60 + sgn)
    h.ico((0.0, -0.16, hz + 0.14), 0.055, hair, subdiv=1, jitter=0.006, seed=seed + 70)
    for sx in (-1, 1):                                                                        # bow on top
        h.prism([(0, 0), (0.15, -0.07), (0.15, 0.07)] if sx > 0 else [(0, 0), (-0.15, 0.07), (-0.15, -0.07)],
                0.045, bow, matrix=L.frame((0.06, -0.02, hz + 0.24), (math.pi / 2, 0, 0.2)))
    h.sphere((0.06, -0.045, hz + 0.24), 0.04, 8, 5, bow)
    parts["head"] = (h, Vector((0, 0, 0.8)))

    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.15, 0.0, 0.73))
        elbow = Vector((sgn * 0.27, -0.12, 0.66))
        hand = Vector((sgn * 0.12, -0.2, 0.93))
        a.sphere(sh, 0.06, 8, 5, dress)                                                       # puff sleeve
        a.cyl(sh, elbow, 0.045, 0.04, segs=8, color=skin)
        a.sphere(elbow, 0.04, 6, 4, skin)
        a.cyl(elbow, hand, 0.04, 0.042, segs=8, color=skin)
        a.sphere(hand, 0.05, 8, 5, skin)
        parts["arm" + side] = (a, sh)
        g = Builder()
        hip = Vector((sgn * 0.085, 0.0, 0.36))
        g.cyl(hip, (sgn * 0.09, 0.0, 0.2), 0.058, 0.052, segs=8, color=skin)
        g.cyl((sgn * 0.09, 0.0, 0.22), (sgn * 0.09, 0.0, 0.06), 0.056, 0.052, segs=8, color="white")
        g.box((sgn * 0.09, -0.04, 0.035), (0.11, 0.2, 0.07), "red")
        g.box((sgn * 0.09, -0.01, 0.075), (0.115, 0.03, 0.012), "red_dark")                    # strap
        parts["leg" + side] = (g, hip)

    if extras:
        extras(parts)

    body_ob = parts["body"].to_object(prefix + "_Body", col, pivot=(0, 0, 0.5), parent=root)
    bld, pv = parts["head"]
    bld.to_object(prefix + "_Head", col, pivot=pv, parent=body_ob)
    for side in ("L", "R"):
        bld, pv = parts["arm" + side]
        bld.to_object(prefix + "_Arm" + side, col, pivot=pv, parent=body_ob)
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "_Leg" + side, col, pivot=pv, parent=body_ob)
    return root


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    Y = -34.0
    return [
        build_shield_soldier(col, (0.0, Y, 0.0)),
        build_drummer(col, (1.6, Y, 0.0)),
        build_frog(col, (3.2, Y, 0.0)),
        build_ballerina(col, (5.0, Y, 0.0)),
        build_lunokhod(col, (7.4, Y, 0.0)),
        build_rc_car(col, (9.8, Y, 0.0)),
        build_dendy_gun(col, (11.4, Y, 0.0)),
        build_cry_doll(col, (12.8, Y, 0.0)),
    ]

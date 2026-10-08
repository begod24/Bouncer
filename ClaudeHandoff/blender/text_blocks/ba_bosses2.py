"""Stage-3 bosses: «Трансформер из ларька» (bazaar) and «Большой плюшевый заяц» (kindergarten).

The transformer is a police «Жигули» that turns into a robot. Every part is one rigid mesh with two poses:
the FBX Boss_Transformer.fbx holds the ROBOT pose (translations only, like every other asset), and
Boss_Transformer_Car.fbx the same hierarchy in the CAR pose (nodes rotated). In Unity TransformerRig blends
the parts between the two. Car panels (front, greenhouse, doors, rear halves) are modelled in car coordinates
and moved into the robot pose; robot-only parts (pelvis, head, arms) are modelled standing and hidden inside the
car in the car pose. Characters face -Y; character left = +X."""
import math, random
import bpy
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2
TURN = Matrix.Diagonal((-1.0, -1.0, 1.0, 1.0))


def _sphere_profile(r, z, rings=8, squash=1.0):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((0.0 if i in (0, rings) else r * math.cos(a), z + r * squash * math.sin(a)))
    return prof


def _xform(b, m):
    for v in b.bm.verts:
        v.co = m @ v.co


def _empty(name, col, parent, location, size=0.1):
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


# ================================================================ the police car («Жигули»)
BODY, STRIPE, CHROME, GLASS, TYRE = "yellow", "blue", "tin", "window", "black"
HALF_W = 0.82          # half width of the body
AXLE_F, AXLE_R = -1.24, 1.18
WHEEL_R, WHEEL_X = 0.3, 0.7
DOOR_Y0, DOOR_Y1 = -0.9, 0.4
REAR_Y1 = 2.0
SILL, BELT, TOP = 0.3, 0.88, 1.4


def _stripe(b, x_side, y0, y1, color=STRIPE):
    """The blue band along the side at belt height (a thin raised slab on the side face)."""
    x = x_side * (HALF_W + 0.006)
    b.box((x, (y0 + y1) / 2, 0.66), (0.012, y1 - y0, 0.1), color)


def _car_front():
    """Front section in car coordinates: hood, fenders over the front arches, grille, four round headlights,
    indicators, chrome bumper, engine block between the wheel wells."""
    b = Builder()
    y0, y1 = -1.98, DOOR_Y0
    b.box((0, (y0 + y1) / 2, 0.75), (2 * HALF_W, y1 - y0, 0.26), BODY)                  # hood level
    b.box((0, -1.77, 0.46), (2 * HALF_W, 0.42, 0.32), BODY)                              # nose below the hood
    b.box((0, -0.96, 0.46), (2 * HALF_W, 0.12, 0.32), BODY)                              # behind the arch
    for sgn in (-1, 1):
        b.box((sgn * 0.7, AXLE_F, 0.46), (0.2, 0.66, 0.32), "black")                     # wheel well
        _stripe(b, sgn, y0, y1)
    b.box((0, AXLE_F, 0.46), (1.2, 0.66, 0.32), "grey_dark")                             # engine block
    b.box((0, -1.2, 0.885), (1.3, 1.25, 0.01), BODY)                                     # hood panel line
    fz = 0.6
    b.box((0, -1.985, fz), (0.62, 0.02, 0.2), "black")                                   # grille
    for k in range(3):
        b.box((0, -1.997, fz - 0.06 + 0.06 * k), (0.6, 0.01, 0.016), CHROME)
    for sgn in (-1, 1):
        for x, r in ((0.62, 0.09), (0.43, 0.08)):
            c = Vector((sgn * x, -1.985, fz))
            b.cyl(c, c + Vector((0, -0.02, 0)), r + 0.018, r + 0.018, segs=10, color=CHROME)
            b.cyl(c + Vector((0, -0.02, 0)), c + Vector((0, -0.026, 0)), r, r, segs=10, color="lamp")
        b.box((sgn * 0.66, -1.99, 0.42), (0.14, 0.02, 0.06), "orange")                  # indicators
    b.box((0, -2.03, 0.42), (2 * HALF_W + 0.04, 0.08, 0.1), CHROME)                      # bumper
    for sgn in (-1, 1):
        b.box((sgn * 0.84, -1.98, 0.42), (0.06, 0.12, 0.1), CHROME)
        b.box((sgn * 0.3, -2.07, 0.42), (0.05, 0.04, 0.16), "black")                    # over-riders
    b.box((0, -2.07, 0.34), (0.44, 0.012, 0.11), "white")                                # number plate
    return b


def _car_greenhouse():
    """Windshield, roof, rear window and side glass with pillars; the roof carries nothing (the beacon is on the
    robot's head and pokes through the roof in the car pose)."""
    b = Builder()
    prof = [(DOOR_Y0, BELT), (1.2, BELT + 0.04), (0.72, TOP), (-0.35, TOP)]             # (y, z), CCW seen from +X
    pts = [(y, z) for y, z in prof]
    m = Matrix(((0.0, 0.0, 1.0), (1.0, 0.0, 0.0), (0.0, 1.0, 0.0))).to_4x4()            # prism XY -> world YZ
    m = Matrix.Translation((-0.74, 0.0, 0.0)) @ m

    def glass(f):
        n = f.normal
        c = f.calc_center_median()
        if n.z > 0.9:
            return BODY
        if n.z < -0.9:
            return "grey"                                    # never seen in the car; the robot's back armour
        return GLASS
    b.prism(pts, 1.48, glass, matrix=m)
    for sgn in (-1, 1):                                                                  # pillars and rails
        x = sgn * 0.745
        b.beam((x, DOOR_Y0, BELT), (x, -0.35, TOP), 0.07, 0.05, BODY, up=(sgn, 0, 0))
        b.beam((x, 0.16, BELT + 0.02), (x, 0.16, TOP), 0.08, 0.05, BODY, up=(sgn, 0, 0))
        b.beam((x, 0.72, TOP), (x, 1.2, BELT + 0.04), 0.14, 0.05, BODY, up=(sgn, 0, 0))
        b.beam((x, -0.35, TOP), (x, 0.72, TOP), 0.05, 0.05, BODY)
    b.beam((-0.74, -0.35, TOP), (0.74, -0.35, TOP), 0.05, 0.05, BODY)
    b.box((0, 0.18, TOP + 0.005), (1.3, 0.9, 0.01), BODY)
    return b


def _car_door(sgn):
    """Front+rear door panel on one side (one slab), the stripe with «МИЛИЦИЯ», handles, sill."""
    b = Builder()
    x = sgn * (HALF_W - 0.02)
    y0, y1 = DOOR_Y0 + 0.02, DOOR_Y1
    b.box((x, (y0 + y1) / 2, (SILL + BELT) / 2), (0.06, y1 - y0, BELT - SILL), BODY)
    _stripe(b, sgn, y0, y1)
    b.box((x + sgn * 0.034, 0.16 - 0.4, 0.5), (0.004, 0.012, 0.36), "grey_dark")          # door seam
    for y in (-0.52, 0.06):
        b.box((x + sgn * 0.036, y, 0.8), (0.02, 0.14, 0.03), CHROME)                       # handles
    b.box((x + sgn * 0.02, (y0 + y1) / 2, SILL + 0.02), (0.05, y1 - y0, 0.05), "grey_dark")   # sill
    rot = (math.pi / 2, 0, math.pi / 2) if sgn > 0 else (math.pi / 2, 0, -math.pi / 2)
    b.decal("sign_militsiya", L.frame((x + sgn * 0.043, (y0 + y1) / 2, 0.66), rot), "white", height=0.07,
            lift=0.004)
    return b


def _car_rear_half(sgn):
    """Rear quarter of one side (x from the middle to the side): quarter panel over the rear arch, half of the
    trunk lid, taillight, half of the bumper. The two halves become the robot's legs."""
    b = Builder()
    cx = sgn * HALF_W / 2
    w = HALF_W
    y0, y1 = DOOR_Y1, REAR_Y1
    b.box((cx, (y0 + y1) / 2, 0.76), (w, y1 - y0, 0.3), BODY)                            # trunk / deck level
    b.box((cx, (y0 + 0.86) / 2, 0.46), (w, 0.86 - y0, 0.32), BODY)                       # ahead of the arch
    b.box((cx, (1.5 + y1) / 2, 0.46), (w, y1 - 1.5, 0.32), BODY)                         # behind the arch
    b.box((sgn * 0.7, AXLE_R, 0.46), (0.2, 0.64, 0.32), "black")
    b.box((sgn * 0.25, AXLE_R, 0.46), (0.5, 0.64, 0.32), "grey_dark")
    _stripe(b, sgn, y0, y1)
    b.box((cx, 1.62, 0.915), (w - 0.04, 0.72, 0.01), BODY)                               # trunk lid line
    b.box((sgn * 0.6, 2.0, 0.7), (0.34, 0.02, 0.14), "red")                              # taillight
    b.box((sgn * 0.72, 2.004, 0.7), (0.1, 0.016, 0.1), "orange")
    b.box((sgn * 0.46, 2.004, 0.7), (0.08, 0.016, 0.1), "white")
    b.box((cx, 2.04, 0.42), (w + 0.02, 0.08, 0.1), CHROME)                               # bumper half
    b.box((sgn * 0.3, 2.08, 0.42), (0.05, 0.04, 0.16), "black")
    return b


def _wheel(center, sgn):
    w = Builder()
    ax = Vector((sgn * 0.09, 0, 0))
    c = Vector(center)
    w.cyl(c - ax, c + ax, WHEEL_R, WHEEL_R, segs=14, color=TYRE)
    w.cyl(c + ax, c + ax * 1.12, 0.17, 0.15, segs=10, color=CHROME)                    # hubcap
    w.cyl(c + ax * 1.12, c + ax * 1.2, 0.05, 0.05, segs=6, color="grey")
    for k in range(4):                                                                   # holes in the hubcap
        a = k * math.pi / 2 + 0.4
        p = c + ax * 1.13 + Vector((0, math.cos(a) * 0.1, math.sin(a) * 0.1))
        w.cyl(p, p + ax * 0.02, 0.02, 0.02, segs=5, color="grey_dark")
    return w


# ---------------------------------------------------------------- robot-only parts
def _robot_pelvis(c):
    """Hips box with a belt and the abdomen above it (the chest sits on the abdomen)."""
    b = Builder()
    b.box(c, (1.2, 0.58, 0.34), "grey_dark")
    b.box(c + Vector((0, -0.295, 0.02)), (1.0, 0.02, 0.1), "grey")                      # belt
    b.box(c + Vector((0, -0.31, 0.02)), (0.18, 0.02, 0.14), CHROME)                    # buckle
    b.box(c + Vector((0, 0.0, 0.32)), (0.78, 0.46, 0.3), "grey")                       # abdomen
    for k in range(3):
        b.box(c + Vector((0, -0.232, 0.22 + 0.08 * k)), (0.6, 0.012, 0.03), "grey_dark")
    for sgn in (-1, 1):
        b.sphere(c + Vector((sgn * 0.42, 0.02, -0.14)), 0.17, 8, 5, "grey")             # hip joints
    return b


def _robot_head(c):
    """Blocky robot head in a militia cap: grey helmet, red band with a gold star, cyan visor, a mouth plate,
    the blue beacon on top (it pokes through the roof in the car pose)."""
    b = Builder()
    b.box(c, (0.44, 0.42, 0.4), "grey")
    b.box(c + Vector((0, -0.2, -0.02)), (0.36, 0.04, 0.1), "cyan")                     # visor
    b.box(c + Vector((0, -0.2, -0.13)), (0.26, 0.05, 0.1), CHROME)                     # mouth plate
    for k in range(3):
        b.box(c + Vector((0, -0.227, -0.16 + 0.03 * k)), (0.2, 0.006, 0.01), "grey_dark")
    for sgn in (-1, 1):
        b.box(c + Vector((sgn * 0.24, 0, 0.02)), (0.06, 0.24, 0.24), "grey_dark")       # ear fins
        b.box(c + Vector((sgn * 0.27, 0, 0.1)), (0.03, 0.08, 0.3), "grey_dark")
    cap = c + Vector((0, 0, 0.22))
    b.box(cap, (0.5, 0.46, 0.08), "navy")                                               # cap band
    b.box(cap + Vector((0, 0, -0.01)), (0.505, 0.465, 0.04), "red")
    b.box(cap + Vector((0, -0.18, -0.07)), (0.46, 0.18, 0.025), "black", rot=(0.2, 0, 0))   # peak
    b.decal("shape_star", L.frame(cap + Vector((0, -0.233, 0.0)), (math.pi / 2, 0, 0)), "gold", height=0.09,
            lift=0.004)
    top = cap + Vector((0, 0, 0.04))
    b.cyl(top, top + Vector((0, 0, 0.05)), 0.1, 0.1, segs=10, color="grey_dark")        # beacon base
    b.lathe(Lathe(_sphere_profile(0.085, 0.0, 6, 1.3)[3:], 10, Matrix.Translation(top + Vector((0, 0, 0.05)))),
            "blue_light")
    return b


def _robot_arm(sh, sgn, gun):
    """Upper arm in grey, forearm cased in yellow panels (car armour), a fist or an arm cannon."""
    b = Builder()
    elbow = sh + Vector((sgn * 0.05, 0.0, -0.62))
    wrist = elbow + Vector((0.0, -0.12, -0.56))
    b.sphere(sh, 0.17, 8, 5, "grey_dark")
    b.beam(sh, elbow, 0.2, 0.22, "grey")
    b.sphere(elbow, 0.13, 8, 5, "grey_dark")
    b.beam(elbow, wrist, 0.28, 0.3, BODY)
    b.beam(elbow + Vector((sgn * 0.14, -0.03, -0.05)), wrist + Vector((sgn * 0.14, 0, 0.06)), 0.02, 0.1, STRIPE)
    if gun:
        g0 = wrist + Vector((0, 0.02, 0.0))
        b.cyl(g0, g0 + Vector((0, -0.12, -0.34)), 0.13, 0.11, segs=10, color="grey_dark")
        tip = g0 + Vector((0, -0.12, -0.34))
        b.cyl(tip, tip + Vector((0, -0.02, -0.05)), 0.08, 0.08, segs=10, color="black")
        b.cyl(tip + Vector((0, 0.03, 0.09)), tip + Vector((0, 0.0, 0.0)), 0.135, 0.135, segs=10, color=STRIPE)
        return b, tip + Vector((0, -0.03, -0.07))
    b.box(wrist + Vector((0, -0.04, -0.13)), (0.24, 0.22, 0.24), "grey_dark")        # fist
    b.box(wrist + Vector((0, -0.16, -0.1)), (0.22, 0.04, 0.16), "grey")
    return b, wrist + Vector((0, -0.05, -0.2))


# ---------------------------------------------------------------- poses
# Robot pose (world, robot standing, facing -Y) and car pose for every part.
#   car panels:  car-space pivot Pc, robot rotation Rr, robot pivot Pr  (robot = Pr + Rr (v - Pc))
#   robot parts: robot pivot Pr, car pivot Pc and car rotation Rc        (car   = Pc + Rc (v - Pr))
def _R(*rots):
    m = Matrix()
    for axis, deg in rots:
        m = m @ Matrix.Rotation(math.radians(deg), 4, axis)
    return m


PELVIS_R = Vector((0.0, 0.05, 1.86))
CHEST_C, CHEST_R = Vector((0.0, -1.45, 0.3)), Vector((0.0, 0.12, 2.3))
CABIN_C, CABIN_R = Vector((0.0, 0.15, 0.9)), Vector((0.0, 0.8, 2.3))
CABIN_ROT = _R(("X", -90))                                   # roof faces back, windshield up: a shell on the back
HEAD_R = Vector((0.0, 0.08, 3.08))
FW_R = {1: Vector((1.14, 0.1, 2.86)), -1: Vector((-1.14, 0.1, 2.86))}


def _leg_rot(sgn):
    """A rear half of the car stands up as a leg: its rear end goes down to the floor, the outer side (with the
    stripe, now vertical) looks forward, the rear wheel sits on the outside of the knee facing forward. The car's
    left half (sgn=+1) becomes the robot's right leg."""
    return _R(("Z", -sgn * 90), ("X", -90))


def _door_rot(sgn):
    """Doors become wings on the back: the long edge points up and out, the outer face (with «МИЛИЦИЯ»)
    looks forward, the hinge corner sits at the shoulder."""
    return _R(("Y", sgn * 35), ("Z", -sgn * 90), ("X", 90))


def build_transformer(col, location=(0, 0, 0), name="Boss_Transformer"):
    """Police «Жигули» / robot. Hierarchy: root -> Pelvis -> LegL/LegR (-> WheelRL/RR), Chest -> Head, ArmL, ArmR
    (-> Muzzle / Hand), Cabin, DoorL, DoorR, WheelFL, WheelFR. Robot ~3.3 m tall, car 4.1 m long; scale in Unity.
    The car pose of every part is stored in root["car_pose"] (local matrices) for preview and export."""
    L.remove_tree(name)
    root = L.empty_root(name, col, location, size=0.6)
    parts = {}       # name -> (builder, robot pivot, parent name, car world matrix of the part)

    # --- car panels
    front = _car_front()
    _xform(front, Matrix.Translation(CHEST_R) @ Matrix.Translation(-CHEST_C))
    parts["Chest"] = (front, CHEST_R, "Pelvis", Matrix.Translation(CHEST_C))
    cab = _car_greenhouse()
    _xform(cab, Matrix.Translation(CABIN_R) @ CABIN_ROT @ Matrix.Translation(-CABIN_C))
    parts["Cabin"] = (cab, CABIN_R, "Chest", Matrix.Translation(CABIN_C) @ CABIN_ROT.inverted())
    for sgn, side in ((1, "L"), (-1, "R")):
        d = _car_door(sgn)
        dc = Vector((sgn * HALF_W, DOOR_Y0, BELT))                  # hinge at the top front corner
        dr = Vector((sgn * 0.5, 0.74, 2.72))
        rot = _door_rot(sgn)
        _xform(d, Matrix.Translation(dr) @ rot @ Matrix.Translation(-dc))
        parts["Door" + side] = (d, dr, "Chest", Matrix.Translation(dc) @ rot.inverted())
    for sgn in (1, -1):
        leg = _car_rear_half(sgn)
        rot = _leg_rot(sgn)
        lc = Vector((sgn * HALF_W / 2, DOOR_Y1, 0.62))
        lr = Vector((-sgn * 0.43, 0.06, 1.7))                        # the halves swap sides when they stand up
        _xform(leg, Matrix.Translation(lr) @ rot @ Matrix.Translation(-lc))
        side = "L" if -sgn > 0 else "R"
        parts["Leg" + side] = (leg, lr, "Pelvis", Matrix.Translation(lc) @ rot.inverted())
        wc = Vector((sgn * WHEEL_X, AXLE_R, WHEEL_R))
        wheel = _wheel(wc, sgn)
        wr = lr + rot @ (wc - lc)
        _xform(wheel, Matrix.Translation(lr) @ rot @ Matrix.Translation(-lc))
        parts["WheelR" + side] = (wheel, wr, "Leg" + side, Matrix.Translation(wc) @ rot.inverted())
    for sgn, side in ((1, "L"), (-1, "R")):                           # front wheels: pauldrons on the robot
        wc = Vector((sgn * WHEEL_X, AXLE_F, WHEEL_R))
        wheel = _wheel(wc, sgn)
        _xform(wheel, Matrix.Translation(FW_R[sgn] - wc))
        parts["WheelF" + side] = (wheel, FW_R[sgn], "Chest", Matrix.Translation(wc))

    # --- robot-only parts, hidden inside the car
    parts["Pelvis"] = (_robot_pelvis(PELVIS_R), PELVIS_R, None,
                       Matrix.Translation((0.0, -0.1, 0.5)))
    parts["Head"] = (_robot_head(HEAD_R), HEAD_R + Vector((0, 0, -0.2)), "Chest",
                     Matrix.Translation((0.0, 0.1, 0.93)))
    hands = {}
    for sgn, side in ((1, "L"), (-1, "R")):
        sh = Vector((sgn * 1.02, 0.06, 2.64))
        arm, hand = _robot_arm(sh, sgn, gun=(sgn < 0))
        hands[side] = hand
        parts["Arm" + side] = (arm, sh, "Chest",
                               Matrix.Translation((sgn * 0.48, -0.78, 0.56)) @ _R(("X", 90)))

    # --- objects (robot pose)
    prefix = name.replace("Boss_", "") + "_"
    order = ["Pelvis", "Chest", "LegL", "LegR", "WheelRL", "WheelRR", "Head", "Cabin", "DoorL", "DoorR",
             "ArmL", "ArmR", "WheelFL", "WheelFR"]
    obs = {}
    for key in order:
        bld, pv, par, _ = parts[key]
        parent = obs[par] if par else root
        obs[key] = bld.to_object(prefix + key, col, pivot=pv, parent=parent)
    _empty(prefix + "Muzzle", col, obs["ArmR"], hands["R"])
    _empty(prefix + "Hand", col, obs["ArmL"], hands["L"])

    # --- car pose: local matrix of every part relative to its parent (asset space)
    world_car = {k: parts[k][3] for k in order}
    pose = {}
    for key in order:
        par = parts[key][2]
        parent_w = world_car[par] if par else Matrix()
        # pivots: robot objects sit at their pivot; the car matrix above maps the part's local mesh
        # (relative to the pivot) straight into car space, so it already is the part's car world matrix.
        pose[prefix + key] = parent_w.inverted() @ world_car[key]
    root["car_pose"] = {k: [v for r in m for v in r] for k, m in pose.items()}   # 16 floats, row-major
    return root


def set_pose(root, car):
    """Preview in Blender: put the transformer into the car pose (car=True) or back into the robot pose."""
    pose = root.get("car_pose")
    for ob in root.children_recursive:
        if ob.type != "MESH":
            continue
        if car and pose and ob.name in pose:
            f = list(pose[ob.name])
            ob.matrix_basis = Matrix([f[0:4], f[4:8], f[8:12], f[12:16]])
        else:
            parent_pv = Vector(ob.parent.get("pivot", (0, 0, 0)))
            ob.matrix_basis = Matrix.Translation(Vector(ob["pivot"]) - parent_pv)


def export_transformer(folder=None):
    """Boss_Transformer.fbx (robot pose, like every asset) + Boss_Transformer_Car.fbx (car pose, rotated nodes)."""
    import os, ba_export
    root = bpy.data.objects["Boss_Transformer"]
    folder = folder or os.path.join(ba_export.MODELS, "Enemies")
    set_pose(root, False)
    robot = ba_export.export_root(root, folder)
    set_pose(root, True)
    try:
        car = _export_posed(root, os.path.join(folder, root.name + "_Car.fbx"))
    finally:
        set_pose(root, False)
    return [robot, car]


def _export_posed(root, path):
    """Like ba_export.export_root, but the nodes may be rotated: the 180-degree turn is applied by conjugating
    every node matrix (T M T^-1) and turning the mesh data, then undone the same way."""
    objs = [root] + list(root.children_recursive)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    old = root.location.copy()
    root.location = Vector(root.get("pivot", (0.0, 0.0, 0.0)))

    def turn():
        for o in objs:
            if o.type == "MESH":
                o.data.transform(TURN)
                o.data.update()
            o.matrix_basis = TURN @ o.matrix_basis @ TURN
    turn()
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
            axis_forward="Y", axis_up="Z", bake_space_transform=False,
            mesh_smooth_type="FACE", use_mesh_modifiers=True, use_triangles=False,
            use_custom_props=False, add_leaf_bones=False, bake_anim=False,
            path_mode="STRIP", embed_textures=False)
    finally:
        turn()
        root.location = old
        for o in objs:
            o.select_set(False)
    return path


# ================================================================ big plush hare (kindergarten boss)
FUR, FUR_LIGHT, INNER = "grey_light", "white", "pink"


def _ear_segment(b, p0, p1, width, thick, color, inner=None):
    """Flat plush ellipsoid from p0 to p1 (its flat side looks forward, -Y); `inner` adds the pink lining."""
    p0, p1 = Vector(p0), Vector(p1)
    z = (p1 - p0).normalized()
    x = Vector((1.0, 0.0, 0.0)) - z * z.x
    x = x.normalized() if x.length > 1e-4 else Vector((0.0, 1.0, 0.0))
    y = z.cross(x)
    half = (p1 - p0).length / 2
    frame_ = Matrix((x, y, z)).transposed().to_4x4()
    frame_.translation = (p0 + p1) / 2
    b.lathe(Lathe(_sphere_profile(1.0, 0.0, 8), 10, frame_ @ Matrix.Diagonal((width, thick, half, 1.0))), color)
    if inner:
        lin = frame_ @ Matrix.Translation((0.0, -thick * 0.45, 0.0)) @ Matrix.Diagonal((width * 0.62, thick * 0.6,
                                                                                         half * 0.85, 1.0))
        b.lathe(Lathe(_sphere_profile(1.0, 0.0, 8), 10, lin), inner)


def build_hare(col, location=(0, 0, 0), name="Boss_Hare", seed=41, overalls="red"):
    """Worn Soviet plush hare ~3.6 m (ears to ~4.8 m): grey fur with a white belly and cheeks, button eyes (one
    hangs on a thread), a stitched grin with buck teeth, red overalls with white buttons, a big plush carrot in the
    right paw, a stitched seam on the left side. Parts: Body (pivot in the hips) -> Head (neck) -> EarL, EarR (the
    right ear is bent; pivot at the ear roots: the ear whip), ArmL, ArmR -> Carrot (thrown as a boomerang),
    LegL/R, Stuffing (tufts poking out of the torn seam: hidden until the seam rips)."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    prefix = name.replace("Boss_", "") + "_"
    root = L.empty_root(name, col, location, size=0.8)
    parts = {}

    # ---------- body: pear-shaped, white belly, overalls, seam, tail
    b = Builder()
    torso = Lathe([(0.0, 0.72), (0.45, 0.76), (0.66, 0.95), (0.74, 1.2), (0.72, 1.5), (0.62, 1.85), (0.46, 2.1),
                   (0.28, 2.25), (0.0, 2.3)], 16, Matrix.Diagonal((1.0, 0.88, 1.0, 1.0)))

    def dress(f):
        z = f.calc_center_median().z
        return overalls if z < 1.32 else FUR
    b.lathe(torso, dress)
    for k in range(6):                                  # the bib: narrow strips, so the flat quads hug the belly
        ph = FRONT + (k - 2.5) * 0.105 / 0.7
        b.decal_strip(torso, [(ph, torso.s_at_z(1.3)), (ph, torso.s_at_z(1.74))], 0.106, overalls,
                      off=0.008 + 0.0008 * k)
    for k in range(2):                                  # pocket on the bib
        ph = FRONT + (k - 0.5) * 0.11 / 0.7
        b.decal_strip(torso, [(ph, torso.s_at_z(1.44)), (ph, torso.s_at_z(1.6))], 0.115, "red_dark", off=0.013)
    for sgn in (-1, 1):
        b.decal_strip(torso, [(FRONT + sgn * 0.42, torso.s_at_z(1.72)), (FRONT + sgn * 0.62, torso.s_at_z(2.02)),
                              (FRONT + sgn * 1.1, torso.s_at_z(2.12)), (FRONT + math.pi - sgn * 0.6, torso.s_at_z(1.85)),
                              (FRONT + math.pi - sgn * 0.4, torso.s_at_z(1.34))], 0.09, overalls, off=0.012)   # straps
        b.decal_disc(torso, FRONT + sgn * 0.42, torso.s_at_z(1.72), 0.055, 0.055, "white", segs=8, off=0.018)
    for d in (-0.2, 0.2):                                                                 # buttons at the waist
        b.decal_disc(torso, FRONT + d, torso.s_at_z(1.28), 0.045, 0.045, "white", segs=8, off=0.014)
    for k in range(4):                                                                  # stitched seam, left side
        z0 = 1.35 + 0.18 * k
        b.decal_strip(torso, [(FRONT + math.pi / 2 + 0.25, torso.s_at_z(z0)),
                              (FRONT + math.pi / 2 + 0.25, torso.s_at_z(z0 + 0.14))], 0.025, "grey", off=0.008)
        b.decal_strip(torso, [(FRONT + math.pi / 2 + 0.18, torso.s_at_z(z0 + 0.07)),
                              (FRONT + math.pi / 2 + 0.32, torso.s_at_z(z0 + 0.07))], 0.02, "grey_dark", off=0.01)
    b.sphere((0, 0.66, 1.05), 0.22, 10, 6, FUR_LIGHT)                                   # tail pompom
    parts["body"] = b

    # ---------- stuffing tufts along the seam (hidden until the seam rips)
    st = Builder()
    seam_c = torso.point(FRONT + math.pi / 2 + 0.25, torso.s_at_z(1.62), 0.02)
    for k in range(9):
        z = 1.36 + 0.07 * k
        p = torso.point(FRONT + math.pi / 2 + 0.25 + rnd.uniform(-0.06, 0.06), torso.s_at_z(z), 0.06)
        st.ico(p, rnd.uniform(0.09, 0.15), "cream", subdiv=1, jitter=0.03, seed=seed + k)
    parts["stuffing"] = (st, seam_c)

    # ---------- head: button eyes, white cheeks, pink nose, buck teeth, stitched grin, whiskers
    h = Builder()
    hz = 2.78
    head = Lathe(_sphere_profile(0.6, hz, 10, 0.9), 16, Matrix.Diagonal((1.06, 0.95, 1.0, 1.0)))
    h.lathe(head, FUR)
    for sgn in (-1, 1):
        h.sphere((sgn * 0.17, -0.5, hz - 0.2), 0.2, 10, 6, FUR_LIGHT, scale=(1.0, 0.8, 0.8))   # cheeks
    h.sphere((0, -0.63, hz - 0.08), 0.1, 8, 5, INNER, scale=(1.2, 0.9, 0.8))                  # nose
    for sgn in (-1, 1):
        h.box((sgn * 0.055, -0.6, hz - 0.38), (0.1, 0.05, 0.14), "white")                      # buck teeth
        for k in range(3):                                                                    # whiskers
            a = (k - 1) * 0.22
            p0 = Vector((sgn * 0.3, -0.6, hz - 0.18 + a * 0.3))
            h.beam(p0, p0 + Vector((sgn * 0.45, -0.05, a)), 0.012, 0.012, "grey_dark")
    grin = [(FRONT - 0.55, head.s_at_z(hz - 0.28)), (FRONT - 0.3, head.s_at_z(hz - 0.37)),
            (FRONT, head.s_at_z(hz - 0.4)), (FRONT + 0.3, head.s_at_z(hz - 0.37)), (FRONT + 0.55, head.s_at_z(hz - 0.28))]
    h.decal_strip(head, grin, 0.025, "maroon", off=0.008)
    for k in range(9):                                                                    # stitches over the grin
        t = -1 + 2 * k / 8
        phi = FRONT + t * 0.5
        s = head.s_at_z(hz - 0.4 + 0.1 * t * t)
        h.decal_strip(head, [(phi, s - 0.035), (phi, s + 0.035)], 0.014, "maroon", off=0.01)
    for sgn, drop in ((1, 0.0), (-1, 0.1)):                                               # button eyes
        c = head.point(FRONT + sgn * 0.38, head.s_at_z(hz + 0.12 - drop), 0.01)
        n = (c - Vector((0, 0, hz))).normalized()
        c2 = c + n * (0.06 if drop else 0.02)
        h.cyl(c2 - n * 0.02, c2 + n * 0.04, 0.11, 0.11, segs=10, color="black")
        for dx, dz in ((-0.035, 0.035), (0.035, 0.035), (-0.035, -0.035), (0.035, -0.035)):
            h.sphere(c2 + n * 0.045 + Vector((dx, 0, dz)), 0.016, 4, 3, "grey")
        if drop:
            h.beam(c, c2 + Vector((0, 0, 0.1)), 0.01, 0.01, "cream")                         # the loose thread
    parts["head"] = (h, Vector((0, 0, 2.2)))

    # ---------- ears: left straight up, right bent over; pink inside
    for sgn, side in ((1, "L"), (-1, "R")):
        e = Builder()
        base = Vector((sgn * 0.25, 0.05, hz + 0.45))
        if side == "L":
            _ear_segment(e, base + Vector((0, 0, -0.1)), base + Vector((sgn * 0.14, 0.0, 1.45)), 0.2, 0.085, FUR,
                         INNER)
        else:
            fold = base + Vector((sgn * 0.06, 0.0, 0.78))
            _ear_segment(e, base + Vector((0, 0, -0.1)), fold + Vector((0, 0, 0.1)), 0.2, 0.085, FUR, INNER)
            _ear_segment(e, fold, fold + Vector((sgn * 0.22, -0.52, -0.3)), 0.19, 0.08, FUR)   # the bent tip
            e.sphere(fold + Vector((0, -0.02, 0.02)), 0.14, 8, 5, FUR, scale=(1.3, 0.75, 1.0))
        parts["ear" + side] = (e, base)

    # ---------- arms and legs
    for sgn, side in ((1, "L"), (-1, "R")):
        a = Builder()
        sh = Vector((sgn * 0.62, 0.0, 2.02))
        paw = Vector((sgn * 0.86, -0.28, 1.3))
        a.sphere(sh, 0.22, 10, 6, FUR)
        a.cyl(sh, paw, 0.21, 0.19, segs=10, color=FUR)
        a.sphere(paw, 0.22, 10, 6, FUR, scale=(1.0, 1.1, 1.0))
        a.sphere(paw + Vector((0, -0.16, -0.02)), 0.12, 8, 5, INNER, scale=(1.0, 0.4, 1.0))
        parts["arm" + side] = (a, sh, paw)
        g = Builder()
        hip = Vector((sgn * 0.36, 0.0, 0.9))
        knee = Vector((sgn * 0.375, -0.03, 0.6))
        g.cyl(hip, knee, 0.27, 0.255, segs=10, color=overalls)                           # shorts
        g.cyl(knee + Vector((0, 0, 0.03)), (sgn * 0.38, -0.05, 0.3), 0.24, 0.23, segs=10, color=FUR)
        g.sphere((sgn * 0.38, -0.18, 0.16), 0.2, 10, 6, FUR, scale=(1.35, 2.4, 0.85))       # big foot
        g.sphere((sgn * 0.38, -0.5, 0.12), 0.1, 8, 5, INNER, scale=(1.3, 0.5, 0.9))
        if sgn < 0:                                                                         # patch on the shorts
            g.box((sgn * 0.38, -0.265, 0.74), (0.22, 0.02, 0.18), "blue", rot=(0.1, 0, 0.2))
            for d in (-0.055, 0.055):
                g.box((sgn * 0.38 + d, -0.277, 0.74), (0.012, 0.01, 0.18), "white", rot=(0.1, 0, 0.2))
        parts["leg" + side] = (g, hip)

    # ---------- the plush carrot in the right paw (the boomerang)
    c_ = Builder()
    grip = parts["armR"][2] + Vector((0.0, -0.12, 0.0))
    axis = Vector((0.15, -1.0, -0.35)).normalized()
    m = Matrix.Translation(grip - axis * 0.25) @ L.rot_z_to(axis)
    carrot = Lathe([(0.17, 0.0), (0.16, 0.3), (0.13, 0.6), (0.08, 0.9), (0.0, 1.15)], 10, m)
    c_.lathe(carrot, "orange")
    for k in range(4):
        c_.decal_strip(carrot, [(2 * math.pi * j / 10, carrot.s[-1] * (0.2 + 0.18 * k)) for j in range(11)], 0.02,
                       "orange_light", off=0.006)
    root_end = grip - axis * 0.25
    for k in range(4):                                                                     # leaves
        a = 2 * math.pi * k / 4
        d = (-axis + Vector((math.cos(a), 0.0, math.sin(a))) * 0.6).normalized()
        c_.lathe(Lathe([(0.06, 0.0), (0.03, 0.3), (0.0, 0.45)], 5, Matrix.Translation(root_end) @ L.rot_z_to(d)),
                 "green")
    parts["carrot"] = (c_, grip)

    body_ob = parts["body"].to_object(prefix + "Body", col, pivot=(0, 0, 0.9), parent=root)
    bld, pv = parts["stuffing"]
    bld.to_object(prefix + "Stuffing", col, pivot=pv, parent=body_ob)
    bld, pv = parts["head"]
    head_ob = bld.to_object(prefix + "Head", col, pivot=pv, parent=body_ob)
    for side in ("L", "R"):
        bld, pv = parts["ear" + side]
        bld.to_object(prefix + "Ear" + side, col, pivot=pv, parent=head_ob)
    arm_r = None
    for side in ("L", "R"):
        bld, pv, _ = parts["arm" + side]
        ob = bld.to_object(prefix + "Arm" + side, col, pivot=pv, parent=body_ob)
        if side == "R":
            arm_r = ob
        bld, pv = parts["leg" + side]
        bld.to_object(prefix + "Leg" + side, col, pivot=pv, parent=body_ob)
    bld, pv = parts["carrot"]
    bld.to_object(prefix + "Carrot", col, pivot=pv, parent=arm_r)
    return root


def build_battery(col, location=(0, 0, 0), name="Boss_Transformer_Battery"):
    """A 9-volt «Крона» battery the robot throws as a mine (~0.4 m, game size): blue body, white band, two terminals
    on top. Origin at its centre (it flies)."""
    L.remove_tree(name)
    b = Builder()
    b.box((0, 0, 0), (0.26, 0.16, 0.4), "blue")
    b.box((0, 0, 0.02), (0.265, 0.165, 0.14), "white")
    b.box((0, 0, 0.205), (0.24, 0.14, 0.012), "tin")
    b.cyl((-0.06, 0, 0.21), (-0.06, 0, 0.25), 0.03, 0.03, segs=6, color="tin")
    b.lathe(Lathe([(0.0, 0.21), (0.035, 0.21), (0.035, 0.24), (0.02, 0.25), (0.0, 0.25)], 6,
                  Matrix.Translation((0.06, 0, 0))), "tin")
    b.decal("shape_bolt", L.frame((0, -0.0826, 0.02), (math.pi / 2, 0, 0)), "red", height=0.1, lift=0.002)
    return L.mesh_root(b, name, col, location)


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    return [build_transformer(col, (0.0, -42.0, 0.0)), build_hare(col, (5.0, -42.0, 0.0)),
            build_battery(col, (2.5, -46.0, 0.2))]

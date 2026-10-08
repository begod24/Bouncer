"""Splash screen / key art and the application icon, built from the Bouncer assets.

Idea «Перед розыгрышем»: kid's-eye view from our half of the chalk dodgeball court at sunset.
Balls lie on the centre line; across it the toys line up: pupsiks in front (one hops),
roly-polys, tin soldiers (one winds up a throw), the big roly-poly boss in the middle,
the courtyard and panel blocks behind them. The sky at the top is left free for the logo.

Scene 'Splash' holds linked duplicates (shared meshes) of the asset objects, so the assets and
the FBX export stay untouched. The duplicates get a splash-only copy of the palette material
through an object-level slot (it adds distance haze). build() recreates the scene from scratch
(run it again after build_all.py, the old copies keep the old meshes), render(path) renders
3840x2160 in Cycles; Tools/branding_art.py in the repo turns that into the splash PNGs.
build_icon() makes the scene 'Icon' for the app icon (see below).
Coordinates: court centre line along X at y = 0, camera at -Y looking +Y, toys face -Y
(their default front).
"""
import bpy, math, random, sys
from mathutils import Vector, Matrix, Euler

if "ba_lib" not in sys.modules:
    sys.modules["ba_lib"] = bpy.data.texts["ba_lib.py"].as_module()
import ba_lib as L

SCENE = "Splash"
COLL = "Splash"
P = "SPL_"

# ---------------------------------------------------------------- look
PALETTE = "T_Palette_Day"
SUN_DIR = (0.90, 0.28, -0.33)           # direction the light travels: low sun (19 deg) from the left
SUN_GLOW = ("FFB77A", 0.55, 3.0)        # sky glow toward the sun: colour, amount, falloff power
SUN_COLOR = "FFBF85"
SUN_ENERGY = 4.2
SKY = [                                  # (elevation sin, sRGB hex): sunset, anti-solar side
    (-1.0, "5A4A48"), (-0.02, "8E7468"), (0.0, "FFD7B5"), (0.05, "F9BDB0"),
    (0.14, "D7A9C9"), (0.3, "8C86CC"), (0.55, "5C6FC0"), (1.0, "32479A"),
]
AMBIENT = 0.55                           # sky strength for lighting (camera sees it at 1.0)
HAZE = ("F5C6B4", 30.0, 260.0, 0.75)    # colour, start m, end m, max amount
EMIT = 1.4

CAM_LOC = (0.0, -4.6, 0.9)
CAM_TARGET = (0.25, 10.0, 2.25)
LENS = 28.0


def srgb(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


# ---------------------------------------------------------------- the cast (x, y on the court; y > 0 = their half)
BALLS = [  # name, x, y, diameter, (tilt x, tilt y, yaw)
    ("Ball_Volleyball", -1.62, 0.05, 0.50, (15, -20, 40)),
    ("Ball_Rubber", 0.05, -0.05, 0.44, (8, 25, -20)),
    ("Ball_Tennis", 1.3, 0.0, 0.26, (70, 10, 30)),
    ("Ball_Medicine", 5.35, 7.0, 0.60, (20, 10, -35)),
    ("Ball_Deflated", -4.6, 5.0, 0.48, (-25, 0, 35)),
]
PUPSIKS = [(-2.2, 2.4, 10, 0), (-1.1, 3.4, -6, 1), (1.15, 3.0, 8, 2), (2.3, 2.2, -8, 3)]
PUP_JUMPER = 2          # this one hops, arms up
ROLYS = [(-2.1, 6.2, (6, -9)), (2.47, 6.2, (4, 8))]
SOLDIERS = [(-4.47, 8.2, False), (-3.43, 9.0, False), (4.47, 8.2, True), (6.1, 9.4, False)]
BOSS = (0.35, 12.5, (5, 3), 1.2)   # x, y, tilt, scale


def pup_run(k):
    """Arms are modelled reaching forward (zombie hug); negative X rotation lifts them."""
    s = 1 if k % 2 else -1
    return {"Pupsik_ArmL": (-30 - 12 * k, 0, 8), "Pupsik_ArmR": (-50 + 9 * k, 0, -8),
            "Pupsik_LegL": (-28 * s, 0, 0), "Pupsik_LegR": (28 * s, 0, 0)}


PUP_JUMP = {"Pupsik_ArmL": (-40, 0, 45), "Pupsik_ArmR": (-45, 0, -45),
            "Pupsik_LegL": (-30, 0, 8), "Pupsik_LegR": (20, 0, -8)}


def arm_throw():
    """Tin soldier's right arm wound up for a throw: upper arm out to the side, forearm (and the
    ball) up next to the head. The arm is modelled hanging down with the forearm forward."""
    m = Matrix(((0.0, 0.0, 1.0), (-1.0, 0.0, 0.0), (0.0, -1.0, 0.0)))   # -Z -> -X, -Y -> +Z
    m = Matrix.Rotation(math.radians(-25), 3, "Y") @ Matrix.Rotation(math.radians(20), 3, "X") @ m
    return tuple(math.degrees(a) for a in m.to_euler("XYZ"))


# ---------------------------------------------------------------- scene plumbing
def _scene(name=SCENE, coll=COLL):
    sc = bpy.data.scenes.get(name) or bpy.data.scenes.new(name)
    col = bpy.data.collections.get(coll)
    if col is None:
        col = bpy.data.collections.new(coll)
    if col.name not in sc.collection.children:
        sc.collection.children.link(col)
    return sc, col


def clear(coll=COLL):
    col = bpy.data.collections.get(coll)
    if col is not None:
        for o in list(col.all_objects):
            bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.lights, bpy.data.cameras):
        for d in list(coll):
            if d.name.startswith(P) and d.users == 0:
                coll.remove(d)


def splash_material():
    """Copy of M_Palette + distance haze (mix toward the horizon colour by camera distance)."""
    mat = bpy.data.materials.get(P + "Palette") or bpy.data.materials.new(P + "Palette")
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    nt.nodes.clear()
    N, Lk = nt.nodes.new, nt.links.new
    out = N("ShaderNodeOutputMaterial"); out.location = (700, 0)
    bsdf = N("ShaderNodeBsdfPrincipled"); bsdf.location = (100, 0)
    tex = N("ShaderNodeTexImage"); tex.location = (-300, 100); tex.name = "Palette"
    tex.image = bpy.data.images[PALETTE]; tex.interpolation = "Closest"
    em = N("ShaderNodeTexImage"); em.location = (-300, -200); em.name = "Emission"
    em.image = bpy.data.images["T_Palette_Emission"]; em.interpolation = "Closest"
    Lk(tex.outputs["Color"], bsdf.inputs["Base Color"])
    Lk(em.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = EMIT
    bsdf.inputs["Roughness"].default_value = 0.85
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    cam = N("ShaderNodeCameraData"); cam.location = (-300, -450)
    mr = N("ShaderNodeMapRange"); mr.location = (-80, -450)
    mr.clamp = True
    mr.inputs["From Min"].default_value = HAZE[1]
    mr.inputs["From Max"].default_value = HAZE[2]
    mr.inputs["To Min"].default_value = 0.0
    mr.inputs["To Max"].default_value = HAZE[3]
    Lk(cam.outputs["View Distance"], mr.inputs["Value"])
    haze = N("ShaderNodeEmission"); haze.location = (100, -300)
    haze.inputs["Color"].default_value = (*srgb(HAZE[0]), 1.0)
    mix = N("ShaderNodeMixShader"); mix.location = (450, 0)
    Lk(mr.outputs["Result"], mix.inputs["Fac"])
    Lk(bsdf.outputs["BSDF"], mix.inputs[1])
    Lk(haze.outputs["Emission"], mix.inputs[2])
    Lk(mix.outputs["Shader"], out.inputs["Surface"])
    return mat


def _link(col, src, parent, tag, mat):
    o = bpy.data.objects.new(P + src.name + tag, src.data)
    if src.type == "EMPTY":
        o.empty_display_type = "PLAIN_AXES"
        o.empty_display_size = 0.3
    col.objects.link(o)
    if parent is not None:
        o.parent = parent
        o.matrix_parent_inverse = src.matrix_parent_inverse.copy()
    o.location = src.location.copy()
    o.rotation_mode = src.rotation_mode
    o.rotation_euler = src.rotation_euler.copy()
    o.scale = src.scale.copy()
    if src.type == "MESH" and mat is not None and len(o.material_slots):
        o.material_slots[0].link = "OBJECT"
        o.material_slots[0].material = mat
    return o


class Placed:
    def __init__(self, root, parts):
        self.root, self.parts = root, parts

    def meshes(self):
        return [o for o in [self.root] + list(self.root.children_recursive) if o.type == "MESH"]


def place(col, mat, name, loc, yaw=None, tilt=(0.0, 0.0), scale=1.0, pose=None, pivot_z=0.0,
          tag="", ground=True, look=None):
    """Linked duplicate of an asset tree. yaw in degrees (None = face `look` point or -Y),
    tilt = (x, y) degrees about the point pivot_z above the base (roly-polys rock on their belly)."""
    src = bpy.data.objects[name]
    parts = {}

    def rec(o, parent):
        n = _link(col, o, parent, tag, mat)
        parts[o.name] = n
        for c in o.children:
            rec(c, n)
        return n

    root = rec(src, None)
    base = Vector(loc)
    if yaw is None:
        tgt = Vector(look) if look is not None else base + Vector((0, -1, 0))
        d = tgt - base
        yaw_r = math.atan2(d.x, -d.y)
    else:
        yaw_r = math.radians(yaw)
    rot = Euler((math.radians(tilt[0]), math.radians(tilt[1]), yaw_r), "XYZ")
    s = scale
    piv = Vector((0, 0, pivot_z * s))
    R = rot.to_matrix()
    root.rotation_mode = "XYZ"
    root.rotation_euler = rot
    root.scale = (s, s, s)
    src_pivot = Vector(src.get("pivot", (0.0, 0.0, 0.0)))
    root.location = base + piv - R @ piv + R @ (src_pivot * s)
    for pname, e in (pose or {}).items():
        o = parts[pname]
        o.rotation_mode = "XYZ"
        o.rotation_euler = Euler([math.radians(v) for v in e], "XYZ")
    pl = Placed(root, parts)
    if ground:
        drop_to_ground(pl)
    return pl


def world_matrix(o):
    """World matrix from the local transforms (matrix_world of a non-active scene is not evaluated)."""
    m = o.matrix_basis.copy()
    while o.parent is not None:
        m = o.parent.matrix_basis @ o.matrix_parent_inverse @ m
        o = o.parent
    return m


def drop_to_ground(pl, z=0.0):
    lo = min((world_matrix(o) @ v.co).z for o in pl.meshes() for v in o.data.vertices)
    pl.root.location.z += z - lo


# ---------------------------------------------------------------- splash-only meshes
def _mesh_from_builder(b, name, col, mat, loc=(0, 0, 0)):
    ob = b.to_object(P + name, col)
    ob.location = loc
    ob.material_slots[0].link = "OBJECT"
    ob.material_slots[0].material = mat
    return ob


def backdrop(col, mat):
    """Big lawn under everything (the yard ground mesh ends at ~50 m)."""
    rnd = random.Random(5)
    b = L.Builder()
    faces = b.grid(700, 700, 35, 35, "green", matrix=L.frame((0, 150, -0.06)))
    b.paint([f for f in faces if rnd.random() < 0.18], "green_light")
    b.paint([f for f in faces if rnd.random() < 0.06], "olive")
    _mesh_from_builder(b, "Backdrop", col, mat)


def cloud(col, mat, name, loc, size, seed):
    rnd = random.Random(seed)
    b = L.Builder()
    n = 5 + rnd.randint(0, 3)
    for i in range(n):
        t = (i / (n - 1) - 0.5) * 2
        r = size * (0.55 + 0.45 * (1 - abs(t))) * rnd.uniform(0.8, 1.1)
        c = (t * size * 1.6, rnd.uniform(-0.3, 0.3) * size, r * 0.35 + rnd.uniform(0, 0.25) * size)
        b.ico(c, r, "white" if rnd.random() < 0.7 else "pink_light", subdiv=1,
              scale=(1.25, 0.9, 0.72), jitter=r * 0.1, seed=rnd.randint(0, 999))
    return _mesh_from_builder(b, name, col, mat, loc)


# ---------------------------------------------------------------- lights, camera, world
def world(sc):
    w = bpy.data.worlds.get(P + "World") or bpy.data.worlds.new(P + "World")
    try:
        w.use_nodes = True
    except Exception:
        pass
    nt = w.node_tree
    nt.nodes.clear()
    N, Lk = nt.nodes.new, nt.links.new
    tc = N("ShaderNodeTexCoord")
    sep = N("ShaderNodeSeparateXYZ")
    Lk(tc.outputs["Generated"], sep.inputs[0])
    mr = N("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = -1.0
    mr.inputs["From Max"].default_value = 1.0
    Lk(sep.outputs["Z"], mr.inputs["Value"])
    ramp = N("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    cr.interpolation = "LINEAR"
    stops = [((z + 1) / 2, h) for z, h in SKY]
    while len(cr.elements) < len(stops):
        cr.elements.new(0.5)
    for el, (pos, h) in zip(cr.elements, stops):
        el.position = pos
        el.color = (*srgb(h), 1.0)
    Lk(mr.outputs["Result"], ramp.inputs["Fac"])
    # warm glow toward the (off-screen) sun
    dot = N("ShaderNodeVectorMath"); dot.operation = "DOT_PRODUCT"
    Lk(tc.outputs["Generated"], dot.inputs[0])
    dot.inputs[1].default_value = tuple(-Vector(SUN_DIR).normalized())
    clampd = N("ShaderNodeMath"); clampd.operation = "MAXIMUM"; clampd.inputs[1].default_value = 0.0
    Lk(dot.outputs["Value"], clampd.inputs[0])
    pw = N("ShaderNodeMath"); pw.operation = "POWER"; pw.inputs[1].default_value = SUN_GLOW[2]
    Lk(clampd.outputs[0], pw.inputs[0])
    amt = N("ShaderNodeMath"); amt.operation = "MULTIPLY"; amt.inputs[1].default_value = SUN_GLOW[1]
    Lk(pw.outputs[0], amt.inputs[0])
    glow = N("ShaderNodeMix"); glow.data_type = "RGBA"; glow.blend_type = "MIX"
    Lk(amt.outputs[0], glow.inputs[0])
    Lk(ramp.outputs["Color"], glow.inputs[6])
    glow.inputs[7].default_value = (*srgb(SUN_GLOW[0]), 1.0)
    sky = glow.outputs[2]
    bg_cam = N("ShaderNodeBackground")
    bg_amb = N("ShaderNodeBackground")
    bg_amb.inputs["Strength"].default_value = AMBIENT
    Lk(sky, bg_cam.inputs["Color"])
    Lk(sky, bg_amb.inputs["Color"])
    lp = N("ShaderNodeLightPath")
    mix = N("ShaderNodeMixShader")
    Lk(lp.outputs["Is Camera Ray"], mix.inputs["Fac"])
    Lk(bg_amb.outputs["Background"], mix.inputs[1])
    Lk(bg_cam.outputs["Background"], mix.inputs[2])
    out = N("ShaderNodeOutputWorld")
    Lk(mix.outputs["Shader"], out.inputs["Surface"])
    sc.world = w


def sun_light(col, name=P + "Sun", direction=SUN_DIR, color=SUN_COLOR, energy=SUN_ENERGY, shadow=True):
    ld = bpy.data.lights.new(name, "SUN")
    ld.energy = energy
    ld.color = srgb(color)
    ld.angle = math.radians(2.5)
    ld.use_shadow = shadow
    sun = bpy.data.objects.new(name, ld)
    col.objects.link(sun)
    d = Vector(direction).normalized()
    sun.rotation_euler = (-d).to_track_quat("Z", "Y").to_euler()
    return sun


def camera(col, sc, name=P + "Cam", loc=CAM_LOC, target=CAM_TARGET, lens=LENS):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.sensor_fit = "HORIZONTAL"
    cd.sensor_width = 36.0
    cd.clip_start = 0.1
    cd.clip_end = 2000.0
    cam = bpy.data.objects.new(name, cd)
    col.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam


# ---------------------------------------------------------------- the set
def build():
    sc, col = _scene()
    clear()
    mat = splash_material()
    look = lambda z=0.0: (CAM_LOC[0], CAM_LOC[1], z)

    # ground: the yard mesh turned so the court's long axis runs along Y (away from the camera)
    place(col, mat, "Env_YardGround", (0, 0, 0), yaw=90, ground=False)
    backdrop(col, mat)

    # balls on the centre line (sizes as in the game prefabs), the red one right in front of us
    for name, x, y, d, rot in BALLS:
        place(col, mat, name, (x, y, 0.0), yaw=rot[2], tilt=rot[:2], scale=d)

    # the toy army
    for i, (x, y, lean, k) in enumerate(PUPSIKS):
        jump = i == PUP_JUMPER
        pl = place(col, mat, "Enemy_Pupsik", (x, y, 0), tilt=(lean, 0), pose=PUP_JUMP if jump else pup_run(k),
                   tag=f".{i}", look=look())
        if jump:
            pl.root.location.z += 0.32
    for i, (x, y, t) in enumerate(ROLYS):
        place(col, mat, "Enemy_RolyPoly", (x, y, 0), tilt=t, pivot_z=0.45, tag=f".{i}", look=look())
    for i, (x, y, throw) in enumerate(SOLDIERS):
        pose = {"TinSoldier_ArmR": arm_throw(), "TinSoldier_ArmL": (-25, 0, 0)} if throw else {}
        place(col, mat, "Enemy_TinSoldier", (x, y, 0), pose=pose, tag=f".{i}", look=look(),
              tilt=(-6, 0) if throw else (0, 0))
    place(col, mat, "Boss_BigRolyPoly", BOSS[:2] + (0,), tilt=BOSS[2], pivot_z=1.35, scale=BOSS[3], look=look())

    # courtyard
    place(col, mat, "Prop_Rocket", (-9.8, 14.5, 0), yaw=25)
    place(col, mat, "Prop_Swings", (10.8, 13.0, 0), yaw=-70)
    place(col, mat, "Prop_Sandbox", (-10.5, 4.5, 0), yaw=10)
    place(col, mat, "Prop_Bench", (8.2, 18.5, 0), yaw=-10)
    place(col, mat, "Prop_Clothesline", (9.6, 20.5, 0), yaw=-25)
    place(col, mat, "Prop_StreetLamp", (6.2, 22.2, 0), yaw=200)
    for i, (x, g) in enumerate([(-17.0, "Prop_Garage_A"), (-13.95, "Prop_Garage_B"), (-10.9, "Prop_Garage_C"),
                                (-7.85, "Prop_Garage_A"), (-4.8, "Prop_Garage_B")]):
        place(col, mat, g, (x, 27.0, 0), yaw=0, tag=f".{i}")
    place(col, mat, "Prop_TrashBin", (-2.6, 24.6, 0), yaw=0)
    for i, (x, y, n) in enumerate([(-1.2, 31.0, "Prop_Tree_Birch"), (4.5, 33.0, "Prop_Tree_Poplar"),
                                   (9.5, 29.0, "Prop_Tree_Poplar"), (15.5, 27.0, "Prop_Tree_Birch"),
                                   (-21.0, 31.0, "Prop_Tree_Poplar"), (21.0, 34.0, "Prop_Tree_Birch")]):
        place(col, mat, n, (x, y, 0), yaw=i * 47, tag=f".{i}")
    for i, (x, y) in enumerate([(1.0, 25.4), (12.0, 25.2), (-19.5, 25.5)]):
        place(col, mat, "Prop_Bush", (x, y, 0), yaw=i * 70, tag=f".{i}")
    for i in range(7):
        place(col, mat, "Prop_Hedge", (3.0 + 2.0 * i, 24.2, 0), yaw=0, tag=f".{i}")
    for i in range(5):
        place(col, mat, "Prop_FenceLow", (-26.0 + 2.0 * i, 24.4, 0), yaw=0, tag=f".{i}")

    # skyline: blocks at the sides, open sky behind the boss
    place(col, mat, "Prop_Panelka_5F", (-44.0, 74.0, 0), yaw=10, tag=".0")
    place(col, mat, "Prop_Panelka_9F", (46.0, 112.0, 0), yaw=-14, tag=".0")
    place(col, mat, "Prop_Panelka_9F", (-95.0, 150.0, 0), yaw=20, tag=".1")
    place(col, mat, "Prop_Panelka_5F", (95.0, 90.0, 0), yaw=-25, tag=".1")

    # clouds low at the sides: the middle of the sky is the logo's
    for i, (x, y, z, s) in enumerate([(-143, 300, 71, 14), (-70, 320, 60, 9), (116, 300, 64, 12),
                                      (160, 280, 104, 11)]):
        cloud(col, mat, f"Cloud.{i}", (x, y, z), s, seed=i + 1)

    world(sc)
    sun_light(col)
    camera(col, sc)
    setup_render(sc)
    return sc


# ---------------------------------------------------------------- app icon
ICON_SCENE = "Icon"
ICON_TAG = "_Icon"                  # name suffix: the same assets are also placed in the Splash scene
ICON_CAM = ((0.3, -5.0, 1.75), (0.1, 0.0, 2.85), 50.0)            # location, target, lens
ICON_BOSS_TILT = (6, 0)                                             # leans toward us
# ball: name, frame position (x, y from the top-left, 0..1), distance from the camera, diameter, rotation
ICON_BALL = ("Ball_Volleyball", (0.25, 0.76), 2.6, 0.54, (25, -35, 60))
ICON_RIM = ((-0.55, -0.75, -0.35), "FFB9C8", 2.2)                   # back light from the right: dir, colour, energy


def frame_point(cam, fx, fy, dist):
    """World point seen at frame position (fx, fy) (0..1 from the top-left) at `dist` metres from a
    square-format camera (loc, target, lens) with a 36 mm sensor."""
    loc, target, lens = Vector(cam[0]), Vector(cam[1]), cam[2]
    rot = (target - loc).to_track_quat("-Z", "Y").to_matrix()
    k = dist * 18.0 / lens
    return loc + rot @ Vector(((fx * 2 - 1) * k, (1 - fy * 2) * k, -dist))


def build_icon():
    """Square close-up for the application icon: the angry boss roly-poly looking down at us and
    a volleyball flying at it. Rendered on transparency (render(path, scene=ICON_SCENE));
    Tools/branding_art.py puts it on the sunset gradient inside the macOS icon shape."""
    sc, col = _scene(ICON_SCENE, ICON_SCENE)
    clear(ICON_SCENE)
    mat = splash_material()
    cam_loc = ICON_CAM[0]
    place(col, mat, "Boss_BigRolyPoly", (0, 0, 0), tilt=ICON_BOSS_TILT, pivot_z=1.35,
          look=(cam_loc[0], cam_loc[1], 0), tag=ICON_TAG)
    name, (fx, fy), dist, d, rot = ICON_BALL
    place(col, mat, name, frame_point(ICON_CAM, fx, fy, dist), yaw=rot[2], tilt=rot[:2], scale=d,
          ground=False, tag=ICON_TAG)
    world(sc)
    sun_light(col, P + "IconSun")
    sun_light(col, P + "IconRim", ICON_RIM[0], ICON_RIM[1], ICON_RIM[2], shadow=False)
    camera(col, sc, P + "IconCam", *ICON_CAM)
    setup_render(sc, res=(2048, 2048))
    sc.render.film_transparent = True
    sc.render.image_settings.color_mode = "RGBA"
    return sc


def setup_render(sc, engine="CYCLES", res=(3840, 2160)):
    sc.render.engine = engine
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    sc.view_settings.exposure = 0.0
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    try:
        sc.eevee.taa_render_samples = 64
    except Exception:
        pass
    if engine == "CYCLES":
        if bpy.app.background:           # command-line run: the preferences may have no GPU picked
            try:
                prefs = bpy.context.preferences.addons["cycles"].preferences
                prefs.compute_device_type = "METAL"
                prefs.get_devices()
                for d in prefs.devices:
                    d.use = True
            except Exception as e:
                print("GPU setup failed:", e)
        sc.cycles.device = "GPU"
        sc.cycles.samples = 512
        sc.cycles.use_denoising = True


def render(path, engine="CYCLES", scale=100, scene=SCENE):
    sc = bpy.data.scenes[scene]
    res = (sc.render.resolution_x, sc.render.resolution_y)
    transparent = sc.render.film_transparent
    setup_render(sc, engine, res)
    sc.render.film_transparent = transparent
    sc.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    sc.render.resolution_percentage = scale
    sc.render.filepath = path
    win = bpy.context.window
    if win is not None:
        prev = win.scene
        win.scene = sc
        try:
            bpy.ops.render.render(write_still=True)
        finally:
            win.scene = prev
    else:
        with bpy.context.temp_override(scene=sc):
            bpy.ops.render.render(write_still=True, scene=sc.name)
    return path

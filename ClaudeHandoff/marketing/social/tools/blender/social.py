"""Social media renders on the splash set (sunset yard, the toy army behind): vertical kid intros, kid cards,
key art with the kids from behind, boss posters. The .blend is never saved.
Run: Blender -b Bouncer.blend --python social.py -- <job> [percent] [out_dir]
jobs: kids_card   4:5 stills, each kid in the select pose            -> <out>/kid_card_<Kid>.png
      kids_anim   9:16 PNG sequences, 30 fps, 2 s per kid             -> <out>/kids_v/<Kid>/0000.png
      key_still   key art, 4:5 and 9:16                               -> <out>/key_4x5.png, key_9x16.png
      key_anim    9:16 push-in, 4 s, 30 fps                           -> <out>/key_v/0000.png
      boss[:Name] 4:5 boss posters (all, or one by object name)       -> <out>/boss_<Name>.png
percent < 100 renders a quick framing draft."""
import bpy, sys, os, math
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
JOB = args[0] if args else "kids_card"
PCT = int(args[1]) if len(args) > 1 else 100
OUT = args[2] if len(args) > 2 else os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
DRAFT = PCT < 100

for mod in ("ba_lib", "ba_kids", "ba_branding"):
    if mod not in sys.modules:
        sys.modules[mod] = bpy.data.texts[mod + ".py"].as_module()
import ba_branding as B

ACTION_FPS = 30
FPS = 30
V = (1080, 1920)        # 9:16
P = (1080, 1350)        # 4:5
PC = (1440, 1800)       # 4:5 with room to crop (cards, posters)

# kid, select pose, prop, prop bone, offset (unity kid axes), yaw; camera orbit from -> to (angle deg, dist m, height m)
KIDS = [
    dict(name="Kid_Otlichnik", action="Pose_Think", prop="Prop_Book", bone="LeftHand", off=(0, -0.02, 0.06), yaw=-8,
         cam_from=(-16, 3.2, 1.05), cam_to=(-7, 2.45, 0.85)),
    dict(name="Kid_Tolstyak", action="Pose_Cheer", prop="Prop_Chips", bone="LeftHand", off=(0, -0.02, 0.05), yaw=6,
         cam_from=(14, 3.2, 0.6), cam_to=(6, 2.45, 0.75)),
    dict(name="Kid_Melkaya", action="Pose_Ready", prop="Prop_JumpRope", bone="RightHand", off=(0, -0.01, 0.0), yaw=-22,
         cam_from=(-18, 3.1, 0.5), cam_to=(-8, 2.4, 0.7)),
    dict(name="Kid_Huligan", action="Pose_Tough", prop="Prop_Football", bone="RightFoot", off=(0, 0.11, 0.05), yaw=8,
         cam_from=(12, 2.9, 0.45), cam_to=(5, 2.5, 0.75)),
]
KID_POS = Vector((0.0, -2.0, 0.0))
KID_LOOK_Z = 0.62        # look point on the kid: below the chest, so the kid sits in the upper part of the frame
KID_DUR = 2.0

# key art: the four kids from behind on our half, the toys across the line, sky for the logo
KEY_KIDS = [("Kid_Otlichnik", -1.3, -2.35, 172), ("Kid_Tolstyak", -0.45, -2.6, 178),
            ("Kid_Melkaya", 0.45, -2.5, 184), ("Kid_Huligan", 1.3, -2.3, 190)]

# bosses: object, scale, tilt, pivot_z (roly-polys rock on their belly), yaw offset
BOSSES = [
    ("Boss_BigRolyPoly", 1.0, (5, 3), 1.35, 0),
    ("Boss_Transformer", 1.0, (0, 0), 0.0, -12),
    ("Boss_Hare", 1.0, (0, 0), 0.0, 10),
    ("Boss_Fizruk", 1.0, (0, 0), 0.0, -8),
    ("Boss_Dusk", 1.0, (0, 0), 0.0, 8),
]
BOSS_POS = Vector((0.0, 3.6, 0.0))


def unity_to_blender(v):
    return Vector((-v[0], -v[2], v[1]))


def setup(res, samples):
    sc = bpy.context.scene
    for lc in sc.view_layers[0].layer_collection.children:
        lc.exclude = lc.name not in {"Kids", "KidProps", "Splash", "SOC_Boss"}
    splash = bpy.data.collections["Splash"]
    if splash.name not in sc.collection.children:
        sc.collection.children.link(splash)
    sc.world = bpy.data.worlds["SPL_World"]
    B.setup_render(sc, res=res)
    sc.cycles.samples = 24 if DRAFT else samples
    sc.cycles.use_denoising = True
    try:
        sc.cycles.denoiser = "OPENIMAGEDENOISE"
    except Exception:
        pass
    sc.render.use_persistent_data = True
    sc.render.resolution_percentage = PCT
    mat = bpy.data.materials["SPL_Palette"]
    for col in ("Kids", "KidProps"):
        for o in bpy.data.collections[col].all_objects:
            o.hide_render = True
            if o.type == "MESH":
                for slot in o.material_slots:
                    if slot.material and slot.material.name == "M_Palette":
                        slot.link = "OBJECT"
                        slot.material = mat
    cd = bpy.data.cameras.new("SOC_Cam")
    cd.lens = 35.0
    cd.sensor_fit = "AUTO"
    cd.sensor_width = 36.0
    cd.clip_start = 0.05
    cd.clip_end = 2000.0
    cd.dof.use_dof = True
    cd.dof.aperture_fstop = 2.2
    cam = bpy.data.objects.new("SOC_Cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam, mat


def look_at(cam, target):
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()


def render_to(sc, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("RENDERED", path, flush=True)


# ---------------------------------------------------------------- kids
def bind_action(rig, name):
    rig.animation_data_create()
    act = bpy.data.actions[name]
    rig.animation_data.action = act
    try:
        if act.slots and rig.animation_data.action_slot is None:
            rig.animation_data.action_slot = act.slots[0]
    except AttributeError:
        pass


def show_kid(k, pos=KID_POS, yaw=None, action=None, prop=True):
    rig = bpy.data.objects[k["name"]]
    mesh = bpy.data.objects[k["name"] + "_Mesh"]
    rig.hide_render = mesh.hide_render = False
    rig.location = pos
    rig.rotation_mode = "XYZ"
    rig.rotation_euler = (0, 0, math.radians(k["yaw"] if yaw is None else yaw))
    bind_action(rig, action or k["action"])
    pr = None
    if prop:
        pr = bpy.data.objects[k["prop"]]
        pr.hide_render = False
        for o in pr.children_recursive:
            o.hide_render = False
        pr.parent = None
        pr.rotation_mode = "XYZ"
        pr.rotation_euler = (0, 0, math.radians(k["yaw"]))
    return rig, pr


def hide_kid(k):
    for n in (k["name"], k["name"] + "_Mesh", k["prop"]):
        bpy.data.objects[n].hide_render = True
    for o in bpy.data.objects[k["prop"]].children_recursive:
        o.hide_render = True


def place_prop(k, rig, prop):
    pb = rig.pose.bones[k["bone"]]
    head = rig.matrix_world @ pb.head
    off = Matrix.Rotation(math.radians(k["yaw"]), 3, "Z") @ unity_to_blender(k["off"])
    if k["bone"].endswith("Foot"):
        prop.location = Vector((head.x + off.x, head.y + off.y, k["off"][1]))
    else:
        prop.location = head + off


def kid_pose_time(sc, t):
    f = t * ACTION_FPS
    sc.frame_set(int(f), subframe=f - int(f))


def kid_camera(cam, k, u):
    ang, dist, h = [a + (b - a) * u for a, b in zip(k["cam_from"], k["cam_to"])]
    r = math.radians(ang)
    cam.location = KID_POS + Vector((math.sin(r) * dist, -math.cos(r) * dist, h))
    look_at(cam, KID_POS + Vector((0, 0, KID_LOOK_Z)))
    cam.data.dof.focus_distance = (KID_POS + Vector((0, 0, 0.8)) - cam.location).length


def kids_card():
    sc, cam, _ = setup(PC, 256)
    cam.data.dof.aperture_fstop = 2.0
    for k in KIDS:
        rig, prop = show_kid(k)
        kid_pose_time(sc, KID_DUR)
        place_prop(k, rig, prop)
        kid_camera(cam, k, 1.0)
        render_to(sc, os.path.join(OUT, f"kid_card_{k['name']}.png"))
        hide_kid(k)


def kids_anim():
    sc, cam, _ = setup(V, 64)
    only = JOB.split(":", 1)[1] if ":" in JOB else None
    for k in KIDS:
        if only and only not in k["name"]:
            continue
        rig, prop = show_kid(k)
        n = 1 if DRAFT else int(round(KID_DUR * FPS))
        for i in range(n):
            t = KID_DUR if DRAFT else i / FPS
            kid_pose_time(sc, t)
            place_prop(k, rig, prop)
            x = min(1.0, t / KID_DUR)
            kid_camera(cam, k, 1 - (1 - x) ** 2.2)
            render_to(sc, os.path.join(OUT, "kids_v", k["name"], f"{i:04d}.png"))
        hide_kid(k)


# ---------------------------------------------------------------- key art
def key_setup(res):
    sc, cam, _ = setup(res, 128)
    for name, x, y, yaw in KEY_KIDS:
        k = next(k for k in KIDS if k["name"] == name)
        show_kid(k, Vector((x, y, 0)), yaw=yaw, action="Idle", prop=False)
    cam.data.dof.aperture_fstop = 5.6
    return sc, cam


def key_camera(cam, u, res):
    # vertical: further back and lower, wide lens, the logo goes into the sky
    if res == V:
        a, b = Vector((0.0, -8.4, 0.5)), Vector((0.0, -7.5, 0.65))
    else:
        a, b = Vector((0.0, -7.2, 0.55)), Vector((0.0, -6.4, 0.7))
    cam.data.lens = 24.0 if res == V else 28.0
    cam.location = a.lerp(b, u)
    look_at(cam, Vector((0.15, 10.0, 2.6 if res == V else 2.2)))
    cam.data.dof.focus_distance = (Vector((0, 2.0, 1.0)) - cam.location).length


def jumper():
    j = [o for o in bpy.data.collections["Splash"].objects if o.name.startswith("SPL_Enemy_Pupsik.2") and o.parent is None]
    return j[0] if j else None


def key_still():
    for res, name in ((P, "key_4x5.png"), (V, "key_9x16.png")):
        sc, cam = key_setup(res)
        kid_pose_time(sc, 0.5)
        key_camera(cam, 1.0, res)
        render_to(sc, os.path.join(OUT, name))
        bpy.data.objects.remove(cam, do_unlink=True)


def key_anim():
    sc, cam = key_setup(V)
    jp = jumper()
    base_z = jp.location.z if jp else 0.0
    dur = 4.0
    n = 1 if DRAFT else int(round(dur * FPS))
    for i in range(n):
        t = i / FPS
        kid_pose_time(sc, t)
        key_camera(cam, 1 - (1 - min(1.0, t / dur)) ** 3, V)
        if jp is not None:
            jp.location.z = base_z - 0.32 + 0.45 * abs(math.sin(math.pi * t / 0.55))
        render_to(sc, os.path.join(OUT, "key_v", f"{i:04d}.png"))


# ---------------------------------------------------------------- bosses
def hide_tree(o):
    for c in [o] + list(o.children_recursive):
        c.hide_render = True


def boss_posters():
    sc, cam, mat = setup(PC, 256)
    cam.data.dof.aperture_fstop = 3.2
    col = bpy.data.collections.new("SOC_Boss")
    sc.collection.children.link(col)
    # a pink rim from behind separates the boss from the sky, a cool fill opens the shaded face
    B.sun_light(col, "SOC_Rim", (-0.45, -0.82, -0.35), "FFB9C8", 2.6, shadow=False)
    B.sun_light(col, "SOC_Fill", (0.25, 0.93, -0.25), "B9C8FF", 0.9, shadow=False)
    # the boss stands in front of the army: the splash boss and the front pupsiks go
    for o in bpy.data.collections["Splash"].objects:
        if o.parent is None and (o.name.startswith("SPL_Boss_") or o.name.startswith("SPL_Enemy_Pupsik")):
            hide_tree(o)
    only = JOB.split(":", 1)[1] if ":" in JOB else None
    for name, scale, tilt, pivot, yaw_off in BOSSES:
        if only and only != name:
            continue
        src = bpy.data.objects[name]
        h = max((c.matrix_world @ v.co).z for c in [src] + list(src.children_recursive) if c.type == "MESH"
                for v in c.data.vertices) * scale
        dist = 1.45 * h
        cam_loc = BOSS_POS + Vector((0.9, -dist, 0.45))
        cam.location = cam_loc
        cam.data.lens = 35.0
        pl = B.place(col, mat, name, BOSS_POS, tilt=tilt, pivot_z=pivot, scale=scale, tag="_SOC",
                     look=(cam_loc.x, cam_loc.y, 0))
        pl.root.rotation_euler.z += math.radians(yaw_off)
        for part in pl.root.children_recursive:
            if part.name.startswith("SPL_Hare_Stuffing"):
                part.hide_render = True          # the seam rips only at half health
        look_at(cam, BOSS_POS + Vector((0, 0, h * 0.47)))
        cam.data.dof.focus_distance = (BOSS_POS + Vector((0, -0.5, h * 0.5)) - cam.location).length
        render_to(sc, os.path.join(OUT, f"boss_{name}.png"))
        for o in [pl.root] + list(pl.root.children_recursive):
            bpy.data.objects.remove(o, do_unlink=True)


JOBS = {"kids_card": kids_card, "kids_anim": kids_anim, "key_still": key_still, "key_anim": key_anim,
        "boss": boss_posters}
JOBS[JOB.split(":", 1)[0]]()
print("DONE")

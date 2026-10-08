"""Trailer: the four kids introduced one by one on the splash court (sunset, the toy army blurred behind).
Run: Blender -b Bouncer.blend --python kids_shot.py -- <mode> [kid]
mode: still  -> one low-res frame per kid (framing check)
      anim   -> PNG sequence at 60 fps for the given kid (or all)
The .blend is never saved."""
import bpy, sys, os, math
from mathutils import Vector, Matrix, Euler

OUT = "/private/tmp/claude-501/-Users-bekbolataldiyarov-Desktop-projects-Game-Projects-Bouncer/f820830a-a4f1-413d-9c37-928eb1812c7d/scratchpad/trailer/blender/kids"
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = args[0] if args else "still"
ONLY = args[1] if len(args) > 1 else None

for mod in ("ba_lib", "ba_kids", "ba_branding"):
    if mod not in sys.modules:
        sys.modules[mod] = bpy.data.texts[mod + ".py"].as_module()
import ba_branding as B

FPS_OUT = 60
ACTION_FPS = 30
DUR = 1.4

# kid, action, prop, prop bone, offset (unity kid axes x right, y up, z forward), camera setup
# camera: (orbit angle deg around the kid, 0 = straight in front; distance m; height m) from -> to,
# look = point on the kid (z) and sideways shift (+ = kid moves to the right third of the frame)
KIDS = [
    dict(name="Kid_Otlichnik", action="Pose_Think", prop="Prop_Book", bone="LeftHand", off=(0, -0.02, 0.06),
         cam_from=(-14, 3.1, 0.95), cam_to=(-6, 2.5, 1.0), look_z=0.85, shift=0.42, yaw=-8),
    dict(name="Kid_Tolstyak", action="Pose_Cheer", prop="Prop_Chips", bone="LeftHand", off=(0, -0.02, 0.05),
         cam_from=(12, 3.1, 0.7), cam_to=(5, 2.5, 0.8), look_z=0.9, shift=-0.42, yaw=6),
    dict(name="Kid_Melkaya", action="Pose_Ready", prop="Prop_JumpRope", bone="RightHand", off=(0, -0.01, 0.0),
         cam_from=(-18, 3.0, 0.55), cam_to=(-8, 2.4, 0.7), look_z=0.75, shift=0.42, yaw=-25),
    dict(name="Kid_Huligan", action="Pose_Tough", prop="Prop_Football", bone="RightFoot", off=(0, 0.11, 0.05),
         cam_from=(10, 2.7, 0.45), cam_to=(4, 3.2, 0.8), look_z=0.75, shift=-0.42, yaw=8),
]
KID_POS = Vector((0.0, -2.0, 0.0))


def unity_to_blender(v):
    """Unity kid axes (x right, y up, z forward) -> Blender for a kid facing -Y (right = -X)."""
    return Vector((-v[0], -v[2], v[1]))


def setup():
    sc = bpy.context.scene
    vl = sc.view_layers[0]
    keep = {"Kids", "KidProps"}
    for lc in vl.layer_collection.children:
        lc.exclude = lc.name not in keep
    splash = bpy.data.collections["Splash"]
    if splash.name not in sc.collection.children:
        sc.collection.children.link(splash)
    sc.world = bpy.data.worlds["SPL_World"]
    B.setup_render(sc, res=(1920, 1080))
    sc.cycles.samples = 64 if MODE == "anim" else 24
    sc.render.use_persistent_data = True
    sc.cycles.use_denoising = True
    try:
        sc.cycles.denoiser = "OPENIMAGEDENOISE"
    except Exception:
        pass
    if MODE == "still":
        sc.render.resolution_percentage = 50
    sc.camera = None
    mat = bpy.data.materials["SPL_Palette"]
    for col in ("Kids", "KidProps"):
        for o in bpy.data.collections[col].all_objects:
            o.hide_render = True
            if o.type == "MESH":
                for slot in o.material_slots:
                    if slot.material and slot.material.name == "M_Palette":
                        slot.link = "OBJECT"
                        slot.material = mat
    # the splash camera stays, ours renders
    cd = bpy.data.cameras.new("TRL_Cam")
    cd.lens = 35.0
    cd.sensor_width = 36.0
    cd.clip_start = 0.05
    cd.clip_end = 2000.0
    cd.dof.use_dof = True
    cd.dof.aperture_fstop = 2.2
    cam = bpy.data.objects.new("TRL_Cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc, cam


def show_kid(k):
    rig = bpy.data.objects[k["name"]]
    mesh = bpy.data.objects[k["name"] + "_Mesh"]
    prop = bpy.data.objects[k["prop"]]
    for o in (rig, mesh, prop):
        o.hide_render = False
    for o in prop.children_recursive:
        o.hide_render = False
    rig.location = KID_POS
    rig.rotation_mode = "XYZ"
    rig.rotation_euler = (0, 0, math.radians(k["yaw"]))
    rig.animation_data_create()
    act = bpy.data.actions[k["action"]]
    rig.animation_data.action = act
    try:
        if act.slots and rig.animation_data.action_slot is None:
            rig.animation_data.action_slot = act.slots[0]
    except AttributeError:
        pass
    prop.parent = None
    prop.rotation_mode = "XYZ"
    prop.rotation_euler = (0, 0, math.radians(k["yaw"]))
    return rig, mesh, prop


def hide_kid(k):
    for n in (k["name"], k["name"] + "_Mesh", k["prop"]):
        bpy.data.objects[n].hide_render = True
    for o in bpy.data.objects[k["prop"]].children_recursive:
        o.hide_render = True


def place_prop(k, rig, prop):
    pb = rig.pose.bones[k["bone"]]
    head = rig.matrix_world @ pb.head
    yaw = Matrix.Rotation(math.radians(k["yaw"]), 3, "Z")
    off = yaw @ unity_to_blender(k["off"])
    if k["bone"].endswith("Foot"):
        prop.location = Vector((head.x + off.x, head.y + off.y, k["off"][1]))
    else:
        prop.location = head + off


def set_time(sc, t, k, rig, prop, cam):
    f = t * ACTION_FPS
    sc.frame_set(int(f), subframe=f - int(f))
    place_prop(k, rig, prop)
    x = min(1.0, max(0.0, t / DUR))
    u = 1 - (1 - x) ** 2                      # ease-out: the move settles on the pose
    ang, dist, h = [a + (b - a) * u for a, b in zip(k["cam_from"], k["cam_to"])]
    r = math.radians(ang)
    cam.location = KID_POS + Vector((math.sin(r) * dist, -math.cos(r) * dist, h))
    chest = KID_POS + Vector((0, 0, k["look_z"]))
    fwd = (chest - cam.location).normalized()
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    look = chest - right * k["shift"]
    cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.dof.focus_distance = (chest - cam.location).length


def main():
    sc, cam = setup()
    os.makedirs(OUT, exist_ok=True)
    for k in KIDS:
        if ONLY and ONLY not in k["name"]:
            continue
        rig, mesh, prop = show_kid(k)
        if MODE == "still":
            for t in (0.0, DUR):
                set_time(sc, t, k, rig, prop, cam)
                sc.render.filepath = os.path.join(OUT, f"still_{k['name']}_{t:.1f}.png")
                bpy.ops.render.render(write_still=True)
        else:
            d = os.path.join(OUT, k["name"])
            os.makedirs(d, exist_ok=True)
            n = int(round(DUR * FPS_OUT))
            for i in range(n):
                set_time(sc, i / FPS_OUT, k, rig, prop, cam)
                sc.render.filepath = os.path.join(d, f"{i:04d}.png")
                bpy.ops.render.render(write_still=True)
                print("FRAME", k["name"], i, flush=True)
        hide_kid(k)
    print("DONE")


main()

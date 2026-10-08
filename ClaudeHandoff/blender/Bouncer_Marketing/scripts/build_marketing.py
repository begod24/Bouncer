"""Builds the marketing .blend files next to Bouncer.blend: one file per series, one scene per shot.
Assets are LINKED from ../Bouncer.blend (local objects on linked mesh/armature data, the way ba_branding.place
works), so a model fixed in Bouncer.blend shows up here on the next open. Bouncer.blend itself is never touched.

Run (Blender closed or open, it doesn't matter — this works in the background):
  /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python scripts/build_marketing.py -- all
  ... -- KeyArt | Kids | Bosses        (one file)
Rebuilding overwrites that file, so hand-made changes in it are lost: add new shots here instead, or in a new file.

Render a shot:  Blender -b Kids.blend -S KidIntro_9x16_Otlichnik -a        (animation, PNGs to //renders/<scene>/)
                Blender -b Bosses.blend -S BossPoster_4x5_Hare -f 0        (still)"""
import bpy, sys, os, math
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))          # Bouncer_Marketing/
LIB = os.path.normpath(os.path.join(HERE, "..", "Bouncer.blend"))
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["all"]
WHICH = args[0] if args else "all"

FPS = 30
V, P, PC, WIDE = (1080, 1920), (1080, 1350), (1440, 1800), (1920, 1080)

# ---------------------------------------------------------------- shared data
KIDS = [
    dict(name="Kid_Otlichnik", short="Otlichnik", action="Pose_Think", prop="Prop_Book", bone="LeftHand",
         off=(0, -0.02, 0.06), yaw=-8, cam_from=(-16, 3.2, 1.05), cam_to=(-7, 2.45, 0.85),
         trl=dict(cam_from=(-14, 3.1, 0.95), cam_to=(-6, 2.5, 1.0), look_z=0.85, shift=0.42)),
    dict(name="Kid_Tolstyak", short="Tolstyak", action="Pose_Cheer", prop="Prop_Chips", bone="LeftHand",
         off=(0, -0.02, 0.05), yaw=6, cam_from=(14, 3.2, 0.6), cam_to=(6, 2.45, 0.75),
         trl=dict(cam_from=(12, 3.1, 0.7), cam_to=(5, 2.5, 0.8), look_z=0.9, shift=-0.42)),
    dict(name="Kid_Melkaya", short="Melkaya", action="Pose_Ready", prop="Prop_JumpRope", bone="RightHand",
         off=(0, -0.01, 0.0), yaw=-22, cam_from=(-18, 3.1, 0.5), cam_to=(-8, 2.4, 0.7),
         trl=dict(cam_from=(-18, 3.0, 0.55), cam_to=(-8, 2.4, 0.7), look_z=0.75, shift=0.42)),
    dict(name="Kid_Huligan", short="Huligan", action="Pose_Tough", prop="Prop_Football", bone="RightFoot",
         off=(0, 0.11, 0.05), yaw=8, cam_from=(12, 2.9, 0.45), cam_to=(5, 2.5, 0.75),
         trl=dict(cam_from=(10, 2.7, 0.45), cam_to=(4, 3.2, 0.8), look_z=0.75, shift=-0.42)),
]
KID_POS = Vector((0.0, -2.0, 0.0))
KID_LOOK_Z = 0.62
KID_FRAMES = 60            # social intro: 2 s at 30 fps, the card is its last pose (frame 60)
TRL_FRAMES = 42            # trailer intro: 1.4 s (the trailer used 60 fps; re-renders here are 30 fps)

KEY_KIDS = [("Kid_Otlichnik", -1.3, -2.35, 172), ("Kid_Tolstyak", -0.45, -2.6, 178),
            ("Kid_Melkaya", 0.45, -2.5, 184), ("Kid_Huligan", 1.3, -2.3, 190)]
END_KIDS = [("Kid_Otlichnik", -1.75, -2.35, 172), ("Kid_Tolstyak", -0.6, -2.6, 178),
            ("Kid_Melkaya", 0.6, -2.5, 184), ("Kid_Huligan", 1.75, -2.3, 190)]
KEY_FRAMES = 120

BOSSES = [("Boss_BigRolyPoly", "BigRolyPoly", (5, 3), 1.35, 0), ("Boss_Transformer", "Transformer", (0, 0), 0.0, -12),
          ("Boss_Hare", "Hare", (0, 0), 0.0, 10), ("Boss_Fizruk", "Fizruk", (0, 0), 0.0, -8),
          ("Boss_Dusk", "Dusk", (0, 0), 0.0, 8)]
BOSS_POS = Vector((0.0, 3.6, 0.0))


# ---------------------------------------------------------------- plumbing
def fresh_file():
    bpy.ops.wm.read_homefile(use_empty=True)
    for m in list(sys.modules):
        if m.startswith("ba_"):
            del sys.modules[m]


def link_assets():
    with bpy.data.libraries.load(LIB, link=True, relative=True) as (src, dst):
        dst.collections = [c for c in ("Enemies", "Yard", "Balls", "Kids", "KidProps") if c in src.collections]
        dst.images = [i for i in src.images if i.startswith("T_Palette") or i == "T_Decals"]
        dst.texts = ["ba_lib.py", "ba_branding.py"]
        dst.actions = list(src.actions)                     # the kids' clips (Idle, Pose_*, Throw...)
    for mod in ("ba_lib", "ba_branding"):
        sys.modules[mod] = bpy.data.texts[mod + ".py"].as_module()
    return sys.modules["ba_branding"]


def build_set(B, front=True):
    """The splash set (sunset yard, the toy army) as local objects on linked meshes. The front of the army
    (pupsiks and the big roly-poly) goes into its own collection so posters can leave it out."""
    sc = B.build()                     # scene 'Splash' + collection 'Splash'
    splash = bpy.data.collections["Splash"]
    front_col = bpy.data.collections.new("Splash_Front")
    for o in list(splash.objects):
        if o.parent is None and (o.name.startswith("SPL_Boss_") or o.name.startswith("SPL_Enemy_Pupsik")):
            for p in [o] + list(o.children_recursive):
                front_col.objects.link(p)
                splash.objects.unlink(p)
    return sc, splash, front_col


def new_scene(name, res, cols, cam, samples, frames=None, still_frame=0, B=None):
    sc = bpy.data.scenes.new(name)
    for c in cols:
        sc.collection.children.link(c)
    sc.collection.objects.link(cam)
    sc.camera = cam
    sc.world = bpy.data.worlds["SPL_World"]
    B.setup_render(sc, res=res)
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    try:
        sc.cycles.denoiser = "OPENIMAGEDENOISE"
    except Exception:
        pass
    sc.render.fps = FPS
    if frames:
        sc.frame_start, sc.frame_end = 0, frames - 1
        sc.render.filepath = f"//renders/{name}/"
    else:
        sc.frame_start = sc.frame_end = still_frame
        sc.render.filepath = f"//renders/{name}_"
    sc.frame_current = still_frame
    sc.render.use_file_extension = True
    return sc


def camera(name, lens=35.0, fstop=2.2, focus=None):
    cd = bpy.data.cameras.new(name)
    cd.lens = lens
    cd.sensor_fit = "AUTO"
    cd.sensor_width = 36.0
    cd.clip_start, cd.clip_end = 0.05, 2000.0
    cd.dof.use_dof = True
    cd.dof.aperture_fstop = fstop
    if focus is not None:
        cd.dof.focus_object = focus
    return bpy.data.objects.new(name, cd)


def empty(name, loc, col):
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "SPHERE"
    e.empty_display_size = 0.08
    e.location = loc
    col.objects.link(e)
    return e


def track(cam, target):
    c = cam.constraints.new("TRACK_TO")
    c.target = target
    c.track_axis = "TRACK_NEGATIVE_Z"
    c.up_axis = "UP_Y"


def linear_keys(obj):
    ad = obj.animation_data
    if not ad or not ad.action:
        return
    try:
        curves = ad.action.fcurves
    except AttributeError:
        curves = [fc for layer in ad.action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]
    for fc in curves:
        for kp in fc.keyframe_points:
            kp.interpolation = "LINEAR"


def bind_action(rig, name):
    rig.animation_data_create()
    act = bpy.data.actions[name]
    rig.animation_data.action = act
    try:
        if act.slots and rig.animation_data.action_slot is None:
            rig.animation_data.action_slot = act.slots[0]
    except AttributeError:
        pass


def make_kid(k, col, pos, yaw, action, mat, tag):
    """Local armature + skinned mesh on the linked kid data."""
    src = bpy.data.objects[k["name"]]
    src_mesh = bpy.data.objects[k["name"] + "_Mesh"]
    rig = bpy.data.objects.new(k["name"] + tag, src.data)
    col.objects.link(rig)
    rig.location = pos
    rig.rotation_mode = "XYZ"
    rig.rotation_euler = (0, 0, math.radians(yaw))
    m = bpy.data.objects.new(k["name"] + "_Mesh" + tag, src_mesh.data)
    col.objects.link(m)
    m.parent = rig
    m.matrix_parent_inverse = src_mesh.matrix_parent_inverse.copy()
    m.matrix_basis = src_mesh.matrix_basis.copy()
    mod = m.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    for slot in m.material_slots:
        if slot.material and slot.material.name.startswith("M_Palette"):
            slot.link = "OBJECT"
            slot.material = mat
    bind_action(rig, action)
    return rig


def unity_to_blender(v):
    return Vector((-v[0], -v[2], v[1]))


def bake_prop(sc, k, rig, prop, frames):
    """Prop follows the hand/foot bone head (as the trailer did), keyed every frame."""
    yawm = Matrix.Rotation(math.radians(k["yaw"]), 3, "Z")
    prop.rotation_mode = "XYZ"
    prop.rotation_euler = (0, 0, math.radians(k["yaw"]))
    for f in frames:
        sc.frame_set(f)
        head = rig.matrix_world @ rig.pose.bones[k["bone"]].head
        off = yawm @ unity_to_blender(k["off"])
        if k["bone"].endswith("Foot"):
            prop.location = Vector((head.x + off.x, head.y + off.y, k["off"][1]))
        else:
            prop.location = head + off
        prop.keyframe_insert("location", frame=f)
    linear_keys(prop)


def save(name):
    path = os.path.join(HERE, name + ".blend")
    for sc in [s for s in bpy.data.scenes if s.name == "Scene" or (s.name == "Splash" and name != "KeyArt")]:
        try:
            bpy.data.scenes.remove(sc)
        except Exception as e:
            print("keep build scene:", e)
            sc.name = "_build"
    bpy.ops.wm.save_as_mainfile(filepath=path, relative_remap=True, compress=True)
    print("SAVED", path, [s.name for s in bpy.data.scenes], flush=True)


# ---------------------------------------------------------------- KeyArt.blend
def build_keyart():
    fresh_file()
    B = link_assets()
    splash_sc, splash, front = build_set(B)
    mat = bpy.data.materials["SPL_Palette"]
    # the splash art of the game itself stays as a scene (the jumper hops: frame 4 ~ the built height)
    splash_sc.name = "Splash_16x9"
    splash_sc.collection.children.link(front)
    splash_sc.cycles.samples = 512
    splash_sc.render.filepath = "//renders/Splash_16x9_"
    jumper = next(o for o in front.objects if o.name.startswith("SPL_Enemy_Pupsik.2") and o.parent is None)
    base_z = jumper.location.z
    for f in range(KEY_FRAMES):
        t = f / FPS
        jumper.location.z = base_z - 0.32 + 0.45 * abs(math.sin(math.pi * t / 0.55))
        jumper.keyframe_insert("location", index=2, frame=f)
    linear_keys(jumper)
    splash_sc.frame_current = 4

    def kids_from_behind(colname, layout):
        col = bpy.data.collections.new(colname)
        for name, x, y, yaw in layout:
            k = next(k for k in KIDS if k["name"] == name)
            make_kid(k, col, Vector((x, y, 0)), yaw, "Idle", mat, "_" + colname)
        return col

    key_kids = kids_from_behind("KeyKids", KEY_KIDS)
    end_kids = kids_from_behind("EndCardKids", END_KIDS)
    rig_col = bpy.data.collections.new("KeyArt_Rigs")
    focus = empty("Focus_KeyArt", (0, 2.0, 1.0), rig_col)

    def push_cam(name, a, b, target, lens, frames):
        cam = camera(name, lens, 5.6, focus)
        tgt = empty("Look_" + name, target, rig_col)
        track(cam, tgt)
        for f in range(frames):
            u = 1 - (1 - min(1.0, f / frames)) ** 3
            cam.location = a.lerp(b, u)
            cam.keyframe_insert("location", frame=f)
        linear_keys(cam)
        return cam

    # 4:5 still = the end of the push-in; 9:16 animated push-in (reels end card); the trailer end card 16:9
    c45 = push_cam("Cam_KeyArt_4x5", Vector((0.0, -7.2, 0.55)), Vector((0.0, -6.4, 0.7)), (0.15, 10.0, 2.2), 28.0, KEY_FRAMES)
    sc = new_scene("KeyArt_4x5", P, [splash, front, key_kids, rig_col], c45, 128, still_frame=KEY_FRAMES - 1, B=B)
    c916 = push_cam("Cam_KeyArt_9x16", Vector((0.0, -8.4, 0.5)), Vector((0.0, -7.5, 0.65)), (0.15, 10.0, 2.6), 24.0, KEY_FRAMES)
    new_scene("KeyArt_9x16", V, [splash, front, key_kids, rig_col], c916, 64, frames=KEY_FRAMES, B=B)
    ce = push_cam("Cam_EndCard_16x9", Vector((0.0, -6.9, 0.68)), Vector((0.0, -6.1, 0.8)),
                  tuple(Vector(B.CAM_TARGET) + Vector((0, 0, 1.6))), B.LENS, KEY_FRAMES)
    new_scene("Trailer_EndCard_16x9", WIDE, [splash, front, end_kids, rig_col], ce, 64, frames=KEY_FRAMES, B=B)
    save("KeyArt")


# ---------------------------------------------------------------- Kids.blend
def build_kids():
    fresh_file()
    B = link_assets()
    splash_sc, splash, front = build_set(B)
    mat = bpy.data.materials["SPL_Palette"]
    for k in KIDS:
        col = bpy.data.collections.new(k["short"])
        rig = make_kid(k, col, KID_POS, k["yaw"], k["action"], mat, "")
        pr = B.place(col, mat, k["prop"], (0, 0, 0), yaw=k["yaw"], ground=False)
        prop = pr.root
        look = empty("Look_" + k["short"], KID_POS + Vector((0, 0, KID_LOOK_Z)), col)
        focus = empty("Focus_" + k["short"], KID_POS + Vector((0, 0, 0.8)), col)
        # social intro 9:16 and the 4:5 card on its last frame share one camera
        cam = camera("Cam_KidIntro_" + k["short"], 35.0, 2.2, focus)
        track(cam, look)
        # props are keyed in the build scene (the context one evaluates poses back into the objects)
        tmp = bpy.context.scene
        tmp.collection.children.link(col)
        bake_prop(tmp, k, rig, prop, range(KID_FRAMES + 1))
        tmp.collection.children.unlink(col)
        for f in range(KID_FRAMES + 1):
            x = min(1.0, f / KID_FRAMES)
            u = 1 - (1 - x) ** 2.2
            ang, dist, h = [a + (b - a) * u for a, b in zip(k["cam_from"], k["cam_to"])]
            r = math.radians(ang)
            cam.location = KID_POS + Vector((math.sin(r) * dist, -math.cos(r) * dist, h))
            cam.keyframe_insert("location", frame=f)
        linear_keys(cam)
        new_scene("KidIntro_9x16_" + k["short"], V, [splash, front, col], cam, 64, frames=KID_FRAMES, B=B)
        sc = new_scene("KidCard_4x5_" + k["short"], PC, [splash, front, col], cam, 256, still_frame=KID_FRAMES, B=B)
        # the trailer's 16:9 intro: the kid in a third of the frame (rotation keyed: the look point moves)
        tc = camera("Cam_TrailerIntro_" + k["short"], 35.0, 2.2)
        t = k["trl"]
        for f in range(TRL_FRAMES + 1):
            x = min(1.0, f / TRL_FRAMES)
            u = 1 - (1 - x) ** 2
            ang, dist, h = [a + (b - a) * u for a, b in zip(t["cam_from"], t["cam_to"])]
            r = math.radians(ang)
            tc.location = KID_POS + Vector((math.sin(r) * dist, -math.cos(r) * dist, h))
            chest = KID_POS + Vector((0, 0, t["look_z"]))
            fwd = (chest - tc.location).normalized()
            right = fwd.cross(Vector((0, 0, 1))).normalized()
            tc.rotation_euler = (chest - right * t["shift"] - tc.location).to_track_quat("-Z", "Y").to_euler()
            tc.data.dof.focus_distance = (chest - tc.location).length
            tc.keyframe_insert("location", frame=f)
            tc.keyframe_insert("rotation_euler", frame=f)
            tc.data.keyframe_insert("dof.focus_distance", frame=f)
        linear_keys(tc)
        new_scene("Trailer_KidIntro_16x9_" + k["short"], WIDE, [splash, front, col], tc, 64, frames=TRL_FRAMES, B=B)
    save("Kids")


# ---------------------------------------------------------------- Bosses.blend
def build_bosses():
    fresh_file()
    B = link_assets()
    splash_sc, splash, front = build_set(B)
    mat = bpy.data.materials["SPL_Palette"]
    lights = bpy.data.collections.new("BossLights")
    # pink rim from behind separates the boss from the sky, a cool fill opens the shaded face
    B.sun_light(lights, "SOC_Rim", (-0.45, -0.82, -0.35), "FFB9C8", 2.6, shadow=False)
    B.sun_light(lights, "SOC_Fill", (0.25, 0.93, -0.25), "B9C8FF", 0.9, shadow=False)
    for name, short, tilt, pivot, yaw_off in BOSSES:
        src = bpy.data.objects[name]
        h = max((B.world_matrix(c) @ v.co).z for c in [src] + list(src.children_recursive) if c.type == "MESH"
                for v in c.data.vertices)
        col = bpy.data.collections.new(short)
        cam_loc = BOSS_POS + Vector((0.9, -1.45 * h, 0.45))
        pl = B.place(col, mat, name, BOSS_POS, tilt=tilt, pivot_z=pivot, look=(cam_loc.x, cam_loc.y, 0))
        pl.root.rotation_euler.z += math.radians(yaw_off)
        for part in pl.root.children_recursive:
            if "Hare_Stuffing" in part.name:
                part.hide_render = part.hide_viewport = True          # the seam rips only at half health
        look = empty("Look_" + short, BOSS_POS + Vector((0, 0, h * 0.47)), col)
        focus = empty("Focus_" + short, BOSS_POS + Vector((0, -0.5, h * 0.5)), col)
        cam = camera("Cam_Boss_" + short, 35.0, 3.2, focus)
        cam.location = cam_loc
        track(cam, look)
        new_scene("BossPoster_4x5_" + short, PC, [splash, lights, col], cam, 256, B=B)
    save("Bosses")


BUILD = {"KeyArt": build_keyart, "Kids": build_kids, "Bosses": build_bosses}
for n in (BUILD if WHICH == "all" else [WHICH]):
    BUILD[n]()
print("DONE")

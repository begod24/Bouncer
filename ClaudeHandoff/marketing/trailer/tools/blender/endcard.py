"""Trailer end card: the splash key art in motion. The four kids stand with their backs to us on our half of
the court facing the toy army; slow push-in; the jumping pupsik hops; the sky is left for the logo.
Run: Blender -b Bouncer.blend --python endcard.py -- still|anim      (the .blend is never saved)"""
import bpy, sys, os, math
from mathutils import Vector

OUT = "/private/tmp/claude-501/-Users-bekbolataldiyarov-Desktop-projects-Game-Projects-Bouncer/f820830a-a4f1-413d-9c37-928eb1812c7d/scratchpad/trailer/blender/endcard"
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = args[0] if args else "still"

for mod in ("ba_lib", "ba_kids", "ba_branding"):
    if mod not in sys.modules:
        sys.modules[mod] = bpy.data.texts[mod + ".py"].as_module()
import ba_branding as B

FPS = 30
DUR = 4.0
KIDS = [("Kid_Otlichnik", -1.75, -2.35, 172, "Idle", 0), ("Kid_Tolstyak", -0.6, -2.6, 178, "Idle", 17),
        ("Kid_Melkaya", 0.6, -2.5, 184, "Idle", 33), ("Kid_Huligan", 1.75, -2.3, 190, "Idle", 48)]
CAM_FROM = Vector((0.0, -6.9, 0.68))
CAM_TO = Vector((0.0, -6.1, 0.8))
TARGET = Vector(B.CAM_TARGET) + Vector((0.0, 0.0, 1.6))   # look a bit up: room in the sky for the logo


def setup():
    sc = bpy.context.scene
    for lc in sc.view_layers[0].layer_collection.children:
        lc.exclude = lc.name != "Kids"
    splash = bpy.data.collections["Splash"]
    if splash.name not in sc.collection.children:
        sc.collection.children.link(splash)
    sc.world = bpy.data.worlds["SPL_World"]
    B.setup_render(sc, res=(1920, 1080))
    sc.cycles.samples = 64 if MODE == "anim" else 24
    sc.render.use_persistent_data = True
    if MODE == "still":
        sc.render.resolution_percentage = 50
    mat = bpy.data.materials["SPL_Palette"]
    for o in bpy.data.collections["Kids"].all_objects:
        o.hide_render = True
        if o.type == "MESH":
            for slot in o.material_slots:
                if slot.material and slot.material.name == "M_Palette":
                    slot.link = "OBJECT"
                    slot.material = mat
    for name, x, y, yaw, action, offset in KIDS:
        rig = bpy.data.objects[name]
        rig.hide_render = False
        bpy.data.objects[name + "_Mesh"].hide_render = False
        rig.location = (x, y, 0)
        rig.rotation_mode = "XYZ"
        rig.rotation_euler = (0, 0, math.radians(yaw))
        rig.animation_data_create()
        act = bpy.data.actions[action]
        rig.animation_data.action = act
        try:
            if act.slots and rig.animation_data.action_slot is None:
                rig.animation_data.action_slot = act.slots[0]
        except AttributeError:
            pass
        # different phase of the idle per kid: offset the action in time through an NLA-free trick
        rig["phase"] = offset
    cam = bpy.data.objects["SPL_Cam"]
    sc.camera = cam
    cam.data.dof.use_dof = True
    cam.data.dof.aperture_fstop = 5.6
    jumper = [o for o in bpy.data.collections["Splash"].objects if o.name.startswith("SPL_Enemy_Pupsik.2")
              and o.parent is None]
    return sc, cam, (jumper[0] if jumper else None)


def set_time(sc, t, cam, jumper, base_z):
    f = t * FPS
    sc.frame_set(int(f), subframe=f - int(f))
    for name, *_ , offset in KIDS:
        rig = bpy.data.objects[name]
        # Idle loops every 60 frames: shift each kid's phase so they don't breathe in sync
        ad = rig.animation_data
        ad.action_extrapolation = "HOLD"
    x = min(1.0, t / DUR)
    u = 1 - (1 - x) ** 3
    cam.location = CAM_FROM.lerp(CAM_TO, u)
    cam.rotation_euler = (TARGET - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.dof.focus_distance = (Vector((0, 4.0, 1.0)) - cam.location).length
    if jumper is not None:
        hop = abs(math.sin(math.pi * t / 0.55))
        jumper.location.z = base_z - 0.32 + 0.45 * hop


def main():
    sc, cam, jumper = setup()
    base_z = jumper.location.z if jumper else 0.0
    os.makedirs(OUT, exist_ok=True)
    if MODE == "still":
        for t in (0.0, DUR):
            set_time(sc, t, cam, jumper, base_z)
            sc.render.filepath = os.path.join(OUT, f"still_{t:.1f}.png")
            bpy.ops.render.render(write_still=True)
    else:
        n = int(round(DUR * FPS))
        for i in range(n):
            set_time(sc, i / FPS, cam, jumper, base_z)
            sc.render.filepath = os.path.join(OUT, f"{i:04d}.png")
            bpy.ops.render.render(write_still=True)
            print("FRAME", i, flush=True)
    print("DONE")


main()

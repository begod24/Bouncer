"""Animations of the kids, keyed by script on the reference skeleton Anim_Kids (same bone names and T-pose as
every kid in ba_kids). Unity imports Anim_Kids.fbx as Humanoid, so every clip plays on every kid.

Poses are written as rotations in the armature's REST axes (see ba_kids.delta_quat), the kid facing -Y:
  spine/neck/head: X+ leans forward, Z+ turns to the kid's left, Y+ leans to the kid's left;
  legs: fwd = thigh forward, knee = shin back, foot = toes down; arms: direction from fwd/out angles + elbow.
Cycles (idle, runs) are functions of the phase 0..1 keyed on every frame; one-shots are key poses.
30 fps. Loops: Idle, Run_*, Cheer, Pose_* (see ArtImportPostprocessor.KidLoopClips in Unity)."""
import math
import bpy
from mathutils import Vector, Matrix
import ba_lib as L
import ba_kids as K
from ba_kids import rot, swing

FPS = 30
RIG = "Anim_Kids"


# ================================================================ pose helpers
def spine(p, fwd=0.0, turn=0.0, side=0.0, bone="Spine"):
    """fwd: lean forward, turn: face to the kid's left, side: lean to the kid's left (degrees)."""
    p[bone] = rot("Z", turn) @ rot("Y", side) @ rot("X", fwd)


def leg(p, s, fwd=0.0, knee=0.0, foot=0.0, out=0.0, turn=0.0):
    """s = +1 left, -1 right. fwd: thigh forward, knee: bend, foot: toes down, out: sideways, turn: toes out."""
    side = "Left" if s > 0 else "Right"
    p[side + "UpperLeg"] = rot("Z", -s * turn) @ rot("Y", -s * out) @ rot("X", -fwd)
    p[side + "LowerLeg"] = rot("X", knee)
    p[side + "Foot"] = rot("X", foot)


def arm(p, s, fwd=0.0, out=12.0, elbow=10.0, twist=0.0, hand=0.0, bend_in=0.0):
    """Upper arm from hanging down: fwd swings it forward (negative: back), out lifts it sideways.
    elbow bends the forearm forward, twist turns the arm around itself, bend_in bends the wrist inwards."""
    side = "Left" if s > 0 else "Right"
    v = rot("Y", -s * out) @ rot("X", -fwd) @ Vector((0, 0, -1))
    D = swing((s, 0, 0), v)
    if twist:
        D = Matrix.Rotation(math.radians(twist), 3, v) @ D
    p[side + "UpperArm"] = D
    p[side + "LowerArm"] = rot("Z", -s * elbow)
    p[side + "Hand"] = rot("Z", -s * hand) @ rot("Y", -s * bend_in)


def arm_dir(p, s, direction, elbow=0.0, twist=0.0, hand=0.0):
    """Upper arm pointing along `direction` (armature axes)."""
    side = "Left" if s > 0 else "Right"
    v = Vector(direction).normalized()
    D = swing((s, 0, 0), v)
    if twist:
        D = Matrix.Rotation(math.radians(twist), 3, v) @ D
    p[side + "UpperArm"] = D
    p[side + "LowerArm"] = rot("Z", -s * elbow)
    p[side + "Hand"] = rot("Z", -s * hand)


def shoulder(p, s, up=0.0, fwd=0.0):
    """Shrug: Humanoid caps the arm at ~100 degrees up, the shoulder adds up to ~30 more."""
    p[("Left" if s > 0 else "Right") + "Shoulder"] = rot("Z", -s * fwd) @ rot("Y", -s * up)


def head(p, fwd=0.0, turn=0.0, side=0.0, neck=0.0):
    p["Neck"] = rot("X", neck)
    p["Head"] = rot("Z", turn) @ rot("Y", side) @ rot("X", fwd)


def hips(p, fwd=0.0, turn=0.0, side=0.0, loc=(0.0, 0.0, 0.0)):
    p["Hips"] = rot("Z", turn) @ rot("Y", side) @ rot("X", fwd)
    p["loc"] = Vector(loc)


def cyc(points, phase):
    """Periodic Catmull-Rom through (phase, value) points (phases 0..1, sorted)."""
    pts = sorted(points)
    n = len(pts)
    t = phase % 1.0
    for i in range(n):
        p0, v0 = pts[i]
        p1, v1 = pts[(i + 1) % n]
        span = (p1 - p0) % 1.0 or 1.0
        rel = (t - p0) % 1.0
        if rel <= span + 1e-9:
            u = rel / span
            vm = pts[(i - 1) % n][1]
            vp = pts[(i + 2) % n][1]
            return 0.5 * ((2 * v0) + (-vm + v1) * u + (2 * vm - 5 * v0 + 4 * v1 - vp) * u * u
                          + (-vm + 3 * v0 - 3 * v1 + vp) * u * u * u)
    return pts[0][1]


def mirror(p):
    """Left-right mirror of a pose: swap sides, flip the turn and side-lean components."""
    M = Matrix.Diagonal((-1.0, 1.0, 1.0))
    out = {}
    for name, D in p.items():
        if name == "loc":
            out["loc"] = Vector((-D[0], D[1], D[2]))
            continue
        other = name.replace("Left", "#").replace("Right", "Left").replace("#", "Right")
        out[other] = M @ D @ M
    return out


# ================================================================ clip building
def begin(rig, name):
    act = bpy.data.actions.get(name)
    if act is not None:
        bpy.data.actions.remove(act)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    return act


def key(rig, frame, pose):
    K.apply_pose(rig, pose, frame=frame)


def finish(rig, act, loop=False, interp="BEZIER"):
    fcurves = _fcurves(act)
    for fc in fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = interp
        if loop:
            fc.modifiers.new("CYCLES")
    rig.animation_data.action = None


def _fcurves(act):
    try:
        return list(act.fcurves)
    except AttributeError:
        out = []
        for layer in act.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    out += list(bag.fcurves)
        return out


def cycle_clip(rig, name, frames, pose_at):
    """A loop: pose_at(phase) keyed on every frame, the last frame equals the first."""
    act = begin(rig, name)
    for f in range(frames + 1):
        key(rig, f, pose_at(f / frames))
    finish(rig, act, loop=False, interp="LINEAR")
    return act


def key_clip(rig, name, keys, interp="BEZIER"):
    """keys = [(frame, pose)]."""
    act = begin(rig, name)
    for f, pose in keys:
        key(rig, f, pose)
    finish(rig, act, interp=interp)
    return act


# ================================================================ base poses
def rest_arms(p, sway=0.0):
    arm(p, 1, fwd=4 + sway, out=13, elbow=14)
    arm(p, -1, fwd=4 - sway, out=13, elbow=14)


def idle_pose(t):
    """Breathing and a slow weight shift, ~2 s."""
    b = math.sin(2 * math.pi * t)
    w = math.sin(2 * math.pi * t + 0.8)
    p = {}
    hips(p, side=1.2 * w, loc=(0.012 * w, 0.0, -0.004 + 0.003 * b))
    spine(p, fwd=2.0 - 1.2 * b, side=-1.5 * w)
    head(p, fwd=-1.0 + 1.2 * b, side=0.8 * w, turn=2.0 * math.sin(2 * math.pi * t * 1.0 + 2.0))
    arm(p, 1, fwd=3 + 1.5 * b, out=13 + 1.5 * b, elbow=14 + 3 * b)
    arm(p, -1, fwd=3 + 1.5 * b, out=13 + 1.5 * b, elbow=14 + 3 * b)
    leg(p, 1, fwd=1.0, knee=3 + 2 * w, out=2, turn=6)
    leg(p, -1, fwd=1.0, knee=3 - 2 * w, out=2, turn=6)
    return p


# Forward run: phase 0 = left foot touches down in front.
THIGH = [(0.0, 34), (0.15, 12), (0.4, -30), (0.55, -18), (0.75, 30), (0.9, 42)]
KNEE = [(0.0, 12), (0.12, 34), (0.35, 22), (0.5, 55), (0.65, 98), (0.85, 40)]
FOOT = [(0.0, -12), (0.15, 4), (0.38, 38), (0.6, 18), (0.85, -8)]


def run_forward(t):
    p = {}
    z = -0.016 - 0.02 * math.cos(4 * math.pi * (t - 0.12))
    hips(p, fwd=4, turn=-8 * math.cos(2 * math.pi * t), side=2 * math.sin(2 * math.pi * t), loc=(0, 0, z))
    spine(p, fwd=10, turn=12 * math.cos(2 * math.pi * t))
    head(p, fwd=-9, turn=-4 * math.cos(2 * math.pi * t))
    for s, ph in ((1, t), (-1, t + 0.5)):
        leg(p, s, fwd=cyc(THIGH, ph), knee=cyc(KNEE, ph), foot=cyc(FOOT, ph))
    swing_ = 48 * math.cos(2 * math.pi * t)        # right arm forward while the left leg is forward
    arm(p, -1, fwd=swing_ + 6, out=16, elbow=78 + 14 * math.cos(2 * math.pi * t))
    arm(p, 1, fwd=-swing_ + 6, out=16, elbow=78 - 14 * math.cos(2 * math.pi * t))
    return p


def run_backward(t):
    """Backpedalling: the forward run's legs in reverse, upright, arms forward for balance."""
    p = {}
    z = -0.02 - 0.016 * math.cos(4 * math.pi * (t - 0.1))
    hips(p, fwd=-2, turn=5 * math.cos(2 * math.pi * t), loc=(0, 0.01, z))
    spine(p, fwd=-3, turn=-7 * math.cos(2 * math.pi * t))
    head(p, fwd=2)
    for s, ph in ((1, -t), (-1, -t + 0.5)):
        leg(p, s, fwd=0.7 * cyc(THIGH, ph) - 4, knee=0.85 * cyc(KNEE, ph) + 6, foot=0.7 * cyc(FOOT, ph))
    sw = 26 * math.cos(2 * math.pi * t)
    arm(p, -1, fwd=24 + sw, out=22, elbow=70)
    arm(p, 1, fwd=24 - sw, out=22, elbow=70)
    return p


# Sideways (to the kid's left): side gallop. Phase 0: the left (leading) foot lands out to the side.
def run_left(t):
    p = {}
    c = math.cos(2 * math.pi * t)
    hop = max(0.0, math.sin(2 * math.pi * (t - 0.55)))
    z = -0.03 + 0.045 * hop
    hips(p, side=-4 + 3 * c, turn=12, loc=(0.0, 0.0, z))
    spine(p, fwd=6, side=5 - 3 * c, turn=-12)
    head(p, side=-4, turn=-3)
    lead = 22 * (0.5 + 0.5 * c)                          # left leg reaches out, the right one closes in
    trail = 10 * (0.5 - 0.5 * c)
    leg(p, 1, out=lead + 4, fwd=8, knee=18 + 22 * hop, foot=-4 + 20 * hop)
    leg(p, -1, out=-trail + 6, fwd=4, knee=28 + 16 * (0.5 - 0.5 * c), foot=10 + 18 * (0.5 - 0.5 * c))
    arm(p, 1, fwd=10, out=34 + 14 * hop, elbow=55)
    arm(p, -1, fwd=18, out=20 + 8 * hop, elbow=70)
    return p


def run_right(t):
    return mirror(run_left(t))


# ================================================================ one-shots and poses
def charge_start():
    """Ball raised by the right shoulder, left arm forward for aiming."""
    p = {}
    spine(p, fwd=4, turn=-14)
    head(p, turn=12)
    arm_dir(p, -1, (-0.62, 0.3, 0.2), elbow=118, twist=-40, hand=-10)
    arm(p, 1, fwd=62, out=18, elbow=24)
    return p


def charge_full():
    """Full wind-up: right arm far back over the shoulder, body turned and leaning back."""
    p = {}
    spine(p, fwd=-6, turn=-34, side=-6)
    head(p, turn=30, fwd=2)
    arm_dir(p, -1, (-0.55, 0.62, 0.55), elbow=100, twist=-55, hand=-20)
    shoulder(p, -1, up=18, fwd=-10)
    arm(p, 1, fwd=78, out=24, elbow=12)
    return p


def throw_keys():
    """From the wind-up the arm whips forward and down across the body; follow-through; settle."""
    a = charge_full()
    b = {}
    spine(b, fwd=14, turn=10)
    head(b, turn=-8, fwd=-6)
    arm_dir(b, -1, (-0.25, -0.9, 0.35), elbow=20, twist=-10, hand=10)
    arm(b, 1, fwd=10, out=30, elbow=40)
    c = {}
    spine(c, fwd=20, turn=24)
    head(c, turn=-18, fwd=-10)
    arm_dir(c, -1, (0.35, -0.7, -0.62), elbow=30, twist=0, hand=10)
    arm(c, 1, fwd=-20, out=28, elbow=50)
    d = {}
    spine(d, fwd=6, turn=6)
    head(d, turn=-4)
    arm(d, -1, fwd=20, out=16, elbow=40)
    arm(d, 1, fwd=6, out=16, elbow=30)
    return [(0, a), (2, b), (4, c), (9, d)]


def catch_pose():
    """Both hands forward at chest height, palms out, ready to take the ball."""
    p = {}
    spine(p, fwd=10)
    head(p, fwd=-8)
    for s in (1, -1):
        arm_dir(p, s, (s * 0.35, -0.88, -0.2), elbow=28, twist=-s * 70, hand=-18)
    return p


def caught_keys():
    """The ball is hugged to the chest, a little bounce back."""
    a = catch_pose()
    b = {}
    spine(b, fwd=-6)
    head(b, fwd=6)
    for s in (1, -1):
        arm_dir(b, s, (s * 0.45, -0.45, -0.75), elbow=112, twist=-s * 40, hand=-10)
    c = {}
    spine(c, fwd=2)
    for s in (1, -1):
        arm_dir(c, s, (s * 0.5, -0.3, -0.82), elbow=95, twist=-s * 30)
    return [(0, a), (3, b), (8, c)]


def hurt_keys():
    """Hit: head and chest jerk back, arms fly up a little, then back."""
    a = {}
    rest_arms(a)
    b = {}
    spine(b, fwd=-16, side=5)
    head(b, fwd=-14, side=6)
    arm(b, 1, fwd=20, out=40, elbow=50)
    arm(b, -1, fwd=24, out=36, elbow=60)
    c = {}
    spine(c, fwd=6)
    head(c, fwd=4)
    arm(c, 1, fwd=8, out=20, elbow=30)
    arm(c, -1, fwd=8, out=20, elbow=30)
    return [(0, a), (2, b), (7, c), (12, a)]


def dash_keys():
    """Quick dive forward (the model faces the dash direction for these 0.16 s)."""
    a = {}
    hips(a, fwd=18, loc=(0, -0.02, -0.08))
    spine(a, fwd=26)
    head(a, fwd=-22)
    leg(a, 1, fwd=40, knee=70, foot=-10)
    leg(a, -1, fwd=-30, knee=40, foot=40)
    arm(a, 1, fwd=-45, out=22, elbow=30)
    arm(a, -1, fwd=-50, out=22, elbow=30)
    b = {}
    hips(b, fwd=12, loc=(0, 0, -0.05))
    spine(b, fwd=18)
    head(b, fwd=-14)
    leg(b, 1, fwd=20, knee=45, foot=0)
    leg(b, -1, fwd=-10, knee=50, foot=20)
    arm(b, 1, fwd=-20, out=20, elbow=45)
    arm(b, -1, fwd=-15, out=20, elbow=45)
    return [(0, a), (3, a), (6, b)]


def slide_pose():
    """«Подкат»: feet first on the asphalt, leaning back on one hand."""
    p = {}
    hips(p, fwd=-40, loc=(0, 0.04, -0.46))
    spine(p, fwd=4)
    head(p, fwd=22)
    leg(p, 1, fwd=52, knee=2, foot=-15)
    leg(p, -1, fwd=-8, knee=105, foot=15, out=10)
    arm(p, 1, fwd=-55, out=38, elbow=12)
    arm(p, -1, fwd=35, out=42, elbow=40)
    return p


def down_keys():
    """Knocked out: the knees give, the kid falls on the back and stays."""
    a = idle_pose(0.0)
    b = {}
    hips(b, fwd=-12, loc=(0, 0.06, -0.14))
    spine(b, fwd=-18)
    head(b, fwd=-10)
    leg(b, 1, fwd=30, knee=60, foot=10)
    leg(b, -1, fwd=20, knee=45, foot=10)
    arm(b, 1, fwd=40, out=50, elbow=40)
    arm(b, -1, fwd=35, out=55, elbow=45)
    c = {}
    hips(c, fwd=-82, loc=(0, 0.32, -0.5))
    spine(c, fwd=-6)
    head(c, fwd=-12, turn=16)
    leg(c, 1, fwd=70, knee=35, foot=20, out=8)
    leg(c, -1, fwd=55, knee=10, foot=25, out=12)
    arm(c, 1, fwd=-20, out=80, elbow=30)
    arm(c, -1, fwd=-10, out=70, elbow=20)
    d = {}
    hips(d, fwd=-86, loc=(0, 0.34, -0.52))
    spine(d, fwd=-4)
    head(d, fwd=-8, turn=20)
    leg(d, 1, fwd=62, knee=28, foot=20, out=8)
    leg(d, -1, fwd=50, knee=8, foot=25, out=12)
    arm(d, 1, fwd=-15, out=85, elbow=25)
    arm(d, -1, fwd=-5, out=75, elbow=15)
    return [(0, a), (5, b), (11, c), (16, d)]


def cheer_pose(t):
    """Jumping for joy with both fists up, ~0.8 s loop."""
    j = max(0.0, math.sin(2 * math.pi * t))
    squat = max(0.0, -math.sin(2 * math.pi * t))
    p = {}
    hips(p, loc=(0, 0, 0.1 * j - 0.06 * squat))
    spine(p, fwd=-4 * j + 8 * squat)
    head(p, fwd=-8 * j)
    for s in (1, -1):
        arm(p, s, fwd=12, out=150 + 10 * j, elbow=40 - 20 * j)
        shoulder(p, s, up=18 + 8 * j)
        leg(p, s, fwd=8 * j + 30 * squat, knee=20 * j + 60 * squat, foot=25 * j - 10 * squat, out=4)
    return p


def pose_think(t):
    """Отличник (думает): a book in the left hand, right hand at the chin, head tilted."""
    b = math.sin(2 * math.pi * t)
    p = {}
    hips(p, side=2, loc=(0.01, 0, -0.005))
    spine(p, fwd=4 + 1.5 * b, side=-2)
    head(p, fwd=10 + 2 * b, side=8, turn=6)
    arm_dir(p, 1, (0.25, -0.55, -0.8), elbow=95, twist=30, hand=10)       # book held in front
    arm_dir(p, -1, (-0.22, -0.6, -0.77), elbow=128 + 2 * b, twist=-35)     # hand at the chin
    shoulder(p, -1, up=6, fwd=6)
    leg(p, 1, fwd=2, knee=4, out=3, turn=8)
    leg(p, -1, fwd=-2, knee=8, out=6, turn=14)
    return p


def pose_cheer(t):
    """Толстяк (радуется): fist pumped up, chips in the other hand, bouncing."""
    j = 0.5 + 0.5 * math.sin(2 * math.pi * t)
    p = {}
    hips(p, loc=(0, 0, -0.03 + 0.05 * j))
    spine(p, fwd=-4, side=-4 * j, turn=6)
    head(p, fwd=-10, side=5)
    arm(p, -1, fwd=20, out=150 + 12 * j, elbow=60 - 30 * j)                # fist up
    shoulder(p, -1, up=20 + 8 * j)
    arm(p, 1, fwd=40, out=22, elbow=80)                                   # chips
    leg(p, 1, fwd=20 * j, knee=30 * j + 10, foot=10 * j, out=6)
    leg(p, -1, fwd=4, knee=12, out=8, turn=10)
    return p


def pose_ready(t):
    """Мелкая (бежит): ready to run, bouncing on the toes, the jump rope in the right hand."""
    j = 0.5 + 0.5 * math.sin(2 * math.pi * t * 2)
    p = {}
    hips(p, fwd=10, turn=10, loc=(0, 0, -0.07 + 0.025 * j))
    spine(p, fwd=18, turn=-6)
    head(p, fwd=-14, turn=-4)
    leg(p, 1, fwd=34, knee=50, foot=-6, out=4)
    leg(p, -1, fwd=-22, knee=30, foot=30, out=4)
    arm(p, -1, fwd=40, out=16, elbow=85)
    arm(p, 1, fwd=-30, out=18, elbow=70)
    return p


def pose_tough(t):
    """Хулиган (готов к драке): arms crossed, the right foot on the football, a small nod."""
    b = math.sin(2 * math.pi * t)
    p = {}
    hips(p, side=-3, loc=(-0.02, 0, -0.01))
    spine(p, fwd=-4, side=2, turn=-8)
    head(p, fwd=-8 + 2 * b, turn=10, side=-3)
    arm_dir(p, 1, (0.3, -0.45, -0.84), elbow=118, twist=65)
    arm_dir(p, -1, (-0.3, -0.5, -0.81), elbow=112, twist=-60)
    leg(p, 1, fwd=-2, knee=4, out=8, turn=12)
    leg(p, -1, fwd=28, knee=40, foot=-20, out=6, turn=10)                 # on the ball
    return p


# ================================================================ build + export
CLIPS = {}


def build(col_parent=None):
    col = L.collection(K.COLLECTION, col_parent)
    rig = K.build_rig(RIG, col, 0.15, 0.072)
    rig.location = (-2.0, 26.0, 0.0)
    clips = [
        ("Idle", lambda r: cycle_clip(r, "Idle", 60, idle_pose)),
        ("Run_F", lambda r: cycle_clip(r, "Run_F", 16, run_forward)),
        ("Run_B", lambda r: cycle_clip(r, "Run_B", 16, run_backward)),
        ("Run_L", lambda r: cycle_clip(r, "Run_L", 14, run_left)),
        ("Run_R", lambda r: cycle_clip(r, "Run_R", 14, run_right)),
        ("Cheer", lambda r: cycle_clip(r, "Cheer", 24, cheer_pose)),
        ("Pose_Think", lambda r: cycle_clip(r, "Pose_Think", 90, pose_think)),
        ("Pose_Cheer", lambda r: cycle_clip(r, "Pose_Cheer", 24, pose_cheer)),
        ("Pose_Ready", lambda r: cycle_clip(r, "Pose_Ready", 40, pose_ready)),
        ("Pose_Tough", lambda r: cycle_clip(r, "Pose_Tough", 80, pose_tough)),
        ("Charge_Start", lambda r: key_clip(r, "Charge_Start", [(0, charge_start()), (1, charge_start())])),
        ("Charge_Full", lambda r: key_clip(r, "Charge_Full", [(0, charge_full()), (1, charge_full())])),
        ("Throw", lambda r: key_clip(r, "Throw", throw_keys())),
        ("Catch", lambda r: key_clip(r, "Catch", [(0, catch_pose()), (1, catch_pose())])),
        ("Caught", lambda r: key_clip(r, "Caught", caught_keys())),
        ("Hurt", lambda r: key_clip(r, "Hurt", hurt_keys())),
        ("Dash", lambda r: key_clip(r, "Dash", dash_keys())),
        ("Slide", lambda r: key_clip(r, "Slide", [(0, slide_pose()), (1, slide_pose())])),
        ("Down", lambda r: key_clip(r, "Down", down_keys())),
    ]
    for name, fn in clips:
        CLIPS[name] = fn(rig)
    return rig


def export(rig=None, folder=None):
    """Anim_Kids.fbx: the skeleton with every clip as a take (Unity: Humanoid, clips by name)."""
    import os
    rig = rig or bpy.data.objects[RIG]
    folder = folder or os.path.join(L.REPO, "Assets/_Project/Art/Models/Kids")
    sc = bpy.context.scene
    old_fps = sc.render.fps
    sc.render.fps = FPS
    try:
        return K.export_rig(rig, folder, animations=True)
    finally:
        sc.render.fps = old_fps

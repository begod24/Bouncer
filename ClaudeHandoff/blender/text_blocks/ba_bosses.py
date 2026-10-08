"""Boss of the evening: the PE teacher mannequin (Физрук-манекен), built on the ba_dusk mannequin at boss scale
and dressed through its extras hook, so the parts and pivots match the regular mannequin.
The final boss «Тот, кто в сумерках» (Бабай with a sack) lives in ba_final; _crow stays here for ba_dusk_elites."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe
import ba_dusk as D
from ba_elites import banded_shell

FRONT = -math.pi / 2


# ================================================================ Физрук-манекен
def _outline_stripe(b, p0, p1, r, sgn, color="white"):
    """A stripe down the outer side (+X for the left limb) of a limb segment of radius r."""
    off = Vector((sgn * (r + 0.004), 0.0, 0.0))
    b.beam(p0 + off, p1 + off, 0.022, 0.012, color, up=(1, 0, 0))


def _pe_teacher(parts):
    body, head = parts["body"], parts["head"]
    torso = parts["torso_lathe"]
    # track suit jacket and the top of the trousers
    banded_shell(body, torso, [(0.9, "navy"), (0.955, "navy"), (1.47, "navy"), (1.52, "white"), (1.535, None)], 0.012)
    body.decal_strip(torso, [(FRONT, torso.s_at_z(1.49)), (FRONT, torso.s_at_z(0.95))], 0.02, "white", off=0.017)   # zip
    body.cyl((0, 0, 1.5), (0, 0, 1.57), 0.066, 0.06, segs=10, color="white")                                         # collar
    for sx in (-1, 1):                                                                   # chest stripes
        body.decal_strip(torso, [(FRONT + sx * 0.35, torso.s_at_z(1.42)), (FRONT + sx * 0.6, torso.s_at_z(1.2))],
                         0.03, "red", off=0.016)
    # whistle on a red cord
    for sx in (-1, 1):                                                                   # the cord follows the chest
        cord = [Vector((sx * 0.055, -0.035, 1.53)), Vector((sx * 0.04, -0.126, 1.47)), Vector((sx * 0.02, -0.133, 1.38)),
                Vector((0.0, -0.125, 1.31))]
        for p0, p1 in zip(cord, cord[1:]):
            body.beam(p0, p1, 0.012, 0.008, "red")
    body.cyl((-0.025, -0.14, 1.285), (0.03, -0.14, 1.285), 0.02, 0.02, segs=8, color="tin")
    body.cyl((-0.025, -0.14, 1.285), (-0.025, -0.15, 1.3), 0.014, 0.014, segs=6, color="tin_dark")
    # cap and a painted moustache on the faceless head
    head.sphere((0, 0.005, 1.81), 0.118, 12, 6, "red", scale=(1.0, 1.0, 0.65))
    head.box((0, -0.135, 1.795), (0.17, 0.11, 0.014), "red", rot=(0.18, 0, 0))
    head.sphere((0, 0.005, 1.885), 0.016, 6, 4, "white")
    for sx in (-1, 1):
        head.sphere((sx * 0.026, -0.1, 1.712), 0.03, 8, 5, "black", scale=(1.0, 0.35, 0.45), rot=(0, sx * 0.3, 0))
    # sleeves with white stripes; clipboard in the left hand, stopwatch in the right
    for sgn, side in ((1, "L"), (-1, "R")):
        a = parts["arm" + side]
        sh = Vector((sgn * 0.2, 0.0, 1.42))
        elbow = Vector((sgn * 0.25, 0.01, 1.13))
        wrist = Vector((sgn * 0.26, -0.06, 0.87))
        a.sphere(sh, 0.078, 10, 6, "navy")
        a.cyl(sh + Vector((sgn * 0.02, 0, -0.03)), elbow, 0.066, 0.058, segs=10, color="navy")
        a.sphere(elbow, 0.058, 8, 5, "navy")
        a.cyl(elbow, wrist + (elbow - wrist).normalized() * 0.03, 0.055, 0.046, segs=10, color="navy")
        a.cyl(wrist + (elbow - wrist).normalized() * 0.05, wrist + (elbow - wrist).normalized() * 0.02, 0.05, 0.05,
              segs=10, color="white")                                                     # cuff
        for p0, p1, r in ((sh + Vector((0, 0, -0.04)), elbow, 0.062), (elbow, wrist + (elbow - wrist).normalized() * 0.05, 0.052)):
            for dz in (-0.018, 0.018):
                _outline_stripe(a, p0 + Vector((0, dz, 0)), p1 + Vector((0, dz, 0)), r, sgn)
        hand = wrist + Vector((0.004 * sgn, -0.02, -0.08))
        if side == "L":
            board = hand + Vector((0.045, -0.02, 0.0))
            a.box(board, (0.02, 0.22, 0.3), "wood")
            a.box(board + Vector((0.012, 0, -0.01)), (0.004, 0.19, 0.25), "white")
            a.box(board + Vector((0.013, 0, 0.14)), (0.02, 0.08, 0.03), "tin")
            for k in range(5):
                a.box(board + Vector((0.016, 0.01, 0.07 - k * 0.04)), (0.002, 0.14, 0.008), "grey")
        else:
            watch = hand + Vector((-0.03, -0.05, 0.0))
            a.cyl(watch + Vector((0, 0.012, 0)), watch - Vector((0, 0.012, 0)), 0.045, 0.045, segs=10,
                  color=lambda f: "white" if f.normal.y < -0.9 else "tin")
            a.cyl(watch + Vector((0, 0, 0.045)), watch + Vector((0, 0, 0.07)), 0.012, 0.012, segs=6, color="tin_dark")
        g = parts["leg" + side]                                                          # track trousers, keds
        hip = Vector((sgn * 0.09, 0.0, 0.92))
        knee = Vector((sgn * 0.1, 0.0, 0.5))
        ankle = Vector((sgn * 0.1, 0.01, 0.1))
        g.cyl(hip + Vector((0, 0, 0.04)), knee, 0.09, 0.07, segs=10, color="navy")
        g.cyl(knee, ankle + Vector((0, 0, 0.03)), 0.068, 0.054, segs=10, color="navy")
        _outline_stripe(g, hip + Vector((0, 0, 0.02)), knee, 0.084, sgn)
        _outline_stripe(g, knee, ankle + Vector((0, 0, 0.05)), 0.064, sgn)
        foot = ankle + Vector((0, -0.06, -0.05))
        g.sphere(foot, 0.14, 10, 5, "white", scale=(0.42, 1.0, 0.4))
        g.box(foot + Vector((0, 0, -0.045)), (0.12, 0.28, 0.02), "grey_dark")
        g.box(foot + Vector((sgn * 0.058, -0.02, 0.0)), (0.006, 0.12, 0.02), "blue", rot=(0.4, 0, 0))


def build_fizruk(col, location, name="Boss_Fizruk"):
    """PE teacher mannequin boss ~4.1 m: navy track suit with white stripes, keds, cap, whistle on a cord,
    a clipboard and a stopwatch; a moustache painted on the faceless head. Same parts as Enemy_Mannequin."""
    return D.build_mannequin(col, location, name=name, scale=2.2, tag=False, seed=33, extras=_pe_teacher)


# ================================================================ crow (shared with ba_dusk_elites)
def _crow(b, c, facing=-1):
    b.sphere(c, 0.075, 8, 5, "black", scale=(0.8, 1.2, 0.85))
    b.sphere(c + Vector((0, 0.09 * facing, 0.06)), 0.045, 8, 5, "black")
    b.cyl(c + Vector((0, 0.13 * facing, 0.06)), c + Vector((0, 0.19 * facing, 0.05)), 0.014, 0.0, segs=4, color="orange")
    b.sphere(c + Vector((0.025, 0.115 * facing, 0.075)), 0.009, 4, 3, "lamp")
    b.box(c + Vector((0, -0.1 * facing, 0.0)), (0.06, 0.1, 0.02), "black", rot=(0.3, 0, 0))


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    return [
        build_fizruk(col, (0.0, -22.0, 0.0)),
    ]

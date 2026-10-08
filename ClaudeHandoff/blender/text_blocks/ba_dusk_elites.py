"""Elite dusk enemies — the pack at the end of the construction site. Each has its own look and ability:
Elite_Mannequin — a department-store mannequin in a red dress, a wide red hat and pearls (throws a strong ball
at your back; the ball in her hand is added in Unity), Elite_Scarecrow — a scarecrow in a quilted vatnik and
an ushanka with glowing eyes and two crows (catches even charged balls and throws them back hard),
Elite_Shadow — the hat man: a shadow in a wide-brimmed hat with longer claws (puts out lamps).
Built on the ba_dusk builders through their extras hooks, so the parts and pivots match the base enemies
and the same Unity code drives them. Make them bigger with the prefab scale."""
import math
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe
import ba_dusk as D
from ba_elites import banded_shell, star_pts, adopt_names
from ba_bosses import _crow

FRONT = -math.pi / 2


# ================================================================ mannequin from the department store
DRESS_SKIRT = [(0.0, 0.5), (0.31, 0.5), (0.28, 0.6), (0.23, 0.78), (0.19, 0.92), (0.0, 0.94)]


def _department_store(parts):
    body, head = parts["body"], parts["head"]
    torso = parts["torso_lathe"]
    # red dress: bodice with a white belt, flared skirt down to the knees
    banded_shell(body, torso, [(0.9, "red"), (0.95, "white"), (0.99, "red"), (1.46, None)], 0.01)
    body.lathe(Lathe(DRESS_SKIRT, 14, Matrix.Diagonal((1.0, 0.8, 1.0, 1.0))), "red")
    for k in range(12):                                                                  # pearls round the neck
        a = k * math.pi / 6
        body.sphere((0.075 * math.cos(a), 0.06 * math.sin(a), 1.515), 0.016, 6, 4, "white")
    # wide red hat with a black band and a white flower, tilted a little
    brim = Matrix.Translation((0.0, 0.0, 1.82)) @ Matrix.Rotation(0.12, 4, "Y")
    head.cyl(brim @ Vector((0, 0, 0)), brim @ Vector((0, 0, 0.018)), 0.25, 0.25, segs=16, color="red")
    head.cyl(brim @ Vector((0, 0, 0.012)), brim @ Vector((0, 0.01, 0.12)), 0.115, 0.1, segs=14, color="red")
    head.cyl(brim @ Vector((0, 0, 0.02)), brim @ Vector((0, 0, 0.05)), 0.118, 0.116, segs=14, color="black")
    for k in range(5):
        a = k * 2 * math.pi / 5
        head.sphere(brim @ Vector((0.1 + 0.022 * math.cos(a), -0.06 + 0.022 * math.sin(a), 0.05)), 0.02, 6, 4, "white")
    head.sphere(brim @ Vector((0.1, -0.06, 0.055)), 0.014, 6, 4, "yellow")
    head.decal_disc(parts["head_lathe"], FRONT, parts["head_lathe"].s_at_z(1.69), 0.026, 0.011, "red", segs=6, off=0.003)
    # red shoes over the feet
    for side, sgn in (("L", 1), ("R", -1)):
        ankle = Vector((sgn * 0.1, 0.01, 0.1))
        parts["leg" + side].sphere(ankle + Vector((0, -0.06, -0.055)), 0.136, 8, 5, "red", scale=(0.4, 1.03, 0.38))


def build_elite_mannequin(col, location, name="Elite_Mannequin"):
    root = D.build_mannequin(col, location, name=name, plastic="white", seed=35, tag=False, extras=_department_store)
    adopt_names(root, "Mannequin_", "Elite_Mannequin_", "Enemy_Mannequin")
    return root


# ================================================================ scarecrow in a vatnik and an ushanka
def _vatnik(parts):
    b, h = parts["body"], parts["head"]
    coat = parts["coat_lathe"]
    for z in (0.84, 0.96, 1.08, 1.2, 1.32):                                              # quilting round the coat
        s = coat.s_at_z(z)
        b.decal_strip(coat, [(FRONT + k * math.pi / 12, s) for k in range(25)], 0.012, "black", off=0.007)
    for k in range(-2, 3):                                                               # and down the front
        phi = FRONT + k * 0.28
        b.decal_strip(coat, [(phi, coat.s_at_z(1.44)), (phi, coat.s_at_z(0.78))], 0.01, "black", off=0.007)
    # ushanka: fur crown, front flap turned up with a red star, ear flaps hanging down
    h.cyl((0, 0.01, 1.84), (0, 0.012, 1.97), 0.2, 0.185, segs=14, color="brown")
    h.sphere((0, 0.012, 1.97), 0.185, 14, 4, "brown", scale=(1.0, 1.0, 0.3))
    h.box((0, -0.2, 1.92), (0.36, 0.05, 0.12), "leather_dark", rot=(0.3, 0, 0))
    h.prism(D.ccw(star_pts(0.035, 0.015)), 0.008, "red",
            matrix=Matrix.Translation((0, -0.232, 1.925)) @ Matrix.Rotation(math.pi / 2 - 0.3, 4, "X"))
    for sx in (-1, 1):
        h.box((sx * 0.2, 0.0, 1.77), (0.05, 0.2, 0.2), "brown", rot=(0, sx * 0.15, 0))
        h.cyl((sx * 0.21, 0.0, 1.67), (sx * 0.21, 0.0, 1.55), 0.008, 0.008, segs=4, color="wood_dark")  # ties
    _crow(parts["armR"], Vector((-0.83, 0.0, 1.55)), facing=-1)


def build_elite_scarecrow(col, location, name="Elite_Scarecrow"):
    root = D.build_scarecrow(col, location, name=name, coat="grey_dark", glowing_eyes=True, hat="none", crow=True,
                             seed=39, extras=_vatnik)
    adopt_names(root, "Scarecrow_", "Elite_Scarecrow_", "Enemy_Scarecrow")
    return root


# ================================================================ the hat man
def _hat_man(parts):
    h = parts["head"]
    hy, top = -0.3, 1.86
    h.cyl((0, hy, top), (0, hy, top + 0.025), 0.37, 0.37, segs=16, color="black")                        # brim
    h.cyl((0, hy, top + 0.02), (0, hy + 0.01, top + 0.25), 0.16, 0.14, segs=14, color="black")            # crown
    h.cyl((0, hy, top + 0.03), (0, hy + 0.002, top + 0.075), 0.163, 0.16, segs=14, color="indigo")         # band
    for sgn, side in ((1, "L"), (-1, "R")):                                              # longer claws
        a = parts["arm" + side]
        hand = Vector((sgn * 0.46, -0.58, 0.8))
        for k in range(3):
            spread = (k - 1) * 0.4
            tip = hand + Vector((sgn * 0.07 * spread, -0.3, -0.26 + 0.04 * abs(spread)))
            a.cyl(hand, tip, 0.02, 0.0, segs=4, color="black")


def build_elite_shadow(col, location, name="Elite_Shadow"):
    root = D.build_shadow(col, location, name=name, seed=43, extras=_hat_man)
    adopt_names(root, "Shadow_", "Elite_Shadow_", "Enemy_Shadow")
    return root


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    y = -28.0
    return [
        build_elite_mannequin(col, (0.0, y, 0.0)),
        build_elite_scarecrow(col, (2.4, y, 0.0)),
        build_elite_shadow(col, (4.8, y, 0.0)),
    ]

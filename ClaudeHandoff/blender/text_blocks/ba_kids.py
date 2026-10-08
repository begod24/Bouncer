"""The four playable kids («Дети двора. 1990-е»): Отличник, Толстяк, Мелкая, Хулиган.

Unlike the enemies (rigid parts animated in code) the kids have a skeleton: an armature with Unity Humanoid
bone names (Hips, Spine, Neck, Head, LeftShoulder, LeftUpperArm ... RightToes), in a T-pose, so the clips from
ba_kids_anim.py play on every kid through Humanoid retargeting. Each kid is ONE mesh: every part is weighted
100% to one bone (rigid skinning, joints hidden by spheres), flat-shaded, palette colours like everything else.
All kids are the same height (the user's choice, ~1.43 m, big heads like on the concept sheet); only the
shoulder/hip width, the build and the clothes differ.
Convention as everywhere: metres, Z up, the kid faces -Y, the kid's left is +X."""
import math
import bpy
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2
COLLECTION = "Kids"

# Shared heights and limb lengths (metres).
ANKLE, KNEE, HIP, SHOULDER = 0.075, 0.33, 0.6, 0.965
UPPER_ARM, LOWER_ARM, HAND = 0.175, 0.155, 0.085
HEAD_PROFILE = [(0.0, 1.055), (0.07, 1.06), (0.118, 1.08), (0.15, 1.12), (0.166, 1.17), (0.17, 1.23),
                (0.166, 1.29), (0.148, 1.345), (0.116, 1.388), (0.064, 1.418), (0.0, 1.428)]
EYE_Z, NOSE_Z, MOUTH_Z, CHEEK_Z, EAR_Z = 1.215, 1.176, 1.124, 1.16, 1.2


# ================================================================ skeleton
def bone_specs(sx, hx):
    """Humanoid bones for shoulder joint half-width sx and hip joint half-width hx: (name, head, tail, parent)."""
    bones = [
        ("Hips", (0, 0, HIP), (0, 0, 0.72), None),
        ("Spine", (0, 0, 0.72), (0, 0, 0.98), "Hips"),
        ("Neck", (0, 0, 0.98), (0, 0, 1.06), "Spine"),
        ("Head", (0, 0, 1.06), (0, 0, 1.43), "Neck"),
    ]
    a1, a2, a3 = sx + UPPER_ARM, sx + UPPER_ARM + LOWER_ARM, sx + UPPER_ARM + LOWER_ARM + HAND
    for s, side in ((1, "Left"), (-1, "Right")):
        bones += [
            (side + "Shoulder", (s * 0.03, 0, SHOULDER - 0.02), (s * sx, 0, SHOULDER), "Spine"),
            (side + "UpperArm", (s * sx, 0, SHOULDER), (s * a1, 0, SHOULDER), side + "Shoulder"),
            (side + "LowerArm", (s * a1, 0, SHOULDER), (s * a2, 0, SHOULDER), side + "UpperArm"),
            (side + "Hand", (s * a2, 0, SHOULDER), (s * a3, 0, SHOULDER), side + "LowerArm"),
            (side + "UpperLeg", (s * hx, 0, HIP), (s * hx, 0, KNEE), "Hips"),
            (side + "LowerLeg", (s * hx, 0, KNEE), (s * hx, 0, ANKLE), side + "UpperLeg"),
            (side + "Foot", (s * hx, 0, ANKLE), (s * hx, -0.075, 0.02), side + "LowerLeg"),
            (side + "Toes", (s * hx, -0.075, 0.02), (s * hx, -0.13, 0.02), side + "Foot"),
        ]
    return bones


def build_rig(name, col, sx, hx):
    """Armature object `name` in a T-pose. Bone rolls are tidy but the posing helpers don't depend on them."""
    remove_kid(name)
    arm = bpy.data.armatures.new(name + "_Armature")
    rig = bpy.data.objects.new(name, arm)
    col.objects.link(rig)
    rig["pivot"] = (0.0, 0.0, 0.0)
    vl = bpy.context.view_layer
    for o in vl.objects:
        o.select_set(False)
    vl.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for bname, h, t, p in bone_specs(sx, hx):
        eb = arm.edit_bones.new(bname)
        eb.head, eb.tail = Vector(h), Vector(t)
        eb.align_roll(Vector((0, -1, 0)) if abs(eb.vector.normalized().z) > 0.7 else Vector((0, 0, 1)))
        if p:
            eb.parent = arm.edit_bones[p]
            eb.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    arm.display_type = "STICK"
    rig.show_in_front = True
    return rig


def remove_kid(name):
    ob = bpy.data.objects.get(name)
    if ob is None:
        return
    for o in [ob] + list(ob.children_recursive):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data is not None and data.users == 0:
            if isinstance(data, bpy.types.Mesh):
                bpy.data.meshes.remove(data)
            elif isinstance(data, bpy.types.Armature):
                bpy.data.armatures.remove(data)


# ================================================================ one mesh, parts weighted to bones
class KidMesh:
    """Geometry of one kid in ONE Builder; `on(bone)` starts the part weighted 100% to that bone."""

    def __init__(self):
        self.b = Builder()
        self.ranges = []
        self._bone = None
        self._start = 0

    def on(self, bone):
        self._close()
        self._bone = bone
        self._start = len(self.b.bm.verts)
        return self.b

    def _close(self):
        if self._bone is not None and len(self.b.bm.verts) > self._start:
            self.ranges.append((self._bone, self._start, len(self.b.bm.verts)))
        self._bone = None

    def finish(self, rig, col):
        self._close()
        ob = self.b.to_object(rig.name + "_Mesh", col)
        for bone, a, z in self.ranges:
            vg = ob.vertex_groups.get(bone) or ob.vertex_groups.new(name=bone)
            vg.add(list(range(a, z)), 1.0, "REPLACE")
        ob.parent = rig
        ob.location = (0, 0, 0)
        mod = ob.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        return ob


# ================================================================ shared body parts
def head(km, skin, xs=1.0):
    b = km.on("Head")
    lat = Lathe(HEAD_PROFILE, 16, Matrix.Diagonal((xs, 1.0, 1.0, 1.0)))
    b.lathe(lat, skin)
    return lat


def ears(km, skin, xs=1.0):
    b = km.on("Head")
    for s in (1, -1):
        b.sphere((s * 0.165 * xs, 0.01, EAR_Z), 0.043, 8, 5, skin, scale=(0.42, 0.75, 1.0))
        b.sphere((s * 0.172 * xs, 0.006, EAR_Z), 0.024, 6, 4, "rubber_dark", scale=(0.3, 0.7, 1.0))


def neck(km, skin):
    b = km.on("Neck")
    b.cyl((0, 0.01, 0.94), (0, 0.01, 1.1), 0.048, 0.046, segs=8, color=skin)


def face(km, lat, iris="wood", eye_w=0.03, eye_h=0.037, ex=0.06, brow="brown", brow_style="calm", mouth="smile",
         cheeks=None, lashes=False, freckles=False, nose_skin="skin_light"):
    """Painted face on the head lathe: eyes (white, iris, pupil, glint), brows, nose, mouth, cheeks."""
    b = km.on("Head")
    s_eye = lat.s_at_z(EYE_Z)
    r = lat.radius_at(s_eye)
    for sgn in (-1, 1):
        ph = FRONT + sgn * ex / r
        b.decal_disc(lat, ph, s_eye, eye_w, eye_h, "white", segs=10, off=0.004)
        b.decal_disc(lat, ph, s_eye - 0.003, eye_w * 0.72, eye_h * 0.72, iris, segs=10, off=0.007)
        b.decal_disc(lat, ph, s_eye - 0.003, eye_w * 0.36, eye_h * 0.36, "black", segs=6, off=0.01)
        b.decal_disc(lat, ph + sgn * 0.007 / r - 0.009 / r, s_eye + eye_h * 0.35, 0.007, 0.007, "white", segs=5, off=0.013)
        if lashes:
            b.decal_strip(lat, [(ph + sgn * eye_w * 0.4 / r, s_eye + eye_h * 0.75), (ph + sgn * eye_w * 1.25 / r, s_eye + eye_h * 1.05)],
                          0.009, "black", off=0.011)
        by = s_eye + eye_h + 0.024
        inner, outer = ph - sgn * 0.027 / r, ph + sgn * 0.033 / r
        if brow_style == "angry":        # inner ends pulled down
            pts = [(inner, by - 0.02), (ph, by - 0.003), (outer, by + 0.008)]
        elif brow_style == "raised":     # happy, surprised
            pts = [(inner, by + 0.002), (ph, by + 0.013), (outer, by + 0.004)]
        elif brow_style == "focused":    # a little frown
            pts = [(inner, by - 0.009), (ph, by + 0.001), (outer, by + 0.002)]
        else:
            pts = [(inner, by), (ph, by + 0.008), (outer, by + 0.002)]
        b.decal_strip(lat, pts, 0.015, brow, off=0.008)
        if cheeks:
            b.decal_disc(lat, FRONT + sgn * 0.098 / r, lat.s_at_z(CHEEK_Z), cheeks[0], cheeks[1], "pink", segs=8, off=0.004)
        if freckles:
            for dx, dz in ((0.07, CHEEK_Z + 0.012), (0.087, CHEEK_Z + 0.02), (0.079, CHEEK_Z + 0.002)):
                b.decal_disc(lat, FRONT + sgn * dx / r, lat.s_at_z(dz), 0.005, 0.005, "wood_light", segs=5, off=0.006)
    ny = -lat.radius_at(lat.s_at_z(NOSE_Z)) * 0.98
    b.sphere((0, ny - 0.004, NOSE_Z), 0.024, 7, 4, nose_skin, scale=(1.0, 0.75, 0.85))
    s_m = lat.s_at_z(MOUTH_Z)
    rm = lat.radius_at(s_m)
    if mouth == "smile":
        pts = [(FRONT + t * 0.038 / rm, s_m + 0.013 * t * t) for t in (-1, -0.5, 0, 0.5, 1)]
        b.decal_strip(lat, pts, 0.012, "maroon", off=0.006)
    elif mouth == "grin":            # big open happy mouth
        b.decal_disc(lat, FRONT, s_m + 0.002, 0.04, 0.022, "maroon", segs=10, off=0.005)
        b.decal_strip(lat, [(FRONT - 0.033 / rm, s_m + 0.014), (FRONT + 0.033 / rm, s_m + 0.014)], 0.009, "white", off=0.008)
        b.decal_disc(lat, FRONT, s_m - 0.009, 0.018, 0.008, "lips", segs=6, off=0.008)
    elif mouth == "smirk":           # one corner up
        pts = [(FRONT - 0.034 / rm, s_m - 0.002), (FRONT, s_m - 0.003), (FRONT + 0.025 / rm, s_m + 0.002),
               (FRONT + 0.038 / rm, s_m + 0.013)]
        b.decal_strip(lat, pts, 0.011, "maroon", off=0.006)
    else:                            # small calm mouth
        pts = [(FRONT + t * 0.024 / rm, s_m + 0.005 * t * t) for t in (-1, 0, 1)]
        b.decal_strip(lat, pts, 0.011, "maroon", off=0.006)


def hairline(front, side, back):
    """Height of the hair's lower edge around the head: front (forehead), side (above the ears), back (nape)."""
    def fn(rel):
        c = math.cos(rel)
        return side + (front - side) * c if c >= 0 else side + (side - back) * c
    return fn


def hair_cap(km, lat, color, line, top_off=0.03, edge_off=0.008, segs=24, rings=7):
    """Hair as a shell over the head lathe; `line(phi_rel)` is the height of its lower edge (phi_rel = 0 at the
    front, +-pi at the back). Thicker on top for volume."""
    b = km.on("Head")
    s_top = lat.s[-1]
    grid = []
    for i in range(segs):
        rel = -math.pi + 2 * math.pi * i / segs
        s0 = lat.s_at_z(line(rel), from_top=True)
        row = []
        for k in range(rings + 1):
            t = k / rings
            s = s0 + (s_top - s0) * (1 - (1 - t) ** 1.6) * 0.999
            off = edge_off + (top_off - edge_off) * math.sin(t * math.pi / 2)
            row.append(b.bm.verts.new(lat.point(FRONT + rel, s, off)))
        grid.append(row)
    faces = []
    for i in range(segs):
        A, B = grid[i], grid[(i + 1) % segs]
        for k in range(rings):
            faces.append(b.bm.faces.new([A[k], B[k], B[k + 1], A[k + 1]]))
    b.paint(faces, color)
    return faces


def tuft(km, base, tip, width, color, bone="Head"):
    """A pointed lock of hair: a four-sided cone from a base point to a tip."""
    b = km.on(bone)
    b.cyl(Vector(base), Vector(tip), width, 0.0, segs=4, color=color)


def torso(km, profile, color, ys=0.7, bone="Spine", segs=14, yoff=0.0):
    b = km.on(bone)
    lat = Lathe(profile, segs, Matrix.Translation((0, yoff, 0)) @ Matrix.Diagonal((1.0, ys, 1.0, 1.0)))
    b.lathe(lat, color)
    return lat


def kid_torso_profile(sx, waist=0.118, chest=0.128):
    """Torso from the waist to the neck; the shoulders reach the arm joints so the arms grow out of them."""
    return [(0.0, 0.69), (waist, 0.695), (waist + 0.006, 0.76), (chest, 0.85), (sx - 0.004, 0.915), (sx + 0.006, 0.95),
            (sx - 0.006, 0.985), (sx - 0.045, 1.0), (0.07, 1.005), (0.0, 1.008)]


def pelvis(km, color, r=0.125, ys=0.76, bottom=0.555, top=0.78):
    b = km.on("Hips")
    lat = Lathe([(0.0, bottom), (r * 0.72, bottom + 0.005), (r, bottom + 0.045), (r * 1.02, 0.68), (r * 0.95, top),
                 (0.0, top + 0.005)], 14, Matrix.Diagonal((1.0, ys, 1.0, 1.0)))
    b.lathe(lat, color)
    return lat


def arm(km, s, sx, skin, sleeve, sleeve_len=0.095, long_sleeve=False, cuff=None, hand=None, stripes=None, thick=1.0):
    """T-pose arm along s*X: shoulder ball, sleeve, skin, elbow, forearm, mitten hand with a thumb.
    stripes = (colour, [offsets]) — lines along the top of the arm in the T-pose (its outer side when it hangs)."""
    side = "Left" if s > 0 else "Right"
    x0, z = s * sx, SHOULDER
    X = lambda d: Vector((x0 + s * d, 0.0, z))
    k = thick
    elbow, wrist = UPPER_ARM, UPPER_ARM + LOWER_ARM
    along = Matrix.Translation(X(0.0)) @ L.rot_z_to((s, 0, 0))
    b = km.on(side + "Shoulder")                   # the joint ball rides on the shoulder: the arm turns inside it
    b.sphere(X(-0.004), 0.046 * k, 10, 6, sleeve)
    b = km.on(side + "UpperArm")
    if long_sleeve:
        b.lathe(Lathe([(0.047 * k, 0.0), (0.044 * k, elbow)], 10, along), sleeve, cap_bottom=False, cap_top=True)
    else:
        b.lathe(Lathe([(0.047 * k, 0.0), (0.052 * k, sleeve_len * 0.7), (0.051 * k, sleeve_len)], 10, along), sleeve,
                cap_bottom=False, cap_top=True)
        b.cyl(X(sleeve_len - 0.02), X(elbow), 0.037 * k, 0.034 * k, segs=8, color=skin)
    if stripes:
        for dy in stripes[1]:
            b.beam(X(0.01) + Vector((0, dy, 0.047 * k)), X(elbow - 0.005) + Vector((0, dy, 0.043 * k)), 0.008, 0.004, stripes[0])
    b = km.on(side + "LowerArm")
    b.sphere(X(elbow), (0.044 if long_sleeve else 0.035) * k, 8, 5, sleeve if long_sleeve else skin)
    if long_sleeve:
        b.cyl(X(elbow), X(wrist - 0.03), 0.044 * k, 0.04 * k, segs=10, color=sleeve)
        b.cyl(X(wrist - 0.034), X(wrist), 0.042 * k, 0.042 * k, segs=10, color=cuff or sleeve)
        if stripes:
            for dy in stripes[1]:
                b.beam(X(elbow + 0.01) + Vector((0, dy, 0.043 * k)), X(wrist - 0.035) + Vector((0, dy, 0.039 * k)),
                       0.008, 0.004, stripes[0])
    else:
        b.cyl(X(elbow), X(wrist), 0.034 * k, 0.029 * k, segs=8, color=skin)
        if cuff:
            b.cyl(X(wrist - 0.025), X(wrist), 0.032 * k, 0.032 * k, segs=8, color=cuff)
    b = km.on(side + "Hand")
    hc = hand or skin
    b.sphere(X(wrist + 0.042), 0.052, 8, 5, hc, scale=(1.0, 0.88, 0.6))
    b.sphere(X(wrist + 0.02) + Vector((0, -0.04, 0.004)), 0.02, 6, 4, hc, scale=(1.3, 1.0, 1.0))


def leg(km, s, hx, skin, shorts, shorts_bottom=0.45, pants=False, sock=None, sock_top=0.2, sock_band=None,
        shoe="white", sole="white", toe_cap=None, stripes=None, knee_patch=None, thick=1.0):
    """T-pose leg at x = s*hx: shorts leg or trousers, knee, shin, sock, shoe split heel/toe."""
    side = "Left" if s > 0 else "Right"
    x, k = s * hx, thick
    down = Matrix.Translation((x, 0, 0.64)) @ L.rot_z_to((0, 0, -1))
    b = km.on(side + "UpperLeg")
    if pants:
        b.lathe(Lathe([(0.078 * k, 0.0), (0.07 * k, 0.15), (0.058 * k, 0.64 - KNEE)], 10, down), shorts)
    else:
        b.lathe(Lathe([(0.078 * k, 0.0), (0.081 * k, 0.08), (0.079 * k, 0.64 - shorts_bottom)], 10, down), shorts)
        b.cyl((x, 0, shorts_bottom + 0.02), (x, 0, KNEE), 0.057 * k, 0.047 * k, segs=8, color=skin)
    if stripes:
        for dy in stripes[1]:
            b.beam((x + s * 0.075 * k, dy, 0.62), (x + s * 0.058 * k, dy, KNEE + 0.01), 0.004, 0.008, stripes[0])
    b = km.on(side + "LowerLeg")
    if pants:
        b.sphere((x, 0, KNEE), 0.058 * k, 8, 5, shorts)
        b.cyl((x, 0, KNEE), (x, 0, 0.1), 0.057 * k, 0.053 * k, segs=10, color=shorts)
        if stripes:
            for dy in stripes[1]:
                b.beam((x + s * 0.057 * k, dy, KNEE - 0.01), (x + s * 0.054 * k, dy, 0.1), 0.004, 0.008, stripes[0])
    else:
        b.sphere((x, 0, KNEE), 0.047 * k, 8, 5, skin)
        if knee_patch:
            b.sphere((x, -0.043 * k, KNEE + 0.003), 0.018, 6, 4, knee_patch, scale=(1.0, 0.4, 0.8))
        b.cyl((x, 0, KNEE), (x, 0, ANKLE), 0.045 * k, 0.037 * k, segs=8, color=skin)
        if sock:
            b.cyl((x, 0, sock_top), (x, 0, 0.07), 0.043 * k, 0.041 * k, segs=8, color=sock)
            b.cyl((x, 0, sock_top + 0.002), (x, 0, sock_top - 0.02), 0.046 * k, 0.046 * k, segs=8, color=sock_band or sock)
    # shoe: heel part on Foot, toe part on Toes
    b = km.on(side + "Foot")
    b.box((x, -0.012, 0.012), (0.098, 0.13, 0.024), sole)
    b.box((x, -0.008, 0.055), (0.088, 0.115, 0.065), shoe, taper=(0.9, 0.8))
    b.cyl((x, 0.0, 0.07), (x, 0.0, 0.1), 0.05 * max(1.0, k * 0.95), 0.047 * max(1.0, k * 0.95), segs=8, color=shoe)
    b = km.on(side + "Toes")
    b.box((x, -0.105, 0.012), (0.098, 0.07, 0.024), sole)
    b.sphere((x, -0.09, 0.034), 0.05, 8, 5, shoe, scale=(0.9, 1.1, 0.64))
    if toe_cap:
        b.sphere((x, -0.118, 0.028), 0.03, 7, 4, toe_cap, scale=(1.25, 0.9, 0.7))


def bands(colors, edges):
    """Colour function for lathe faces: horizontal stripes by the face centre height."""
    def fn(f):
        zc = sum(v.co.z for v in f.verts) / len(f.verts)
        for i, e in enumerate(edges):
            if zc < e:
                return colors[i % len(colors)]
        return colors[len(edges) % len(colors)]
    return fn


def profile_with(rz, z0, z1, extra, step=0.03):
    """Lathe profile from a radius function r(z) sampled every `step` plus exact `extra` heights (band edges)."""
    zs = set(round(z0 + step * i, 4) for i in range(int((z1 - z0) / step) + 1))
    zs |= set(round(z, 4) for z in extra if z0 < z < z1)
    zs |= {z0, z1}
    return [(rz(z), z) for z in sorted(zs)]


# ================================================================ the kids
def build_otlichnik(col, location=(0, 0, 0)):
    """Отличник: side-parted brown hair, thick black glasses, plaid shirt under an olive knitted vest, navy shorts,
    white socks, brown shoes and a rigid Soviet school satchel (ранец) on the back."""
    name, sx, hx = "Kid_Otlichnik", 0.145, 0.068
    rig = build_rig(name, col, sx, hx)
    km = KidMesh()
    skin = "skin_light"
    lat = head(km, skin)
    ears(km, skin)
    neck(km, skin)
    face(km, lat, iris="wood", brow="brown", brow_style="calm", mouth="small")
    hair_cap(km, lat, "brown", hairline(1.33, 1.262, 1.15), top_off=0.03, edge_off=0.009)
    for dx, dz, tx in ((-0.07, 1.375, 0.05), (-0.028, 1.385, 0.055), (0.018, 1.382, 0.065), (0.066, 1.365, 0.07)):
        tuft(km, (dx, -0.142, dz), (dx + tx, -0.18, dz - 0.07), 0.037, "brown")          # fringe swept to the kid's left
    b = km.on("Head")                                                                          # glasses
    y = -0.19
    for s in (1, -1):
        cx = s * 0.06
        for a, c in (((cx - 0.037, y, EYE_Z + 0.026), (cx + 0.037, y, EYE_Z + 0.026)),
                     ((cx - 0.037, y, EYE_Z - 0.024), (cx + 0.037, y, EYE_Z - 0.024)),
                     ((cx - 0.037, y, EYE_Z + 0.026), (cx - 0.037, y, EYE_Z - 0.024)),
                     ((cx + 0.037, y, EYE_Z + 0.026), (cx + 0.037, y, EYE_Z - 0.024))):
            b.beam(a, c, 0.012, 0.01, "black", up=(0, -1, 0))
        b.beam((s * 0.097, y, EYE_Z + 0.02), (s * 0.168, 0.02, EYE_Z + 0.01), 0.009, 0.009, "black")     # temple
    b.beam((-0.023, y - 0.002, EYE_Z + 0.012), (0.023, y - 0.002, EYE_Z + 0.012), 0.009, 0.009, "black")  # bridge

    vest = torso(km, kid_torso_profile(sx), "olive", ys=0.7)
    b = km.on("Spine")
    b.decal_strip(vest, [(i * 2 * math.pi / 28, vest.s_at_z(0.72)) for i in range(29)], 0.03, "green_dark", off=0.004)
    b.decal_disc(vest, FRONT, vest.s_at_z(0.935), 0.05, 0.07, "blue_pale", segs=10, off=0.004)     # shirt in the V
    b.decal_strip(vest, [(FRONT - 0.34, vest.s_at_z(0.978)), (FRONT, vest.s_at_z(0.865)), (FRONT + 0.34, vest.s_at_z(0.978))],
                  0.018, "green_dark", off=0.006)                                                   # V-neck rib
    for s in (1, -1):                                                                            # collar points
        b.prism([(0, 0), (0.05, 0.0), (0.012, -0.045)], 0.012, "white",
                matrix=Matrix.Translation((s * 0.012, -0.07, 0.99)) @ Matrix.Rotation(math.pi / 2 + 0.25, 4, "X")
                @ Matrix.Diagonal((s, 1, 1, 1)))
    # ранец: rigid box on the back, flap, two tin clasps, straps over the shoulders
    b.box((0, 0.15, 0.845), (0.25, 0.1, 0.27), "leather")
    b.box((0, 0.156, 0.915), (0.256, 0.1, 0.14), "leather_dark")
    b.box((0, 0.105, 0.97), (0.08, 0.02, 0.03), "leather_dark")                                  # handle base
    for s in (1, -1):
        b.box((s * 0.07, 0.207, 0.85), (0.03, 0.012, 0.04), "tin")
        b.beam((s * 0.085, 0.11, 0.975), (s * 0.095, -0.085, 0.978), 0.028, 0.012, "leather_dark")   # over the shoulder
        b.beam((s * 0.095, -0.1, 0.962), (s * 0.1, -0.104, 0.8), 0.028, 0.01, "leather_dark")       # down the front
    pelvis(km, "navy")
    for s in (1, -1):
        side = "Left" if s > 0 else "Right"
        arm(km, s, sx, skin, "blue_pale", long_sleeve=True, cuff="white")
        b = km.on(side + "UpperArm")
        for d in (0.055, 0.12):                                                                  # plaid lines
            b.cyl((s * (sx + d), 0, SHOULDER), (s * (sx + d + 0.012), 0, SHOULDER), 0.0505, 0.0505, segs=10, color="steel")
        b = km.on(side + "LowerArm")
        for d in (UPPER_ARM + 0.05, UPPER_ARM + 0.1):
            b.cyl((s * (sx + d), 0, SHOULDER), (s * (sx + d + 0.012), 0, SHOULDER), 0.0435, 0.0435, segs=10, color="steel")
        leg(km, s, hx, skin, "navy", sock="white", sock_top=0.23, shoe="leather", sole="leather_dark")
    km.finish(rig, col)
    rig.location = location
    return rig


def build_tolstyak(col, location=(0, 0, 0)):
    """Толстяк: round, sandy hair with a tuft, rosy cheeks, blue/white striped T-shirt, navy shorts,
    white ankle socks, dark sneakers with white toes. No backpack."""
    name, sx, hx = "Kid_Tolstyak", 0.19, 0.098
    rig = build_rig(name, col, sx, hx)
    km = KidMesh()
    skin = "skin_light"
    lat = head(km, skin, xs=1.07)
    ears(km, skin, xs=1.07)
    neck(km, skin)
    face(km, lat, iris="eye_blue", eye_w=0.025, eye_h=0.03, ex=0.062, brow="wood", brow_style="raised", mouth="grin",
         cheeks=(0.036, 0.026))
    hair_cap(km, lat, "wood_light", hairline(1.34, 1.262, 1.16), top_off=0.024, edge_off=0.008)
    for dx, tx in ((-0.05, -0.02), (0.0, 0.0), (0.05, 0.02)):
        tuft(km, (dx, -0.13, 1.385), (dx + tx, -0.172, 1.33), 0.032, "wood_light")
    tuft(km, (0.0, 0.0, 1.44), (0.035, -0.02, 1.505), 0.03, "wood_light")                     # the tuft on top
    edges = [0.66 + 0.046 * i for i in range(1, 9)]
    rz = lambda z: (max(0.178 + 0.042 * math.exp(-((z - 0.8) / 0.1) ** 2), 0.196 * min(1.0, max(0.0, (z - 0.86) / 0.07)))
                    if z <= 0.965 else 0.196 - 0.13 * ((z - 0.965) / 0.04) ** 1.5)
    prof = [(0.0, 0.65)] + profile_with(rz, 0.65, 1.0, edges) + [(0.0, 1.004)]
    prof = [(max(0.0, min(r, 0.225)), z) for r, z in prof]
    torso(km, prof, bands(["blue", "white"], edges), ys=0.88, yoff=-0.014)
    pelvis(km, "navy", r=0.17, ys=0.86, top=0.72)
    for s in (1, -1):
        side = "Left" if s > 0 else "Right"
        arm(km, s, sx, skin, "blue", sleeve_len=0.105, thick=1.25)
        b = km.on(side + "UpperArm")
        b.cyl((s * (sx + 0.04), 0, SHOULDER), (s * (sx + 0.07), 0, SHOULDER), 0.068, 0.068, segs=10, color="white")
        leg(km, s, hx, skin, "navy", shorts_bottom=0.45, sock="white", sock_top=0.14, shoe="grey_dark", sole="white",
            toe_cap="white", thick=1.3)
    km.finish(rig, col)
    rig.location = location
    return rig


def build_melkaya(col, location=(0, 0, 0)):
    """Мелкая: brown pigtails with big red bows, white T-shirt with a bunny, red shorts, scraped knees,
    white knee socks with a blue band, red sneakers."""
    name, sx, hx = "Kid_Melkaya", 0.138, 0.064
    rig = build_rig(name, col, sx, hx)
    km = KidMesh()
    skin = "skin_light"
    lat = head(km, skin)
    ears(km, skin)
    neck(km, skin)
    face(km, lat, iris="wood", eye_w=0.033, eye_h=0.04, brow="wood_dark", brow_style="focused", mouth="smile",
         lashes=True, freckles=True, cheeks=(0.022, 0.016))
    hair_cap(km, lat, "wood", hairline(1.325, 1.25, 1.13), top_off=0.026, edge_off=0.009)
    for dx, tx in ((-0.076, -0.012), (-0.038, 0.0), (0.0, 0.0), (0.038, 0.0), (0.076, 0.012)):      # bangs
        tuft(km, (dx, -0.135, 1.38), (dx + tx, -0.176, 1.31), 0.033, "wood")
    b = km.on("Head")
    for s in (1, -1):                                                                              # pigtails + bows
        root = Vector((s * 0.165, 0.035, 1.345))
        b.sphere(root + Vector((s * 0.055, 0.01, -0.035)), 0.058, 8, 5, "wood", scale=(1.0, 0.85, 1.35),
                 rot=(0.0, s * 0.55, 0.0))
        b.cyl(root + Vector((s * 0.065, 0.012, -0.11)), root + Vector((s * 0.085, 0.02, -0.19)), 0.036, 0.0, segs=6,
              color="wood")
        b.sphere(root, 0.03, 6, 4, "red")
        for dz in (0.035, -0.035):
            b.cyl(root, root + Vector((s * 0.012, -0.025, dz * 1.3)), 0.022, 0.048, segs=4, color="red")
    shirt = torso(km, kid_torso_profile(sx, waist=0.11, chest=0.12), "white", ys=0.7)
    b = km.on("Spine")
    s_b = shirt.s_at_z(0.84)
    rb = shirt.radius_at(s_b)
    b.decal_disc(shirt, FRONT, s_b - 0.02, 0.033, 0.041, "sand", segs=9, off=0.004)                    # bunny body
    b.decal_disc(shirt, FRONT, s_b + 0.03, 0.026, 0.024, "sand", segs=9, off=0.005)                    # head
    for s in (1, -1):
        b.decal_disc(shirt, FRONT + s * 0.012 / rb, s_b + 0.072, 0.008, 0.028, "sand", segs=6, off=0.004, rot=s * 0.2)
        b.decal_disc(shirt, FRONT + s * 0.012 / rb, s_b + 0.072, 0.004, 0.018, "pink", segs=5, off=0.007, rot=s * 0.2)
        b.decal_disc(shirt, FRONT + s * 0.009 / rb, s_b + 0.034, 0.003, 0.003, "black", segs=4, off=0.008)
    b.decal_disc(shirt, FRONT, s_b + 0.023, 0.004, 0.003, "pink", segs=4, off=0.008)
    b.decal_strip(shirt, [(FRONT - 0.3, shirt.s_at_z(0.985)), (FRONT, shirt.s_at_z(0.972)), (FRONT + 0.3, shirt.s_at_z(0.985))],
                  0.012, "pink_light", off=0.004)                                                  # neckline trim
    pelvis(km, "red", r=0.12, top=0.74, bottom=0.565)
    for s in (1, -1):
        arm(km, s, sx, skin, "white", sleeve_len=0.09)
        leg(km, s, hx, skin, "red", shorts_bottom=0.53, sock="white", sock_top=0.29, sock_band="blue_light", shoe="red",
            sole="white", toe_cap="white", knee_patch="coral")
    km.finish(rig, col)
    rig.location = location
    return rig


def build_huligan(col, location=(0, 0, 0)):
    """Хулиган: navy cap with a red patch, navy tracksuit with two white stripes open over a red T-shirt,
    white sneakers, a tough frown."""
    name, sx, hx = "Kid_Huligan", 0.158, 0.074
    rig = build_rig(name, col, sx, hx)
    km = KidMesh()
    skin = "skin_light"
    lat = head(km, skin)
    ears(km, skin)
    neck(km, skin)
    face(km, lat, iris="wood_dark", eye_w=0.029, eye_h=0.027, brow="brown", brow_style="angry", mouth="smirk")
    hair_cap(km, lat, "brown", hairline(1.31, 1.24, 1.16), top_off=0.012, edge_off=0.006)
    b = km.on("Head")                                                                               # cap
    cap = Lathe([(0.19, 1.3), (0.192, 1.335), (0.18, 1.38), (0.151, 1.418), (0.1, 1.442), (0.0, 1.45)], 16)
    b.lathe(cap, "navy")
    for i in range(6):                                                                              # panel seams
        ph = FRONT + i * math.pi / 3
        b.decal_strip(cap, [(ph, cap.s_at_z(1.305)), (ph, cap.s[-1] - 0.012)], 0.006, "indigo", off=0.003)
    b.decal_disc(cap, FRONT, cap.s_at_z(1.36), 0.056, 0.036, "red", segs=4, off=0.004, rot=math.pi / 4)
    b.decal_disc(cap, FRONT, cap.s_at_z(1.362), 0.018, 0.014, "yellow", segs=5, off=0.007)
    b.sphere((0, 0, 1.45), 0.017, 6, 3, "navy")
    visor = [(0.145 * math.cos(math.pi * i / 8), -0.125 * math.sin(math.pi * i / 8)) for i in range(9)]
    b.prism(list(reversed(visor)), 0.015, "navy",
            matrix=Matrix.Translation((0, -0.16, 1.302)) @ Matrix.Rotation(0.18, 4, "X"))
    jacket = torso(km, kid_torso_profile(sx, waist=0.126, chest=0.134), "navy", ys=0.72)
    b = km.on("Spine")
    b.decal_disc(jacket, FRONT, jacket.s_at_z(0.945), 0.062, 0.075, "red", segs=12, off=0.004)          # T-shirt in the opening
    for s in (1, -1):
        b.decal_strip(jacket, [(FRONT + s * 0.42, jacket.s_at_z(1.0)), (FRONT + s * 0.03, jacket.s_at_z(0.872))], 0.016,
                      "indigo", off=0.006)                                                           # lapels of the V
    b.decal_strip(jacket, [(FRONT, jacket.s_at_z(0.875)), (FRONT, jacket.s_at_z(0.7))], 0.01, "grey_light", off=0.004)  # zip
    b.decal_strip(jacket, [(i * 2 * math.pi / 28, jacket.s_at_z(0.705)) for i in range(29)], 0.028, "indigo", off=0.004)
    b.decal_disc(jacket, FRONT + 0.45, jacket.s_at_z(0.9), 0.016, 0.01, "white", segs=4, off=0.004)      # little logo
    pelvis(km, "navy")
    for s in (1, -1):
        arm(km, s, sx, skin, "navy", long_sleeve=True, cuff="indigo", stripes=("white", (-0.012, 0.012)))
        leg(km, s, hx, skin, "navy", pants=True, shoe="white", sole="grey_light", toe_cap="grey_light",
            stripes=("white", (-0.012, 0.012)))
    km.finish(rig, col)
    rig.location = location
    return rig


BUILDERS = [build_otlichnik, build_tolstyak, build_melkaya, build_huligan]
NAMES = ["Kid_Otlichnik", "Kid_Tolstyak", "Kid_Melkaya", "Kid_Huligan"]


def build_all(col_parent):
    col = L.collection(COLLECTION, col_parent)
    return [fn(col, (1.2 * i, 26.0, 0.0)) for i, fn in enumerate(BUILDERS)]


# ================================================================ posing (armature-space deltas)
def delta_quat(rig, bone, D):
    """Local pose rotation for a rotation D (3x3) given in the armature's rest axes, relative to the parent.
    The same D means the same thing on every kid, whatever the bone rolls."""
    R = rig.data.bones[bone].matrix_local.to_3x3()
    return (R.inverted() @ D @ R).to_quaternion()


def rot(axis, deg):
    return Matrix.Rotation(math.radians(deg), 3, axis)


def swing(v_from, v_to):
    return Vector(v_from).normalized().rotation_difference(Vector(v_to).normalized()).to_matrix()


def arm_dir(s, raise_deg, fwd_deg):
    """Upper arm direction: raise 0 = hanging down, 90 = horizontal, 180 = up; fwd 0 = to the side, 90 = forward,
    negative = behind. s = +1 left arm, -1 right arm."""
    r, f = math.radians(raise_deg), math.radians(fwd_deg)
    return Vector((s * math.sin(r) * math.cos(f), -math.sin(r) * math.sin(f), -math.cos(r)))


def apply_pose(rig, pose, frame=None):
    """pose: {bone: 3x3 delta} (+ optional 'loc': Vector for Hips). Missing bones go back to rest."""
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        D = pose.get(pb.name)
        pb.rotation_quaternion = delta_quat(rig, pb.name, D) if D is not None else (1, 0, 0, 0)
        if frame is not None:
            pb.keyframe_insert("rotation_quaternion", frame=frame)
    hips = rig.pose.bones["Hips"]
    loc = Vector(pose.get("loc", (0, 0, 0)))
    R = rig.data.bones["Hips"].matrix_local.to_3x3()
    hips.location = R.inverted() @ loc
    if frame is not None:
        hips.keyframe_insert("location", frame=frame)


def stand_pose():
    """Relaxed standing pose for previews: arms down a little away from the body, elbows slightly bent."""
    p = {}
    for s, side in ((1, "Left"), (-1, "Right")):
        p[side + "UpperArm"] = swing((s, 0, 0), arm_dir(s, 14, 10))
        p[side + "LowerArm"] = rot("Z", -s * 12)
    return p


# ================================================================ export
TURN = Matrix.Diagonal((-1.0, -1.0, 1.0, 1.0))


def export_rig(rig, folder, animations=False):
    """FBX of the armature and its mesh, turned 180 degrees like every asset (see ba_export): the kid's front -Y
    ends up +Z in Unity. Mesh data and armature bones are turned (and turned back after); pose keys are local
    to the bones, so the animations turn with them."""
    import os
    os.makedirs(folder, exist_ok=True)
    kids = [rig] + [c for c in rig.children_recursive if c.type == "MESH"]
    vl = bpy.context.view_layer
    for o in vl.objects:
        o.select_set(False)
    for o in kids:
        o.select_set(True)
    vl.objects.active = rig
    old = rig.location.copy()
    rig.location = (0, 0, 0)
    for o in kids[1:]:
        o.data.transform(TURN)
        o.data.update()
    rig.data.transform(TURN)
    path = os.path.join(folder, rig.name + ".fbx")
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
            axis_forward="Y", axis_up="Z", bake_space_transform=False,
            mesh_smooth_type="FACE", use_mesh_modifiers=False, use_triangles=False,
            use_custom_props=False, add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
            use_armature_deform_only=True, armature_nodetype="NULL",
            bake_anim=animations, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=animations, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0, path_mode="STRIP", embed_textures=False)
    finally:
        for o in kids[1:]:
            o.data.transform(TURN)
            o.data.update()
        rig.data.transform(TURN)
        rig.location = old
        for o in kids:
            o.select_set(False)
    return path


# ================================================================ props for the select screen poses
PROPS_COLLECTION = "KidProps"
PROPS = ["Prop_Book", "Prop_Chips", "Prop_JumpRope", "Prop_Football"]


def _prop(b, name, col, location):
    ob = b.to_object(name, col, pivot=(0, 0, 0))
    ob.location = location
    return ob


def build_book(col, location=(0, 0, 0)):
    """Отличник's textbook «ФИЗИКА»: teal hardcover, cream pages. Origin at the centre (held in the hand);
    the front cover faces -Y."""
    L.remove_tree("Prop_Book")
    b = Builder()
    w, t, h = 0.15, 0.036, 0.2
    for y in (-t / 2 + 0.0025, t / 2 - 0.0025):
        b.box((0, y, 0), (w, 0.005, h), "teal")                                         # covers
    b.box((-w / 2 + 0.003, 0, 0), (0.006, t, h), "bottle")                             # spine
    b.box((0.003, 0, 0), (w - 0.012, t - 0.008, h - 0.012), "cream")                   # pages
    b.decal("label_fizika", L.frame((0.004, -t / 2, 0.04), (math.pi / 2, 0, 0)), "white", width=0.112)
    b.box((0.004, -t / 2 - 0.001, 0.008), (0.1, 0.002, 0.005), "white")               # a rule under the title
    return _prop(b, "Prop_Book", col, location)


def build_chips(col, location=(0, 0, 0)):
    """Толстяк's bag of chips «ЧИПСЫ»: red pillow bag with crimped ends and a yellow band. Origin at the centre."""
    L.remove_tree("Prop_Chips")
    b = Builder()
    w, t, h = 0.13, 0.046, 0.14
    b.box((0, 0, 0), (w, t, h), "red", taper=None)
    b.box((0, 0, h / 2 + 0.014), (w, t, 0.028), "red", taper=(1.0, 0.15))             # crimped top
    b.box((0, 0, -h / 2 - 0.014), (w, t, 0.028), "red", rot=(math.pi, 0, 0), taper=(1.0, 0.15))
    b.box((0, 0, 0.012), (w + 0.002, t + 0.002, 0.052), "yellow")                     # band
    b.decal("label_chipsy", L.frame((0, -t / 2 - 0.001, 0.012), (math.pi / 2, 0, 0)), "red_dark", width=0.1)
    for dx, dz in ((-0.028, -0.042), (0.012, -0.05), (0.036, -0.036)):                 # chips on the picture
        b.box((dx, -t / 2 - 0.002, dz), (0.026, 0.002, 0.018), "ochre", rot=(0, dx * 8, 0))
    return _prop(b, "Prop_Chips", col, location)


def build_jump_rope(col, location=(0, 0, 0)):
    """Мелкая's jump rope: both red handles in one fist, the rope hanging in a loop that trails back.
    Origin at the grip."""
    L.remove_tree("Prop_JumpRope")
    b = Builder()
    for dx in (-0.018, 0.018):
        b.cyl((dx, 0, 0.03), (dx, 0, -0.08), 0.016, 0.016, segs=8, color="red")
        b.cyl((dx, 0, -0.08), (dx, 0, -0.095), 0.019, 0.013, segs=8, color="red_dark")
    pts = []
    for k in range(17):
        a = math.pi * k / 16
        r = 0.018 + 0.06 * math.sin(a)
        pts.append((r * math.cos(a), 0.09 * math.sin(a) ** 2, -0.095 - 0.46 * math.sin(a)))
    b.tube_path(pts, 0.007, "red_light", segs=5)
    return _prop(b, "Prop_JumpRope", col, location)


def build_football(col, location=(0, 0, 0)):
    """Хулиган's football: a slightly rounded truncated icosahedron, black pentagons, white hexagons, r = 0.11.
    Origin at the centre."""
    import bmesh
    L.remove_tree("Prop_Football")
    b = Builder()
    r = 0.11
    ico = bmesh.ops.create_icosphere(b.bm, subdivisions=1, radius=r)
    if len(ico["verts"]) != 12:
        raise RuntimeError("expected an icosahedron, got %d verts" % len(ico["verts"]))
    bmesh.ops.bevel(b.bm, geom=ico["verts"], offset=33.34, offset_type="PERCENT", affect="VERTICES", segments=1)
    for v in b.bm.verts:
        v.co = v.co.normalized() * (r * 0.4 + v.co.length * 0.6)
    b.paint(list(b.bm.faces), lambda f: "black" if len(f.verts) == 5 else "white")
    return _prop(b, "Prop_Football", col, location)


def build_props(col_parent):
    col = L.collection(PROPS_COLLECTION, col_parent)
    return [build_book(col, (5.0, 26.0, 1.0)), build_chips(col, (5.4, 26.0, 1.0)),
            build_jump_rope(col, (5.8, 26.0, 1.0)), build_football(col, (6.3, 26.0, 0.11))]


def export_kids(folder=None):
    """Kid models (T-pose, no clips) to Art/Models/Kids; the clips come from ba_kids_anim.export."""
    import os
    folder = folder or os.path.join(L.REPO, "Assets/_Project/Art/Models/Kids")
    out = []
    for name in NAMES:
        rig = bpy.data.objects[name]
        apply_pose(rig, {})
        out.append(export_rig(rig, folder))
    return out

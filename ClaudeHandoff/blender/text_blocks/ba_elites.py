"""Elite versions of the day toys: each has its own look (a readable silhouette detail + gold/red paint).
The base models come from ba_enemies / ba_toys under an Elite_ name; then the paint is swapped (UVs moved
to other palette cells) and accessories are merged into the parts. The hierarchy stays the same as the
base toy, so the same animator code drives the elite; make it bigger with the prefab scale."""
import math
import bpy, bmesh
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe
import ba_enemies as E
import ba_toys as T

FRONT = -math.pi / 2


# ================================================================ helpers
def recolor(root, mapping, exclude=()):
    """Repaint an already built asset: every face in cell `src` moves to cell `dst` (mapping src -> dst)."""
    remap = {L.INDEX[a]: L.INDEX[b] for a, b in mapping.items()}
    for o in [root] + list(root.children_recursive):
        if o.type != "MESH" or o.name in exclude:
            continue
        for d in o.data.uv_layers.active.data:
            u, v = d.uv
            cell = (int(u * L.GRID), int((1.0 - v) * L.GRID))
            if cell in remap:
                c2, r2 = remap[cell]
                d.uv = (u + (c2 - cell[0]) / L.GRID, v - (r2 - cell[1]) / L.GRID)


def merge_into(ob, builder, scale=1.0):
    """Append a builder's geometry (asset coordinates) to an existing part mesh."""
    bm = builder.bm
    pv = Vector(ob["pivot"])
    for v in bm.verts:
        v.co = v.co * scale - pv
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    if ngons:
        bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
    for f in bm.faces:
        f.smooth = False
    tmp = bpy.data.meshes.new("_ba_merge")
    bm.to_mesh(tmp)
    bm.free()
    target = bmesh.new()
    target.from_mesh(ob.data)
    target.from_mesh(tmp)
    target.normal_update()
    target.to_mesh(ob.data)
    target.free()
    bpy.data.meshes.remove(tmp)
    ob.data.update()


def adopt_names(root, old_prefix, new_prefix, base_root_name):
    """The ba_enemies builders give parts fixed names (Pupsik_Body...), so a second copy gets .001 names and
    steals the base meshes' names. Rename the copy's parts to new_prefix and give the base meshes their names back."""
    for o in list(root.children_recursive):
        base = o.name.split(".")[0]
        if base.startswith(old_prefix):
            o.name = new_prefix + base[len(old_prefix):]
        if o.type == "MESH":
            o.data.name = o.name
    base_root = bpy.data.objects.get(base_root_name)
    if base_root is not None:
        for o in base_root.children_recursive:
            if o.type == "MESH":
                o.data.name = o.name
    for me in list(bpy.data.meshes):
        if me.users == 0 and me.name.endswith("_old"):
            bpy.data.meshes.remove(me)


def star_pts(r_out, r_in, n=5, rot=math.pi / 2):
    return T._star2d(r_out, r_in, n, rot)


def tangent_frame(angle, radius, z, lean=0.0):
    """Frame on a vertical circle around Z: X along the tangent, Y up (leaning inwards by `lean`), Z outwards."""
    n = Vector((math.cos(angle), math.sin(angle), 0.0))
    x = Vector((-math.sin(angle), math.cos(angle), 0.0))
    y = Vector((0, 0, 1)) * math.cos(lean) - n * math.sin(lean)
    out = x.cross(y)
    m = Matrix((x, y, out)).transposed().to_4x4()
    m.translation = Vector((n.x * radius, n.y * radius, z))
    return m


def tangent_box(b, angle, radius, z_base, height, width, depth, color, lean=0.0):
    m = tangent_frame(angle, radius, z_base, lean)
    c = m @ Vector((0, height / 2, 0))
    mm = m.to_3x3().to_4x4()
    mm.translation = c
    b.box_m(mm, (width, height, depth), color)
    return m @ Vector((0, height, 0))          # top-centre point


# ================================================================ golden matryoshka in a kokoshnik
def build_elite_roly_poly(col, location, name="Elite_RolyPoly"):
    root = E.build_roly_poly(col, location, name=name, seed=13)
    recolor(root, {"red": "gold", "red_light": "red"})
    part = name.replace("Enemy_", "")
    body, head = bpy.data.objects[part + "_Body"], bpy.data.objects[part + "_Head"]

    # Khokhloma curls round the sides and back of the body
    b = Builder()
    lat = Lathe(E.ROLY_BODY, 16)
    for k in range(6):
        phi0 = FRONT + math.pi * (0.42 + 0.23 * k)
        s0 = lat.s_at_z(0.36 if k % 2 == 0 else 0.6)
        r0 = lat.radius_at(s0)
        pts = []
        for i in range(12):
            t = i / 11
            th = 2.6 * math.pi * t
            rho = 0.1 * (1.0 - 0.8 * t)
            pts.append((phi0 + rho * math.cos(th) / r0, s0 + rho * math.sin(th)))
        b.decal_strip(lat, pts, 0.022, "red", off=0.006)
        for j in (-1, 1):
            b.decal_disc(lat, phi0 + j * 0.13 / r0, s0 - 0.1, 0.03, 0.018, "black", segs=6, off=0.007, rot=j * 0.6)
    merge_into(body, b)

    # kokoshnik: a red crescent round the top of the head, pearls on the edge, a green stone in the middle
    h = Builder()
    n = 11
    spread = 1.25
    for i in range(n):
        t = (i + 0.5) / n * 2 - 1                       # -1 .. 1 across the crescent
        a = FRONT + t * spread
        z_b = 1.19 - 0.08 * t * t
        height = 0.07 + 0.26 * math.cos(t * math.pi / 2) ** 1.2
        width = 2 * 0.29 * math.sin(spread / n) + 0.012
        top = tangent_box(h, a, 0.29, z_b, height, width, 0.03, "red", lean=0.3)
        tangent_box(h, a, 0.295, z_b - 0.005, 0.035, width, 0.036, "yellow", lean=0.3)
        h.sphere(top, 0.02, 6, 4, "white")
    gem = tangent_frame(FRONT, 0.3, 1.19, 0.3) @ Vector((0, 0.17, 0.02))
    h.sphere(gem, 0.045, 8, 5, "green", scale=(1, 0.6, 1.2))
    h.sphere(gem + Vector((0, 0.012, 0)), 0.058, 8, 5, "yellow", scale=(1, 0.4, 1.25))
    merge_into(head, h)
    return root


# ================================================================ pupsik in a bonnet, with a dummy and a bib
def build_elite_pupsik(col, location, name="Elite_Pupsik"):
    scale = 1.3
    root = E.build_pupsik(col, location, name=name, scale=scale)
    adopt_names(root, "Pupsik_", "Elite_Pupsik_", "Enemy_Pupsik")
    body = bpy.data.objects["Elite_Pupsik_Body"]

    b = Builder()
    # bonnet: a spherical hood round the back of the head, the opening framing the face
    m = Matrix.Translation((0, 0.0, 0.475)) @ Matrix.Rotation(1.83, 4, "X")
    R = 0.182
    prof = [(0.0, -R)]
    for k in range(1, 8):
        z = -R + (R + 0.05) * k / 7
        prof.append((math.sqrt(max(0.0, R * R - z * z)), z))
    outer = Lathe(prof, 14, m)
    b.lathe(outer, "pink_light", cap_bottom=False, cap_top=False)
    inner = Lathe([(r * 0.93, z * 0.93) for r, z in prof], 14, m)
    faces = b.lathe(inner, "pink_light", cap_bottom=False, cap_top=False)
    bmesh.ops.reverse_faces(b.bm, faces=faces)
    rim_r, rim_z = prof[-1]
    for k in range(18):                                                    # lace frill round the face
        a = 2 * math.pi * k / 18
        b.sphere(m @ Vector((rim_r * math.cos(a), rim_r * math.sin(a), rim_z)), 0.024, 6, 4, "white")
    for sx in (-1, 1):                                                     # ribbon ties and the bow
        start = m @ Vector((sx * rim_r * 0.9, -rim_r * 0.45, rim_z))
        b.beam(start, (sx * 0.03, -0.1, 0.33), 0.018, 0.008, "pink")
        b.prism([(0, 0), (0.06, -0.03), (0.06, 0.03)] if sx > 0 else [(0, 0), (-0.06, 0.03), (-0.06, -0.03)],
                0.02, "pink", matrix=L.frame((0, -0.1, 0.325), (math.pi / 2, 0, 0)))
    b.sphere((0, -0.115, 0.325), 0.017, 6, 4, "pink")
    # dummy in the mouth
    b.cyl((0, -0.155, 0.385), (0, -0.182, 0.385), 0.042, 0.042, segs=10,
          color=lambda f: "sky" if abs(f.normal.y) > 0.9 else "blue_light")
    ring = [(0.028 * math.cos(a), -0.196, 0.385 + 0.028 * math.sin(a)) for a in [k * 2 * math.pi / 8 for k in range(9)]]
    b.tube_path(ring, 0.008, "blue", segs=5)
    b.sphere((0, -0.19, 0.385), 0.014, 6, 4, "white")
    # bib on the chest
    b.sphere((0, -0.14, 0.268), 0.092, 12, 5, "pink", scale=(1.0, 0.14, 0.82))
    b.sphere((0, -0.146, 0.268), 0.083, 12, 5, "white", scale=(1.0, 0.14, 0.82))
    b.sphere((0, -0.16, 0.26), 0.018, 6, 4, "yellow", scale=(1.2, 0.4, 1.0))
    merge_into(body, b, scale=scale)
    return root


# ================================================================ officer tin soldier with a sabre
TORSO = [(0.0, 0.5), (0.175, 0.5), (0.168, 0.56), (0.148, 0.62), (0.152, 0.7), (0.162, 0.78), (0.158, 0.85),
         (0.12, 0.9), (0.06, 0.915), (0.0, 0.92)]


def build_elite_tin_soldier(col, location, name="Elite_TinSoldier"):
    root = E.build_tin_soldier(col, location, name=name, seed=12)
    adopt_names(root, "TinSoldier_", "Elite_TinSoldier_", "Enemy_TinSoldier")
    recolor(root, {"blue": "red", "red": "gold"}, exclude=("Elite_TinSoldier_Ball",))
    body = bpy.data.objects["Elite_TinSoldier_Body"]
    arm_l = bpy.data.objects["Elite_TinSoldier_ArmL"]

    b = Builder()
    b.sphere((0, -0.045, 1.5), 0.045, 8, 5, "white", scale=(1.0, 1.0, 2.4))            # tall white plume
    for sgn in (-1, 1):                                                                # epaulette fringe
        for k in range(5):
            y = -0.05 + 0.025 * k
            b.box((sgn * 0.215, y, 0.835), (0.012, 0.012, 0.06), "yellow", rot=(0, sgn * 0.35, 0))
    lat = Lathe(TORSO, 8)
    for k, dx in enumerate((-0.05, 0.0)):                                              # two medals
        s = lat.s_at_z(0.735)
        b.decal_disc(lat, FRONT + 0.33 + dx / 0.15, s + 0.025, 0.012, 0.02, "sky", segs=4, off=0.012)
        b.decal_disc(lat, FRONT + 0.33 + dx / 0.15, s - 0.012, 0.018, 0.018, "yellow" if k else "white", segs=7, off=0.012)
    merge_into(body, b)

    s = Builder()                                    # sabre held upright at the left shoulder, outside the arm
    s.beam((0.225, -0.03, 0.47), (0.232, -0.035, 0.575), 0.024, 0.024, "wood_dark")                        # grip
    s.sphere((0.224, -0.03, 0.465), 0.018, 6, 4, "gold")                                                     # pommel
    s.beam((0.2, -0.035, 0.585), (0.275, -0.04, 0.585), 0.02, 0.03, "gold")                                 # guard
    blade = [Vector((0.24, -0.04, 0.595)), Vector((0.27, -0.07, 0.86)), Vector((0.29, -0.11, 1.08)),
             Vector((0.3, -0.15, 1.16))]
    for p0, p1 in zip(blade, blade[1:]):
        s.beam(p0, p1, 0.032, 0.012, "tin", up=(0, 1, 0))
    merge_into(arm_l, s)
    return root


# ================================================================ sailor bear
def banded_shell(b, lat, bands, off):
    """Thin shell over a Lathe between band edges, painted band by band (a striped vest).
    bands = [(z_from, colour), ...] bottom to top, the last entry only closes the top edge.
    Rings are also cut at the lathe's own profile points so the shell follows its bends."""
    z0, z1 = bands[0][0], bands[-1][0]
    zs = sorted({round(z, 4) for z, _ in bands} | {z for _, z in lat.profile if z0 < z < z1})
    prof = [(lat.at(lat.s_at_z(z))[0] + off, z) for z in zs]
    shell = Lathe(prof, lat.segs, lat.m)

    def paint(f):
        zc = f.calc_center_median().z
        col = bands[0][1]
        for z, c in bands[:-1]:
            if zc >= z:
                col = c
        return col
    return b.lathe(shell, paint, cap_bottom=False, cap_top=False)


def _sailor(parts):
    lat = parts["torso_lathe"]
    b = parts["body"]
    bands, z, k = [], 0.5, 0
    while z < 1.07:
        bands.append((z, "white" if k % 2 == 0 else "navy"))
        z += 0.05 if k % 2 == 0 else 0.03
        k += 1
    bands.append((min(z, 1.1), None))
    banded_shell(b, lat, bands, 0.0055)
    h = parts["head"]
    hz = 1.44
    h.cyl((0, 0.02, hz + 0.19), (0, 0.035, hz + 0.255), 0.205, 0.205, segs=14, color="navy")
    h.cyl((0, 0.035, hz + 0.255), (0, 0.04, hz + 0.3), 0.235, 0.235, segs=14,
          color=lambda f: "white" if f.normal.z > 0.5 else "grey_light")
    h.box((0, -0.19, hz + 0.225), (0.07, 0.02, 0.04), "gold")                        # cap badge
    for sx in (-1, 1):                                                                # ribbons
        h.beam((sx * 0.03, 0.2, hz + 0.21), (sx * 0.07, 0.33, hz + 0.02), 0.04, 0.01, "navy", up=(0, 1, 0))
        h.box((sx * 0.07, 0.335, hz + 0.015), (0.04, 0.012, 0.04), "gold", rot=(0.4, 0, 0))


def build_elite_bear(col, location, name="Elite_Bear"):
    return T.build_bear(col, location, name=name, seed=6, fur="wood", dark="leather", light="sand", extras=_sailor)


# ================================================================ tin rooster
def _rooster(parts):
    b = parts["body"]
    for k, (dx, col_) in enumerate(((-0.1, "green"), (-0.05, "teal"), (0.0, "blue"), (0.05, "green_dark"),
                                    (0.1, "red"))):
        lift = 0.06 * (2 - abs(k - 2))
        pts = [Vector((dx, 0.22, 0.48)), Vector((dx * 1.4, 0.36, 0.66 + lift)), Vector((dx * 1.8, 0.46, 0.84 + lift)),
               Vector((dx * 2.0, 0.44, 0.97 + lift)), Vector((dx * 2.0, 0.36, 1.02 + lift))]
        b.tube_path(pts, 0.032, col_, segs=5)
    h = parts["head"]
    hz = 0.72
    for y, r in ((-0.14, 0.06), (-0.07, 0.075), (0.0, 0.07), (0.06, 0.055)):          # big comb
        h.sphere((0, y - 0.04, hz + 0.19), r, 8, 5, "red", scale=(0.5, 1, 1.2))
    for sx in (-1, 1):                                                                # wattles
        h.sphere((sx * 0.025, -0.23, hz - 0.1), 0.035, 6, 4, "red", scale=(0.7, 0.7, 1.3))


def build_elite_chick(col, location, name="Elite_Chick"):
    return T.build_chick(col, location, name=name, seed=10, main="orange", wing="red", beak="yellow", legs="yellow",
                         extras=_rooster)


# ================================================================ sputnik spinning top
def _sputnik(parts):
    b = parts["body"]
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        p0 = Vector((0.5 * math.cos(a), 0.5 * math.sin(a), 0.5))
        p1 = Vector((1.0 * math.cos(a), 1.0 * math.sin(a), 0.2))
        b.cyl(p0, p1, 0.014, 0.01, segs=5, color="tin")
        b.sphere(p1, 0.035, 6, 4, "red")
    h = parts["handle"]
    h.prism(star_pts(0.085, 0.036), 0.025, "red",
            matrix=Matrix.Translation((0, 0.0125, 1.31)) @ Matrix.Rotation(math.pi / 2, 4, "X"))


def build_elite_top(col, location, name="Elite_Top"):
    return T.build_top(col, location, name=name, colors=("navy", "white", "red", "gold"), extras=_sputnik)


# ================================================================ fire horse
def _fire(b):
    b.cyl((0, -0.47, 1.23), (0, -0.47, 1.29), 0.03, 0.035, segs=6, color="gold")          # plume holder
    b.sphere((0, -0.47, 1.4), 0.07, 8, 5, "red", scale=(0.55, 0.55, 1.7))
    b.sphere((0, -0.47, 1.53), 0.035, 6, 4, "yellow", scale=(0.8, 0.8, 1.4))
    back = Vector((0, 0.894, 0.447))
    for k in range(8):                                                                # flame tips on the mane
        t = 0.02 + k / 7
        axis = Vector((0, -0.3 - 0.16 * t, 0.8 + 0.32 * t))
        p = axis + back * (0.19 - 0.03 * t)
        b.box(p, (0.04, 0.05, 0.08), "yellow" if k % 2 else "orange", rot=(-0.6, 0, 0))
    for dx in (-0.06, 0.0, 0.06):                                                     # flame tips on the tail
        b.sphere((dx * 2.4, 0.7, 0.45), 0.03, 6, 4, "yellow", scale=(1, 1, 1.6))
    b.cyl((0, -0.4, 0.74), (0, -0.445, 0.74), 0.07, 0.07, segs=10,
          color=lambda f: "gold" if abs(f.normal.y) > 0.9 else "ochre")                   # medallion
    b.cyl((0, -0.44, 0.74), (0, -0.452, 0.74), 0.035, 0.035, segs=8, color="red")


def build_elite_rocking_horse(col, location, name="Elite_RockingHorse"):
    return T.build_rocking_horse(col, location, name=name, coat="grey_dark", spots="black", mane="orange_red",
                                 saddle="gold", rocker="red_dark", muzzle="grey", extras=_fire)


def build_all(col_parent):
    col = L.collection("Enemies", col_parent)
    y = -12.0
    return [
        build_elite_roly_poly(col, (0.0, y, 0.0)),
        build_elite_pupsik(col, (1.4, y, 0.0)),
        build_elite_tin_soldier(col, (2.6, y, 0.0)),
        build_elite_bear(col, (4.2, y, 0.0)),
        build_elite_chick(col, (5.8, y, 0.0)),
        build_elite_top(col, (7.2, y, 0.0)),
        build_elite_rocking_horse(col, (9.2, y, 0.0)),
    ]

"""Arena 3, the abandoned construction site («Стройка», dusk): concrete slabs, rebar, well rings, sand and
gravel, pallets of bricks, barrels (one with a fire), a cable reel, the site cabin, the diamond-pattern
concrete fence ПО-2, an unfinished frame, a tower crane, work lights, a wooden lamp post and the ground.
Light sources (fire, lamps, the cabin window) use the lamp / window_lit cells, so they glow at dusk —
the shadows are only vulnerable in their light. Props: origin on the ground, front to -Y."""
import math, random
import bmesh
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

SITE_W, SITE_D = 44.0, 30.0
UP = Vector((0, 0, 1))


def ccw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return pts if area > 0 else list(reversed(pts))


def circle(r, n, cx=0.0, cy=0.0):
    return [(cx + r * math.cos(2 * math.pi * k / n), cy + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


def concrete(f):
    return "concrete_light" if f.normal.z > 0.9 else "concrete"


def jitter(b, faces, amount, rnd, keep_z_below=None):
    """Roughen a primitive: move its vertices a little (not those at or below keep_z_below)."""
    verts = {v for f in faces for v in f.verts}
    for v in verts:
        if keep_z_below is not None and v.co.z <= keep_z_below + 1e-4:
            continue
        v.co += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * amount


# ================================================================ slabs, rebar, rings
def _slab(b, m, length=4.8, width=1.2, thick=0.22, holes=5):
    """Hollow-core floor slab (ПК) on frame m (centre), long axis = local X; round voids at both ends."""
    b.box_m(m, (length, width, thick), concrete)
    for sx in (-1, 1):
        for k in range(holes):
            y = -width / 2 + width * (k + 0.5) / holes
            fm = m @ Matrix.Translation((sx * (length / 2 - 0.001), y, 0)) @ Matrix.Rotation(sx * math.pi / 2, 4, "Y")
            b.prism(circle(thick * 0.3, 8), 0.004, "grey_dark", matrix=fm)


def slab_stack(col, loc, name="Site_SlabStack", seed=71):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    z = 0.0
    for k in range(3):
        for x in (-1.6, 1.6):                                            # wooden spacers
            b.box((x + rnd.uniform(-0.1, 0.1), 0, z + 0.04), (0.1, 1.3, 0.08), "wood")
        z += 0.08
        m = L.frame((rnd.uniform(-0.15, 0.15), rnd.uniform(-0.08, 0.08), z + 0.11), (0, 0, rnd.uniform(-0.05, 0.05)))
        _slab(b, m)
        z += 0.22
    for _ in range(4):                                                   # stains
        b.box((rnd.uniform(-2, 2), rnd.uniform(-0.4, 0.4), z + 0.002), (rnd.uniform(0.3, 0.8), rnd.uniform(0.2, 0.5), 0.004),
              "grey")
    return L.mesh_root(b, name, col, loc)


def slab_tilted(col, loc, name="Site_SlabTilted"):
    """A slab resting with one end on a concrete block: cover to hide behind, a ramp for balls."""
    L.remove_tree(name)
    b = Builder()
    b.box((1.9, 0, 0.3), (0.6, 1.0, 0.6), concrete)
    ang = math.atan2(0.6, 4.4)
    _slab(b, L.frame((0.0, 0, 0.3 + 0.11), (0, -ang, 0)))
    return L.mesh_root(b, name, col, loc)


def column_rebar(col, loc, name="Site_ColumnRebar", seed=72):
    """Stub of a concrete column, broken at the top, bent rebar sticking out."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    b.box((0, 0, 0.45), (0.42, 0.42, 0.9), concrete)
    for k in range(4):                                                   # broken chunks on top
        b.box((rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), 0.93), (0.18, 0.16, 0.1), "concrete",
              rot=(rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(0, 1)))
    for sx in (-1, 1):
        for sy in (-1, 1):
            p = Vector((sx * 0.15, sy * 0.15, 0.85))
            bend = Vector((sx * rnd.uniform(0.1, 0.35), sy * rnd.uniform(0.0, 0.25), 0))
            pts = [p, p + Vector((0, 0, 0.45)), p + Vector((0, 0, 0.75)) + bend * 0.5, p + Vector((0, 0, 0.85)) + bend]
            b.tube_path(pts, 0.014, "rust", segs=5)
    return L.mesh_root(b, name, col, loc)


def rebar_bundle(col, loc, name="Site_RebarBundle", seed=73):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    for x in (-1.5, 0.0, 1.5):
        b.box((x, 0, 0.05), (0.14, 0.9, 0.1), "wood_dark")
    for k in range(14):
        y = -0.3 + (k % 7) * 0.09 + rnd.uniform(-0.01, 0.01)
        z = 0.12 + (k // 7) * 0.03
        b.cyl((-2.0 + rnd.uniform(-0.1, 0.1), y, z), (2.0 + rnd.uniform(-0.1, 0.1), y, z), 0.014, 0.014, segs=5, color="rust")
    for x in (-0.8, 0.8):
        b.box((x, 0, 0.14), (0.02, 0.7, 0.07), "grey_dark")
    return L.mesh_root(b, name, col, loc)


def _ring_lathe(m=None):
    prof = [(0.66, 0.0), (0.78, 0.0), (0.78, 0.9), (0.66, 0.9), (0.66, 0.0)]
    return Lathe(prof, 16, m)


def well_ring(col, loc, name="Site_Ring", lying=False):
    """Concrete well ring (колодезное кольцо) 1.56 m wide: standing, or lying on its side to crawl through."""
    L.remove_tree(name)
    b = Builder()
    m = None
    if lying:
        m = Matrix.Translation((0, 0.45, 0.78)) @ Matrix.Rotation(math.pi / 2, 4, "X")
    b.lathe(_ring_lathe(m), lambda f: "concrete_light" if f.normal.z > 0.8 else "concrete", cap_bottom=False, cap_top=False)
    return L.mesh_root(b, name, col, loc)


def rings_stack(col, loc, name="Site_RingsStack"):
    L.remove_tree(name)
    b = Builder()
    for z in (0.0, 0.92):
        b.lathe(_ring_lathe(Matrix.Translation((0, 0, z))), lambda f: "concrete_light" if f.normal.z > 0.8 else "concrete",
                cap_bottom=False, cap_top=False)
    return L.mesh_root(b, name, col, loc)


# ================================================================ piles, pallets, barrels, reel
def _mound(b, r, h, colors, rnd, segs=12):
    prof = [(r, 0.0), (r * 0.8, h * 0.3), (r * 0.5, h * 0.72), (r * 0.18, h * 0.96), (0.0, h)]
    lat = Lathe(prof, segs)
    faces = b.lathe(lat, lambda f: rnd.choice(colors), cap_bottom=True, cap_top=False)
    jitter(b, faces, r * 0.06, rnd, keep_z_below=0.0)
    return faces


def sand_pile(col, loc, name="Site_SandPile", seed=74):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    _mound(b, 1.7, 0.95, ["sand", "sand", "sand_dark"], rnd)
    b.cyl((0.45, -0.2, 0.55), (0.62, -0.28, 1.6), 0.022, 0.022, segs=6, color="wood")          # shovel
    b.box((0.42, -0.19, 0.45), (0.22, 0.03, 0.28), "grey", rot=(0.15, -0.2, 0.2))
    return L.mesh_root(b, name, col, loc)


def gravel_pile(col, loc, name="Site_GravelPile", seed=75):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    _mound(b, 1.5, 0.75, ["grey", "grey_light", "concrete"], rnd)
    for _ in range(10):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(1.5, 2.0)
        b.ico((r * math.cos(a), r * math.sin(a), 0.04), rnd.uniform(0.05, 0.1), rnd.choice(["grey", "concrete"]),
              subdiv=1, jitter=0.02, seed=rnd.randint(0, 999))
    return L.mesh_root(b, name, col, loc)


def _pallet(b, z0=0.0):
    for x in (-0.5, 0.0, 0.5):
        b.box((x, 0, z0 + 0.05), (0.1, 0.8, 0.1), "wood")
    for k in range(5):
        b.box((0, -0.34 + k * 0.17, z0 + 0.12), (1.2, 0.12, 0.03), "wood_light")


def brick_pallet(col, loc, name="Site_BrickPallet", seed=76):
    """Pallet with a strapped stack of bricks; a few loose bricks around."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    _pallet(b)
    b.box((0, 0, 0.44), (1.0, 0.7, 0.6), "rust_light")
    for k in range(1, 8):                                                # mortar lines
        z = 0.14 + k * 0.075
        b.box((0, 0, z), (1.004, 0.704, 0.008), "rust")
    for x in (-0.3, 0.3):                                                # straps
        b.box((x, 0, 0.44), (0.03, 0.71, 0.61), "blue")
    for _ in range(3):
        b.box((rnd.uniform(-0.9, 0.9), rnd.uniform(0.5, 0.8), 0.04), (0.25, 0.12, 0.07), "rust_light",
              rot=(0, 0, rnd.uniform(0, 3)))
    return L.mesh_root(b, name, col, loc)


def pallet(col, loc, name="Site_Pallet"):
    L.remove_tree(name)
    b = Builder()
    _pallet(b)
    return L.mesh_root(b, name, col, loc)


def _barrel(b, body, rnd, rust_spots=6):
    prof = [(0.0, 0.0), (0.29, 0.0), (0.3, 0.02), (0.3, 0.88), (0.29, 0.9), (0.0, 0.9)]
    lat = Lathe(prof, 12)
    b.lathe(lat, lambda f: "tin_dark" if f.normal.z > 0.9 else body)
    for z in (0.3, 0.6):
        b.cyl((0, 0, z), (0, 0, z + 0.03), 0.31, 0.31, segs=12, color=body)
    for _ in range(rust_spots):
        b.decal_disc(lat, rnd.uniform(0, 2 * math.pi), lat.s_at_z(rnd.uniform(0.05, 0.85)), rnd.uniform(0.03, 0.08),
                     rnd.uniform(0.03, 0.1), "rust", segs=6, off=0.004, rot=rnd.uniform(0, 3))
    return lat


def barrel(col, loc, name="Site_Barrel", seed=77):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    _barrel(b, "blue", rnd)
    b.cyl((0.12, 0.05, 0.9), (0.12, 0.05, 0.92), 0.04, 0.04, segs=6, color="tin")
    return L.mesh_root(b, name, col, loc)


def fire_barrel(col, loc, name="Site_FireBarrel", seed=78):
    """Rusty barrel with a fire inside: the flames are lamp / window_lit / orange, so they glow at dusk."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    prof = [(0.0, 0.0), (0.29, 0.0), (0.3, 0.02), (0.3, 0.9), (0.26, 0.9), (0.26, 0.7), (0.0, 0.7)]
    lat = Lathe(prof, 12)
    b.lathe(lat, lambda f: "black" if f.calc_center_median().z > 0.69 and abs(f.normal.z) < 0.5 and
            f.calc_center_median().xy.length < 0.28 else "rust")
    for _ in range(8):
        b.decal_disc(lat, rnd.uniform(0, 2 * math.pi), lat.s_at_z(rnd.uniform(0.05, 0.8)), rnd.uniform(0.03, 0.08),
                     rnd.uniform(0.03, 0.1), "rust_light", segs=6, off=0.004, rot=rnd.uniform(0, 3))
    for k in range(4):                                                   # air holes glowing low down
        a = k * math.pi / 2 + 0.4
        b.box((0.301 * math.cos(a), 0.301 * math.sin(a), 0.2), (0.08, 0.08, 0.05), "orange", rot=(0, 0, a))
    for k in range(7):                                                   # flames
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.0, 0.16)
        base = Vector((r * math.cos(a), r * math.sin(a), 0.72))
        h = rnd.uniform(0.3, 0.6)
        tip = base + Vector((rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06), h))
        b.cyl(base, tip, rnd.uniform(0.07, 0.11), 0.0, segs=5, color=["orange", "window_lit", "lamp"][k % 3])
    return L.mesh_root(b, name, col, loc)


def cable_reel(col, loc, name="Site_CableReel"):
    """Wooden cable spool standing on its rims (axis along X), some black cable left on the drum."""
    L.remove_tree(name)
    b = Builder()
    R = 0.9
    for x in (-0.42, 0.42):
        b.cyl((x - 0.03, 0, R), (x + 0.03, 0, R), R, R, segs=14, color=lambda f: "wood" if abs(f.normal.x) > 0.9 else "wood_dark")
        for k in range(6):                                               # planks on the rim
            a = k * math.pi / 3
            b.box((x + (0.034 if x > 0 else -0.034), 0.45 * math.cos(a), R + 0.45 * math.sin(a)), (0.008, 0.05, 0.9),
                  "wood_dark", rot=(a, 0, 0))
    b.cyl((-0.39, 0, R), (0.39, 0, R), 0.35, 0.35, segs=12, color="wood_light")
    b.cyl((-0.39, 0, R), (0.1, 0, R), 0.47, 0.47, segs=12, color="black")
    return L.mesh_root(b, name, col, loc)


# ================================================================ cabin and fence
def cabin(col, loc, name="Site_Cabin", seed=79):
    """Site cabin (бытовка) 5 x 2.3 m on blocks: ribbed metal walls, barred window (lit at dusk: the
    watchman is in), door with steps, a sign «ВХОД ВОСПРЕЩЁН», rust streaks, a cable."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    W, D, H, z0 = 5.0, 2.3, 2.4, 0.2
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * 2.1, sy * 0.85, 0.1), (0.4, 0.3, 0.2), "concrete")
    b.box((0, 0, z0 + H / 2), (W, D, H), "steel")
    b.box((0, 0, z0 + H + 0.06), (W + 0.12, D + 0.12, 0.12), lambda f: "roof" if f.normal.z > 0.9 else "tin_dark")
    for sy in (-1, 1):                                                   # ribs on the long walls
        for k in range(11):
            x = -W / 2 + 0.25 + k * 0.45
            if sy < 0 and (0.9 < x < 2.3 or -1.7 < x < -0.3):
                continue
            b.box((x, sy * (D / 2 + 0.01), z0 + H / 2), (0.04, 0.03, H - 0.1), "tin_dark")
    b.box((1.6, -D / 2 - 0.02, z0 + 1.0), (0.9, 0.05, 1.95), "blue")          # door
    b.box((1.25, -D / 2 - 0.06, z0 + 1.0), (0.05, 0.05, 0.16), "tin")
    for k, (z, dep) in enumerate(((0.12, 0.7), (0.3, 0.45))):             # steps
        b.box((1.6, -D / 2 - dep / 2 - 0.02, z), (1.1, dep, 0.12), "wood" if k else "concrete")
    b.box((-1.0, -D / 2 - 0.01, z0 + 1.45), (1.2, 0.03, 0.8), "window_lit")   # window, lit
    b.box((-1.0, -D / 2 - 0.03, z0 + 1.45), (1.3, 0.04, 0.08), "grey_dark")
    for k in range(4):
        b.box((-1.45 + k * 0.3, -D / 2 - 0.04, z0 + 1.45), (0.03, 0.03, 0.8), "grey_dark")     # bars
    b.box((-1.0, -D / 2 - 0.04, z0 + 1.02), (1.3, 0.05, 0.06), "grey_dark")
    b.box((-1.0, -D / 2 - 0.04, z0 + 1.88), (1.3, 0.05, 0.06), "grey_dark")
    b.box((0.35, -D / 2 - 0.03, z0 + 1.75), (0.9, 0.02, 0.36), "white")
    b.decal("sign_no_entry", Matrix.Translation((0.35, -D / 2 - 0.04, z0 + 1.75)) @ Matrix.Rotation(math.pi / 2, 4, "X"),
            "red", height=0.3, lift=0.002)
    for _ in range(7):                                                   # rust streaks under the roof
        x = rnd.uniform(-2.3, 2.3)
        sy = rnd.choice((-1, 1))
        b.box((x, sy * (D / 2 + 0.012), z0 + H - 0.35), (rnd.uniform(0.05, 0.12), 0.01, rnd.uniform(0.3, 0.7)), "rust")
    b.tube_path([(-2.4, 0.9, z0 + H + 0.1), (-2.7, 0.9, z0 + H - 0.2), (-2.75, 0.9, 1.0), (-3.3, 0.9, 0.03),
                 (-4.5, 1.2, 0.03)], 0.02, "black", segs=5)
    return L.mesh_root(b, name, col, loc)


def _diamonds(b, x0, x1, z0, z1, y, cols, rows, skip=None):
    """Raised rhombus relief of the ПО-2 fence panel on the face at y (front = -Y)."""
    for i in range(cols):
        for j in range(rows):
            cx = x0 + (x1 - x0) * (i + 0.5) / cols
            cz = z0 + (z1 - z0) * (j + 0.5) / rows
            if skip and skip(cx, cz):
                continue
            w, h = (x1 - x0) / cols * 0.42, (z1 - z0) / rows * 0.42
            pts = [(cx - w, cz), (cx, cz - h), (cx + w, cz), (cx, cz + h)]
            b.prism(ccw([(p[0], p[1]) for p in pts]), 0.025, "concrete_light",
                    matrix=Matrix.Translation((0, y, 0)) @ Matrix.Rotation(math.pi / 2, 4, "X"))


def fence_panel(col, loc, name="Site_Fence", broken=False, seed=80):
    """Soviet concrete fence panel ПО-2 (4 x 2.4 m) with the diamond relief on the front, a post at the
    +X end. broken=True: a hole kids crawl through, with broken chunks on the ground."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    W, T, H = 4.0, 0.14, 2.4
    b.box((W / 2 + 0.15, 0, 1.3), (0.3, 0.3, 2.6), concrete)
    if not broken:
        b.box((0, 0, H / 2), (W, T, H), concrete)
        _diamonds(b, -W / 2 + 0.1, W / 2 - 0.1, 0.15, H - 0.15, -T / 2, 10, 6)
    else:
        hx0, hx1, hz1 = -0.7, 0.5, 1.2                                    # the hole
        b.box((0, 0, (hz1 + H) / 2), (W, T, H - hz1), concrete)
        b.box(((-W / 2 + hx0) / 2, 0, hz1 / 2), (hx0 + W / 2, T, hz1), concrete)
        b.box(((hx1 + W / 2) / 2, 0, hz1 / 2), (W / 2 - hx1, T, hz1), concrete)
        for k in range(5):                                               # jagged edge of the hole
            x = hx0 + (hx1 - hx0) * (k + 0.5) / 5
            b.box((x, 0, hz1 - rnd.uniform(0.03, 0.12)), (0.25, T, 0.2), "concrete", rot=(0, rnd.uniform(-0.5, 0.5), 0))
        for sx in (hx0, hx1):
            b.box((sx, 0, hz1 * 0.55), (0.12, T, hz1 * 0.9), "concrete", rot=(0, rnd.uniform(-0.2, 0.2), 0))
        _diamonds(b, -W / 2 + 0.1, W / 2 - 0.1, 0.15, H - 0.15, -T / 2, 10, 6,
                  skip=lambda x, z: hx0 - 0.15 < x < hx1 + 0.15 and z < hz1 + 0.15)
        for x, y in ((-0.4, -0.5), (0.2, -0.7), (0.0, 0.5)):              # chunks on the ground
            b.box((x, y, 0.06), (0.3, 0.2, 0.12), "concrete", rot=(0.2, 0.3, rnd.uniform(0, 3)))
        for k in range(3):                                               # rebar in the hole
            x = hx0 + 0.2 + k * 0.4
            b.tube_path([(x, 0, hz1 - 0.05), (x + 0.05, 0.05, hz1 - 0.35), (x + 0.15, 0.15, hz1 - 0.5)], 0.01, "rust", segs=4)
    return L.mesh_root(b, name, col, loc)


# ================================================================ unfinished frame and crane
def building_frame(col, loc, name="Site_Frame", seed=81):
    """Unfinished three-storey concrete frame 12 x 6 m: columns, beams, some floor slabs, a brick wall
    begun on the ground floor, rebar sticking out on top. Background piece."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    xs, ys = (-6, -2, 2, 6), (-3, 0, 3)
    for x in xs:
        for y in ys:
            top = 9.0 if (x, y) not in ((6, 3), (2, 3), (6, 0)) else 6.4
            b.box((x, y, top / 2), (0.4, 0.4, top), concrete)
            for k in range(3 if top > 7 else 2):
                b.tube_path([(x + rnd.uniform(-0.12, 0.12), y + rnd.uniform(-0.12, 0.12), top),
                             (x + rnd.uniform(-0.2, 0.2), y + rnd.uniform(-0.2, 0.2), top + rnd.uniform(0.4, 0.9))],
                            0.012, "rust", segs=4)
    for z in (3.0, 6.0):
        for y in ys:
            b.box((0, y, z - 0.2), (12.4, 0.35, 0.4), "concrete")
        for x in xs:
            b.box((x, 0, z - 0.2), (0.35, 6.4, 0.4), "concrete")
    for x0, z in ((-4, 3.0), (0, 3.0), (-4, 6.0)):                        # floor slabs laid so far
        _slab(b, L.frame((x0, -1.5, z + 0.11), (0, 0, 0)), length=4.0, width=3.0, thick=0.22)
        _slab(b, L.frame((x0, 1.5, z + 0.11), (0, 0, 0)), length=4.0, width=3.0, thick=0.22)
    for k in range(7):                                                   # brick wall begun, stepped
        b.box((-4.5, -3.0, 0.15 + k * 0.3), ((9 - k) * 0.5, 0.25, 0.28), "rust_light")
    b.box((4.0, -3.0, 1.5), (0.06, 0.06, 3.0), "wood")
    return L.mesh_root(b, name, col, loc)


def _lattice(b, p0, p1, width, height, color, step=1.2, up=(0, 0, 1)):
    """Triangular-ish lattice girder between two points: 4 chords with zig-zag braces on the sides."""
    p0, p1 = Vector(p0), Vector(p1)
    x = (p1 - p0).normalized()
    u = Vector(up).normalized()
    s = u.cross(x).normalized()
    u = x.cross(s).normalized()
    L_ = (p1 - p0).length
    corners = [s * (width / 2) + u * (height / 2), -s * (width / 2) + u * (height / 2),
               -s * (width / 2) - u * (height / 2), s * (width / 2) - u * (height / 2)]
    for c in corners:
        b.beam(p0 + c, p1 + c, 0.08, 0.08, color, up=up)
    n = max(1, int(L_ / step))
    for k in range(n):
        a = p0 + x * (L_ * k / n)
        c_ = p0 + x * (L_ * (k + 1) / n)
        for i in range(4):
            j = (i + 1) % 4
            if k % 2 == 0:
                b.beam(a + corners[i], c_ + corners[j], 0.05, 0.05, color, up=up)
            else:
                b.beam(a + corners[j], c_ + corners[i], 0.05, 0.05, color, up=up)


def tower_crane(col, loc, name="Site_Crane"):
    """Tower crane ~22 m: lattice mast on a concrete foot, cab, jib to -X with a trolley and a hook,
    counter-jib with the counterweight to +X, tie bars from the peak. Background piece."""
    L.remove_tree(name)
    b = Builder()
    b.box((0, 0, 0.3), (4.0, 4.0, 0.6), concrete)
    H = 18.0
    _lattice(b, (0, 0, 0.6), (0, 0, H), 1.6, 1.6, "yellow", step=1.5, up=(1, 0, 0))
    b.box((0, 0, H + 0.3), (2.0, 2.0, 0.6), "yellow")
    b.box((0.2, -1.4, H + 1.4), (1.6, 1.4, 1.8), "orange")                           # cab
    b.box((0.2, -2.11, H + 1.6), (1.4, 0.02, 1.0), "window")
    _lattice(b, (-0.8, 0, H + 1.1), (-17.0, 0, H + 1.1), 1.0, 1.1, "yellow", step=1.6, up=(0, 0, 1))
    _lattice(b, (0.8, 0, H + 1.1), (7.0, 0, H + 1.1), 1.2, 0.9, "yellow", step=1.6, up=(0, 0, 1))
    b.box((6.2, 0, H + 0.4), (1.6, 1.6, 1.6), "concrete")                            # counterweight
    peak = Vector((0, 0, H + 5.0))
    for sx in (-1, 1):
        b.beam((sx * 0.6, -0.5, H + 0.6), peak, 0.12, 0.12, "yellow")
        b.beam((sx * 0.6, 0.5, H + 0.6), peak, 0.12, 0.12, "yellow")
    b.beam(peak, (-12.0, 0, H + 1.65), 0.04, 0.04, "grey_dark")
    b.beam(peak, (6.8, 0, H + 1.55), 0.04, 0.04, "grey_dark")
    b.box((-9.0, 0, H + 0.4), (0.8, 0.8, 0.4), "grey_dark")                            # trolley
    b.beam((-9.0, 0, H + 0.2), (-9.0, 0, 7.5), 0.03, 0.03, "grey_dark")
    b.box((-9.0, 0, 7.3), (0.5, 0.35, 0.5), "red")                                    # hook block
    b.tube_path([(-9.0, 0, 7.05), (-9.0, 0, 6.8), (-8.85, 0, 6.65), (-8.75, 0, 6.8)], 0.04, "grey_dark", segs=5)
    return L.mesh_root(b, name, col, loc)


# ================================================================ lights
def work_light(col, loc, name="Site_WorkLight"):
    """Floodlight on a tripod stand, aimed down to -Y; a black cable on the ground."""
    L.remove_tree(name)
    b = Builder()
    top = Vector((0, 0, 1.6))
    for k in range(3):
        a = k * 2 * math.pi / 3 + math.pi / 2
        b.beam((0.55 * math.cos(a), 0.55 * math.sin(a), 0.0), top - Vector((0, 0, 0.6)), 0.04, 0.04, "yellow")
    b.cyl((0, 0, 0.9), top, 0.03, 0.03, segs=6, color="grey")
    m = L.frame((0, -0.05, 1.75), (0.55, 0, 0))
    b.box_m(m, (0.5, 0.25, 0.38), "yellow")
    b.box_m(m @ Matrix.Translation((0, -0.13, 0)), (0.42, 0.02, 0.3), "lamp")
    b.tube_path([(0, 0.05, 1.6), (0.1, 0.2, 0.9), (0.3, 0.4, 0.02), (1.4, 0.8, 0.02)], 0.015, "black", segs=4)
    return L.mesh_root(b, name, col, loc)


def wood_lamp_post(col, loc, name="Site_LampPost"):
    """Wooden pole on a concrete stake (приставка) with a lamp on a bent arm and insulators on the crossbar.
    The lamp faces -Y."""
    L.remove_tree(name)
    b = Builder()
    b.box((0.0, 0.14, 0.9), (0.2, 0.14, 1.8), concrete)
    for z in (0.55, 1.45):
        b.cyl((-0.14, 0.07, z), (0.14, 0.07, z), 0.02, 0.02, segs=5, color="grey_dark")
    b.cyl((0, 0, 0.2), (0, 0, 7.4), 0.12, 0.1, segs=8, color="wood_dark")
    b.box((0, 0, 7.0), (1.4, 0.1, 0.1), "wood_dark")
    for x in (-0.6, -0.2, 0.2, 0.6):
        b.cyl((x, 0, 7.05), (x, 0, 7.2), 0.03, 0.04, segs=6, color="white")
    b.tube_path([(0, -0.08, 6.4), (0, -0.4, 6.6), (0, -0.9, 6.6)], 0.03, "grey_dark", segs=5)
    b.cyl((0, -1.0, 6.62), (0, -1.0, 6.42), 0.1, 0.24, segs=10, color=lambda f: "lamp" if f.normal.z < -0.9 else "grey_dark")
    return L.mesh_root(b, name, col, loc)


# ================================================================ ground
def site_ground(col, loc, name="Env_SiteGround", seed=82):
    """Churned-up ground of the site in arena coordinates: dirt, gravel and concrete pads, tyre tracks,
    puddles. Visual only."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    base = b.grid(110, 90, 44, 36, "brown", matrix=L.frame((0, 8, -0.02)))
    b.paint([f for f in base if rnd.random() < 0.25], "wood")
    b.paint([f for f in base if rnd.random() < 0.08], "sand_dark")
    b.paint([f for f in base if rnd.random() < 0.05], "olive")
    arena = b.grid(SITE_W + 2, SITE_D + 2, int(SITE_W / 2), int(SITE_D / 2), "wood", matrix=L.frame((0, 0, 0)))
    b.paint([f for f in arena if rnd.random() < 0.3], "brown")
    b.paint([f for f in arena if rnd.random() < 0.12], "grey")
    for cx, cy, w, d in ((-12, 6, 8, 6), (10, -7, 6, 5)):                 # concrete pads
        pad = b.grid(w, d, int(w), int(d), "concrete", matrix=L.frame((cx, cy, 0.01)))
        b.paint([f for f in pad if rnd.random() < 0.2], "concrete_light")
        b.paint([f for f in pad if rnd.random() < 0.08], "grey")
    for x0 in (-3.0, -1.2):                                              # tyre tracks
        pts = [(x0 + 2.5 * math.sin(t * 0.25), -18 + t * 2.2) for t in range(18)]
        for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
            d = Vector((xb - xa, yb - ya))
            b.grid(d.length + 0.05, 0.45, 1, 1, "wood_dark",
                   matrix=L.frame(((xa + xb) / 2, (ya + yb) / 2, 0.012), (0, 0, math.atan2(d.y, d.x))))
    for _ in range(7):                                                   # puddles
        cx, cy = rnd.uniform(-18, 18), rnd.uniform(-12, 12)
        pts = [(cx + r * math.cos(a), cy + r * 0.7 * math.sin(a)) for a, r in
               [(2 * math.pi * k / 10, rnd.uniform(0.8, 1.6)) for k in range(10)]]
        b.prism(ccw(pts), 0.002, "steel", matrix=Matrix.Translation((0, 0, 0.012)))
    for v in b.bm.verts:
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return L.mesh_root(b, name, col, loc)


def build_all(col_parent):
    col = L.collection("Site", col_parent)
    X, Y = -200.0, 0.0
    out = [site_ground(col, (X, Y, 0))]
    props = [
        (slab_stack, "Site_SlabStack"), (slab_tilted, "Site_SlabTilted"), (column_rebar, "Site_ColumnRebar"),
        (rebar_bundle, "Site_RebarBundle"), (sand_pile, "Site_SandPile"), (gravel_pile, "Site_GravelPile"),
        (brick_pallet, "Site_BrickPallet"), (pallet, "Site_Pallet"), (barrel, "Site_Barrel"),
        (fire_barrel, "Site_FireBarrel"), (cable_reel, "Site_CableReel"), (work_light, "Site_WorkLight"),
        (wood_lamp_post, "Site_LampPost"), (cabin, "Site_Cabin"),
    ]
    for k, (fn, nm) in enumerate(props):
        out.append(fn(col, (X - 35 + (k % 5) * 6.0, Y - 30 - (k // 5) * 6.0, 0), name=nm))
    out.append(well_ring(col, (X - 35, Y - 50, 0), name="Site_Ring"))
    out.append(well_ring(col, (X - 31, Y - 50, 0), name="Site_Ring_Lying", lying=True))
    out.append(rings_stack(col, (X - 27, Y - 50, 0)))
    out.append(fence_panel(col, (X - 20, Y - 50, 0)))
    out.append(fence_panel(col, (X - 14, Y - 50, 0), name="Site_Fence_Broken", broken=True))
    out.append(building_frame(col, (X + 40, Y - 30, 0)))
    out.append(tower_crane(col, (X + 40, Y - 60, 0)))
    return out

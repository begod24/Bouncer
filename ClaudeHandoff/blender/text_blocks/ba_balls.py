"""Balls. All normalized to diameter 1 m (like Unity's primitive sphere),
so they drop into the existing Ball prefab 'Mesh' child (scale = 2 * radius)."""
import math, random
import bmesh
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

R = 0.5

CUBE_FACES = [
    (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))),
    (Vector((-1, 0, 0)), Vector((0, -1, 0)), Vector((0, 0, 1))),
    (Vector((0, 1, 0)), Vector((-1, 0, 0)), Vector((0, 0, 1))),
    (Vector((0, -1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))),
    (Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0))),
    (Vector((0, 0, -1)), Vector((1, 0, 0)), Vector((0, -1, 0))),
]


def cube_sphere(b, coords, color_fn, radius=R):
    """Spherified cube; coords = grid positions in [-1, 1] per face (may be non-uniform).
    color_fn(face_idx, i, j, cube_point) -> palette name."""
    new_verts = []
    n = len(coords)
    for fi, (nrm, u, v) in enumerate(CUBE_FACES):
        grid = []
        for j in range(n):
            row = []
            for i in range(n):
                a = math.tan(coords[i] * math.pi / 4)
                c = math.tan(coords[j] * math.pi / 4)
                p = (nrm + u * a + v * c).normalized() * radius
                vert = b.bm.verts.new(p)
                new_verts.append(vert)
                row.append(vert)
            grid.append(row)
        for j in range(n - 1):
            for i in range(n - 1):
                f = b.bm.faces.new([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]])
                cm = (coords[i] + coords[i + 1]) / 2
                cn = (coords[j] + coords[j + 1]) / 2
                b.paint([f], color_fn(fi, i, j, nrm + u * cm + v * cn))
    bmesh.ops.remove_doubles(b.bm, verts=new_verts, dist=1e-5)


def uv_ball(b, color_fn, segs=16, rings=11, radius=R):
    prof = []
    for i in range(rings + 1):
        a = -math.pi / 2 + math.pi * i / rings
        prof.append((0.0 if i in (0, rings) else radius * math.cos(a), radius * math.sin(a)))
    lat = Lathe(prof, segs)
    faces = b.lathe(lat, "red")
    b.paint(faces, lambda f: color_fn(f.calc_center_median()))
    return lat


# ---------------------------------------------------------------- the five balls
def rubber(col, loc):
    L.remove_tree("Ball_Rubber")
    b = Builder()
    uv_ball(b, lambda c: "white" if abs(c.z) < 0.06 else "red")
    return L.mesh_root(b, "Ball_Rubber", col, loc)


def volleyball(col, loc):
    L.remove_tree("Ball_Volleyball")
    b = Builder()
    strip_colors = ["blue", "white", "yellow"]

    def color(fi, i, j, p):
        axis = {0: 2, 1: 2, 2: 0, 3: 0, 4: 1, 5: 1}[fi]   # strips on opposite panels are parallel,
        c = p[axis]                                        # neighbouring panels perpendicular
        k = min(2, max(0, int((c + 1) / 2 * 3)))
        return strip_colors[k]

    cube_sphere(b, [-1 + 2 * k / 6 for k in range(7)], color)
    return L.mesh_root(b, "Ball_Volleyball", col, loc)


def medicine(col, loc):
    L.remove_tree("Ball_Medicine")
    b = Builder()
    coords = [-1, -0.9, -0.45, 0, 0.45, 0.9, 1]
    last = len(coords) - 2

    def color(fi, i, j, p):
        return "leather_dark" if i in (0, last) or j in (0, last) else "leather"

    cube_sphere(b, coords, color)
    # stitched valve / label patch
    b.paint(b.box((0, -0.49, 0.0), (0.12, 0.04, 0.12), "wood_light"), "wood_light")
    return L.mesh_root(b, "Ball_Medicine", col, loc)


def tennis(col, loc):
    L.remove_tree("Ball_Tennis")
    b = Builder()
    cube_sphere(b, [-1 + 2 * k / 6 for k in range(7)], lambda *a: "lime")
    # the seam is its own closed strip floating just above the fuzz
    a_, b_ = 0.7, 0.3
    n = 72
    pts = []
    for k in range(n):
        t = 2 * math.pi * k / n
        pts.append(Vector((a_ * math.cos(t) + b_ * math.cos(3 * t),
                           a_ * math.sin(t) - b_ * math.sin(3 * t),
                           2 * math.sqrt(a_ * b_) * math.sin(2 * t))).normalized())
    left, right = [], []
    w = 0.05
    for k in range(n):
        p, q = pts[k], pts[(k + 1) % n]
        side = p.cross(q - pts[k - 1]).normalized()
        left.append(b.bm.verts.new((p + side * w / R / 2).normalized() * (R + 0.006)))
        right.append(b.bm.verts.new((p - side * w / R / 2).normalized() * (R + 0.006)))
    faces = []
    for k in range(n):
        j = (k + 1) % n
        f = b.bm.faces.new([right[k], right[j], left[j], left[k]])
        f.normal_update()
        if f.normal.dot(pts[k]) < 0:
            f.normal_flip()
        faces.append(f)
    b.paint(faces, "white")
    return L.mesh_root(b, "Ball_Tennis", col, loc)


def deflated(col, loc):
    L.remove_tree("Ball_Deflated")
    b = Builder()
    uv_ball(b, lambda c: "cream" if abs(c.z) < 0.06 else "red_light")
    rnd = random.Random(5)
    dent = Vector((0.1, -0.08))
    for v in b.bm.verts:
        p = v.co
        p.x *= 1.12
        p.y *= 1.12
        p.z *= 0.74
        if p.z > -0.05:
            d = (Vector((p.x, p.y)) - dent).length / 0.34
            if d < 1:
                p.z -= 0.2 * (1 - d * d) * (0.6 + 0.4 * (p.z + 0.05) / 0.42)
        p += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * 0.012
    return L.mesh_root(b, "Ball_Deflated", col, loc, pivot=(0, 0, 0))


def build_all(col_parent):
    col = L.collection("Balls", col_parent)
    y = -3.0
    return [
        rubber(col, (0.0, y, 0.5)),
        volleyball(col, (1.3, y, 0.5)),
        medicine(col, (2.6, y, 0.5)),
        tennis(col, (3.9, y, 0.5)),
        deflated(col, (5.2, y, 0.5)),
    ]

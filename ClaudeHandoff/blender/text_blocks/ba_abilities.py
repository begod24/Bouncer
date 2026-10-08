"""Карточки 2026-10: три новых мяча (в коллекцию Balls) и предметы умений (коллекция AbilityProps).
Мячи — диаметр 1 м, как остальные. Предметы — в реальном размере, начало координат у основания/рукоятки."""
import math
import bmesh
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe
import ba_balls as B


# ---------------------------------------------------------------- мячи
def seam(b, pts, width, color, lift=0.006):
    """Замкнутая полоска на поверхности мяча по точкам pts (единичные векторы), как шов теннисного."""
    n = len(pts)
    left, right = [], []
    for k in range(n):
        p, q, o = pts[k], pts[(k + 1) % n], pts[k - 1]
        side = p.cross(q - o).normalized()
        left.append(b.bm.verts.new((p + side * width / B.R / 2).normalized() * (B.R + lift)))
        right.append(b.bm.verts.new((p - side * width / B.R / 2).normalized() * (B.R + lift)))
    faces = []
    for k in range(n):
        j = (k + 1) % n
        f = b.bm.faces.new([right[k], right[j], left[j], left[k]])
        f.normal_update()
        if f.normal.dot(pts[k]) < 0:
            f.normal_flip()
        faces.append(f)
    b.paint(faces, color)


def basketball(col, loc):
    L.remove_tree("Ball_Basketball")
    b = Builder()
    B.uv_ball(b, lambda c: "orange", segs=20, rings=12)
    n = 64
    ring = [2 * math.pi * k / n for k in range(n)]
    seam(b, [Vector((math.cos(t), math.sin(t), 0)) for t in ring], 0.035, "black")
    seam(b, [Vector((0, math.cos(t), math.sin(t))) for t in ring], 0.035, "black")
    for side in (-1, 1):
        seam(b, [Vector((side * 0.62, 0.785 * math.cos(t), 0.785 * math.sin(t))).normalized() for t in ring], 0.035, "black")
    return L.mesh_root(b, "Ball_Basketball", col, loc)


def pingpong(col, loc):
    L.remove_tree("Ball_PingPong")
    b = Builder()
    B.uv_ball(b, lambda c: "orange_light" if abs(Vector(c).z) < 0.035 else "white", segs=14, rings=9)
    return L.mesh_root(b, "Ball_PingPong", col, loc)


def _icosahedron():
    phi = (1 + math.sqrt(5)) / 2
    verts = []
    for a in (-1, 1):
        for b_ in (-phi, phi):
            verts += [Vector((0, a, b_)), Vector((a, b_, 0)), Vector((b_, 0, a))]
    edges = [(i, j) for i in range(12) for j in range(i + 1, 12) if abs((verts[i] - verts[j]).length - 2) < 1e-4]
    adj = {i: set() for i in range(12)}
    for i, j in edges:
        adj[i].add(j)
        adj[j].add(i)
    faces = [(i, j, k) for i in range(12) for j in adj[i] if j > i for k in adj[i] & adj[j] if k > j]
    return verts, adj, faces


def _ordered(points, normal):
    """Точки многоугольника по кругу вокруг normal."""
    c = sum(points, Vector()) / len(points)
    u = (points[0] - c).normalized()
    v = normal.cross(u)
    return sorted(points, key=lambda p: math.atan2((p - c).dot(v), (p - c).dot(u)))


def _panel(b, corners, inner_color, seam_color, seam=0.09, split=3):
    """Выпуклая сферическая панель: середина — inner_color, у краёв полоска шва seam_color."""
    n = len(corners)
    c = (sum(corners, Vector()) / n).normalized()
    outer, inner = [], []
    for k in range(n):
        a, d = corners[k], corners[(k + 1) % n]
        for s_ in range(split):
            p = a.lerp(d, s_ / split)
            outer.append(p.normalized() * B.R)
            inner.append(c.lerp(p, 1 - seam).normalized() * B.R)
    center = b.bm.verts.new(c * B.R)
    vo = [b.bm.verts.new(p) for p in outer]
    vi = [b.bm.verts.new(p) for p in inner]
    m = len(vo)
    fan, band = [], []
    for k in range(m):
        j = (k + 1) % m
        fan.append(b.bm.faces.new([center, vi[k], vi[j]]))
        band.append(b.bm.faces.new([vi[k], vo[k], vo[j], vi[j]]))
    for f in fan + band:
        f.normal_update()
        if f.normal.dot(c) < 0:
            f.normal_flip()
    b.paint(fan, inner_color)
    b.paint(band, seam_color)


def football(col, loc):
    """Классический мяч «Телстар»: усечённый икосаэдр, 12 чёрных пятиугольников, 20 белых шестиугольников, швы."""
    L.remove_tree("Ball_Football")
    b = Builder()
    verts, adj, faces = _icosahedron()
    third = lambda i, j: verts[i].lerp(verts[j], 1 / 3)
    for i in range(12):
        ring = [third(i, j) for j in adj[i]]
        _panel(b, _ordered(ring, verts[i].normalized()), "black", "grey_dark")
    for i, j, k in faces:
        ring = [third(i, j), third(j, i), third(j, k), third(k, j), third(k, i), third(i, k)]
        nrm = (verts[i] + verts[j] + verts[k]).normalized()
        _panel(b, _ordered(ring, nrm), "white", "grey_light")
    bmesh.ops.remove_doubles(b.bm, verts=b.bm.verts, dist=1e-5)
    return L.mesh_root(b, "Ball_Football", col, loc)


# ---------------------------------------------------------------- предметы умений
def fence_plank(col, loc):
    """Штакетина: доска с острым верхом, низ на нуле."""
    L.remove_tree("Prop_FencePlank")
    b = Builder()
    w, t, h = 0.16, 0.045, 1.05
    pts = [(-w / 2, 0.0), (w / 2, 0.0), (w / 2, h), (0.0, h + 0.13), (-w / 2, h)]
    m = Matrix.Translation((0, t / 2, 0)) @ Matrix.Rotation(math.radians(90), 4, "X")
    b.prism(pts, t, "wood_light", m)
    return L.mesh_root(b, "Prop_FencePlank", col, loc)


def fence_rail(col, loc):
    """Поперечная перекладина забора, 3.2 м, по центру."""
    L.remove_tree("Prop_FenceRail")
    b = Builder()
    b.box((0, 0.045, 0), (3.2, 0.04, 0.09), "wood")
    return L.mesh_root(b, "Prop_FenceRail", col, loc)


def cap_gun(col, loc):
    """Пугач с пистонами: ствол вперёд (-Y), рукоять вниз, начало — у рукояти."""
    L.remove_tree("Prop_CapGun")
    b = Builder()
    b.box((0, -0.08, 0.06), (0.04, 0.2, 0.05), "grey_dark")          # ствол
    b.cyl((0, -0.18, 0.06), (0, -0.2, 0.06), 0.016, segs=8, color="black")
    b.cyl((0, -0.03, 0.06), (0, 0.01, 0.06), 0.032, segs=8, color="tin")   # барабан
    b.box((0, 0.03, -0.02), (0.035, 0.05, 0.12), "red", rot=(math.radians(-15), 0, 0))   # рукоять
    b.box((0, -0.03, 0.0), (0.012, 0.04, 0.03), "black")             # спуск
    b.box((0, 0.02, 0.095), (0.012, 0.03, 0.02), "grey")             # курок
    return L.mesh_root(b, "Prop_CapGun", col, loc)


def camera_smena(col, loc):
    """Фотоаппарат «Смена»: корпус, объектив вперёд (-Y), вспышка сверху."""
    L.remove_tree("Prop_CameraSmena")
    b = Builder()
    b.box((0, 0, 0), (0.2, 0.07, 0.12), "black")
    b.box((0, -0.036, 0.02), (0.19, 0.005, 0.06), "grey")            # серебристая полоса
    b.cyl((0, -0.035, -0.005), (0, -0.09, -0.005), 0.042, 0.038, segs=12, color="grey_dark")
    b.cyl((0, -0.09, -0.005), (0, -0.093, -0.005), 0.03, segs=12, color="blue_light")   # стекло
    b.box((0.06, 0, 0.075), (0.05, 0.05, 0.035), "tin")              # вспышка
    b.box((0.06, -0.026, 0.075), (0.042, 0.004, 0.026), "lamp")
    b.cyl((-0.07, 0, 0.06), (-0.07, 0, 0.075), 0.012, segs=8, color="tin")   # кнопка
    return L.mesh_root(b, "Prop_CameraSmena", col, loc)


def magnet(col, loc):
    """Подковообразный магнит: дуга вверх, концы вниз (серые)."""
    L.remove_tree("Prop_Magnet")
    b = Builder()
    r, th = 0.11, 0.055
    n = 12
    for i in range(n):
        a0 = math.pi * i / n
        a1 = math.pi * (i + 1) / n
        p0 = Vector((math.cos(a0) * r, 0, math.sin(a0) * r + 0.12))
        p1 = Vector((math.cos(a1) * r, 0, math.sin(a1) * r + 0.12))
        b.beam(p0, p1, th, th, "red", up=(0, 1, 0))
    for x in (-r, r):
        b.box((x, 0, 0.07), (th, th, 0.1), "red")
        b.box((x, 0, 0.0), (th * 1.02, th * 1.02, 0.045), "tin")
    return L.mesh_root(b, "Prop_Magnet", col, loc)


def cassette(col, loc):
    """Аудиокассета стоя (лицом вперёд -Y), начало в центре."""
    L.remove_tree("Prop_Cassette")
    b = Builder()
    b.box((0, 0, 0), (0.2, 0.025, 0.125), "black")
    b.box((0, -0.0135, 0.02), (0.17, 0.003, 0.06), "cream")          # наклейка
    b.box((0, -0.0135, 0.02), (0.17, 0.0032, 0.012), "orange")
    for x in (-0.045, 0.045):
        b.cyl((x, -0.012, -0.03), (x, -0.016, -0.03), 0.022, segs=10, color="wood")
        b.cyl((x, -0.0165, -0.03), (x, -0.017, -0.03), 0.008, segs=6, color="white")
    b.box((0, -0.013, -0.052), (0.1, 0.003, 0.012), "grey_dark")
    return L.mesh_root(b, "Prop_Cassette", col, loc)


def walkie(col, loc):
    """Рация: корпус стоя, антенна вверх, решётка динамика спереди (-Y)."""
    L.remove_tree("Prop_Walkie")
    b = Builder()
    b.box((0, 0, 0.09), (0.075, 0.04, 0.18), "green_dark")
    b.box((0, -0.0205, 0.13), (0.055, 0.003, 0.06), "black")
    for z in (0.115, 0.13, 0.145):
        b.box((0, -0.0225, z), (0.05, 0.002, 0.005), "grey")
    b.box((0, -0.0205, 0.06), (0.04, 0.003, 0.02), "orange")         # кнопка
    b.cyl((0.025, 0, 0.18), (0.025, 0, 0.33), 0.007, 0.004, segs=6, color="black")
    b.sphere((0.025, 0, 0.335), 0.01, segs=6, rings=4, color="red")
    return L.mesh_root(b, "Prop_Walkie", col, loc)


def tamagotchi(col, loc):
    """Тамагочи: яйцо с ремешком-колечком, вырез под экран спереди (-Y). Экран — отдельный квадрат в Unity."""
    L.remove_tree("Prop_Tamagotchi")
    b = Builder()
    prof = []
    for i in range(11):
        a = -math.pi / 2 + math.pi * i / 10
        rad = 0.07 * math.cos(a) * (1.0 - 0.18 * math.sin(a))
        prof.append((0.0 if i in (0, 10) else rad, 0.085 + 0.085 * math.sin(a)))
    lat = Lathe(prof, 14, Matrix.Scale(0.55, 4, Vector((0, 1, 0))))
    b.lathe(lat, "pink")
    b.box((0, -0.037, 0.095), (0.075, 0.004, 0.06), "grey_dark")       # рамка экрана
    for x in (-0.025, 0.0, 0.025):
        b.cyl((x, -0.03, 0.04), (x, -0.042, 0.04), 0.008, segs=6, color="yellow")
    b.cyl((0, 0, 0.175), (0, 0, 0.19), 0.012, segs=6, color="tin")
    return L.mesh_root(b, "Prop_Tamagotchi", col, loc)


def build_all(col_parent):
    balls = L.collection("Balls", col_parent)
    props = L.collection("AbilityProps", col_parent)
    y = -5.0
    return [
        basketball(balls, (0.0, y, 0.5)),
        pingpong(balls, (1.3, y, 0.5)),
        football(balls, (2.6, y, 0.5)),
        fence_plank(props, (0.0, -7.0, 0.0)),
        fence_rail(props, (2.5, -7.0, 0.0)),
        cap_gun(props, (5.0, -7.0, 0.2)),
        camera_smena(props, (5.6, -7.0, 0.2)),
        magnet(props, (6.2, -7.0, 0.0)),
        cassette(props, (6.8, -7.0, 0.1)),
        walkie(props, (7.4, -7.0, 0.0)),
        tamagotchi(props, (8.0, -7.0, 0.0)),
    ]


NAMES = ["Ball_Basketball", "Ball_PingPong", "Ball_Football", "Prop_FencePlank", "Prop_FenceRail", "Prop_CapGun",
         "Prop_CameraSmena", "Prop_Magnet", "Prop_Cassette", "Prop_Walkie", "Prop_Tamagotchi"]

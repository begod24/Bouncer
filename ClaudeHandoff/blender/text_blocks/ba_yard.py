"""Courtyard ('Двор') props. Origins at ground level, footprint centre; fronts face -Y."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

FRONT = -math.pi / 2


# ================================================================ sandbox with mushroom
def sandbox(col, loc, name="Prop_Sandbox"):
    L.remove_tree(name)
    rnd = random.Random(2)
    b = Builder()
    half, t = 1.5, 0.06
    # walls: two stacked boards per side
    for k in range(4):
        rot = k * math.pi / 2
        m = Matrix.Rotation(rot, 4, "Z")
        for z0, z1 in ((0.0, 0.14), (0.155, 0.29)):
            c = m @ Vector((0, -half + t / 2, (z0 + z1) / 2))
            b.box(c, (2 * half - 2 * t, t, z1 - z0), "blue", rot=(0, 0, rot))
        # seat rim on top
        c = m @ Vector((0, -half + 0.11, 0.31))
        b.box(c, (2 * half + 0.04, 0.24, 0.04), "yellow", rot=(0, 0, rot))
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * (half - 0.05), sy * (half - 0.05), 0.15), (0.1, 0.1, 0.3), "wood_dark")
    # sand with a little mound and a dug hole
    def h(x, y):
        z = 0.2 + 0.07 * math.exp(-((x + 0.6) ** 2 + (y - 0.5) ** 2) / 0.35)
        z -= 0.05 * math.exp(-((x - 0.55) ** 2 + (y + 0.45) ** 2) / 0.08)
        return z + rnd.uniform(-0.012, 0.012)
    sand = b.grid(2 * half - 2 * t, 2 * half - 2 * t, 8, 8, "sand", height_fn=h)
    b.paint([f for f in sand if rnd.random() < 0.1], "sand_dark")
    # mushroom
    b.cyl((0, 0, 0.15), (0, 0, 2.12), 0.075, 0.065, segs=8, color="white")
    b.cyl((0, 0, 0.15), (0, 0, 0.55), 0.085, 0.085, segs=8, color="green")
    cap = Lathe([(0.0, 2.08), (1.18, 1.97), (1.3, 1.99), (1.26, 2.07), (0.82, 2.34), (0.36, 2.52),
                 (0.0, 2.56)], 12)
    b.paint(b.lathe(cap, "red"), lambda f: "cream" if f.normal.z < -0.2 else "red")
    for z, n, off in ((2.12, 9, 0.0), (2.37, 6, 0.5), (2.5, 1, 0.0)):
        s = cap.s_at_z(z, from_top=True)
        for k in range(n):
            phi = 2 * math.pi * (k + off) / n
            if n == 1:
                s = cap.s_at_z(2.535, from_top=True)
            b.decal_disc(cap, phi, s, 0.11 if z < 2.3 else 0.09, None, "white", segs=8, off=0.008)
    # toy bucket & spade left in the sand
    bk = Lathe([(0.0, 0.0), (0.075, 0.0), (0.1, 0.16), (0.09, 0.16), (0.066, 0.02), (0.0, 0.02)], 8,
               L.frame((0.75, -0.8, 0.2), (0.25, 0.1, 0)))
    b.paint(b.lathe(bk, "red"), lambda f: "yellow" if f.calc_center_median().z > 0.33 else "red")
    b.cyl((0.2, -0.95, 0.24), (0.52, -0.72, 0.36), 0.015, 0.015, segs=5, color="yellow")
    b.box((0.13, -1.0, 0.22), (0.12, 0.02, 0.14), "yellow", rot=(0.3, 0, 0.62))
    return L.mesh_root(b, name, col, loc)


# ================================================================ swings
def swings(col, loc, name="Prop_Swings"):
    L.remove_tree(name)
    b = Builder()
    top = 2.4
    for sx in (-1.15, 1.15):
        for sy in (-0.8, 0.8):
            b.cyl((sx, sy, 0.0), (sx, 0.0, top), 0.045, 0.045, segs=6, color="blue")
            b.box((sx, sy, 0.04), (0.22, 0.22, 0.1), "concrete")
    b.cyl((-1.3, 0, top), (1.3, 0, top), 0.055, 0.055, segs=6, color="yellow")
    root = L.mesh_root(b, name, col, loc)

    s = Builder()                                     # seat swings around the crossbar
    for sx in (-0.3, 0.3):
        s.box((sx, 0, top), (0.08, 0.13, 0.13), "grey_dark")
        s.cyl((sx, 0, top - 0.05), (sx, 0, 0.56), 0.018, 0.018, segs=5, color="grey")
    s.box((0, 0, 0.53), (0.72, 0.06, 0.04), "grey_dark")
    for sy in (-0.09, 0.09):
        s.box((0, sy, 0.575), (0.74, 0.15, 0.045), "orange")
    s.to_object(name + "_Seat", col, pivot=(0, 0, top), parent=root)
    return root


# ================================================================ rocket climbing frame
def rocket(col, loc, name="Prop_Rocket"):
    L.remove_tree(name)
    b = Builder()
    R = 0.8
    angs = [math.radians(22.5 + 45 * k) for k in range(8)]
    for k, a in enumerate(angs):
        b.cyl((R * math.cos(a), R * math.sin(a), 0.3), (R * math.cos(a), R * math.sin(a), 3.5), 0.032,
              segs=6, color="white" if k % 2 else "blue")
    for z in (0.4, 1.0, 1.6, 2.2, 2.8, 3.4):
        pts = [((R + 0.02) * math.cos(a), (R + 0.02) * math.sin(a), z) for a in angs]
        b.tube_path(pts + [pts[0]], 0.03, "yellow" if z < 3.3 else "red", segs=6)
    # floor inside (octagon plate)
    oct_pts = [(0.78 * math.cos(a), 0.78 * math.sin(a)) for a in angs]
    b.prism(oct_pts, 0.05, "wood_light", matrix=Matrix.Translation((0, 0, 1.58)))
    # fins (4, diagonal) doubling as legs
    for k in range(4):
        a = math.radians(45 + 90 * k)
        x_ax = Vector((math.cos(a), math.sin(a), 0))
        z_ax = Vector((math.sin(a), -math.cos(a), 0))
        m = Matrix((x_ax, (0, 0, 1), z_ax)).transposed().to_4x4()
        m.translation = -z_ax * 0.03
        b.prism([(0.72, 0.0), (1.5, 0.0), (1.5, 0.22), (0.85, 1.75), (0.72, 1.75)], 0.06, "red", matrix=m)
    # nose cone with a porthole
    cone = Lathe([(0.0, 3.46), (0.86, 3.46), (0.86, 3.64), (0.64, 4.2), (0.34, 4.66), (0.1, 4.9), (0.0, 4.95)], 8)
    b.paint(b.lathe(cone, "red"), lambda f: "white" if f.calc_center_median().z < 3.66 else
            ("yellow" if f.calc_center_median().z > 4.75 else "red"))
    s = cone.s_at_z(4.05)
    b.decal_disc(cone, FRONT, s, 0.2, 0.2, "white", segs=10, off=0.006)
    b.decal_disc(cone, FRONT, s, 0.14, 0.14, "sky", segs=10, off=0.012)
    return L.mesh_root(b, name, col, loc)


# ================================================================ benches
def bench(col, loc, name="Prop_Bench", back=True, slat="green"):
    L.remove_tree(name)
    b = Builder()
    for sx in (-0.75, 0.75):
        b.box((sx, 0.0, 0.21), (0.12, 0.46, 0.42), "concrete", taper=(1.0, 0.8))
        if back:
            b.beam((sx, 0.19, 0.4), (sx, 0.27, 0.86), 0.1, 0.08, "concrete", up=(0, -1, 0))
    for sy in (-0.15, 0.0, 0.15):
        b.box((0, sy, 0.44), (2.0, 0.12, 0.045), slat)
    if back:
        for z in (0.62, 0.78):
            y = 0.19 + (z - 0.4) / 0.46 * 0.08 + 0.05
            b.box((0, y, z), (2.0, 0.035, 0.12), slat, rot=(-0.17, 0, 0))
    return L.mesh_root(b, name, col, loc)


# ================================================================ clothesline with laundry
def _cloth(b, x0, x1, top_fn, height, y, color_fn, amp=0.03, nx=6, ny=4, seed=0):
    """Double-sided hanging cloth (sheet/towel). color_fn(i, j) -> name."""
    rnd = random.Random(seed)
    def pt(i, j):
        x = x0 + (x1 - x0) * i / nx
        z = top_fn(x) - height * j / ny
        dy = amp * math.sin(i * 1.7 + seed) * (0.3 + j / ny) + rnd.uniform(-0.004, 0.004)
        return Vector((x, y + dy, z))
    grid = [[pt(i, j) for i in range(nx + 1)] for j in range(ny + 1)]
    for side, off in ((-1, -0.006), (1, 0.006)):
        for j in range(ny):
            for i in range(nx):
                q = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
                q = [p + Vector((0, off, 0)) for p in q]
                b.quad(q, color_fn(i, j), want=(0, side, 0))


def clothesline(col, loc, name="Prop_Clothesline"):
    L.remove_tree(name)
    b = Builder()
    X, top = 2.6, 2.2
    for sx in (-X, X):
        b.cyl((sx, 0, 0), (sx, 0, top), 0.042, segs=6, color="steel")
        b.cyl((sx, -0.58, top - 0.04), (sx, 0.58, top - 0.04), 0.035, segs=6, color="steel")
        b.box((sx, 0, 0.05), (0.25, 0.25, 0.1), "concrete")
    sag = 0.14
    line_z = lambda x: top - 0.03 - sag * (1 - (x / X) ** 2)
    for y in (-0.45, 0.0, 0.45):
        pts = [(x, y, line_z(x)) for x in [-X + 2 * X * k / 8 for k in range(9)]]
        b.tube_path(pts, 0.009, "white", segs=4)
    root = L.mesh_root(b, name, col, loc)

    w = Builder()   # laundry: separate so it can sway
    _cloth(w, -2.0, -0.6, lambda x: line_z(x) - 0.01, 1.1, 0.45, lambda i, j: "blue_pale", amp=0.05, seed=1)
    _cloth(w, 0.8, 1.5, lambda x: line_z(x) - 0.01, 0.75, -0.45,
           lambda i, j: "orange_red" if j % 2 == 0 else "white", nx=4, ny=4, seed=2)
    _cloth(w, -0.3, -0.12, lambda x: line_z(x) - 0.01, 0.3, -0.45, lambda i, j: "yellow", nx=1, ny=2, seed=3)
    _cloth(w, -0.06, 0.12, lambda x: line_z(x) - 0.01, 0.3, -0.45, lambda i, j: "yellow", nx=1, ny=2, seed=4)
    # t-shirt and dress: flat prisms (thin), hung from the middle line and the back line
    # (build in local XY, then map local Y -> world Z)
    mm = Matrix.Translation((0.55, 0.0, line_z(0.55) - 0.01)) @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1))) @ Matrix.Translation((0, 0, -0.006))
    w.prism([(x, y) for x, y in [(-0.28, 0), (-0.28, -0.14), (-0.2, -0.14), (-0.2, -0.62), (0.2, -0.62),
                                 (0.2, -0.14), (0.28, -0.14), (0.28, 0), (0.12, 0), (0.0, -0.06), (-0.12, 0)]],
            0.012, "sky", matrix=mm)
    mm = Matrix.Translation((1.0, 0.45, line_z(1.0) - 0.01)) @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1))) @ Matrix.Translation((0, 0, -0.006))
    w.prism([(-0.14, 0), (-0.3, -0.85), (0.3, -0.85), (0.14, 0)], 0.012, "pink", matrix=mm)
    for x, y in ((-1.9, 0.45), (-0.7, 0.45), (0.9, -0.45), (1.4, -0.45), (0.4, 0.0), (0.7, 0.0), (0.88, 0.45),
                 (1.12, 0.45)):
        w.box((x, y, line_z(x) + 0.005), (0.02, 0.03, 0.06), "red" if x > 0 else "green_light")   # pegs
    w.to_object(name + "_Laundry", col, pivot=(0, 0, top), parent=root)
    return root


# ================================================================ street lamp
def street_lamp(col, loc, name="Prop_StreetLamp"):
    L.remove_tree(name)
    b = Builder()
    pole = Lathe([(0.0, 0.0), (0.15, 0.0), (0.14, 0.45), (0.105, 0.5), (0.085, 6.0), (0.0, 6.05)], 8)
    b.lathe(pole, "concrete")
    b.cyl((0, 0, 0.0), (0, 0, 0.45), 0.16, 0.15, segs=8, color="concrete_light")
    arm = [(0, 0, 5.55), (0, -0.3, 5.9), (0, -0.75, 6.08), (0, -1.25, 6.14)]
    b.tube_path(arm, 0.038, "grey_dark", segs=6)
    b.sphere((0, -1.5, 6.12), 0.26, 8, 4, "grey", scale=(0.6, 1.25, 0.38))
    b.box((0, -1.52, 6.045), (0.24, 0.5, 0.04), "lamp")
    return L.mesh_root(b, name, col, loc)


# ================================================================ garages
def garage(col, loc, name, walls, doors, rust=0.1, seed=0):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    W, D, H0, H1 = 3.0, 5.5, 2.45, 2.2
    m = Matrix(((0, 0, 1, -W / 2), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    b.prism([(-D / 2, 0), (D / 2, 0), (D / 2, H1), (-D / 2, H0)], W, walls, matrix=m)
    b.beam((0, -D / 2 - 0.18, H0 + 0.04), (0, D / 2 + 0.1, H1 + 0.04), W + 0.2, 0.06, "roof")
    # door frame
    fy = -D / 2 - 0.02
    b.box((0, fy, 2.2), (2.7, 0.06, 0.1), "grey_dark")
    for sx in (-1.33, 1.33):
        b.box((sx, fy, 1.1), (0.08, 0.06, 2.2), "grey_dark")
    # two door leaves with ribs, handle and lock
    for sx in (-1, 1):
        cx = sx * 0.645
        b.box((cx, fy - 0.02, 1.08), (1.26, 0.04, 2.14), doors)
        for z in (0.45, 1.08, 1.7):
            b.box((cx, fy - 0.05, z), (1.16, 0.03, 0.07), doors)
        for i in range(int(rust * 20)):
            b.box((cx + rnd.uniform(-0.5, 0.5), fy - 0.043, rnd.uniform(0.1, 1.9)),
                  (rnd.uniform(0.08, 0.3), 0.004, rnd.uniform(0.05, 0.2)),
                  "rust" if rnd.random() < 0.5 else "rust_light")
    b.box((-0.1, fy - 0.07, 1.1), (0.04, 0.04, 0.22), "grey_dark")
    b.box((0.1, fy - 0.07, 1.0), (0.1, 0.05, 0.12), "black")
    # rust streaks on the side walls
    for sx in (-1, 1):
        for i in range(int(rust * 14)):
            b.box((sx * (W / 2 + 0.003), rnd.uniform(-2.4, 2.4), rnd.uniform(0.2, 2.0)),
                  (0.006, rnd.uniform(0.1, 0.5), rnd.uniform(0.05, 0.35)), "rust")
    return L.mesh_root(b, name, col, loc)


# ================================================================ panel apartment block
def panelka(col, loc, name, sections=4, floors=5, seed=0):
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    BAY, PER, FH, PL = 3.0, 5, 2.8, 0.6
    Lx = sections * PER * BAY
    D = 12.0
    H = PL + floors * FH
    wall, stair = "panel_beige", "panel_blue"

    def win_color():
        r = rnd.random()
        return "window_lit" if r < 0.12 else ("orange_light" if r < 0.17 else "window")

    def facade(origin, u, v, width, cols):
        """cols = list of (width, color, [(u0, u1, v0, v1, glass_color, depth, mullion)])"""
        w = u.cross(v)
        P = lambda a, c, d=0.0: origin + u * a + v * c + w * d
        x = 0.0
        for cw, ccol, wins in cols:
            wins = sorted(wins, key=lambda t: t[2])
            y = 0.0
            for (u0, u1, v0, v1, gcol, dep, mul) in wins:
                if v0 > y:
                    b.quad([P(x, y), P(x + cw, y), P(x + cw, v0), P(x, v0)], ccol, want=w)
                b.quad([P(x, v0), P(x + u0, v0), P(x + u0, v1), P(x, v1)], ccol, want=w)
                b.quad([P(x + u1, v0), P(x + cw, v0), P(x + cw, v1), P(x + u1, v1)], ccol, want=w)
                a0, a1 = x + u0, x + u1
                b.quad([P(a0, v0), P(a1, v0), P(a1, v0, -dep), P(a0, v0, -dep)], "concrete", want=v)
                b.quad([P(a0, v1), P(a1, v1), P(a1, v1, -dep), P(a0, v1, -dep)], ccol, want=-v)
                b.quad([P(a0, v0), P(a0, v1), P(a0, v1, -dep), P(a0, v0, -dep)], ccol, want=u)
                b.quad([P(a1, v0), P(a1, v1), P(a1, v1, -dep), P(a1, v0, -dep)], ccol, want=-u)
                b.quad([P(a0, v0, -dep), P(a1, v0, -dep), P(a1, v1, -dep), P(a0, v1, -dep)], gcol, want=w)
                if mul:
                    mid = (a0 + a1) / 2
                    b.quad([P(mid - 0.04, v0, -dep + 0.02), P(mid + 0.04, v0, -dep + 0.02),
                            P(mid + 0.04, v1, -dep + 0.02), P(mid - 0.04, v1, -dep + 0.02)], "white", want=w)
                y = v1
            if y < H:
                b.quad([P(x, y), P(x + cw, y), P(x + cw, H), P(x, H)], ccol, want=w)
            x += cw

    def floor_windows(stair_bay=False, front=True, entrance=False):
        wins = []
        if stair_bay:
            if entrance:
                wins.append((0.9, 2.1, 0.0, 2.2, "wood_dark", 0.2, False))
            for k in range(floors - 1):
                v0 = PL + FH * k + FH * 0.5 + 0.9
                wins.append((0.9, 2.1, v0, v0 + 1.1, win_color(), 0.12, True))
        else:
            for k in range(floors):
                v0 = PL + FH * k + 0.85
                wins.append((0.8, 2.2, v0, v0 + 1.4, win_color(), 0.12, True))
        return wins

    x0, y0 = -Lx / 2, -D / 2
    for side in ("front", "back"):
        cols = []
        for s in range(sections):
            for i in range(PER):
                is_stair = (i == PER // 2)
                cols.append((BAY, stair if is_stair else wall,
                             floor_windows(stair_bay=is_stair and side == "front", entrance=True)))
        if side == "front":
            facade(Vector((x0, y0, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)), Lx, cols)
        else:
            facade(Vector((-x0, -y0, 0)), Vector((-1, 0, 0)), Vector((0, 0, 1)), Lx, cols)
    # blind end walls
    for sx in (-1, 1):
        o = Vector((sx * Lx / 2, -sx * D / 2, 0))
        u = Vector((0, sx, 0))
        facade(o, u, Vector((0, 0, 1)), D, [(D, wall, [])])
    # roof + parapet
    b.quad([(x0, y0, H), (-x0, y0, H), (-x0, -y0, H), (x0, -y0, H)], "roof", want=(0, 0, 1))
    for sy in (-1, 1):
        b.box((0, sy * (D / 2 - 0.1), H + 0.25), (Lx, 0.2, 0.5), "concrete")
    for sx in (-1, 1):
        b.box((sx * (Lx / 2 - 0.1), 0, H + 0.25), (0.2, D, 0.5), "concrete")
    # plinth
    b.box((0, 0, PL / 2), (Lx + 0.12, D + 0.12, PL), "concrete")
    # entrances: canopy + steps; vent shafts + TV antennas on the roof
    for s in range(sections):
        cx = x0 + (s * PER + PER // 2) * BAY + BAY / 2
        b.box((cx, y0 - 0.65, 2.55), (2.2, 1.4, 0.12), "concrete")
        b.box((cx, y0 - 0.5, 0.08), (1.8, 1.0, 0.16), "concrete_light")
        for sx in (-0.95, 0.95):
            b.cyl((cx + sx, y0 - 1.2, 0), (cx + sx, y0 - 1.2, 2.5), 0.04, segs=5, color="grey_dark")
        b.box((cx + BAY, 0.8, H + 0.5), (0.9, 0.6, 1.0), "red_dark")
        for k in range(2):
            ax = cx + rnd.uniform(-5, 5)
            ay = rnd.uniform(-3, 3)
            b.cyl((ax, ay, H), (ax, ay, H + 2.2), 0.025, segs=4, color="grey")
            for j, z in enumerate((H + 1.6, H + 1.9, H + 2.15)):
                hw = 0.5 - j * 0.12
                b.cyl((ax - hw, ay, z), (ax + hw, ay, z), 0.012, segs=4, color="grey")
    if floors >= 9:
        b.box((0, 1.0, H + 1.3), (5.0, 4.0, 2.6), wall)
    # balconies on the front: slabs + colourful parapets
    for s in range(sections):
        for i in (1, 3):
            cx = x0 + (s * PER + i) * BAY + BAY / 2
            for k in range(1, floors):
                z = PL + FH * k
                b.box((cx, y0 - 0.55, z), (BAY - 0.3, 1.1, 0.12), "concrete")
                pc = rnd.choice(["panel_blue", "white", "concrete_light", "green_light", "orange_light"])
                b.box((cx, y0 - 1.08, z + 0.55), (BAY - 0.3, 0.05, 1.0), pc)
                for sx in (-1, 1):
                    b.box((cx + sx * (BAY / 2 - 0.17), y0 - 0.55, z + 0.55), (0.05, 1.1, 1.0), pc)
                if rnd.random() < 0.3:        # glazed balcony
                    b.box((cx, y0 - 1.06, z + 1.5), (BAY - 0.35, 0.04, 0.9), "window")
    return L.mesh_root(b, name, col, loc)


# ================================================================ small stuff
def curb(col, loc, name="Prop_Curb"):
    L.remove_tree(name)
    b = Builder()
    m = Matrix(((0, 0, 1, -0.5), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    b.prism([(-0.075, -0.17), (0.075, -0.17), (0.075, 0.15), (-0.05, 0.15), (-0.075, 0.125)], 1.0,
            "concrete_light", matrix=m)
    return L.mesh_root(b, name, col, loc)


def fence_low(col, loc, name="Prop_FenceLow"):
    """Courtyard lawn fence from painted pipe arches, 2 m section."""
    L.remove_tree(name)
    b = Builder()
    colors = ["yellow", "green", "red", "blue"]
    r = 0.3
    for k in range(6):
        cx = -0.75 + k * 0.3
        pts = []
        for i in range(7):
            a = math.pi * i / 6
            pts.append((cx + r * math.cos(a), 0.0, -0.05 + 0.42 * math.sin(a)))
        b.tube_path(pts, 0.017, colors[k % 4], segs=5)
    for sx in (-1.0, 1.0):
        b.cyl((sx, 0, 0), (sx, 0, 0.48), 0.028, segs=6, color="grey_dark")
    b.cyl((-1.0, 0, 0.46), (1.0, 0, 0.46), 0.02, segs=5, color="grey_dark")
    return L.mesh_root(b, name, col, loc)


def trash_bin(col, loc, name="Prop_TrashBin"):
    L.remove_tree(name)
    b = Builder()
    lat = Lathe([(0.0, 0.0), (0.17, 0.0), (0.19, 0.06), (0.24, 0.55), (0.265, 0.6), (0.22, 0.6),
                 (0.19, 0.25), (0.0, 0.25)], 8)
    def c(f):
        cm, n = f.calc_center_median(), f.normal
        if n.z > 0.5 and cm.z < 0.3:
            return "grey_dark"
        return "grey" if n.to_2d().dot(cm.to_2d()) < 0 else "concrete"
    b.paint(b.lathe(lat, "concrete"), c)
    return L.mesh_root(b, name, col, loc)


def _crown(b, center, radius, colors, rnd, n=5, stretch=1.0):
    for i in range(n):
        off = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.6, 0.8) * stretch)) * radius * 0.55
        rr = radius * rnd.uniform(0.55, 0.8)
        b.ico(Vector(center) + off, rr, colors[i % len(colors)], subdiv=1,
              scale=(1, 1, stretch * rnd.uniform(0.85, 1.1)), jitter=rr * 0.12, seed=rnd.randint(0, 999))


def birch(col, loc, name="Prop_Tree_Birch"):
    L.remove_tree(name)
    rnd = random.Random(8)
    b = Builder()
    trunk = Lathe([(0.0, 0.0), (0.14, 0.0), (0.11, 1.0), (0.08, 3.5), (0.04, 5.2), (0.0, 5.3)], 6)
    b.lathe(trunk, "white")
    for i in range(16):
        s = rnd.uniform(0.3, 4.2)
        phi = rnd.uniform(0, 2 * math.pi)
        w = rnd.uniform(0.04, 0.09)
        b.decal_strip(trunk, [(phi - w / 0.1, s), (phi + w / 0.1, s + rnd.uniform(-0.02, 0.02))],
                      rnd.uniform(0.02, 0.04), "black", off=0.004)
    for a, z in ((0.3, 2.9), (2.4, 3.4), (4.3, 3.9)):
        b.cyl((0, 0, z), (0.8 * math.cos(a), 0.8 * math.sin(a), z + 0.7), 0.03, 0.015, segs=4, color="white")
    _crown(b, (0, 0, 4.4), 1.5, ["green_light", "green", "green_light", "green"], rnd, n=6, stretch=1.2)
    return L.mesh_root(b, name, col, loc)


def poplar(col, loc, name="Prop_Tree_Poplar"):
    L.remove_tree(name)
    rnd = random.Random(4)
    b = Builder()
    trunk = Lathe([(0.0, 0.0), (0.24, 0.0), (0.2, 1.2), (0.13, 6.0), (0.0, 7.5)], 6)
    b.lathe(trunk, "grey")
    _crown(b, (0, 0, 5.3), 1.6, ["green_dark", "green", "bottle", "green"], rnd, n=7, stretch=2.2)
    return L.mesh_root(b, name, col, loc)


def bush(col, loc, name="Prop_Bush"):
    L.remove_tree(name)
    rnd = random.Random(12)
    b = Builder()
    for i, (x, y, r) in enumerate(((0, 0, 0.7), (0.65, 0.15, 0.55), (-0.6, 0.1, 0.5), (0.1, 0.45, 0.5))):
        b.ico((x, y, r * 0.8), r, ["green", "green_dark", "green_light", "green"][i], subdiv=1,
              scale=(1, 1, 0.85), jitter=r * 0.12, seed=i + 3)
    return L.mesh_root(b, name, col, loc)


def _hash_rand(p, seed=0):
    return random.Random(hash((round(p.x, 3), round(p.y, 3), round(p.z, 3), seed)))


def hedge(col, loc, name="Prop_Hedge"):
    """Trimmed hedge section 2 x 0.8 x 1.2 m. Ends stay flat so sections tile along X."""
    L.remove_tree(name)
    import bmesh
    b = Builder()
    Lx, D, H = 2.0, 0.8, 1.2
    nx, ny, nz = 5, 2, 3
    xs = [-Lx / 2 + Lx * i / nx for i in range(nx + 1)]
    ys = [-D / 2 + D * j / ny for j in range(ny + 1)]
    zs = [H * k / nz for k in range(nz + 1)]
    faces = []
    # sides (front/back), top, and the two end caps
    def grid(pts):
        rows = len(pts) - 1
        cols = len(pts[0]) - 1
        vv = [[b.bm.verts.new(p) for p in row] for row in pts]
        out = []
        for r in range(rows):
            for c in range(cols):
                out.append(b.bm.faces.new([vv[r][c], vv[r][c + 1], vv[r + 1][c + 1], vv[r + 1][c]]))
        return out
    front = grid([[Vector((x, -D / 2, z)) for x in xs] for z in zs])
    back = grid([[Vector((x, D / 2, z)) for x in reversed(xs)] for z in zs])
    top = grid([[Vector((x, y, H)) for x in xs] for y in ys])
    left = grid([[Vector((-Lx / 2, y, z)) for y in reversed(ys)] for z in zs])
    right = grid([[Vector((Lx / 2, y, z)) for y in ys] for z in zs])
    allf = front + back + top + left + right
    bmesh.ops.remove_doubles(b.bm, verts=list({v for f in allf for v in f.verts}), dist=1e-5)
    # organic jitter (ends and ground line untouched so sections tile)
    for v in b.bm.verts:
        p = v.co
        end = abs(abs(p.x) - Lx / 2) < 1e-4
        r = _hash_rand(p, 7)
        if p.z > 1e-4 and not end:
            p.y += r.uniform(-0.025, 0.025) + (0.02 if p.y > 0 else -0.02) * (p.z / H)
            p.z += r.uniform(-0.025, 0.03) if p.z > H - 1e-4 else r.uniform(-0.02, 0.02)
        if p.z > H - 1e-4:                                 # soft rounded top edge
            p.y *= 0.86
    b.bm.normal_update()
    rnd = random.Random(3)
    for f in b.bm.faces:
        f.normal_update()
        if f.normal.z > 0.6:
            b.paint([f], "green_light" if rnd.random() < 0.85 else "green")
        else:
            b.paint([f], "green_dark" if rnd.random() < 0.1 else "green")
    return L.mesh_root(b, name, col, loc)


# Внутренний размер арены «Двор» между бортами, метры (как в Unity: x — восток, y — север).
ARENA_W, ARENA_D = 44.0, 30.0


def yard_ground(col, loc, name="Env_YardGround"):
    """Ground of the courtyard in Unity-arena coordinates (arena ARENA_W x ARENA_D around the origin).
    Asphalt yard, lawns, a driveway behind the garages, chalk markings. Visual only (no collider).
    Layout around the arena: garages along the north side, the driveway behind them,
    sidewalks in front of the blocks (north block at hd + 22, side blocks at +-(hw + 17.5) in Unity)."""
    L.remove_tree(name)
    rnd = random.Random(21)
    b = Builder()
    hw, hd = ARENA_W / 2, ARENA_D / 2
    # lawn: coarse grid with colour variation, slightly below the asphalt
    lawn = b.grid(100, 90, 20, 18, "green", matrix=L.frame((0, 12, -0.02)))
    b.paint([f for f in lawn if rnd.random() < 0.12], "green_light")
    b.paint([f for f in lawn if rnd.random() < 0.04], "olive")
    # yard asphalt (under the hedges too)
    yw, yd = ARENA_W + 2, ARENA_D + 2
    yard = b.grid(yw, yd, int(yw), int(yd), "asphalt", matrix=L.frame((0, 0, 0)))
    b.paint([f for f in yard if rnd.random() < 0.03], "grey_dark")
    # driveway behind the garages and sidewalks in front of the blocks
    road_y = hd + 9.5
    road = b.grid(100, 7, 25, 2, "asphalt", matrix=L.frame((0, road_y, -0.005)))
    b.paint([f for f in road if rnd.random() < 0.06], "grey_dark")
    for i in range(-12, 13):
        b.grid(1.6, 0.14, 1, 1, "white", matrix=L.frame((i * 4.0, road_y, 0.003)))
    b.grid(100, 2.2, 25, 1, "concrete_light", matrix=L.frame((0, road_y + 4.7, -0.004)))
    side_x = hw + 9.0
    b.grid(2.2, 90, 1, 22, "concrete_light", matrix=L.frame((-side_x, 12, -0.004)))
    b.grid(2.2, 90, 1, 22, "concrete_light", matrix=L.frame((side_x, 12, -0.004)))
    # chalk: a dodgeball court ('вышибалы') and hopscotch
    def line(x0, y0, x1, y1, w, color):
        d = Vector((x1 - x0, y1 - y0))
        m = L.frame(((x0 + x1) / 2, (y0 + y1) / 2, 0.004), (0, 0, math.atan2(d.y, d.x)))
        b.grid(d.length, w, max(1, int(d.length / 2)), 1, color, matrix=m)
    cw, ch = 9.0, 5.0
    for x0, y0, x1, y1 in ((-cw, -ch, cw, -ch), (cw, -ch, cw, ch), (cw, ch, -cw, ch), (-cw, ch, -cw, -ch),
                           (0, -ch, 0, ch)):
        line(x0, y0, x1, y1, 0.09, "grey_light")
    for k, (dx, dy) in enumerate(((0, 0), (0, 0.55), (-0.28, 1.1), (0.28, 1.1), (0, 1.65), (-0.28, 2.2),
                                  (0.28, 2.2), (0, 2.75))):
        cx, cy = hw - 4.0 + dx, -hd + 1.7 + dy
        col_c = ["white", "yellow", "pink"][k % 3]
        line(cx - 0.26, cy - 0.26, cx + 0.26, cy - 0.26, 0.06, col_c)
        line(cx + 0.26, cy - 0.26, cx + 0.26, cy + 0.26, 0.06, col_c)
        line(cx + 0.26, cy + 0.26, cx - 0.26, cy + 0.26, 0.06, col_c)
        line(cx - 0.26, cy + 0.26, cx - 0.26, cy - 0.26, 0.06, col_c)
    # manhole cover and a couple of drains
    for x, y in ((-4.0, 7.0), (16.5, 2.0), (-17.0, -9.5)):
        ring = [(x + 0.36 * math.cos(a), y + 0.36 * math.sin(a)) for a in [2 * math.pi * i / 10 for i in range(10)]]
        b.prism(ring, 0.01, "grey_dark", matrix=Matrix.Translation((0, 0, 0.0)))
    # Размечено в координатах арены Unity (x — восток, y — север). Экспорт переводит Blender (x, y) в
    # Unity (-x, -y), поэтому заранее разворачиваем на 180°, чтобы в Unity север был там, где задуман.
    for v in b.bm.verts:
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return L.mesh_root(b, name, col, loc)


def build_all(col_parent):
    col = L.collection("Yard", col_parent)
    Y = 8.0
    out = [
        sandbox(col, (0, Y, 0)),
        swings(col, (5, Y, 0)),
        rocket(col, (9.5, Y, 0)),
        bench(col, (13.5, Y - 1, 0)),
        bench(col, (13.5, Y + 1, 0), name="Prop_Bench_NoBack", back=False, slat="blue"),
        clothesline(col, (18.5, Y, 0)),
        street_lamp(col, (23, Y, 0)),
        garage(col, (0, Y + 9, 0), "Prop_Garage_A", "green_dark", "green", rust=0.15, seed=1),
        garage(col, (3.05, Y + 9, 0), "Prop_Garage_B", "steel", "blue", rust=0.25, seed=2),
        garage(col, (6.1, Y + 9, 0), "Prop_Garage_C", "grey", "rust_light", rust=0.6, seed=3),
        curb(col, (10, Y + 7, 0)),
        fence_low(col, (12.5, Y + 7, 0)),
        trash_bin(col, (15, Y + 7, 0)),
        birch(col, (18, Y + 8, 0)),
        poplar(col, (21.5, Y + 8, 0)),
        bush(col, (25, Y + 8, 0)),
        panelka(col, (0, 48, 0), "Prop_Panelka_5F", sections=4, floors=5, seed=1),
        panelka(col, (52, 48, 0), "Prop_Panelka_9F", sections=2, floors=9, seed=2),
        hedge(col, (28, Y, 0)),
        yard_ground(col, (0, -120, 0)),
    ]
    return out

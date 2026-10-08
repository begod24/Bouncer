"""Arena 2, the hockey box («Хоккейная коробка», evening): boards with rounded corners, faded hockey
markings on the asphalt, goals, floodlight masts, a team bench and straight board modules.
Env_* pieces are laid out in Unity-arena coordinates (x east, y north, like Env_YardGround) and turned
180 degrees before export for the same reason. Props: origin on the ground, front to -Y."""
import math, random
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

# Inner size of the box between the boards, metres, and the corner radius.
RINK_W, RINK_D, RINK_R = 40.0, 26.0, 6.0
BOARD_H = 1.2
UP = Vector((0, 0, 1))


def ccw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return pts if area > 0 else list(reversed(pts))


def face_frame(origin, normal, along):
    """Frame on a vertical face: X = along (reading direction), Y = up, Z = normal (out of the face)."""
    n = Vector(normal).normalized()
    x = Vector(along).normalized()
    m = Matrix((x, UP, n)).transposed().to_4x4()
    m.translation = Vector(origin)
    return m


def flat_box(b, m, x, y, w, h, depth, color, rot=0.0):
    """Box lying on a face frame m: centre (x, y) in the face, standing proud by `depth`."""
    local = Matrix.Translation((x, y, depth / 2)) @ Matrix.Rotation(rot, 4, "Z")
    return b.box_m(m @ local, (w, h, depth), color)


def flat_prism(b, m, pts, depth, color, x=0.0, y=0.0, rot=0.0, scale=1.0):
    local = Matrix.Translation((x, y, 0.0)) @ Matrix.Rotation(rot, 4, "Z") @ Matrix.Diagonal((scale, scale, 1, 1))
    return b.prism(ccw(pts), depth, color, matrix=m @ local)


# ================================================================ graffiti (decals from the atlas)
TAGS = ["tag_vitya_lena", "tag_dvor", "tag_hockey", "tag_5b", "tag_my_tut", "tag_seryoga", "tag_tut_byl"]
DOODLES = [("doodle_heart", 0.42), ("doodle_scribble", 0.26), ("doodle_arrow", 0.24), ("doodle_smiley", 0.4),
           ("shape_star", 0.3)]
SPRAY = ["pink", "yellow", "lime", "orange", "white", "red_light", "cyan", "lavender"]


def graffiti(b, m, width, rnd, tag=None):
    """Scrawls on one board face (m = face_frame at mid-height): a handwritten tag or a doodle, sprayed in
    one palette colour. Each one is a single decal quad."""
    col = rnd.choice(SPRAY)
    place = m @ Matrix.Translation((rnd.uniform(-0.1, 0.1), rnd.uniform(-0.05, 0.1), 0.0)) \
        @ Matrix.Rotation(rnd.uniform(-0.08, 0.08), 4, "Z")
    if tag:
        h = 0.22
        w = min(h * L.decal_entry(tag)["aspect"], width * 0.85)
        b.decal(tag, place, col, width=w, lift=0.002)
    else:
        name, h = rnd.choice(DOODLES)
        w = min(h * L.decal_entry(name)["aspect"], width * 0.85)
        b.decal(name, place, col, width=w, lift=0.002)


# ================================================================ boards
PAINT = {"planks": "blue", "backing": "navy", "kick": "yellow", "rail": "white", "post": "grey"}


def board_section(b, p0, p1, inward, rnd=None, graffiti_in=None, graffiti_out=None, post=True, door=False):
    """One straight run of boards between ground points p0 -> p1. `inward` points into the box."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    L_ = d.length
    x = d.normalized()
    n = Vector(inward).normalized()
    mid = (p0 + p1) / 2

    side = UP.cross(x)                     # keeps every box right-handed whichever way the run goes

    def piece(z0, z1, thick, color, off=0.0):
        m = Matrix((x, side, UP)).transposed().to_4x4()
        m.translation = mid + n * off + UP * ((z0 + z1) / 2)
        b.box_m(m, (L_ + 0.004, thick, z1 - z0), color)

    piece(0.0, 0.22, 0.15, PAINT["kick"])
    piece(0.22, 1.12, 0.07, PAINT["backing"])
    for k in range(4):                                                           # planks with thin dark gaps
        z0 = 0.225 + k * 0.224
        piece(z0, z0 + 0.216, 0.11, PAINT["planks"] if not door else "blue_light")
    piece(1.12, BOARD_H, 0.16, PAINT["rail"])
    if post:
        m = Matrix((x, side, UP)).transposed().to_4x4()
        m.translation = p0 - n * 0.12 + UP * (BOARD_H / 2 + 0.02)
        b.box_m(m, (0.1, 0.1, BOARD_H + 0.04), PAINT["post"])
    if door:                                                                     # a closed gate: frame, hinges, latch
        fm = face_frame(mid - n * 0.056, -n, x)
        for sx in (-1, 1):
            flat_box(b, fm, sx * (L_ / 2 - 0.05), 0.66, 0.06, 0.9, 0.012, "white")
            flat_box(b, fm, sx * (L_ / 2 - 0.05), 0.4, 0.1, 0.05, 0.02, "tin_dark")
            flat_box(b, fm, sx * (L_ / 2 - 0.05), 0.95, 0.1, 0.05, 0.02, "tin_dark")
        flat_box(b, fm, 0.0, 0.7, 0.14, 0.04, 0.03, "tin")
    if rnd is not None:
        if graffiti_in is not None:
            gm = face_frame(mid + n * 0.056 + UP * 0.66, n, -x)
            graffiti(b, gm, L_, rnd, graffiti_in if isinstance(graffiti_in, str) else None)
        if graffiti_out is not None:
            gm = face_frame(mid - n * 0.056 + UP * 0.66, -n, x)
            graffiti(b, gm, L_, rnd, graffiti_out if isinstance(graffiti_out, str) else None)


def _outline():
    """Board runs around the box: (p0, p1, inward) — straight sides split into ~2 m panels, corners in arcs."""
    hw, hd, R = RINK_W / 2, RINK_D / 2, RINK_R
    runs = []

    def straight(a, b_, inward):
        a, b_ = Vector(a), Vector(b_)
        n = max(1, round((b_ - a).length / 2.0))
        for k in range(n):
            runs.append((a.lerp(b_, k / n), a.lerp(b_, (k + 1) / n), inward))

    def arc(cx, cy, a0, a1, segs=8):
        for k in range(segs):
            t0 = a0 + (a1 - a0) * k / segs
            t1 = a0 + (a1 - a0) * (k + 1) / segs
            p0 = Vector((cx + R * math.cos(t0), cy + R * math.sin(t0), 0))
            p1 = Vector((cx + R * math.cos(t1), cy + R * math.sin(t1), 0))
            mid = (t0 + t1) / 2
            runs.append((p0, p1, Vector((-math.cos(mid), -math.sin(mid), 0))))

    # counter-clockwise from the south-west corner
    straight((-hw + R, -hd, 0), (hw - R, -hd, 0), (0, 1, 0))
    arc(hw - R, -hd + R, -math.pi / 2, 0)
    straight((hw, -hd + R, 0), (hw, hd - R, 0), (-1, 0, 0))
    arc(hw - R, hd - R, 0, math.pi / 2)
    straight((hw - R, hd, 0), (-hw + R, hd, 0), (0, -1, 0))
    arc(-hw + R, hd - R, math.pi / 2, math.pi)
    straight((-hw, hd - R, 0), (-hw, -hd + R, 0), (1, 0, 0))
    arc(-hw + R, -hd + R, math.pi, 1.5 * math.pi)
    return runs


def _net_fence(b, x_side):
    """Tall ball-stop net above the end boards: poles and a sparse mesh."""
    hd = RINK_D / 2 - RINK_R
    ys = [-hd + k * (2 * hd) / 4 for k in range(5)]
    x = x_side * (RINK_W / 2 + 0.12)
    top = 3.6
    for y in ys:
        b.cyl((x, y, BOARD_H), (x, y, top), 0.045, 0.04, segs=6, color="grey_dark")
    b.beam((x, ys[0], top), (x, ys[-1], top), 0.05, 0.05, "grey_dark")
    for k in range(1, 6):                                                         # horizontal wires
        z = BOARD_H + (top - BOARD_H) * k / 6
        b.beam((x, ys[0], z), (x, ys[-1], z), 0.012, 0.012, "grey_light")
    y = ys[0]
    while y < ys[-1]:                                                             # vertical wires
        b.beam((x, y, BOARD_H), (x, y, top), 0.012, 0.012, "grey_light")
        y += 0.5


def rink_boards(col, loc, name="Env_RinkBoards", seed=61):
    """All boards of the box in arena coordinates, two closed gates on the long sides, ball-stop nets
    behind the goals, graffiti inside and out. The box keeps balls in: its runs are where colliders go."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    runs = _outline()
    doors = {len(runs) // 8 + 1, len(runs) // 2 + len(runs) // 8 + 1}   # one on each long side
    tags = list(TAGS)
    rnd.shuffle(tags)
    for k, (p0, p1, inward) in enumerate(runs):
        g_in = g_out = None
        if k not in doors:
            if rnd.random() < 0.18:
                g_in = tags.pop() if tags and rnd.random() < 0.55 else True
            if rnd.random() < 0.25:
                g_out = tags.pop() if tags and rnd.random() < 0.4 else True
        board_section(b, p0, p1, inward, rnd, g_in, g_out, door=k in doors)
    _net_fence(b, 1)
    _net_fence(b, -1)
    for v in b.bm.verts:                                     # arena coords -> export turns them back
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return L.mesh_root(b, name, col, loc)


def rink_ground(col, loc, name="Env_RinkGround", seed=62):
    """Asphalt inside the box with faded hockey markings (centre line, blue lines, circles, creases),
    a trodden band round the boards and lawn beyond. Arena coordinates, visual only."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    hw, hd = RINK_W / 2, RINK_D / 2
    lawn = b.grid(110, 90, 22, 18, "green", matrix=L.frame((0, 8, -0.03)))
    b.paint([f for f in lawn if rnd.random() < 0.12], "green_light")
    b.paint([f for f in lawn if rnd.random() < 0.05], "olive")
    band = b.grid(RINK_W + 8, RINK_D + 8, 12, 8, "olive", matrix=L.frame((0, 0, -0.015)))
    b.paint([f for f in band if rnd.random() < 0.35], "sand_dark")
    rink = b.grid(RINK_W + 0.6, RINK_D + 0.6, int(RINK_W), int(RINK_D), "asphalt", matrix=L.frame((0, 0, 0)))
    b.paint([f for f in rink if rnd.random() < 0.05], "grey_dark")
    b.paint([f for f in rink if rnd.random() < 0.02], "grey")

    def line(x0, y0, x1, y1, w, color, z=0.004):
        d = Vector((x1 - x0, y1 - y0))
        m = L.frame(((x0 + x1) / 2, (y0 + y1) / 2, z), (0, 0, math.atan2(d.y, d.x)))
        b.grid(d.length, w, max(1, int(d.length / 2)), 1, color, matrix=m)

    def ring(cx, cy, r, w, color, segs=40, z=0.005):
        for k in range(segs):
            a0, a1 = 2 * math.pi * k / segs, 2 * math.pi * (k + 1) / segs
            pts = [(cx + (r - w / 2) * math.cos(a0), cy + (r - w / 2) * math.sin(a0), z),
                   (cx + (r + w / 2) * math.cos(a0), cy + (r + w / 2) * math.sin(a0), z),
                   (cx + (r + w / 2) * math.cos(a1), cy + (r + w / 2) * math.sin(a1), z),
                   (cx + (r - w / 2) * math.cos(a1), cy + (r - w / 2) * math.sin(a1), z)]
            if rnd.random() > 0.08:                                                  # worn gaps
                b.quad(pts, color, want=(0, 0, 1))

    def disc(cx, cy, r, color, segs=16, z=0.006):
        pts = [(cx + r * math.cos(2 * math.pi * k / segs), cy + r * math.sin(2 * math.pi * k / segs)) for k in range(segs)]
        b.prism(pts, 0.001, color, matrix=Matrix.Translation((0, 0, z - 0.001)))

    red, blue = "coral", "blue_light"
    line(0, -hd, 0, hd, 0.3, red)                                                  # centre line
    for sx in (-1, 1):
        line(sx * hw / 3, -hd, sx * hw / 3, hd, 0.3, blue)                          # blue lines
        gx = sx * (hw - 3.4)
        line(gx, -hd + 2.2, gx, hd - 2.2, 0.06, red)                               # goal lines
        crease = [(gx, -1.4)]
        for k in range(1, 8):
            a = -math.pi / 2 + math.pi * k / 8
            crease.append((gx - sx * 1.6 * math.cos(a), 1.4 * math.sin(a)))
        crease.append((gx, 1.4))
        b.prism(ccw(crease), 0.001, "sky", matrix=Matrix.Translation((0, 0, 0.005)))   # goal crease
        for sy in (-1, 1):
            cx, cy = sx * (hw - 8.0), sy * (hd / 2 - 0.5)
            ring(cx, cy, 3.6, 0.07, red)
            disc(cx, cy, 0.3, red)
            disc(sx * (hw / 3 - 1.5), sy * (hd / 2 - 0.5), 0.3, red)
    ring(0, 0, 3.6, 0.07, blue)
    disc(0, 0, 0.3, blue)
    for v in b.bm.verts:
        v.co.x, v.co.y = -v.co.x, -v.co.y
    return L.mesh_root(b, name, col, loc)


# ================================================================ props
def goal(col, loc, name="Rink_Goal"):
    """Hockey goal 1.83 x 1.22 m, 1.1 m deep: red frame, white net. Origin at the goal line, mouth to -Y."""
    L.remove_tree(name)
    b = Builder()
    w, h, dep = 1.83 / 2, 1.22, 1.1
    red = "red"
    for sx in (-1, 1):
        b.cyl((sx * w, 0, 0), (sx * w, 0, h), 0.03, 0.03, segs=8, color=red)             # posts
        b.tube_path([(sx * w, 0, 0.02), (sx * (w + 0.1), dep * 0.6, 0.02), (sx * 0.5, dep, 0.02)], 0.025, red, segs=6)
        b.tube_path([(sx * w, 0.02, h), (sx * 0.55, dep * 0.45, h - 0.15), (sx * 0.4, dep * 0.7, 0.25)], 0.02, "white",
                    segs=5)
    b.cyl((-w, 0, h), (w, 0, h), 0.03, 0.03, segs=8, color=red)                           # crossbar
    b.beam((-0.5, dep, 0.02), (0.5, dep, 0.02), 0.05, 0.05, red)
    for k in range(9):                                                                  # net: vertical strands
        t = -1 + 2 * k / 8
        top = Vector((t * w * 0.95, 0.03, h - 0.02))
        back = Vector((t * 0.48, dep - 0.02, 0.05))
        b.beam(top, (top + back) / 2 + Vector((0, 0.18, -0.05)), 0.012, 0.012, "white")
        b.beam((top + back) / 2 + Vector((0, 0.18, -0.05)), back, 0.012, 0.012, "white")
    for k in range(1, 4):                                                               # net: horizontal strands
        f = k / 4
        z = h * (1 - f) + 0.05 * f
        y = 0.03 + (dep - 0.05) * f + 0.18 * math.sin(math.pi * f)
        b.beam((-w * (1 - f) - 0.48 * f, y, z), (w * (1 - f) + 0.48 * f, y, z), 0.012, 0.012, "white")
    return L.mesh_root(b, name, col, loc)


def floodlight(col, loc, name="Rink_Floodlight"):
    """Floodlight mast ~8 m on a concrete foot, a bar with two lamps angled down towards -Y (the box).
    The lamp glass is the lamp cell: it glows in the evening."""
    L.remove_tree(name)
    b = Builder()
    b.box((0, 0, 0.2), (0.6, 0.6, 0.4), "concrete")
    b.cyl((0, 0, 0.4), (0, 0, 8.0), 0.12, 0.08, segs=8, color="grey")
    for z in [1.5 + 0.45 * k for k in range(13)]:                                       # rungs
        b.beam((-0.16, 0.1, z), (0.16, 0.1, z), 0.025, 0.025, "grey_dark")
    for sx in (-1, 1):
        b.beam((sx * 0.14, 0.1, 1.4), (sx * 0.14, 0.1, 7.3), 0.03, 0.03, "grey_dark")
    b.beam((-0.9, 0, 8.0), (0.9, 0, 8.0), 0.1, 0.1, "grey_dark")
    for sx in (-1, 1):
        m = L.frame((sx * 0.6, -0.2, 7.85), (0.6, 0, 0))
        b.box_m(m, (0.55, 0.3, 0.4), "grey_dark")
        b.box_m(m @ Matrix.Translation((0, -0.16, 0)), (0.47, 0.02, 0.32), "lamp")
    return L.mesh_root(b, name, col, loc)


def team_bench(col, loc, name="Rink_TeamBench"):
    """Long bench for the teams, 4 m, planks on metal legs, a back rest."""
    L.remove_tree(name)
    b = Builder()
    for k in range(3):
        b.box((0, -0.12 + k * 0.13, 0.45), (4.0, 0.11, 0.04), "wood_light" if k % 2 else "wood")
    for k in range(2):
        b.box((0, 0.28, 0.65 + k * 0.16), (4.0, 0.03, 0.11), "wood")
    for x in (-1.8, 0.0, 1.8):
        b.box((x, 0.0, 0.22), (0.05, 0.36, 0.44), "grey_dark")
        b.box((x, 0.27, 0.6), (0.05, 0.04, 0.5), "grey_dark")
    return L.mesh_root(b, name, col, loc)


def board_module(col, loc, name="Rink_Board_2m", seed=0, tag=None):
    """A single straight 2 m board panel (for building other layouts); `tag` adds graffiti inside."""
    L.remove_tree(name)
    rnd = random.Random(seed)
    b = Builder()
    board_section(b, (1.0, 0, 0), (-1.0, 0, 0), (0, -1, 0), rnd if tag else None, tag, None)
    return L.mesh_root(b, name, col, loc)


def build_all(col_parent):
    col = L.collection("Rink", col_parent)
    X = -130.0
    return [
        rink_ground(col, (X, 0, 0)),
        rink_boards(col, (X, 0, 0)),
        goal(col, (X - 30, -6, 0)),
        floodlight(col, (X - 30, -10, 0)),
        team_bench(col, (X - 30, -14, 0)),
        board_module(col, (X - 36, -6, 0)),
        board_module(col, (X - 36, -9, 0), name="Rink_Board_2m_Graffiti", seed=5, tag="tag_vitya_lena"),
    ]

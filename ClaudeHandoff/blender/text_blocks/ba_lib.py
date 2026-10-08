"""Bouncer art toolkit: palette texture, shared material, low-poly builders, preview renders.

All models are flat-shaded and use ONE material (M_Palette). Every face is UV-mapped
into a single cell of an 8x8 palette texture, so recoloring / time-of-day = swapping the texture.
Convention: Blender units = meters, Z up, characters face -Y (front).
"""
import bpy, bmesh, math, random
from mathutils import Vector, Matrix

# ---------------------------------------------------------------- palette
GRID = 8          # 8x8 cells
CELL = 8          # px per cell
TEX = GRID * CELL # 64x64 texture

ROWS = [
    ["red_dark", "red", "red_light", "pink", "pink_light", "maroon", "coral", "orange_red"],
    ["orange", "orange_light", "ochre", "yellow", "cream", "sand", "sand_dark", "gold"],
    ["green_dark", "green", "green_light", "lime", "olive", "teal", "mint", "bottle"],
    ["navy", "blue", "blue_light", "sky", "cyan", "steel", "blue_pale", "indigo"],
    ["purple", "lavender", "skin_light", "skin", "rubber", "rubber_dark", "lips", "eye_blue"],
    ["wood_dark", "wood", "wood_light", "leather", "leather_dark", "rust", "rust_light", "brown"],
    ["black", "grey_dark", "grey", "grey_light", "concrete", "concrete_light", "asphalt", "white"],
    ["tin", "tin_dark", "panel_beige", "panel_blue", "window", "window_lit", "lamp", "roof"],
]

DAY = {
    "red_dark": "8E1B1F", "red": "D8262E", "red_light": "EE5A4F", "pink": "EF8AA3",
    "pink_light": "F7BFCB", "maroon": "5C1A24", "coral": "F07A5A", "orange_red": "E6472A",
    "orange": "F08A24", "orange_light": "F7B04A", "ochre": "C99A2E", "yellow": "F6D03C",
    "cream": "FFF0C8", "sand": "E6C98C", "sand_dark": "C4A263", "gold": "E0B03A",
    "green_dark": "2E5B2D", "green": "4A8B3A", "green_light": "7DBA4E", "lime": "C4DC4C",
    "olive": "7A7B3A", "teal": "2D8A7E", "mint": "9ED8B4", "bottle": "1F4A3C",
    "navy": "1E2A5C", "blue": "2E5EB4", "blue_light": "5B8FDB", "sky": "9DC7F0",
    "cyan": "46B6D4", "steel": "5E7894", "blue_pale": "CADDF2", "indigo": "3A3380",
    "purple": "7A3FA8", "lavender": "B08AD6", "skin_light": "FBDDC6", "skin": "F1B898",
    "rubber": "F2C2A6", "rubber_dark": "D39277", "lips": "D24E5C", "eye_blue": "3C79C6",
    "wood_dark": "57381F", "wood": "8A5A33", "wood_light": "BA8757", "leather": "86502C",
    "leather_dark": "4C2915", "rust": "9A4921", "rust_light": "C2713E", "brown": "6A4429",
    "black": "17171B", "grey_dark": "3A3B41", "grey": "6E7077", "grey_light": "A5A7AA",
    "concrete": "B6B0A4", "concrete_light": "D8D2C5", "asphalt": "4A4A4F", "white": "F4F2EC",
    "tin": "BAC0C6", "tin_dark": "7C838C", "panel_beige": "D8CCB2", "panel_blue": "9CB2C2",
    "window": "2B3646", "window_lit": "FFE39A", "lamp": "FFF6D0", "roof": "54504A",
}

# Cells that glow (emission texture). Used for lamps and lit windows at dusk.
EMISSIVE = {"window_lit": 1.0, "lamp": 1.0}

INDEX = {name: (c, r) for r, row in enumerate(ROWS) for c, name in enumerate(row)}


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def cell_uv(name):
    c, r = INDEX[name]
    return Vector(((c + 0.5) / GRID, 1.0 - (r + 0.5) / GRID))


def morning_variant(rgb):
    """Morning = warm yellow/green tint, a bit softer than day. The warm shift scales with saturation,
    so neutral greys (asphalt, concrete) stay almost neutral and don't turn brown."""
    r, g, b = rgb
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    sat = max(rgb) - min(rgb)
    r, g, b = [lum + (x - lum) * 0.9 for x in (r, g, b)]      # slight desaturation
    w = 0.35 + 0.65 * min(1.0, sat * 2.5)
    r, g, b = r * (1 + 0.05 * w) + 0.02 * w, g * (1 + 0.02 * w) + 0.015 * w, b * (1 - 0.14 * w)
    return tuple(min(1.0, max(0.0, x)) for x in (r, g, b))


def _tinted(rgb, desat, mul, add):
    """Shared recipe of the evening/dusk/night palettes: desaturate, multiply by the light tint, lift."""
    lum = 0.3 * rgb[0] + 0.59 * rgb[1] + 0.11 * rgb[2]
    out = []
    for x, m, a in zip(rgb, mul, add):
        x = lum + (x - lum) * (1.0 - desat)
        out.append(min(1.0, max(0.0, x * m + a)))
    return tuple(out)


# Lamps and lit windows keep their colour at any time of day: they glow.
_KEEP_LIT = ("window_lit", "lamp")


def evening_variant(rgb):
    """Evening = warm orange-pink light, a little darker than day; greens drift towards olive.
    Neutral greys get half the tint (the orange sun in Unity warms them anyway)."""
    tinted = _tinted(rgb, 0.08, (1.02, 0.84, 0.74), (0.045, 0.015, 0.035))
    w = 0.5 + 0.5 * min(1.0, (max(rgb) - min(rgb)) * 2.5)
    return tuple(a + (b - a) * w for a, b in zip(rgb, tinted))


def dusk_variant(rgb):
    """Dusk = blue-violet, much less saturated; lamps and lit windows stand out."""
    return _tinted(rgb, 0.35, (0.62, 0.62, 0.9), (0.03, 0.02, 0.08))


def night_variant(rgb):
    """Night = dark moonlit blue."""
    return _tinted(rgb, 0.55, (0.38, 0.42, 0.68), (0.02, 0.02, 0.06))


def _variant(fn):
    return lambda n: hex_rgb(DAY[n]) if n in _KEEP_LIT else fn(hex_rgb(DAY[n]))


# Time-of-day palettes built next to Day/Morning (same cell layout). Exported by ba_export.
EXTRA_PALETTES = {
    "T_Palette_Evening": evening_variant,
    "T_Palette_Dusk": dusk_variant,
    "T_Palette_Night": night_variant,
}


def _image(name, fn):
    img = bpy.data.images.get(name)
    if img is None:
        img = bpy.data.images.new(name, TEX, TEX, alpha=False)
    elif img.size[0] != TEX:
        img.scale(TEX, TEX)
    px = [0.0] * (TEX * TEX * 4)
    for r, row in enumerate(ROWS):
        for c, cname in enumerate(row):
            col = fn(cname)
            y0 = TEX - (r + 1) * CELL
            for y in range(y0, y0 + CELL):
                for x in range(c * CELL, (c + 1) * CELL):
                    i = (y * TEX + x) * 4
                    px[i:i + 4] = [col[0], col[1], col[2], 1.0]
    img.pixels.foreach_set(px)
    img.update()
    img.pack()
    img.use_fake_user = True
    return img


def build_palettes():
    day = _image("T_Palette_Day", lambda n: hex_rgb(DAY[n]))
    morning = _image("T_Palette_Morning", lambda n: morning_variant(hex_rgb(DAY[n])))
    emis = _image("T_Palette_Emission", lambda n: hex_rgb(DAY[n]) if n in EMISSIVE else (0, 0, 0))
    for name, fn in EXTRA_PALETTES.items():
        _image(name, _variant(fn))
    return day, morning, emis


def get_material():
    mat = bpy.data.materials.get("M_Palette")
    if mat is None:
        mat = bpy.data.materials.new("M_Palette")
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (400, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); bsdf.location = (100, 0)
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.location = (-300, 100); tex.name = "Palette"
    tex.image = bpy.data.images["T_Palette_Day"]; tex.interpolation = "Closest"
    em = nt.nodes.new("ShaderNodeTexImage"); em.location = (-300, -200); em.name = "Emission"
    em.image = bpy.data.images["T_Palette_Emission"]; em.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(em.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    bsdf.inputs["Roughness"].default_value = 0.85
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (0.8, 0.8, 0.8, 1)
    return mat


def set_palette(name="T_Palette_Day"):
    for mat_name in ("M_Palette", "M_Decals"):
        mat = bpy.data.materials.get(mat_name)
        if mat is not None and mat.node_tree and "Palette" in mat.node_tree.nodes:
            mat.node_tree.nodes["Palette"].image = bpy.data.images[name]


# ---------------------------------------------------------------- decals (inscriptions as a texture)
# Text and doodles are NOT geometry: a decal is one quad (2 triangles) with the M_Decals material.
# UV0 = palette cell (colour + time of day, like every other face), UV1 ("UVDecal") = place in the mask
# atlas T_Decals made by Tools/decal_art.py in the repo. Unity: shader Bouncer/PaletteDecal.
REPO = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer"
DECAL_JSON = REPO + "/Tools/decal_atlas.json"
DECAL_PNG = REPO + "/Assets/_Project/Art/Decals/T_Decals.png"
_decals = None


def decal_entry(name):
    global _decals
    if _decals is None:
        import json
        with open(DECAL_JSON, encoding="utf-8") as f:
            _decals = json.load(f)["entries"]
    return _decals[name]


def reload_decals():
    """Re-read the atlas layout and image after Tools/decal_art.py was run again."""
    global _decals
    _decals = None
    img = bpy.data.images.get("T_Decals")
    if img is not None:
        if img.packed_file:
            img.unpack(method="REMOVE")
        img.filepath = DECAL_PNG
        img.reload()
        img.pack()


def get_decal_material():
    """Blender preview of M_Decals: colour from the palette through UV0, mask from the atlas through UV1."""
    mat = bpy.data.materials.get("M_Decals")
    if mat is not None:
        return mat
    img = bpy.data.images.get("T_Decals")
    if img is None:
        img = bpy.data.images.load(DECAL_PNG)
        img.name = "T_Decals"
        img.colorspace_settings.name = "Non-Color"
        img.pack()
    img.use_fake_user = True
    mat = bpy.data.materials.new("M_Decals")
    try:
        mat.use_nodes = True
    except Exception:
        pass
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); out.location = (500, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); bsdf.location = (200, 0)
    uv0 = nt.nodes.new("ShaderNodeUVMap"); uv0.uv_map = "UVMap"; uv0.location = (-700, 150)
    uv1 = nt.nodes.new("ShaderNodeUVMap"); uv1.uv_map = "UVDecal"; uv1.location = (-700, -300)
    pal = nt.nodes.new("ShaderNodeTexImage"); pal.name = "Palette"; pal.location = (-400, 200)
    pal.image = bpy.data.images["T_Palette_Day"]; pal.interpolation = "Closest"
    em = nt.nodes.new("ShaderNodeTexImage"); em.name = "Emission"; em.location = (-400, -50)
    em.image = bpy.data.images["T_Palette_Emission"]; em.interpolation = "Closest"
    atlas = nt.nodes.new("ShaderNodeTexImage"); atlas.name = "Decals"; atlas.location = (-400, -300)
    atlas.image = img
    cut = nt.nodes.new("ShaderNodeMath"); cut.operation = "GREATER_THAN"; cut.location = (-100, -300)
    cut.inputs[1].default_value = 0.5
    nt.links.new(uv0.outputs["UV"], pal.inputs["Vector"])
    nt.links.new(uv0.outputs["UV"], em.inputs["Vector"])
    nt.links.new(uv1.outputs["UV"], atlas.inputs["Vector"])
    nt.links.new(pal.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(em.outputs["Color"], bsdf.inputs["Emission Color"])
    nt.links.new(atlas.outputs["Color"], cut.inputs[0])
    nt.links.new(cut.outputs["Value"], bsdf.inputs["Alpha"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    bsdf.inputs["Roughness"].default_value = 0.85
    bsdf.inputs["Specular IOR Level"].default_value = 0.2
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    try:
        mat.surface_render_method = "DITHERED"
    except Exception:
        mat.blend_method = "CLIP"
    return mat


# ---------------------------------------------------------------- helpers
def rot_z_to(d):
    d = Vector(d).normalized()
    return Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4()


def frame(origin=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    from mathutils import Euler
    s = scale if isinstance(scale, (tuple, list, Vector)) else (scale, scale, scale)
    return (Matrix.Translation(Vector(origin)) @ Euler(rot).to_matrix().to_4x4()
            @ Matrix.Diagonal((s[0], s[1], s[2], 1.0)))


class Lathe:
    """Surface of revolution around local Z. profile = [(r, z), ...] bottom -> top.
    Face centre sits at phi = -90deg (front, -Y) so painted regions are symmetric."""

    def __init__(self, profile, segs=12, matrix=None, phase=None):
        self.profile = [(float(r), float(z)) for r, z in profile]
        self.segs = segs
        self.step = 2 * math.pi / segs
        self.phase = (-math.pi / 2 - self.step / 2) if phase is None else phase
        self.m = matrix if matrix is not None else Matrix()
        # arc length + per-vertex normals for decals
        self.s = [0.0]
        segn = []
        for (r0, z0), (r1, z1) in zip(self.profile, self.profile[1:]):
            L = math.hypot(r1 - r0, z1 - z0)
            self.s.append(self.s[-1] + L)
            segn.append(((z1 - z0) / L, -(r1 - r0) / L) if L > 0 else (1, 0))
        self.vn = []
        for i in range(len(self.profile)):
            a = segn[max(0, i - 1)]
            b = segn[min(len(segn) - 1, i)]
            n = Vector((a[0] + b[0], a[1] + b[1]))
            n = n.normalized() if n.length > 1e-6 else Vector(a)
            self.vn.append((n.x, n.y))

    def at(self, s):
        s = max(0.0, min(self.s[-1], s))
        for i in range(len(self.s) - 1):
            if s <= self.s[i + 1] or i == len(self.s) - 2:
                L = self.s[i + 1] - self.s[i]
                t = 0 if L == 0 else (s - self.s[i]) / L
                (r0, z0), (r1, z1) = self.profile[i], self.profile[i + 1]
                n0, n1 = self.vn[i], self.vn[i + 1]
                return (r0 + (r1 - r0) * t, z0 + (z1 - z0) * t,
                        n0[0] + (n1[0] - n0[0]) * t, n0[1] + (n1[1] - n0[1]) * t)

    def s_at_z(self, z, from_top=False):
        idx = range(len(self.profile) - 1)
        if from_top:
            idx = reversed(list(idx))
        for i in idx:
            (r0, z0), (r1, z1) = self.profile[i], self.profile[i + 1]
            if min(z0, z1) <= z <= max(z0, z1) and z1 != z0:
                t = (z - z0) / (z1 - z0)
                return self.s[i] + (self.s[i + 1] - self.s[i]) * t
        return self.s[-1] * 0.5

    def facet(self, phi):
        rel = (phi - self.phase) % self.step - self.step / 2
        return math.cos(self.step / 2) / math.cos(rel)

    def point(self, phi, s, off=0.0):
        r, z, nr, nz = self.at(s)
        k = self.facet(phi)
        p = Vector((r * k * math.cos(phi) + off * nr * math.cos(phi),
                    r * k * math.sin(phi) + off * nr * math.sin(phi),
                    z + off * nz))
        return self.m @ p

    def radius_at(self, s):
        return max(1e-4, self.at(s)[0])


class Builder:
    """Accumulates geometry for one mesh object. Every primitive returns its faces."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    # ---- painting
    def paint(self, faces, color):
        if callable(color):
            for f in faces:
                f.normal_update()
                self._paint_face(f, color(f))
        else:
            for f in faces:
                self._paint_face(f, color)
        return faces

    def _paint_face(self, f, name):
        c = cell_uv(name)
        rad = 0.3 / GRID
        n = len(f.loops)
        for i, l in enumerate(f.loops):
            a = 2 * math.pi * i / n + 0.3
            l[self.uv].uv = (c.x + rad * math.cos(a), c.y + rad * math.sin(a))

    @staticmethod
    def faces_of(verts):
        return list({f for v in verts for f in v.link_faces})

    # ---- primitives
    def lathe(self, lathe, color, cap_bottom=True, cap_top=True):
        bm, m = self.bm, lathe.m
        rings = []
        for r, z in lathe.profile:
            if r < 1e-6:
                rings.append([bm.verts.new(m @ Vector((0, 0, z)))])
            else:
                rings.append([bm.verts.new(m @ Vector((r * math.cos(lathe.phase + lathe.step * k),
                                                        r * math.sin(lathe.phase + lathe.step * k), z)))
                              for k in range(lathe.segs)])
        faces = []
        n = lathe.segs
        for i in range(len(rings) - 1):
            A, B = rings[i], rings[i + 1]
            if len(A) == 1 and len(B) == 1:
                continue
            for k in range(n):
                k2 = (k + 1) % n
                if len(A) == 1:
                    vs = [A[0], B[k2], B[k]]
                elif len(B) == 1:
                    vs = [A[k], A[k2], B[0]]
                else:
                    vs = [A[k], A[k2], B[k2], B[k]]
                faces.append(bm.faces.new(vs))
        if cap_bottom and len(rings[0]) > 1:
            faces.append(bm.faces.new(list(reversed(rings[0]))))
        if cap_top and len(rings[-1]) > 1:
            faces.append(bm.faces.new(rings[-1]))
        if m.determinant() < 0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        self.paint(faces, color)
        return faces

    def cyl(self, p0, p1, r0, r1=None, segs=6, color="grey", caps=True, twist=0.0):
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        m = Matrix.Translation(p0) @ rot_z_to(d) @ Matrix.Rotation(twist, 4, "Z")
        L = Lathe([(r0, 0.0), (r0 if r1 is None else r1, d.length)], segs, m, phase=0.0)
        return self.lathe(L, color, caps, caps)

    def box(self, center, size, color, rot=(0, 0, 0), taper=None):
        """Axis box; taper=(sx, sy) scales the top face (for trapezoids)."""
        sx, sy, sz = [v / 2 for v in size]
        pts = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
               (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
        if taper:
            pts = pts[:4] + [(x * taper[0], y * taper[1], z) for x, y, z in pts[4:]]
        m = frame(center, rot)
        v = [self.bm.verts.new(m @ Vector(p)) for p in pts]
        idx = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        faces = [self.bm.faces.new([v[i] for i in f]) for f in idx]
        self.paint(faces, color)
        return faces

    def box_m(self, matrix, size, color):
        sx, sy, sz = [v / 2 for v in size]
        pts = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
               (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz)]
        v = [self.bm.verts.new(matrix @ Vector(p)) for p in pts]
        idx = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        faces = [self.bm.faces.new([v[i] for i in f]) for f in idx]
        self.paint(faces, color)
        return faces

    def beam(self, p0, p1, w, h, color, up=(0, 0, 1)):
        """Rectangular beam between two points (planks, rails)."""
        p0, p1 = Vector(p0), Vector(p1)
        d = (p1 - p0)
        x = d.normalized()
        upv = Vector(up)
        y = upv.cross(x)
        if y.length < 1e-6:
            y = Vector((0, 1, 0)).cross(x)
        y.normalize()
        z = x.cross(y)
        m = Matrix((x, y, z)).transposed().to_4x4()
        m.translation = (p0 + p1) / 2
        return self.box_m(m, (d.length, w, h), color)

    def prism(self, pts2d, depth, color, matrix=None):
        """Extrude a CCW 2D polygon (local XY) along local +Z by depth."""
        m = matrix if matrix is not None else Matrix()
        b = [self.bm.verts.new(m @ Vector((x, y, 0))) for x, y in pts2d]
        t = [self.bm.verts.new(m @ Vector((x, y, depth))) for x, y in pts2d]
        n = len(pts2d)
        faces = [self.bm.faces.new(list(reversed(b))), self.bm.faces.new(t)]
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new([b[i], b[j], t[j], t[i]]))
        if m.determinant() < 0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        self.paint(faces, color)
        return faces

    def sphere(self, center, radius, segs=10, rings=6, color="white", scale=(1, 1, 1), rot=(0, 0, 0)):
        prof = []
        for i in range(rings + 1):
            a = -math.pi / 2 + math.pi * i / rings
            prof.append((radius * math.cos(a) if 0 < i < rings else 0.0, radius * math.sin(a)))
        L = Lathe(prof, segs, frame(center, rot, scale))
        return self.lathe(L, color)

    def ico(self, center, radius, color, subdiv=1, scale=(1, 1, 1), jitter=0.0, seed=0):
        ret = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=radius,
                                         matrix=frame(center, (0, 0, 0), scale))
        rnd = random.Random(seed)
        if jitter:
            for v in ret["verts"]:
                v.co += Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * jitter
        faces = self.faces_of(ret["verts"])
        self.paint(faces, color)
        return faces

    def grid(self, size_x, size_y, nx, ny, color, matrix=None, height_fn=None):
        m = matrix if matrix is not None else Matrix()
        verts = []
        for j in range(ny + 1):
            row = []
            for i in range(nx + 1):
                x = -size_x / 2 + size_x * i / nx
                y = -size_y / 2 + size_y * j / ny
                z = height_fn(x, y) if height_fn else 0.0
                row.append(self.bm.verts.new(m @ Vector((x, y, z))))
            verts.append(row)
        faces = []
        for j in range(ny):
            for i in range(nx):
                faces.append(self.bm.faces.new([verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]]))
        self.paint(faces, color)
        return faces

    def quad(self, pts, color, want=None):
        """Face from points; if want (a direction) is given, winding is fixed to face it."""
        vs = [self.bm.verts.new(Vector(p)) for p in pts]
        f = self.bm.faces.new(vs)
        f.normal_update()
        if want is not None and f.normal.dot(Vector(want)) < 0:
            f.normal_flip()
        self.paint([f], color)
        return [f]

    def tube_path(self, pts, r, color, segs=6, caps=False):
        """Pipe along a polyline (one cylinder per segment + joint spheres hidden by overlap)."""
        faces = []
        for a, b in zip(pts, pts[1:]):
            faces += self.cyl(a, b, r, r, segs=segs, color=color, caps=caps)
        return faces

    # ---- decals conforming to a Lathe surface
    def decal_disc(self, L, phi, s, ru, rs=None, color="black", segs=8, off=0.004, rot=0.0, max_step=0.03):
        """Ellipse painted on a Lathe surface. Built as a polar grid so it hugs the curvature."""
        rs = ru if rs is None else rs
        r0 = L.radius_at(s)
        rings = max(1, int(math.ceil(max(ru, rs) / max_step)))
        segs = max(segs, int(math.ceil(2 * math.pi * max(ru, rs) / (max_step * 1.5))))
        c = self.bm.verts.new(L.point(phi, s, off))
        grid = []
        for k in range(1, rings + 1):
            t = k / rings
            ring = []
            for i in range(segs):
                a = 2 * math.pi * i / segs + rot
                du, ds = t * ru * math.cos(a), t * rs * math.sin(a)
                ring.append(self.bm.verts.new(L.point(phi + du / r0, s + ds, off)))
            grid.append(ring)
        faces = [self.bm.faces.new([c, grid[0][i], grid[0][(i + 1) % segs]]) for i in range(segs)]
        for k in range(rings - 1):
            A, B = grid[k], grid[k + 1]
            for i in range(segs):
                j = (i + 1) % segs
                faces.append(self.bm.faces.new([A[i], B[i], B[j], A[j]]))
        if L.m.determinant() < 0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        self.paint(faces, color)
        return faces

    def decal_strip(self, L, pts, width, color, off=0.004):
        """pts = [(phi, s)] polyline on the surface; width in meters."""
        # resample so no segment is long enough to cut through the curved surface
        dense = [pts[0]]
        for (p0, s0), (p1, s1) in zip(pts, pts[1:]):
            r = L.radius_at((s0 + s1) / 2)
            n = max(1, int(math.ceil(math.hypot((p1 - p0) * r, s1 - s0) / 0.02)))
            for k in range(1, n + 1):
                t = k / n
                dense.append((p0 + (p1 - p0) * t, s0 + (s1 - s0) * t))
        pts = dense
        uv = []
        for phi, s in pts:
            uv.append(Vector((phi * L.radius_at(s), s)))
        left, right = [], []
        n = len(pts)
        for i in range(n):
            a = uv[max(0, i - 1)]
            b = uv[min(n - 1, i + 1)]
            t = (b - a).normalized()
            perp = Vector((-t.y, t.x)) * (width / 2)
            for side, sign in ((left, 1), (right, -1)):
                q = uv[i] + perp * sign
                s = q.y
                side.append(self.bm.verts.new(L.point(q.x / L.radius_at(s), s, off)))
        faces = [self.bm.faces.new([right[i], right[i + 1], left[i + 1], left[i]]) for i in range(n - 1)]
        if L.m.determinant() < 0:
            bmesh.ops.reverse_faces(self.bm, faces=faces)
        self.paint(faces, color)
        return faces

    # ---- decals (text, doodles): a textured quad, see decal_entry / M_Decals
    def _decal_uv(self):
        layer = self.bm.loops.layers.uv.get("UVDecal")
        return layer if layer is not None else self.bm.loops.layers.uv.new("UVDecal")

    def decal(self, name, matrix, color, height=None, width=None, lift=0.003):
        """Quad showing atlas entry `name` in palette colour `color`. It lies in the local XY plane of
        `matrix` (reads along +X, up = +Y) lifted by `lift` along +Z, centred on the origin. Give height
        or width, the other follows the entry's aspect."""
        e = decal_entry(name)
        if width is None:
            width = height * e["aspect"]
        elif height is None:
            height = width / e["aspect"]
        hw, hh = width / 2, height / 2
        corners = [(-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh)]
        u0, v0, u1, v1 = e["uv"]
        uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
        if matrix.determinant() < 0:
            corners.reverse()
            uvs.reverse()
        verts = [self.bm.verts.new(matrix @ Vector((x, y, lift))) for x, y in corners]
        f = self.bm.faces.new(verts)
        self._paint_face(f, color)
        layer = self._decal_uv()
        for loop, uv in zip(f.loops, uvs):
            loop[layer].uv = uv
        f.material_index = 1
        return [f]

    # ---- output
    def to_object(self, name, collection, pivot=(0, 0, 0), parent=None, scale=1.0):
        pv = Vector(pivot) * scale
        for v in self.bm.verts:
            v.co = v.co * scale - pv
        ngons = [f for f in self.bm.faces if len(f.verts) > 4]
        if ngons:   # concave n-gons would be triangulated wrongly by importers
            bmesh.ops.triangulate(self.bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
        for f in self.bm.faces:
            f.smooth = False
        self.bm.normal_update()
        me = bpy.data.meshes.get(name)
        if me is not None:
            me.name = name + "_old"
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        me.materials.append(bpy.data.materials["M_Palette"])
        if any(p.material_index == 1 for p in me.polygons):
            me.materials.append(get_decal_material())
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        ob["pivot"] = (pv.x, pv.y, pv.z)
        if parent is not None:
            ob.parent = parent
            ob.location = pv - _root_offset(parent)
        else:
            ob.location = pv
        return ob


def _root_offset(parent):
    # children are expressed relative to the asset origin (0,0,0); parent's own pivot
    return Vector(parent.get("pivot", (0, 0, 0)))


# ---------------------------------------------------------------- scene management
def collection(name, parent=None):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(col)
    return col


def remove_tree(name):
    ob = bpy.data.objects.get(name)
    if ob is None:
        return
    todo = [ob] + list(ob.children_recursive)
    for o in todo:
        me = o.data if o.type == "MESH" else None
        bpy.data.objects.remove(o, do_unlink=True)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
    for me in list(bpy.data.meshes):
        if me.users == 0 and me.name.endswith("_old"):
            bpy.data.meshes.remove(me)


def empty_root(name, collection_, location=(0, 0, 0), size=0.3):
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_type = "ARROWS"
    ob.empty_display_size = size
    collection_.objects.link(ob)
    ob.location = location
    ob["pivot"] = (0.0, 0.0, 0.0)
    return ob


def mesh_root(builder, name, collection_, location=(0, 0, 0), pivot=(0, 0, 0)):
    ob = builder.to_object(name, collection_, pivot)
    ob["pivot"] = tuple(pivot)
    ob.location = Vector(location) + Vector(pivot)
    return ob


def stats(root):
    tris = 0
    for o in [root] + list(root.children_recursive):
        if o.type == "MESH":
            tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return tris


# ---------------------------------------------------------------- preview rendering
def setup_preview():
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.view_settings.view_transform = "Standard"
    sc.view_settings.look = "None"
    col = collection("_Preview")
    cam = bpy.data.objects.get("_PreviewCam")
    if cam is None:
        cd = bpy.data.cameras.new("_PreviewCam")
        cam = bpy.data.objects.new("_PreviewCam", cd)
        col.objects.link(cam)
    sun = bpy.data.objects.get("_PreviewSun")
    if sun is None:
        ld = bpy.data.lights.new("_PreviewSun", "SUN")
        ld.energy = 3.2
        ld.angle = math.radians(8)
        sun = bpy.data.objects.new("_PreviewSun", ld)
        col.objects.link(sun)
    sun.rotation_euler = (math.radians(50), math.radians(12), math.radians(35))
    w = sc.world or bpy.data.worlds.new("World")
    sc.world = w
    try:
        w.use_nodes = True
    except Exception:
        pass
    bg = w.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.62, 0.72, 0.85, 1)
        bg.inputs["Strength"].default_value = 0.9
    sc.camera = cam
    return cam


def render_objects(objs, path, direction=(0.8, -1.0, 0.75), res=(1200, 800), margin=1.12, ortho=True):
    sc = bpy.context.scene
    cam = setup_preview()
    meshes = []
    for o in objs:
        for x in [o] + list(o.children_recursive):
            if x.type == "MESH":
                meshes.append(x)
    bpy.context.view_layer.update()
    pts = [x.matrix_world @ Vector(c) for x in meshes for c in x.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    d = Vector(direction).normalized()
    q = (-d).to_track_quat("-Z", "Y")
    cam.rotation_euler = q.to_euler()
    radius = (hi - lo).length / 2
    cam.location = center + d * (radius * 4 + 5)
    cam.data.clip_end = radius * 10 + 100
    rot = q.to_matrix()
    right, up = rot.col[0], rot.col[1]
    xs = [(p - center).dot(right) for p in pts]
    ys = [(p - center).dot(up) for p in pts]
    aspect = res[0] / res[1]
    wx = (max(xs) - min(xs))
    wy = (max(ys) - min(ys))
    cx = (max(xs) + min(xs)) / 2
    cy = (max(ys) + min(ys)) / 2
    cam.location = cam.location + right * cx + up * cy
    cam.data.sensor_fit = "AUTO"
    if ortho:
        cam.data.type = "ORTHO"
        cam.data.ortho_scale = max(wx, wy * aspect) * margin
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    keep = set(meshes) | {cam, bpy.data.objects.get("_PreviewSun")}
    hidden = []
    for o in sc.objects:
        if o not in keep and not o.hide_render:
            o.hide_render = True
            hidden.append(o)
    sc.render.filepath = path
    try:
        bpy.ops.render.render(write_still=True)
    finally:
        for o in hidden:
            o.hide_render = False
    return path

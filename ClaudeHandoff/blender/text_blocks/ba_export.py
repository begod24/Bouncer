"""Export every asset root to FBX for Unity + palettes to PNG.
Blender: meters, Z up, front -Y  ->  Unity: Y up, front +Z, scale 1, zero rotations on every node.
FBX is written in Blender's native axes and Unity bakes the Z-up -> Y-up conversion
(ModelImporter.bakeAxisConversion, see ArtImportPostprocessor). That conversion maps Blender (x, y, z)
to Unity (x, z, y), i.e. the front would face -Z, so for the export the assets are turned 180 degrees
around the vertical axis (mesh data + child offsets, exact sign flips, undone right after).
Net mapping: Blender (x, y, z) -> Unity (-x, z, -y).
Blender's own "Apply Transform" is not used: it breaks nested hierarchies (limbs parented to a body)."""
import os
import bpy
from mathutils import Matrix, Vector

TURN = Matrix.Diagonal((-1.0, -1.0, 1.0, 1.0))   # 180 degrees around Z

REPO = "/Users/bekbolataldiyarov/Desktop/projects/Game Projects/Bouncer"
MODELS = os.path.join(REPO, "Assets/_Project/Art/Models")
PALETTES = os.path.join(REPO, "Assets/_Project/Art/Palettes")

FOLDERS = {"Enemies": "Enemies", "Balls": "Balls", "Yard": "Yard", "Pickups": "Pickups", "Shop": "Shop",
           "Rink": "Rink", "Site": "Site", "KidProps": "KidProps", "Bazaar": "Bazaar", "Kindergarten": "Kindergarten",
           "AbilityProps": "AbilityProps"}
# The kids themselves (armature + skinned mesh, collection "Kids") are exported by ba_kids.export_kids() and
# ba_kids_anim.export(): a skeleton needs its own FBX options, see ba_kids.export_rig.


def roots_of(collection_name):
    col = bpy.data.collections.get(collection_name)
    return [o for o in col.objects if o.parent is None] if col else []


def export_root(root, folder):
    os.makedirs(folder, exist_ok=True)
    objs = [root] + list(root.children_recursive)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    old = root.location.copy()
    root.location = Vector(root.get("pivot", (0.0, 0.0, 0.0)))   # asset origin to world origin
    path = os.path.join(folder, root.name + ".fbx")
    _turn(objs)
    try:
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={"EMPTY", "MESH"},
            apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
            axis_forward="Y", axis_up="Z", bake_space_transform=False,
            mesh_smooth_type="FACE", use_mesh_modifiers=True, use_triangles=False,
            use_custom_props=False, add_leaf_bones=False, bake_anim=False,
            path_mode="STRIP", embed_textures=False)
    finally:
        _turn(objs)                      # the same flip again restores the scene exactly
        root.location = old
        for o in objs:
            o.select_set(False)
    return path


def _turn(objs):
    """Turn an asset 180 degrees around Z about the world origin: mesh data and node offsets.
    All nodes only have translations, so negating x/y is exact and undone by calling it twice."""
    for o in objs:
        if o.type == "MESH":
            o.data.transform(TURN)
            o.data.update()
        o.location.x, o.location.y = -o.location.x, -o.location.y


def _write_png(img, path):
    """Exact 8-bit PNG from the image buffer (no colour management, no side effects on the .blend)."""
    import struct, zlib
    w, h = img.size
    px = list(img.pixels)
    rows = []
    for y in reversed(range(h)):            # Blender rows go bottom-up, PNG top-down
        row = bytearray([0])
        for x in range(w):
            i = (y * w + x) * 4
            row += bytes(max(0, min(255, int(round(px[i + c] * 255)))) for c in range(3))
        rows.append(bytes(row))
    raw = zlib.compress(b"".join(rows), 9)

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", raw))
        f.write(chunk(b"IEND", b""))


def export_palettes():
    os.makedirs(PALETTES, exist_ok=True)
    out = []
    import ba_lib
    for name in ("T_Palette_Day", "T_Palette_Morning", "T_Palette_Emission", *ba_lib.EXTRA_PALETTES):
        path = os.path.join(PALETTES, name + ".png")
        _write_png(bpy.data.images[name], path)
        out.append(path)
    return out


def export_all():
    written = []
    for col_name, sub in FOLDERS.items():
        for root in roots_of(col_name):
            written.append(export_root(root, os.path.join(MODELS, sub)))
    written += export_palettes()
    return written


def export_named(names):
    """Export only these asset roots. The FBX header carries a creation time, so re-exporting
    everything would change every model in git even when nothing changed."""
    names = set(names)
    written = []
    for col_name, sub in FOLDERS.items():
        for root in roots_of(col_name):
            if root.name in names:
                written.append(export_root(root, os.path.join(MODELS, sub)))
    return written

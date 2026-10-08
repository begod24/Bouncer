"""Pickups: coins of three denominations (tiyn and tenge, the in-run currency) and the school briefcase (card loot).
Sizes are the in-game sizes (exaggerated for readability from the game camera), not real ones."""
import math
from mathutils import Vector, Matrix
import ba_lib as L
from ba_lib import Builder, Lathe

# Text lies in local XY facing +Z; these turn it to face the front (-Y) or the back (+Y) of an asset.
FACE_FRONT = Matrix.Rotation(math.pi / 2, 4, "X")
FACE_BACK = Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X")


# ================================================================ coins
# name: (diameter, thickness, face, rim, relief colour, decal of the front). The back shows "coin_back".
COINS = {
    "Pickup_Coin_1": (0.26, 0.045, "gold", "ochre", "yellow", "coin_1_tiyn"),
    "Pickup_Coin_5": (0.34, 0.05, "gold", "ochre", "yellow", "coin_5_tiyn"),
    "Pickup_Coin_Tenge": (0.44, 0.06, "tin", "tin_dark", "white", "coin_1_tenge"),
}
# Earlier names of the same coins: removed from the scene on rebuild.
RETIRED = ("Pickup_Coin_Ruble",)


def build_coin(col, name, location):
    """Coin standing on its edge, faces to -Y / +Y. The relief (value, word, wheat; star on the back)
    is a decal from the atlas, the coin itself is a lathe with a raised rim."""
    L.remove_tree(name)
    d, t, face, rim, relief, front = COINS[name]
    R = d / 2
    h = t / 2
    f = h - 0.007                      # the face sits a little below the raised rim
    w = R * 0.11                       # rim width
    b = Builder()
    prof = [(0.0, -f), (R - w, -f), (R - w, -h), (R, -h * 0.75), (R, h * 0.75), (R - w, h), (R - w, f), (0.0, f)]
    coin = Lathe(prof, 20 if R > 0.2 else 16, Matrix.Rotation(math.pi / 2, 4, "X"))
    b.lathe(coin, lambda fc: face if fc.calc_center_median().length < R - w * 1.01 and abs(fc.normal.y) > 0.9 else rim)
    size = 2 * (R - w) * 0.97
    b.decal(front, Matrix.Translation((0, -f, 0)) @ FACE_FRONT, relief, height=size, lift=0.002)
    b.decal("coin_back", Matrix.Translation((0, f, 0)) @ FACE_BACK, relief, height=size, lift=0.002)
    return L.mesh_root(b, name, col, location=location, pivot=(0, 0, 0))


# ================================================================ briefcase (портфель)
def _rounded_bottom(w, h, r, n=3):
    """CCW outline (x across, y up) of a w x h rectangle standing on y = 0 with rounded bottom corners."""
    pts = []
    for k in range(n + 1):                                   # bottom-right corner
        a = -math.pi / 2 + (math.pi / 2) * k / n
        pts.append((w / 2 - r + r * math.cos(a), r + r * math.sin(a)))
    pts += [(w / 2, h), (-w / 2, h)]
    for k in range(n + 1):                                   # bottom-left corner
        a = math.pi + (math.pi / 2) * k / n
        pts.append((-w / 2 + r + r * math.cos(a), r + r * math.sin(a)))
    return pts


def build_briefcase(col, location, name="Pickup_Portfel"):
    """School briefcase ~0.66 m wide standing on its bottom edge, clasps to the front (-Y).
    Corners of gum-wrapper cards peek out from under the flap: that is what you get inside."""
    L.remove_tree(name)
    W, D, H = 0.66, 0.2, 0.48
    leather, dark, metal = "leather", "leather_dark", "tin"
    front = -D / 2
    b = Builder()
    # body: side profile (depth x height, rounded bottom) extruded across the width.
    # local x -> world y (depth), local y -> world z (height), local z -> world x (width)
    body_m = Matrix(((0, 0, 1, -W / 2), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    b.prism(_rounded_bottom(D, H, 0.06), W, lambda fc: dark if abs(fc.normal.x) > 0.7 else leather, matrix=body_m)
    for sx in (-1, 1):                                       # gusset seams on the ends
        b.box((sx * (W / 2 + 0.002), 0, H * 0.5), (0.006, D * 0.55, H * 0.8), dark)
    # flap: over the top and down the front to ~55 % of the height
    t = 0.014
    b.box((0, 0, H + t / 2), (W + 0.01, D + 0.014, t), dark)
    flap_h = H * 0.55
    flap = [(-W / 2 - 0.005, 0.0), (-W / 2 - 0.005, -flap_h + 0.05), (-W / 2 + 0.045, -flap_h),
            (W / 2 - 0.045, -flap_h), (W / 2 + 0.005, -flap_h + 0.05), (W / 2 + 0.005, 0.0)]
    flap_m = Matrix.Translation((0, front + 0.001, H + t)) @ Matrix.Rotation(math.pi / 2, 4, "X")
    b.prism(list(reversed(flap)) if _cw(flap) else flap, t, dark, matrix=flap_m)
    surface = front - t + 0.001                              # outer face of the flap
    b.box((0, surface - 0.002, H + t - flap_h + 0.014), (W - 0.1, 0.004, 0.008), "sand_dark")   # stitching
    for sx in (-1, 1):
        b.box((sx * (W / 2 - 0.014), surface - 0.002, H + t - flap_h * 0.5 + 0.02), (0.008, 0.004, flap_h - 0.08),
              "sand_dark")
    # two clasps at the bottom edge of the flap
    for sx in (-1, 1):
        cx = sx * W * 0.28
        b.box((cx, surface - 0.006, H + t - flap_h + 0.012), (0.07, 0.014, 0.055), metal)
        b.box((cx, surface - 0.014, H + t - flap_h + 0.008), (0.036, 0.004, 0.03), "tin_dark")
        b.box((cx, front - 0.006, H + t - flap_h - 0.028), (0.05, 0.014, 0.032), metal)   # lower half on the body
    # handle on two metal mounts
    top = H + t
    arch = [(-0.11, 0, top + 0.005), (-0.1, 0, top + 0.06), (-0.05, 0, top + 0.085), (0.05, 0, top + 0.085),
            (0.1, 0, top + 0.06), (0.11, 0, top + 0.005)]
    b.tube_path(arch, 0.016, dark, segs=6)
    for sx in (-1, 1):
        b.box((sx * 0.115, 0, top + 0.012), (0.05, 0.05, 0.024), metal)
    # a red star badge on the flap
    star = []
    for k in range(10):
        a = math.pi / 2 + k * math.pi / 5
        r = 0.042 if k % 2 == 0 else 0.018
        star.append((r * math.cos(a), r * math.sin(a)))
    b.prism(star, 0.008, "red", matrix=Matrix.Translation((-W * 0.3, surface + 0.001, H + t - flap_h * 0.45))
            @ Matrix.Rotation(math.pi / 2, 4, "X"))
    # corners of wrapper cards peeking out of the top edge, under the flap
    for x, tilt, colr in ((-0.16, 0.25, "sky"), (0.02, -0.12, "yellow"), (0.18, 0.3, "pink")):
        b.box((x, front + 0.012, H + 0.03), (0.11, 0.006, 0.13), colr, rot=(0, tilt, 0))
        b.box((x - 0.05 * math.sin(tilt), front + 0.008, H + 0.075), (0.06, 0.004, 0.022), "white", rot=(0, tilt, 0))
    return L.mesh_root(b, name, col, location=location, pivot=(0, 0, 0))


def _cw(pts):
    area = sum(x0 * y1 - x1 * y0 for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]))
    return area < 0


def build_all(col_parent):
    col = L.collection("Pickups", col_parent)
    for name in RETIRED:
        L.remove_tree(name)
    out = []
    x = 9.0
    for name in COINS:
        out.append(build_coin(col, name, (x, -3.0, 0.4)))
        x += 0.7
    out.append(build_briefcase(col, (x + 0.6, -3.0, 0.0)))
    return out

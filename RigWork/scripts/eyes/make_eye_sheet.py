# Expression sheet for the pawn, every expression made from the one base eye of the concept art:
# a black oval 1.72 times taller than wide, a white glint (31% of the width) 18% down from its top and a little right
# of centre. Lids cut the same oval; strokes (closed, happy, X, spiral, > <, flat) share one thickness and the eye's
# width. 4x4 cells, eyes at fixed places in every cell, transparent PNG (black shapes, white glints), plus a blurred
# height map of the shapes (for a shader bevel) and a preview on the body colour.
# blender -b -P make_eye_sheet.py -- <out dir> [shape: oval|pill]
import bpy, sys, os, math
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
OUT = a[0]
SHAPE = a[1] if len(a) > 1 else "oval"
os.makedirs(OUT, exist_ok=True)

CELL = 512
W = 92.0                 # eye width in a cell (px)
H = W * 1.72             # eye height
SPACING = 2.5 * W        # between the two eye centres
CY = CELL / 2
EYES = (CELL / 2 - SPACING / 2, CELL / 2 + SPACING / 2)   # left, right (viewer's)
T = 0.22 * W             # stroke thickness
GLINT_D = 0.31 * W
AA = 1.0

yy, xx = np.mgrid[0:CELL, 0:CELL].astype(np.float32)
# Blender image rows start at the bottom, so the row index is already y up

def ellipse(cx, cy, w, h):
    # good-enough signed distance for an ellipse (exact on the axes)
    px, py = (xx - cx) / (w / 2), (yy - cy) / (h / 2)
    r = np.sqrt(px * px + py * py)
    return (r - 1.0) * min(w, h) / 2

def pill(cx, cy, w, h):
    # rounded rectangle with full rounding on the short side
    rad = w / 2
    qx = np.abs(xx - cx) - (w / 2 - rad)
    qy = np.abs(yy - cy) - (h / 2 - rad)
    return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - rad

def base(cx, cy, k=1.0):
    return (ellipse if SHAPE == "oval" else pill)(cx, cy, W * k, H * k)

def circle(cx, cy, r):
    return np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - r

def seg(ax, ay, bx, by, t=T):
    px, py, dx, dy = xx - ax, yy - ay, bx - ax, by - ay
    h = np.clip((px * dx + py * dy) / (dx * dx + dy * dy), 0, 1)
    return np.sqrt((px - dx * h) ** 2 + (py - dy * h) ** 2) - t / 2

def arc(cx, cy, r, a0, a1, t=T):
    """Stroke along a circular arc from angle a0 to a1 (radians, a0 < a1), round ends."""
    ang = np.arctan2(yy - cy, xx - cx)
    mid, half = (a0 + a1) / 2, (a1 - a0) / 2
    d_ang = np.angle(np.exp(1j * (ang - mid)))
    on = np.abs(d_ang) <= half
    dr = np.abs(np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - r)
    e0 = np.sqrt((xx - (cx + r * math.cos(a0))) ** 2 + (yy - (cy + r * math.sin(a0))) ** 2)
    e1 = np.sqrt((xx - (cx + r * math.cos(a1))) ** 2 + (yy - (cy + r * math.sin(a1))) ** 2)
    return np.where(on, dr, np.minimum(e0, e1)) - t / 2

def lid(d, cx, cy, inner_drop, outer_drop, inner_side, k=1.0):
    """Cut the eye's top with a straight lid: drops are fractions of the eye height from its top, at the inner and
    outer edges. Returns the cut shape and the lid's height function."""
    top = cy + H * k / 2
    xi = cx + inner_side * W * k / 2
    xo = cx - inner_side * W * k / 2
    yi, yo = top - inner_drop * H * k, top - outer_drop * H * k
    # signed distance to the line through (xi, yi), (xo, yo); positive above it
    nx, ny = -(yo - yi), (xo - xi)
    if ny < 0:
        nx, ny = -nx, -ny
    L = math.hypot(nx, ny)
    above = ((xx - xi) * nx + (yy - yi) * ny) / L
    return np.maximum(d, above), (lambda x: yi + (yo - yi) * (x - xi) / (xo - xi))

def star(cx, cy, ro, ri):
    pts = []
    for i in range(10):
        r = ro if i % 2 == 0 else ri
        ang = math.pi / 2 + i * math.pi / 5
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return polygon(pts)

def polygon(pts):
    d = np.full(xx.shape, 1e9, dtype=np.float32)
    s = np.ones(xx.shape, dtype=np.float32)
    n = len(pts)
    for i in range(n):
        (ax, ay), (bx, by) = pts[i], pts[i - 1]
        ex, ey = ax - bx, ay - by
        wx, wy = xx - bx, yy - by
        h = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        d = np.minimum(d, (wx - ex * h) ** 2 + (wy - ey * h) ** 2)
        c1 = yy >= by
        c2 = yy < ay
        c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        s = np.where(flip, -s, s)
    return s * np.sqrt(d)

def heart(cx, cy, size):
    # iq's heart, the tip at the bottom; size = width
    sc = size / 1.15
    px = np.abs(xx - cx) / sc
    py = (yy - (cy - 0.55 * sc)) / sc
    d1 = np.sqrt((px - 0.25) ** 2 + (py - 0.75) ** 2) - math.sqrt(2) / 4
    m = np.maximum(px + py, 0) * 0.5
    d2 = np.sqrt(np.minimum((px) ** 2 + (py - 1.0) ** 2, (px - m) ** 2 + (py - m) ** 2)) * np.sign(px - py)
    return np.where(py + px > 1.0, d1, d2) * sc

def spiral(cx, cy, rmax, turns=2.2, t=T * 0.8):
    ang = np.arctan2(yy - cy, xx - cx)
    r = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    b = rmax / (2 * math.pi * turns)
    best = np.full(xx.shape, 1e9, dtype=np.float32)
    for k in range(int(turns) + 2):
        th = (ang % (2 * math.pi)) + 2 * math.pi * k
        rs = b * th
        valid = th <= 2 * math.pi * turns
        best = np.where(valid, np.minimum(best, np.abs(r - rs)), best)
    end = (cx + rmax * math.cos(2 * math.pi * turns), cy + rmax * math.sin(2 * math.pi * turns))
    best = np.minimum(best, np.sqrt((xx - end[0]) ** 2 + (yy - end[1]) ** 2))
    return best - t / 2

def glint(cx, cy, k=1.0, lid_y=None):
    gx, gy = cx + 0.08 * W * k, cy + H * k / 2 - 0.18 * H * k
    r = GLINT_D * k / 2
    if lid_y is not None:
        gy = min(gy, lid_y(gx) - 1.5 * r)
        if gy - r < cy - H * k / 2 + 0.12 * H * k:
            return None
    return circle(gx, gy, r)

def cell(expr):
    """(black distance, white distance) for one expression."""
    blacks, whites = [], []
    for side, cx in enumerate(EYES):
        inner = 1 if side == 0 else -1          # the inner edge faces the other eye
        if expr == "neutral" or (expr == "wink" and side == 0):
            blacks.append(base(cx, CY)); whites.append(glint(cx, CY))
        elif expr == "half":
            d, ly = lid(base(cx, CY), cx, CY, 0.5, 0.5, inner)
            blacks.append(d); whites.append(glint(cx, CY, lid_y=ly))
        elif expr == "closed":
            blacks.append(arc(cx, CY + 0.55 * W, 0.62 * W, math.radians(-145), math.radians(-35)))
        elif expr in ("happy",) or (expr == "wink" and side == 1):
            blacks.append(arc(cx, CY - 0.30 * W, 0.50 * W, math.radians(15), math.radians(165)))
        elif expr == "surprised":
            blacks.append(base(cx, CY, 1.18)); whites.append(glint(cx, CY, 1.18))
        elif expr == "angry":
            d, ly = lid(base(cx, CY), cx, CY, 0.44, 0.10, inner)
            blacks.append(d); whites.append(glint(cx, CY, lid_y=ly))
        elif expr == "sad":
            d, ly = lid(base(cx, CY), cx, CY, 0.10, 0.44, inner)
            blacks.append(d); whites.append(glint(cx, CY, lid_y=ly))
        elif expr == "determined":
            d, ly = lid(base(cx, CY), cx, CY, 0.30, 0.22, inner)
            blacks.append(d); whites.append(glint(cx, CY, lid_y=ly))
        elif expr == "ko":
            s = 0.46 * W
            blacks.append(np.minimum(seg(cx - s, CY - s, cx + s, CY + s), seg(cx - s, CY + s, cx + s, CY - s)))
        elif expr == "dizzy":
            blacks.append(spiral(cx, CY, 0.62 * W))
        elif expr == "pain":
            s = 0.46 * W
            tip = cx + inner * s * 0.8
            back = cx - inner * s * 0.8
            blacks.append(np.minimum(seg(back, CY + s, tip, CY), seg(tip, CY, back, CY - s)))
        elif expr == "stars":
            blacks.append(star(cx, CY, 0.70 * W, 0.30 * W))
        elif expr == "hearts":
            blacks.append(heart(cx, CY, 1.25 * W))
        elif expr == "sleepy":
            d, ly = lid(base(cx, CY), cx, CY, 0.62, 0.62, inner)
            blacks.append(d)
        elif expr == "flat":
            blacks.append(seg(cx - 0.55 * W, CY, cx + 0.55 * W, CY))
    db = np.minimum.reduce(blacks)
    dw = np.minimum.reduce([w for w in whites if w is not None]) if any(w is not None for w in whites) else None
    return db, dw

ORDER = ["neutral", "half", "closed", "happy",
         "surprised", "angry", "sad", "determined",
         "ko", "dizzy", "pain", "wink",
         "stars", "hearts", "sleepy", "flat"]

sheet = np.zeros((4 * CELL, 4 * CELL, 4), dtype=np.float32)
height = np.zeros((4 * CELL, 4 * CELL), dtype=np.float32)
for i, expr in enumerate(ORDER):
    r, c = divmod(i, 4)
    db, dw = cell(expr)
    alpha = np.clip(0.5 - db / AA, 0, 1)
    white = np.clip(0.5 - dw / AA, 0, 1) * alpha if dw is not None else np.zeros_like(alpha)
    rgb = np.repeat(white[..., None], 3, axis=2)
    y0 = (3 - r) * CELL                      # Blender rows start at the bottom; row 0 is the top
    sheet[y0:y0 + CELL, c * CELL:(c + 1) * CELL, :3] = rgb
    sheet[y0:y0 + CELL, c * CELL:(c + 1) * CELL, 3] = alpha
    # height: how far inside the shape (px), soft-capped: a rounded bevel ~10 px wide
    height[y0:y0 + CELL, c * CELL:(c + 1) * CELL] = np.clip(-db / 10.0, 0, 1) ** 0.6

def save(arr, name):
    h, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, h, alpha=True)
    if arr.ndim == 2:
        arr = np.dstack([arr, arr, arr, np.ones_like(arr)])
    img.pixels = arr.ravel()
    img.filepath_raw = os.path.join(OUT, name)
    img.file_format = "PNG"
    img.save()

save(sheet, "pawn_eyes_%s.png" % SHAPE)
save(height, "pawn_eyes_%s_height.png" % SHAPE)
# preview on the body colour, with grid lines between the cells
bg = np.array([0.97, 0.88, 0.77], dtype=np.float32)
prev = sheet[..., :3] * sheet[..., 3:4] + bg * (1 - sheet[..., 3:4])
for k in range(1, 4):
    prev[k * CELL - 2:k * CELL + 2, :, :] = 0.75
    prev[:, k * CELL - 2:k * CELL + 2, :] = 0.75
save(np.dstack([prev, np.ones(prev.shape[:2], dtype=np.float32)]), "pawn_eyes_%s_preview.png" % SHAPE)
print("SHEET", SHAPE, ORDER)

# Silhouette tracing from 승규's lobby asset sheet (asset_sheet.webp, 1671 x 941, 2026-11-11).
# Runs inside Blender (needs bpy for the image and numpy). 1 sheet pixel = 1 cm in the kit scene;
# sheet row 941 (bottom) is z = 0, sheet column 0 is x = 0, front faces look at -Y.
import bpy, os, math
import numpy as np

KIT = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else r"C:\Work\ChessFight\ArtSource\Lobby\KingdomInterior\Kit"
SHEET = os.path.join(KIT, "asset_sheet.webp")
SH_W, SH_H = 1671, 941
U = 0.01
BG = np.array([46.0, 34.0, 35.0])
_D = None


def diff():
    """Per-pixel distance from the dark background (row 0 = top of the sheet)."""
    global _D
    if _D is None:
        img = bpy.data.images.get("asset_sheet.webp") or bpy.data.images.load(SHEET, check_existing=True)
        w, h = img.size
        a = np.empty(w * h * 4, np.float32)
        img.pixels.foreach_get(a)
        a = a.reshape(h, w, 4)[::-1, :, :3] * 255.0
        _D = np.abs(a - BG).sum(2)
    return _D


def X(px):
    return px * U


def Z(py):
    return (SH_H - py) * U


def rows(x1, y1, x2, y2, th=150):
    """(row, left, right) of the foreground inside a box; right is exclusive."""
    m = diff()[y1:y2 + 1, x1:x2 + 1] > th
    out = []
    for i, r in enumerate(m):
        idx = np.nonzero(r)[0]
        if len(idx):
            out.append((y1 + i, x1 + int(idx[0]), x1 + int(idx[-1]) + 1))
    return out


def rdp(pts, eps):
    """Ramer-Douglas-Peucker on a 2D polyline."""
    if len(pts) < 3:
        return list(pts)
    a, b = np.array(pts[0]), np.array(pts[-1])
    ab = b - a
    n = np.hypot(*ab) or 1e-9
    best, bi = -1.0, 0
    for i in range(1, len(pts) - 1):
        p = np.array(pts[i])
        d = abs(ab[0] * (p - a)[1] - ab[1] * (p - a)[0]) / n
        if d > best:
            best, bi = d, i
    if best > eps:
        return rdp(pts[:bi + 1], eps)[:-1] + rdp(pts[bi:], eps)
    return [pts[0], pts[-1]]


def smooth(v, k=1):
    v = np.array(v, float)
    if k <= 0 or len(v) < 3:
        return v
    out = v.copy()
    for i in range(len(v)):
        lo, hi = max(0, i - k), min(len(v), i + k + 1)
        out[i] = np.median(v[lo:hi])
    return out


def half_profile(x1, y1, x2, y2, th=150, axis=None, side="both", k=1, eps=.6, flat=True):
    """Bottom-to-top [(r, z)] in metres for a part standing in the box.
    axis: sheet column of the centre line (None = mid of each row's extent when side='both').
    side: 'both' (r = half width), 'left' or 'right' (r = distance of that edge from axis)."""
    rs = rows(x1, y1, x2, y2, th)
    # drop the thin rows at both ends (the top face seen from above, anti-aliased bottom edge)
    def wid(r):
        return (r[2] - r[1]) if side == "both" else (axis - r[1] if side == "left" else r[2] - axis)
    # (at most 4 rows each end, so a narrower step at the top or bottom of a part stays)
    for _ in range(4):
        if len(rs) > 8 and wid(rs[0]) < .8 * np.median([wid(r) for r in rs[1:7]]):
            rs = rs[1:]
    for _ in range(4):
        if len(rs) > 8 and wid(rs[-1]) < .8 * np.median([wid(r) for r in rs[-7:-1]]):
            rs = rs[:-1]
    ys = [r[0] for r in rs]
    if side == "both":
        rad = [(r[2] - r[1]) / 2 for r in rs]
    elif side == "left":
        rad = [axis - r[1] for r in rs]
    else:
        rad = [r[2] - axis for r in rs]
    rad = smooth(rad, k)
    if flat:
        # the sheet looks slightly down, so a flat top shows as an ellipse that narrows upwards:
        # hold the width of the first widest row up to the top (and the same at the bottom)
        rad = np.array(rad, float)
        for order in (slice(None), slice(None, None, -1)):
            r = rad[order]
            k2 = 0
            while k2 + 1 < min(len(r), 14) and r[k2 + 1] >= r[k2] - .25:
                k2 += 1
            r[:k2] = r[k2]
            rad[order] = r
    pts = [(float(r), float(y)) for r, y in zip(rad, ys)][::-1]   # bottom -> top in sheet rows
    # close at the bottom and top edges of the silhouette
    pts = [(pts[0][0], pts[0][1] + .5)] + pts + [(pts[-1][0], pts[-1][1] - .5)]
    pts = rdp(pts, eps)
    return [(max(r, 0.0) * U, Z(y)) for r, y in pts]


def centre(x1, y1, x2, y2, th=150):
    rs = rows(x1, y1, x2, y2, th)
    return float(np.median([(r[1] + r[2]) / 2 for r in rs]))


def filled(x1, y1, x2, y2, th=150):
    """Foreground of the box with its holes filled: background is what floods in from the border,
    so gaps between crown points or a knight's ears stay open."""
    from collections import deque
    m = diff()[y1:y2 + 1, x1:x2 + 1] > th
    h, w = m.shape
    out = np.zeros_like(m)
    q = deque()
    for i in range(h):
        for j in (0, w - 1):
            if not m[i, j] and not out[i, j]:
                out[i, j] = True
                q.append((i, j))
    for j in range(w):
        for i in (0, h - 1):
            if not m[i, j] and not out[i, j]:
                out[i, j] = True
                q.append((i, j))
    while q:
        i, j = q.popleft()
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            a, b = i + di, j + dj
            if 0 <= a < h and 0 <= b < w and not m[a, b] and not out[a, b]:
                out[a, b] = True
                q.append((a, b))
    return ~out


def outline(x1, y1, x2, y2, th=150, eps=.8):
    """Outer contour of the largest blob in the box (sheet pixels), counter-clockwise, simplified.
    Marching along the boundary of the mask with a square tracing walk."""
    m = filled(x1, y1, x2, y2, th)
    h, w = m.shape
    # Moore-neighbour tracing from the top-left foreground pixel
    ys, xs = np.nonzero(m)
    start = (ys[0], xs[0])
    dirs = [(-1, 0), (-1, 1), (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1)]
    cur, back = start, 6
    pts = [start]
    for _ in range(200000):
        found = False
        for i in range(8):
            d = (back + 1 + i) % 8
            ny, nx = cur[0] + dirs[d][0], cur[1] + dirs[d][1]
            if 0 <= ny < h and 0 <= nx < w and m[ny, nx]:
                back = (d + 4) % 8
                cur = (ny, nx)
                found = True
                break
        if not found or cur == start:
            break
        pts.append(cur)
    pts = [(x1 + p[1] + .5, y1 + p[0] + .5) for p in pts]
    # a closed loop: split it at the point farthest from the start, simplify both halves
    p0 = np.array(pts[0])
    k = int(np.argmax([np.hypot(*(np.array(p) - p0)) for p in pts]))
    a = rdp(pts[:k + 1], eps)
    b = rdp(pts[k:] + [pts[0]], eps)
    return a[:-1] + b[:-1]

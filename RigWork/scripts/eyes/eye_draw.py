# The eye drawings for the expression atlas, as signed distance fields in "sticker" units: one eye's own tangent
# plane on the head, measured in eye widths (W = 1), u to the right and v up as seen from the front, eye centre at 0.
# Pure numpy (no bpy), shared by make_eye_atlas.py and the render scripts.
#
# Atlas layout: 4 x 4 cells (row 0 at the top), each cell two halves side by side: the left half is the eye on the
# viewer's left (the piece's right eye), the right half the other eye. A half covers u in [-HU, HU], v in [-HV, HV].
import math
import numpy as np

HU, HV = 0.87, 1.74            # half-cell extent in eye widths (a half is 1.74 W x 3.48 W)

# The design-matched base eyes, per piece style (measured on the design sheets, sizes in eye widths):
#   aspect  eye height / width in the sticker plane
#   glint   centre (u, v) from the eye centre and diameter; ring = darker ring around it (white team only)
STYLES = {
    # pawn: an upright ellipse (calibrated on renders against white_01_pawn_front, see RigWork/README.md)
    "oval": dict(aspect=1.562, glint=(0.03, 0.472, 0.31), glint_aspect=1.06, ring=0.035, bevel=0.03, glint_rise=0.6),
    # rook: a rounded rectangle. Sticker values, calibrated by rendering on RigWork/pieces/models/Rook_LowPoly.blend
    # against white_09_rook_front (projected there: h/w 1.912, corner 0.41 W, glint +0.143 W, +0.288 H, 0.326 W)
    "rect": dict(aspect=1.756, corner=0.367, glint=(0.143, 0.503, 0.317), glint_aspect=1.0, ring=0.035, bevel=0.03,
                 glint_rise=0.6),
    # bishop: a tall diamond with a white dot just above its middle. Sticker values, calibrated by rendering on
    # RigWork/pieces/models/Bishop_LowPoly.blend against white_13_bishop_front (projected there: h/w 1.98, dot +0.008 W,
    # +0.044 H, 0.264 W, in the diamond's middle on both eyes)
    "diamond": dict(aspect=1.905, corner=0.02, glint=(0.008, 0.106, 0.25), glint_inner=0.011, glint_aspect=1.0,
                    ring=0.035, bevel=0.03, glint_rise=0.6),
}

ORDER = ["neutral", "half", "closed", "happy",
         "surprised", "angry", "sad", "determined",
         "ko", "dizzy", "pain", "wink",
         "stars", "hearts", "sleepy", "flat"]


class Grid:
    def __init__(self, px_w=256, px_h=512):
        self.px_w, self.px_h = px_w, px_h
        self.ppw = px_w / (2 * HU)                       # pixels per eye width
        cols = (np.arange(px_w, dtype=np.float32) + 0.5) / self.ppw - HU
        rows = HV - (np.arange(px_h, dtype=np.float32) + 0.5) / self.ppw      # row 0 = top
        self.X, self.Y = np.meshgrid(cols, rows)


# ---------------------------------------------------------------- distance primitives (negative inside)
def ellipse(g, cx, cy, w, h):
    """Signed distance (approximate off the axes, exact on them) to an axis-aligned ellipse w x h."""
    px, py = (g.X - cx) / (w / 2), (g.Y - cy) / (h / 2)
    r = np.sqrt(px * px + py * py)
    # first-order correction: divide by the gradient's length so the distance is right near the curve
    gx, gy = px / (w / 2), py / (h / 2)
    gl = np.sqrt(gx * gx + gy * gy) / np.maximum(r, 1e-6)
    return (r - 1.0) / np.maximum(gl, 1e-6)


def superellipse(g, cx, cy, w, h, upper=(2.0, 2.0), lower=(2.0, 2.0)):
    """Signed distance (first order, right near the curve) to |x/a|^p + |y/b|^q = 1 with a = w/2, b = h/2; the upper
    and lower halves have their own (p, q): p flattens the top or bottom, q the sides. (2, 2) is the ellipse."""
    a_, b_ = w / 2, h / 2
    X, Y = (g.X - cx) / a_, (g.Y - cy) / b_
    up = Y >= 0
    p = np.where(up, upper[0], lower[0]).astype(np.float32)
    q = np.where(up, upper[1], lower[1]).astype(np.float32)
    ax, ay = np.maximum(np.abs(X), 1e-6), np.maximum(np.abs(Y), 1e-6)
    G = ax ** p + ay ** q - 1
    gx = p * ax ** (p - 1) / a_
    gy = q * ay ** (q - 1) / b_
    return G / np.maximum(np.sqrt(gx * gx + gy * gy), 1e-6)


def circle(g, cx, cy, r):
    return np.sqrt((g.X - cx) ** 2 + (g.Y - cy) ** 2) - r


def box(g, cx, cy, w, h, rad):
    """Rounded rectangle w x h with corner radius rad."""
    qx = np.abs(g.X - cx) - (w / 2 - rad)
    qy = np.abs(g.Y - cy) - (h / 2 - rad)
    return np.sqrt(np.maximum(qx, 0) ** 2 + np.maximum(qy, 0) ** 2) + np.minimum(np.maximum(qx, qy), 0) - rad


def seg(g, ax, ay, bx, by, t):
    px, py, dx, dy = g.X - ax, g.Y - ay, bx - ax, by - ay
    h = np.clip((px * dx + py * dy) / (dx * dx + dy * dy), 0, 1)
    return np.sqrt((px - dx * h) ** 2 + (py - dy * h) ** 2) - t / 2


def arc(g, cx, cy, r, a0, a1, t):
    """Stroke along a circular arc from angle a0 to a1 (radians, a0 < a1), round ends."""
    ang = np.arctan2(g.Y - cy, g.X - cx)
    mid, half = (a0 + a1) / 2, (a1 - a0) / 2
    d_ang = np.angle(np.exp(1j * (ang - mid)))
    on = np.abs(d_ang) <= half
    dr = np.abs(np.sqrt((g.X - cx) ** 2 + (g.Y - cy) ** 2) - r)
    e0 = np.sqrt((g.X - (cx + r * math.cos(a0))) ** 2 + (g.Y - (cy + r * math.sin(a0))) ** 2)
    e1 = np.sqrt((g.X - (cx + r * math.cos(a1))) ** 2 + (g.Y - (cy + r * math.sin(a1))) ** 2)
    return np.where(on, dr, np.minimum(e0, e1)) - t / 2


def polygon(g, pts):
    d = np.full(g.X.shape, 1e9, dtype=np.float32)
    s = np.ones(g.X.shape, dtype=np.float32)
    n = len(pts)
    for i in range(n):
        (ax, ay), (bx, by) = pts[i], pts[i - 1]
        ex, ey = ax - bx, ay - by
        wx, wy = g.X - bx, g.Y - by
        h = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        d = np.minimum(d, (wx - ex * h) ** 2 + (wy - ey * h) ** 2)
        c1 = g.Y >= by
        c2 = g.Y < ay
        c3 = ex * wy > ey * wx
        flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
        s = np.where(flip, -s, s)
    return s * np.sqrt(d)


def rounded_polygon(g, pts, rad):
    """A polygon with its corners rounded by rad (the polygon is shrunk by rad, then grown back)."""
    cx = sum(p[0] for p in pts) / len(pts); cy = sum(p[1] for p in pts) / len(pts)
    # shrink towards the centroid along each vertex's bisector: good enough for convex shapes
    inner = []
    n = len(pts)
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        e1 = (p1[0] - p0[0], p1[1] - p0[1]); e2 = (p2[0] - p1[0], p2[1] - p1[1])
        n1 = _unit((e1[1], -e1[0])); n2 = _unit((e2[1], -e2[0]))
        # outward normals point away from the centroid
        if (p1[0] - cx) * n1[0] + (p1[1] - cy) * n1[1] < 0: n1 = (-n1[0], -n1[1])
        if (p1[0] - cx) * n2[0] + (p1[1] - cy) * n2[1] < 0: n2 = (-n2[0], -n2[1])
        b = _unit((n1[0] + n2[0], n1[1] + n2[1]))
        cosh = b[0] * n1[0] + b[1] * n1[1]
        k = rad / max(cosh, 1e-3)
        inner.append((p1[0] - b[0] * k, p1[1] - b[1] * k))
    return polygon(g, inner) - rad


def _unit(v):
    l = math.hypot(*v)
    return (v[0] / l, v[1] / l)


def star(g, cx, cy, ro, ri, rad=0.0):
    pts = []
    for i in range(10):
        r = ro if i % 2 == 0 else ri
        ang = math.pi / 2 + i * math.pi / 5
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    return polygon(g, pts) if rad <= 0 else polygon(g, pts)


def heart(g, cx, cy, size):
    """Inigo Quilez's heart, tip down; size = its width."""
    sc = size / 1.15
    px = np.abs(g.X - cx) / sc
    py = (g.Y - (cy - 0.55 * sc)) / sc
    d1 = np.sqrt((px - 0.25) ** 2 + (py - 0.75) ** 2) - math.sqrt(2) / 4
    m = np.maximum(px + py, 0) * 0.5
    d2 = np.sqrt(np.minimum(px ** 2 + (py - 1.0) ** 2, (px - m) ** 2 + (py - m) ** 2)) * np.sign(px - py)
    return np.where(py + px > 1.0, d1, d2) * sc


def spiral(g, cx, cy, rmax, turns, t, r0_frac=0.12):
    """An Archimedean spiral from radius r0 out to rmax (round ends)."""
    ang = np.arctan2(g.Y - cy, g.X - cx)
    r = np.sqrt((g.X - cx) ** 2 + (g.Y - cy) ** 2)
    r0 = r0_frac * rmax
    th_max = 2 * math.pi * turns
    b = (rmax - r0) / th_max
    best = np.full(g.X.shape, 1e9, dtype=np.float32)
    for k in range(int(turns) + 2):
        th = (ang % (2 * math.pi)) + 2 * math.pi * k
        rs = r0 + b * th
        valid = th <= th_max
        best = np.where(valid, np.minimum(best, np.abs(r - rs)), best)
    for th in (0.0, th_max):                             # round caps at both ends
        e = (cx + (r0 + b * th) * math.cos(th), cy + (r0 + b * th) * math.sin(th))
        best = np.minimum(best, np.sqrt((g.X - e[0]) ** 2 + (g.Y - e[1]) ** 2))
    return best - t / 2


# ---------------------------------------------------------------- the base eye of each style
def base_shape(g, style, k=1.0, cx=0.0, cy=0.0):
    s = STYLES[style]
    w, h = k, k * s["aspect"]
    if style == "oval":
        if "shape" in s:                                 # the design's outline: flatter than an ellipse
            return superellipse(g, cx, cy, w, h, s["shape"][:2], s["shape"][2:])
        return ellipse(g, cx, cy, w, h)
    if style == "rect":
        return box(g, cx, cy, w, h, s["corner"] * k)
    if style == "diamond":
        return rounded_polygon(g, [(cx, cy + h / 2), (cx + w / 2, cy), (cx, cy - h / 2), (cx - w / 2, cy)], s["corner"] * k)
    raise ValueError(style)


def glint_shape(g, style, k=1.0, cx=0.0, cy=0.0, lid=None, inner=0):
    """The glint (and its ring distance), or None when a lid hides it. glint_inner moves it towards the nose (the
    bishop's dot sits in the middle of the drawn diamond, which the head's curve pushes outwards)."""
    s = STYLES[style]
    gu, gv, gd = s["glint"]
    gx, gy = cx + (gu + inner * s.get("glint_inner", 0.0)) * k, cy + gv * k
    r = gd * k / 2
    if lid is not None:
        top = lid(gx) - 0.04 * k                         # keep it under the lid
        if gy + r > top:
            gy = top - r
        if gy < cy - 0.25 * k * s["aspect"]:              # no room left above the eye's lower quarter
            return None
    return ellipse(g, gx, gy, 2 * r, 2 * r * s.get("glint_aspect", 1.0))


def lid_cut(g, d, style, inner_side, inner_drop, outer_drop, k=1.0, cx=0.0, cy=0.0):
    """Cut the eye's top with a straight lid; drops are fractions of the eye's height from its top, at the inner and
    outer edges. inner_side = +1 when the inner edge (towards the nose) is at +u. Returns the cut distance and the
    lid's height as a function of u."""
    s = STYLES[style]
    w, h = k, k * s["aspect"]
    top = cy + h / 2
    xi, xo = cx + inner_side * w / 2, cx - inner_side * w / 2
    yi, yo = top - inner_drop * h, top - outer_drop * h
    nx, ny = -(yo - yi), (xo - xi)
    if ny < 0:
        nx, ny = -nx, -ny
    L = math.hypot(nx, ny)
    above = ((g.X - xi) * nx + (g.Y - yi) * ny) / L
    return np.maximum(d, above), (lambda x: yi + (yo - yi) * (x - xi) / (xo - xi))


# ---------------------------------------------------------------- expressions
def expression(g, expr, side, style, team):
    """(body distance, glint distance or None) for one eye. side 0 = the viewer's left eye (its inner edge at +u)."""
    s = STYLES[style]
    inner = 1 if side == 0 else -1
    A = s["aspect"]
    T = 0.24                                              # stroke thickness for line expressions
    glint = team == "white"
    def with_glint(d, k=1.0, lid=None):
        return d, (glint_shape(g, style, k, lid=lid, inner=inner) if glint else None)
    if expr == "neutral" or (expr == "wink" and side == 0):
        return with_glint(base_shape(g, style))
    if expr == "half":
        d, ly = lid_cut(g, base_shape(g, style), style, inner, 0.5, 0.5)
        return with_glint(d, lid=ly)
    if expr == "closed":                                   # a gentle downward arc (a closed lid), at the eye's middle
        return arc(g, 0.0, 0.49, 0.62, math.radians(-145), math.radians(-35), T), None
    if expr == "happy" or (expr == "wink" and side == 1):  # ^ ^, at the eye's middle
        return arc(g, 0.0, -0.33, 0.52, math.radians(15), math.radians(165), T), None
    if expr == "surprised":
        return with_glint(base_shape(g, style, 1.18), 1.18)
    if expr == "angry":
        d, ly = lid_cut(g, base_shape(g, style), style, inner, 0.44, 0.10)
        return with_glint(d, lid=ly)
    if expr == "sad":
        d, ly = lid_cut(g, base_shape(g, style), style, inner, 0.10, 0.44)
        return with_glint(d, lid=ly)
    if expr == "determined":
        d, ly = lid_cut(g, base_shape(g, style), style, inner, 0.30, 0.22)
        return with_glint(d, lid=ly)
    if expr == "ko":
        q = 0.46
        return np.minimum(seg(g, -q, -q, q, q, T), seg(g, -q, q, q, -q, T)), None
    if expr == "dizzy":
        return spiral(g, 0.0, 0.0, 0.62, 2.2, T * 0.8), None
    if expr == "pain":                                     # > <, the point towards the nose
        q = 0.46
        tip, back = inner * q * 0.8, -inner * q * 0.8
        return np.minimum(seg(g, back, q, tip, 0.0, T), seg(g, tip, 0.0, back, -q, T)), None
    if expr == "stars":
        return star(g, 0.0, 0.0, 0.70, 0.30), None
    if expr == "hearts":
        return heart(g, 0.0, 0.0, 1.25), None
    if expr == "sleepy":
        d, ly = lid_cut(g, base_shape(g, style), style, inner, 0.62, 0.62)
        return d, None
    if expr == "flat":
        return seg(g, -0.55, 0.0, 0.55, 0.0, T), None
    raise ValueError(expr)


# ---------------------------------------------------------------- colours and the height (relief) map
TEAM_COLOURS = {
    # sRGB albedo: eye, glint, glint ring
    "white": dict(eye=(0.045, 0.042, 0.038), glint=(0.97, 0.97, 0.97), ring=(0.008, 0.008, 0.008)),
    "black": dict(eye=(0.97, 0.90, 0.76), glint=None, ring=None),
}


# per team and style, where a piece's own sheet has a different eye colour (black_09_rook / black_13_bishop ivory is
# (0.996, 0.941, 0.82) and (0.988, 0.949, 0.82) against the pawn's (0.992, 0.918, 0.765))
TEAM_COLOURS_STYLE = {("black", "rect"): dict(eye=(0.97, 0.92, 0.80)), ("black", "diamond"): dict(eye=(0.97, 0.92, 0.80)),
                      ("white", "rect"): dict(eye=(0.03, 0.03, 0.03)), ("white", "diamond"): dict(eye=(0.02, 0.02, 0.02))}


def coverage(d, ppw, aa=1.0):
    """Anti-aliased coverage from a distance in eye widths."""
    return np.clip(0.5 - d * ppw / aa, 0, 1)


# Per team, per style: the whole drawing scaled about the eye's centre and moved down (eye widths), when a team's
# design sheet draws the eyes a little differently (the black pawn's eyes are 4.5% smaller and 0.06 W lower).
TEAM_FIT = {("black", "oval"): (0.954, 0.11), ("black", "rect"): (0.97, 0.0)}
# Per team, per style: shape values that differ on the team's own sheet (black_09_rook_front: h/w 1.955, corner 0.455 W)
TEAM_STYLE = {("black", "rect"): dict(aspect=1.795, corner=0.407),     # projected 1.955 and 0.455 W
              # white pawn (2026-11-10, user: "sides flatter, a touch smaller, a touch lower"): the design's outline
              # is flatter than an ellipse (two-half superellipse fitted to white_01_pawn_front), drawn for the
              # front-leaning projection (pawn_face.py eye "front": 0.9). The black pawn keeps the plain oval.
              ("white", "oval"): dict(shape=(1.97, 2.42, 2.30, 1.97), aspect=1.634, glint=(0.03, 0.490, 0.318))}


class _Moved:
    def __init__(self, g, k, dy):
        self.X, self.Y, self.ppw = g.X / k, (g.Y + dy) / k, g.ppw * k


def half_cell(g, expr, side, style, team, bevel=None, glint_rise=None):
    """RGBA (sRGB, straight alpha) and height (0..1) of one eye in its half-cell."""
    col = dict(TEAM_COLOURS[team], **TEAM_COLOURS_STYLE.get((team, style), {}))
    bevel = STYLES[style].get("bevel", 0.10) if bevel is None else bevel
    glint_rise = STYLES[style].get("glint_rise", 0.6) if glint_rise is None else glint_rise
    k, dy = TEAM_FIT.get((team, style), (1.0, 0.0))
    gm = _Moved(g, k, dy) if (k, dy) != (1.0, 0.0) else g
    db, dg = expression(gm, expr, side, style, team)
    if gm is not g:
        db = db * k
        dg = dg * k if dg is not None else None
    al = coverage(db, g.ppw)
    rgb = np.empty(db.shape + (3,), dtype=np.float32)
    rgb[:] = col["eye"]
    # height: a rounded bevel 'bevel' eye widths wide (quarter circle), flat on top
    t = np.clip(-db / bevel, 0, 1)
    height = np.sqrt(1 - (1 - t) ** 2)
    if dg is not None:
        ring_w = STYLES[style]["ring"]
        ring = coverage(dg - ring_w, g.ppw) * al
        rgb = rgb * (1 - ring[..., None]) + np.array(col["ring"], dtype=np.float32) * ring[..., None]
        gw = coverage(dg, g.ppw) * al
        rgb = rgb * (1 - gw[..., None]) + np.array(col["glint"], dtype=np.float32) * gw[..., None]
        # the glint stands a little proud of the eye, rounded
        gt = np.clip(-dg / 0.06, 0, 1)
        height = height + glint_rise * 0.25 * np.sqrt(1 - (1 - gt) ** 2)
    return np.dstack([rgb, al]).astype(np.float32), (height * (al > 0)).astype(np.float32)


def atlas(style, team, order=ORDER, px_w=256, px_h=512):
    """The 4 x 4 atlas (rows from the top): RGBA and height."""
    STYLES[style].update(TEAM_STYLE.get((team, style), {}))
    g = Grid(px_w, px_h)
    n = 4
    rgba = np.zeros((n * px_h, n * 2 * px_w, 4), dtype=np.float32)
    hgt = np.zeros((n * px_h, n * 2 * px_w), dtype=np.float32)
    for i, expr in enumerate(order):
        r, c = divmod(i, n)
        for side in (0, 1):
            col, hh = half_cell(g, expr, side, style, team)
            x0 = c * 2 * px_w + side * px_w
            rgba[r * px_h:(r + 1) * px_h, x0:x0 + px_w] = col
            hgt[r * px_h:(r + 1) * px_h, x0:x0 + px_w] = hh
    return rgba, hgt

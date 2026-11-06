# The boat crossing's royal boat (R446), modelled here after 승규's reference photo (a royal blue boat with a striped
# canopy on gold pillars, purple and cream furniture inside), without the canopy and the pillars (승규 11-05: "에셋
# 사용하지 말고 너가 모델링 해서 텍스쳐 입혀", "기둥 지붕 지워주고", "모델링을 완전 사진이랑 똑같이 하라니까 내부가 지금
# 아예 없잖아"). The photo's proportions were measured (its sheer, stems, plan and heights; no mesh of it is used):
#   the hull     deep and round ended, both stems curving forward, the gunwale rising to both ends (most at the bow):
#                royal blue planks with sunk seams, a raised gold bead, a cream bottom band; a broad gold cap all round
#   the ends     a big gold scroll curling back over the bow, a smaller one on the stern; a raised deck at each end with
#                a gold lantern on a column (glowing panes)
#   the sides    a gold crown on each bow quarter; two blue and white fenders a side on twisted ropes over the cap
#   inside       the hull's wooden inside down to a planked floor at the deck (3.8, the walking surface); along the far
#                side a purple buttoned sofa between two purple settees, gold framed, purple cushions; two cream buttoned
#                armchairs, gold framed, before the settees; all facing the near (quay) side
# Its sizes: 6.1 m stem to stem (6.4 with the bow scroll), 2.1 m over the cap, 2.38 m with the fenders (the tunnels'
# lanes are 2.4); the cap 4.16 midships and 4.5 at the bow, the furniture's backs 4.6, the lanterns 5.3 (the tunnels'
# eave is at 4.4: R446). The bottom (2.85) is 0.15 m in the reservoir (3.0).
import math

import bmesh
from mathutils import Matrix, Vector

from gc_kit import GOLD, M, Mesh, material, dome


def _mat(key, name, rgb, rough=.6, metal=0., emit=0.):
    if key not in M:
        M[key] = material(name, rgb, rough, metal, emit)
    return M[key]


BLUE = _mat("boat_blue", "GC | boat blue", (.13, .3, .74), .45)
SEAM = _mat("boat_seam", "GC | boat blue seam", (.07, .18, .5), .6)
CREAM = _mat("boat_cream", "GC | boat cream", (.94, .88, .76), .5)
WOOD = M["wood"]
SHADE = _mat("boat_floor_shade", "GC | boat floor shade", (.2, .12, .07), .85)
PURPLE = _mat("velvet", "GC | velvet purple", (.42, .24, .68), .75)
PURPLE_LIGHT = _mat("velvet_light", "GC | velvet purple light", (.54, .33, .8), .7)
UPHOLSTERY = _mat("upholstery", "GC | cream upholstery", (.93, .87, .77), .6)
FENDER = _mat("fender", "GC | fender blue", (.12, .27, .72), .35)
WHITE = M["white"]
ROPE = _mat("rope", "GC | rope", (.93, .88, .76), .8)
GLOW = _mat("glow", "GC | lantern glow", (1.0, .84, .55), .3, 0, 4)

# ------------------------------------------------------------------ the hull's form (boat frame: x along, bow at -x)
FLOOR = 3.812                       # the planks' top (the graybox deck, the walking surface, is 3.8)
KEEL = 2.85                         # the flat bottom
SHEER_MID, BOW_RISE, STERN_RISE = 4.16, .38, .22
BOW_END, STERN_END = 3.12, 3.0      # the stems' tops
BEAM = 1.03                         # half width at the gunwale
SUPER = 2.3                         # the plan's superellipse
WALL = .1
N = 72                              # points round the hull (both stems on one)
THETA = [2 * math.pi * i / N for i in range(N)]
X_FORE, X_AFT = -2.3, 2.42          # the raised end decks
Z_FORE, Z_AFT = 4.2, 4.18
SEAMS = [.3 + .1286 * k for k in range(1, 5)]          # five planks from the gold bead to the cap


def spow(v, e):
    return math.copysign(abs(v) ** e, v)


def sheer(x):
    if x < 0:
        return SHEER_MID + BOW_RISE * min(1., -x / BOW_END) ** 3
    return SHEER_MID + STERN_RISE * min(1., x / STERN_END) ** 2.4


def stems(h):
    """How far the bow and the stern reach at level h (0 the bottom, 1 the gunwale): the stems curve forward."""
    return 2.48 + .64 * (1 - (1 - h) ** 3), 2.31 + .69 * (1 - (1 - h) ** 2.5)


def breadth(h):
    return BEAM * (.5 + .5 * max(0., 1 - (1 - h) ** 3) ** .5)


def plan(th, h):
    c, s = math.cos(th), math.sin(th)
    bow, stern = stems(h)
    e = 2 / SUPER
    return spow(c, e) * (stern if c >= 0 else bow), spow(s, e) * breadth(h)


def hull_xyz(th, h):
    x, y = plan(th, h)
    return Vector((x, y, KEEL + (sheer(x) - KEEL) * h))


def plan_normal(th, h, d=1e-4):
    a, b = plan(th - d, h), plan(th + d, h)
    n = Vector((b[1] - a[1], -(b[0] - a[0])))
    return n.normalized()


def h_at(th, z):
    lo, hi = 0., 1.
    for _ in range(36):
        mid = (lo + hi) / 2
        if hull_xyz(th, mid).z < z:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def surface_at(x, z, side):
    """The hull's outside at (x, z) on side +1 (y > 0) or -1, and its normal."""
    lo, hi = (0., math.pi) if side > 0 else (math.pi, 2 * math.pi)
    for _ in range(40):
        mid = (lo + hi) / 2
        xm = hull_xyz(mid, h_at(mid, z)).x
        if (xm > x) == (side > 0):
            lo = mid
        else:
            hi = mid
    th = (lo + hi) / 2
    h = h_at(th, z)
    d = 1e-4
    n = (hull_xyz(th + d, h) - hull_xyz(th - d, h)).cross(hull_xyz(th, h + d) - hull_xyz(th, h - d)).normalized()
    if n.y * side < 0:
        n = -n
    return hull_xyz(th, h), n, (hull_xyz(th, h + d) - hull_xyz(th, h - d)).normalized()


# ------------------------------------------------------------------ helpers
def _frame(at, z_axis, up=Vector((0, 0, 1))):
    z = Vector(z_axis).normalized()
    x = Vector(up).cross(z)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    y = z.cross(x)
    return Matrix.Translation(Vector(at)) @ Matrix((x, y, z)).transposed().to_4x4()


def _one_sided(m, faces, outward):
    """Faces of an open surface kept facing out of (or into) the boat; done() leaves them so."""
    score = 0.
    for f in faces:
        f.normal_update()
        c = f.calc_center_median()
        score += f.normal.dot(Vector((c.x - max(-1.5, min(1.5, c.x)), c.y, 0)))
    if (score < 0) == outward:
        for f in faces:
            f.normal_flip()
    m.no_recalc.update(faces)


def _faces(m, faces, mat, smooth=True):
    i = m.mi(mat)
    for f in faces:
        f.material_index = i
        f.smooth = smooth


def frames_along(pts):
    """A rotation-minimising (tangent, normal, binormal) frame at each point of a path."""
    out, n = [], None
    for i, p in enumerate(pts):
        t = (pts[min(len(pts) - 1, i + 1)] - pts[max(0, i - 1)]).normalized()
        if n is None:
            ref = Vector((0, 0, 1)) if abs(t.z) < .9 else Vector((0, 1, 0))
            n = (ref - t * ref.dot(t)).normalized()
        else:
            n = (n - t * n.dot(t)).normalized()
        out.append((t, n, t.cross(n)))
    return out


def tube(m, pts, radii, mat, segs=8):
    """A closed tube along pts."""
    rings = []
    for p, r, (t, n, b) in zip(pts, radii, frames_along(pts)):
        rings.append([m.bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * r)
                      for a in (2 * math.pi * k / segs for k in range(segs))])
    faces = [m.bm.faces.new((ra[k], ra[(k + 1) % segs], rb[(k + 1) % segs], rb[k]))
             for ra, rb in zip(rings, rings[1:]) for k in range(segs)]
    _faces(m, faces, mat)
    _faces(m, [m.bm.faces.new(rings[0]), m.bm.faces.new(rings[-1])], mat, False)


def sweep(m, path, profile, mat, closed=False, up=Vector((0, 0, 1)), smooth=True):
    """A profile (s across, v up; or a function of the point's index giving one) swept along path."""
    n = len(path)
    rings = []
    for i, p in enumerate(path):
        a = path[(i - 1) % n] if (closed or i > 0) else p
        b = path[(i + 1) % n] if (closed or i < n - 1) else p
        t = (b - a).normalized()
        side = t.cross(up)
        if side.length < 1e-6:
            side = t.orthogonal()
        side.normalize()
        upv = side.cross(t).normalized()
        prof = profile(i) if callable(profile) else profile
        rings.append([m.bm.verts.new(p + side * s + upv * v) for s, v in prof])
    k = len(rings[0])
    faces = []
    for i in range(n if closed else n - 1):
        ra, rb = rings[i], rings[(i + 1) % n]
        faces += [m.bm.faces.new((ra[j], ra[(j + 1) % k], rb[(j + 1) % k], rb[j])) for j in range(k)]
    _faces(m, faces, mat, smooth)
    if not closed:
        _faces(m, [m.bm.faces.new(list(reversed(rings[0]))), m.bm.faces.new(rings[-1])], mat, False)


def spline(ctrl, step):
    pts = []
    c = [ctrl[0]] + list(ctrl) + [ctrl[-1]]
    for i in range(1, len(c) - 2):
        p0, p1, p2, p3 = c[i - 1], c[i], c[i + 1], c[i + 2]
        k = max(2, int((p2 - p1).length / step))
        for j in range(k):
            t = j / k
            pts.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                             + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    pts.append(c[-2])
    return pts


def chaikin(pts, times=2):
    for _ in range(times):
        out = [pts[0]]
        for a, b in zip(pts, pts[1:]):
            out += [a.lerp(b, .25), a.lerp(b, .75)]
        out.append(pts[-1])
        pts = out
    return pts


def clip(poly, axis, value, keep_greater):
    out = []
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        ina = a[axis] >= value if keep_greater else a[axis] <= value
        inb = b[axis] >= value if keep_greater else b[axis] <= value
        if ina:
            out.append(a)
        if ina != inb:
            t = (value - a[axis]) / (b[axis] - a[axis])
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out


def side_y(poly, x, side):
    """The polygon's edge on side +1 (largest y) or -1 at x."""
    ys = []
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        if (a[0] - x) * (b[0] - x) <= 0 and a[0] != b[0]:
            t = (x - a[0]) / (b[0] - a[0])
            ys.append(a[1] + (b[1] - a[1]) * t)
    return max(ys) if side > 0 else min(ys)


def placed(m, frame, build):
    """Builds a piece in its own frame (build() makes it about the origin), then puts it at `frame`."""
    before = set(m.bm.verts)
    build()
    bmesh.ops.transform(m.bm, matrix=frame, verts=[v for v in m.bm.verts if v not in before])


# ------------------------------------------------------------------ the hull
def _hull(m):
    levels = [(0., 0., CREAM), (.03, 0., CREAM), (.07, 0., CREAM), (.13, 0., CREAM), (.2, 0., CREAM),
              (.25, 0., GOLD), (.275, .022, GOLD), (.3, 0., BLUE)]
    for g in SEAMS:
        levels += [(g - .006, 0., SEAM), (g, -.005, SEAM), (g + .006, 0., BLUE)]
    levels.append((1., 0., None))
    rings = []
    for h, out, _ in levels:
        ring = []
        for th in THETA:
            p = hull_xyz(th, h)
            if out:
                n = plan_normal(th, h)
                p += Vector((n.x, n.y, 0)) * out
            ring.append(m.bm.verts.new(p))
        rings.append(ring)
    faces = []
    for k in range(len(rings) - 1):
        a, b = rings[k], rings[k + 1]
        strip = [m.bm.faces.new((a[i], a[(i + 1) % N], b[(i + 1) % N], b[i])) for i in range(N)]
        _faces(m, strip, levels[k][2])
        faces += strip
    bottom = m.bm.faces.new(list(reversed(rings[0])))
    _faces(m, [bottom], CREAM, False)
    _one_sided(m, faces + [bottom], True)


def _inner_rings():
    """The wall's inside at the floor (z 3.75) and under the cap (following the sheer)."""
    floor, top = [], []
    for th in THETA:
        h = h_at(th, 3.75)
        p, n = hull_xyz(th, h), plan_normal(th, h)
        floor.append(Vector((p.x - n.x * WALL, p.y - n.y * WALL, 3.75)))
        p, n = hull_xyz(th, 1.), plan_normal(th, 1.)
        top.append(Vector((p.x - n.x * WALL, p.y - n.y * WALL, sheer(p.x) - .03)))
    return floor, top


def wall_at(floor, top, z):
    """The wall's inside at height z, as a polygon (x, y)."""
    out = []
    for a, b in zip(floor, top):
        t = max(0., min(1., (z - a.z) / (b.z - a.z)))
        p = a.lerp(b, t)
        out.append((p.x, p.y))
    return out


def _inside(m, floor, top):
    a = [m.bm.verts.new(p) for p in floor]
    b = [m.bm.verts.new(p) for p in top]
    wall = [m.bm.faces.new((b[i], b[(i + 1) % N], a[(i + 1) % N], a[i])) for i in range(N)]
    _faces(m, wall, WOOD)
    _one_sided(m, wall, False)
    # Under the planks, the shade their seams show.
    under = m.bm.faces.new([m.bm.verts.new((x, y, 3.775)) for x, y in wall_at(floor, top, 3.775)])
    _faces(m, [under], SHADE, False)
    m.no_recalc.add(under)
    # The floor: planks along the boat, 0.2 m wide, their joints staggered.
    poly = wall_at(floor, top, 3.78)
    poly = clip(clip(poly, 0, X_FORE + .01, True), 0, X_AFT - .01, False)
    for k in range(10):
        y0, y1 = -1. + .2 * k + .006, -1. + .2 * (k + 1) - .006
        strip = clip(clip(poly, 1, y0, True), 1, y1, False)
        if len(strip) < 3:
            continue
        cut = X_FORE - (k * .73) % 2.1
        while cut < X_AFT:
            board = clip(clip(strip, 0, cut + .005, True), 0, cut + 2.1 - .005, False)
            if len(board) >= 3:
                m.slab(board, Matrix.Identity(4), 3.78, FLOOR, WOOD)
            cut += 2.1
    # The raised decks at the ends, a gold nosing on their steps.
    for x_edge, z, keep in ((X_FORE, Z_FORE, False), (X_AFT, Z_AFT, True)):
        deck = clip(wall_at(floor, top, z), 0, x_edge, keep)
        m.slab(deck, Matrix.Identity(4), 3.78, z, WOOD)
        ys = sorted(y for x, y in deck if abs(x - x_edge) < 1e-6)
        xa, xb = (x_edge - .012, x_edge + .03) if keep else (x_edge - .03, x_edge + .012)
        m.box((xa, ys[0] + .01, z - .045), (xb, ys[-1] - .01, z + .012), GOLD, .008)


def _cap(m):
    path = []
    for th in THETA:
        p, n = hull_xyz(th, 1.), plan_normal(th, 1.)
        path.append(Vector((p.x - n.x * WALL / 2, p.y - n.y * WALL / 2, sheer(p.x))))
    profile = [(.1, -.075), (.106, -.06), (.106, .015), (.092, .045), (.06, .057), (-.06, .057), (-.092, .045),
               (-.106, .015), (-.106, -.06), (-.1, -.075)]
    sweep(m, path, profile, GOLD, closed=True)
    # A gold bead down each stem, from the cap to the gold band.
    for th in (0., math.pi):
        tube(m, [hull_xyz(th, h) for h in [.27 + .73 * k / 12 for k in range(13)]], [.03] * 13, GOLD, 8)


def _scroll(m, tip, inboard, r0, r1, turn_deg, width):
    """A gold volute on a stem's top: from the tip it rises forward, curls back over the top and winds in."""
    phi0, total = math.radians(225), math.radians(turn_deg)
    k = math.log(r0 / r1) / total
    c = tip + Vector((inboard * r0 * .7071, 0, r0 * .7071))
    steps = 44
    path = []
    for i in range(steps + 1):
        f = i / steps
        phi, r = phi0 - total * f, r0 * math.exp(-k * total * f)
        path.append(c + Vector((inboard * r * math.cos(phi), 0, r * math.sin(phi))))

    def prof(i):
        th = .075 - .04 * i / steps
        w, ch = width / 2, .014
        return [(th / 2, -w + ch), (th / 2, w - ch), (th / 2 - ch, w), (-th / 2 + ch, w), (-th / 2, w - ch),
                (-th / 2, -w + ch), (-th / 2 + ch, -w), (th / 2 - ch, -w)]
    sweep(m, path, prof, GOLD, up=Vector((0, 1, 0)))
    eye = path[-1].lerp(c, .5)
    for s in (-1, 1):
        dome(m, eye + Vector((0, s * width / 2, 0)), (0, s, 0), .045)


def _lantern(m, base, column):
    """A gold lantern on a column: the pedestal on a deck at `base`, glowing hexagonal panes."""
    t = Matrix.Translation(base)
    m.box(base + Vector((-.13, -.13, -.005)), base + Vector((.13, .13, .1)), GOLD, .015)
    z = .1
    m.lathe([(0, z), (.07, z), (.07, z + .03), (.045, z + .055), (.045, z + column - .05), (.06, z + column - .03),
             (.08, z + column), (0, z + column)], t, GOLD, 16)
    m.lathe([(0, z + column * .45), (.06, z + column * .45), (.06, z + column * .45 + .03), (0, z + column * .45 + .03)],
            t, GOLD, 16)
    z += column
    hexa = t @ Matrix.Rotation(math.radians(30), 4, "Z")
    m.lathe([(0, z), (.12, z), (.165, z + .055), (0, z + .055)], hexa, GOLD, 6)
    m.lathe([(0, z + .055), (.134, z + .055), (.154, z + .35), (0, z + .35)], hexa, GLOW, 6)
    for k in range(6):
        a = math.radians(30 + 60 * k)
        p0 = base + Vector((math.cos(a) * .152, math.sin(a) * .152, z + .055))
        p1 = base + Vector((math.cos(a) * .172, math.sin(a) * .172, z + .35))
        tube(m, [p0, p0.lerp(p1, .5), p1], [.016] * 3, GOLD, 6)
    m.lathe([(0, z + .34), (.18, z + .34), (.186, z + .375), (0, z + .375)], hexa, GOLD, 6)
    m.lathe([(0, z + .375), (.23, z + .375), (.21, z + .41), (.07, z + .54), (0, z + .54)], hexa, GOLD, 6)
    m.lathe([(0, z + .54), (.04, z + .54), (.032, z + .585), (0, z + .585)], t, GOLD, 10)
    m.sphere(base + Vector((0, 0, z + .635)), .052, GOLD, subdiv=2)


CROWN_PTS = [(-.42, 0), (.42, 0), (.47, .06), (.44, .12), (.5, .5), (.58, .72), (.36, .45), (.25, .41), (.14, .46),
             (0, .8), (-.14, .46), (-.25, .41), (-.36, .45), (-.58, .72), (-.5, .5), (-.44, .12), (-.47, .06)]
CROWN_BAND = [(-.46, -.17), (.46, -.17), (.49, -.12), (.46, -.07), (-.46, -.07), (-.49, -.12)]


def _crown(m, x, z, side, size=.5):
    p, n, _ = surface_at(x, z, side)
    frame = _frame(p + n * .004, n) @ Matrix.Scale(size, 4)
    m.slab(CROWN_PTS, frame, 0, .06, GOLD)
    m.slab(CROWN_BAND, frame, 0, .06, GOLD)


def _rope(m, ctrl, r=.04, pitch=.09, segs=9):
    """A three-strand rope along a spline through ctrl: one tube whose lobed section turns along it."""
    pts = spline(ctrl, .02)
    acc = [0.]
    for a, b in zip(pts, pts[1:]):
        acc.append(acc[-1] + (b - a).length)
    rings = []
    for p, s, (t, n, b) in zip(pts, acc, frames_along(pts)):
        turn = s / pitch * 2 * math.pi
        ring = []
        for k in range(segs):
            phi = 2 * math.pi * k / segs
            rr = r * (.8 + .2 * abs(math.cos(1.5 * (phi - turn))))
            ring.append(m.bm.verts.new(p + (n * math.cos(phi) + b * math.sin(phi)) * rr))
        rings.append(ring)
    faces = [m.bm.faces.new((ra[k], ra[(k + 1) % segs], rb[(k + 1) % segs], rb[k]))
             for ra, rb in zip(rings, rings[1:]) for k in range(segs)]
    _faces(m, faces, ROPE)
    _faces(m, [m.bm.faces.new(rings[0]), m.bm.faces.new(rings[-1])], ROPE, False)


def _fender(m, x, side, floor, top):
    p, n, up = surface_at(x, 3.5, side)
    flat = Vector((n.x, n.y, 0)).normalized()
    centre = p + flat * .101
    axis = up
    base = centre - axis * .29
    f = _frame(base, axis, Vector((1, 0, 0)))
    m.lathe([(0, 0), (.03, 0), (.04, .014), (.044, .035), (0, .035)], f, WHITE, 12)
    m.lathe([(0, .035), (.062, .035), (.088, .07), (.1, .14), (.1, .33), (.094, .37), (0, .37)], f, FENDER, 14)
    m.lathe([(0, .37), (.094, .37), (.085, .44), (.066, .5), (.045, .55), (.034, .58), (0, .58)], f, WHITE, 12)
    # The rope: twice round the fender's neck, up the hull, over the cap and tied inside.
    neck = base + axis * .53
    for dz, rr in ((0., .07), (.04, .06)):
        ring = [neck + axis * dz + (f.to_3x3() @ Vector((math.cos(a), math.sin(a), 0))) * rr
                for a in (2 * math.pi * i / 14 for i in range(14))]
        sweep(m, ring, [(.02 * math.cos(a), .02 * math.sin(a)) for a in (2 * math.pi * i / 6 for i in range(6))],
              ROPE, closed=True, up=axis)
    top_z = sheer(x)
    rim, _, _ = surface_at(x, top_z - .01, side)
    q, nq, _ = surface_at(x, 4.0, side)
    inner_y = side_y(wall_at(floor, top, top_z - .1), x, side)
    ctrl = [base + axis * .58, base + axis * .68 + flat * .02, q + nq * .05,
            Vector((x, rim.y + side * .1, top_z - .02)), Vector((x, rim.y + side * .07, top_z + .08)),
            Vector((x, (rim.y + inner_y) / 2, top_z + .095)), Vector((x, inner_y - side * .07, top_z + .08)),
            Vector((x, inner_y - side * .1, top_z - .03)), Vector((x, inner_y - side * .045, top_z - .16))]
    _rope(m, ctrl)


# ------------------------------------------------------------------ the furniture (each built about its own origin:
# x along it, its front toward -y, z up from the floor)
def _button(m, at):
    m.lathe([(0, 0), (.014, 0), (.011, .01), (0, .014)], _frame(at, (0, -1, 0)), GOLD, 6)


def _tufted(m, w, top, depth, back, mat, cell=.045, px=.2, pz=.17):
    """A buttoned cushion upright in the x-z plane: x -w/2..w/2, z 0..top(x); its face bulges toward -y (most between
    the buttons), its back flat at y = back."""
    nx = max(4, round(w / cell))
    zmax = max(top(-w / 2 + w * i / nx) for i in range(nx + 1))
    nz = max(4, round(zmax / cell))
    buttons, zb, row = [], .11, 0
    while zb < zmax - .06:
        xb = -w / 2 + .1 + (px / 2 if row % 2 else 0)
        while xb < w / 2 - .08:
            if zb < top(xb) - .07:
                buttons.append((xb, zb))
            xb += px
        zb += pz
        row += 1

    def face(x, z, tz):
        d = max(0., min(x + w / 2, w / 2 - x, z, tz - z))
        dim = sum(math.exp(-((x - bx) ** 2 + (z - bz) ** 2) / (2 * .032 ** 2)) for bx, bz in buttons)
        return -(depth * min(1., d / .07) ** .6 * (1 - .6 * min(1., dim)) + .004)
    grid = []
    for j in range(nz + 1):
        row_v = []
        for i in range(nx + 1):
            x = -w / 2 + w * i / nx
            tz = top(x)
            z = tz * j / nz
            row_v.append(m.bm.verts.new((x, face(x, z, tz), z)))
        grid.append(row_v)
    front = [m.bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]))
             for j in range(nz) for i in range(nx)]
    _faces(m, front, mat)
    border = ([grid[0][i] for i in range(nx + 1)] + [grid[j][nx] for j in range(1, nz + 1)]
              + [grid[nz][i] for i in range(nx - 1, -1, -1)] + [grid[j][0] for j in range(nz - 1, 0, -1)])
    backs = [m.bm.verts.new((v.co.x, back, v.co.z)) for v in border]
    nb = len(border)
    sides = [m.bm.faces.new((border[i], backs[i], backs[(i + 1) % nb], border[(i + 1) % nb])) for i in range(nb)]
    _faces(m, sides + [m.bm.faces.new(backs)], mat, False)
    for bx, bz in buttons:
        _button(m, (bx, face(bx, bz, top(bx)) - .002, bz))


def _back_frame(m, w, top, off=.04, size=(.028, .055, .04)):
    """A gilded frame round a cushion's sides and top (the cushion made by _tufted in the same frame)."""
    s, vf, vb = size
    pts = [Vector((-w / 2 - off, 0, 0))]
    pts += [Vector((-w / 2 - off, 0, top(-w / 2) * k / 4)) for k in range(1, 5)]
    pts += [Vector((x, 0, top(x) + off)) for x in [-w / 2 + w * i / 16 for i in range(17)]]
    pts += [Vector((w / 2 + off, 0, top(w / 2) * k / 4)) for k in range(4, -1, -1)]
    pts = chaikin(pts, 2)
    prof = [(s, -vf + .012), (s, vb), (-s, vb), (-s, -vf + .012), (-s + .012, -vf), (s - .012, -vf)]
    sweep(m, pts, prof, GOLD, up=Vector((0, 1, 0)))


def _legs(m, xs, ys, h=.1):
    for x in xs:
        for y in ys:
            m.lathe([(0, -.006), (.03, -.006), (.022, .045), (.034, h - .02), (.03, h), (0, h)],
                    Matrix.Translation((x, y, 0)), GOLD, 8)


def _pillow(m, size, thick, mat):
    """A square cushion, its face in the x-y plane, puffed to +-thick in z, about the origin."""
    nu, nv, e1, e2 = 8, 16, 2 / 1.7, 2 / 4.5
    rings = []
    for i in range(1, nu):
        u = -math.pi / 2 + math.pi * i / nu
        cu = spow(math.cos(u), e1)
        rings.append([m.bm.verts.new((size * cu * spow(math.cos(v), e2), size * cu * spow(math.sin(v), e2),
                                      thick * spow(math.sin(u), e1)))
                      for v in (-math.pi + 2 * math.pi * j / nv for j in range(nv))])
    lo, hi = m.bm.verts.new((0, 0, -thick)), m.bm.verts.new((0, 0, thick))
    faces = [m.bm.faces.new((lo, rings[0][(j + 1) % nv], rings[0][j])) for j in range(nv)]
    faces += [m.bm.faces.new((ra[j], ra[(j + 1) % nv], rb[(j + 1) % nv], rb[j]))
              for ra, rb in zip(rings, rings[1:]) for j in range(nv)]
    faces += [m.bm.faces.new((rings[-1][j], rings[-1][(j + 1) % nv], hi)) for j in range(nv)]
    _faces(m, faces, mat)
    m.lathe([(0, 0), (.016, 0), (.012, .01), (0, .014)], Matrix.Translation((0, 0, thick - .006)), GOLD, 6)


def _settee(m, length, depth=.78, mat=PURPLE, pillows=()):
    """A gold framed, buttoned settee (the photo's purple sofa): rolled arms with gold scroll ends."""
    arm, L, D = .15, length, depth
    _legs(m, (-L / 2 + .07, L / 2 - .07), (-D / 2 + .07, D / 2 - .07))
    m.box((-L / 2, -D / 2, .1), (L / 2, D / 2, .3), mat, .03)
    m.box((-L / 2 + .02, -D / 2 - .012, .115), (L / 2 - .02, -D / 2 + .01, .155), GOLD, .008)
    n = max(1, round((L - 2 * arm) / .62))
    cw = (L - 2 * arm) / n
    for k in range(n):
        x0 = -L / 2 + arm + cw * k
        m.box((x0 + .006, -D / 2 + .02, .3), (x0 + cw - .006, D / 2 - .2, .43), mat, .045)
    for sx in (-1, 1):
        xa, xb = sorted((sx * L / 2, sx * (L / 2 - arm)))
        xc = (xa + xb) / 2
        m.box((xa, -D / 2 + .02, .3), (xb, D / 2 - .05, .5), mat, .03)
        m.lathe([(0, -D / 2 + .02), (.085, -D / 2 + .02), (.085, D / 2 - .065), (0, D / 2 - .065)],
                Matrix.Translation((xc, 0, .52)) @ Matrix.Rotation(-math.pi / 2, 4, "X"), mat, 16)
        m.lathe([(0, 0), (.062, 0), (.062, .014), (.04, .026), (0, .03)],
                Matrix.Translation((xc, -D / 2 + .02, .52)) @ Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 16)
    bw = L - 2 * arm + .1

    def top(x):
        return .34 + .07 * max(0., math.cos(math.pi * x / bw)) ** 1.5
    back = Matrix.Translation((0, D / 2 - .17, .36)) @ Matrix.Rotation(math.radians(-8), 4, "X")

    def build():
        _tufted(m, bw, top, .06, .12, mat)
        _back_frame(m, bw, top)
    placed(m, back, build)
    for px in pillows:
        placed(m, Matrix.Translation((px, D / 2 - .32, .6)) @ Matrix.Rotation(math.radians(78), 4, "X"),
               lambda: _pillow(m, .19, .07, PURPLE_LIGHT))


def _armchair(m):
    """A gold framed cream armchair (the photo's bergeres): buttoned arched back, padded gold arms."""
    W, D = .74, .68
    _legs(m, (-W / 2 + .06, W / 2 - .06), (-D / 2 + .06, D / 2 - .06))
    m.box((-W / 2, -D / 2, .1), (W / 2, D / 2, .29), GOLD, .02)
    m.box((-W / 2 + .035, -D / 2 - .005, .13), (W / 2 - .035, D / 2 - .03, .265), UPHOLSTERY, .015)
    m.box((-W / 2 + .07, -D / 2 + .015, .29), (W / 2 - .07, D / 2 - .14, .42), UPHOLSTERY, .04)
    for sx in (-1, 1):
        xc = sx * (W / 2 - .045)
        m.box((xc - .032, -D / 2 + .05, .296), (xc + .032, D / 2 - .1, .5), UPHOLSTERY, .015)
        m.box((xc - .045, -D / 2 + .02, .5), (xc + .045, D / 2 - .08, .545), GOLD, .012)
        m.box((xc - .038, -D / 2 + .05, .54), (xc + .038, D / 2 - .1, .58), UPHOLSTERY, .015)
        m.lathe([(0, .29), (.03, .29), (.022, .36), (.03, .44), (.026, .5), (0, .5)],
                Matrix.Translation((xc, -D / 2 + .04, 0)), GOLD, 10)
        m.lathe([(0, 0), (.04, 0), (.04, .02), (.025, .03), (0, .03)],
                Matrix.Translation((xc, -D / 2 + .02, .523)) @ Matrix.Rotation(math.pi / 2, 4, "X"), GOLD, 12)
    bw = W - .16

    def top(x):
        return .38 + .09 * max(0., math.cos(math.pi * x / (bw + .06))) ** .8
    back = Matrix.Translation((0, D / 2 - .1, .34)) @ Matrix.Rotation(math.radians(-10), 4, "X")

    def build():
        _tufted(m, bw, top, .055, .1, UPHOLSTERY, px=.18, pz=.16)
        _back_frame(m, bw, top, .035, (.032, .055, .045))
    placed(m, back, build)


def _furnish(m, floor, top):
    poly = wall_at(floor, top, 3.8)

    def against_far_wall(x, length, depth):
        x0, x1 = x - length / 2, x + length / 2
        y0, y1 = side_y(poly, x0, 1), side_y(poly, x1, 1)
        ang = math.atan2(y1 - y0, x1 - x0)
        y = min(y0, y1, side_y(poly, x, 1)) - depth / 2 - .03
        return Matrix.Translation((x, y, FLOOR - .004)) @ Matrix.Rotation(ang, 4, "Z"), y - depth / 2

    fronts = []
    for x, length, depth, pillows in ((.2, 1.5, .72, (-.38, .38)), (-1.2, .9, .66, (.1,)), (1.5, .9, .66, (-.1,))):
        frame, front = against_far_wall(x, length, depth)
        fronts.append(front)
        placed(m, frame, lambda length=length, depth=depth, pillows=pillows: _settee(m, length, depth, PURPLE, pillows))
    for x, turn, front in ((-1.2, 12, fronts[1]), (1.3, -12, fronts[2])):
        y = front - .03 - .41
        near = side_y(poly, x, -1)
        if y - .41 < near + .03:
            print("ROYAL BOAT: armchair at x %.2f pressed to the near wall (%.2f < %.2f)" % (x, y - .41, near + .03))
            y = near + .03 + .41
        placed(m, Matrix.Translation((x, y, FLOOR - .004)) @ Matrix.Rotation(math.radians(turn), 4, "Z"),
               lambda: _armchair(m))


# ------------------------------------------------------------------ the boat
def build(name, bx, by, collection=None):
    """'Boat <...>' at (bx, by) (its deck's middle; deck 3.8), built in place."""
    m = Mesh(name) if collection is None else Mesh(name, collection)
    floor, top = _inner_rings()
    _hull(m)
    _inside(m, floor, top)
    _cap(m)
    _scroll(m, Vector((-BOW_END, 0, sheer(-BOW_END) - .02)), 1, .33, .055, 470, .22)
    _scroll(m, Vector((STERN_END, 0, sheer(STERN_END) - .02)), -1, .19, .042, 440, .16)
    _lantern(m, Vector((-2.62, 0, Z_FORE)), .39)
    _lantern(m, Vector((2.7, 0, Z_AFT)), .38)
    for side in (-1, 1):
        _crown(m, -2.0, 3.77, side)
        for x in (.8, 1.95):
            _fender(m, x, side, floor, top)
    _furnish(m, floor, top)
    bmesh.ops.translate(m.bm, vec=Vector((bx, by, 0)), verts=m.bm.verts[:])
    ob = m.done()
    me = ob.data
    print("ROYAL BOAT %s tris %d" % (name, sum(len(p.vertices) - 2 for p in me.polygons)))
    return ob

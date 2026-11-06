# Royal water garden - concept-art dressing, sample 8 v2: the castle walls round the garden (R474).
#
# v1 (R472): a ring of 22 round towers on a convex curve, crenellated walls with blind arches between. 승규 11-05:
# "모양 더 수정" - "벽이 갈라지거나 그런것도 넣어, 평면을 더 둥글게, 탑을 그림처럼 길쭉하게, 아치를 크게"; then "레퍼런스
# 왼쪽 기둥은 4개인데 넌 왜 11개나 돼? 왼쪽 오른쪽 각각 6개만 건축 해". So:
#   the plan     an oval: the wall's line is a superellipse (x / 54)^2.4 + ((z - 4.21) / 66.81)^2.4 = 1 through the
#                gate's wing ends (24.8, 66.5) and the way in (towers at x +-9.5, z -62.2); six towers a side evenly
#                along it (the way in's the sixth; ~27 m apart, the first on the terraces' corner at (44.4, 48.6)),
#                straight walls between (chords). It cuts the old rectangle's corners: the terraces by the gate and the
#                reservoir are clipped to it (dressed here now, not in gate_sample_v2.py), the water outside it is under
#                a lawn round the castle (z 0, out to the section's edge); along the sides it stands out past the water
#                (banks). The first wall leaves the gate wing's low end block (x 22.6 .. 24.8, z 65.3 .. 67.7) with its
#                outer face out of the block's outer corner and its end inside the block: no corner left standing out
#   the towers   as the picture's: slim (r 2.1, the two at the way in 2.4) and tall - the shaft to 17.6 with a band at
#                the wall walk, a few slits, a ring of six small windows under the eave, a stone eave, a pointed blue
#                slate cone (1.2 x its width high), gold band, finial, pole and red pennant; a crowned banner on the
#                garden side; a planter with cone topiaries where there is ground before it
#   the arches   big, as the picture's: 4.4 m wide, quoins bonding into the courses, radial voussoirs with a proud
#                keystone; two to a wall (one where the ground under it changes; none on the terraces, too high for
#                one), over the moat too (their sill 0.6 m over the water). 승규 (Unity, R474): "아치형 창 뚫어주고" -
#                windows through the wall: both faces share one set of courses, each face's quoins and voussoirs run
#                back to the wall's middle where the other's meet them, a sill through the wall, no dark back
#   the wear     blocks with chipped corners (a broken notch), blocks split by jagged cracks 2.4 cm open with their dark
#                inside showing, cracks running down through two to four courses and up from some arches' crowns, a few
#                sunken (weathered) blocks; chipped merlons; slanting cracks splitting some tower blocks. No moss: a
#                whole green-tinted block read as paint, not moss
#   the stone    승규 (Unity, R474): "벽 텍스쳐를 엔딩 문으로 바꿔" - as the gate's masonry (gate_sample_v2.py gate_run):
#                one stone (no warmer second tone), the blocks' face edges rounded in two segments (were a flat chamfer),
#                the joints over a warm mortar core 9 cm behind both faces (it stood 1.6 m back and the joints read
#                black), bigger blocks (courses 1.3, blocks about 2.0 / 2.4 m; were 1.15 and 1.7)
# Everything else as v1: the masonry (random lengths, no joint within 25 cm of the one below, a few cm of proudness,
# walls meeting only towers and the gate's end block), the wall walk at 11.0 on corbels, merlons (clear of the towers'
# shafts), the strip at 2.8 between the yard and the wall.
# All faces outward and no two faces in one plane (PITFALLS 38, 39): check_coplanar.py, which since R474 looks at faces
# in any plane and counts only real common area, finds none.
#
# Run:  blender -b --python walls_sample_v2.py
# Out:  WallsSample_v2.blend, render_walls_v2_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Walls dressing"
os.environ["GC_SEED"] = "88"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403
import bmesh  # noqa: E402
import water_tiers as wt  # noqa: E402

RND = random.Random(473)
UPPER = 3.8
LAKE, MOAT, RES = wt.MIDDLE, wt.EARLY, wt.LATE
STRIP = UPPER - 1.0
BANK = .6
WATER_EDGE = 45.8


# ------------------------------------------------------------------ materials (the gate's, R460)
def gate_stone_texture():
    """2 m of the reference gate's stone (sRGB): pale peach, soft deeper blotches, a fine grain, a few specks."""
    n = 1024
    t = np.clip(.7 + .3 * periodic_noise(n, n, 31, 2.3, 2), 0, 1)
    t = t * t * (3 - 2 * t)
    light, deep = np.array([.99, .905, .82]), np.array([.965, .845, .735])
    rgb = deep * (1 - t[..., None]) + light * t[..., None]
    rgb = rgb * (1 + .012 * periodic_noise(n, n, 32, .4, 80))[..., None]
    pits = np.random.default_rng(33).random((n, n))
    rgb[pits < .0012] *= .87
    return save_image("Stone_Gate", np.clip(rgb, 0, 1))


GATE_TEX = gate_stone_texture()
# R477 (승규 Unity "색이 다르잖아"): the walls' own stone - the gate's picture under a warmer, deeper tint. In the
# garden's light the walls' long faces face every way: the sunlit ones washed to white, the ones in shade went
# grey, while the gate's south face, lit aslant, reads peach. This keeps them peach (the reference gate's median
# 251 222 190) lit or not; the gate keeps GC | gate stone.
GS = textured("GC | castle stone", GATE_TEX, (1.0, .93, .86), .74, .5)
GS2 = GS          # R474 (승규 "벽 텍스쳐를 엔딩 문으로 바꿔"): one stone for every block and trim, as the gate's
GJ = material("GC | gate joint", (.80, .65, .53), .85)
GG = material("GC | gate gold", (.98, .82, .30), .3, .75)
BLUE = material("GC | gate blue", (.18, .38, .78), .55)
RED = material("GC | pennant red", (.86, .14, .12), .55)
SHADE = material("GC | arch shade", (.12, .11, .13), .9)
LAWN = material("GC | lawn", (.46, .64, .30), .9)

# ------------------------------------------------------------------ the plan: an oval ring
RING_A, RING_B, RING_CY, RING_N = 54.0, 66.81, 4.21, 2.4
T = 1.2
WALK = 11.0
COURSE = 1.3      # R474: bigger blocks, nearer the gate's (1.7 x 2.2); was 1.15
R_TOWER, R_DOOR = 2.1, 2.4
EAVE = 17.6
CENTRE = Vector((0, 2, 0))
UP = Vector((0, 0, 1))


def ring_pt(t):
    c, s = math.cos(t), math.sin(t)
    e = 2 / RING_N
    return Vector((math.copysign(abs(c) ** e, c) * RING_A, RING_CY + math.copysign(abs(s) ** e, s) * RING_B, 0))


TOWERS_PER_SIDE = 6                   # R474 (승규 "왼쪽 오른쪽 각각 6개만"): the way in's tower is the sixth


def ring_towers(count=TOWERS_PER_SIDE):
    """The east half's towers, evenly along the ring from the gate's wing end to the tower at the way in (the last)."""
    t_w = math.acos((24.8 / RING_A) ** (RING_N / 2))
    t_e = -math.acos((9.5 / RING_A) ** (RING_N / 2))
    ts = [t_w + (t_e - t_w) * i / 4000 for i in range(4001)]
    pts = [ring_pt(t) for t in ts]
    acc = [0.]
    for a, b in zip(pts, pts[1:]):
        acc.append(acc[-1] + (b - a).length)
    n = count
    out = []
    j = 0
    for i in range(1, n + 1):
        s = acc[-1] * i / n
        while j < len(acc) - 2 and acc[j + 1] < s:
            j += 1
        f = (s - acc[j]) / max(1e-9, acc[j + 1] - acc[j])
        p = pts[j].lerp(pts[j + 1], f)
        out.append((p.x, p.y, R_DOOR if i == n else R_TOWER))
    return pts[0], out


_, EAST = ring_towers()

# The first wall leaves the gate wing's low end block (gate_sample_v2.py: x 22.6 .. 24.8, z 65.3 .. 67.7, 13.96 high).
# Its line starts on the block's end plane (x 24.8) where its outer face runs out of the block's outer corner (2 cm
# outside it, so the corner's edge is in the wall), and the wall starts there: its end is inside the block and the
# block's end inside it, its inner face comes out of the block's garden face.
END_CORNER = Vector((24.82, 67.72, 0))


def wing_start(first):
    """The east side's first wall to the tower `first`: its start point on x 24.8 and its u at END_CORNER."""
    b = Vector((first[0], first[1], 0))
    lo, hi = 63.0, 67.7
    for _ in range(60):
        y = (lo + hi) / 2
        a = Vector((24.8, y, 0))
        d = (b - a).normalized()
        out = Vector((-d.y, d.x, 0))                        # away from the garden for this (clockwise) run
        if (END_CORNER - a).dot(out) > T:
            lo = y
        else:
            hi = y
    a = Vector((24.8, (lo + hi) / 2, 0))
    return a, (END_CORNER - a).dot((b - a).normalized())


WING, WING_US = wing_start(EAST[0])
ARCHES = []                                                 # (centre on the inner face, n, d, sill, spring): cameras


def ground_at(x, y):
    """The ground (or water) inside the garden at a plan point near the wall."""
    ax = abs(x)
    if y >= 63.1 and ax <= 25.2:
        return STRIP
    if y > 60.5 and ax < 6:
        return UPPER
    if (y > 59 and ax >= 14.3) or (y > 60.5 and ax >= 6):
        return STRIP
    if 14.3 <= ax <= 44 and 48.6 <= y <= 59:
        return UPPER
    if 26 <= ax <= 44 and 33 <= y <= 48.6:
        return UPPER
    if ax >= WATER_EDGE and -61.5 <= y <= 59:
        return BANK
    return LAKE if y > -15.5 else MOAT


# ------------------------------------------------------------------ helpers
def frame_on(at, out, up=UP):
    z = Vector(out).normalized()
    x = Vector(up).cross(z)
    if x.length < 1e-6:
        x = Vector((1, 0, 0))
    x.normalize()
    y = z.cross(x)
    return Matrix.Translation(Vector(at)) @ Matrix((x, y, z)).transposed().to_4x4()


def face_frame_u(origin, u_dir, normal):
    return Matrix.Translation(Vector(origin)) @ Matrix((Vector(u_dir), UP, Vector(normal))).transposed().to_4x4()


def chamfer(m, verts, offset, mat):
    edges = [e for e in {e for v in verts for e in v.link_edges}
             if len(e.link_faces) == 2 and e.calc_face_angle(0) > .5]
    if not edges or offset <= 0:
        return
    r = bmesh.ops.bevel(m.bm, geom=edges, offset=offset, segments=1, affect="EDGES", profile=.5, clamp_overlap=True)
    i = m.mi(mat)
    for f in r["faces"]:
        f.material_index = i
        f.smooth = False


def round_face_edges(m, vs, frame, depths, offset, mat, straight_only=True):
    """Rounds the edges round a block's face at each local depth in `depths`, in two segments as the gate's blocks
    (R474: the walls' stone as the gate's; was a flat chamfer on every edge). straight_only: only the block's straight
    sides along u or v, so a crack or a chip stays a sharp break."""
    inv = frame.inverted()
    local = {v: inv @ v.co for v in vs}
    edges = set()
    for depth in depths:
        face = {v for v in vs if abs(local[v].z - depth) < 1e-4}
        for e in {e for v in face for e in v.link_edges}:
            a, b = e.verts
            if a not in face or b not in face or len(e.link_faces) != 2 or e.calc_face_angle(0) < .5:
                continue
            if straight_only and abs(local[a].x - local[b].x) > 1e-4 and abs(local[a].y - local[b].y) > 1e-4:
                continue
            edges.add(e)
    if not edges or offset <= 0:
        return
    r = bmesh.ops.bevel(m.bm, geom=list(edges), offset=offset, segments=2, affect="EDGES", profile=.5,
                        clamp_overlap=True)
    i = m.mi(mat)
    for f in r["faces"]:
        f.material_index = i
        f.smooth = False


def block(m, poly, frame, d0, d1, mat, bev, both=False):
    """A block of the 2D `poly` from depth d0 back to its face d1 (both: its back d0 is seen too), its face's straight
    edges rounded."""
    vs = m.slab(poly, frame, d0, d1, mat)
    round_face_edges(m, vs, frame, (d1, d0) if both else (d1,), bev, mat)


def loft_face(m, frame, inner, outer, d0, d1, mat, bev):
    vs = []

    def row(pts, d):
        r = [m.bm.verts.new(frame @ Vector((u, v, d))) for u, v in pts]
        vs.extend(r)
        return r
    fi, fo, bi, bo = row(inner, d1), row(outer, d1), row(inner, d0), row(outer, d0)
    new = m.bm.faces.new
    for i in range(len(inner) - 1):
        j = i + 1
        new((fi[i], fi[j], fo[j], fo[i]))
        new((bo[i], bo[j], bi[j], bi[i]))
        new((bi[i], bi[j], fi[j], fi[i]))
        new((fo[i], fo[j], bo[j], bo[i]))
    new((fi[0], fo[0], bo[0], bi[0]))
    new((bi[-1], bo[-1], fo[-1], fi[-1]))
    m.tag(vs, mat)
    round_face_edges(m, vs, frame, (d1,), bev, mat, False)


def tube(m, pts, r, mat, segs=8):
    pts = [Vector(p) for p in pts]
    rings = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        n = t.orthogonal().normalized()
        b = t.cross(n)
        rings.append([m.bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * r)
                      for a in (2 * math.pi * k / segs for k in range(segs))])
    for ra, rb in zip(rings, rings[1:]):
        for k in range(segs):
            m.bm.faces.new((ra[k], ra[(k + 1) % segs], rb[(k + 1) % segs], rb[k]))
    m.bm.faces.new(rings[0])
    m.bm.faces.new(list(reversed(rings[-1])))
    m.tag([v for r in rings for v in r], mat, True)


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


def area(poly):
    return .5 * sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(poly, poly[1:] + poly[:1]))


def clip_convex(subject, convex):
    """subject clipped to a convex polygon (counter-clockwise)."""
    out = list(subject)
    for i in range(len(convex)):
        if len(out) < 3:
            return []
        a, b = convex[i], convex[(i + 1) % len(convex)]
        src, out = out, []

        def inside(p):
            return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) >= 0
        for j in range(len(src)):
            p, q = src[j], src[(j + 1) % len(src)]
            ip, iq = inside(p), inside(q)
            if ip:
                out.append(p)
            if ip != iq:
                dx, dy = q[0] - p[0], q[1] - p[1]
                ex, ey = b[0] - a[0], b[1] - a[1]
                den = ex * dy - ey * dx
                if abs(den) > 1e-12:
                    t = (ex * (a[1] - p[1]) - ey * (a[0] - p[0])) / den
                    out.append((p[0] + dx * t, p[1] + dy * t))
    return out


def pave(m, poly, z, mat=None):
    if len(poly) < 3 or abs(area(poly)) < .05:
        return
    if area(poly) < 0:
        poly = list(reversed(poly))
    vs = [m.bm.verts.new((x, y, z)) for x, y in poly]
    f = m.bm.faces.new(vs)
    m.tag(vs, mat or M["paving"])
    if mat is None:
        m.fixed[f] = {v: (v.co.x / (TILE_SIZE * 4), v.co.y / (TILE_SIZE * 4)) for v in vs}
    m.no_recalc.add(f)


def body(m, poly, z0, z1, mat):
    if len(poly) >= 3 and abs(area(poly)) > .05:
        m.slab(poly if area(poly) > 0 else list(reversed(poly)), Matrix.Identity(4), z0, z1, mat)


# ------------------------------------------------------------------ the masonry and its wear
def split(s, e, length, rnd, prev):
    span = e - s
    if span <= .05:
        return []
    n = max(1, round(span / length))
    best = None
    for _ in range(16):
        w = [rnd.uniform(.7, 1.35) for _ in range(n)]
        tot = sum(w)
        cuts, acc = [s], s
        for wi in w[:-1]:
            acc += span * wi / tot
            cuts.append(acc)
        cuts.append(e)
        worst = min([min((abs(c - p) for p in prev), default=9.) for c in cuts[1:-1]] or [9.])
        if best is None or worst > best[0]:
            best = (worst, cuts)
        if worst > .25:
            break
    cuts = best[1]
    return list(zip(cuts, cuts[1:]))


def disc_cut(a0, a1, zb, zt, c, p, R, steps=6):
    def w(v):
        d = v - p
        return math.sqrt(R * R - d * d) if abs(d) < R else 0.
    vs = [zb + (zt - zb) * i / steps for i in range(steps + 1)]
    out = []
    if a0 < c:
        edge = [(min(a1, c - w(v)), v) for v in vs]
        if max(x for x, _ in edge) - a0 > .18:
            out.append([(a0, zb)] + [(max(a0 + .02, x), v) for x, v in edge] + [(a0, zt)])
    if a1 > c:
        edge = [(max(a0, c + w(v)), v) for v in vs]
        if a1 - min(x for x, _ in edge) > .18:
            out.append([(a1, zb), (a1, zt)] + [(min(a1 - .02, x), v) for x, v in reversed(edge)])
    return out


def crack_split(a0, a1, zb, zt, xt, xb, rnd):
    """A block split by a jagged crack from (xt, top) to (xb, bottom), 2.4 cm open: two pieces and the crack's path."""
    h = zt - zb
    zs = [zt, zt - h * rnd.uniform(.3, .45), zb + h * rnd.uniform(.25, .4), zb]
    xs = [xt, xt + (xb - xt) * .4 + rnd.uniform(-.09, .09), xt + (xb - xt) * .7 + rnd.uniform(-.09, .09), xb]
    xs = [min(a1 - .1, max(a0 + .1, x)) for x in xs]
    g = .012
    left = [(a0, zb), (xs[3] - g, zb), (xs[2] - g, zs[2]), (xs[1] - g, zs[1]), (xs[0] - g, zt), (a0, zt)]
    right = [(xs[3] + g, zb), (a1, zb), (a1, zt), (xs[0] + g, zt), (xs[1] + g, zs[1]), (xs[2] + g, zs[2])]
    return [left, right], list(zip(xs, zs))


def crack_shadow(m, frame, path, pr):
    """The dark inside of a crack, 3 cm behind the cracked block's face pr (seen through the crack; behind a sunken
    block's face too), in front of the mortar core (9 cm behind the face)."""
    w = .02
    z0, z1 = min(z for _, z in path) + .004, max(z for _, z in path) - .004   # its ends off the block's bed and top
    poly = [(x - w, min(z1, max(z0, z))) for x, z in path] + [(x + w, min(z1, max(z0, z))) for x, z in reversed(path)]
    m.slab(poly, frame, pr - .08, pr - .03, SHADE)


def chip(poly, rnd, corners=(0, 1, 2, 3)):
    """A rectangle with one corner broken off (a ragged notch)."""
    i = rnd.choice(corners)
    c = Vector(poly[i])
    pv, nx = Vector(poly[i - 1]), Vector(poly[(i + 1) % 4])
    la, lb = (pv - c).length, (nx - c).length
    da, db = min(la * .45, rnd.uniform(.12, .32)), min(lb * .45, rnd.uniform(.1, .24))
    p1 = c + (pv - c).normalized() * da
    p2 = c + (nx - c).normalized() * db
    mid = (p1 + p2) / 2 + ((p1 + p2) / 2 - c).normalized() * rnd.uniform(.0, .06)
    out = []
    for j, q in enumerate(poly):
        if j == i:
            out += [tuple(p1), tuple(mid), tuple(p2)]
        else:
            out.append(q)
    return out


def course_bounds(v0, v1, course=COURSE):
    n = max(1, round((v1 - v0) / course))
    h = (v1 - v0) / n
    return [v0 + h * k for k in range(n + 1)]


def crack_lines(us, ue, bounds, openings, rnd, count):
    """Cracks running down through 2..4 courses: {course: [(x_top, x_bottom)]}."""
    lines = {}
    n_c = len(bounds) - 1
    for _ in range(count):
        for _try in range(10):
            x = rnd.uniform(us + .8, ue - .8)
            if all(abs(x - o["c"]) > o["R"] + .6 for o in openings):
                break
        else:
            continue
        k = rnd.randrange(max(1, n_c // 2), n_c)
        for _ in range(rnd.randint(2, 4)):
            if k < 0:
                break
            x2 = x + rnd.uniform(-.35, .35)
            lines.setdefault(k, []).append((x, x2))
            x, k = x2, k - 1
    return lines


def build_face(m, frame, us, ue, bounds, openings, deep, length, bev, rnd, wear=1.0, plinth=True, back=T):
    """One face of a wall in courses, around arches, with its wear (chips, cracks, sunken blocks)."""
    prev = []
    g = .012
    cracks = crack_lines(us, ue, bounds, openings, rnd, rnd.choice((2, 3, 3, 4)) if wear >= 1 else (1 if wear else 0))
    for o in openings:
        if wear and rnd.random() < .6:
            k = next((k for k, z in enumerate(bounds) if z >= o["spring"] + o["R"] - .2), None)
            x = o["c"] + rnd.uniform(-.4, .4)
            while k is not None and k < len(bounds) - 1 and rnd.random() < .8:
                x2 = x + rnd.uniform(-.3, .3)
                cracks.setdefault(k, []).append((x, x2))
                x, k = x2, k + 1
    for k in range(len(bounds) - 1):
        zb, zt = bounds[k], bounds[k + 1]
        blocked, discs = [], []
        for o in openings:
            if zb >= o["sill"] - 1e-6 and zt <= o["spring"] + 1e-6:
                q = o["ql"] if (k - o["k_sill"]) % 2 == 0 else o["qs"]
                blocked.append((o["c"] - o["a"] - q, o["c"] + o["a"] + q))
                for qa, qb in ((o["c"] - o["a"] - q, o["c"] - o["a"]), (o["c"] + o["a"], o["c"] + o["a"] + q)):
                    poly = [(qa + g, zb + g), (qb - g, zb + g), (qb - g, zt - g), (qa + g, zt - g)]
                    if wear and rnd.random() < .12:
                        poly = chip(poly, rnd)
                    # back to about the wall's middle, where the other face's quoin meets it (the window's reveal;
                    # 1 cm off the middle, the plane the ground's and lawn's edges lie in)
                    block(m, poly, frame, -back, .03 + rnd.uniform(0, .01), GS, bev)
            elif o["spring"] - 1e-6 <= zb < o["spring"] + o["R"]:
                top = min(zt, o["spring"] + o["R"] - 1e-4) - o["spring"]
                w = math.sqrt(max(0., o["R"] ** 2 - top * top))
                blocked.append((o["c"] - w, o["c"] + w))
                discs.append(o)
        blocked.sort()
        free, cur = [], us
        for a, b in blocked:
            if a > cur:
                free.append((cur, min(a, ue)))
            cur = max(cur, b)
        if cur < ue:
            free.append((cur, ue))
        joints = [x for a, b in blocked for x in (a, b)]
        for s, e in free:
            for a0, a1 in split(s, e, length, rnd, prev):
                joints.append(a1)
                pr = rnd.uniform(0, .015)
                mat, bb = GS, bev                                # R474: one stone, as the gate's
                if plinth and k == 0:
                    pr, bb = .09 + pr, bev * 1.4
                if wear and rnd.random() < .035 * wear:
                    pr = -rnd.uniform(.025, .05)                 # a weathered, sunken block (in front of the core)
                x0, x1, y0, y1 = a0 + g, a1 - g, zb + g, zt - g
                pieces = None
                for o in discs:
                    if a1 > o["c"] - o["R"] and a0 < o["c"] + o["R"]:
                        pieces = disc_cut(x0, x1, y0, y1, o["c"], o["spring"], o["R"] + .02)
                if pieces is None:
                    through = [(xt, xb) for xt, xb in cracks.get(k, []) if x0 + .15 < xt < x1 - .15 and x0 + .15 < xb < x1 - .15]
                    path = None
                    if through:
                        pieces, path = crack_split(x0, x1, y0, y1, through[0][0], through[0][1], rnd)
                    elif wear and rnd.random() < .07 * wear and x1 - x0 > .7:
                        xt = rnd.uniform(x0 + .25, x1 - .25)
                        pieces, path = crack_split(x0, x1, y0, y1, xt, xt + rnd.uniform(-.25, .25), rnd)
                    else:
                        poly = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
                        if wear and rnd.random() < .16 * wear:
                            poly = chip(poly, rnd)
                        pieces = [poly]
                    if path:
                        crack_shadow(m, frame, path, pr)
                for i, poly in enumerate(pieces):
                    block(m, poly, frame, -deep, pr + (.004 * i if len(pieces) > 1 else 0), mat, bb)
        prev = joints


def arch_parts(m, frame, o, rnd, sill, back=T):
    """An arch's ring on one face: 13 radial voussoirs back to the wall's middle, where the other face's meet them, the
    keystone proud; with `sill`, the sill through the wall. R474 (승규 "아치형 창 뚫어주고"): the arches are windows
    through the wall (no dark back)."""
    c, a, p, ring = o["c"], o["a"], o["spring"], o["ring"]
    n_v = 13
    for k in range(n_v):
        t0, t1 = math.pi * k / n_v + .006, math.pi * (k + 1) / n_v - .006
        ts = [t0 + (t1 - t0) * i / 3 for i in range(4)]
        key = k == n_v // 2
        ro = a + ring + (.14 if key else 0)
        inner = [(c + a * math.cos(t), p + a * math.sin(t)) for t in ts]
        outer = [(c + ro * math.cos(t), p + ro * math.sin(t)) for t in ts]
        loft_face(m, frame, inner, outer, -back, .05 + (.07 if key else rnd.uniform(0, .015)), GS, .05)
    if sill:
        block(m, [(c - a, o["sill"]), (c + a, o["sill"]), (c + a, o["sill"] + .22), (c - a, o["sill"] + .22)], frame,
              -(2 * T + .07), .07, GS, .05, True)


def core_outline(us, ue, v0, v1, openings, gap=.02):
    """The wall's elevation from v0 to v1 (u from us to ue) less its arches' windows, each grown by `gap` (behind their
    reveals and soffits), as polygons (u, v): the columns between windows, under each sill, over each arch."""
    out = []
    u = us
    for o in sorted(openings, key=lambda o: o["c"]):
        a = o["a"] + gap
        left, right = o["c"] - a, o["c"] + a
        if left > u:
            out.append([(u, v0), (left, v0), (left, v1), (u, v1)])
        out.append([(left, v0), (right, v0), (right, o["sill"] + .1), (left, o["sill"] + .1)])
        arc = [(o["c"] + a * math.cos(t), o["spring"] + a * math.sin(t)) for t in (math.pi * (1 - i / 24) for i in range(25))]
        out.append(arc + [(right, v1), (left, v1)])
        u = right
    if ue > u:
        out.append([(u, v0), (ue, v0), (ue, v1), (u, v1)])
    return out


def banner(m, at, n, width, length, crown=.5):
    n = Vector(n).normalized()
    side = UP.cross(n).normalized()
    at = Vector(at)
    tube(m, [at - side * (width / 2 + .18), at + side * (width / 2 + .18)], .06, GG, 8)
    for e in (-1, 1):
        m.sphere(at + side * e * (width / 2 + .22), .1, GG, subdiv=1)
    cloth = [(-width / 2, 0), (width / 2, 0), (width / 2, -length * .86), (0, -length), (-width / 2, -length * .86)]
    m.slab(offset_polygon(list(reversed(cloth)), .1), frame_on(at - n * .03 - UP * .08, n), 0, .04, GG)
    m.slab(cloth, frame_on(at - UP * .08, n), 0, .05, BLUE)
    crown_badge(m, frame_on(at + n * .051 - UP * (length * .5), n), crown, GG, GG)


def planter(deco, p, along, gnd, cones=2):
    ang = math.atan2(along.y, along.x)
    pf = Matrix.Translation((p.x, p.y, gnd)) @ Matrix.Rotation(ang, 4, "Z")
    vs = deco.slab([(-1.0, -.5), (1.0, -.5), (1.0, .5), (-1.0, .5)], pf, 0, .72, GS2)
    chamfer(deco, vs, .04, GS2)
    deco.slab([(-.88, -.38), (.88, -.38), (.88, .38), (-.88, .38)], pf, .72, .76, M["soil"])
    for e in ((-.45, .45) if cones == 2 else (0,)):
        q = pf @ Vector((e, 0, 0))
        deco.lathe([(0, .74), (.42, .8), (.36, 1.4), (.22, 2.1), (.08, 2.55), (0, 2.65)],
                   Matrix.Translation((q.x, q.y, gnd)), M["leaf3"], 16)


# ------------------------------------------------------------------ a wall between two towers
def wall_segment(walls, deco, a, b, ra, rb, rnd, us=None):
    """The wall from a to b (tower radii ra, rb; None: no tower there, the wall starts at u = us)."""
    A, B = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    L = (B - A).length
    d = (B - A) / L
    n = Vector((-d.y, d.x, 0))
    mid = (A + B) / 2
    if n.dot(CENTRE - mid) < 0:
        n = -n
    us = (math.sqrt(max(0., ra * ra - T * T)) - .3) if ra else (-.3 if us is None else us)
    ue = L - ((math.sqrt(max(0., rb * rb - T * T)) - .3) if rb else -.3)
    samples = [us + (ue - us) * i / 15 for i in range(16)]
    g_in = [ground_at(*(A + d * u + n * (T + .35)).xy) for u in samples]
    v0_in, v0_out = min(g_in) - .35, -.35
    top = WALK - .36
    fin = face_frame_u(A + n * T, d, n)
    fout = face_frame_u(A - n * T, d, -n)
    # R474: one set of courses for both faces, so the arches' windows go through the wall course for course.
    bounds = course_bounds(min(v0_in, v0_out), top)
    openings = []
    span = ue - us
    a_half, ring = 2.2, .85
    n_arch = max(0, int((span - 1.0) / 9.0)) or (1 if span > 6.6 else 0)
    for i in range(n_arch):
        c = us + span * (i + .5) / n_arch
        gs = {round(ground_at(*(A + d * u + n * (T + .6)).xy), 2) for u in (c - a_half - 1, c, c + a_half + 1)}
        if len(gs) != 1:
            continue
        gnd = gs.pop()
        lift = .3 if gnd >= BANK else .6                     # R474: over the moat too, its sill 0.6 m over the water
        ks = next((k for k, z in enumerate(bounds) if z >= gnd + lift), None)
        if ks is None:
            continue
        room = (top - 1.0) - (a_half + ring) - bounds[ks]
        kp = next((k for k, z in enumerate(bounds) if z - bounds[ks] >= min(5.2, room) - 1e-6), None)
        if kp is None or bounds[kp] - bounds[ks] < 2.0 or bounds[kp] + a_half + ring > top - .9:
            kp = max((k for k, z in enumerate(bounds) if z + a_half + ring <= top - .9), default=None)
            if kp is None or bounds[kp] - bounds[ks] < 2.0:
                continue
        openings.append(dict(c=c, a=a_half, sill=bounds[ks], k_sill=ks, spring=bounds[kp], ring=ring, R=a_half + ring,
                             ql=1.2, qs=.65))
        ARCHES.append((A + d * c + n * T, n, d, bounds[ks], bounds[kp]))
    build_face(walls, fin, us, ue, bounds, openings, .42, 2.0, .06, rnd, back=T + .01)
    build_face(walls, fout, us, ue, bounds, openings, .3, 2.4, .05, rnd, .5, back=T - .01)
    for o in openings:
        arch_parts(walls, fin, o, rnd, True, T + .01)
        arch_parts(walls, fout, o, rnd, False, T - .01)
    # The mortar core 9 cm behind both faces (R474: the joints show it, warm, as the gate's do - it stood 1.6 m back
    # and they read black), holed where the windows go through.
    for poly in core_outline(us, ue, bounds[0] - .1, top - .02, openings):     # under the corbels' tops
        walls.slab(poly, fin, -(2 * T - .09), -.09, GJ)
    band = [A + d * us + n * (T + .22), A + d * ue + n * (T + .22), A + d * ue - n * (T + .22), A + d * us - n * (T + .22)]
    vs = walls.slab([(p.x, p.y) for p in band], Matrix.Identity(4), top, WALK, GS)
    round_face_edges(walls, vs, Matrix.Identity(4), (WALK, top), .05, GS, False)
    u = us + .55
    while u < ue - .5:
        block(walls, [(u - .15, top - .48), (u + .15, top - .48), (u + .15, top), (u - .15, top)], fin, -.1, .2, GS, .03)
        u += 1.15
    # Merlons, some chipped, not all quite the same height; clear of the towers' shafts (R474: inside them their tops
    # had met the towers' ring beds in one plane).
    u = (ra + .05) if ra else us + .1
    u_end = (L - rb - .05) if rb else ue
    while u + .95 <= u_end:
        hm = 1.25 + rnd.uniform(-.04, .03)
        poly = [(u, WALK), (u + .95, WALK), (u + .95, WALK + hm), (u, WALK + hm)]
        if rnd.random() < .28:
            poly = chip(poly, rnd, (2, 3))
        block(walls, poly, fin, -(2 * T + .03), .03, GS, .05, True)
        u += .95 + .62
    # A banner beside the arch where there is room (or in the middle when there is none).
    spots = []
    if openings:
        edges = [us + .6] + [x for o in openings for x in (o["c"] - o["R"] - .25, o["c"] + o["R"] + .25)] + [ue - .6]
        gaps = [(x1 - x0, (x0 + x1) / 2) for x0, x1 in zip(edges[::2], edges[1::2])]
        best = max(gaps)
        if best[0] > 2.1:
            spots.append(best[1])
    elif span > 5:
        spots.append((us + ue) / 2)
    for c in spots:
        at = A + d * c + n * (T + .26)
        at.z = top - .75
        banner(deco, at, n, 1.4, 3.6, .45)


# ------------------------------------------------------------------ a round tower
def ring_block(m, c, r_in, r_out, t0, t1, zb, zt, mat, bev, top_t=None):
    """A curved block; top_t (t0, t1) gives its top other ends than its bed (a slanted end: a crack)."""
    t0t, t1t = top_t if top_t else (t0, t1)
    steps = max(2, int((t1 - t0) * r_out / .6))
    rows, vs, curved = [], [], []
    for j in range(steps + 1):
        tb = t0 + (t1 - t0) * j / steps
        tt = t0t + (t1t - t0t) * j / steps
        r = [m.bm.verts.new((c.x + rr * math.cos(t), c.y + rr * math.sin(t), z))
             for rr, z, t in ((r_in, zb, tb), (r_out, zb, tb), (r_out, zt, tt), (r_in, zt, tt))]
        rows.append(r)
        vs += r
    for j in range(steps):
        for q in range(4):
            f = m.bm.faces.new((rows[j][q], rows[j][(q + 1) % 4], rows[j + 1][(q + 1) % 4], rows[j + 1][q]))
            if q in (1, 3):
                curved.append(f)
    m.bm.faces.new(rows[0])
    m.bm.faces.new(list(reversed(rows[-1])))
    m.tag(vs, mat)
    outer = {v for v in vs if (v.co.xy - c.xy).length >= r_out - 1e-3}
    # Its face's bed, top and upright ends rounded in two segments as the gate's blocks (R474), a crack's slanted end
    # left a sharp break.
    edges = [e for e in {e for v in outer for e in v.link_edges} if e.verts[0] in outer and e.verts[1] in outer
             and len(e.link_faces) == 2 and e.calc_face_angle(0) > .5
             and (abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-4 or (e.verts[0].co.xy - e.verts[1].co.xy).length < 1e-3)]
    if edges and bev > 0:
        r = bmesh.ops.bevel(m.bm, geom=edges, offset=bev, segments=2, affect="EDGES", profile=.5, clamp_overlap=True)
        i = m.mi(mat)
        for f in r["faces"]:
            f.material_index = i
            f.smooth = True
    for f in curved:
        if f.is_valid:
            f.smooth = True


def rings(m, c, R, z0, z1, rnd, length=2.0, depth=.3, course=1.3, wear=1.0):
    bounds = course_bounds(z0, z1, course)
    prev = []
    g = .012
    for k in range(len(bounds) - 1):
        zb, zt = bounds[k] + g, bounds[k + 1] - g
        n = max(6, round(2 * math.pi * R / length))
        best = None
        for _ in range(16):
            w = [rnd.uniform(.75, 1.3) for _ in range(n)]
            tot = sum(w)
            off = rnd.uniform(0, 2 * math.pi)
            cuts, acc = [], off
            for wi in w:
                cuts.append(acc)
                acc += 2 * math.pi * wi / tot
            worst = min(min((abs(math.remainder(a - p, 2 * math.pi)) * R for p in prev), default=9.) for a in cuts)
            if best is None or worst > best[0]:
                best = (worst, cuts)
            if worst > .25:
                break
        cuts = best[1]
        for i, t0 in enumerate(cuts):
            t1 = cuts[(i + 1) % n] if i + 1 < n else cuts[0] + 2 * math.pi
            gap = g / R
            a, b = t0 + gap, t1 - gap
            mat = GS                                         # R474: one stone, as the gate's
            ro = R + rnd.uniform(0, .014)
            if wear and rnd.random() < .08 and (b - a) * R > .8:
                # A crack across the block, slanting, dark inside (in front of the mortar core at R - .09).
                tm = a + (b - a) * rnd.uniform(.35, .65)
                tt = tm + rnd.uniform(-.25, .25) / R
                cg = .012 / R
                ring_block(m, c, R - depth, ro, a, tm - cg, zb, zt, mat, .06, (a, tt - cg))
                ring_block(m, c, R - depth, ro + .003, tm + cg, b, zb, zt, mat, .06, (tt + cg, b))
                w = .03 / R
                ring_block(m, c, R - .14, R - .05, tm - w, tm + w, zb + .01, zt - .01, SHADE, 0, (tt - w, tt + w))
            else:
                ring_block(m, c, R - depth, ro, a, b, zb, zt, mat, .06)
        prev = cuts


def tower(towers, roofs, deco, x, y, R, rnd):
    c = Vector((x, y, 0))
    din = CENTRE - c
    din.z = 0
    din.normalize()
    around = [Vector((math.cos(a), math.sin(a), 0)) for a in (2 * math.pi * i / 16 for i in range(16))]
    gr = [ground_at(*(c + v * (R + .4)).xy) if v.dot(din) > -.2 else 0. for v in around]
    base = min(gr) - .37                                     # (not -0.9: the terrace runs' foot, R474)
    plinth = max(gr) + .9
    t = Matrix.Translation(c)
    towers.lathe([(0, base), (R + .4, base), (R + .4, plinth - .4), (R + .13, plinth), (0, plinth)], t, GS2, 36)
    # The mortar core 9 cm behind the blocks' faces (R474: the joints show it warm, as the gate's; was 25 cm back).
    towers.lathe([(0, plinth - .1), (R - .09, plinth - .1), (R - .09, EAVE + .1), (0, EAVE + .1)], t, GJ, 48)
    rings(towers, c, R, plinth, EAVE, rnd)
    ring_z = [b + e for b in course_bounds(plinth, EAVE, 1.3) for e in (-.012, .012)]
    # The band at the wall walk, 2 cm proud of the walk above and below (R474: its top and bottom had lain in the walk
    # band's planes where the walls run into the towers).
    towers.lathe([(0, WALK - .38), (R + .2, WALK - .38), (R + .24, WALK - .32), (R + .24, WALK - .04),
                  (R + .2, WALK + .02), (0, WALK + .02)], t, GS2, 36)
    side = UP.cross(din).normalized()

    def clear_of_rings(z, h):
        """z nudged so a window frame's beds keep 6 mm off the ring blocks' beds (not in their planes, R474)."""
        for dz in (0, .02, -.02, .04, -.04, .06, -.06):
            if all(abs(z + dz + e - r) > .006 for e in (-h / 2 - .1, -h / 2, h / 2, h / 2 + .1) for r in ring_z):
                return z + dz
        return z

    def window(v, z, w, h, frame_mat=GS2):
        at = c + v * (R + .015)
        at.z = z
        f = frame_on(at, v)
        towers.slab([(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)], f, 0, .02, SHADE)
        fw = .1
        for poly in ([(-w / 2 - fw, -h / 2 - fw), (w / 2 + fw, -h / 2 - fw), (w / 2 + fw, -h / 2), (-w / 2 - fw, -h / 2)],
                     [(-w / 2 - fw, h / 2), (w / 2 + fw, h / 2), (w / 2 + fw, h / 2 + fw), (-w / 2 - fw, h / 2 + fw)],
                     [(-w / 2 - fw, -h / 2), (-w / 2, -h / 2), (-w / 2, h / 2), (-w / 2 - fw, h / 2)],
                     [(w / 2, -h / 2), (w / 2 + fw, -h / 2), (w / 2 + fw, h / 2), (w / 2, h / 2)]):
            block(towers, poly, f, -.04, .07, frame_mat, .02)
    # Slits on the garden side and outside, the ring of small windows under the eave.
    for v in (din * math.cos(.5) + side * math.sin(.5), din * math.cos(-.5) + side * math.sin(-.5), -din):
        window(v.normalized(), clear_of_rings(WALK + 1.9, .9), .2, .9)
    for i in range(6):
        a = 2 * math.pi * i / 6 + math.pi / 6
        v = din * math.cos(a) + side * math.sin(a)
        window(v, clear_of_rings(EAVE - 1.1, .5), .34, .5)
    # The eave and the roof: a pointed slate cone (1.2 x its width high), a gold band, finial, pole, red pennant.
    towers.lathe([(0, EAVE), (R + .28, EAVE), (R + .34, EAVE + .1), (R + .34, EAVE + .3), (R + .26, EAVE + .38),
                  (0, EAVE + .38)], t, GS2, 36)
    z0 = EAVE + .38
    Rr = R + .42
    Hr = 1.2 * 2 * Rr
    prof = [(0, z0), (Rr + .08, z0)]
    rows = 9
    for i in range(rows):
        ta, tb = i / rows, (i + 1) / rows
        ra, rb = Rr * (1 - ta) ** 1.05, Rr * (1 - tb) ** 1.05
        za, zb = z0 + Hr * ta, z0 + Hr * tb
        prof += [(ra + .08, za + .02), (rb + .02, zb - .02)]
    prof += [(0, z0 + Hr)]
    roofs.lathe(prof, t, BLUE, 36)
    roofs.lathe([(0, z0 - .12), (Rr + .14, z0 - .12), (Rr + .18, z0 - .02), (Rr + .14, z0 + .1), (0, z0 + .1)], t, GG, 36)
    apex = z0 + Hr
    roofs.lathe([(0, apex - .4), (.17, apex - .4), (.22, apex - .18), (.1, apex + .05), (.14, apex + .18), (0, apex + .28)], t, GG, 14)
    roofs.sphere((x, y, apex + .45), .18, GG, subdiv=2)
    roofs.lathe([(0, apex + .55), (.055, apex + .55), (.045, apex + 2.7), (0, apex + 2.7)], t, GG, 8)
    roofs.sphere((x, y, apex + 2.78), .11, GG, subdiv=1)
    pen = [(0, 0), (1.8, .28), (0, .58)]
    pf = Matrix.Translation((x + .05, y, apex + 2.0)) @ Matrix.Rotation(math.radians(rnd.uniform(-14, 14)), 4, "Z") \
        @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    roofs.slab(pen, pf, -.03, .03, RED)
    # The banner on the garden side, a planter before the tower where there is level ground.
    at = c + din * (R + .3)
    at.z = EAVE - 1.9
    banner(deco, at, din, 1.5, 4.0, .5)
    p = c + din * (R + 1.35)
    gs = {round(ground_at(*(p + side * e).xy), 2) for e in (-1.1, 0, 1.1)}
    if len(gs) == 1:
        gnd = gs.pop()
        if gnd >= BANK and ground_at(*(p + din * .7).xy) == gnd:
            planter(deco, p, side, gnd)


# ------------------------------------------------------------------ R481: 승규's tower asset in place of tower()
# (승규 "기둥 쪽은 이렇게 만들고 에셋 교체 해서 자연스럽게 택스쳐 입혀 빨간색 깃발은 제거해"): tower_asset.py cleans the
# Tripo tower (no flag, the garden's materials by part, ~18k faces) into TowerAsset_v1.blend; each tower is a copy, its
# front (the asset's +x: the vine, the bushes) toward the garden, its pillar and wall stub along the walls (every other
# one mirrored, so the pillar is not always on the same side), 1.1 x the old towers' girth (shaft r 0.102 -> R) and
# tall enough that its eave is near 18 m (the walls' merlons at 12.3; its pillar's top meets them), a little turned and
# sized at random; a stone footing down into the ground or the water under it.
TOWER_ASSET = os.path.join(HERE, "TowerAsset_v1.blend")
ASSET_SHAFT_R, ASSET_EAVE = .102, .62
_asset_mesh = []


def tower_asset_mesh():
    if not _asset_mesh:
        with bpy.data.libraries.load(TOWER_ASSET, link=False) as (src, dst):
            dst.meshes = [n for n in src.meshes]
        me = max(dst.meshes, key=lambda m: len(m.polygons))
        for i, m in enumerate(me.materials):
            base = m.name.rsplit(".", 1)[0] if m.name[-4:-3] == "." else m.name
            if base != m.name and base in bpy.data.materials:
                me.materials[i] = bpy.data.materials[base]       # the garden's own material of that name
        _asset_mesh.append(me)
    return _asset_mesh[0]


def asset_tower(towers, x, y, R, rnd, index, number):
    c = Vector((x, y, 0))
    din = CENTRE - c
    din.z = 0
    din.normalize()
    side = UP.cross(din).normalized()
    around = [Vector((math.cos(a), math.sin(a), 0)) for a in (2 * math.pi * i / 16 for i in range(16))]
    gr = [ground_at(*(c + v * (R + .6)).xy) for v in around]
    front = [ground_at(*(c + v * (R + 1.0)).xy) for v in around if v.dot(din) > .5]
    base = max(front)
    sxy = 1.1 * R / ASSET_SHAFT_R * rnd.uniform(.97, 1.03)
    sz = min((18.0 - base) / ASSET_EAVE, 1.35 * sxy)
    mirror = -1 if index % 2 else 1
    turn = Matrix.Rotation(math.radians(rnd.uniform(-4, 4)), 4, "Z")
    axes = Matrix((din, side * mirror, UP)).transposed().to_4x4()
    m = Matrix.Translation((x, y, base)) @ turn @ axes @ Matrix.Diagonal((sxy, sxy, sz, 1))
    me = tower_asset_mesh().copy()
    me.transform(m)
    if m.determinant() < 0:
        me.flip_normals()
    # smooth shading but for edges sharper than 40 deg (the decimated surface read as facets)
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(40))
    me.update()
    uv = me.uv_layers.new(name="UVMap")
    for p in me.polygons:
        n = p.normal
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (co.x, co.y) if abs(n.z) > .7 else ((co.y, co.z) if abs(n.x) > abs(n.y) else (co.x, co.z))
    name = "Castle tower %s%d" % ("E" if x > 0 else "W", number)
    me.name = name
    ob = bpy.data.objects.new(name, me)
    DRESS.objects.link(ob)
    # its footing: stone from under the ground (or the water) up into the asset's foot
    towers.lathe([(0, min(gr) - .5), (R + .25, min(gr) - .5), (R + .25, base + .2), (0, base + .2)],
                 Matrix.Translation(c), GS, 36)


# ------------------------------------------------------------------ build
walls = Mesh("Castle walls")
towers = Mesh("Castle towers")
roofs = Mesh("Castle roofs")
deco = Mesh("Castle banners and planters")
ground = Mesh("Castle grounds")
lawn = Mesh("Castle lawn")

east_pts = [(WING.x, WING.y, None)] + EAST
poly = [(x, y) for x, y, _ in east_pts] + [(-x, y) for x, y, _ in reversed(east_pts)]
ccw = poly if area(poly) > 0 else list(reversed(poly))
for s in (1, -1):
    pts = [(s * x, y, r) for x, y, r in east_pts]
    for (ax, ay, ar), (bx, by, br) in zip(pts, pts[1:]):
        wall_segment(walls, deco, (ax, ay), (bx, by), ar, br, RND, WING_US if ar is None else None)
    for i, (x, y, r) in enumerate(pts[1:]):
        asset_tower(towers, x, y, r, RND, i + (s < 0), i + 1)    # R481 (was tower(): our own towers)
    # The banks past the water's edge, on a block quay.
    side = clip(clip(clip(ccw, 0, s * WATER_EDGE, s > 0), 1, 59.0, False), 1, -61.5, True)
    if len(side) >= 3:
        pave(ground, side, BANK)
        body(ground, clip(side, 0, s * (WATER_EDGE + .35), s > 0), -2.4, BANK - .01, M["stone2"])
        ys = [y for x, y in side if abs(x - s * WATER_EDGE) < 1e-6]
        if len(ys) >= 2:
            d0, d1 = (0, .3) if s > 0 else (-.3, 0)
            kit_wall_run(ground, "y", s * WATER_EDGE, min(ys), max(ys), d0, d1, BANK, -2.3, True, 1.0, 2.2)
    # The terraces by the gate and the reservoir (3.8), clipped to the ring, their faces.
    for x0, x1, y0, y1 in ((14.3, 44, 48.6, 59), (26, 44, 33, 48.6)):
        xa, xb = sorted((s * x0, s * x1))
        terr = clip_convex([(xa, y0), (xb, y0), (xb, y1), (xa, y1)], ccw)
        pave(ground, terr, UPPER)
        # its body behind the faces' blocks (not in their planes)
        if y0 == 33:
            inner = clip(clip(terr, 1, 33.35, True), 0, s * 43.65, s < 0)
        else:
            inner = clip(terr, 1, 58.65, False)
        body(ground, inner, 0, UPPER - .01, M["stone2"])
    xr = max(x for x, y in clip(clip(ccw, 1, 59.0, True), 1, 59.0, False) if x * s > 0) if s > 0 else \
        min(x for x, y in clip(clip(ccw, 1, 59.0, True), 1, 59.0, False) if x * s > 0)
    xa, xb = sorted((s * 14.3, xr - s * 1.0))
    kit_wall_run(ground, "x", 59, xa, xb, -.3, 0, UPPER, 2.4)                    # the gate terrace's edge, to the strip
    d0, d1 = (-.3, 0) if s > 0 else (0, .3)
    # the reservoir terrace's end, the trench: from behind the lake face's run, its coping from behind that run's
    # coping (R474: the two had shared the corner, their blocks' and copings' tops in one plane)
    kit_wall_run(ground, "y", s * 44, 33.3, 48.6, d0, d1, UPPER - .17, -.9, False)
    ground.box((s * 44 + d0 - .05, 33.35, UPPER - .17), (s * 44 + d1 + .05, 48.6, UPPER + .012), M["stone3"], .04)
    xa, xb = sorted((s * 35.6, s * 44)) if s > 0 else sorted((s * 44, s * 26))
    kit_wall_run(ground, "x", 33, xa, xb, 0, .3, UPPER, -.9)                     # its face to the lake
    # R488 (승규 Unity picture + "작은 나무 총 8개 지워"): the gate terraces' four potted cone topiaries a side (x +-16.6,
    # z 50.6 .. 58.4) gone - taken out of WallsSample_v2.blend as it stood after the hand-made pass, so nothing else moved.
    # The strip between the yard and the wall (2.8: the old north wall's place).
    for x0, x1, y0 in ((6.0, 14.3, 60.5), (14.3, 25.2, 59.0)):
        xa, xb = sorted((s * x0, s * x1))
        strip = clip_convex([(xa, y0), (xb, y0), (xb, 63.1), (xa, 63.1)], ccw)
        pave(ground, strip, STRIP)
        body(ground, strip, 0, STRIP - .01, M["stone2"])
    north = clip(clip(ccw, 0, s * 25.2, s > 0), 1, 59.0, True)
    pave(ground, north, STRIP)
    body(ground, north, 0, STRIP - .01, M["stone2"])

# The lawn round the castle (26 m out to the section's edge; not on the ways in and out).
nv = len(ccw)
norms = []
for i in range(nv):
    a, b, c_ = Vector(ccw[i - 1] + (0,)), Vector(ccw[i] + (0,)), Vector(ccw[(i + 1) % nv] + (0,))
    e1, e2 = (b - a).normalized(), (c_ - b).normalized()
    n1, n2 = Vector((e1.y, -e1.x, 0)), Vector((e2.y, -e2.x, 0))
    nn = (n1 + n2).normalized()
    norms.append(nn / max(.4, nn.dot(n2)))
for i in range(nv):
    j = (i + 1) % nv
    p, q = Vector(ccw[i] + (0,)), Vector(ccw[j] + (0,))
    mid = (p + q) / 2
    if (abs(mid.x) < 12 and mid.y < -55) or (abs(mid.x) < 25 and mid.y > 60):
        continue                                                     # the way in, the gate
    quad = [tuple(p.xy), tuple(q.xy), tuple((q + norms[j] * 26).xy), tuple((p + norms[i] * 26).xy)]
    quad = clip(clip(clip(clip(quad, 1, -64.0, True), 1, 71.5, False), 0, -60.0, True), 0, 60.0, False)
    if len(quad) >= 3:
        pave(lawn, quad, -.02, LAWN)
        body(lawn, quad, -.7, -.03, LAWN)

for mesh in (walls, towers, roofs, deco, ground, lawn):
    if mesh.bm.faces:                                              # R481: the roofs come with the asset
        mesh.done()

# ------------------------------------------------------------------ the context: the garden's other dressings
ctx = Mesh("Context water", CTX)
ctx.box((-26, 33, RES - .05), (26, 48.6, RES), material("Context | reservoir", (.03, .5, .78), .06))
ctx.done()


def append_dressing(blend, name):
    path = os.path.join(HERE, blend)
    if not os.path.exists(path):
        return
    with bpy.data.libraries.load(path, link=False) as (src, dst):
        dst.collections = [c for c in src.collections if c == name]
    for c in dst.collections:
        if c is not None:
            c.name = "Context | " + name
            CTX.children.link(c)


for blend, name in (("PlazaSample_v4.blend", "Plaza dressing"), ("CanalSample_v2.blend", "Canal dressing"),
                    ("PlatformsSample_v3.blend", "Platforms dressing"), ("LockDykeSample_v1.blend", "Lock dressing"),
                    ("BoatsSample_v2.blend", "Boats dressing"), ("WaterSample_v2.blend", "Water dressing"),
                    ("GateSample_v2.blend", "Gate dressing")):
    append_dressing(blend, name)

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((0, 57.5, UPPER), (50.0, 4.2, BANK), (21.0, 61.0, STRIP)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "WallsSample_v2.blend"))
print("TOWERS", len(EAST) * 2, "WING", tuple(round(v, 2) for v in WING.xy), "US %.3f" % WING_US)
for x, y, r in EAST:
    print("  TOWER (%.2f, %.2f) r %.1f" % (x, y, r))
print("ARCHES", len(ARCHES))


def inward(p):
    v = CENTRE - p
    v.z = 0
    return v.normalized()


# The cameras on the second tower, the east arch nearest the middle, a wall face by the third tower (R474: they follow
# the towers), the wing junction from outside, the first tower on the terraces' corner.
t2 = Vector((EAST[1][0], EAST[1][1], 0))
ap, an, ad, a_sill, _ = min((a for a in ARCHES if a[0].x > 0), key=lambda a: abs(a[0].y - 4))
t3, t4 = Vector((EAST[2][0], EAST[2][1], 0)), Vector((EAST[3][0], EAST[3][1], 0))
d34 = (t4 - t3).normalized()
wp = t3 + d34 * 4.5 + inward((t3 + t4) / 2) * T + UP * 3.4
for name, at, look, lens in (("overview", (0, -125, 100), (0, 4, 0), 30),
                             ("top", (0, 4, 210), (0, 4, 0), 35),
                             ("north", (0, 18, 16), (0, 62, 14), 22),
                             ("reference", (-14, 30, 13), (18, 58, 12), 24),
                             ("side", (34, 2, 8), (53.5, 4, 8), 26),
                             ("arch", ap + an * 8.5 - ad * 2.0 + UP * (a_sill + 1.6), ap + UP * (a_sill + 2.6), 28),
                             ("tower", t2 + inward(t2) * 10 + UP * 11, t2 + UP * 13, 28),
                             ("wear", wp + inward((t3 + t4) / 2) * 5.5 + d34 * 1.2, wp, 30),
                             ("south", (0, -40, 9), (0, -62, 10), 26),
                             ("junction", (33, 76, 9), (24.8, 67.2, 8), 26),
                             ("corner", (36, 40, 7.5), (44.2, 47.5, 4.8), 26),
                             # R474: the same window from outside (it goes through the wall)
                             ("outside", ap - an * (2 * T + 9) - ad * 2.0 + UP * (a_sill + 1.6),
                              ap - an * 2 * T + UP * (a_sill + 2.6), 28)):
    shoot("walls_v2", name, tuple(at), tuple(look), lens)
print("WALLS SAMPLE DONE")

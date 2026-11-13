# Royal water garden - concept-art dressing, sample 8 v1: the castle walls round the garden (R472).
#
# 승규 11-05, with a picture of a castle wall (an arc of crenellated walls with round arches, round towers with blue
# cone roofs and red pennants, crowned blue banners, planters with cone topiaries, the gate in the middle):
# "성벽 외관 부야 정문은 만들 었으니까 벽만 해 직사각형 모양으로 하지 말고 저런 모양으로" - after "너가 블렌더 하는 건 너무 ai
# 티가 나 모서리 처리 부분 이음새 부분". So:
#   the plan     not the graybox's rectangle (walls x +-46.5..48.5, z -63..62, corner towers): a ring of round towers on
#                a convex curve with straight walls between, from the gate's wings (z 66.5; the gate is the wall's middle
#                as in the picture) round both sides (out to x +-50.4) to two towers flanking the way in (x +-9.3). The old
#                north wall between the yard and the gate goes (gate_sample_v2.py: a balustrade on the yard's edge).
#   the masonry  nothing lines up as if stamped: courses of blocks of random length (0.7 .. 1.35 of the face's block
#                length, no joint within 25 cm of one in the course below), chamfered edges, up to 1.5 cm of random
#                proudness, two barely different stones (the gate's); a darker mortar core behind the joints. Walls meet
#                only towers (their ends inside the towers), so the wall has no corners of its own; the towers are rings
#                of curved blocks staggered the same way. Arches: quoins alternating long and short through the courses
#                (the jambs bond into the wall), radial voussoirs, the courses above cut along the arch's back.
#   the walls    2.4 m thick, a plinth course, the wall walk at 11.0 (a projecting string course on corbels), merlons;
#                on the garden side blind arches (a dark back 0.9 m in, a sill) where the ground in front is level
#                (the banks, the strip by the gate), crowned banners between
#   the towers   a battered plinth, the shaft to 15.2, a band at the wall walk, a ring of corbels, a wider head with four
#                small windows, an eave, a blue cone roof in rows of slates with a gold band, a gold finial, pole and a red
#                pennant; a crowned banner on the garden side; a planter with a cone topiary where there is ground
#   the ground   where the wall stands out past the water's edge (x +-45.8) a paved bank on a block quay; between the yard
#                and the gate (z 59 / 60.5 .. the wall) a paved strip at the gate podium's height (2.8)
# Stone, joints, gold and blue are the gate's materials (same names: one Unity material each).
# All faces outward and no two faces in one plane (PITFALLS 38, 39).
#
# Run:  blender -b --python walls_sample_v1.py
# Out:  WallsSample_v1.blend, render_walls_v1_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Walls dressing"
os.environ["GC_SEED"] = "88"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403
import bmesh  # noqa: E402
import water_tiers as wt  # noqa: E402

RND = random.Random(468)
UPPER = 3.8
LAKE, MOAT, RES = wt.MIDDLE, wt.EARLY, wt.LATE
STRIP = UPPER - 1.0                      # the gate podium's height: the strip between the yard and the gate
BANK = .6
WATER_EDGE = 45.8                        # the bank's quay (the water sheets end at 46.5, under it)


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
GS = textured("GC | gate stone", GATE_TEX, (1.0, 1.0, 1.0), .72, .5)
GS2 = textured("GC | gate stone warm", GATE_TEX, (.975, .952, .935), .74, .5)
GJ = material("GC | gate joint", (.80, .65, .53), .85)
GG = material("GC | gate gold", (.98, .82, .30), .3, .75)
BLUE = material("GC | gate blue", (.18, .38, .78), .55)
RED = material("GC | pennant red", (.86, .14, .12), .55)
SHADE = material("GC | arch shade", (.12, .11, .13), .9)

# ------------------------------------------------------------------ the plan
T = 1.2                 # half the walls' thickness
WALK = 11.0             # the wall walk (the string course's top)
COURSE = 1.15
SHAFT = 15.2            # the towers' shafts
WING = (24.8, 66.5)     # the gate's wing end (gate_sample_v2.py: the low end block, x 22.6 .. 24.8)
EAST = [(29.6, 65.6, 2.6), (39.5, 62.4, 2.6), (47.6, 55.0, 3.0), (49.6, 36.0, 2.6), (50.4, 14.0, 2.6),
        (50.4, -9.0, 2.6), (49.6, -32.0, 2.6), (46.8, -55.0, 3.0), (34.0, -61.0, 2.6), (20.0, -62.2, 2.6),
        (9.3, -62.2, 2.8)]
CENTRE = Vector((0, 2, 0))
UP = Vector((0, 0, 1))


def ground_at(x, y):
    """The ground (or water) inside the garden at a plan point near the wall."""
    ax = abs(x)
    if y >= 63.1 and ax <= 25.2:
        return STRIP                              # the gate's podium
    if y > 60.5 and ax < 6:
        return UPPER                              # the exit terrace
    if (y > 59 and ax >= 14.3) or (y > 60.5 and ax >= 6):
        return STRIP
    if ax >= WATER_EDGE and -61.5 <= y <= 59:
        return BANK
    if 14.3 <= ax <= 44 and 48.6 <= y <= 59:
        return UPPER                              # the gate's terraces
    if 26 <= ax <= 44 and 33 <= y <= 48.6:
        return UPPER                              # the reservoir's terraces
    return LAKE if y > -15.5 else MOAT


def outside_at(x, y):
    """Outside the wall: the land (0), or the water where the wall cuts across the old rectangle's corners."""
    if abs(x) < 46.5 and -61.5 < y < 60.5:
        return LAKE if y > -15.5 else MOAT
    return 0.


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
    """A wall face's frame: local x along the wall, local y = world height, local z out of the face."""
    return Matrix.Translation(Vector(origin)) @ Matrix((Vector(u_dir), UP, Vector(normal))).transposed().to_4x4()


def chamfer(m, verts, offset, mat):
    """Chamfers a piece's sharp edges (one segment: a rounded look without many faces)."""
    edges = [e for e in {e for v in verts for e in v.link_edges}
             if len(e.link_faces) == 2 and e.calc_face_angle(0) > .5]
    if not edges or offset <= 0:
        return
    r = bmesh.ops.bevel(m.bm, geom=edges, offset=offset, segments=1, affect="EDGES", profile=.5, clamp_overlap=True)
    i = m.mi(mat)
    for f in r["faces"]:
        f.material_index = i
        f.smooth = False


def block(m, poly, frame, d0, d1, mat, bev):
    vs = m.slab(poly, frame, d0, d1, mat)
    chamfer(m, vs, bev, mat)


def loft_face(m, frame, inner, outer, d0, d1, mat, bev):
    """A solid between two curves of a face ((u, v) lists of one length), from depth d0 to d1 (local z)."""
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
    chamfer(m, vs, bev, mat)


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


def pave(m, poly, z):
    """A paved floor over a plan polygon (the kit's tiles)."""
    if len(poly) < 3 or abs(area(poly)) < .05:
        return
    if area(poly) < 0:
        poly = list(reversed(poly))
    vs = [m.bm.verts.new((x, y, z)) for x, y in poly]
    f = m.bm.faces.new(vs)
    m.tag(vs, M["paving"])
    m.fixed[f] = {v: (v.co.x / (TILE_SIZE * 4), v.co.y / (TILE_SIZE * 4)) for v in vs}
    m.no_recalc.add(f)


# ------------------------------------------------------------------ the masonry
def split(s, e, length, rnd, prev):
    """[s, e] in blocks of about `length` (0.7 .. 1.35 of it, filling it exactly), no joint within 25 cm of `prev`."""
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
    """A block [a0, a1] x [zb, zt] less the disc (c, p) radius R: the pieces left (polygons, the cut edge curved)."""
    def w(v):
        d = v - p
        return math.sqrt(R * R - d * d) if abs(d) < R else 0.
    vs = [zb + (zt - zb) * i / steps for i in range(steps + 1)]
    out = []
    if a0 < c:
        edge = [(min(a1, c - w(v)), v) for v in vs]
        if max(x for x, _ in edge) - a0 > .18:
            pts = [(a0, zb)] + [(max(a0 + .02, x), v) for x, v in edge] + [(a0, zt)]
            out.append(pts)
    if a1 > c:
        edge = [(max(a0, c + w(v)), v) for v in vs]
        if a1 - min(x for x, _ in edge) > .18:
            pts = [(a1, zb), (a1, zt)] + [(min(a1 - .02, x), v) for x, v in reversed(edge)]
            out.append(pts)
    return out


def course_bounds(v0, v1, course=COURSE):
    n = max(1, round((v1 - v0) / course))
    h = (v1 - v0) / n
    return [v0 + h * k for k in range(n + 1)]


def build_face(m, frame, us, ue, bounds, openings, deep, length, bev, rnd, plinth=True):
    """One face of a wall in courses (`bounds`), its blocks `deep` into the wall, around `openings` (arches)."""
    prev = []
    g = .012
    for k in range(len(bounds) - 1):
        zb, zt = bounds[k], bounds[k + 1]
        blocked, discs = [], []
        for o in openings:
            if zb >= o["sill"] - 1e-6 and zt <= o["spring"] + 1e-6:
                q = o["ql"] if (k - o["k_sill"]) % 2 == 0 else o["qs"]
                blocked.append((o["c"] - o["a"] - q, o["c"] + o["a"] + q))
                for qa, qb in ((o["c"] - o["a"] - q, o["c"] - o["a"]), (o["c"] + o["a"], o["c"] + o["a"] + q)):
                    block(m, [(qa + g, zb + g), (qb - g, zb + g), (qb - g, zt - g), (qa + g, zt - g)], frame,
                          -(o["nd"] + .3), .03 + rnd.uniform(0, .01), GS2 if rnd.random() < .3 else GS, bev)
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
                dd, mat, bb = deep, (GS2 if rnd.random() < .25 else GS), bev
                if plinth and k == 0:
                    pr, mat, bb = .09 + pr, GS2, bev * 1.4
                pieces = [[(a0 + g, zb + g), (a1 - g, zb + g), (a1 - g, zt - g), (a0 + g, zt - g)]]
                for o in discs:
                    if a1 > o["c"] - o["R"] and a0 < o["c"] + o["R"]:
                        pieces = disc_cut(a0 + g, a1 - g, zb + g, zt - g, o["c"], o["spring"], o["R"] + .02)
                for poly in pieces:
                    block(m, poly, frame, -dd, pr, mat, bb)
        prev = joints


def arch_parts(m, frame, o, rnd):
    """An arch's voussoirs (radial, the keystone proud), its dark back and its sill."""
    c, a, p, ring, nd = o["c"], o["a"], o["spring"], o["ring"], o["nd"]
    n_v = 11
    for k in range(n_v):
        t0, t1 = math.pi * k / n_v + .006, math.pi * (k + 1) / n_v - .006
        ts = [t0 + (t1 - t0) * i / 3 for i in range(4)]
        key = k == n_v // 2
        ro = a + ring + (.12 if key else 0)
        inner = [(c + a * math.cos(t), p + a * math.sin(t)) for t in ts]
        outer = [(c + ro * math.cos(t), p + ro * math.sin(t)) for t in ts]
        loft_face(m, frame, inner, outer, -(nd + .3), .05 + (.06 if key else rnd.uniform(0, .015)),
                  GS2 if key or rnd.random() < .3 else GS, .04)
    back = [(c - a, o["sill"]), (c + a, o["sill"])] + [(c + a * math.cos(t), p + a * math.sin(t))
                                                       for t in (math.pi * i / 20 for i in range(21))]
    m.slab(back, frame, -(nd + .1), -nd, SHADE)
    block(m, [(c - a, o["sill"]), (c + a, o["sill"]), (c + a, o["sill"] + .22), (c - a, o["sill"] + .22)], frame,
          -nd, .07, GS2, .04)


def banner(m, at, n, width, length, crown=.5):
    """A crowned royal blue banner hanging from a gold rod at `at` (its top middle), facing n."""
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


# ------------------------------------------------------------------ a wall between two towers
def wall_segment(walls, deco, a, b, ra, rb, rnd):
    A, B = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
    L = (B - A).length
    d = (B - A) / L
    n = Vector((-d.y, d.x, 0))
    mid = (A + B) / 2
    if n.dot(CENTRE - mid) < 0:
        n = -n                                    # n: toward the garden
    us = (math.sqrt(max(0., ra * ra - T * T)) - .3) if ra else -.3
    ue = L - ((math.sqrt(max(0., rb * rb - T * T)) - .3) if rb else -.3)
    samples = [us + (ue - us) * i / 15 for i in range(16)]
    g_in = [ground_at(*(A + d * u + n * (T + .35)).xy) for u in samples]
    g_out = [outside_at(*(A + d * u - n * (T + .35)).xy) for u in samples]
    v0_in, v0_out = min(g_in) - .35, min(g_out) - .35
    top = WALK - .36
    fin = face_frame_u(A + n * T, d, n)
    fout = face_frame_u(A - n * T, d, -n)
    b_in = course_bounds(v0_in, top)
    # Blind arches on the garden side, where the ground before them is level.
    openings = []
    span = ue - us
    n_arch = int((span - 1.5) / 8.0)
    a_half, ring, nd = 2.0, .8, .9
    for i in range(n_arch):
        c = us + span * (i + .5) / n_arch
        gs = {round(ground_at(*(A + d * u + n * (T + .6)).xy), 2) for u in (c - a_half - 1, c, c + a_half + 1)}
        if len(gs) != 1:
            continue
        gnd = gs.pop()
        if gnd < BANK:
            continue
        ks = next((k for k, z in enumerate(b_in) if z >= gnd + .35), None)
        if ks is None:
            continue
        kp = next((k for k, z in enumerate(b_in) if z >= b_in[ks] + 3.3), None)
        if kp is None or b_in[kp] + a_half + ring > top - .9:
            continue
        openings.append(dict(c=c, a=a_half, sill=b_in[ks], k_sill=ks, spring=b_in[kp], ring=ring, R=a_half + ring,
                             ql=1.15, qs=.62, nd=nd))
    build_face(walls, fin, us, ue, b_in, openings, .42, 1.7, .045, rnd)
    build_face(walls, fout, us, ue, course_bounds(v0_out, top), [], .3, 2.4, .03, rnd)
    for o in openings:
        arch_parts(walls, fin, o, rnd)
    # The core behind the faces (its front 1 m in from the garden face, behind the arches' backs).
    core = [A + d * us + n * (T - 1.0), A + d * ue + n * (T - 1.0), A + d * ue - n * (T - .3), A + d * us - n * (T - .3)]
    walls.slab([(p.x, p.y) for p in core], Matrix.Identity(4), min(v0_in, v0_out) - .1, top, GJ)
    # The string course (the wall walk), its corbels on the garden side, the merlons.
    band = [A + d * us + n * (T + .22), A + d * ue + n * (T + .22), A + d * ue - n * (T + .22), A + d * us - n * (T + .22)]
    vs = walls.slab([(p.x, p.y) for p in band], Matrix.Identity(4), top, WALK, GS2)
    chamfer(walls, vs, .05, GS2)
    u = us + .55
    while u < ue - .5:
        block(walls, [(u - .15, top - .48), (u + .15, top - .48), (u + .15, top), (u - .15, top)], fin, -.1, .2, GS2, .03)
        u += 1.15
    u = us + .1
    while u + .95 <= ue:
        mer = [A + d * u + n * (T + .03), A + d * (u + .95) + n * (T + .03), A + d * (u + .95) - n * (T + .03),
               A + d * u - n * (T + .03)]
        vs = walls.slab([(p.x, p.y) for p in mer], Matrix.Identity(4), WALK, WALK + 1.25 + rnd.uniform(-.02, .02), GS)
        chamfer(walls, vs, .05, GS)
        u += .95 + .62
    # Banners on the garden side, between the arches (or in the middle when there are none).
    spots = []
    if openings:
        edges = [us + .8] + [x for o in openings for x in (o["c"] - o["R"] - .3, o["c"] + o["R"] + .3)] + [ue - .8]
        for x0, x1 in zip(edges[::2], edges[1::2]):
            if x1 - x0 > 2.4:
                spots.append((x0 + x1) / 2)
    elif span > 5:
        spots.append((us + ue) / 2)
    for c in spots:
        at = A + d * c + n * (T + .26)
        at.z = top - .75
        banner(deco, at, n, 1.4, 3.6, .45)


# ------------------------------------------------------------------ a round tower
def ring_block(m, c, r_in, r_out, t0, t1, zb, zt, mat, bev):
    steps = max(2, int((t1 - t0) * r_out / .6))
    rows = []
    vs = []
    for j in range(steps + 1):
        t = t0 + (t1 - t0) * j / steps
        cs, sn = math.cos(t), math.sin(t)
        r = [m.bm.verts.new((c.x + rr * cs, c.y + rr * sn, z)) for rr, z in ((r_in, zb), (r_out, zb), (r_out, zt), (r_in, zt))]
        rows.append(r)
        vs += r
    curved = []
    for j in range(steps):
        for q in range(4):
            f = m.bm.faces.new((rows[j][q], rows[j][(q + 1) % 4], rows[j + 1][(q + 1) % 4], rows[j + 1][q]))
            if q in (1, 3):
                curved.append(f)        # the outer (q 1) and inner (q 3) faces: smooth; the bed, top and ends flat
    m.bm.faces.new(rows[0])
    m.bm.faces.new(list(reversed(rows[-1])))
    m.tag(vs, mat)
    # Only the edges round the outer face are chamfered (the rest is inside the tower).
    outer = {v for v in vs if (v.co.xy - c.xy).length >= r_out - 1e-3}
    edges = [e for e in {e for v in outer for e in v.link_edges} if e.verts[0] in outer and e.verts[1] in outer
             and len(e.link_faces) == 2 and e.calc_face_angle(0) > .5]
    if edges and bev > 0:
        r = bmesh.ops.bevel(m.bm, geom=edges, offset=bev, segments=1, affect="EDGES", profile=.5, clamp_overlap=True)
        i = m.mi(mat)
        for f in r["faces"]:
            f.material_index = i
    for f in curved:
        if f.is_valid:
            f.smooth = True


def rings(m, c, R, z0, z1, rnd, length=1.7, depth=.3, course=COURSE):
    """Courses of curved blocks round a tower, staggered (no joint within 25 cm of one below)."""
    bounds = course_bounds(z0, z1, course)
    prev = []
    g = .012
    for k in range(len(bounds) - 1):
        zb, zt = bounds[k] + g, bounds[k + 1] - g
        circ = 2 * math.pi * R
        n = max(6, round(circ / length))
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
            ring_block(m, c, R - depth, R + rnd.uniform(0, .014), t0 + gap, t1 - gap, zb, zt,
                       GS2 if rnd.random() < .25 else GS, .04)
        prev = cuts


def tower(towers, roofs, deco, x, y, R, rnd):
    c = Vector((x, y, 0))
    din = CENTRE - c
    din.z = 0
    din.normalize()
    around = [Vector((math.cos(a), math.sin(a), 0)) for a in (2 * math.pi * i / 16 for i in range(16))]
    gr = [ground_at(*(c + v * (R + .4)).xy) if v.dot(din) > -.2 else outside_at(*(c + v * (R + .4)).xy) for v in around]
    base = min(gr) - .35
    plinth = max(gr) + .9
    t = Matrix.Translation(c)
    towers.lathe([(0, base), (R + .42, base), (R + .42, plinth - .4), (R + .14, plinth), (0, plinth)], t, GS2, 40)
    towers.lathe([(0, plinth - .1), (R - .25, plinth - .1), (R - .25, SHAFT + .1), (0, SHAFT + .1)], t, GJ, 24)
    rings(towers, c, R, plinth, SHAFT, rnd, 2.0, .3, 1.3)
    # The band at the wall walk, round the tower.
    towers.lathe([(0, WALK - .36), (R + .2, WALK - .36), (R + .24, WALK - .3), (R + .24, WALK - .06), (R + .2, WALK),
                  (0, WALK)], t, GS2, 40)
    # A ring of corbels, the wider head on them with four small windows, the eave.
    n_c = max(16, round(2 * math.pi * R / .62))
    for i in range(n_c):
        a = 2 * math.pi * (i + .5) / n_c
        ring_block(towers, c, R - .2, R + .48, a - .13 / R, a + .13 / R, SHAFT, SHAFT + .58, GS2, .03)
    head0 = SHAFT + .58
    towers.lathe([(0, head0), (R + .55, head0), (R + .55, head0 + .2), (0, head0 + .2)], t, GS2, 40)
    head1 = head0 + .2 + 2.6
    rings(towers, c, R + .45, head0 + .2, head1, rnd, 1.5, .35, 1.3)
    side = UP.cross(din).normalized()
    for v in (din, -din, side, -side):
        at = c + v * (R + .46)
        at.z = head0 + 1.55
        f = frame_on(at, v)
        towers.slab([(-.24, -.42), (.24, -.42), (.24, .42), (-.24, .42)], f, 0, .03, SHADE)
        for poly in ([(-.38, -.56), (.38, -.56), (.38, -.42), (-.38, -.42)], [(-.38, .42), (.38, .42), (.38, .58), (-.38, .58)],
                     [(-.38, -.42), (-.24, -.42), (-.24, .42), (-.38, .42)], [(.24, -.42), (.38, -.42), (.38, .42), (.24, .42)]):
            block(towers, poly, f, -.04, .07, GS2, .02)
    towers.lathe([(0, head1), (R + .66, head1), (R + .7, head1 + .1), (R + .7, head1 + .26), (R + .62, head1 + .34),
                  (0, head1 + .34)], t, GS2, 40)
    # The roof: blue slates in rows, a gold band at its foot, a gold finial, a pole and a red pennant.
    z0 = head1 + .34
    Rr, Hr = R + .95, 2.0 * (R + .95)
    prof = [(0, z0), (Rr + .1, z0)]
    rows = 8
    for i in range(rows):
        ta, tb = i / rows, (i + 1) / rows
        ra, rb = Rr * (1 - ta) ** 1.08, Rr * (1 - tb) ** 1.08
        za, zb = z0 + Hr * ta, z0 + Hr * tb
        prof += [(ra + .09, za + .02), (rb + .02, zb - .02)]
    prof += [(0, z0 + Hr)]
    roofs.lathe(prof, t, BLUE, 40)
    roofs.lathe([(0, z0 - .12), (Rr + .16, z0 - .12), (Rr + .2, z0 - .02), (Rr + .16, z0 + .1), (0, z0 + .1)], t, GG, 40)
    apex = z0 + Hr
    roofs.lathe([(0, apex - .45), (.2, apex - .45), (.26, apex - .2), (.12, apex + .05), (.16, apex + .2), (0, apex + .3)], t, GG, 16)
    roofs.sphere((x, y, apex + .48), .2, GG, subdiv=2)
    roofs.lathe([(0, apex + .6), (.06, apex + .6), (.05, apex + 3.0), (0, apex + 3.0)], t, GG, 8)
    roofs.sphere((x, y, apex + 3.08), .12, GG, subdiv=1)
    pen = [(0, 0), (1.9, .3), (0, .62)]
    pf = Matrix.Translation((x + .05, y, apex + 2.25)) @ Matrix.Rotation(math.radians(rnd.uniform(-14, 14)), 4, "Z") \
        @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    roofs.slab(pen, pf, -.03, .03, RED)
    # A crowned banner on the garden side, under the corbels.
    at = c + din * (R + .3)
    at.z = SHAFT - .35
    banner(deco, at, din, 1.6, 4.4, .55)
    # A planter with a cone topiary where there is level ground before the tower.
    p = c + din * (R + 1.35)
    gs = {round(ground_at(*(p + side * e).xy), 2) for e in (-1.1, 0, 1.1)}
    if len(gs) == 1:
        gnd = gs.pop()
        if gnd >= BANK and ground_at(*(p + din * .7).xy) == gnd:
            ang = math.atan2(side.y, side.x)
            pf = Matrix.Translation((p.x, p.y, gnd)) @ Matrix.Rotation(ang, 4, "Z")
            vs = deco.slab([(-1.0, -.5), (1.0, -.5), (1.0, .5), (-1.0, .5)], pf, 0, .72, GS2)
            chamfer(deco, vs, .04, GS2)
            deco.slab([(-.88, -.38), (.88, -.38), (.88, .38), (-.88, .38)], pf, .72, .76, M["soil"])
            for e in (-.45, .45):
                q = pf @ Vector((e, 0, 0))
                deco.lathe([(0, .74), (.42, .8), (.36, 1.4), (.22, 2.1), (.08, 2.55), (0, 2.65)],
                           Matrix.Translation((q.x, q.y, gnd)), M["leaf3"], 16)


# ------------------------------------------------------------------ build
walls = Mesh("Castle walls")
towers = Mesh("Castle towers")
roofs = Mesh("Castle roofs")
deco = Mesh("Castle banners and planters")
ground = Mesh("Castle grounds")

east_pts = [(WING[0], WING[1], None)] + EAST
poly = [(x, y) for x, y, _ in east_pts] + [(-x, y) for x, y, _ in reversed(east_pts)]
for s in (1, -1):
    pts = [(s * x, y, r) for x, y, r in east_pts]
    for (ax, ay, ar), (bx, by, br) in zip(pts, pts[1:]):
        wall_segment(walls, deco, (ax, ay), (bx, by), ar, br, RND)
    for x, y, r in pts[1:]:
        tower(towers, roofs, deco, x, y, r, RND)
    # The banks where the wall stands out past the water's edge, on a block quay.
    side = clip(poly, 0, s * WATER_EDGE, s > 0)
    side = clip(clip(side, 1, 59.0, False), 1, -61.5, True)
    if len(side) >= 3:
        pave(ground, side, BANK)
        body = clip(side, 0, s * (WATER_EDGE + .35), s > 0)          # behind the quay's blocks (not in their plane)
        if len(body) >= 3:
            ground.slab(body if area(body) > 0 else list(reversed(body)), Matrix.Identity(4), -2.4, BANK - .01, M["stone2"])
        ys = [y for x, y in side if abs(x - s * WATER_EDGE) < 1e-6]
        if len(ys) >= 2:
            d0, d1 = (0, .3) if s > 0 else (-.3, 0)
            kit_wall_run(ground, "y", s * WATER_EDGE, min(ys), max(ys), d0, d1, BANK, -2.3, True, 1.0, 2.2)
    # The strip between the yard / the terraces and the wall, at the podium's height (the old north wall's place).
    for x0, x1, y0 in ((6.0, 14.3, 60.5), (14.3, 25.2, 59.0)):
        xa, xb = sorted((s * x0, s * x1))
        pave(ground, [(xa, y0), (xb, y0), (xb, 63.1), (xa, 63.1)], STRIP)
        ground.slab([(xa, y0), (xb, y0), (xb, 63.1), (xa, 63.1)], Matrix.Identity(4), 0, STRIP - .01, M["stone2"])
    north = clip(clip(poly, 0, s * 25.2, s > 0), 1, 59.0, True)
    if len(north) >= 3:
        pave(ground, north, STRIP)
        ground.slab(north if area(north) > 0 else list(reversed(north)), Matrix.Identity(4), 0, STRIP - .01, M["stone2"])

for mesh in (walls, towers, roofs, deco, ground):
    mesh.done()

# ------------------------------------------------------------------ the context: the outside ground, the garden's dressings
ctx = Mesh("Context ground", CTX)
ring_in = [Vector((x, y, -.02)) for x, y in poly]
ring_out = [CENTRE + (p - CENTRE).normalized() * 130 for p in ring_in]
vin = [ctx.bm.verts.new(p) for p in ring_in]
vout = [ctx.bm.verts.new((p.x, p.y, -.02)) for p in ring_out]
for i in range(len(vin)):
    j = (i + 1) % len(vin)
    f = ctx.bm.faces.new((vin[i], vout[i], vout[j], vin[j]))
    f.material_index = ctx.mi(M["clay"])
ctx.box((-26, 33, RES - .05), (26, 48.6, RES), material("Context | reservoir", (.03, .5, .78), .06))   # its picture in Unity
for s in (-1, 1):
    xa, xb = sorted((s * 26, s * 44))
    ctx.box((xa, 33, -2), (xb, 48.6, UPPER), M["clay"])         # the reservoir's terraces (graybox, undressed)
ctx.done()
for ob in bpy.data.objects:
    if ob.name == "Context ground":
        for f in ob.data.polygons:
            f.use_smooth = False


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
for px, py, pz in ((0, 57.5, UPPER), (47.8, 14.0, BANK), (31.0, 61.2, STRIP)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "WallsSample_v1.blend"))
for name, at, look, lens in (("overview", (0, -118, 95), (0, 4, 0), 30),
                             ("north", (0, 18, 16), (0, 62, 14), 22),
                             ("reference", (-14, 30, 13), (16, 60, 12), 24),
                             ("corner", (30, 40, 9), (47.6, 55, 10), 28),
                             ("side", (30, 0, 8), (50.4, 2, 8), 26),
                             ("arch", (42.5, 10, 4.5), (49.2, 14, 4.2), 30),
                             ("tower", (43, 22, 9), (50.4, 14, 11), 30),
                             ("south", (0, -40, 9), (0, -62, 10), 26)):
    shoot("walls_v1", name, at, look, lens)
print("WALLS SAMPLE DONE")

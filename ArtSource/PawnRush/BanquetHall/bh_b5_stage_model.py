"""B5 the stage, the curtain, the dessert buffet and the chocolate fountain (bh-hall session, seoungtest R838,
2026-11-25; PLAN.md 1 "B5"; BH_A07, BH_A08).

    blender -b --factory-startup --python bh_b5_stage_model.py -- --part stage_front

One part at a time as B7 / B6 (bh_b7_hall_model.py's helpers): stills/b5/parts/<name>_compare.png.

The builder (GrandBanquetHallBuilder.StageAndCurtain / Buffet; Unity x east, y up, z north):
  - the stage x -32 .. -20, y 0 .. 7.4 (walked on top), z 29.05 .. 49.05; its front (x -20) a sheer face not climbed;
  - the curtain on x -24.5: 20 pleats 1.05 wide, 0.24 deep, 7.7 .. 18 high (each a moving wall to climb), the rail
    18 .. 18.3, the pelmet 18.3 .. 19.9 (x -25 .. -24), z 12.5 .. 51.05;
  - the double basses at x -23.95, z 19 and 27.05: a body 0.7 x 7 x 1.6, the neck to 9.6 (it knocks a hanging pawn);
  - the buffet x -32 .. -23.5, y 0 .. 7.4, z 3 .. 17 (walked on top, its skirts not climbed); the fountain at
    (-27.7, 9.2): its bowl r 4.2 to 7.9, tiers r 3.3 / 2.3 / 1.3 to 10.733 / 13.567 / 16.4 (their sides climbed against
    the flowing chocolate, the last 0.45 m under a rim still), the crown r 0.35 to 17.6 and a finial.
Models stay on / inside those faces; walked tops are flush.
Part frame (fronts): x along the face, y out of it, z up from the floor.
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import bh_b7_hall_model as h   # noqa: E402

h.STILLS = os.path.join(HERE, "stills", "b5")
STAGE_H = 7.4


def hollow(hw, za, zb, r, cx=0.0, segs=8):
    """A rectangle (x, z) with hollow quarter-round corners, counter-clockwise (the sheet's panel frames)."""
    pts = []
    for (x, z, a0) in ((hw, za, 180), (hw, zb, 270), (-hw, zb, 0), (-hw, za, 90)):
        for i in range(segs + 1):
            t = math.radians(a0 - 90 * i / segs)
            pts.append((cx + x + r * math.cos(t), z + r * math.sin(t)))
    return pts


def stage_front(m, x0, x1, H=STAGE_H, panel=3.4):
    """A07 cell 1 along x0 .. x1: the stage's front - a walnut plinth, walnut stiles and rails standing out of sunk
    walnut panels framed in gold with hollow corners, a thick gilt cap whose top is the stage's floor, a walnut
    moulding under the cap. Its front at y 0 (the face's plane), everything within 0.2 m of it."""
    n = max(1, round((x1 - x0) / panel))
    xs = [x0 + (x1 - x0) * i / n for i in range(n + 1)]
    cap = .62
    rec = .1
    h.box(m, "walnut", x0, x1, -.3, -rec, 0, H - cap)                       # the panels' floor
    h.prism_yz(m, "walnut", [(-.3, 0), (.14, 0), (.14, .32), (.08, .38), (.04, .46), (-.3, .46)], x0, x1)   # plinth
    h.prism_yz(m, "gold", [(-.3, H - cap), (.14, H - cap), (.2, H - cap + .1), (.2, H - cap + .2), (.28, H - cap + .3),
                           (.3, H - .12), (.26, H - .03), (.22, H), (-.3, H)], x0, x1)   # the cap (its top = the floor)
    h.prism_yz(m, "gold", [(-.3, .0), (.18, .0), (.2, .08), (.16, .16), (-.3, .16)], x0, x1)   # a gold foot band
    h.prism_yz(m, "walnut", [(-.3, H - cap - .28), (0, H - cap - .28), (.08, H - cap - .2), (.1, H - cap - .08),
                             (.06, H - cap), (-.3, H - cap)], x0, x1)       # the moulding under it
    za, zb = .46, H - cap - .28                                             # the frame's rails' inner edges
    h.box(m, "walnut", x0, x1, -rec, 0, za, za + .32)
    h.box(m, "walnut", x0, x1, -rec, 0, zb - .32, zb)
    for x in xs:
        a, b = max(x0, x - .28), min(x1, x + .28)
        h.box(m, "walnut", a, b, -rec, 0, za, zb)
        # a pilaster strip standing out of the stile, a gold base and a gold capital
        h.box(m, "walnut", a + .04, b - .04, 0, .12, za, zb)
        h.box(m, "gold", a - .02, b + .02, -rec, .16, za, za + .22)
        h.box(m, "gold", a, b, -rec, .14, za + .22, za + .28)
        h.box(m, "gold", a - .02, b + .02, -rec, .16, zb - .22, zb)
        h.box(m, "gold", a, b, -rec, .14, zb - .28, zb - .22)
    for xa, xb in zip(xs, xs[1:]):
        cx = (xa + xb) / 2
        hw = (xb - xa) / 2 - .28 - .32
        z0, z1 = za + .32 + .3, zb - .32 - .3
        cr = .32
        outer = hollow(hw, z0, z1, cr, cx)
        inner = hollow(hw - .17, z0 + .17, z1 - .17, cr + .17, cx)
        h.ring_xz(m, "gold", outer, inner, -rec - .01, -rec + .05)
        bead = hollow(hw - .09, z0 + .09, z1 - .09, cr + .09, cx)
        h.ring_xz(m, "gold", bead, inner, -rec - .01, -rec + .1)
        # a bevelled moulding round the sunk panel where it meets the frame
        for p0, p1, q0, q1 in ((xa + .28, xb - .28, za + .32, za + .38), (xa + .28, xb - .28, zb - .38, zb - .32),
                               (xa + .28, xa + .34, za + .32, zb - .32), (xb - .34, xb - .28, za + .32, zb - .32)):
            h.box(m, "walnut", p0, p1, -rec, -rec + .05, q0, q1)


PELMET_H = 1.6        # the builder's pelmet: 18.3 .. 19.9, a metre deep (x -25 .. -24)


def tassel(m, top, length=.75, s=1.0):
    """A gold tassel hung from `top`: a cord, a knot, a turned head, a skirt of strands."""
    top = Vector(top)
    knot = top - Vector((0, 0, length * .45))
    h.tube(m, "gold", [top, knot], .018 * s, sides=6)
    h.ball(m, "gold", knot, .045 * s, 10, 6)
    hz = knot.z - .04 * s
    h.lathe(m, "gold", [(0, 0), (.06 * s, -.02 * s), (.085 * s, -.08 * s), (.07 * s, -.14 * s), (.1 * s, -.17 * s),
                        (.075 * s, -.2 * s), (0, -.21 * s)], at=(knot.x, knot.y, hz), segs=14)
    sk = hz - .2 * s
    n = 10
    for i in range(n):
        a = 2 * math.pi * i / n
        r = .085 * s
        p0 = Vector((knot.x + r * math.cos(a), knot.y + r * math.sin(a), sk))
        p1 = Vector((knot.x + r * 1.45 * math.cos(a), knot.y + r * 1.45 * math.sin(a), sk - length * .42))
        h.tube(m, "gold", [p0, p1], .016 * s, sides=5)
    h.lathe(m, "gold", [(0, 0), (.1 * s, 0), (.11 * s, -.03 * s), (0, -.03 * s)], at=(knot.x, knot.y, sk + .01), segs=14)


def rosette(m, at, r=.28, out=Vector((0, 1, 0))):
    """A gilt rosette facing `out`: a domed boss, a ring of petals, a beaded rim."""
    q = Vector(out).to_track_quat("Z", "Y").to_matrix()
    h.lathe(m, "gold", [(0, 0), (r, 0), (r * 1.02, .03), (r * .9, .06), (0, .06)], at=at, segs=24, axis=q)
    for i in range(12):
        a = 2 * math.pi * i / 12
        d = q @ Vector((math.cos(a), math.sin(a), 0))
        h.broad_leaf(m, "gold", Vector(at) + q @ Vector((0, 0, .06)) + d * r * .18, d, Vector(out), r * .32, r * .7,
                     curl=.35, bulge=.2, lobes=2, thick=.03, sides=6)
    h.ball(m, "gold", Vector(at) + q @ Vector((0, 0, .1)), r * .25, 14, 8, scale=(1, 1, .7))


def pelmet(m, x0, x1, span=2.4, H=PELMET_H):
    """A07 cell 2 along x0 .. x1: the valance over the curtain, its front at y 0, a metre deep. A gilt cornice board
    on top; under it a flat red velvet back; red velvet swags in folds (held still), each hemmed with a gold rope,
    hung between the junctions; a gold tassel at each junction; gilt C-scrolls and a shell over each swag; rosettes
    at the ends; the ends turned back in velvet."""
    d = 1.0
    top = H
    h.prism_yz(m, "gold", [(-d, top - .3), (.06, top - .3), (.12, top - .24), (.12, top - .16), (.18, top - .1),
                           (.18, top - .02), (.14, top), (-d, top)], x0, x1)
    h.box(m, "velvet", x0, x1, -d, -.02, .45, top - .3)
    for x in (x0, x1):
        h.box(m, "velvet", min(x, x + (.02 if x == x0 else -.02)), max(x, x + (.02 if x == x0 else -.02)), -d, 0, .2, top - .3)
    n = max(1, round((x1 - x0) / span))
    js = [x0 + (x1 - x0) * i / n for i in range(n + 1)]
    for xa, xb in zip(js, js[1:]):
        w = xb - xa
        # the folds: four ribbons, each sagging more and standing further out, the lowest hemmed in gold
        hem = None
        for k in range(4):
            sag = .25 + .17 * k
            zt = 1.12 - .04 * k
            pts = []
            for i in range(17):
                u = i / 16
                z = zt - sag * math.sin(math.pi * u) ** 1.2
                y = .04 + .05 * k + .12 * math.sin(math.pi * u) * (k + 1) / 4
                pts.append(Vector((xa + w * u, y, z)))
            widths = [.22 + .06 * math.sin(math.pi * i / 16) for i in range(17)]
            h.blade(m, "velvet", pts, widths, Vector((0, 1, 0)), thick=.08, sides=8, side=None)
            hem = pts
        # the gold rope along the lowest fold's hem
        rope = [p + Vector((0, .05, -.12)) for p in hem]
        h.tube(m, "gold", rope, .035, sides=8)
        # the scrolls and the shell over the swag
        cx = (xa + xb) / 2
        for sg in (-1, 1):
            pts = []
            for i in range(17):
                t = i / 16
                x = cx + sg * (.2 + (w / 2 - .45) * t)
                z = 1.2 + .1 * math.sin(math.pi * t * 2)
                pts.append(Vector((x, .04, z)))
            h.tube(m, "gold", pts, .055, sides=10, taper=lambda i: .06 - .025 * i / 16)
            h.spiral(m, "gold", pts[-1] + Vector((0, 0, -.1)), (sg, 0, 0), (0, 0, 1), .12, .025, 1.4, .045, .02, start=math.pi / 2)
            h.spiral(m, "gold", pts[6] + Vector((0, 0, .1)), (-sg, 0, 0), (0, 0, 1), .09, .02, 1.3, .035, .018, start=-math.pi / 2)
            h.broad_leaf(m, "gold", pts[3] + Vector((0, .03, 0)), Vector((sg, 0, .35)), Vector((0, 1, 0)), .12, .3, curl=.5, bulge=.2, lobes=2, thick=.03, sides=6)
        for k in range(7):
            a = math.radians(-60 + 20 * k)
            dirv = Vector((math.sin(a), 0, math.cos(a)))
            h.blade(m, "gold", [Vector((cx, .08, 1.08)) + dirv * t * .3 for t in (0, .35, .7, 1)], [.05, .07, .06, .01],
                    Vector((0, 1, 0)), thick=.04, sides=6, side=Vector((math.cos(a), 0, -math.sin(a))))
        h.ball(m, "gold", Vector((cx, .1, 1.06)), .05, 10, 6)
    for i, x in enumerate(js):
        if i in (0, len(js) - 1):
            rosette(m, Vector((x + (.25 if i == 0 else -.25), .05, 1.05)))
        tassel(m, Vector((x + (.15 if i == 0 else (-.15 if i == len(js) - 1 else 0)), .14, .9)), .9)


PLEAT_W, PLEAT_D, PLEAT_H = 1.05, .24, 10.3   # the builder's pleat box: 1.05 along the curtain, 0.24 through, 7.7 .. 18


def pleat_y(u, z, H):
    """The cloth's fold line at u (0 .. 1 across the pleat) and height z: three deep folds per pleat, their phase and
    depth drifting a little with height so they read as hung cloth; the same at u 0 and 1, so pleats side by side
    meet without a seam. Kept inside the box (|y| + half the cloth <= 0.12)."""
    t = z / H
    # the sheet's velvet: heavy round tubes, three to a pleat, nearly the box's whole depth; a little shallower under
    # the heading, fullest at the hem; their phase wanders a little with height so they hang rather than extrude
    a = .089 - .011 * max(0.0, (t - .85) / .15) - .004 * (1 - t) + .002 * math.sin(t * 9.3 + .4)
    ph = .035 * math.sin(t * 3.1) + .02 * math.sin(t * 8.3 + 1.2)
    # uneven folds (the sheet's velvet is never regular): a warp of u that is 0 at the pleat's edges, stronger low
    # down so the folds fan out a little toward the hem; each fold its own depth
    g = .45 + .55 * (1 - t)
    uw = u + g * (.035 * math.sin(2 * math.pi * u + .5) - .035 * math.sin(.5) * math.cos(2 * math.pi * u)
                  + .02 * math.sin(4 * math.pi * u))
    a *= 1 + .1 * math.sin(2 * math.pi * u + 2.0)
    w = 2 * math.pi * (3 * uw + ph)
    c = math.cos(w) + .12 * math.cos(2 * w + .6)
    # tubes: broad round crowns and backs (a squarish cosine), kept inside 0.12 with the cloth's half-thickness
    return a * math.copysign(min(1.0, abs(c / 1.08)) ** .45, c)


def cloth(m, slot, u0, u1, z0, z1, H, off=0.0, half=.02, nu=36, nz=40, x0=-PLEAT_W / 2, fn=None):
    """A thick cloth sheet following pleat_y between u0 .. u1 and z0 .. z1, `half` each side of the line, pushed
    `off` along y. Both faces and the edges closed (seen from the hall and from the stage)."""
    fn = fn or pleat_y
    mi = m.mat(slot)
    bm = m.bm
    grid = {}
    for s in (-1, 1):
        for i in range(nu + 1):
            u = u0 + (u1 - u0) * i / nu
            for j in range(nz + 1):
                z = z0 + (z1 - z0) * j / nz
                # the line's normal in (x, y): offset the two faces along it so the cloth keeps its thickness
                du = 1e-3
                dy = (fn(u + du, z, H) - fn(u - du, z, H)) / (2 * du * PLEAT_W)
                n = Vector((-dy, 1, 0)).normalized()
                p = Vector((x0 + PLEAT_W * u, fn(u, z, H) + off, z)) + n * (half * s)
                grid[s, i, j] = bm.verts.new(p)
    for i in range(nu):
        for j in range(nz):
            h.face(bm, [grid[1, i, j], grid[1, i + 1, j], grid[1, i + 1, j + 1], grid[1, i, j + 1]], mi, True)
            h.face(bm, [grid[-1, i, j + 1], grid[-1, i + 1, j + 1], grid[-1, i + 1, j], grid[-1, i, j]], mi, True)
    for i in range(nu):
        h.face(bm, [grid[-1, i, 0], grid[-1, i + 1, 0], grid[1, i + 1, 0], grid[1, i, 0]], mi, True)
        h.face(bm, [grid[1, i, nz], grid[1, i + 1, nz], grid[-1, i + 1, nz], grid[-1, i, nz]], mi, True)
    for j in range(nz):
        h.face(bm, [grid[-1, 0, j + 1], grid[-1, 0, j], grid[1, 0, j], grid[1, 0, j + 1]], mi, True)
        h.face(bm, [grid[1, nu, j + 1], grid[1, nu, j], grid[-1, nu, j], grid[-1, nu, j + 1]], mi, True)


def curtain_tassel(m, top, s=1.0):
    """The sheet's tassel, small: a cord from the hem rope, a round knot, a turned head and collar, a full bell skirt
    ribbed with cords and a rolled foot. From `top` down to 0.03 over the pleat's foot; 0.075 at its widest."""
    x, y, z = top
    S = s
    h.tube(m, "gold", [Vector((x, y, z)), Vector((x, y, z - .07 * S))], .012 * S, sides=6)
    h.ball(m, "gold", Vector((x, y, z - .085 * S)), .026 * S, 10, 6)
    # head (an onion), collar, the skirt flaring to a rolled foot
    prof = [(0, 0), (.022, -.004), (.038, -.02), (.043, -.04), (.036, -.06), (.026, -.07), (.04, -.074), (.042, -.086),
            (.03, -.092), (.034, -.11), (.044, -.15), (.056, -.2), (.066, -.245), (.072, -.27), (.07, -.282),
            (.06, -.286), (0, -.286)]
    zh = z - .105 * S
    prof = [(r * S, q * S) for r, q in prof]
    h.lathe(m, "gold", prof, at=(x, y, zh), segs=18)
    for i in range(14):
        a = 2 * math.pi * (i + .5) / 14
        pts = []
        for k in range(6):
            t = k / 5
            zz = (-.1 - .175 * t) * S
            r = (.034 + (.069 - .034) * t ** .9 + .004) * S
            pts.append(Vector((x + r * math.cos(a), y + r * math.sin(a), zh + zz)))
        h.tube(m, "gold", pts, .007 * S, sides=4)


def pleat(m, x0=-PLEAT_W / 2, H=PLEAT_H):
    """One pleat of the stage curtain (no sheet cell of its own: the A07 pelmet's red velvet and gold), its foot at
    z 0 (the builder's PleatBottom), its top at H (the rail), within the 1.05 x 0.24 box round x0 .. x0 + 1.05,
    y 0. Velvet in three deep uneven folds, a heavier rolled hem, a twisted gold rope and a gold tassel per fold, a gold
    heading band with its rings at the top. Everything is the pleat's: it moves with the pleat."""
    fx = lambda u: x0 + PLEAT_W * u
    # the velvet: from the hem rope to under the heading; a fuller rolled hem over the rope
    cloth(m, "velvet", 0, 1, .44, H - .32, H, half=.018, x0=x0, nu=60, nz=18)
    cloth(m, "velvet", 0, 1, .44, .6, H, half=.021, nu=60, nz=3, x0=x0)
    # the heading: a gold band with raised edges, a velvet top over it
    cloth(m, "gold", 0, 1, H - .34, H - .18, H, half=.02, nu=60, nz=2, x0=x0)
    cloth(m, "gold", 0, 1, H - .3, H - .27, H, half=.022, nu=60, nz=1, x0=x0)
    cloth(m, "gold", 0, 1, H - .23, H - .2, H, half=.022, nu=60, nz=1, x0=x0)
    cloth(m, "velvet", 0, 1, H - .18, H - .06, H, half=.018, nu=60, nz=2, x0=x0)
    # the sheet's gold rope along the hem: two twisted strands following the folds
    for k in range(2):
        pts = []
        for i in range(181):
            u = i / 180
            ang = 2 * math.pi * 26 * u + math.pi * k
            pts.append(Vector((fx(u), .95 * pleat_y(u, .41, H) + .011 * math.cos(ang), .41 + .011 * math.sin(ang))))
        h.tube(m, "gold", pts, .015, sides=7, cap=True)
    # a gold tassel under each fold's crown (the sheet's tassels), hung from inside the hem, within the box
    us = [i / 600 for i in range(600)]
    ys = [pleat_y(u, .5, H) for u in us]
    crowns = [us[i] for i in range(600) if ys[i] >= ys[i - 1] and ys[i] >= ys[(i + 1) % 600] and ys[i] > 0]
    for u in crowns:
        curtain_tassel(m, Vector((fx(u), .03, .4)))
    # the heading: a ring at each fold's back (where a pin hook would go), on the rail line at the top
    for u in (1 / 6, .5, 5 / 6):
        c = Vector((fx(u), 0, H - .02))
        h.lathe(m, "gold", [(.04, -.02), (.062, -.02), (.07, 0), (.062, .02), (.04, .02), (.04, -.02)], at=c, segs=12,
                axis=Matrix.Rotation(math.pi / 2, 3, "X"))
        h.tube(m, "gold", [Vector((fx(u), pleat_y(u, H - .2, H), H - .2)), c + Vector((0, 0, -.06))], .012, sides=6)


def pleat_part(col):
    m = h.Mesh()
    for k in range(3):
        pleat(m, x0=-1.5 * PLEAT_W + k * PLEAT_W)
    return [m.finish("BH curtain pleats", col, bevel=0, merge=False)]


BASS_W, BASS_D, BASS_BODY, BASS_TOP, NECK = .8, .35, 7.0, 9.6, .15   # the builder's boxes (half sizes, tops)
# the body's half width up its height (t 0 .. 1 over z 0.3 .. 6.95): the sheet's outline - round lower bout, the
# waist, the smaller upper bout, the bass's sloping shoulders into the neck - narrowed to the builder's 1.6 m
BASS_OUTLINE = [(0, .05), (.01, .32), (.03, .53), (.07, .68), (.13, .77), (.2, .8), (.27, .79), (.34, .74),
                (.4, .64), (.43, .56), (.46, .47), (.52, .43), (.58, .45), (.62, .52), (.66, .6), (.72, .64),
                (.77, .62), (.82, .55), (.88, .42), (.94, .29), (.98, .2), (1, .155)]
BZ0, BZ1 = .3, 6.95
RIB, ARCH = .15, .08


def bass_w(t):
    """The outline through BASS_OUTLINE, a Catmull-Rom curve (smooth bouts, no flats at the points)."""
    pts = BASS_OUTLINE
    t = min(1.0, max(0.0, t))
    for k in range(len(pts) - 1):
        (t0, w0), (t1, w1) = pts[k], pts[k + 1]
        if t <= t1 or k == len(pts) - 2:
            wa = pts[k - 1][1] if k > 0 else w0 - (w1 - w0)
            wb = pts[k + 2][1] if k + 2 < len(pts) else w1 + (w1 - w0)
            f = (t - t0) / (t1 - t0)
            return .5 * (2 * w0 + (-wa + w1) * f + (2 * wa - 5 * w0 + 4 * w1 - wb) * f * f
                         + (-wa + 3 * w0 - 3 * w1 + wb) * f ** 3)
    return pts[-1][1]


def bass_front(x, w):
    return RIB + ARCH * max(0.0, 1 - (x / w) ** 2) ** .8 if w > 1e-4 else RIB


def bass_top(x, z):
    return bass_front(x, bass_w((z - BZ0) / (BZ1 - BZ0)))


def bass_body(m, nz=70, nx=20):
    """The body: ribs RIB each side of y 0, the top and the back arched ARCH over them, its outline bass_w; closed
    at the foot and under the neck; gilt edging round both rims and a gilt inlay line inside the top's edge."""
    mi = m.mat("walnut")
    bm = m.bm
    rings = []
    for j in range(nz + 1):
        t = j / nz
        z = BZ0 + (BZ1 - BZ0) * t
        w = bass_w(t)
        ring = []
        for i in range(nx + 1):
            x = -w + 2 * w * i / nx
            ring.append(Vector((x, bass_front(x, w), z)))
        for i in range(nx + 1):
            x = w - 2 * w * i / nx
            ring.append(Vector((x, -bass_front(x, w), z)))
        rings.append([bm.verts.new(v) for v in ring])
    n = len(rings[0])
    for j in range(nz):
        a, b = rings[j], rings[j + 1]
        for i in range(n):
            h.face(bm, [a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]], mi, True)
    h.face(bm, list(reversed(rings[0])), mi)
    h.face(bm, rings[-1], mi)
    for sg in (1, -1):
        for side in (-1, 1):
            pts = [Vector((side * (bass_w(j / nz) - .01), sg * (RIB + .006), BZ0 + (BZ1 - BZ0) * j / nz))
                   for j in range(nz + 1)]
            h.tube(m, "gold", pts, .022, sides=6)
    for side in (-1, 1):
        pts = []
        for j in range(nz + 1):
            t = j / nz
            w = bass_w(t)
            if w < .25:
                continue
            x = side * (w - .09)
            pts.append(Vector((x, bass_front(x, w) + .004, BZ0 + (BZ1 - BZ0) * t)))
        h.tube(m, "gold", pts, .009, sides=5)


def f_hole(m, side, zc):
    """An f-hole on the top: a slim S slot (black), a round eye at each end, a nick across its waist."""
    pts = []
    for i in range(25):
        t = i / 24
        x = side * (.27 - .07 * math.sin(math.pi * (t * 2 - 1)))
        z = zc - .75 + 1.5 * t
        pts.append(Vector((x, bass_top(x, z) + .002, z)))
    h.tube(m, "piece_black", pts, .04, sides=6, taper=lambda i: .026 + .022 * math.sin(math.pi * i / 24))
    for q in (pts[0], pts[-1]):
        h.ball(m, "piece_black", q, .075, 12, 6, scale=(1, .45, 1))
    mid = pts[12]
    h.box(m, "piece_black", mid.x - .09, mid.x + .09, mid.y - .01, mid.y + .006, mid.z - .012, mid.z + .012)


def bass(m):
    """A07 cell 5: the double bass in the builder's two boxes - its body x +-0.8, y +-0.35 to 7 m, its neck +-0.15 to
    9.6 m; its top (front) facing +y (the hall). Walnut body with gilt edging and inlay, f-holes, a pale bridge, a
    black tailpiece and fingerboard, four silver strings, a walnut neck, pegbox and scroll with gilt pegs, a gilt
    end pin. The bow beside it in the sheet is left out (it would stand outside the boxes)."""
    bass_body(m)
    zb = BZ0 + (BZ1 - BZ0) * .5                       # the waist: the bridge between the f-holes' nicks
    f_hole(m, 1, zb)
    f_hole(m, -1, zb)
    top = bass_top
    # the end pin and its gilt collar
    h.lathe(m, "gold", [(0, 0), (.025, 0), (.03, .1), (.06, .22), (.09, .26), (.09, .3), (.07, .33), (0, .33)],
            at=(0, 0, 0), segs=16)
    # the tailpiece: a black tapered board up the lower bout, rising off the top, a gilt bar across its head
    tz0, tz1 = .85, 2.3
    for k in range(6):
        za, zc = tz0 + (tz1 - tz0) * k / 6, tz0 + (tz1 - tz0) * (k + 1) / 6
        wa, wc = .08 + .14 * (k / 6) ** .8, .08 + .14 * ((k + 1) / 6) ** .8
        ya = top(0, za) + .02 + .035 * k / 6
        yc = top(0, zc) + .02 + .035 * (k + 1) / 6
        h.prism_xz(m, "piece_black", [(-wa, za), (wa, za), (wc, zc), (-wc, zc)], ya - .03, max(ya, yc) + .02)
    h.box(m, "gold", -.23, .23, top(0, tz1) + .02, top(0, tz1) + .085, tz1 - .07, tz1)
    for xs in (-.15, -.05, .05, .15):
        h.ball(m, "gold", Vector((xs, top(0, tz1) + .075, tz1 - .2)), .03, 8, 6)
    h.box(m, "piece_black", -.06, .06, top(0, .45) - .02, top(0, .45) + .03, .32, tz0 + .05)   # the tail gut
    # the bridge: pale maple, two feet on the top, standing up to the strings, an arched crown
    bz = zb
    y0 = top(0, bz)
    by1 = .31                                         # the strings over the bridge (the neck box stays clear)
    for xa, xb in ((-.32, -.18), (.18, .32)):
        h.box(m, "piece_white", xa, xb, y0 - .02, y0 + .03, bz, bz + .14)            # the feet
    h.prism_yz(m, "piece_white", [(y0 + .02, bz + .03), (by1, bz + .03), (by1, bz + .11), (y0 + .02, bz + .11)], -.29, -.19)
    h.prism_yz(m, "piece_white", [(y0 + .02, bz + .03), (by1, bz + .03), (by1, bz + .11), (y0 + .02, bz + .11)], .19, .29)
    h.prism_yz(m, "piece_white", [(by1 - .07, bz + .03), (by1, bz + .03), (by1, bz + .11), (by1 - .07, bz + .11)], -.29, .29)
    # the neck (walnut, its heel on the shoulders), the black fingerboard over it and over the upper body
    nz0, nut = BZ1 - .4, 9.0
    h.box(m, "walnut", -.12, .12, -.13, .05, nz0, nut)
    h.lathe(m, "walnut", [(0, 0), (.15, 0), (.15, .1), (.12, .4), (0, .45)], at=(0, 0, nz0 - .05), segs=16)
    fz0 = 4.6
    # its face: 0.07 at the nut, 0.11 where the neck leaves the body box (7 m), rising off the top to 0.2 at its end
    fy = lambda z: (.07 + .04 * (nut - z) / (nut - 7.0)) if z >= 7.0 else (.11 + .09 * (7.0 - z) / (7.0 - fz0))
    k = 12
    for i in range(k):
        za, zc = fz0 + (nut - fz0) * i / k, fz0 + (nut - fz0) * (i + 1) / k
        wa, wc = .14 - .04 * i / k, .14 - .04 * (i + 1) / k
        h.prism_xz(m, "piece_black", [(-wa, za), (wa, za), (wc, zc), (-wc, zc)],
                   min(fy(za), fy(zc)) - .03, max(fy(za), fy(zc)) + .02)
    # the nut (ivory) and the four strings from the tailpiece over the bridge to the nut
    h.box(m, "piece_white", -.11, .11, .04, fy(nut) + .045, nut - .03, nut + .03)
    for xs in (-.105, -.035, .035, .105):
        a = Vector((xs * .9, top(0, tz1) + .06, tz1 - .02))
        b = Vector((xs * 1.1, by1 + .006, bz + .07))
        c = Vector((xs * .7, fy(nut) + .05, nut))
        h.tube(m, "silver", [a, b, c], .009, sides=5)
    # the pegbox: walnut cheeks and back; gilt pegs - bosses on the cheeks, a shaft across, a key out the back
    pz0, pz1 = nut, 9.42
    for sx in (-1, 1):
        h.box(m, "walnut", sx * .13 - .02, sx * .13 + .02, -.13, .07, pz0, pz1)
        h.box(m, "gold", sx * .15 - .005 * sx - .005, sx * .15 - .005 * sx + .005, -.11, .05, pz0 + .03, pz1 - .05)
    h.box(m, "walnut", -.13, .13, -.13, -.09, pz0, pz1)
    for i in range(2):
        z = pz0 + .1 + .16 * i
        for sx in (-1, 1):
            h.lathe(m, "gold", [(0, 0), (.05, 0), (.06, .012), (.04, .02), (0, .02)], at=(sx * .15 - sx * .02, -.02, z),
                    segs=12, axis=Matrix.Rotation(sx * math.pi / 2, 3, "Y"))
            h.ball(m, "gold", Vector((sx * .05, -.13, z)), .05, 10, 6, scale=(1, .4, 1))
        h.tube(m, "gold", [Vector((-.11, -.02, z)), Vector((.11, -.02, z))], .018, sides=6)
    # the scroll: a volute each side of the head round a turned centre, a gilt eye in each
    sc = Vector((0, -.01, 9.44))
    for sx in (-1, 1):
        h.spiral(m, "walnut", sc + Vector((sx * .07, 0, .02)), (0, 0, -1), (0, 1, 0), .115, .015, 2.3, .036, .02,
                 start=0, n=48)
        h.ball(m, "gold", sc + Vector((sx * .11, 0, 0)), .025, 8, 6)
    h.ball(m, "walnut", sc + Vector((0, 0, .01)), .08, 14, 8, scale=(.9, 1, 1))


def bass_part(col):
    m = h.Mesh()
    bass(m)
    return [m.finish("BH double bass", col, bevel=.008, merge=False)]


BUFFET = (-32.0, -23.5, 3.0, 17.0, 7.4)    # the builder's buffet: x0, x1, z0, z1 (Unity), its walked top


def drape(m, slot, x0, x1, z0, z1, fy, half=.015, nx=60, nz=12, zlo=None):
    """A cloth surface over x0 .. x1, z0 .. z1 on the line y = fy(x, z), `half` thick each side, its foot following
    zlo(x) if given (a scalloped hem). Both faces and the edges closed."""
    mi = m.mat(slot)
    bm = m.bm
    g = {}
    for s in (-1, 1):
        for i in range(nx + 1):
            x = x0 + (x1 - x0) * i / nx
            za = zlo(x) if zlo else z0
            for j in range(nz + 1):
                z = za + (z1 - za) * j / nz
                d = 1e-3
                dy = (fy(x + d, z) - fy(x - d, z)) / (2 * d)
                n = Vector((-dy, 1, 0)).normalized()
                g[s, i, j] = bm.verts.new(Vector((x, fy(x, z), z)) + n * half * s)
    for i in range(nx):
        for j in range(nz):
            h.face(bm, [g[1, i, j], g[1, i + 1, j], g[1, i + 1, j + 1], g[1, i, j + 1]], mi, True)
            h.face(bm, [g[-1, i, j + 1], g[-1, i + 1, j + 1], g[-1, i + 1, j], g[-1, i, j]], mi, True)
        h.face(bm, [g[-1, i, 0], g[-1, i + 1, 0], g[1, i + 1, 0], g[1, i, 0]], mi, True)
        h.face(bm, [g[1, i, nz], g[1, i + 1, nz], g[-1, i + 1, nz], g[-1, i, nz]], mi, True)
    for j in range(nz):
        h.face(bm, [g[-1, 0, j + 1], g[-1, 0, j], g[1, 0, j], g[1, 0, j + 1]], mi, True)
        h.face(bm, [g[1, nx, j + 1], g[1, nx, j], g[-1, nx, j], g[-1, nx, j + 1]], mi, True)


SKIRT_TOP, FOLD = 6.05, 1.0


def skirt_y(x, z, L):
    """The skirt's fold line: round tubes a metre apart (a whole number of them along the face), a touch fuller
    toward the hem; set 0.32 back so the gold swags, rosettes and tassels hang in front, all behind the face plane."""
    n = max(1, round(L / FOLD))
    t = z / SKIRT_TOP
    a = .075 + .025 * (1 - t)
    c = math.cos(2 * math.pi * n * x / L)
    return -.32 + a * math.copysign(abs(c) ** .5, c)      # set back: the swags hang in front of it


def skirt_hem(x, L):
    """The scalloped foot: each fold's crown hangs lowest."""
    n = max(1, round(L / FOLD))
    c = math.cos(2 * math.pi * n * x / L)
    return .05 + .12 * (1 - c) / 2


def gold_swag(m, xa, xb, ztop, drop=1.45):
    """A gold cloth swag (the sheet's) between two medallions: three ribbons, each sagging deeper and standing a
    little further out, all behind the face plane."""
    w = xb - xa
    for k in range(4):
        sag = drop * (.5 + .2 * k)
        zt = ztop - .1 * k
        pts = []
        for i in range(25):
            u = i / 24
            pts.append(Vector((xa + w * u, -.15 + .02 * k + .03 * math.sin(math.pi * u),
                               zt - sag * math.sin(math.pi * u) ** 1.3)))
        widths = [.3 + .2 * math.sin(math.pi * i / 24) for i in range(25)]
        h.blade(m, "gold", pts, widths, Vector((0, 1, 0)), thick=.1, sides=10, side=None)


def shell(m, at, r):
    """The sheet's gilt shell: a fan of ribbed lobes over a scrolled foot and a ball, facing +y."""
    at = Vector(at)
    for k in range(9):
        a = math.radians(-72 + 18 * k)
        d = Vector((math.sin(a), 0, math.cos(a)))
        pts = [at + d * r * t + Vector((0, .07 * math.sin(math.pi * t), 0)) for t in (0, .3, .6, .85, 1)]
        h.blade(m, "gold", pts, [r * .1, r * .22, r * .27, r * .24, r * .08], Vector((0, 1, 0)), thick=.07, sides=8,
                side=Vector((math.cos(a), 0, -math.sin(a))))
    h.tube(m, "gold", [at + Vector((r * math.sin(math.radians(a)), .02, r * .95 * math.cos(math.radians(a))))
                       for a in range(-80, 81, 10)], .035, sides=6)
    for sg in (-1, 1):
        h.spiral(m, "gold", at + Vector((sg * r * .35, 0, -r * .2)), (sg, 0, 0), (0, 0, 1), r * .22, r * .04, 1.3,
                 r * .07, r * .03, start=math.pi / 2)
    h.ball(m, "gold", at + Vector((0, .03, -r * .12)), r * .14, 12, 8)


def buffet_face(m, L, n_swags, first=True):
    """One skirted face of the buffet, local frame (x 0 .. L along it, y out, its plane at y 0): the marble top's
    moulded edge (its top the walked top), a gilt band under it, the linen skirt in round folds with a scalloped
    gold hem band, gold swags between gilt rosettes, a gold tassel under each rosette."""
    H = BUFFET[4]
    # the marble top's edge: square face flush with the builder's, a rounded lip, a cove under it
    # (the faces after the first start inside the first's depth: its end shows the corner, no overlapping lips)
    a = 0 if first else .75
    h.prism_yz(m, "marble", [(-.7, 6.4), (-.12, 6.4), (-.04, 6.46), (0, 6.58), (0, 7.18), (-.02, 7.3), (-.07, 7.37),
                             (-.14, H), (-.7, H)], a, L)
    # the gilt band under the top: a flat with a bead and a small cove
    h.prism_yz(m, "gold", [(-.6, 6.0), (-.1, 6.0), (-.05, 6.06), (-.03, 6.14), (-.03, 6.24), (-.05, 6.3), (-.02, 6.34),
                           (-.02, 6.4), (-.6, 6.4)], a, L)
    # the skirt and its hem band (gold, following the folds and the scallops)
    fy = lambda x, z: skirt_y(x, z, L)
    drape(m, "linen", 0, L, .2, SKIRT_TOP, fy, nx=int(L * 14), nz=10, zlo=lambda x: skirt_hem(x, L) + .45)
    drape(m, "gold", 0, L, 0, .62, fy, half=.022, nx=int(L * 14), nz=2, zlo=lambda x: skirt_hem(x, L))
    drape(m, "gold", 0, L, .6, .7, fy, half=.026, nx=int(L * 14), nz=1, zlo=lambda x: skirt_hem(x, L) + .55)
    # the swags and the rosettes and tassels at their ends
    js = [.62 + (L - 1.24) * i / n_swags for i in range(n_swags + 1)]   # the end rosettes clear of the corners
    for xa, xb in zip(js, js[1:]):
        gold_swag(m, xa, xb, 5.75)
    for i, x in enumerate(js):
        if 0 < i < n_swags and i * 2 == n_swags:
            shell(m, Vector((x, -.15, 5.45)), .75)
        else:
            rosette(m, Vector((x, -.25, 5.55)), r=.5)
        curtain_tassel(m, Vector((x, -.26, 5.15)), 3.2)


def buffet(m):
    """A07 cell 3: the dessert buffet in the builder's box (Unity x -32 .. -23.5, z 3 .. 17, top 7.4), in the hall's
    frame (Blender x = Unity x, y = Unity z). Its marble top, flush with the walked top; three skirted faces (east,
    south, north - the west one is against the wall); a linen column at each outer corner where the skirts meet."""
    x0, x1, z0, z1, H = BUFFET
    h.box(m, "marble", x0, x1 - .05, z0 + .05, z1 - .05, 6.85, H)
    faces = (
        (Matrix(((0, 1, 0, x1), (-1, 0, 0, z1), (0, 0, 1, 0), (0, 0, 0, 1))), z1 - z0, 4),    # east, along -y
        (Matrix(((-1, 0, 0, x1), (0, -1, 0, z0), (0, 0, 1, 0), (0, 0, 0, 1))), x1 - x0, 2),   # south, along -x
        (Matrix(((1, 0, 0, x0), (0, 1, 0, z1), (0, 0, 1, 0), (0, 0, 0, 1))), x1 - x0, 2),     # north, along +x
    )
    for f, (M, L, n) in enumerate(faces):
        k = len(m.bm.verts)
        buffet_face(m, L, n, first=f == 0)
        m.bm.verts.ensure_lookup_table()
        bmesh.ops.transform(m.bm, matrix=M, verts=m.bm.verts[k:])
    for y in (z0, z1):
        sy = 1 if y == z0 else -1
        h.tube(m, "linen", [Vector((x1 - .32, y + sy * .32, .1)), Vector((x1 - .32, y + sy * .32, SKIRT_TOP))], .11, sides=12)
        h.tube(m, "gold", [Vector((x1 - .32, y + sy * .32, 0)), Vector((x1 - .32, y + sy * .32, .7))], .125, sides=12)


def buffet_part(col):
    m = h.Mesh()
    buffet(m)
    # for the stills: its east face turned to +y (where the part cameras look from), centred
    x0, x1, z0, z1, _ = BUFFET
    bmesh.ops.transform(m.bm, matrix=Matrix.Rotation(math.pi / 2, 4, "Z") @ Matrix.Translation((-(x0 + x1) / 2, -(z0 + z1) / 2, 0)),
                        verts=m.bm.verts[:])
    return [m.finish("BH dessert buffet", col, bevel=0, merge=False)]


def scallop_lathe(m, slot, profile, at, R, n, depth, segs=96):
    """A turned shape whose outline is scalloped: profile [(rho, z), ...] in fractions of R (rho 0 = the axis),
    every ring's radius R * rho * (1 + depth * |cos(n theta / 2)|-ish), bottom up, closed at the axis."""
    mi = m.mat(slot)
    bm = m.bm
    at = Vector(at)
    rings = []
    for rho, z in profile:
        if rho <= 1e-6:
            rings.append(bm.verts.new(at + Vector((0, 0, z))))
            continue
        ring = []
        for i in range(segs):
            th = 2 * math.pi * i / segs
            k = 1 - depth + depth * abs(math.cos(n * th / 2))
            ring.append(bm.verts.new(at + Vector((R * rho * k * math.cos(th), R * rho * k * math.sin(th), z))))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        if isinstance(a, list) and isinstance(b, list):
            for i in range(segs):
                h.face(bm, [a[i], a[(i + 1) % segs], b[(i + 1) % segs], b[i]], mi, True)
        elif isinstance(b, list):
            for i in range(segs):
                h.face(bm, [a, b[(i + 1) % segs], b[i]], mi, True)
        elif isinstance(a, list):
            for i in range(segs):
                h.face(bm, [a[i], a[(i + 1) % segs], b], mi, True)


def tart(m, at, s=1.0):
    """A fruit tart: a fluted golden crust, a piped cream swirl, a strawberry with its green calyx."""
    at = Vector(at)
    scallop_lathe(m, "wax", [(0, 0), (.72, 0), (.95, .1 * s), (1, .14 * s), (.97, .155 * s), (.86, .15 * s), (0, .15 * s)],
                  at, .16 * s, 22, .07, 66)
    pts, rs = [], []
    for i in range(61):
        t = i / 60
        a = 2 * math.pi * 2.6 * t
        r = .085 * s * (1 - t * .85)
        pts.append(at + Vector((r * math.cos(a), r * math.sin(a), .17 * s + .09 * s * t)))
        rs.append(.04 * s * (1 - .5 * t))
    h.tube(m, "china", pts, .04 * s, sides=8, taper=lambda i: rs[i])
    top = at + Vector((0, 0, .27 * s))
    h.ball(m, "velvet", top + Vector((0, 0, .06 * s)), .065 * s, 12, 8, scale=(1, 1, 1.3))
    for k in range(5):
        a = 2 * math.pi * k / 5
        d = Vector((math.cos(a), math.sin(a), .2)).normalized()
        h.blade(m, "runner", [top + Vector((0, 0, .13 * s)) + d * t * .07 * s for t in (0, .5, 1)], [.02 * s, .03 * s, .005 * s],
                Vector((0, 0, 1)), thick=.008 * s, sides=4)


def choc_cake(m, at, s=1.0, turn=0.0):
    """A square layered chocolate cake: sponge and cream layers, a glazed top, a gold curl on it."""
    at = Vector(at)
    w = .14 * s
    z = 0
    for slot_name, hgt in (("chocolate", .07), ("linen", .025), ("chocolate", .06), ("linen", .025), ("chocolate", .04)):
        k = len(m.bm.verts)
        h.box(m, slot_name, -w, w, -w, w, z, z + hgt * s)
        m.bm.verts.ensure_lookup_table()
        bmesh.ops.transform(m.bm, matrix=Matrix.Translation(at) @ Matrix.Rotation(turn, 4, "Z"), verts=m.bm.verts[k:])
        z += hgt * s
    h.spiral(m, "gold", at + Vector((0, 0, z + .03 * s)), (1, 0, 0), (0, 0, 1), .035 * s, .01 * s, 1.5, .01 * s, .006 * s, n=24)
    h.ball(m, "velvet", at + Vector((.06 * s, .05 * s, z + .04 * s)), .04 * s, 10, 6, scale=(1, 1, 1.25))


def macaron(m, at, slot_name, s=1.0):
    """A macaron: two domed shells with their ruffled feet, a cream filling."""
    at = Vector(at)
    for z0, up in ((0, 1), (.13 * s, -1)):
        prof = [(0, 0), (.11 * s, 0), (.125 * s, .02 * s), (.12 * s, .04 * s), (.08 * s, .065 * s), (0, .07 * s)]
        if up < 0:
            prof = [(r, -q) for r, q in reversed(prof)]
        h.lathe(m, slot_name, prof if up > 0 else prof, at=(at.x, at.y, at.z + z0), segs=18)
    h.lathe(m, "linen", [(0, .04 * s), (.1 * s, .04 * s), (.105 * s, .065 * s), (.1 * s, .09 * s), (0, .09 * s)],
            at=(at.x, at.y, at.z), segs=18)


CAKE_H = 2.6     # a cake stand on the buffet's top: a picture without a collider, about two pawns tall


def cake_stand(m, at=(0, 0, 0)):
    """A07 cell 4: a three-tier gilt cake stand - a domed foot, a column with knops, three scalloped white china
    plates with gilt rims (biggest at the foot), a gilt loop handle on top; tarts, layered chocolate cakes and
    macarons on the plates."""
    at = Vector(at)
    x, y, z = at
    # the foot and the column
    h.lathe(m, "gold", [(0, 0), (.5, 0), (.52, .04), (.48, .08), (.36, .14), (.3, .24), (.16, .3), (.12, .36), (.15, .4),
                        (.1, .44), (0, .44)], at=at, segs=36)
    levels = ((.62, .95), (1.36, .77), (2.0, .6))
    zc = .44
    for zl, R in levels:
        h.tube(m, "gold", [Vector((x, y, zc)), Vector((x, y, zl - .02))], .055, sides=14)
        zk = (zc + zl) / 2
        h.lathe(m, "gold", [(0, -.08), (.09, -.07), (.12, 0), (.09, .07), (0, .08)], at=(x, y, zk), segs=16)
        # the plate: scalloped, a shallow dish, its gilt rim; a gilt collar under it
        scallop_lathe(m, "china", [(0, 0), (.3, 0), (.8, .015), (.97, .045), (1, .07), (.98, .085), (.9, .06), (.3, .045), (0, .045)],
                      (x, y, zl), R, 14, .05, 112)
        pts = []
        for i in range(225):
            th = 2 * math.pi * i / 224
            k = .95 + .05 * abs(math.cos(14 * th / 2))
            pts.append(Vector((x + R * .995 * k * math.cos(th), y + R * .995 * k * math.sin(th), zl + .075)))
        h.tube(m, "gold", pts, .018, sides=6)
        h.lathe(m, "gold", [(0, -.06), (.16, -.05), (.2, -.01), (.12, 0)], at=(x, y, zl), segs=20)
        zc = zl + .045
    # the handle: a knop, a stem and the gilt loop
    zt = zc
    h.tube(m, "gold", [Vector((x, y, zt)), Vector((x, y, zt + .22))], .05, sides=12)
    h.lathe(m, "gold", [(0, -.06), (.08, -.05), (.1, 0), (.08, .05), (0, .06)], at=(x, y, zt + .1), segs=14)
    hc = Vector((x, y, zt + .42))
    # the sheet's handle: one tall loop, waisted, its head lobed, small scrolls curling out at its foot
    pts = []
    for i in range(73):
        th = 2 * math.pi * i / 72
        r = 1 + .12 * math.cos(3 * (th - math.pi / 2)) * max(0.0, math.sin(th))
        wx = .13 * (1 - .22 * math.exp(-((math.sin(th) + .1) / .3) ** 2)) * r
        pts.append(hc + Vector((wx * math.cos(th), 0, .06 + .2 * r * math.sin(th))))
    h.tube(m, "gold", pts, .03, sides=8)
    for sg in (-1, 1):
        h.spiral(m, "gold", hc + Vector((sg * .1, 0, -.1)), (sg, 0, 0), (0, 0, 1), .06, .012, 1.3, .022, .012,
                 start=math.pi / 2, n=30)
    h.ball(m, "gold", hc + Vector((0, 0, .3)), .045, 10, 6)
    h.ball(m, "gold", hc + Vector((0, 0, -.12)), .05, 10, 6)
    # the desserts
    s = 1.1
    top = levels[2][0] + .05
    for k in range(4):
        a = 2 * math.pi * (k + .5) / 4
        tart(m, (x + .36 * math.cos(a), y + .36 * math.sin(a), top), s)
    mid = levels[1][0] + .05
    for k in range(5):
        a = 2 * math.pi * k / 5
        p = (x + .5 * math.cos(a), y + .5 * math.sin(a), mid)
        if k % 2:
            choc_cake(m, p, 1.15, turn=a)
        else:
            tart(m, p, s)
    low = levels[0][0] + .05
    for k in range(8):
        a = 2 * math.pi * k / 8
        p = (x + .7 * math.cos(a), y + .7 * math.sin(a), low)
        if k % 3 == 2:
            tart(m, p, s)
        else:
            macaron(m, p, ("piece_white", "wax", "linen")[k % 3] if k % 3 != 2 else "wax", 1.1)


def cake_part(col):
    m = h.Mesh()
    cake_stand(m)
    return [m.finish("BH cake stand", col, bevel=0, merge=False)]


FOUNTAIN = dict(base=7.4, bowl_r=4.2, bowl_top=7.9, tier_r=(3.3, 2.3, 1.3), tier_top=(10.733, 13.567, 16.4),
                still=.45, crown_r=.35, crown_top=17.6, finial_r=.6, finial_top=18.6)


def polar_surface(m, slot, rfn, z0, z1, segs=120, nz=8, cap=False):
    """A closed tube round the axis between z0 and z1 whose radius is rfn(theta, z) (the chocolate's streams)."""
    mi = m.mat(slot)
    bm = m.bm
    rings = []
    for j in range(nz + 1):
        z = z0 + (z1 - z0) * j / nz
        rings.append([bm.verts.new(Vector((rfn(2 * math.pi * i / segs, z) * math.cos(2 * math.pi * i / segs),
                                           rfn(2 * math.pi * i / segs, z) * math.sin(2 * math.pi * i / segs), z)))
                      for i in range(segs)])
    for a, b in zip(rings, rings[1:]):
        for i in range(segs):
            h.face(bm, [a[i], a[(i + 1) % segs], b[(i + 1) % segs], b[i]], mi, True)


def streams(seed, R, n):
    """The chocolate's side: a sheet just inside R with n rounded streams standing out to R, of uneven widths,
    widening a little as they run down."""
    import random
    rnd = random.Random(seed)
    ss = [(rnd.uniform(0, 2 * math.pi), rnd.uniform(.05, .12), rnd.uniform(.6, 1.0)) for _ in range(n)]

    def r(th, z, top):
        f = 0.0
        down = min(1.0, max(0.0, (top - z) / 3.0))
        for c, w, a in ss:
            d = math.atan2(math.sin(th - c), math.cos(th - c))
            ww = w * (1 + .5 * down)
            f = max(f, a * math.exp(-(d / ww) ** 2))
        return R - .07 + .065 * f
    return r, ss


def gold_basin_band(m, R, top, still, lobes):
    """The sheet's gilt basin as the tier's still band: a lobed gold body swelling out to R, a rolled rim at R whose
    top is the walked top, all inside R."""
    zb = top - still
    scallop_lathe(m, "gold", [(0, 0), ((R - .2) / R, 0), ((R - .1) / R, .1), ((R - .04) / R, .22), ((R - .02) / R, .3),
                              ((R - .05) / R, .33), (0, .33)], (0, 0, zb), R, lobes, .035, max(72, lobes * 6))
    h.lathe(m, "gold", [(R - .3, top - .14), (R - .06, top - .15), (R - .005, top - .1), (R, top - .05), (R - .02, top - .01),
                        (R - .1, top), (R - .2, top), (R - .26, top - .03)], segs=max(72, lobes * 6))


def chocolate_pool(m, r0, r1, z):
    """The chocolate standing in a basin: a flat glossy ring, a little meniscus at its edges."""
    h.lathe(m, "chocolate", [(r0, z - .06), (r1, z - .06), (r1, z - .01), (r1 - .04, z), (r0 + .04, z), (r0, z - .01)],
            segs=96)


def drips(m, ss, R, z, top):
    """Drops hanging where the streams leave the gold band: teardrops on the streams' crowns, inside R."""
    for c, w, a in ss:
        rr = R - .07 + .065 * a - .04
        p = Vector((rr * math.cos(c), rr * math.sin(c), z))
        h.ball(m, "chocolate", p - Vector((0, 0, .12 * a)), .045 + .03 * a, 10, 8, scale=(1, 1, 1.6))


def fountain(m):
    """A08: the chocolate fountain at the builder's numbers, its axis at x y 0 (Unity Fountain), z = Unity height.
    The bowl (r 4.2, 7.4 .. 7.9) a lobed gold basin with a rolled rim and a fluted foot, chocolate standing in it;
    the three tiers (r 3.3 / 2.3 / 1.3 to 10.733 / 13.567 / 16.4): each the sheet's gold basin as its top 0.45 m (the
    still band under the rim), the flowing chocolate below it (a glossy sheet with rounded streams and drops), a pool
    in it; the crown (r 0.35 to 17.6) a fluted gold pedestal and over it the sheet's gold crown (in the finial's cone,
    r 0.6 to 18.6). Everything inside its cylinder; walked tops flush. The ripples stay Unity's (ChocolateRipples)."""
    F = FOUNTAIN
    base, BR, BT = F["base"], F["bowl_r"], F["bowl_top"]
    # the bowl: a fluted foot set in, the lobed body, the rolled rim (its top the walked 7.9)
    scallop_lathe(m, "gold", [(0, 0), (3.6 / BR, 0), (3.72 / BR, .05), (3.7 / BR, .12), (3.95 / BR, .16), (4.12 / BR, .3),
                              (4.18 / BR, .38), (0, .38)], (0, 0, base), BR, 40, .025, 240)
    h.lathe(m, "gold", [(3.6, base), (3.78, base + .02), (3.8, base + .06), (3.66, base + .1)], segs=160)
    h.lathe(m, "gold", [(3.85, BT - .14), (4.14, BT - .15), (4.195, BT - .1), (BR, BT - .05), (4.18, BT - .01), (4.08, BT),
                        (3.98, BT), (3.92, BT - .03)], segs=200)
    chocolate_pool(m, F["tier_r"][0] - .1, 3.96, BT - .015)
    below = BT
    for k, (R, T) in enumerate(zip(F["tier_r"], F["tier_top"])):
        side_top = T - F["still"] + .05
        lobes = (32, 24, 16)[k]
        # the side: the sheet's lobed gold basin carried down the whole tier (just inside R) ...
        scallop_lathe(m, "gold", [(0, 0), ((R - .05) / R, 0), ((R - .05) / R, side_top - below), (0, side_top - below)],
                      (0, 0, below - .02), R, lobes, .03, lobes * 8)
        # ... and the chocolate pouring over it in streams of uneven width, some reaching the foot, some ending in a drop
        import random
        rnd = random.Random(11 + k)
        n = (16, 12, 8)[k]
        for i in range(n):
            c = 2 * math.pi * (i + rnd.uniform(-.25, .25)) / n
            d = Vector((math.cos(c), math.sin(c), 0))
            w0 = rnd.uniform(.25, .45) * (1, .9, .8)[k]
            full = rnd.random() < .6
            z_end = below + .05 if full else side_top - rnd.uniform(.8, side_top - below - .3)
            pts, ws = [], []
            for j in range(13):
                u = j / 12
                z = side_top + .02 - (side_top + .02 - z_end) * u
                wob = .04 * math.sin(u * 7 + i)
                q = Vector((math.cos(c + wob / R), math.sin(c + wob / R), 0))
                pts.append(q * (R - .035) + Vector((0, 0, z)))
                ws.append(w0 * (1 + .35 * u) if full else w0 * (1 - .55 * u ** 2))
            h.blade(m, "chocolate", pts, ws, lambda j, pts=pts: Vector((pts[j].x, pts[j].y, 0)).normalized(), thick=.06,
                    sides=10)
            if not full:
                h.ball(m, "chocolate", pts[-1] + d * .0 - Vector((0, 0, .08)), w0 * .3, 10, 8, scale=(1, .55, 1.5))
        # the foot: the streams spreading into the pool under it (a low flare, 4 cm over the walked ring)
        h.lathe(m, "chocolate", [(R - .1, below + .2), (R - .03, below + .08), (R + .05, below + .04), (R + .14, below + .01),
                                 (R + .1, below - .02), (R - .1, below - .02)], segs=(160, 120, 84)[k])
        gold_basin_band(m, R, T, F["still"], lobes)
        r_in = F["tier_r"][k + 1] - .1 if k + 1 < len(F["tier_r"]) else F["crown_r"] - .05
        chocolate_pool(m, r_in, R - .2, T - .015)
        below = T
    # the crown's pedestal: a moulded foot, a fluted shaft, a collar (its top the crown cylinder's, 17.6)
    T, CR, CT = F["tier_top"][-1], F["crown_r"], F["crown_top"]
    h.lathe(m, "gold", [(0, T), (CR, T), (CR, T + .08), (.3, T + .12), (.32, T + .18), (.25, T + .24), (0, T + .24)], segs=36)
    scallop_lathe(m, "gold", [(0, 0), (.25 / CR, 0), (.21 / CR, .3), (.2 / CR, .6), (.22 / CR, .75), (0, .75)],
                  (0, 0, T + .24), CR, 12, .12, 72)
    h.lathe(m, "gold", [(0, CT - .2), (.3, CT - .2), (CR, CT - .14), (CR, CT - .05), (.3, CT), (0, CT)], segs=36)
    # the crown (the sheet's): a band, five points each with a ball, smaller balls between, a ball on top
    zc = CT
    h.lathe(m, "gold", [(0, zc), (.42, zc), (.46, zc + .04), (.46, zc + .12), (.5, zc + .16), (.5, zc + .2), (0, zc + .2)], segs=40)
    for i in range(5):
        a = 2 * math.pi * i / 5
        d = Vector((math.cos(a), math.sin(a), 0))
        base_p = Vector((0, 0, zc + .2)) + d * .44
        tip = Vector((0, 0, zc + .62)) + d * .48
        h.blade(m, "gold", [base_p, base_p.lerp(tip, .5) + d * .04, tip], [.34, .2, .06], d, thick=.06, sides=6,
                side=Vector((-math.sin(a), math.cos(a), 0)))
        h.ball(m, "gold", tip + Vector((0, 0, .06)), .075, 12, 8)
        a2 = a + math.pi / 5
        d2 = Vector((math.cos(a2), math.sin(a2), 0))
        h.ball(m, "gold", Vector((0, 0, zc + .26)) + d2 * .47, .05, 10, 6)
    h.lathe(m, "gold", [(0, zc + .2), (.3, zc + .2), (.25, zc + .45), (.12, zc + .6), (0, zc + .64)], segs=24)
    h.ball(m, "gold", Vector((0, 0, zc + .74)), .1, 14, 8)


def fountain_part(col):
    m = h.Mesh()
    fountain(m)
    return [m.finish("BH chocolate fountain", col, bevel=0, merge=False)]


def pelmet_part(col):
    m = h.Mesh()
    pelmet(m, -3.6, 3.6)
    return [m.finish("BH stage pelmet", col, bevel=0)]


def stage_front_part(col):
    m = h.Mesh()
    stage_front(m, -5.1, 5.1)
    return [m.finish("BH stage front", col, bevel=.02)]


h.CLOSE.update({
    "fountain": ((0, 3.1, 10.0), (-.35, 1, .2), 3.6, 40),
    "cake": ((0, 0, 1.5), (-.4, 1, .45), 2.6, 40),
    "buffet": ((5.6, 4.1, 5.0), (.55, 1, .15), 6.5, 40),
    "bass": ((0, 0, 8.6), (.6, 1, -.1), 2.4, 40),
    "pleat": ((0, 0, .6), (-.35, 1, .25), 2.6, 40),
    "pelmet": ((-2.4, 0, .9), (-.4, 1, .15), 3.2, 40),
    "stage_front": ((-3.4, 0, 6.0), (-.5, 1, .3), 4.5, 40),
})
h.PARTS.update({
    "fountain": (fountain_part, "BH_A08_chocolate_fountain.png", (10, 20, 610, 1000), None),
    "cake": (cake_part, "BH_A07_stage_buffet.png", (1020, 300, 1245, 750), None),
    "buffet": (buffet_part, "BH_A07_stage_buffet.png", (612, 420, 1000, 705), None),
    "bass": (bass_part, "BH_A07_stage_buffet.png", (1250, 240, 1525, 765), None),
    "pleat": (pleat_part, "BH_A07_stage_buffet.png", (350, 450, 610, 675), None),
    "pelmet": (pelmet_part, "BH_A07_stage_buffet.png", (350, 450, 610, 675), None),
    "stage_front": (stage_front_part, "BH_A07_stage_buffet.png", (10, 410, 345, 745), None),
})


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bpy.ops.wm.read_factory_settings(use_empty=True)
    name = args[args.index("--part") + 1] if "--part" in args else "stage_front"
    col = bpy.data.collections.new("BH B5 " + name)
    bpy.context.scene.collection.children.link(col)
    objs = h.PARTS[name][0](col)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b5_stage_%s.blend" % name))
    h.part_stills(name, objs)

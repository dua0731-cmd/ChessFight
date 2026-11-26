"""B3 the royal dais and the king's balcony (seoungtest R837, 2026-11-25; PLAN.md B3), every part after its cell of the
R836 sheet BH_A06 (dais face, stair balustrade, head table, balcony with canopy, throne).

    blender -b --factory-startup --python bh_b3_dais_model.py -- [stills] [export]
    -> bh_b3_dais.blend; "stills": stills/b3/parts/<part>_compare.png + stills/b3/dais.png;
       "export": Assets/Resources/BanquetHall/Models/B3/*.fbx (never while Unity is in Play).

Numbers are GrandBanquetHallBuilder's (Dais, RoyalBalcony; read off bh_blockout.json); colliders and floors stay its:
  dais       x -32 .. 32, z 56 .. 72, top 10.4 (a floor); its front a sheer face at z 56 - the cloche lifts' platters
             rise 0.1 m off it (x 14.8 - 25.2), so nothing on the face stands out more than 8 cm
  balustrade on the dais edge, 1.1 high, z 56 - 56.3: x -32 .. -5.6, -2.9 .. 14.4, 25.6 .. 32 (the openings: the
             boarding place between the lamp posts, the lifts); the gallery's ends: low walls (2.6) at x -7.3 and 26
  head table W x -24 .. -6, E 6 .. 24, z 61.48 .. 67.48, top 14.8 (floors); the builder's candles at x +-9 / 15 / 21
  balcony    x -6 .. 6, z 60.58 .. 72, top 16 (a floor; its front where the last ropes land - open); rails W/E 16 - 17.1
             (x -6.3 .. -6, 6 .. 6.3); thrones at x +-4.2, z 69.4 .. 70.8; the canopy at 23.8 - 24.6, z 69 .. 72
"""
import math, os, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else \
    r"C:\Users\Makebot2\Documents\GitHub\ChessFight\ArtSource\PawnRush\BanquetHall"
sys.path.insert(0, HERE)
import importlib
import bh_kit as k
import bh_parts as P
import bh_stills as st
for _m in (k, P, st):
    importlib.reload(_m)
_argv = sys.argv
sys.argv = [a for a in sys.argv if a not in ("stills", "export")]   # bh_b1's helpers only
import bh_b1_servery_model as B1
sys.argv = _argv
from bh_b1_servery_model import basis, rect, arched, frustum, frame_ring, plane_prism, frame_bead

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
A06 = os.path.join(st.SHEETS, "BH_A06_dais_balcony.png")
CELLS = {"face": (A06, (5, 232, 402, 762)), "balustrade": (A06, (388, 430, 722, 745)), "table": (A06, (732, 598, 1078, 762)),
         "balcony": (A06, (1078, 322, 1332, 762)), "throne": (A06, (1342, 402, 1528, 752))}

DAIS_Y, DAIS_Z, HALL_Z1 = 10.4, 56.0, 72.0
BAL_Y, BAL_X, BAL_Z0 = 16.0, 6.0, 60.58
TABLE_Z0, TABLE_Z1, HEAD_Y = 61.48, 67.48, 14.8


def U(x, y, z):
    return Vector((x, z, y))


def collection(name):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(c)
    for o in list(c.objects):
        bpy.data.objects.remove(o)
    return c


def face_panel(bm, m, w, h, mat=P.MARBLE, clip=.0, depth=.035, bead=.03):
    """A marble panel in a gold frame on a face (m: x across, y up from its foot, z out): a raised, chamfered field
    with clipped (cove) corners when clip > 0, the gold bead round it, all within `depth` + bead of the face."""
    outline = arched(w, h, clip) if clip > 0 else rect(w, h)
    P.merge(bm, P.transform(frustum(outline, .04, depth, mat), m))
    P.merge(bm, P.transform(frame_ring(outline, bead), m @ Matrix.Translation((0, 0, bead * .6))))


# ------------------------------------------------------------------ the dais: its front face and its top

LIFT_X = (14.8, 25.2)   # where the platters rise 0.1 m off the dais face: nothing there stands out over 8 cm


def dais(col):
    """The dais front (BH_A06 first cell) along z 56, 64 m in eight 8 m bays (the sheet's proportions: a bay 0.77 of
    its height): a plinth with long gold-framed panels, a broad pilaster at every bay joint (a gold base block and a
    gold capital), between them a tall clipped-corner panel flanked by two narrow ones in bold gold frames, a frieze of
    gold egg-and-dart, a cornice with a gilt top. Relief 22 cm deep, except the bays the cloche lifts' platters pass
    (x 14.8 - 25.2: 6 cm, they rise 0.1 m off the face). Its top a marble floor flush at 10.4, gold lines inlaid."""
    bm = bmesh.new()
    z = DAIS_Z
    out = Vector((0, -1, 0))   # Blender: the face looks south (-y)
    P.merge(bm, P.box(U(-32, 0, z), U(32, DAIS_Y, HALL_Z1), P.MARBLE))

    def proud(x0, x1):
        return .06 if (x1 > LIFT_X[0] and x0 < LIFT_X[1]) else .22

    def band(x0, x1, y0, y1, p, mat):
        P.merge(bm, P.box(U(x0, y0, z - p), U(x1, y1, z), mat))

    for i in range(8):
        x0, x1 = -32 + 8 * i, -24 + 8 * i
        r = proud(x0, x1)
        bead = .065 if r > .1 else .028
        # plinth, its gold fillet; frieze; cornice and its gilt top
        band(x0, x1, 0, 1.4, r * .55, P.MARBLE)
        band(x0, x1, 1.4, 1.5, r * .62, P.GOLD)
        band(x0, x1, 8.5, 9.7, r * .45, P.MARBLE)
        band(x0, x1, 8.4, 8.5, r * .55, P.GOLD)
        band(x0, x1, 9.7, 9.85, r * .75, P.GOLD)
        band(x0, x1, 9.85, 10.2, r * .9, P.MARBLE)
        band(x0, x1, 10.2, DAIS_Y, r, P.GOLD)
        # egg-and-dart in the frieze: gold eggs half sunk in it, a dart between
        x = x0 + .25
        while x < x1 - .1:
            egg = bmesh.new()
            bmesh.ops.create_uvsphere(egg, u_segments=12, v_segments=8, radius=1.0)
            P.transform(egg, Matrix.Translation(U(x, 9.1, z - r * .45)) @ Matrix.Diagonal((.2, r * .5 + .02, .42, 1)))
            for f in egg.faces:
                f.material_index = P.GOLD
                f.smooth = True
            P.merge(bm, egg)
            P.merge(bm, P.box(U(x + .23, 8.75, z - r * .45 - .03), U(x + .27, 9.45, z - r * .45 + .01), P.GOLD))
            x += .5
        # the panels between the pilasters (x0 + .6 .. x1 - .6)
        cx = (x0 + x1) / 2
        m = lambda u, y: basis(None, out, U(cx + u, y, z - r * .2))
        face_panel(bm, m(0, 2.1), 3.0, 5.9, P.MARBLE, .5, r * .35, bead)
        for s in (-1, 1):
            face_panel(bm, m(s * 2.35, 2.5), .85, 5.1, P.MARBLE, .22, r * .3, bead * .8)
        P.merge(bm, P.box(U(x0 + .6, 1.5, z - r * .2), U(x1 - .6, 8.4, z), P.MARBLE))   # the field the panels sit on
        mp = lambda u: basis(None, out, U(cx + u, .3, z - r * .55))
        face_panel(bm, mp(0), 4.6, .8, P.MARBLE, 0.0, r * .2, bead * .7)
        for s in (-1, 1):
            face_panel(bm, mp(s * 3.25), .7, .8, P.MARBLE, 0.0, r * .2, bead * .7)
    # the pilasters at the joints (shared by the bays either side)
    for j in range(9):
        xp = -32 + 8 * j
        r = max(proud(xp - .6, xp - .55), proud(xp + .55, xp + .6))
        a, b = max(-32, xp - .6), min(32, xp + .6)
        band(a, b, 0, 1.4, r * .75, P.MARBLE)
        band(a - .02, b + .02, 1.5, 1.95, r * .85, P.GOLD)
        band(a + .05, b - .05, 1.95, 7.9, r * .7, P.MARBLE)
        band(a - .02, b + .02, 7.9, 8.15, r * .82, P.GOLD)
        band(a - .06, b + .06, 8.15, 8.5, r * .95, P.GOLD)
        band(a - .06, b + .06, 8.5, 9.7, r * .6, P.MARBLE)
        face_panel(bm, basis(None, out, U(xp if -32 < xp < 32 else (a + b) / 2, 2.3, z - r * .7)), (b - a) - .45, 5.2, P.MARBLE, 0.0,
                   r * .12, bead * .6)
    for i in range(9):
        xx = -32 + 8 * i
        P.merge(bm, P.box(U(xx - .04, DAIS_Y - .002, z + .3), U(xx + .04, DAIS_Y + .001, HALL_Z1), P.GOLD))
    P.merge(bm, P.box(U(-32, DAIS_Y - .002, z + .96), U(32, DAIS_Y + .001, z + 1.04), P.GOLD))
    ob = P.obj(col, "BH3 dais", bm)
    return P.finish(ob, bevel=.008)


# ------------------------------------------------------------------ the balustrades (BH_A06 second cell's rail)

def baluster(h):
    s = h
    prof = [(0, 0), (.12, 0), (.12, .06), (.095, .09), (.08, .14), (.135, .32), (.145, .42), (.11, .56), (.06, .68), (.055, .74),
            (.085, .78), (.06, .82), (.095, .88), (.095, .94), (0, .94)]
    return k.lathe([(r * s, zz * s) for r, zz in prof], 14, mat=P.GOLD)


def newel(bm, f, u, height, lamp=False):
    """A walnut newel (BH_A06): a plinth, a shaft with a sunk gold-framed panel each face, a stepped gold cap, a gold ball
    on a turned neck."""
    def box(x0, x1, y0, y1, h0, h1, mat):
        return P.transform(P.box((x0, y0, h0), (x1, y1, h1), mat), f)
    if lamp:   # up to the lamp's globe: a slim walnut post, a gold cup holding the globe (12.2 over a 10.4 floor)
        P.merge(bm, box(u - .16, u + .16, -.16, .16, 0, .3, P.WALNUT))
        P.merge(bm, box(u - .17, u + .17, -.17, .17, .3, .36, P.GOLD))
        P.merge(bm, box(u - .13, u + .13, -.13, .13, .36, 1.5, P.WALNUT))
        P.merge(bm, box(u - .16, u + .16, -.16, .16, 1.5, 1.58, P.GOLD))
        cup = [(0.0, 0.0), (.1, 0.0), (.08, .06), (.05, .12), (.06, .16), (.16, .2), (.24, .22), (.25, .24), (.2, .23), (0.0, .2)]
        P.merge(bm, P.transform(k.lathe(cup, 20, mat=P.GOLD), f @ Matrix.Translation((u, 0, 1.58))))
        return
    P.merge(bm, box(u - .26, u + .26, -.26, .26, 0, .3, P.WALNUT))
    P.merge(bm, box(u - .27, u + .27, -.27, .27, .3, .36, P.GOLD))
    P.merge(bm, box(u - .21, u + .21, -.21, .21, .36, height + .1, P.WALNUT))
    for n in (Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))):
        side = Vector((0, 0, 1)).cross(n)
        c = Vector((u, 0, 0)) + n * .215
        ring = [c + side * .12 + Vector((0, 0, .5)), c + side * .12 + Vector((0, 0, height - .05)), c - side * .12 + Vector((0, 0, height - .05)),
                c - side * .12 + Vector((0, 0, .5))]
        P.merge(bm, P.transform(P.tube(ring + [ring[0]], .016, 6), f))
    P.merge(bm, box(u - .25, u + .25, -.25, .25, height + .1, height + .16, P.GOLD))
    P.merge(bm, box(u - .28, u + .28, -.28, .28, height + .16, height + .24, P.GOLD))
    R = .17
    ball = [(0.0, 0.0), (.13, 0.0), (.13, .03), (.08, .06), (.06, .1), (.07, .13)] + \
           [(R * math.sin(math.pi * i / 12), .13 + R - R * math.cos(math.pi * i / 12)) for i in range(3, 13)]
    P.merge(bm, P.transform(k.lathe(ball, 20, mat=P.GOLD), f @ Matrix.Translation((u, 0, height + .24))))


def balustrade(bm, a, b, floor, height=1.1, depth=.3, newel_a=True, newel_b=True, lamps=()):
    """A rail from a to b (Unity x, z points at the rail's middle line) on a floor: a marble plinth with a gold fillet,
    stout gold vase balusters 0.4 apart, a rounded walnut handrail on a gold fillet, a panelled walnut newel at each end."""
    A, B = U(a[0], floor, a[1]), U(b[0], floor, b[1])
    d = B - A
    L = d.length
    along = d.normalized()
    side = Vector((0, 0, 1)).cross(along)
    f = Matrix((along, side, Vector((0, 0, 1)))).transposed().to_4x4()
    f.translation = A
    def box(x0, x1, y0, y1, h0, h1, mat):
        return P.transform(P.box((x0, y0, h0), (x1, y1, h1), mat), f)
    P.merge(bm, box(0, L, -depth / 2, depth / 2, 0, .16, P.MARBLE))
    P.merge(bm, box(0, L, -depth / 2 - .02, depth / 2 + .02, .16, .2, P.GOLD))
    top = height - .17
    P.merge(bm, box(0, L, -.1, .1, top - .03, top, P.GOLD))
    rail = [(-.13, 0.0), (.13, 0.0), (.14, .05), (.12, .11), (.06, .16), (0.0, .17), (-.06, .16), (-.12, .11), (-.14, .05)]
    P.merge(bm, P.transform(P.sweep([Vector((0, 0, top)), Vector((L, 0, top))], [(-x, y) for x, y in rail], P.WALNUT), f))
    n = max(1, int((L - .6) / .4))
    for i in range(n + 1):
        u = .3 + (L - .6) * i / max(1, n)
        P.merge(bm, P.transform(baluster(top - .23), f @ Matrix.Translation((u, 0, .2))))
    for u, on, end in ((0.0, newel_a, "a"), (L, newel_b, "b")):
        if on:
            newel(bm, f, u, height, end in lamps)


def low_wall(bm, x0, x1, z0, z1, floor, h):
    """A gallery's end: a low walnut wall, panelled both faces, a gold cap."""
    P.merge(bm, P.box(U(x0, floor, z0), U(x1, floor + h - .1, z1), P.WALNUT))
    P.merge(bm, P.box(U(x0 - .04, floor + h - .1, z0 - .04), U(x1 + .04, floor + h, z1 + .04), P.GOLD))
    long_x = (x1 - x0) > (z1 - z0)
    length = (x1 - x0) if long_x else (z1 - z0)
    n = max(1, round(length / 1.6))
    for s in (-1, 1):
        for i in range(n):
            t = (i + .5) / n
            if long_x:
                at, out = U(x0 + (x1 - x0) * t, floor + .3, (z1 if s > 0 else z0)), Vector((0, s, 0))
            else:
                at, out = U((x1 if s > 0 else x0), floor + .3, z0 + (z1 - z0) * t), Vector((s, 0, 0))
            m = basis(None, out, at)
            face_panel(bm, m, length / n - .3, h - .8, P.WALNUT, .15, .04, .02)


def rails(col):
    bm = bmesh.new()
    for a, b, na, nb, lamps in (((-32, 56.15), (-5.75, 56.15), False, True, "b"), ((-2.75, 56.15), (14.4, 56.15), True, True, "a"),
                                ((25.6, 56.15), (32, 56.15), True, False, "")):
        balustrade(bm, a, b, DAIS_Y, newel_a=na, newel_b=nb, lamps=lamps)
    low_wall(bm, -7.3, -7.0, 56.3, 61.48, DAIS_Y, 2.6)
    low_wall(bm, 26.0, 26.3, 56.3, 61.78, DAIS_Y, 2.6)
    low_wall(bm, 24.0, 26.0, 61.48, 61.78, DAIS_Y, 2.6)
    ob = P.obj(col, "BH3 rails", bm)
    return P.finish(ob, bevel=.006)


# ------------------------------------------------------------------ the head tables (BH_A06 third cell)

def draped(col, name, x0, x1, z0, z1, top, drop_to=.25, out=.15):
    """A table under a cloth draped on all four sides (still folds), a gold hem, a border of fleurs-de-lis, a red
    velvet runner across the top with a gold damask middle and gold edges (BH_A06's head table)."""
    bm = bmesh.new()
    # the perimeter: round the table, the cloth `out` past it, corners rounded
    per = []
    def run(a, b, n_):
        return [a + (b - a) * i / n_ for i in range(n_)]
    corners = [(Vector((x0, z0)), math.pi), (Vector((x1, z0)), 1.5 * math.pi), (Vector((x1, z1)), 0.0), (Vector((x0, z1)), .5 * math.pi)]
    pts = []
    for ci, (c, a0) in enumerate(corners):
        for i in range(7):
            a = a0 + math.pi / 2 * i / 6
            pts.append((c + Vector((math.cos(a), math.sin(a))) * out, Vector((math.cos(a), math.sin(a)))))
        nxt = corners[(ci + 1) % 4][0]
        a_end = a0 + math.pi / 2
        n_ = Vector((math.cos(a_end), math.sin(a_end)))
        start, end = c + n_ * out, nxt + n_ * out
        for p in run(start, end, max(2, int((end - start).length / .12)))[1:]:
            pts.append((p, n_))
    s_acc = [0.0]
    for i in range(1, len(pts)):
        s_acc.append(s_acc[-1] + (pts[i][0] - pts[i - 1][0]).length)
    drop = top + .01 - drop_to
    def at(i, t, extra=0.0):
        p, n_ = pts[i]
        y = top + .01 - drop * t
        d = top + .01 - y
        roll = .02 + (out - .02) * math.sin(min(1.0, d / .2) * math.pi / 2)
        a = .06 * t ** 1.3 * (math.sin(2 * math.pi * s_acc[i] / 1.1) + .35 * math.sin(2 * math.pi * s_acc[i] / .5 + 1.3))
        q = p - n_ * out + n_ * (roll + a + extra)
        return U(q.x, y, q.y)
    nt = 20
    grid = [[bm.verts.new(at(i, (j / nt) ** 1.7)) for j in range(nt + 1)] for i in range(len(pts))]
    for i in range(len(pts)):
        i2 = (i + 1) % len(pts)
        for j in range(nt):
            f = bm.faces.new((grid[i][j], grid[i][j + 1], grid[i2][j + 1], grid[i2][j]))
            f.material_index = P.LINEN
            f.smooth = True
    bm.faces.new([g[0] for g in grid]).material_index = P.LINEN
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for line_h, r in ((.06, .07), (.2, .018), (1.45, .022)):
        t = 1 - line_h / drop
        ring = [at(i, t, .012) for i in range(len(pts))]
        P.merge(bm, P.tube(ring + [ring[0]], r, 6))
    t_foot = 1 - .25 / drop
    s = .45
    while s < s_acc[-1] - .3:
        i = min(range(len(pts)), key=lambda q: abs(s_acc[q] - s))
        if abs(pts[i][1].x) > .1 and abs(pts[i][1].y) > .1:
            s += .3
            continue
        a, b = at(min(i + 1, len(pts) - 1), t_foot, .02), at(max(i - 1, 0), t_foot, .02)
        along = (a - b).normalized()
        up = Vector((0, 0, 1))
        m = Matrix((along, up, along.cross(up))).transposed().to_4x4()
        m.translation = at(i, t_foot, .02)
        P.merge(bm, P.transform(P.fleur(1.1, .035), m))
        if s + .55 < s_acc[-1] - .3:
            j = min(range(len(pts)), key=lambda q: abs(s_acc[q] - (s + .55)))
            if not (abs(pts[j][1].x) > .1 and abs(pts[j][1].y) > .1):
                a2, b2 = at(min(j + 1, len(pts) - 1), t_foot, .02), at(max(j - 1, 0), t_foot, .02)
                al = (a2 - b2).normalized()
                m2 = Matrix((al, up, al.cross(up))).transposed().to_4x4()
                m2.translation = at(j, t_foot, .02)
                P.merge(bm, P.transform(P.fleur(.65, .03), m2))
        s += 1.1
    # the runner across the top: red velvet, gold edges, a damask lozenge in its middle
    cx, cz, rw = (x0 + x1) / 2, (z0 + z1) / 2, min(4.4, (z1 - z0) * .75)
    P.merge(bm, P.box(U(x0 + .4, top + .01, cz - rw / 2), U(x1 - .4, top + .025, cz + rw / 2), P.VELVET))
    for sgn in (-1, 1):
        P.merge(bm, P.box(U(x0 + .4, top + .01, cz + sgn * rw / 2 - .1), U(x1 - .4, top + .03, cz + sgn * rw / 2 + .0), P.GOLD))
    # the runner's gold: a big fleur laid flat in its middle, scrolls either side, a fleur at each end
    flat = Matrix(((0, 1, 0, 0), (-1, 0, 0, 0), (0, 0, 1, 0), (0, 0, 0, 1)))
    P.merge(bm, P.transform(P.fleur(2.2, .012), Matrix.Translation(U(cx, top + .025, cz - 1.1)) @ Matrix.Rotation(math.pi / 2, 4, "Z")))
    for sgn in (-1, 1):
        sc = [Vector((cx + sgn * .5, cz, 0)), Vector((cx + sgn * 2.0, cz + .7, 0)), Vector((cx + sgn * 3.6, cz + .2, 0)),
              Vector((cx + sgn * 4.2, cz - .6, 0)), Vector((cx + sgn * 3.6, cz - .9, 0)), Vector((cx + sgn * 3.3, cz - .5, 0))]
        pts_ = [Vector((q.x, q.y, top + .03)) for q in P.curve(sc, 8)]
        P.merge(bm, P.tube(pts_, .05, 6))
        sc2 = [Vector((q.x, 2 * cz - q.y, q.z)) for q in pts_]
        P.merge(bm, P.tube(sc2, .05, 6))
        P.merge(bm, P.transform(P.fleur(1.2, .012), Matrix.Translation(U(cx + sgn * (x1 - x0) * .38, top + .025, cz - .6)) @
                                Matrix.Rotation(math.pi / 2, 4, "Z")))
    ob = P.obj(col, name, bm)
    return P.finish(ob)


def head_tables(col):
    objs = [draped(col, "BH3 head table W", -24.0, -6.0, TABLE_Z0, TABLE_Z1, HEAD_Y), draped(col, "BH3 head table E", 6.0, 24.0, TABLE_Z0, TABLE_Z1, HEAD_Y)]
    wb = bmesh.new()
    for x in (-21.0, -15.0, -9.0, 9.0, 15.0, 21.0):
        bm, w = P.candelabra(2.4)
        P.merge(wb, P.transform(bm, Matrix.Translation(U(x, HEAD_Y + .02, 62.68))))
    objs.append(P.finish(P.obj(col, "BH3 head candelabra", wb)))
    return objs


# ------------------------------------------------------------------ the balcony, its canopy (BH_A06 fourth cell)

def balcony(col):
    """The king's balcony: its marble front (z 60.58; where the ropes' riders land - nothing on its top edge) and sides
    in gold-framed panels over a plinth, a cornice flush with its floor; gold balustrades on the side rails; at the back
    a walnut canopy (a carved gold crest, a valance, scrolls at the corners) on two gold columns over the thrones."""
    bm = bmesh.new()
    P.merge(bm, P.box(U(-BAL_X, DAIS_Y, BAL_Z0), U(BAL_X, BAL_Y, HALL_Z1), P.MARBLE))
    P.merge(bm, P.box(U(-BAL_X - .06, DAIS_Y, BAL_Z0 - .06), U(BAL_X + .06, DAIS_Y + .5, HALL_Z1), P.MARBLE))
    P.merge(bm, P.box(U(-BAL_X - .07, DAIS_Y + .5, BAL_Z0 - .07), U(BAL_X + .07, DAIS_Y + .56, HALL_Z1), P.GOLD))
    P.merge(bm, P.box(U(-BAL_X - .08, BAL_Y - .4, BAL_Z0 - .12), U(BAL_X + .08, BAL_Y - .004, HALL_Z1), P.GOLD))
    P.merge(bm, P.box(U(-BAL_X - .05, BAL_Y - .62, BAL_Z0 - .06), U(BAL_X + .05, BAL_Y - .4, HALL_Z1), P.MARBLE))
    front = Vector((0, -1, 0))
    for i in range(3):
        cx = -BAL_X + 2 + 4 * i
        face_panel(bm, basis(None, front, U(cx, DAIS_Y + .8, BAL_Z0)), 3.2, 3.9, P.MARBLE, .35)
    for s in (-1, 1):
        for i in range(3):
            cz = BAL_Z0 + (HALL_Z1 - BAL_Z0) * (i + .5) / 3
            face_panel(bm, basis(None, Vector((s, 0, 0)), U(s * BAL_X, DAIS_Y + .8, cz)), 3.2, 3.9, P.MARBLE, .35)
        balustrade(bm, (s * 6.15, BAL_Z0 + .1), (s * 6.15, HALL_Z1), BAL_Y, depth=.3, newel_a=True, newel_b=False)
    # the canopy: two gold columns at its front corners, the walnut canopy over them (the builder's 23.8 - 24.6)
    for s in (-1, 1):
        x, zc = s * 4.85, 69.2
        col_prof = [(0.0, 0.0), (.32, 0.0), (.32, .12), (.26, .18), (.22, .3), (.17, .42), (.15, 6.9), (.19, 7.0), (.24, 7.1),
                    (.3, 7.25), (.3, 7.4), (0.0, 7.4)]
        P.merge(bm, P.transform(k.lathe(col_prof, 24, mat=P.GOLD), Matrix.Translation(U(x, BAL_Y, zc))))
        P.merge(bm, P.box(U(x - .36, BAL_Y + 7.15, zc - .36), U(x + .36, BAL_Y + 7.4, zc + .36), P.GOLD))
        for i in range(5):   # a balustrade's stub each side of the thrones (the sheet's)
            pass
    P.merge(bm, P.box(U(-5.3, 23.4, 68.8), U(5.3, 23.8, HALL_Z1), P.GOLD))
    P.merge(bm, P.box(U(-5.1, 23.8, 69.0), U(5.1, 24.6, HALL_Z1), P.WALNUT))
    P.merge(bm, P.box(U(-5.25, 24.6, 68.85), U(5.25, 24.75, HALL_Z1), P.GOLD))
    # the valance: a walnut skirt with a scalloped lower edge and a gold bead
    val = [(-5.1, 0.0)] + [(-5.1 + 10.2 * i / 24, -.35 - .18 * abs(math.sin(math.pi * i / 4))) for i in range(25)] + [(5.1, 0.0)]
    P.merge(bm, P.transform(k.prism(val, 0.0, .1, mat=P.WALNUT), Matrix.Translation(U(0, 23.4, 68.8)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
    # a domed roof rising to the crest
    roof = []
    for i in range(9):
        t = i / 8
        roof.append((-5.25 + 0.0, 24.75 + .9 * math.sin(math.pi / 2 * t)))
    rb = bmesh.new()
    nz = 12
    rows = []
    for j in range(nz + 1):
        zz = 68.85 + (HALL_Z1 - 68.85) * j / nz
        row = []
        for i in range(17):
            u = -5.25 + 10.5 * i / 16
            h = .9 * (1 - (u / 5.25) ** 2) ** .6 * (1 - ((zz - 68.85) / (HALL_Z1 - 68.85) - .5) ** 2 * 1.2)
            row.append(rb.verts.new(U(u, 24.75 + max(0.0, h), zz)))
        rows.append(row)
    for j in range(nz):
        for i in range(16):
            f = rb.faces.new((rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i]))
            f.material_index = P.WALNUT
            f.smooth = True
    bmesh.ops.recalc_face_normals(rb, faces=rb.faces)
    P.merge(bm, rb)
    # the crest: gold scrolls rising to a palmette over the front, a scroll at each front corner
    cz = 68.85
    crest = bmesh.new()
    for s in (-1, 1):
        sc = [Vector((s * .3, 0, 0)), Vector((s * 1.6, 0, .4)), Vector((s * 2.9, 0, .3)), Vector((s * 3.3, 0, .85)), Vector((s * 2.9, 0, 1.2)),
              Vector((s * 2.55, 0, .9)), Vector((s * 2.85, 0, .75))]
        P.merge(crest, P.tube(P.curve(sc, 6), .12, 8))
        cc = [Vector((s * 4.6, 0, 0)), Vector((s * 4.9, 0, .4)), Vector((s * 5.05, 0, .8)), Vector((s * 4.8, 0, .95)), Vector((s * 4.65, 0, .75))]
        P.merge(crest, P.tube(P.curve(cc, 6), .08, 8))
    for i in range(7):
        t = math.radians(-60 + 20 * i)
        b = Vector((math.sin(t) * .85, 0, .5 + math.cos(t) * 1.2))
        P.merge(crest, P.tube([Vector((0, 0, .3)), (b + Vector((0, 0, .3))) * .5, b], .09, 8, radii=[.07, .15, .07]))
    P.merge(crest, P.transform(P.fleur(1.5, .1), Matrix.Translation((0, -.06, 1.3)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
    P.merge(bm, P.transform(crest, Matrix.Translation(U(0, 24.75, cz))))
    ob = P.obj(col, "BH3 balcony", bm)
    return P.finish(ob, bevel=.008)


# ------------------------------------------------------------------ the throne (BH_A06 last cell)



def throne(col, name, x):
    """A throne facing the hall (BH_A06 last cell), built here, not on the guests' chair: square walnut feet with gold
    bands, a walnut base with a gold cartouche (scrolls round a fleur) on its front, panelled side pedestals rising to
    the arms, each arm ending in a big rolled gold scroll, an olive pad on each arm; tall back posts with gold-banded
    square caps and gold onion finials, between them an arched top rail with a gold crest (a fleur between C-scrolls)
    over an arched olive velvet back in a gold bead; a thick olive seat. Faces -z (Unity) on the balcony floor (16),
    its middle at z 70.1. Returns (hard, soft) objects."""
    H, hw, hd, seat = 4.6, 1.2, 1.0, 1.7
    hard, soft = bmesh.new(), bmesh.new()
    B = lambda lo, hi, mat: P.box(lo, hi, mat)
    # feet
    for sx in (-1, 1):
        for sy in (-1, 1):
            fx, fy = sx * (hd - .17), sy * (hw - .17)
            P.merge(hard, B((fx - .17, fy - .17, 0), (fx + .17, fy + .17, .28), P.WALNUT))
            P.merge(hard, B((fx - .18, fy - .18, .02), (fx + .18, fy + .18, .08), P.GOLD))
            P.merge(hard, B((fx - .19, fy - .19, .28), (fx + .19, fy + .19, .34), P.GOLD))
    # the base under the seat; its gold fillets; the cartouche on the front (+x)
    P.merge(hard, B((-hd, -hw, .34), (hd, hw, seat - .25), P.WALNUT))
    P.merge(hard, B((-hd - .02, -hw - .02, seat - .3), (hd + .02, hw + .02, seat - .24), P.GOLD))
    P.merge(hard, B((-hd - .02, -hw - .02, .34), (hd + .02, hw + .02, .4), P.GOLD))
    fm = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # fleur x -> y, fleur y -> z, raised -> x
    P.merge(hard, P.transform(P.fleur(.62, .05), Matrix.Translation((hd, 0, .55)) @ fm))
    for s in (-1, 1):
        sc = [Vector((hd + .02, s * .12, .62)), Vector((hd + .03, s * .45, .78)), Vector((hd + .03, s * .75, .7)), Vector((hd + .03, s * .82, .55)),
              Vector((hd + .03, s * .7, .5)), Vector((hd + .03, s * .62, .6))]
        P.merge(hard, P.tube(P.curve(sc, 6), .045, 8))
    # the side pedestals up to the arms, panelled; the arm rests; the scrolls
    arm_y = 2.55
    for s in (-1, 1):
        y0, y1 = (s * hw - .14, s * hw + .14) if s > 0 else (s * hw - .14, s * hw + .14)
        P.merge(hard, B((-hd + .1, y0, seat - .25), (hd - .25, y1, arm_y - .12), P.WALNUT))
        ring = [Vector((-hd + .35, s * (hw + .145), seat)), Vector((hd - .5, s * (hw + .145), seat)), Vector((hd - .5, s * (hw + .145), arm_y - .35)),
                Vector((-hd + .35, s * (hw + .145), arm_y - .35))]
        P.merge(hard, P.tube(ring + [ring[0]], .02, 6))
        P.merge(hard, B((-hd + .05, s * hw - .19, arm_y - .12), (hd - .15, s * hw + .19, arm_y), P.WALNUT))
        P.merge(soft, B((-hd + .15, s * hw - .15, arm_y), (hd - .25, s * hw + .15, arm_y + .12), P.OLIVE))
        # the rolled gold scroll at the arm's front: a spiral seen from the side, extruded across the arm
        spiral = []
        for i in range(29):
            t = math.pi * 2.2 * i / 28
            rr = .36 - .25 * i / 28
            spiral.append(Vector((hd - .2 + rr * math.sin(t), 0, arm_y - .36 + rr * math.cos(t))))
        for dy in (-.13, 0.0, .13):
            P.merge(hard, P.tube([p + Vector((0, s * hw + dy, 0)) for p in spiral], .07, 8))
        P.merge(hard, B((hd - .5, s * hw - .2, seat - .25), (hd - .15, s * hw + .2, arm_y - .6), P.GOLD))
    # the back posts, caps and finials
    for s in (-1, 1):
        y = s * (hw - .16)
        P.merge(hard, B((-hd, y - .17, .34), (-hd + .34, y + .17, 3.95), P.WALNUT))
        ring = [Vector((-hd + .345, y - .1, seat + .2)), Vector((-hd + .345, y + .1, seat + .2)), Vector((-hd + .345, y + .1, 3.7)),
                Vector((-hd + .345, y - .1, 3.7))]
        P.merge(hard, P.tube(ring + [ring[0]], .018, 6))
        for (h0, h1, g, mat) in ((3.95, 4.02, .2, P.GOLD), (4.02, 4.18, .19, P.WALNUT), (4.18, 4.24, .21, P.GOLD)):
            P.merge(hard, B((-hd + .17 - g, y - g, h0), (-hd + .17 + g, y + g, h1), mat))
        onion = [(0.0, 0.0), (.1, 0.0), (.07, .04), (.06, .08), (.14, .16), (.17, .24), (.15, .32), (.09, .4), (.04, .47), (.03, .52),
                 (.05, .55), (0.0, .6)]
        P.merge(hard, P.transform(k.lathe(onion, 20, mat=P.GOLD), Matrix.Translation((-hd + .17, y, 4.24))))
    # the back: a slab between the posts, the arched top rail, the velvet panel, the gold bead, the crest
    py = hw - .32
    P.merge(hard, B((-hd + .02, -py, seat - .25), (-hd + .26, py, 3.7), P.WALNUT))
    arch = [(py * math.cos(math.pi * i / 16), 3.7 + .45 * math.sin(math.pi * i / 16)) for i in range(17)]
    ab = k.prism([(-py, 3.4), (py, 3.4)] + [(yy, zz) for yy, zz in arch] , 0.0, .3, mat=P.WALNUT)
    for v in ab.verts:
        v.co = Vector((v.co.z - hd + .0, v.co.x, v.co.y))
    P.merge(hard, ab)
    panel = [(py - .12, seat + .1)] + [((py - .12) * math.cos(math.pi * i / 14), 3.55 + .35 * math.sin(math.pi * i / 14)) for i in range(15)] + \
            [(-(py - .12), seat + .1)]
    pb = k.prism(panel, 0.0, .1, mat=P.OLIVE)
    for v in pb.verts:
        v.co = Vector((v.co.z - hd + .26, v.co.x, v.co.y))
        if v.co.x > -hd + .3:
            yy = v.co.y / (py - .12)
            v.co.x += .06 * max(0.0, 1 - yy * yy) ** .5
    P.merge(soft, pb)
    P.merge(hard, P.tube([Vector((-hd + .33, yy, zz)) for yy, zz in panel] + [Vector((-hd + .33, panel[0][0], panel[0][1]))], .035, 8))
    crest = P.transform(P.fleur(.9, .08), Matrix.Translation((-hd + .1, 0, 4.1)) @ fm)
    P.merge(hard, crest)
    for s in (-1, 1):
        sc = [Vector((-hd + .14, s * .15, 4.15)), Vector((-hd + .14, s * .45, 4.25)), Vector((-hd + .14, s * .62, 4.5)),
              Vector((-hd + .14, s * .5, 4.65)), Vector((-hd + .14, s * .38, 4.52))]
        P.merge(hard, P.tube(P.curve(sc, 6), .05, 8))
    # the seat
    P.merge(soft, B((-hd + .25, -hw + .16, seat - .25), (hd + .04, hw - .16, seat + .12), P.OLIVE))
    P.merge(hard, P.tube([Vector((hd + .05, -hw + .16, seat - .22)), Vector((hd + .05, hw - .16, seat - .22))], .03, 6))
    m = Matrix.Translation(U(x, BAL_Y, 70.1)) @ Matrix.Rotation(-math.pi / 2, 4, "Z")
    ob = P.finish(P.obj(col, name, hard, m), bevel=.01)
    pad = P.obj(col, name + " pads", soft, m)
    k.bevel(pad, .06, 4, 30.0, harden=False)
    k.apply_modifiers(pad)
    return [ob, P.finish(pad)]


# ------------------------------------------------------------------ main

def build():
    scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o)
    got = {"dais": [dais(collection("BH3 dais"))], "rails": [rails(collection("BH3 rails"))],
           "tables": head_tables(collection("BH3 head tables")), "balcony": [balcony(collection("BH3 balcony"))]}
    tc = collection("BH3 thrones")
    got["thrones"] = throne(tc, "BH3 throne W", -4.2) + throne(tc, "BH3 throne E", 4.2)
    k.preview_materials()
    print("[bh_b3] tris", {n: k.tris(o) for n, o in got.items()})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b3_dais.blend"))
    return got


def proxy(centre, size):
    p = bpy.data.objects.new("_proxy", bpy.data.meshes.new("_proxy"))
    b = bmesh.new()
    bmesh.ops.create_cube(b, size=1.0)
    b.transform(Matrix.Diagonal(tuple(size) + (1,)))
    b.to_mesh(p.data)
    p.location = centre
    bpy.context.scene.collection.objects.link(p)
    bpy.context.view_layer.update()
    return p


def stills(got):
    scene = bpy.context.scene
    out = os.path.join(HERE, "stills", "b3", "parts")
    os.makedirs(out, exist_ok=True)
    cam = st.setup_render(scene)
    jobs = (("face", got["dais"], U(-12, 5.2, 56), (8.4, 1, 10.6), -25, 8, 50),
            ("balustrade", got["rails"], U(-4.3, 11.3, 56.15), (6.0, 1, 2.0), -30, 14, 50),
            ("table", got["tables"][:1] + got["tables"][2:], U(-15, 12.6, 64.5), (21, 6.5, 8), -12, 16, 40),
            ("balcony", got["balcony"] + got["thrones"], U(0, 17.5, 66), (13, 11.5, 15), -30, 10, 35),
            ("throne", got["thrones"][:2], U(-4.2, 18.3, 70.1), (2.8, 2.8, 4.8), -35, 8, 60))
    for name, objs, c, size, yaw, pitch, lens in jobs:
        p = proxy(c, size)
        st.frame(cam, [p], math.radians(yaw), math.radians(pitch), lens, .95)
        bpy.data.objects.remove(p)
        path = os.path.join(out, name + "_render.png")
        st.render_only(scene, objs, path)
        st.compare(*CELLS[name], path, os.path.join(out, name + "_compare.png"))
    allm = [o for o in scene.objects if o.type == "MESH"]
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    p = proxy(U(0, 12, 62), (66, 16, 18))
    st.frame(cam, [p], math.radians(-20), math.radians(18), 30, .8)
    bpy.data.objects.remove(p)
    st.render_only(scene, allm, os.path.join(HERE, "stills", "b3", "dais.png"))


def export(got):
    """B3 to Unity (GrandBanquetHallDressing.Dais.cs), all in the section's frame. The head tables' candelabra go with
    their wicks for B8's flames."""
    k.export("BH3 dais", "bh_dais", "B3")
    k.export("BH3 rails", "bh_dais_rails", "B3")
    k.export("BH3 head tables", "bh_head_tables", "B3")
    k.export("BH3 balcony", "bh_balcony", "B3")
    k.export("BH3 thrones", "bh_thrones", "B3")


if __name__ == "__main__":
    got = build()
    if "stills" in ARGS:
        stills(got)
    if "export" in ARGS:
        export(got)

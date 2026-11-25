"""B2 the cloche lifts, the dumbwaiter's frame, the seated guests and the magician (seoungtest R837, 2026-11-25; PLAN.md
B2 - the long table itself is bh_b0_table_model.py's), the lift parts after the R836 sheet BH_A05, the people the user's
figurines (bh_figures.py).

    blender -b --factory-startup --python bh_b2_cloche_model.py -- [stills] [export]
    -> bh_b2_cloche.blend; "stills": stills/b2/parts/<part>_compare.png + stills/b2/lifts.png + stills/b2/guests.png;
       "export": Assets/Resources/BanquetHall/Models/B2/*.fbx (never while Unity is in Play).

Numbers are GrandBanquetHallBuilder's (Cloches, DumbwaiterFrame, LongTable); colliders, floors and the movers stay its:
  platter   root "BH cloche platter W/E" at (17 / 23, 4.52, 53.7), its top the root (a floor 4.4 across, 0.12 thick);
            brass lugs at x +-2.38, the cables hooked at (+-2.4, 0.07). The model: built round the root, its top flush.
  cloche    root "BH cloche W/E" 2 m over its platter while open; a band of slate pieces r 2.2, 1.6 high (colliders), a
            lid at 1.6 - 1.8 (collider), the dome over it a picture. The model: built round the root; its handle's ball
            tops out at 3.64 so that at the top of the lift (root 12.4) it stays under the pulleys (16.19).
  frame     posts 0.3 square at x 14.15 / 20 / 25.85, z 53.7, from the table top (the third from the pantry's wall top,
            7.8) to the beam at 16.6 - 16.95 (x 14 - 26); a pulley 0.42 across under the beam over every cable and chain.
  people    guests on the chairs at x 12.8, z 21 + 4 i (nine), facing the table; the magician at (20, 0, 15.2) facing it.
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
import bh_figures as F
for _m in (k, P, st, F):
    importlib.reload(_m)

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
A05 = os.path.join(st.SHEETS, "BH_A05_cloche_lift.png")
CELLS = {"cloche": (A05, (20, 255, 470, 775)), "platter": (A05, (488, 595, 912, 765)), "post": (A05, (912, 190, 1098, 778)),
         "pulley": (A05, (1105, 380, 1292, 772)), "chain": (A05, (1325, 365, 1520, 772))}

TABLE_Y, CLOCHE_Z, CLOCHE_R, BAND = 4.4, 53.7, 2.2, 1.6
PLATTER_TOP = 4.52
CLOCHE_X = (17.0, 23.0)
GANTRY_X = (14.15, 20.0, 25.85)
GANTRY_Y, CABLE_X = 16.6, 2.4
DOME_TOP, BALL_R = 3.0, .28


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


def flute(bm, count=24, depth=.012, zmin=-1e9, rmin=.5):
    """Vertical flutes round z: every vertex over zmin and out from the axis more than rmin pulled in by up to depth
    (as a fraction of its radius) in `count` soft grooves (the sheet's cloche)."""
    for v in bm.verts:
        r = math.hypot(v.co.x, v.co.y)
        if v.co.z < zmin or r < rmin:
            continue
        a = math.atan2(v.co.y, v.co.x)
        k_ = 1 - depth * (1 + math.cos(count * a)) / 2
        v.co.x *= k_
        v.co.y *= k_


# ------------------------------------------------------------------ the cloche (BH_A05 first cell)

def cloche(col, name):
    """In the cloche root's frame: a fluted silver bell - its wall the band's ring (r 2.2, 0 - 1.6), a broad gold band
    round its foot, the dome rising from the lid to 3.0, a gold collar, the gold ball handle with a ring each side
    (the sheet's) and a loop on top for the chain."""
    bm = bmesh.new()
    R = CLOCHE_R
    # the bell: a super-ellipse from just over the gold foot band (r 2.2, z 0.46) to the top (z 3.0) - the sheet's tall
    # bell; at the band's top (1.6) it is within 7 cm of the band's colliders (r 2.2)
    def bell(t, r0=R, z0=.46, z1=DOME_TOP, n=2.7):
        return (r0 * max(0.0, 1 - t ** n) ** (1 / n), z0 + (z1 - z0) * t)
    foot = [(R - .1, 0.0), (R + .045, 0.0), (R + .05, .05), (R + .05, .36), (R + .02, .4), (R + .03, .44), (R, .46)]
    dome = [bell(i / 24) for i in range(1, 25)]
    inner = [bell(1 - i / 24, R - .1, .0, DOME_TOP - .1) for i in range(25)]
    prof = foot + dome + inner
    mats = [P.GOLD] * 6 + [P.SILVER] * (len(dome) + len(inner))
    shell = k.lathe(prof, 96, mat=mats)
    flute(shell, 24, .014, zmin=.47, rmin=1.0)
    P.merge(bm, shell)
    # the collar, the ball, a ring each side of it and the loop on top
    P.merge(bm, k.lathe([(0.0, DOME_TOP - .05), (.42, DOME_TOP - .03), (.44, DOME_TOP + .02), (.32, DOME_TOP + .06),
                         (.24, DOME_TOP + .1), (.2, DOME_TOP + .12), (0.0, DOME_TOP + .12)], 32, mat=P.GOLD))
    zc = DOME_TOP + .1 + BALL_R
    P.merge(bm, k.lathe([(BALL_R * math.sin(math.pi * i / 16), zc - BALL_R * math.cos(math.pi * i / 16)) for i in range(17)], 32, mat=P.GOLD))
    for s in (-1, 1):
        ring = P.torus(.2, .035, 24, 8, P.GOLD)
        P.merge(bm, P.transform(ring, Matrix.Translation((s * (BALL_R + .12), 0, zc - .05)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
    loop = P.torus(.09, .028, 16, 8, P.GOLD)
    P.merge(bm, P.transform(loop, Matrix.Translation((0, 0, zc + BALL_R + .06)) @ Matrix.Rotation(math.pi / 2, 4, "Y")))
    ob = P.obj(col, name, bm)
    return P.finish(ob)


# ------------------------------------------------------------------ the platter (BH_A05 second cell)

def platter(col, name):
    """In the platter root's frame (its top at 0, a floor): brushed silver, its walking top flat to the gold rim and the
    rim flush with it (nothing to trip on), a dished underside, a brass lug each side (x +-2.38) with a gold ring
    standing up for the cable's shackle."""
    R = CLOCHE_R
    prof = [(0.0, 0.0), (R - .16, 0.0), (R - .14, -.004), (R - .02, .0), (R, -.02), (R + .01, -.07), (R - .01, -.12),
            (R - .1, -.16), (R * .7, -.24), (R * .3, -.28), (0.0, -.29)]
    mats = [P.SILVER, P.GOLD, P.GOLD, P.GOLD, P.GOLD, P.GOLD, P.GOLD, P.SILVER, P.SILVER, P.SILVER]
    bm = k.lathe(prof, 96, mat=mats)
    P.merge(bm, P.transform(P.torus(R - .55, .012, 96, 6, P.SILVER), Matrix.Translation((0, 0, -.004))))   # a turned line on the top
    for s in (-1, 1):
        # the lug (the builder's 0.36 x 0.3 brass block at x +-2.38): a gold boss, a ring hanging from it (the sheet's)
        P.merge(bm, P.box((s * (R - .02) - (.0 if s > 0 else .24), -.16, -.13), (s * (R - .02) + (.24 if s > 0 else 0), .16, .0), P.GOLD))
        boss = k.lathe([(0.0, 0.0), (.13, 0.0), (.15, .05), (.13, .1), (0.0, .1)], 20, mat=P.GOLD)
        P.merge(bm, P.transform(boss, Matrix.Translation((s * (R + .22), 0, -.065)) @ Matrix.Rotation(s * math.pi / 2, 4, "Y")))
        hang = P.torus(.17, .045, 24, 8, P.GOLD)
        P.merge(bm, P.transform(hang, Matrix.Translation((s * (R + .3), 0, -.2)) @ Matrix.Rotation(math.pi / 2, 4, "Y")))
        ring = P.torus(.13, .03, 24, 8, P.GOLD)
        P.merge(bm, P.transform(ring, Matrix.Translation((s * CABLE_X, 0, .07 + .1)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
    ob = P.obj(col, name, bm)
    return P.finish(ob)


# ------------------------------------------------------------------ the frame: posts, beam, pulleys (BH_A05 3rd, 4th cells)

def post(bm, x, foot, top):
    """A frame post (BH_A05): a stepped gold base, a panelled walnut shaft 0.44 square, a stepped gold capital under the
    beam."""
    z = CLOCHE_Z
    w = .44
    for (y0, y1, half, mat) in ((foot, foot + .14, .34, P.GOLD), (foot + .14, foot + .2, .3, P.GOLD), (foot + .2, foot + .36, .27, P.GOLD),
                                (top - .3, top - .2, .26, P.GOLD), (top - .2, top - .1, .29, P.GOLD), (top - .1, top, .32, P.GOLD)):
        P.merge(bm, P.box(U(x - half, y0, z - half), U(x + half, y1, z + half), mat))
    P.merge(bm, P.box(U(x - w / 2, foot + .36, z - w / 2), U(x + w / 2, top - .3, z + w / 2), P.WALNUT))
    # a sunk panel on each face: a walnut bead frame
    for n in (Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0))):
        c = U(x, 0, z) + n * (w / 2 + .005)
        side = Vector((0, 0, 1)).cross(n)
        ya, yb = foot + .7, top - .65
        ring = [c + side * .13 + Vector((0, 0, ya)), c + side * .13 + Vector((0, 0, yb)), c - side * .13 + Vector((0, 0, yb)),
                c - side * .13 + Vector((0, 0, ya)), c + side * .13 + Vector((0, 0, ya))]
        P.merge(bm, P.tube(ring, .018, 6, mat=P.WALNUT))
        P.merge(bm, P.tube([p + n * .01 for p in ring], .007, 6, mat=P.GOLD))


def pulley(bm, at):
    """A pulley under the beam after BH_A05's fourth cell (user 11-25: "시트랑 비슷하게 다듬고 특히 접합부 부분 디테일
    신경 써"), every joint of it made: a square plate bolted to the beam's underside with four domed rivets; under it a
    hinge - a knuckle (a barrel with end caps) through two leaves, one from the plate, one from the yoke; the yoke, a flat
    strap with rounded corners over the top of the sheave and down both its faces, each leg ending in a round eye on the
    axle with a washer; the axle through, a turned cap on each end; the sheave - two flanges with a deep groove between
    and a step inside each, a hub boss each side, eight tapered spokes. The sheave's middle 5 cm under the builder's
    pulley point (16.4), so that the line's end (16.4) is inside it and the whole fits under the beam (16.6).
    at: the builder's pulley point (Unity)."""
    x, y, z = at
    R, T = .15, .12                      # the sheave's radius and thickness
    c = U(x, y - .05, z)                 # its middle
    axle = Matrix.Translation(c) @ Matrix.Rotation(math.pi / 2, 4, "X")   # local z: along Unity z (Blender -y)
    def on_axle(bmx, zz=0.0):
        return P.transform(bmx, axle @ Matrix.Translation((0, 0, zz)))
    # -- the sheave: flanges, the deep groove, the inner steps, the web cut into spokes
    # two grooves with a ridge between (the sheet's double sheave)
    sheave = [(R * .74, -T / 2), (R * .97, -T / 2), (R, -T / 2 + .008), (R, -T / 2 + .018), (R * .9, -T / 2 + .024),
              (R * .82, -T / 4), (R * .9, -.008), (R * .93, 0.0), (R * .9, .008), (R * .82, T / 4), (R * .9, T / 2 - .024),
              (R, T / 2 - .018), (R, T / 2 - .008), (R * .97, T / 2), (R * .74, T / 2), (R * .72, T / 2 - .012), (R * .66, T / 2 - .016), (R * .66, -T / 2 + .016),
              (R * .72, -T / 2 + .012), (R * .74, -T / 2)]
    P.merge(bm, on_axle(k.lathe(sheave, 40, mat=P.GOLD)))
    for i in range(8):
        t = math.pi * 2 * (i + .5) / 8
        a, b = Vector((math.cos(t), math.sin(t), 0)) * .048, Vector((math.cos(t), math.sin(t), 0)) * (R * .67)
        P.merge(bm, on_axle(P.tube([a, b], .012, 8, radii=[.016, .009])))
    hub = [(0.0, -.05), (.05, -.05), (.062, -.04), (.062, .04), (.05, .05), (0.0, .05)]
    P.merge(bm, on_axle(k.lathe(hub, 20, mat=P.GOLD)))
    # -- the axle, the eyes of the yoke, a washer and a turned cap each end
    P.merge(bm, on_axle(k.lathe([(0.0, -.13), (.016, -.13), (.016, .13), (0.0, .13)], 12, mat=P.GOLD)))
    gap = T / 2 + .028                    # the yoke's legs this far from the sheave's middle
    for s in (-1, 1):
        washer = k.lathe([(.016, 0.0), (.032, 0.0), (.032, .006), (.016, .006)], 16, mat=P.GOLD)
        P.merge(bm, on_axle(washer, s * (gap - .007) - (.006 if s < 0 else 0)))
        cap = k.lathe([(0.0, 0.0), (.03, 0.0), (.03, .03), (.026, .036), (.016, .04), (0.0, .041)], 16, mat=P.GOLD)
        capm = Matrix.Rotation(math.pi, 4, "X") if s < 0 else Matrix.Identity(4)
        P.merge(bm, P.transform(cap, axle @ Matrix.Translation((0, 0, s * (gap + .012))) @ capm))
    # -- the yoke: a strap 0.045 wide, 0.012 thick, over the top and down both faces to an eye round the axle
    top_y = R + .03                       # the strap's underside over the sheave's top (local, from its middle)
    rr = .018                             # its corners' radius (the sheet's nearly square bends)
    def leg_path(s):
        pts = [Vector((0, 0, s * gap))]
        pts.append(Vector((0, top_y - rr, s * gap)))
        for i in range(1, 6):
            a = math.pi / 2 * i / 6
            pts.append(Vector((0, top_y - rr + rr * math.sin(a), s * (gap - rr + rr * math.cos(a)))))
        pts.append(Vector((0, top_y, s * (gap - rr))))
        return pts
    path = leg_path(-1) + list(reversed(leg_path(1)))   # up one leg, over the top, down the other
    # the path runs in the local y-z plane (y up the leg, z across): to Blender through the axle frame's turn
    strap_prof = [(-.03, 0.0), (.03, 0.0), (.03, .014), (-.03, .014)]
    yoke = bmesh.new()
    rows = []
    for i, p in enumerate(path):
        a_ = path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]
        a_.normalize()
        across = Vector((1, 0, 0))
        out = a_.cross(across)            # off the sheave
        if out.dot(Vector((0, p.y, p.z))) < 0:
            out = -out
        rows.append([yoke.verts.new(p + across * u + out * v) for u, v in strap_prof])
    for r0, r1 in zip(rows, rows[1:]):
        for j in range(4):
            yoke.faces.new((r0[j], r0[(j + 1) % 4], r1[(j + 1) % 4], r1[j])).material_index = P.GOLD
    for r_ in (rows[0], rows[-1]):
        yoke.faces.new(r_).material_index = P.GOLD
    bmesh.ops.recalc_face_normals(yoke, faces=yoke.faces)
    # the eyes: a round boss where each leg meets the axle
    for s in (-1, 1):
        eye = k.lathe([(.016, 0.0), (.04, 0.0), (.042, .006), (.042, .02), (.04, .026), (.016, .026)], 20, mat=P.GOLD)
        P.merge(yoke, P.transform(eye, Matrix.Translation((0, 0, s * gap + (0.0 if s > 0 else -.024)))))
    # local frame for the yoke: x along Unity x, y up, z along the axle (Unity z)
    yf = Matrix.Translation(c) @ Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))
    P.transform(yoke, yf)
    bmesh.ops.reverse_faces(yoke, faces=yoke.faces)   # (that frame swaps y and z: a mirror)
    P.merge(bm, yoke)
    # -- the hinge: a leaf down from the plate and one up from the yoke's top, the knuckle through both (along x)
    ky = y - .05 + top_y + .014 + .024    # the knuckle's axis height (Unity), along the axle as the sheet's
    leaf_lo = P.box(U(x - .024, y - .05 + top_y + .014, z - .05), U(x + .024, ky, z + .05), P.GOLD)
    leaf_hi = P.box(U(x - .024, ky, z - .07), U(x + .024, GANTRY_Y - .012, z + .07), P.GOLD)
    P.merge(bm, leaf_lo)
    P.merge(bm, leaf_hi)
    knuckle = k.lathe([(0.0, -.07), (.018, -.07), (.022, -.064), (.022, .064), (.018, .07), (0.0, .07)], 16, mat=P.GOLD)
    P.merge(bm, P.transform(knuckle, Matrix.Translation(U(x, ky, z)) @ Matrix.Rotation(math.pi / 2, 4, "X")))
    for s in (-1, 1):   # the pin's heads
        head = k.lathe([(0.0, 0.0), (.016, 0.0), (.016, .006), (.01, .012), (0.0, .013)], 12, mat=P.GOLD)
        P.merge(bm, P.transform(head, Matrix.Translation(U(x, ky, z + s * .07)) @ Matrix.Rotation(-s * math.pi / 2, 4, "X")))
    # -- the plate on the beam's underside, four domed rivets
    P.merge(bm, P.box(U(x - .09, GANTRY_Y - .012, z - .09), U(x + .09, GANTRY_Y, z + .09), P.GOLD))
    for (bx, bz) in ((-.06, -.06), (.06, -.06), (-.06, .06), (.06, .06)):
        rivet = k.lathe([(0.0, 0.0), (.016, 0.0)] + [(.016 * math.cos(math.pi / 2 * i / 4), .012 * math.sin(math.pi / 2 * i / 4)) for i in range(1, 5)],
                        12, mat=P.GOLD)
        P.merge(bm, P.transform(rivet, Matrix.Translation(U(x + bx, GANTRY_Y - .012, z + bz)) @ Matrix.Rotation(math.pi, 4, "X")))


def frame(col):
    bm = bmesh.new()
    for x in GANTRY_X:
        foot = 7.8 if x > 25.0 else TABLE_Y
        post(bm, x, foot, GANTRY_Y)
        # over the beam: a cap block and a gold ball (the sheet's finial)
        P.merge(bm, P.box(U(x - .3, GANTRY_Y + .35, CLOCHE_Z - .3), U(x + .3, GANTRY_Y + .45, CLOCHE_Z + .3), P.GOLD))
        P.merge(bm, P.box(U(x - .24, GANTRY_Y + .45, CLOCHE_Z - .24), U(x + .24, GANTRY_Y + .5, CLOCHE_Z + .24), P.GOLD))
        ball = k.lathe([(0.0, 0.0), (.1, 0.0), (.06, .05), (.06, .09)] + [(.2 * math.sin(math.pi * i / 12), .09 + .2 * (1 - math.cos(math.pi * i / 12)))
                                                                         for i in range(1, 13)], 20, mat=P.GOLD)
        P.merge(bm, P.transform(ball, Matrix.Translation(U(x, GANTRY_Y + .5, CLOCHE_Z))))
    # the beam: walnut, a gold fillet along each edge, panels between the posts
    P.merge(bm, P.box(U(13.85, GANTRY_Y, CLOCHE_Z - .27), U(26.15, GANTRY_Y + .35, CLOCHE_Z + .27), P.WALNUT))
    for s in (-1, 1):
        for y in (GANTRY_Y + .02, GANTRY_Y + .33):
            P.merge(bm, P.tube([U(13.85, y, CLOCHE_Z + s * .275), U(26.15, y, CLOCHE_Z + s * .275)], .02, 6))
    for cx in CLOCHE_X:
        for s in (-1, 0, 1):
            pulley(bm, (cx + s * CABLE_X, GANTRY_Y - .2, CLOCHE_Z))
    ob = P.obj(col, "BH2 frame", bm)
    return P.finish(ob, bevel=.01)


# ------------------------------------------------------------------ the chain link and the cable's end (last cells)

def chain_link(col):
    """One gold link (0.3 long, Unity code lays them from the pulley to the cloche's loop each frame, each turned a
    quarter to the last): built along z, its middle at the origin."""
    bm = bmesh.new()
    L, W, r = .3, .17, .032
    pts = []
    for i in range(24):
        t = math.pi * 2 * i / 24
        x = (W / 2 - r) * math.cos(t)
        zz = (L / 2 - r) * math.sin(t)
        zz += math.copysign(max(0.0, (L - W) / 2 * 0), zz)
        pts.append(Vector((x * 1.0, 0, zz)))
    # a stadium: straight sides, round ends
    pts = []
    for i in range(13):
        t = math.pi * i / 12
        pts.append(Vector(((W / 2 - r) * math.cos(t), 0, (L - W) / 2 + (W / 2 - r) * math.sin(t))))
    for i in range(13):
        t = math.pi + math.pi * i / 12
        pts.append(Vector(((W / 2 - r) * math.cos(t), 0, -(L - W) / 2 + (W / 2 - r) * math.sin(t))))
    pts.append(pts[0])
    P.merge(bm, P.tube(pts, r, 8, mat=P.GOLD, caps=False))
    ob = P.obj(col, "BH2 chain link", bm)
    return P.finish(ob)


# ------------------------------------------------------------------ the people: the user's figurines

GUEST_X, GUEST_Z0, GUESTS, SEAT_Y, GUEST_TOP = 12.9, 21.0, 9, 2.2, 5.55
MAGE_AT, MAGE_TOP = (20.0, 0.0, 15.2), 9.3


def people(col):
    objs = []
    for i in range(GUESTS):
        g = F.figure("guest", col, "BH2 guest %d" % (i + 1))
        h = F.size(g)[2]
        # the model faces +y (its back to the sheet's viewer); turned to face the table (+x)
        g.matrix_world = Matrix.Translation(U(GUEST_X, SEAT_Y, GUEST_Z0 + 4 * i)) @ Matrix.Rotation(-math.pi / 2, 4, "Z") @ Matrix.Scale((GUEST_TOP - SEAT_Y) / h, 4)
        objs.append(g)
    m = F.figure("magician", col, "BH2 magician")
    h = F.size(m)[2]
    m.matrix_world = Matrix.Translation(U(*MAGE_AT)) @ Matrix.Rotation(math.pi, 4, "Z") @ Matrix.Scale(MAGE_TOP / h, 4)
    objs.append(m)
    return objs


# ------------------------------------------------------------------ main

def build():
    scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o)
    got = {}
    got["cloche"] = [cloche(collection("BH2 cloche"), "BH2 cloche")]
    got["platter"] = [platter(collection("BH2 platter"), "BH2 platter")]
    got["frame"] = [frame(collection("BH2 frame"))]
    got["chain"] = [chain_link(collection("BH2 chain link"))]
    got["people"] = people(collection("BH2 people"))
    k.preview_materials()
    print("[bh_b2] tris", {n: k.tris(o) for n, o in got.items()})
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "bh_b2_cloche.blend"))
    return got


def place_lifts(got, phase=0):
    """Copies of the cloche and platter at their places on the lifts (for the stills): open at the table, or for the
    east one risen to the dais (phase 1)."""
    scene = bpy.context.scene
    out = []
    for i, x in enumerate(CLOCHE_X):
        up = (i == 1) * 5.88
        for o, y in ((got["platter"][0], PLATTER_TOP + up), (got["cloche"][0], PLATTER_TOP + 2.0 + up)):
            c = o.copy()
            c.matrix_world = Matrix.Translation(U(x, y, CLOCHE_Z))
            scene.collection.objects.link(c)
            out.append(c)
    return out


def stills(got):
    scene = bpy.context.scene
    out = os.path.join(HERE, "stills", "b2", "parts")
    os.makedirs(out, exist_ok=True)
    cam = st.setup_render(scene)
    fr = got["frame"][0]
    # the frame's parts alone: one post (its middle third) and one pulley, cut out by camera framing on proxies
    jobs = (("cloche", got["cloche"], math.radians(-15), math.radians(10), 60),
            ("platter", got["platter"], math.radians(-15), math.radians(24), 60),
            ("chain", got["chain"], math.radians(-20), math.radians(8), 80))
    for name, objs, yaw, pitch, lens in jobs:
        st.frame(cam, objs, yaw, pitch, lens)
        path = os.path.join(out, name + "_render.png")
        st.render_only(scene, objs, path)
        st.compare(*CELLS[name], path, os.path.join(out, name + "_compare.png"))
    # post and pulley: the frame rendered, the camera on that part
    for name, centre, rad, yaw, pitch in (("post", U(20.0, 10.6, CLOCHE_Z), 6.5, -20, 6), ("pulley", U(17.0, 16.42, CLOCHE_Z), .26, -28, 12)):
        e = bpy.data.objects.new("_aim", None)
        scene.collection.objects.link(e)
        e.empty_display_size = rad
        e.location = centre
        bpy.context.view_layer.update()
        proxy = bpy.data.objects.new("_box", bpy.data.meshes.new("_box"))
        bmp = bmesh.new()
        bmesh.ops.create_cube(bmp, size=1.0)
        bmp.transform(Matrix.Diagonal(((.9, .9, rad * 2) if name == "post" else (rad * 1.4,) * 3) + (1,)))
        bmp.to_mesh(proxy.data)
        proxy.location = centre
        scene.collection.objects.link(proxy)
        bpy.context.view_layer.update()
        st.frame(cam, [proxy], math.radians(yaw), math.radians(pitch), 60, .9)
        path = os.path.join(out, name + "_render.png")
        st.render_only(scene, [fr], path)
        st.compare(*CELLS[name], path, os.path.join(out, name + "_compare.png"))
        bpy.data.objects.remove(proxy)
        bpy.data.objects.remove(e)
    # the lifts together: the frame, the west lift open at the table, the east one up at the dais
    lifts = place_lifts(got) + [fr]
    scene.render.resolution_x, scene.render.resolution_y = 1400, 1100
    st.frame(cam, lifts, math.radians(-20), math.radians(12), 35, .95)
    st.render_only(scene, lifts, os.path.join(HERE, "stills", "b2", "lifts.png"))
    # the guests and the magician at the table's south end, with the table and chairs (bh_b2_table_export.blend)
    ppl = list(got["people"])
    src = os.path.join(HERE, "bh_b2_table_export.blend")
    if os.path.exists(src):
        with bpy.data.libraries.load(src) as (data_from, data_to):
            data_to.objects = [n for n in data_from.objects]
        for o in data_to.objects:
            if o is not None and o.type == "MESH":
                scene.collection.objects.link(o)
                ppl.append(o)
    st.frame(cam, got["people"][:4] + got["people"][-1:], math.radians(-140), math.radians(20), 35, .9)
    st.render_only(scene, ppl, os.path.join(HERE, "stills", "b2", "people.png"))


def export(got):
    """B2 to Unity (GrandBanquetHallDressing.Table.cs): the cloche and the platter in their roots' frames (built there),
    the frame and the people in the section's, the chain link round its middle (Table lays the links)."""
    k.export("BH2 cloche", "bh_cloche", "B2")
    k.export("BH2 platter", "bh_platter", "B2")
    k.export("BH2 frame", "bh_dumbwaiter", "B2")
    k.export("BH2 chain link", "bh_chain_link", "B2")
    ppl = got["people"]
    for name, objs in (("bh_guests", ppl[:-1]), ("bh_magician", ppl[-1:])):
        c = bpy.data.collections.new("BH2 " + name)
        bpy.context.scene.collection.children.link(c)
        for o in objs:
            c.objects.link(o)
        k.export(c.name, name, "B2")


if __name__ == "__main__":
    got = build()
    if "stills" in ARGS:
        stills(got)
    if "export" in ARGS:
        export(got)

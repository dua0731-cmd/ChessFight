# Stair end of the kingdom hall, assembled in grey clay from the lobby kit (kit_build.py), after
# 승규's stair reference (reference_stairs.webp, 2026-11-11).
# Layout read off the reference: two carpeted flights rise from a joined two-step base on either side
# of a curtained alcove with a round dais; at the top each flight turns outward in a U (winders) and
# lands on a corner gallery whose front face holds a lantern niche; the centre balcony over the alcove
# carries the white knight in front of a round window; columns, banners and the crown chandelier.
# Run inside Blender (the kit scene must exist):
#   exec(open(stair_end.py).read(), {"__file__": path, "__name__": "stair_end"}); build_stair_end()
# Axes as blockout_hall.py: +Y away from the lobby camera, +Z up, x = 0 on the hall axis.
# The left side is built with s = -1 and the right side mirrored in the geometry (no negative scales).
import bpy, math, os, builtins
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
K = getattr(builtins, "KIT_G", None)
if K is None:
    K = {"__file__": os.path.join(HERE, "kit_build.py"), "__name__": "kit_build"}
    exec(open(K["__file__"], encoding="utf-8").read(), K)
    builtins.KIT_G = K
MB, U, X, Z = K["MB"], K["U"], K["X"], K["Z"]
half_profile, centre, crown_solid, ccw = K["half_profile"], K["centre"], K["crown_solid"], K["ccw"]
TAU = 2 * math.pi
GOLD, RED = 1, 2          # R576: paint tags (kit_build.PAINT_TAGS)

# ---------------------------------------------------------------- dimensions (metres)
SIDE_X, BACK_Y, END_H = 9.5, 15.5, 10.6
RISE, TREAD = .27, .42
BASE_Y0 = 6.6                        # front of the joined two-step base
BASE_HALF = 7.0
FL_IN, FL_OUT = 2.6, 5.0             # |x| of a flight's inner and outer edges
FL_Y0 = BASE_Y0 + 2 * TREAD          # 7.44, first flight step
FL_N = 11
TURN_Y = FL_Y0 + FL_N * TREAD        # 12.06, where the flight starts to turn
LAND_Z = RISE * (2 + FL_N)           # 3.51, centre balcony over the alcove
R_IN = 1.0                           # inner (open, railed) radius of the U turn
TURN_CX = FL_OUT + R_IN              # |x| of the turn's centre
TURN_N = 11
TURN_DA = math.pi / TURN_N
GAL_Z = LAND_Z + TURN_N * RISE       # 6.48, corner galleries
GAL_IN = TURN_CX + R_IN              # |x| 7.0, inner face of the corner block
GAL_FRONT = 9.0
BAL_FRONT = 11.0                     # front face of the centre balcony (alcove wall)
AL_HALF, AL_TOP, AL_BACK = 1.75, 2.55, 12.0
DAIS_Y = BAL_FRONT
ROSE_Z = 7.8                         # R563: the great rose's centre, its voussoirs standing on the balcony (was 6.45, a 2 m window)
GRAND_ROSE = True                    # R563: lunette wall with the great rose, no balcony columns, no glass fan there
CARPET_X, CARPET_HALF = (FL_IN + FL_OUT) / 2, .85
COL_BACK_X = 2.65                    # back wall columns either side of the round window
FRONT_COL = (9.15, 8.45)             # big front columns, engaged in the side walls
FRONT_COL_R, COL_BACK_R = .56, .3    # their shafts' radius at the foot (R560: the traced column was .32 and .26)
NICHE_X = 7.95                        # |x| of the lantern niche in the corner block's front face
# R552 (승규 "말 빼주고 깃발도 지워줘"): no knight on the balcony, no banners; the round window has no glass
# so the sunset comes in through it, and its tracery is its own object that Unity turns
KNIGHT, BANNERS = False, False
CHANDELIER = False          # R561 (승규 "샹들리에 지워줘")

# the turn as seen from above, for the left side: centre, nosing line, the rectangle it fills
def turn_c(s):
    return Vector((s * TURN_CX, TURN_Y))


def tdir(s, a):
    """Unit vector from the turn's centre at angle a: a = 0 points at the flight (towards the hall
    axis), a = pi/2 at the end wall, a = pi at the side wall."""
    return Vector((-s * math.cos(a), math.sin(a)))


def nose_z_flight(y):
    """Height of the line through the flight's nosings."""
    return RISE * 3 + (y - FL_Y0) / TREAD * RISE


def nose_z_turn(a):
    """Same line round the turn (a = angle from the start of the turn, radians), level on the gallery."""
    return min(LAND_Z + RISE + a / TURN_DA * RISE, GAL_Z)


# ---------------------------------------------------------------- scene, collections, kit instances
def se_scene():
    sc = bpy.data.scenes.get("Stair End")
    if sc is None:
        sc = bpy.data.scenes.new("Stair End")
    root = bpy.data.collections.get("SE")
    if root is None:
        root = bpy.data.collections.new("SE")
    if root.name not in sc.collection.children:
        sc.collection.children.link(root)
    return sc, root


def se_col(name):
    sc, root = se_scene()
    c = bpy.data.collections.get("SE " + name)
    if c is None:
        c = bpy.data.collections.new("SE " + name)
        root.children.link(c)
    return c


def clear_se():
    sc, root = se_scene()
    for c in list(root.children):
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
    for me in list(bpy.data.meshes):
        if me.users == 0 and (me.name.startswith("P ") or me.name.startswith("SE ")):
            bpy.data.meshes.remove(me)


ANCHORS = {   # kit objects whose placing point is not their box's bottom centre
    "KN Knight White": (X(907), 0.0),
    "BD Board Stage": (X(1436), 219 * U + 10 * U),
}


NO_BEVEL = {"CH Chandelier", "KN Knight White", "BD Board Stage"}   # dense parts: their bevel only doubles them


def kit_mesh(name):
    """The kit object's finished mesh (modifiers applied), moved so its placing point is the origin."""
    pm = bpy.data.meshes.get("P " + name)
    if pm is not None:
        return pm
    src = bpy.data.objects[name]
    if name in NO_BEVEL:
        me = src.data.copy()
    else:
        kit_scene = next(s for s in bpy.data.scenes if src.name in s.objects)
        dg = kit_scene.view_layers[0].depsgraph
        dg.update()
        me = bpy.data.meshes.new_from_object(src.evaluated_get(dg), depsgraph=dg)
    me.transform(src.matrix_world)
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    ax, ay = ANCHORS.get(name, ((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2))
    me.transform(Matrix.Translation((-ax, -ay, -min(zs))))
    me.name = "P " + name
    return me


def put_kit(name, col, loc, rz=0.0, s=1.0, label=None, hang=False):
    """Place a kit part; hang=True puts its top (not its bottom) at loc.z."""
    me = kit_mesh(name)
    o = bpy.data.objects.new(label or name, me)
    col.objects.link(o)
    h = max(v.co.z for v in me.vertices) if hang else 0.0
    sv = s if isinstance(s, (tuple, list)) else (s, s, s)
    o.location = (loc[0], loc[1], loc[2] - h * sv[2])
    o.rotation_euler = (0, 0, rz)
    o.scale = sv
    return o


def finish(mb, name, col, bevel=1.2, sharp=35, segs=1):
    o = mb.obj("SE " + name, col, bevel=bevel, segs=segs, sharp=sharp)
    o.name = name
    return o


# ---------------------------------------------------------------- shapes from the kit's sheet profiles
def _unit(prof):
    z0 = prof[0][1]
    h = prof[-1][1] - z0
    return [(r / h, (z - z0) / h) for r, z in prof]


_PROF = {}


def baluster_unit():
    """R1's baluster (sheet), height 1."""
    if "bal" not in _PROF:
        cxx = centre(1235 - 14, 445, 1235 + 14, 480)
        _PROF["bal"] = _unit(half_profile(int(cxx) - 15, 438, int(cxx) + 15, 486))
    return _PROF["bal"]


def column_prof():
    """W1's round column (sheet), metres from its foot."""
    if "col" not in _PROF:
        ax = centre(20, 28, 110, 46)
        p = half_profile(15, 28, int(ax) + 1, 327, axis=ax, side="left")
        _PROF["col"] = [(r, z - p[0][1]) for r, z in p]
    return _PROF["col"]


def column(mb, x, y, z0, H, k=1.0, seg=32):
    """W1 column scaled by k, its plain shaft stretched so the whole column is H tall."""
    p = [(r * k, z * k) for r, z in column_prof()]
    seg_i = max(range(len(p) - 1), key=lambda i: p[i + 1][1] - p[i][1])
    extra = H - p[-1][1]
    mb.lathe([(r, z0 + z + (extra if i > seg_i else 0.0)) for i, (r, z) in enumerate(p)], x, y, seg)


# R560 (승규 "기둥 입체감이나 원형 기둥 입체감이 별로야"): the traced W1 column had a stepped, very slim shaft.
# Round columns now follow the gate picture's foreground columns: a square plinth, a big round torus, a thick
# gold ring, a fluted shaft with entasis (straight for its lower third, then 14% thinner at the top), a gold
# astragal, a neck, an ovolo echinus and a square abacus. All sizes are in R, the shaft's radius at its foot.
COL_FLUTES, COL_FLUTE_FILLET, COL_FLUTE_DEPTH = 16, .22, .1


def _arc(cx, cz, rh, rv, a0, a1, n):
    """Points of an elliptical arc (degrees), for lathe profiles [(r, z)]."""
    return [(cx + rh * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cz + rv * math.sin(math.radians(a0 + (a1 - a0) * i / n)))
            for i in range(n + 1)]


def round_column(mb, blk, x, y, z0, H, R, seg=40):
    """Column on the floor at (x, y, z0), H tall, foot radius R: lathe parts in mb, plinth and abacus in blk."""
    blk.box(x - 1.32 * R, x + 1.32 * R, y - 1.32 * R, y + 1.32 * R, z0, z0 + .3 * R)
    base = [(1.04, .3)] + _arc(1.04, .5, .2, .2, -90, 90, 10)[1:]
    base += [(1.02, .72), (.95, .76), (.93, .8), (.95, .84), (1.0, .86)]
    mb.lathe([(r * R, z0 + z * R) for r, z in base], x, y, seg)
    keep_mat, mb.mat = mb.mat, GOLD                       # R576: the gold ring
    mb.lathe([(r * R, z0 + z * R) for r, z in [(.95, .86)] + _arc(.99, .99, .13, .13, -90, 90, 10) + [(.95, 1.12)]], x, y, seg)
    mb.mat = keep_mat
    mb.lathe([(r * R, z0 + z * R) for r, z in [(1.0, 1.12), (1.0, 1.22)]], x, y, seg)
    za, zb = z0 + 1.22 * R, z0 + H - 1.15 * R
    rad = lambda t: R * (1 - .14 * max(0.0, (t - 1 / 3) / (2 / 3)) ** 1.6)
    fade = .4 * R
    zs = [za + fade * f for f in (0, .12, .35, .65, 1.0)]
    zs += [za + (zb - za) * t for t in (.12, .25, 1 / 3, .45, .57, .68, .78, .87)]
    zs += [zb - fade * f for f in (1.0, .65, .35, .12, 0)]
    zs = sorted(set(round(z, 5) for z in zs if za - 1e-6 <= z <= zb + 1e-6))
    w = TAU / COL_FLUTES
    pts = []
    for k in range(COL_FLUTES):
        a = k * w
        pts += [(a, 0.0), (a + COL_FLUTE_FILLET * w, 0.0)]
        pts += [(a + (COL_FLUTE_FILLET + (1 - COL_FLUTE_FILLET) * i / 5) * w, math.sin(math.pi * i / 5)) for i in range(1, 5)]
    rings = []
    for z in zs:
        m = min(1.0, (z - za) / fade, (zb - z) / fade)
        m = math.sin(math.pi / 2 * max(0.0, m))
        r = rad((z - za) / (zb - za))
        d = COL_FLUTE_DEPTH * R * m
        rings.append([(x + (r - d * f) * math.cos(a), y + (r - d * f) * math.sin(a), z) for a, f in pts])
    mb.loft(rings)
    rt = rad(1.0) / R
    keep_mat, mb.mat = mb.mat, GOLD                       # R576: the gold astragal
    mb.lathe([(r * R, zb + z * R) for r, z in [(rt - .02, 0.0)] + _arc(rt + .02, .09, .09, .09, -90, 90, 8)], x, y, seg)
    mb.mat = keep_mat
    cap = [(rt + .02, .18), (rt + .01, .2), (rt, .23), (rt, .42), (rt + .04, .44), (rt + .04, .47)]
    cap += [(rt + .04 + (1.22 - rt - .04) * math.sin(math.pi / 2 * i / 6), .47 + .31 * (1 - math.cos(math.pi / 2 * i / 6)))
            for i in range(1, 7)]
    cap += [(1.2, .8)]
    mb.lathe([(r * R, zb + z * R) for r, z in cap], x, y, seg)
    ab = [(1.3, .8), (1.3, 1.0)] + [(1.24 + .06 * math.cos(math.pi / 2 * i / 3), 1.0 + .06 * math.sin(math.pi / 2 * i / 3))
                                    for i in range(1, 4)] + [(1.2, 1.09), (1.2, 1.15)]
    blk.sq([(r * R, zb + z * R) for r, z in ab], x, y)


def baluster(mb, x, y, z0, h, kr=.8, seg=12):
    mb.lathe([(r * h * kr, z0 + z * h) for r, z in baluster_unit()], x, y, seg)


def diamond(mb, c, w, h, depth, facing=Vector((0, -1, 0))):
    """Gold diamond stud (C1's) on a wall: octahedron flattened into the face."""
    f = facing.normalized()
    side = Vector((-f.y, f.x, 0))
    c = Vector(c)
    up = Vector((0, 0, 1))
    v = [c + f * depth, c - side * w, c + up * h, c + side * w, c - up * h, c - f * .02]
    keep_mat, mb.mat = mb.mat, GOLD
    mb.add([tuple(p) for p in v], [(0, 1, 4), (0, 4, 3), (0, 3, 2), (0, 2, 1), (5, 4, 1), (5, 3, 4), (5, 2, 3), (5, 1, 2)])
    mb.mat = keep_mat


def sweep(mb, path, prof, flip=False):
    """Cross-section prof [(u, v)] swept along a 3D polyline: u along the path's left (horizontal)
    normal, v straight up. Sharp corners are mitred."""
    P = [Vector(p) for p in path]
    sgn = -1.0 if flip else 1.0
    def left(a, b):
        t = Vector((b.x - a.x, b.y - a.y, 0)).normalized()
        return Vector((-t.y, t.x, 0))
    rings = []
    for i, p in enumerate(P):
        ls = []
        if i > 0:
            ls.append(left(P[i - 1], p))
        if i < len(P) - 1:
            ls.append(left(p, P[i + 1]))
        n = (ls[0] + ls[-1]).normalized()
        m = 1.0 / max(n.dot(ls[0]), .3)
        rings.append([tuple(p + n * (sgn * u * m) + Vector((0, 0, v))) for u, v in prof])
    mb.loft(rings)


def smooth_z(path, span=.5):
    """Ease the kinks of a rail's height profile (moving average over about `span` metres)."""
    P = [Vector(p) for p in path]
    d = [0.0]
    for a, b in zip(P, P[1:]):
        d.append(d[-1] + (Vector((b.x, b.y)) - Vector((a.x, a.y))).length)
    out = []
    for i, p in enumerate(P):
        zs = [q.z for q, dq in zip(P, d) if abs(dq - d[i]) <= span / 2]
        out.append((p.x, p.y, sum(zs) / len(zs)))
    return out


# rail parts (u toward the open side, v up from the nosing line / floor edge)
STRINGER = [(-.36, -.62), (.07, -.62), (.07, .09), (.03, .15), (-.36, .15)]
PLINTH = [(-.36, -.42), (.06, -.42), (.06, -.30), (.09, -.27), (.09, .04), (.05, .15), (-.36, .15)]
RAIL_U = -.15
RAIL = [(RAIL_U - .15, .88), (RAIL_U + .15, .88), (RAIL_U + .17, .93), (RAIL_U + .15, 1.0), (RAIL_U + .09, 1.05),
        (RAIL_U - .09, 1.05), (RAIL_U - .15, 1.0), (RAIL_U - .17, .93)]
BAL_Z0, BAL_Z1 = .15, .88


def ring(mb, prof, seg=48):
    """Closed (r, z) profile spun round the z axis with no end caps (rings, mouldings)."""
    rings = [[(r * math.cos(TAU * k / seg), r * math.sin(TAU * k / seg), z) for k in range(seg)] for r, z in prof + prof[:1]]
    mb.loft(rings, False, False)


def post(mb, x, y, z0, z1, w=.42):
    """Square newel post: plinth, panelled body, cap (R1's end post, scaled)."""
    h = w / 2
    mb.box(x - h - .04, x + h + .04, y - h - .04, y + h + .04, z0, z0 + .14)
    mb.box(x - h, x + h, y - h, y + h, z0 + .14, z1 - .12)
    mb.box(x - h - .05, x + h + .05, y - h - .05, y + h + .05, z1 - .12, z1 - .05)
    mb.box(x - h - .02, x + h + .02, y - h - .02, y + h + .02, z1 - .05, z1)


def stone_blocks(mb, x0, x1, z0, z1, y_face, k=1.6, skip=None, M=None):
    """The kit's running-bond wall blocks (W1), k times the sheet size, on a face looking at -Y
    at y_face (M turns the face to another wall)."""
    keep = mb.M
    base = M if M is not None else Matrix.Identity(4)
    mb.M = keep @ base @ Matrix.Translation((0, y_face, 0)) @ Matrix.Diagonal((k, k, k, 1))
    sk = None
    if skip is not None:
        sk = lambda xa, xb, za, zb: [(a / k, b / k) for a, b in skip(xa * k, xb * k, za * k, zb * k)]
    K["wall_blocks"](mb, x0 / k, x1 / k, z0 / k, z1 / k, 0.0, sk)
    mb.M = keep


# ---------------------------------------------------------------- the pieces
def base_and_dais(col):
    mb = MB()
    for k in range(2):        # joined two-step base under both flights and the dais
        y0 = BASE_Y0 + k * TREAD
        mb.box(-BASE_HALF, BASE_HALF, y0, TURN_Y, 0, RISE * (k + 1))
    for k, r in enumerate(DAIS_R):     # round dais in front of the alcove, two more steps
        pts = [(r * math.cos(a), DAIS_Y + r * math.sin(a)) for a in [math.pi + math.pi * i / 32 for i in range(33)]]
        mb.prism_plan(ccw(pts), 2 * RISE, RISE * (3 + k))
        dais_trim(mb, r, RISE * (3 + k), last=k == len(DAIS_R) - 1)
    finish(mb, "Base And Dais", col)


# R593 (승규 "원형 계단이 좀 밋밋해 보여"): the round dais's steps get a rounded nosing over each riser, a thin gold
# bead along each riser, a gold line inlaid round each tread, and the top a red rug with a gold border (the
# stair carpets' red and gold, reference_stairs.webp).
DAIS_R = (2.3, 1.85)
DAIS_NOSE = .05                          # the nosing's round: it stands this far out of the riser
DAIS_ARC = 48


def _dais_arc(r, z):
    return [(r * math.cos(a), DAIS_Y + r * math.sin(a), z) for a in [math.pi + math.pi * i / DAIS_ARC for i in range(DAIS_ARC + 1)]]


def _dais_band(mb, r0, r1, z0, z1):
    """A closed half-ring slab between radii r0 < r1 (r0 = 0: a half disc) and heights z0 < z1."""
    n = DAIS_ARC
    rings = [[(r * math.cos(a), DAIS_Y + r * math.sin(a), z) for r, z in ((max(r0, 1e-3), z0), (r1, z0), (r1, z1), (max(r0, 1e-3), z1))]
             for a in [math.pi + math.pi * i / n for i in range(n + 1)]]
    mb.loft(rings)


def dais_trim(mb, r, z_top, last):
    keep = mb.mat
    mb.tube(_dais_arc(r - .005, z_top - DAIS_NOSE), DAIS_NOSE, seg=12)          # nosing (stone)
    mb.mat = GOLD
    mb.tube(_dais_arc(r + .004, z_top - RISE * .55), .018, seg=8)                # gold bead on the riser
    _dais_band(mb, r - .19, r - .15, z_top, z_top + .004)                      # gold line round the tread
    if last:
        _dais_band(mb, 1.22, 1.3, z_top, z_top + .016)                          # the rug's gold border
        mb.mat = RED
        _dais_band(mb, 0.0, 1.22, z_top, z_top + .014)                          # the red rug
    mb.mat = keep


def flights(col, s):
    """One straight flight (s = -1 left, +1 right) and its U turn up to the corner gallery."""
    mb = MB()
    x_in, x_out = s * FL_IN, s * FL_OUT
    for k in range(FL_N):
        y0 = FL_Y0 + k * TREAD
        mb.box(x_in, x_out, y0, TURN_Y, 0, RISE * (3 + k))
    # winders: from the inner arc out to the walls / the balcony side
    c = turn_c(s)
    xa, xb = (x_in, s * SIDE_X)
    lo_x, hi_x = min(xa, xb), max(xa, xb)
    def hit(a):
        d = tdir(s, a)
        ts = []
        if d.x > 1e-9:
            ts.append((hi_x - c.x) / d.x)
        if d.x < -1e-9:
            ts.append((lo_x - c.x) / d.x)
        if d.y > 1e-9:
            ts.append((BACK_Y - c.y) / d.y)
        t = min(t for t in ts if t > 0)
        return c + d * t
    corners = [Vector((x_in, BACK_Y)), Vector((s * SIDE_X, BACK_Y))]
    def ang(p):
        v = p - c
        return math.atan2(v.y, -s * v.x)
    for k in range(TURN_N):
        a0, a1 = k * TURN_DA, (k + 1) * TURN_DA
        inner = [c + tdir(s, a) * R_IN for a in (a0 + (a1 - a0) * i / 4 for i in range(5))]
        outer = [hit(a1)] + [q for q in sorted(corners, key=ang, reverse=True) if a0 < ang(q) < a1] + [hit(a0)]
        poly = [tuple(p) for p in inner + outer]
        mb.prism_plan(ccw(poly), 0, LAND_Z + RISE * (k + 1))
    finish(mb, f"Flight {'L' if s < 0 else 'R'}", col)
    # smooth faces on the open side, under the stringer: the flight's outer side and the turn's
    # inner drum (the steps' own bevelled ends would show as stripes there)
    mb = MB()
    e = .004
    rings = []
    for y in (FL_Y0 - .02, TURN_Y):
        top = nose_z_flight(y) - .5
        rings.append([(x_out + s * e, y, 0.0), (x_out + s * e, y, top)])
    for i in range(1, 49):
        a = math.pi * i / 48
        p = c + tdir(s, a) * (R_IN - e)
        rings.append([(p.x, p.y, 0.0), (p.x, p.y, nose_z_turn(a) - .5)])
    for i in range(len(rings) - 1):
        a0, a1 = rings[i], rings[i + 1]
        mb.add([a0[0], a1[0], a1[1], a0[1]], [(0, 1, 2, 3)])
    finish(mb, f"Flight Side {'L' if s < 0 else 'R'}", col, bevel=0, sharp=60)
    # carpet with two gold stripes, on the base steps, the flight and round the turn
    mb = MB()
    cx = s * CARPET_X
    t, sw = .015, .035
    for k in range(2 + FL_N):
        y0 = BASE_Y0 + k * TREAD
        zt = RISE * (k + 1)
        y1 = y0 + TREAD if k < 1 + FL_N else TURN_Y
        mb.box(cx - CARPET_HALF, cx + CARPET_HALF, y0 - t, y1, zt, zt + t)
        mb.box(cx - CARPET_HALF, cx + CARPET_HALF, y0 - t, y0, zt - RISE, zt + t)
        mb.mat = GOLD                                     # R576: the gold stripes
        for sx in (-.72, .72):
            mb.box(cx + sx - sw, cx + sx + sw, y0 - 2 * t, y1, zt + t, zt + 2 * t)
            mb.box(cx + sx - sw, cx + sx + sw, y0 - 2 * t, y0 - t, zt - RISE, zt + 2 * t)
        mb.mat = 0
    # the same lane round the turn: the flight's outer edge becomes the turn's inner radius
    r0 = R_IN + FL_OUT - (CARPET_X + CARPET_HALF)
    r1 = R_IN + FL_OUT - (CARPET_X - CARPET_HALF)
    def sector(ra, rb, a0, a1, z0, z1, n=6):
        pts = [c + tdir(s, a0 + (a1 - a0) * i / n) * ra for i in range(n + 1)]
        pts += [c + tdir(s, a1 - (a1 - a0) * i / n) * rb for i in range(n + 1)]
        mb.prism_plan(ccw([tuple(p) for p in pts]), z0, z1)
    def riser(ra, rb, a, z0, z1, off):
        d = tdir(s, a)
        tn = Vector((s * math.sin(a), math.cos(a))) * off     # towards the step below
        p = [c + d * ra - tn, c + d * rb - tn, c + d * rb, c + d * ra]
        mb.prism_plan(ccw([tuple(q) for q in p]), z0, z1)
    for k in range(TURN_N):
        a0, a1 = k * TURN_DA, (k + 1) * TURN_DA
        zt = LAND_Z + RISE * (k + 1)
        sector(r0, r1, a0, a1, zt, zt + t)
        riser(r0, r1, a0, zt - RISE, zt + t, t)
        mb.mat = GOLD
        for rs in (r0 + .13, r1 - .13):
            sector(rs - sw, rs + sw, a0, a1, zt + t, zt + 2 * t)
            riser(rs - sw, rs + sw, a0, zt - RISE, zt + 2 * t, 2 * t)
        mb.mat = 0
    finish(mb, f"Carpet {'L' if s < 0 else 'R'}", col, bevel=0)


def rail_path(s):
    """Edge line of one side's railing: up the flight's outer edge, round the turn, along the corner
    gallery's inner edge and front. Returns [(x, y, z_line)] for the left side's frame."""
    pts = []
    y = 8.1
    while y < TURN_Y - 1e-6:
        pts.append((s * FL_OUT, y, nose_z_flight(y)))
        y += .25
    c = turn_c(s)
    n = 36
    for i in range(n + 1):
        a = math.pi * i / n
        p = c + tdir(s, a) * R_IN
        pts.append((p.x, p.y, nose_z_turn(a)))
    for y in (11.4, 10.6, 9.8):
        pts.append((s * GAL_IN, y, GAL_Z))
    pts.append((s * GAL_IN, GAL_FRONT, GAL_Z))
    pts.append((s * (FRONT_COL[0] - .45), GAL_FRONT, GAL_Z))
    return pts


def railing(col, s):
    path = rail_path(s)
    flip = s > 0
    # stringer up the stairs, plinth along the gallery (the profile switches where the line levels out)
    k_lvl = next(i for i, p in enumerate(path) if p[2] >= GAL_Z - 1e-6)
    mb = MB()
    sweep(mb, path[:k_lvl + 1], STRINGER, flip)
    sweep(mb, path[k_lvl:], PLINTH, flip)
    finish(mb, f"Stringer {'L' if s < 0 else 'R'}", col)
    mb = MB()
    sweep(mb, smooth_z(path, .7), RAIL, flip)
    finish(mb, f"Handrail {'L' if s < 0 else 'R'}", col, bevel=.5)
    # balusters: one per tread up the flight and round the turn, every 0.45 m along the gallery
    mb = MB()
    def at(p, q, u):
        t = Vector((q[0] - p[0], q[1] - p[1])).normalized()
        n = Vector((-t.y, t.x)) * (-1 if flip else 1)
        return p[0] + n.x * u, p[1] + n.y * u
    for k in range(3, FL_N):
        y = FL_Y0 + (k + .5) * TREAD
        x, yy = at((s * FL_OUT, y), (s * FL_OUT, y + 1), RAIL_U)
        z0 = nose_z_flight(y) + BAL_Z0
        baluster(mb, x, yy, z0, nose_z_flight(y) + BAL_Z1 - z0)
    c = turn_c(s)
    for k in range(TURN_N):
        a = (k + .5) * TURN_DA
        p = c + tdir(s, a) * (R_IN - RAIL_U)
        z = nose_z_turn(a)
        baluster(mb, p.x, p.y, z + BAL_Z0, BAL_Z1 - BAL_Z0)
    gx = s * (GAL_IN - RAIL_U)
    for y in (11.55, 11.1, 10.65, 10.2, 9.75):
        baluster(mb, gx, y, GAL_Z + BAL_Z0, BAL_Z1 - BAL_Z0)
    for x in (GAL_IN + .55, GAL_IN + 1.0):
        baluster(mb, s * x, GAL_FRONT - RAIL_U, GAL_Z + BAL_Z0, BAL_Z1 - BAL_Z0)
    finish(mb, f"Balusters {'L' if s < 0 else 'R'}", col, bevel=0)
    mb = MB()
    post(mb, s * (GAL_IN - RAIL_U), GAL_FRONT - RAIL_U, GAL_Z, GAL_Z + 1.12)
    finish(mb, f"Posts {'L' if s < 0 else 'R'}", col)


def balcony(col):
    """Centre balcony over the alcove: mass, alcove frame, front balustrade, knight plinth."""
    mb = MB()
    mb.box(-FL_IN, FL_IN, AL_BACK, BACK_Y, 0, LAND_Z)                       # behind the alcove
    for s in (-1, 1):
        mb.box(s * FL_IN, s * AL_HALF, BAL_FRONT, AL_BACK, 0, LAND_Z)       # piers
    mb.box(-AL_HALF, AL_HALF, BAL_FRONT, AL_BACK, AL_TOP, LAND_Z)          # lintel
    mb.box(-AL_HALF, AL_HALF, BAL_FRONT, AL_BACK, 0, 4 * RISE)             # alcove floor on the dais
    # moulded frame round the opening, keyed lintel band
    f, pr = .2, .07
    for s in (-1, 1):
        x0, x1 = sorted((s * AL_HALF, s * (AL_HALF + f)))
        mb.box(x0, x1, BAL_FRONT - pr, BAL_FRONT + .01, 4 * RISE, AL_TOP + f)
    mb.box(-AL_HALF - f, AL_HALF + f, BAL_FRONT - pr, BAL_FRONT + .01, AL_TOP, AL_TOP + f)
    mb.box(-AL_HALF - f - .06, AL_HALF + f + .06, BAL_FRONT - pr - .03, BAL_FRONT + .01, AL_TOP + f, AL_TOP + f + .07)
    diamond(mb, (0, BAL_FRONT - pr - .02, AL_TOP + f + .4), .26, .3, .16)
    # stone blocks on the balcony face either side of the opening
    stone_blocks(mb, -FL_IN, FL_IN, 4 * RISE, LAND_Z - .45, BAL_FRONT,
                 skip=lambda xa, xb, za, zb: [(a, b) for a, b in ((xa, min(xb, -AL_HALF - f - .02)), (max(xa, AL_HALF + f + .02), xb)) if b > a]
                 if za < AL_TOP + f + .07 else [(xa, xb)])
    finish(mb, "Balcony", col)
    # front edge: plinth band, balusters, rail, posts at the ends and the knight plinth in the middle
    mb = MB()
    KP = .8 if KNIGHT else 0.0      # half width of the knight plinth
    if not KNIGHT:                  # one rail right across, from the right end to the left (as the halves run)
        path = [(FL_IN, BAL_FRONT, LAND_Z), (-FL_IN, BAL_FRONT, LAND_Z)]
        sweep(mb, path, PLINTH)
        sweep(mb, path, RAIL)
    for s in (-1, 1):
        if KNIGHT:
            path = [(s * KP, BAL_FRONT, LAND_Z), (s * FL_IN, BAL_FRONT, LAND_Z)]
            if s > 0:
                path = path[::-1]
            sweep(mb, path, PLINTH)
            sweep(mb, path, RAIL)
        n = 4 if KNIGHT else 6
        for i in range(n):
            x = s * (KP + .3 + (FL_IN - KP - .6) * i / (n - 1))
            baluster(mb, x, BAL_FRONT - RAIL_U, LAND_Z + BAL_Z0, BAL_Z1 - BAL_Z0)
        post(mb, s * (FL_IN - .2), BAL_FRONT - RAIL_U, LAND_Z, LAND_Z + 1.12)
    finish(mb, "Balcony Rail", col)
    if not KNIGHT:
        return
    mb = MB()
    mb.box(-KP - .06, KP + .06, BAL_FRONT - .14, BAL_FRONT + 1.3, LAND_Z, LAND_Z + .14)
    mb.box(-KP, KP, BAL_FRONT - .08, BAL_FRONT + 1.24, LAND_Z + .14, LAND_Z + .72)
    mb.box(-KP - .06, KP + .06, BAL_FRONT - .14, BAL_FRONT + 1.3, LAND_Z + .72, LAND_Z + .82)
    K["panel"](mb, -KP + .14, KP - .14, LAND_Z + .24, LAND_Z + .62, BAL_FRONT - .08, t=2.5, proud=1.5)
    finish(mb, "Knight Plinth", col)


def curtains(col):
    """Drapes tied back either side of the alcove, a two-swag valance, tassels on cords."""
    mb = MB()
    y_f = BAL_FRONT + .12
    for s in (-1, 1):
        # (z, inner x (from the middle), outer x) of the drape's outline, waves along its width
        rows = [(4 * RISE, 1.15, AL_HALF), (1.55, 1.38, AL_HALF), (1.95, 1.5, AL_HALF), (2.3, 1.2, AL_HALF), (AL_TOP, .9, AL_HALF)]
        rings = []
        for z, xi, xo in rows:
            n = 14
            front = []
            for i in range(n + 1):
                t = i / n
                x = xi + (xo - xi) * t
                y = y_f - .07 - .07 * math.sin(t * math.pi * 5) ** 2
                front.append((s * x, y, z))
            back = [(s * (xi + (xo - xi) * t), y_f + .06, z) for t in (1.0, 0.0)]
            rings.append(front + back)
        mb.loft(rings)
        # tie-back cord from the top corner and the tassel
        tx = s * 1.45
        mb.tube([(s * (AL_HALF - .05), y_f - .2, AL_TOP - .05), (s * 1.55, y_f - .2, 2.3), (tx, y_f - .21, 1.98)], .025, 6)
        mb.lathe([(1e-4, 1.98), (.06, 1.95), (.05, 1.88), (.035, 1.84), (.09, 1.62), (.1, 1.5), (1e-4, 1.5)], tx, y_f - .22, 12)
    # valance: two swags hanging between the corners and the middle
    rings = []
    n = 40
    for i in range(n + 1):
        x = -AL_HALF + 2 * AL_HALF * i / n
        sag = .42 * abs(math.sin(math.pi * x / AL_HALF))
        zb = AL_TOP - .12 - sag
        bul = .1 + .08 * abs(math.sin(math.pi * x / AL_HALF))
        rings.append([(x, y_f - .02, AL_TOP + .02), (x, y_f - bul, (AL_TOP + zb) / 2), (x, y_f - .03, zb), (x, y_f + .08, zb + .05), (x, y_f + .08, AL_TOP + .02)])
    mb.loft(rings)
    finish(mb, "Curtains", col, bevel=0, sharp=70)


def corner_block(col, s):
    """Block under a corner gallery: front face (lantern niche), inner face, mouldings, stone blocks."""
    mb = MB()
    x_in, x_out = s * GAL_IN, s * SIDE_X
    lo, hi = min(x_in, x_out), max(x_in, x_out)
    nx = s * NICHE_X
    w, sill, spring = 1.4, .4, 3.0      # tall like the reference's: from near the floor to above the crowns
    a = w / 2
    # body with the niche cut into its front: pieces round the opening, recess back
    depth = .5
    mb.box(lo, hi, GAL_FRONT + depth, TURN_Y, 0, GAL_Z)
    xl, xr = nx - a, nx + a
    mb.box(lo, min(xl, xr), GAL_FRONT, GAL_FRONT + depth, 0, GAL_Z)
    mb.box(max(xl, xr), hi, GAL_FRONT, GAL_FRONT + depth, 0, GAL_Z)
    mb.box(min(xl, xr), max(xl, xr), GAL_FRONT, GAL_FRONT + depth, 0, sill)
    arc = K["arch_pts"](nx, spring, a, a, 24)
    mb.prism(ccw(arc + [(nx + a, GAL_Z), (nx - a, GAL_Z)]), GAL_FRONT, GAL_FRONT + depth)
    # niche frame: jambs, arch band, imposts, keystone, sill
    f, pr = .17, .07
    outer = K["arch_pts"](nx, spring, a + f, a + f, 24)
    mb.strip(arc, outer, GAL_FRONT - pr, GAL_FRONT + .01)
    for e in (-1, 1):
        xa, xb = sorted((nx + e * a, nx + e * (a + f)))
        mb.box(xa, xb, GAL_FRONT - pr, GAL_FRONT + .01, sill, spring)
        xa, xb = sorted((nx + e * (a - .03), nx + e * (a + f + .05)))
        mb.box(xa, xb, GAL_FRONT - pr - .03, GAL_FRONT + .01, spring - .1, spring + .08)
    K["keystone"](mb, nx, spring + a - .05, spring + a + f + .14, .22, .3, GAL_FRONT - pr - .04, GAL_FRONT + .01)
    mb.box(nx - a - f - .05, nx + a + f + .05, GAL_FRONT - pr - .06, GAL_FRONT + .01, sill - .14, sill)
    # plinth band and a string course (both faces)
    for z0, z1, pr2 in ((0, .5, .08), (.5, .58, .05), (GAL_Z - 1.0, GAL_Z - .92, .05)):
        mb.box(lo, hi, GAL_FRONT - pr2, GAL_FRONT + .01, z0, z1)
        x0, x1 = sorted((x_in, x_in - s * pr2))
        mb.box(x0, x1, GAL_FRONT - pr2, TURN_Y - R_IN * 0 - .0, z0, z1)
    # stone blocks round the niche and on the inner face
    nl, nr = min(nx - a - f - .05, nx + a + f + .05), max(nx - a - f - .05, nx + a + f + .05)
    def skip(xa, xb, za, zb):
        if zb <= sill - .14 or za >= spring + a + f + .2:
            return [(xa, xb)]
        return [(p, q) for p, q in ((xa, min(xb, nl)), (max(xa, nr), xb)) if q > p]
    stone_blocks(mb, lo, hi, .58, GAL_Z - 1.0, GAL_FRONT, skip=skip)
    # inner face: local x runs along world y (front to back for the left side)
    if s < 0:
        stone_blocks(mb, GAL_FRONT, TURN_Y, .58, GAL_Z - 1.0, GAL_IN, M=Matrix.Rotation(math.pi / 2, 4, "Z"))
    else:
        stone_blocks(mb, -TURN_Y, -GAL_FRONT, .58, GAL_Z - 1.0, GAL_IN, M=Matrix.Rotation(-math.pi / 2, 4, "Z"))
    # niche back blocks
    stone_blocks(mb, min(xl, xr), max(xl, xr), sill, spring + a, GAL_FRONT + depth - .01,
                 skip=lambda xa, xb, za, zb: [(xa, xb)] if zb <= spring or all(((x - nx) / a) ** 2 + ((zb - spring) / a) ** 2 < .9 for x in (xa, xb)) else [])
    finish(mb, f"Corner Block {'L' if s < 0 else 'R'}", col)
    # lantern on the niche's back wall (the kit lantern's plate turned to the wall)
    lt_lantern(col, (nx, GAL_FRONT + depth - .41, 1.95), math.pi / 2, 1.0, f"Niche Lantern {'L' if s < 0 else 'R'}")


# R563 (승규 "회전 창문이 좀 밋밋해 빛 그래픽도 별로고", then "디자인이 별로야 ... 플레이어가 압도 할만한 느낌"): a cathedral
# rose. The stair end's wall now rises into the vault as a stone lunette (no glass fan there), and the window,
# 6.9 m across inside a 10 m ring of voussoirs, fills it above the balcony. Gothic tracery turns in it: a boss
# with six foils, a hub ring, 12 colonettes carrying pointed arches with an oculus in each head, a middle ring,
# 24 colonettes with their own pointed arches and oculi, 24 roundels between the arch heads and the rim.
# Behind it, stained glass in four colours. Unity turns tracery and glass together; the light in the air and on
# the floor reads the window from Resources/Lobby/KingdomHall/RosePattern.png, which rose_pattern.py renders from
# these very meshes, so after changing anything here run it again.
ROSE_S = .72                          # R563 (승규 "사이즈를 좀 줄여줘"): every size below x 0.8 (was 6.9 m across); R590 (승규 "회전 창문 크기 살짝 줄여줘"): x 0.72
_rs = lambda *v: tuple(round(x * ROSE_S, 4) for x in v)
ROSE_OPEN = 3.40 * ROSE_S             # the frame's lining (the tracery turns inside it)
ROSE_HOLE = 3.47 * ROSE_S             # the hole in the wall plate
ROSE_FRAME = 4.34 * ROSE_S            # the moulded frame's outer edge
ROSE_VOUSSOIR = _rs(4.36, 5.05)       # the ring of wedge stones round it
ROSE_BOSS, ROSE_FOILS = .26 * ROSE_S, _rs(.40, .19)   # boss; six foils (centre radius, radius)
ROSE_HUB, ROSE_MID, ROSE_RIM = _rs(.62, .74), _rs(1.90, 2.02), _rs(3.26, 3.38)
ROSE_IN = dict(n=12, spoke=.065 * ROSE_S, base=1.40 * ROSE_S, apex=1.88 * ROSE_S, oculus=_rs(1.56, .17))
ROSE_OUT = dict(n=24, spoke=.05 * ROSE_S, base=2.70 * ROSE_S, apex=3.10 * ROSE_S, oculus=_rs(2.86, .11), roundel=_rs(3.12, .135))
ROSE_GLASS = ("Gold", "Red", "Blue", "Amber")


def _polar(r, a, z):
    return (r * math.cos(a), r * math.sin(a), z)


def _bez(p0, p1, p2, n):
    return [tuple((1 - t) ** 2 * a + 2 * (1 - t) * t * b + t * t * c for a, b, c in zip(p0, p1, p2))
            for t in (i / n for i in range(n + 1))]


def pointed_arch(mb, rb, ra, a1, a2, z, rr):
    """A pointed arch between two colonettes (window-plane polar coordinates): each side rises nearly along its
    colonette, then leans over to the apex on the middle line."""
    am = (a1 + a2) / 2
    apex = _polar(ra, am, z)
    for a in (a1, a2):
        side = _bez(_polar(rb, a, z), _polar(rb + (ra - rb) * .8, a, z), apex, 7)
        mb.tube(side, rr, 6)


def circle_bar(mb, cx, cy, rad, z, rr, n=18):
    mb.tube([(cx + rad * math.cos(TAU * i / n), cy + rad * math.sin(TAU * i / n), z) for i in range(n + 1)], rr, 6, caps=False)


def rose_tracery(col, rot, centre):
    """The turning tracery and its glass, both with their origin on the window's centre."""
    mb = MB()
    mb.M = rot
    zc = .2                                   # the bars' middle depth, set back in the opening
    mb.lathe([(.001, .02), (.12, .03), (.2, .07), (ROSE_BOSS, .13), (ROSE_BOSS, .3), (.001, .3)], 0, 0, 32)
    for k in range(6):
        a = TAU * k / 6
        circle_bar(mb, ROSE_FOILS[0] * math.cos(a), ROSE_FOILS[0] * math.sin(a), ROSE_FOILS[1], zc, .035)
    for r0, r1 in (ROSE_HUB, ROSE_MID, ROSE_RIM):
        rm = (r0 + r1) / 2
        ring(mb, [(r0, .3), (r0, .14), (rm, .08), (r1, .14), (r1, .3)], 128 if r0 > 2 else 64)
    for zone, r_in, r_ring in ((ROSE_IN, ROSE_HUB[1], ROSE_MID[0]), (ROSE_OUT, ROSE_MID[1], ROSE_RIM[0])):
        n, step = zone["n"], TAU / zone["n"]
        for k in range(n):
            a = k * step
            mb.tube([_polar(r_in - .02, a, zc), _polar((r_in + zone["base"]) / 2, a, zc - .015), _polar(zone["base"] + .02, a, zc)],
                    zone["spoke"], 8)
            mb.sphere(_polar(zone["base"], a, zc - .03), zone["spoke"] * 1.7, 8, 5)       # the colonette's capital
            pointed_arch(mb, zone["base"], zone["apex"], a, a + step, zc, zone["spoke"] * .85)
            oc, orad = zone["oculus"]
            o = _polar(oc, a + step / 2, zc)
            circle_bar(mb, o[0], o[1], orad, zc, zone["spoke"] * .6)
            if "roundel" in zone:
                rc, rrad = zone["roundel"]
                q = _polar(rc, a, zc)
                circle_bar(mb, q[0], q[1], rrad, zc, .035)
    mb.M = Matrix.Identity(4)
    parts = [(mb, "Rose Tracery", .5)]
    # the glass, thin layers behind the bars: zones as rings, the small panes (oculi, roundels, foils) in front
    glass = {k: MB() for k in ROSE_GLASS}
    for g in glass.values():
        g.M = rot
    disc = lambda g, cx, cy, r, z: g.lathe([(.0005, z), (r, z), (r, z + .008), (.0005, z + .008)], cx, cy, 32)
    ann = lambda g, r0, r1, z: ring(g, [(r0, z), (r1, z), (r1, z + .008), (r0, z + .008)], 128)
    ann(glass["Gold"], ROSE_BOSS - .02, ROSE_HUB[0] + .02, .34)
    ann(glass["Red"], ROSE_HUB[1] - .02, ROSE_MID[0] + .02, .34)
    ann(glass["Blue"], ROSE_MID[1] - .02, ROSE_RIM[0] + .02, .34)
    for k in range(6):
        a = TAU * k / 6
        disc(glass["Amber"], ROSE_FOILS[0] * math.cos(a), ROSE_FOILS[0] * math.sin(a), ROSE_FOILS[1], .33)
    for zone, pane in ((ROSE_IN, "Blue"), (ROSE_OUT, "Red")):
        step = TAU / zone["n"]
        oc, orad = zone["oculus"]
        for k in range(zone["n"]):
            o = _polar(oc, (k + .5) * step, 0)
            disc(glass[pane], o[0], o[1], orad, .33)
            if "roundel" in zone:
                q = _polar(zone["roundel"][0], k * step, 0)
                disc(glass["Gold"], q[0], q[1], zone["roundel"][1], .33)
    for k, g in glass.items():
        g.M = Matrix.Identity(4)
        parts.append((g, f"Rose Glass {k}", 0))
    for m, name, bev in parts:
        o = finish(m, name, col, bevel=bev)
        o.data.transform(Matrix.Translation(-centre))
        o.location = centre


def back_wall(col):
    """End wall: a stone lunette up into the vault with the great rose (R563), its moulded frame and ring of
    voussoirs, the cornice and gallery band stopping at them, stone blocks."""
    mb = MB()
    y0, y1 = BACK_Y, BACK_Y + .6
    LUN = SIDE_X - .05                        # the lunette under the vault's arch (centre on the spring line)

    def edge(d):
        """How far from the window's centre the wall goes in direction d (rectangle up to END_H, then the
        half disc of the lunette: both convex and both hold the centre, so the larger exit wins)."""
        ts = []
        if abs(d.x) > 1e-9:
            ts.append(SIDE_X / abs(d.x))
        if d.y < -1e-9:
            ts.append(ROSE_Z / -d.y)
        elif d.y > 1e-9:
            ts.append((END_H - ROSE_Z) / d.y)
        t_rect = min(ts) if ts else 1e9
        oz = ROSE_Z - END_H                   # the centre seen from the lunette's centre
        b = d.y * oz
        t_disc = -b + math.sqrt(max(b * b - (oz * oz - LUN * LUN), 0))
        return max(t_rect, t_disc if d.y > 0 else 0)

    angles = [math.pi - TAU * i / 160 for i in range(161)]
    for cx, cz in ((-SIDE_X, 0), (SIDE_X, 0), (-SIDE_X, END_H), (SIDE_X, END_H)):
        a = math.atan2(cz - ROSE_Z, cx)
        while a > math.pi:
            a -= TAU
        while a < -math.pi:
            a += TAU
        angles.append(a)
    angles = sorted(set(round(a, 9) for a in angles), reverse=True)
    angles = [a for a in angles if a <= math.pi + 1e-9]
    angles.append(angles[0] - TAU)
    circ, outer = [], []
    for a in angles:
        d = Vector((math.cos(a), math.sin(a)))
        circ.append((d.x * ROSE_HOLE, ROSE_Z + d.y * ROSE_HOLE))
        t = edge(d)
        outer.append((d.x * t, ROSE_Z + d.y * t))
    mb.strip(circ, outer, y0, y1)
    # cornice at the vault's spring and a band at the gallery level, both stopping at the voussoirs
    for z0, z1, proud in ((END_H - .5, END_H - .3, .22), (END_H - .3, END_H, .3), (GAL_Z + 1.4, GAL_Z + 1.52, .08)):
        dz = min(abs(z0 - ROSE_Z), abs(z1 - ROSE_Z))
        cut = math.sqrt(max(ROSE_VOUSSOIR[1] ** 2 - dz * dz, 0)) - .05
        for sx in (-1, 1):
            mb.box(sx * SIDE_X, sx * cut, y0 - proud, y1, z0, z1)

    # stone blocks on the wall and the lunette, clear of the voussoirs
    def skip(xa, xb, za, zb):
        hw = SIDE_X
        if zb > END_H:
            hw = math.sqrt(max((LUN - .25) ** 2 - (zb - END_H) ** 2, 0))
        x0, x1 = max(xa, -hw), min(xb, hw)
        if x1 <= x0:
            return []
        R = ROSE_VOUSSOIR[1] + .08
        if zb < ROSE_Z - R or za > ROSE_Z + R:
            return [(x0, x1)]
        dz = 0 if za < ROSE_Z < zb else min(abs(za - ROSE_Z), abs(zb - ROSE_Z))
        half = math.sqrt(max(R * R - dz * dz, 0))
        return [(p, q) for p, q in ((x0, min(x1, -half)), (max(x0, half), x1)) if q > p]
    stone_blocks(mb, -SIDE_X, SIDE_X, LAND_Z, END_H + LUN, y0, skip=skip)
    finish(mb, "Back Wall", col)

    # the frame: three stepped orders, each with a roll, deeper inside, then a hollow and the outer edge
    mb = MB()
    rot = Matrix.Translation((0, y0, ROSE_Z)) @ Matrix.Rotation(-math.pi / 2, 4, "X")
    keep = mb.M
    mb.M = rot      # lathe's z axis now points into the wall (+Y); negative z stands proud of it
    arc = lambda cr, cz, rr: [(cr + rr * math.cos(math.radians(a)), cz + rr * math.sin(math.radians(a))) for a in range(180, 361, 20)]
    o = 3.40        # the profile drawn at full size, its radii then x ROSE_S
    prof = [(o, .6), (o, .16)] + arc(o + .08, .12, .08)
    prof += [(o + .2, .12), (o + .2, -.06)] + arc(o + .3, -.08, .1)
    prof += [(o + .44, -.08), (o + .44, -.26)] + arc(o + .56, -.3, .12)
    prof += [(o + .72, -.3), (o + .72, -.4), (o + .8, -.44), (o + .87, -.41), (o + .91, -.33), (o + .93, -.18), (4.34, -.04),
             (4.34, .02), (3.47, .02), (3.47, .6)]
    ring(mb, [(r * ROSE_S, z) for r, z in prof], 160)
    mb.M = keep
    # the voussoirs: 32 wedge stones, a big keystone on top and smaller ones at the sides and the foot
    n = 32
    for k in range(n):
        am = math.pi / 2 + TAU * k / n      # k = 0 on top
        key = k in (0, 8, 24)               # the top and the two sides (the foot stands on the balcony)
        half = .085 if key else TAU / n / 2 - .006
        a0, a1 = am - half, am + half
        r0, r1 = ROSE_VOUSSOIR[0], ROSE_VOUSSOIR[1] + ((.55 if k == 0 else .3) * ROSE_S if key else 0)
        pts = [(r0 * math.cos(a0), ROSE_Z + r0 * math.sin(a0)), (r1 * math.cos(a0), ROSE_Z + r1 * math.sin(a0)),
               (r1 * math.cos(a1), ROSE_Z + r1 * math.sin(a1)), (r0 * math.cos(a1), ROSE_Z + r0 * math.sin(a1))]
        mb.prism(ccw(pts), y0 - (.24 if key else .12), y0 + .02)
    finish(mb, "Round Window", col, bevel=1.0, segs=2)
    rose_tracery(col, rot, Vector((0, y0, ROSE_Z)))
    # the big engaged columns at the front corners (R560); the balcony's two columns made way for the rose
    mb, blk = MB(), MB()
    for s in (-1, 1):
        if not GRAND_ROSE:
            round_column(mb, blk, s * COL_BACK_X, BACK_Y - .1, LAND_Z, END_H - .5 - LAND_Z, COL_BACK_R)
        round_column(mb, blk, s * FRONT_COL[0], FRONT_COL[1], 0, END_H - .5, FRONT_COL_R)
    finish(mb, "Columns", col, bevel=0)
    finish(blk, "Column Blocks", col, bevel=1.5, segs=2)


HALL_FRONT = -14.0      # how far towards the lobby camera this preview builds the side walls
GATE_WALL = -25.0       # the gate wall (blockout_hall.py): the side bays are laid out up to it
FRONT_END = False       # the hall's other end wall is there too (glass spokes at HALL_FRONT)


def full_hall(gate_wall=-25.0):
    """For the Unity export: side walls, vault and floor run on to the gate wall."""
    global HALL_FRONT, GATE_WALL, FRONT_END
    HALL_FRONT = GATE_WALL = gate_wall
    FRONT_END = True


# ---------------------------------------------------------------- side walls (R553)
# 승규's side window picture (Kit/reference_side_windows.webp, "옆면 창 레퍼런스 사진이야"): bays between square
# pilasters (base, plinth, a gold ring at the sill, panelled shaft, gold ring capital, architrave block, frieze
# panel, cornice block). Each bay holds two round-headed windows with diamond leading, a mullion and a transom.
# Each window sits in its own moulded stone arch with impost blocks and a keystone, and one sill ledge runs under
# both. A string course, frieze and cornice run along the wall, with stone blocks between. The windows are open:
# the sky shows through them as through the vault.
# Measured in pixels on the picture's middle bay (REF_*). Widths scale so BAYS bays fill the wall from the big
# front column to the gate wall; heights scale so the cornice tops the wall at END_H (about 7% taller than the
# picture). The picture's banners stay out (R552 "깃발도 지워줘").
BAYS = 4
REF_BAY, REF_FLOOR, REF_TOP = 427.0, 590.0, 72.0     # px: pilaster centres apart, floor row, cornice-top row
REF_PIL = 38.5                                        # px: half the pilaster shaft
WALL_T = .5
PIL_DR = .6                                           # a pilaster's depth out of the wall / its half width (R560: was .4)
PIL_SINK = .06                                        # how deep the pilasters' panels are sunk


def bay_scale():
    """(bay pitch in m, metres per picture px across, metres per picture px up)."""
    pitch = (FRONT_COL[1] - GATE_WALL) / (BAYS + REF_PIL / REF_BAY)   # the last shaft stands against the gate wall
    return pitch, pitch / REF_BAY, END_H / (REF_FLOOR - REF_TOP)


def bay_piers():
    """World y of the pilasters; the big front column starts the first bay."""
    p = bay_scale()[0]
    return [FRONT_COL[1] - k * p for k in range(1, BAYS + 1)]


def _cut(ua, ub, holes):
    """[ua, ub] minus the (lo, hi) intervals in holes."""
    out = [(ua, ub)]
    for lo, hi in holes:
        nxt = []
        for p, q in out:
            if hi <= p or lo >= q:
                nxt.append((p, q))
                continue
            if lo > p:
                nxt.append((p, lo))
            if hi < q:
                nxt.append((hi, q))
        out = nxt
    return out


def window_glass(light, blue, cx, ai, z0, z_sp, h, y0, y1):
    """R582 (승규 "다음 텍스쳐 칠해", the reference's side windows): the panes behind diamond_leading, one per
    diamond, alternately pale and blue as in the pictures. The leading's lines are u + v and u - v whole numbers
    (u = (x - cx) / ai, v = (z - z0) / h), so each diamond is a cell of that grid, clipped to the round-headed
    opening (convex: Sutherland-Hodgman)."""
    hole = [(cx - ai, z0), (cx + ai, z0)] + [(cx + ai * math.cos(math.pi * i / 24), z_sp + ai * math.sin(math.pi * i / 24)) for i in range(25)]
    def clip(poly):
        for k in range(len(hole)):
            a, b = hole[k], hole[(k + 1) % len(hole)]
            side = lambda p: (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
            out = []
            for i in range(len(poly)):
                p, q = poly[i], poly[(i + 1) % len(poly)]
                sp, sq_ = side(p), side(q)
                if sp >= 0:
                    out.append(p)
                if (sp >= 0) != (sq_ >= 0):
                    t = sp / (sp - sq_)
                    out.append((p[0] + t * (q[0] - p[0]), p[1] + t * (q[1] - p[1])))
            poly = out
            if len(poly) < 3:
                return []
        return poly
    vmax = (z_sp + ai - z0) / h
    for i in range(-2, int(vmax) + 3):
        for j in range(-int(vmax) - 3, 3):
            st = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
            poly = clip([(cx + ai * (a + b) / 2, z0 + h * (a - b) / 2) for a, b in st])
            if len(poly) >= 3:
                (light if (i + j) % 2 == 0 else blue).prism(ccw(poly), y0, y1)


def diamond_leading(mb, cx, ai, z0, z_sp, h, y0, y1, bar=.06):
    """Diagonal leading over a round-headed opening (half width ai, straight sides from z0 to z_sp, then a
    half circle): diamonds as wide as half the opening, so their points touch the frame and the mullion,
    and h tall. Each line is clipped to the opening by stepping along it."""
    w = ai
    m = h / w
    def inside(x, z):
        if abs(x - cx) > ai or z < z0:
            return False
        return z <= z_sp or (x - cx) ** 2 + (z - z_sp) ** 2 < ai * ai
    n = int((z_sp + ai - z0) / h) + 3
    steps = int(2 * (ai + .2) / .01)
    for sgn in (1, -1):
        for k in range(-2 * n, 2 * n + 1):
            x0 = cx + k * w
            run, segs = None, []
            for i in range(steps + 1):
                x = cx - ai - .2 + i * .01
                z = z0 + sgn * m * (x - x0)
                if inside(x, z):
                    run = [(x, z), (x, z)] if run is None else [run[0], (x, z)]
                elif run is not None:
                    segs.append(run)
                    run = None
            if run is not None:
                segs.append(run)
            for p, q in segs:
                pa, pb = Vector((p[0], 0, p[1])), Vector((q[0], 0, q[1]))
                if (pb - pa).length < .03:
                    continue
                d = (pb - pa).normalized()
                nn = Vector((-d.z, 0, d.x)) * bar / 2
                mb.prism(ccw([(pa.x - nn.x, pa.z - nn.z), (pb.x - nn.x, pb.z - nn.z),
                              (pb.x + nn.x, pb.z + nn.z), (pa.x + nn.x, pa.z + nn.z)]), y0, y1)


def panel_block(mb, uc, hw, z0, z1, ph, pz0, pz1, dr=None, sink=None, bead=.035):
    """A pilaster block (u = uc +- hw, out of the wall hw * dr) with a panel sunk into its front between
    uc +- ph and pz0..pz1, a round bead in the panel's edge."""
    dr = PIL_DR if dr is None else dr
    sink = PIL_SINK if sink is None else sink
    D = hw * dr
    mb.box(uc - hw, uc + hw, -D + sink, hw * dr, z0, z1)
    for a, b, c, d in ((uc - hw, uc - ph, z0, z1), (uc + ph, uc + hw, z0, z1), (uc - ph, uc + ph, z0, pz0), (uc - ph, uc + ph, pz1, z1)):
        mb.box(a, b, -D, -D + sink + .002, c, d)
    e = bead * .7
    loop = [(uc - ph + e, pz0 + e), (uc + ph - e, pz0 + e), (uc + ph - e, pz1 - e), (uc - ph + e, pz1 - e)]
    for (ua, za), (ub, zb) in zip(loop, loop[1:] + loop[:1]):
        mb.tube([(ua, -D + sink, za), (ub, -D + sink, zb)], bead, 8)


def pilaster(mb, up, px, zr):
    """승규's side-window pilaster at u = up (wall-local frame, face at y = 0): plinth with a rounded top,
    base block with a sunk panel, a fat gold ring, the shaft's long sunk panel, a fatter gold ring under a
    gold band, the capital block with a sunk panel, and a moulded cap (cove, fillet, ovolo) under the cornice."""
    sq = lambda prof: mb.sq([(px(w), z) for w, z in prof], up, 0.0, PIL_DR)
    sq([(52.5, 0.0), (52.5, zr(557))] + [(47 + 5.5 * math.cos(math.pi / 2 * i / 5), zr(557) + 10 * (zr(547) - zr(557)) / 10 * math.sin(math.pi / 2 * i / 5))
                                          for i in range(1, 6)] + [(40, zr(547))])
    panel_block(mb, up, px(46), zr(547), zr(500), px(34), zr(542), zr(505))
    ring = [(42, zr(500))] + [(41.5 + 8.5 * math.cos(math.radians(a)), (zr(491) + 9 * (zr(482) - zr(491)) / 9 * math.sin(math.radians(a))))
                              for a in range(-75, 91, 15)] + [(36, zr(482))]
    mb.mat = GOLD                                           # R576: the gold rings and band
    sq(ring)
    mb.mat = 0
    panel_block(mb, up, px(38.5), zr(482), zr(190), px(27), zr(475), zr(195))
    top = [(36, zr(190))] + [(39 + 10 * math.cos(math.radians(a)), zr(176) + (zr(162) - zr(176)) * math.sin(math.radians(a)))
                             for a in range(-90, 91, 15)]
    top += [(49.5, zr(162)), (51.5, zr(160.5)), (51.5, zr(141.5)), (49.5, zr(140)), (40, zr(140))]
    mb.mat = GOLD
    sq(top)
    mb.mat = 0
    panel_block(mb, up, px(44), zr(140), zr(102), px(32), zr(136), zr(106))
    capm = [(40, zr(102)), (44, zr(102))]
    capm += [(44 + 8 * (1 - math.cos(math.pi / 2 * i / 5)), zr(102) + (zr(97) - zr(102)) * math.sin(math.pi / 2 * i / 5)) for i in range(1, 6)]
    capm += [(53.5, zr(97)), (53.5, zr(94))]
    capm += [(53.5 + 3.5 * math.sin(math.pi / 2 * i / 5), zr(94) + (zr(90) - zr(94)) * (1 - math.cos(math.pi / 2 * i / 5))) for i in range(1, 6)]
    capm += [(57, END_H)]
    sq(capm)


def side_walls(col):
    _, sh, sv = bay_scale()
    px = lambda n: n * sh                                   # picture px across -> m
    zr = lambda row: (REF_FLOOR - row) * sv                 # picture row -> height
    arch = K["arch_pts"]
    a = px(45)                                              # window half width (the frame's outside = the opening)
    f = px(9)                                               # frame band
    ai = a - f
    z_sill, z_apex = zr(506), zr(251)
    z_sp = z_apex - a                                       # round heads
    z_tr = zr(327)                                          # transom and impost blocks
    off = px(85)                                            # window centres either side of the bay's middle
    band, lip = px(15), px(31)                              # the stone arch: flat band, then the moulded lip
    piers = bay_piers()
    bays = [(y0, y1) for y0, y1 in zip([FRONT_COL[1]] + piers[:-1], piers) if (y0 + y1) / 2 > HALL_FRONT]
    y_end = min(HALL_FRONT, bays[-1][1]) if bays else HALL_FRONT
    wall, mould, glazing, blocks = MB(), MB(), MB(), MB()
    pane_light, pane_blue = MB(), MB()                     # R582: the windows' glass
    for s in (-1, 1):
        # wall-local frame: u along the wall, y = depth (negative into the hall), z up; the face at y = 0
        M = Matrix.Rotation(-s * math.pi / 2, 4, "Z") @ Matrix.Translation((0, SIDE_X, 0))
        u = (lambda y: y) if s < 0 else (lambda y: -y)
        for m in (wall, mould, glazing, blocks, pane_light, pane_blue):
            m.M = M
        # behind the big front column and the corner gallery: plain wall, the old cornice, blocks over the gallery
        ua, ub = sorted((u(FRONT_COL[1]), u(BACK_Y + .6)))
        wall.box(ua, ub, 0, WALL_T, 0, END_H)
        ua, ub = sorted((u(FRONT_COL[1]), u(BACK_Y)))
        wall.box(ua, ub, -.3, 0, END_H - .5, END_H)
        ua, ub = sorted((u(GAL_FRONT), u(BACK_Y)))
        stone_blocks(blocks, ua, ub, GAL_Z, END_H - .5, 0.0)
        # along the bays: base, string course, cornice
        ua, ub = sorted((u(y_end), u(GAL_FRONT)))
        mould.box(ua, ub, -.08, 0, 0, zr(555))
        mould.box(ua, ub, -.04, 0, zr(555), zr(551))
        ua, ub = sorted((u(y_end), u(FRONT_COL[1])))
        mould.box(ua, ub, -.06, 0, zr(170), zr(165))
        mould.box(ua, ub, -.12, 0, zr(165), zr(147))
        for z0, z1, d in ((zr(102), zr(94), .12), (zr(94), zr(82), .25), (zr(82), END_H, .35)):
            mould.box(ua, ub, -d, 0, z0, z1)
        if bays and y_end < bays[-1][1] - 1e-6:   # wall after the last pilaster, up to the gate wall
            ua, ub = sorted((u(y_end), u(bays[-1][1])))
            wall.box(ua, ub, 0, WALL_T, 0, END_H)
        uc_col = u(FRONT_COL[1])
        holes_full = [(uc_col - 1.0, uc_col + 1.0)]     # no stone blocks here at any height
        holes_win = []                                  # nor here between these heights
        for y0, y1 in bays:
            u0, u1 = sorted((u(y0), u(y1)))
            uc = (u0 + u1) / 2
            w1, w2 = uc - off, uc + off
            # the wall with its two openings
            for p, q in ((u0, w1 - a), (w1 + a, w2 - a), (w2 + a, u1)):
                wall.box(p, q, 0, WALL_T, 0, END_H)
            for cx in (w1, w2):
                wall.box(cx - a, cx + a, 0, WALL_T, 0, z_sill)
                wall.prism(ccw([(cx - a, END_H)] + arch(cx, z_sp, a, a, 24) + [(cx + a, END_H)]), 0, WALL_T)
                # stone arch: flat band, then the moulded lip, both down to the sill; impost blocks; keystone
                for r0, r1, d in ((a, a + band, .05), (a + band, a + lip, .12)):
                    mould.strip(arch(cx, z_sp, r0, r0, 24), arch(cx, z_sp, r1, r1, 24), -d, 0)
                    for e in (-1, 1):
                        p, q = sorted((cx + e * r0, cx + e * r1))
                        mould.box(p, q, -d, 0, z_sill + .02, z_sp)
                for e in (-1, 1):
                    p, q = sorted((cx + e * (a - .02), cx + e * (a + lip + .06)))
                    mould.box(p, q, -.16, 0, z_tr - .15, z_tr + .15)
                K["keystone"](mould, cx, z_apex + 19 * sv, z_apex + 44 * sv, px(18), px(25), -.2, 0)
                holes_win.append((cx - a - lip - .1, cx + a + lip + .1, z_sill, z_sp + a + lip + .35))
                # the window: frame, mullion, transom and the diamond leading, set back in the opening
                glazing.strip(arch(cx, z_sp, ai, ai, 24), arch(cx, z_sp, a, a, 24), .02, .14)
                for e in (-1, 1):
                    p, q = sorted((cx + e * ai, cx + e * a))
                    glazing.box(p, q, .02, .14, z_sill, z_sp)
                glazing.box(cx - a, cx + a, .02, .14, z_sill, z_sill + f)
                glazing.box(cx - px(2.5), cx + px(2.5), .05, .11, z_sill + f, z_sp + ai)
                glazing.box(cx - ai, cx + ai, .05, .11, z_tr - px(2.5), z_tr + px(2.5))
                diamond_leading(glazing, cx, ai, z_sill + f, z_sp, 42.5 * sv, .07, .10)
                window_glass(pane_light, pane_blue, cx, ai, z_sill + f, z_sp, 42.5 * sv, .084, .088)
            # one sill under both windows
            mould.box(uc - px(162), uc + px(162), -.1, 0, zr(527), z_sill - .1)
            mould.box(uc - px(166), uc + px(166), -.18, 0, z_sill - .1, z_sill + .02)
            holes_win.append((uc - px(166) - .05, uc + px(166) + .05, zr(527) - .05, z_sill + .05))
            # the pilaster ending the bay (R560: deeper, round mouldings, panels really sunk)
            up = u(y1)
            pilaster(mould, up, px, zr)
            holes_full.append((up - px(54) - .05, up + px(54) + .05))
        def skip(xa, xb, za, zb, hf=holes_full, hw=holes_win):
            return _cut(xa, xb, list(hf) + [(lo, hi) for lo, hi, z0, z1 in hw if zb > z0 and za < z1])
        ua, ub = sorted((u(y_end), u(FRONT_COL[1])))
        stone_blocks(blocks, ua, ub, zr(551), zr(170), 0.0, skip=skip)
        stone_blocks(blocks, ua, ub, zr(147), zr(102), 0.0, skip=skip)
    finish(wall, "Side Walls", col)
    finish(mould, "Side Bays", col, bevel=.8)
    finish(glazing, "Side Windows", col, bevel=0)
    finish(pane_light, "Window Glass Light", col, bevel=0)
    finish(pane_blue, "Window Glass Blue", col, bevel=0)
    finish(blocks, "Side Wall Blocks", col, bevel=0)
    side_lanterns(col, piers, y_end, px(38.5) * PIL_DR - PIL_SINK)     # the plate on the sunk panel


# R556, the picture's wall lanterns (승규 "등불도 넣어줘"): the kit's LT lantern, the same design, on the face of a
# pilaster, standing straight out of it. In the picture they sit on the pilasters at both ends of three bays
# (rows 230-385: about 3 m tall, 1.4 m across, centred 5.8 m up). Here they go on every other pilaster;
# the fourth one stands against the gate wall, with no room for one.
LANTERN_PIERS = (0, 2)
LANTERN_H, LANTERN_W, LANTERN_MID = 3.0, 1.4, 5.78
LANTERN_GLASS = (722 - 655) * U      # sheet: the glass's middle above the part's bottom (bottom plate end, Z(722))
LANTERN_AXIS = []                     # (x, y, z) of each lantern's glass middle, for Unity's glow lights


def side_lanterns(col, piers, y_end, face):
    me = kit_mesh("LT Wall Lantern")
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    h = max(v.co.z for v in me.vertices)
    sw = LANTERN_W / (max(ys) - min(ys))
    sz = LANTERN_H / h
    reach = max(xs)                   # the plate's wall side, out from the part's origin (its arms run along x)
    axis = min(xs) + 40 * U * math.cos(math.pi / 6)   # its own axis: the hexagonal brim (r 40 px) is the far side
    z0 = LANTERN_MID - LANTERN_H / 2
    LANTERN_AXIS.clear()
    for s in (-1, 1):
        for k in LANTERN_PIERS:
            y = piers[k]
            if y < y_end - .01:
                continue
            x = s * (SIDE_X - face - reach * sw)
            lt_lantern(col, (x, y, z0), 0.0 if s > 0 else math.pi, (sw, sw, sz), f"Pilaster Lantern {'L' if s < 0 else 'R'} {k}")
            LANTERN_AXIS.append((x + s * axis * sw, y, z0 + LANTERN_GLASS * sz))


LT_PLACED = []      # every kit LT lantern this script puts down: lantern_glass() covers their glass


def lt_lantern(col, loc, rz, s, label):
    o = put_kit("LT Wall Lantern", col, loc, rz=rz, s=s, label=label)
    LT_PLACED.append(o)
    return o


def lantern_glass(col):
    """The LT lanterns' glass as one object of its own, so Unity can make it glow: part_LT's hexagonal
    body (r 21 px at the bottom, 30 px at the top, rows 680-630), 3% bigger so it covers the clay one."""
    if not LT_PLACED:
        return None
    me = kit_mesh("LT Wall Lantern")
    ax = min(v.co.x for v in me.vertices) + 40 * U * math.cos(math.pi / 6)
    z0, z1 = (722 - 680) * U - .002, (722 - 630) * U + .002
    mb = MB()
    for o in LT_PLACED:
        mb.M = Matrix.LocRotScale(o.location, o.rotation_euler, o.scale)
        mb.lathe([(1e-4, z0), (21.6 * U, z0), (30.9 * U, z1), (1e-4, z1)], ax, 0.0, 6, a0=math.pi / 6)
    return finish(mb, "Lantern Glass", col, bevel=0)


# ---------------------------------------------------------------- gate end (R558)
# 승규's gate picture (reference_gate_end.webp, "정면 해줘"): the kit's G1 great gate at hall size. Read off
# the picture at GK metres per pixel (door axis x 707, floor row 422), so the door, its pilasters, the
# crowned pedestals and the lanterns fit between the side walls (the picture's hall is wider: its lanterns
# move in a little and its corner pilasters are the side walls' last ones). The picture's cornice under
# the glass sits on the side walls' string course; the end wall goes on up to their cornice, the stone
# arch and the crest rising through it as in the picture. No banners (R552).
# Second pass (승규 "문쪽 이랑 천장 입체감이 부족해, 들어간 부분 나와 있는 부분 굴곡진 부분"): the door sits
# 0.65 m back in a stepped reveal; curved mouldings (the arch's outer roll and inner bead, a two-band gold
# frame, moulded cornices, round pilaster rings and caps); raised shield panels and round ribs on the
# leaves; a 3D crown on a cushioned crest; rounded voussoirs and deep wall joints.
GK = .026
GATE_AX, GATE_FLOOR = 707, 422
GATE_RECESS = .25           # the face inside the stone arch, behind the wall face (gate-local y)
GATE_FRAME_Y = .95          # back of the gold frame
GATE_LEAF_Y = 1.05          # hinge line, the leaves' middle (front .97, back 1.13)
GATE_DEPTH = 1.2            # the end wall's thickness: the passage starts behind it
GATE_PASSAGE = 7.5          # the lit passage's far end (gate-local y)
GATE_LANTERN_X = 8.45       # picture 9.2 m: in to clear the side walls' last pilaster
GATE_LANTERN_AXIS = []


def gate_frame():
    """The gate end's own frame: x mirrored, y = depth behind the wall face (negative = into the hall),
    so the kit's front (-Y) looks into the hall."""
    return Matrix.Translation((0, GATE_WALL, 0)) @ Matrix.Rotation(math.pi, 4, "Z")


def gate_band(mb, inner, outer, y0, y1):
    for k in range(len(inner) - 1):
        mb.prism(ccw([inner[k], inner[k + 1], outer[k + 1], outer[k]]), y0, y1)


def plane_sweep(mb, pts, nrm, prof, cap=True):
    """Cross-section prof [(n, y)] swept along a curve in the xz plane: n along the curve's in-plane
    normal, y straight (depth)."""
    mb.loft([[(x + n * nx, y, z + n * nz) for n, y in prof] for (x, z), (nx, nz) in zip(pts, nrm)], cap, cap)


def portal_path(a, b, sp, z0=None, n=32):
    """A round-headed opening's outline as points and outward normals: up the left jamb from z0
    (None: the arch only), over the semi-ellipse (a across, b up, springing at sp), down the right jamb."""
    pts, nrm = [], []
    if z0 is not None:
        pts.append((-a, z0))
        nrm.append((-1.0, 0.0))
    for i in range(n + 1):
        t = math.pi * (1 - i / n)
        pts.append((a * math.cos(t), sp + b * math.sin(t)))
        v = Vector((b * math.cos(t), a * math.sin(t))).normalized()
        nrm.append((v.x, v.y))
    if z0 is not None:
        pts.append((a, z0))
        nrm.append((1.0, 0.0))
    return pts, nrm


def qcurve(p0, p1, convex, n=5):
    """Quarter ellipse from p0 to p1 [(y, z)], p0 left out: convex = ovolo (out first, then up),
    else a cove (up first, then out)."""
    (y0, z0), (y1, z1) = p0, p1
    out = []
    for i in range(1, n + 1):
        a = math.pi / 2 * i / n
        if convex:
            out.append((y0 + (y1 - y0) * math.sin(a), z0 + (z1 - z0) * (1 - math.cos(a))))
        else:
            out.append((y0 + (y1 - y0) * (1 - math.cos(a)), z0 + (z1 - z0) * math.sin(a)))
    return out


def cornice_prof(z0, z1, d):
    """Closed (y, z) section of a moulded cornice on a wall face at y = 0 (out = -y), projecting d:
    a round bead, a cove, the corona with its drip, an ovolo, the top fillet."""
    h = z1 - z0
    r = .07 * h
    P = [(0.0, z0), (-.12 * d, z0)]
    P += [(-.12 * d - r * math.sin(math.pi * i / 6), z0 + r - r * math.cos(math.pi * i / 6)) for i in range(1, 7)]
    P += qcurve((-.12 * d, z0 + 2 * r), (-.62 * d, z0 + .5 * h), False)
    P += [(-.78 * d, z0 + .5 * h), (-.78 * d, z0 + .74 * h)]
    P += qcurve((-.78 * d, z0 + .74 * h), (-d, z0 + .92 * h), True, 4)
    P += [(-d, z1), (0.0, z1)]
    return P


def run_x(mb, prof, x0, x1):
    """A (y, z) section run straight along x from x0 (a number, or a function of z) to x1."""
    f0 = x0 if callable(x0) else (lambda z: x0)
    mb.loft([[(f0(z), y, z) for y, z in prof], [(x1, y, z) for y, z in prof]])


def pilaster_prof():
    """Door pilaster half widths (picture px) against rows, bottom to top: plinth, torus, scotia, base
    block, round gold ring, shaft, astragal, ovolo cap, abacus."""
    P = [(43, 422), (43, 412)]
    P += [(40.5 + 3.5 * math.sin(math.pi * i / 6), 412 - 2 * i) for i in range(1, 7)]
    P += [(40.5 - 1.2 * math.sin(math.pi * i / 4), 400 - 1.25 * i) for i in range(1, 5)]
    P += [(40.5, 375)]
    P += [(39 + 4 * math.sin(math.pi * i / 8), 375 - 2.5 * i) for i in range(9)]
    P += [(38.5, 355), (38.5, 218)]
    P += [(38.5 + 2 * math.sin(math.pi * i / 4), 218 - 1.5 * i) for i in range(1, 5)]
    P += [(39 + 5.5 * math.sin(math.pi / 2 * i / 5), 212 - 2.4 * i) for i in range(6)]
    P += [(44.5, 200), (44.5, 192)]
    return P


def gate_end(col):
    px = lambda n: n * GK
    zr = lambda row: (GATE_FLOOR - row) * GK
    _, _, sv = bay_scale()
    zs = lambda row: (REF_FLOOR - row) * sv            # heights on the side walls (their own picture)
    arch = K["arch_pts"]
    Mg = gate_frame()
    # door: the gold frame (its outside is the hole through the wall), the leaves inside it
    fo, fi, f_sp, bo, bi = px(139.5), px(117), zr(235), px(113), px(91)
    # pilasters either side; the stone arch springs from their caps, a big round roll round it
    p_in, p_out = px(142), px(219)
    p_c = (p_in + p_out) / 2
    a_sp, ai, bai, ao, bao = zr(192), p_in, px(97), px(209.5), px(152)
    ROLL = .27
    lo, blo = ao + ROLL, bao + ROLL
    lip_x = lambda z: lo * math.sqrt(max(0.0, 1 - ((z - a_sp) / blo) ** 2)) if z > a_sp else lo
    z_lc0, z_lc1, z_tc = zs(170), zs(147), zs(102)
    XW = SIDE_X + .5
    RC, D = GATE_RECESS, GATE_DEPTH
    wall, stone, vous, gold, blocks, crest, pas, end = (MB() for _ in range(8))
    gold.mat = crest.mat = GOLD                             # R576: the frame and crest are gold, the shield's field red
    for m in (wall, stone, vous, gold, blocks, crest, pas, end):
        m.M = Mg

    # the wall in two layers: inside the stone arch its face steps back RC (the dark band round the frame
    # in the picture), then the gold frame's hole runs on through the wall's depth
    for a, b, sp, y0, y1 in ((ai, bai, a_sp, 0.0, RC), (fo, bo, f_sp, RC, D)):
        wall.box(-XW, -a, y0, y1, 0, END_H)
        wall.box(a, XW, y0, y1, 0, END_H)
        wall.prism(ccw([(-a, END_H)] + arch(0, sp, a, b, 32) + [(a, END_H)]), y0, y1)
    wall.box(-p_in, p_in, -1.1, RC, 0, .14)            # the step the runners cross (LobbyStage.Hall.cs HallPath)
    # plinth band with a round top, the low cornice (the side walls' string course) into the arch's roll,
    # the top cornice: all moulded
    zp = zr(395)
    plinth = [(0.0, 0.0), (-.1, 0.0), (-.1, zp - .1)]
    plinth += [(-.1 - .05 * math.sin(math.pi * i / 6), zp - .1 + .05 * (1 - math.cos(math.pi * i / 6))) for i in range(1, 7)]
    plinth += [(0.0, zp)]
    for s in (-1, 1):
        run_x(stone, plinth, s * (p_c + px(44.5)), s * SIDE_X)
        run_x(stone, cornice_prof(z_lc0, z_lc1, .32), lambda z, s=s: s * (lip_x(z) - .06), s * SIDE_X)
    run_x(stone, cornice_prof(z_tc, END_H, .38), -SIDE_X, SIDE_X)
    # pilasters with round mouldings; a broad raised frame on the shaft with a bead inside it
    prof = [(px(w), zr(r)) for w, r in pilaster_prof()]
    for s in (-1, 1):
        # R577 (승규 "문쪽 재질과 텍스쳐가 달라", the gate picture): the round ring at the foot and the astragal
        # under the cap are gold
        for i0, i1, m in ((0, 12, 0), (12, 22, GOLD), (22, 23, 0), (23, 27, GOLD), (27, len(prof) - 1, 0)):
            stone.mat = m
            stone.sq(prof[i0:i1 + 1], s * p_c, -.275, .375)
        stone.mat = 0
        x0, x1 = sorted((s * px(157), s * px(205)))
        K["panel"](stone, x0, x1, zr(350), zr(225), -.65, t=12, proud=6)
        K["panel"](stone, x0 + .2, x1 - .2, zr(350) + .2, zr(225) - .2, -.65, t=4, proud=2.5)
    # the stone arch: 9 rounded voussoirs, the big outer roll, a round bead in the inner edge
    K["ellipse_ring"](vous, 0, a_sp, ai, bai, ao, bao, -.55, RC, blocks=9, gap=3.0)
    pts, nrm = portal_path(ao, bao, a_sp, None, 40)
    plane_sweep(stone, pts, nrm, [(-.03, RC), (-.03, -.56), (.03, -.63), (.10, -.665), (.17, -.645), (.22, -.58),
                                  (.25, -.46), (.26, -.3), (ROLL, -.12), (ROLL, RC)])
    pts, nrm = portal_path(ai, bai, a_sp, None, 40)
    stone.tube([(x - .01 * nx, -.42, z - .01 * nz) for (x, z), (nx, nz) in zip(pts, nrm)], .09, 10)
    # wall blocks three times deeper than the side walls', kept off the pilasters, the arch and the cornices
    def skip(xa, xb, za, zb):
        h = (p_c + px(44.5) + .02) if za < a_sp + .02 else (lip_x(za) + .03 if za < a_sp + blo else 0.0)
        return _cut(xa, xb, [(-h, h)] if h > 0 else [])
    deep = Matrix.Diagonal((1, 3, 1, 1))
    stone_blocks(blocks, -SIDE_X, SIDE_X, zp, z_lc0, 0.0, skip=skip, M=deep)
    stone_blocks(blocks, -SIDE_X, SIDE_X, z_lc1, z_tc, 0.0, skip=skip, M=deep)

    # the gold frame: a broad round band outside, a narrow one inside, a groove between; lugs with rivets
    F = GATE_FRAME_Y
    pts, nrm = portal_path(fi, bi, f_sp, 0.0, 36)
    plane_sweep(gold, pts, nrm, [(.585, F), (.585, .74), (.565, .69), (.51, .655), (.43, .645), (.35, .655),
                                 (.295, .69), (.275, .74), (.275, F)])
    plane_sweep(gold, pts, nrm, [(0.0, F), (0.0, .76), (.025, .72), (.1, .705), (.175, .72), (.2, .76), (.2, F)])
    plane_sweep(gold, pts, nrm, [(.19, F), (.19, .84), (.285, .84), (.285, F)])
    for s in (-1, 1):
        x0, x1 = sorted((s * px(114), s * px(146)))
        for r0, r1 in ((247, 225), (407, 392)):
            gold.box(x0, x1, .55, .72, zr(r0), zr(r1))
            for zz in (zr(r0) + .12, zr(r1) - .12):
                gold.sphere((s * px(130), .54, zz), .05, 10, 6)

    # the crest: shield with a raised rim and a cushioned field, a 3D crown (flattened into it), two
    # rivets. Its outline is drawn from the picture (the sheet's traced shield is too coarse at 2.7 m):
    # a peak in the top's middle, square shoulders, sides narrowing a little, a pointed foot
    def bez(p0, p1, p2, n):
        return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
                 (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]) for t in (i / n for i in range(1, n + 1))]
    half = [(0, 11)] + bez((0, 11), (5, 21), (16.5, 22.5), 6) + [(50, 24.5)] + bez((50, 24.5), (52.5, 25), (52.5, 28), 3)
    half += [(42.5, 104)] + bez((42.5, 104), (42, 109), (39, 112), 3) + bez((39, 112), (22, 128), (0, 139), 8)
    out = [(x, r) for x, r in half] + [(-x, r) for x, r in half[-2:0:-1]]
    shield = ccw([(px(x), zr(r)) for x, r in out])
    cz = zr(72)
    crest.prism(shield, -.75, -.5)
    rim = [(x * .79, cz + (z - cz) * .79) for x, z in shield]
    gate_band(crest, rim + rim[:1], shield + shield[:1], -.88, -.74)
    pillow = [(x * .7, cz + (z - cz) * .7) for x, z in shield]
    crest.mat = RED
    crest.loft([[(x, -.75, z) for x, z in rim], [(x, -.81, z) for x, z in pillow]], False, True)
    crest.mat = GOLD
    crest.M = Mg @ Matrix.Translation((0, -.8, 0)) @ Matrix.Diagonal((1, .35, 1, 1))
    crown_solid(crest, 0, 0, zr(94), px(30), 169, seg=48)
    crest.M = Mg
    for s in (-1, 1):
        crest.sphere((s * px(40), -.9, zr(32)), .07, 10, 6)

    # the lit passage behind the gate (Gate Passage End glows in Unity)
    top = f_sp + bo
    pas.box(-fo, fo, 0, GATE_PASSAGE, -.2, .05)
    for s in (-1, 1):
        x0, x1 = sorted((s * fo, s * (fo + .3)))
        pas.box(x0, x1, D, GATE_PASSAGE, 0, top + .3)
    pas.box(-fo - .3, fo + .3, D, GATE_PASSAGE, top + .05, top + .35)
    end.box(-fo, fo, GATE_PASSAGE - .3, GATE_PASSAGE, 0, top + .1)

    finish(wall, "Gate Wall", col)
    finish(stone, "Gate Stone", col)
    finish(vous, "Gate Arch", col, bevel=4.0, segs=3)
    finish(blocks, "Gate Wall Blocks", col, bevel=.8)
    finish(gold, "Gate Frame", col, bevel=.8)
    finish(crest, "Gate Crest", col, bevel=1.2, segs=2)
    finish(pas, "Gate Passage", col, bevel=0)
    finish(end, "Gate Passage End", col, bevel=0)

    # the leaves, each on its hinge empty (Unity swings GateLeaf_L / GateLeaf_R): gate-local +x is the
    # hall's -x, the L side as blockout_hall.py
    FY = GATE_LEAF_Y - .08                              # the leaves' front
    for sl, side in ((1, "L"), (-1, "R")):
        hinge = Mg @ Vector((sl * fi, GATE_LEAF_Y, 0))
        pivot = bpy.data.objects.new(f"GateLeaf_{side}", None)
        col.objects.link(pivot)
        pivot.location = hinge
        lf = MB()
        lf.M = Mg
        q = lambda a, b, i, n=16: (sl * max(a * math.cos(math.pi / 2 * i / n), .004), f_sp + b * math.sin(math.pi / 2 * i / n))
        lf.prism(ccw([(sl * .004, .06), (sl * fi, .06)] + [q(fi, bi, i) for i in range(17)]), FY, FY + .16)
        st = px(20)
        x0, x1 = sorted((sl * .004, sl * st))
        lf.mat = GOLD                                     # R577: stile, rails, studs, cord and knight are gold
        lf.box(x0, x1, FY - .06, FY, .06, f_sp + bi - .08)                         # meeting stile
        lf.tube([(sl * st / 2, FY - .06, .15), (sl * st / 2, FY - .06, f_sp + bi - .3)], .09, 10)   # its round rib
        lf.tube([(x, FY - .03, z) for x, z in (q(fi - .33, bi - .33, i) for i in range(17))], .07, 8)   # arched rail
        x0, x1 = sorted((sl * st, sl * (fi - .02)))
        lf.box(x0, x1, FY - .05, FY, .06, zr(407))                                 # foot rail
        # R577: the gold edge round the leaf, as the picture's broad frame
        lf.tube([(x, FY - .03, z) for x, z in [(sl * (fi - .1), .1)] + [q(fi - .1, bi - .1, i) for i in range(17)]], .07, 8)
        for k in range(4):
            lf.sphere((sl * (st + .35 + k * (fi - st - .7) / 3), FY - .06, (.06 + zr(407)) / 2), .055, 10, 6)
        # the shield panel: raised and bevelled, a round cord round it, the knight on it. Pointed top and
        # foot with hollow (ogee) shoulders
        cxs, hw = sl * px(67.5), px(47.5)
        z_top, z_sh, z_lo, z_bot = zr(203), zr(240), zr(340), zr(377)
        def og(u, side, upper):
            x = cxs + side * hw * u
            return (x, z_sh + (z_top - z_sh) * (1 - u) ** 1.8) if upper else (x, z_lo - (z_lo - z_bot) * (1 - u) ** 1.8)
        us = [i / 10 for i in range(11)]
        loop = [og(u, 1, True) for u in us] + [og(u, 1, False) for u in us[::-1]]
        loop += [og(u, -1, False) for u in us[1:]] + [og(u, -1, True) for u in us[::-1][:-1]]
        czs = (z_top + z_bot) / 2
        field = [(cxs + (x - cxs) * .9, czs + (z - czs) * .93) for x, z in loop]
        lf.mat = 0                                        # the shield's field: the diamond wood
        lf.loft([[(x, FY, z) for x, z in loop], [(x, FY - .06, z) for x, z in field]], False, True)
        lf.mat = GOLD
        lf.tube([(x, FY - .01, z) for x, z in loop + loop[:1]], .065, 8, caps=False)
        K["relief"](lf, "knight", K["KNIGHT_BOX"], sl * px(67), zr(322), px(50), px(77), FY - .06, .12, mirror=(sl > 0))
        leaf = finish(lf, f"GateLeaf_{side} Door", col, bevel=1.2, segs=2)
        leaf.parent = pivot
        leaf.matrix_parent_inverse = Matrix.Translation(hinge).inverted()

    # crowned pedestals (kit C1) and lanterns (kit LT, glass through lantern_glass)
    me = kit_mesh("C1 Crown Pedestal")
    xs = [v.co.x for v in me.vertices]
    s_xy = px(91) / (max(xs) - min(xs))
    s_z = zr(275) / max(v.co.z for v in me.vertices)
    for s in (-1, 1):
        put_kit("C1 Crown Pedestal", col, (s * px(270), GATE_WALL + px(91) / 2 + .12, 0), rz=math.pi, s=(s_xy, s_xy, s_z),
                label=f"Gate Pedestal {'L' if s < 0 else 'R'}")
    me = kit_mesh("LT Wall Lantern")
    lx = [v.co.x for v in me.vertices]
    ly = [v.co.y for v in me.vertices]
    l_xy = px(50) / (max(ly) - min(ly))
    l_z = (zr(180) - zr(272)) / max(v.co.z for v in me.vertices)
    reach = max(lx)
    axis = min(lx) + 40 * U * math.cos(math.pi / 6)
    GATE_LANTERN_AXIS.clear()
    for s in (-1, 1):
        x = s * GATE_LANTERN_X
        lt_lantern(col, (x, GATE_WALL + reach * l_xy, zr(272)), -math.pi / 2, (l_xy, l_xy, l_z), f"Gate Lantern {'L' if s < 0 else 'R'}")
        GATE_LANTERN_AXIS.append((x, GATE_WALL + (reach - axis) * l_xy, zr(272) + LANTERN_GLASS * l_z))


# R558 (승규 "천장 입체감이 부족해"): sections of the vault's members (u along the hall or the arc, v in
# from the glass line): ribs with a top flange, chamfers and a round bead underneath; purlins; thin bars
RIB_PROF = ([(-.2, -.06), (.2, -.06), (.2, .06), (.15, .1), (.15, .3), (.11, .36), (.07, .40)]
            + [(.07 * math.cos(math.pi * i / 6), .40 + .07 * math.sin(math.pi * i / 6)) for i in range(1, 6)]
            + [(-.07, .40), (-.11, .36), (-.15, .3), (-.15, .1), (-.2, .06)])
PURLIN_PROF = [(-.09, -.05), (.09, -.05), (.09, .16), (.05, .22), (-.05, .22), (-.09, .16)]
BAR_PROF = [(-.035, -.03), (.035, -.03), (.035, .07), (.02, .09), (-.02, .09), (-.035, .07)]


VAULT_GLASS_OUT = .07    # R588: the glass this far outside the vault's glass line (the flanges reach .06)


def vault(col):
    """Glass barrel vault over the hall (the glass is the sky): moulded ribs over every side pilaster and
    bay middle (R553), purlins, a thin glazing bar halfway between each (panes about 2 x 2.5 m, as the picture), and a fan of
    spokes, two rings and a half boss over each end wall."""
    mb = MB()
    R = SIDE_X
    p = bay_scale()[0]
    y_lo = HALL_FRONT + (.2 if FRONT_END else 0.0)
    ribs = [BACK_Y - .2, (BACK_Y + FRONT_COL[1]) / 2] + [FRONT_COL[1] - k * p / 2 for k in range(2 * BAYS + 1)]
    ribs = [y for y in ribs if y > HALL_FRONT + (.8 if FRONT_END else -.01)]
    if FRONT_END:
        ribs.append(y_lo)     # the gate wall's own rib
    ribs.sort()

    def arc(y, prof, n=32):
        rings = []
        for i in range(n + 1):
            t = math.pi * i / n
            c, s = math.cos(t), math.sin(t)
            rings.append([((R - v) * c, y + u, END_H + (R - v) * s) for u, v in prof])
        mb.loft(rings)

    def run(a, prof, y0, y1):
        c, s = math.cos(a), math.sin(a)
        ring = lambda y: [((R - v) * c - w * s, y, END_H + (R - v) * s + w * c) for w, v in prof]
        mb.loft([ring(y0), ring(y1)])

    for y in ribs:
        arc(y, RIB_PROF)
    for y0, y1 in zip(ribs, ribs[1:]):
        arc((y0 + y1) / 2, BAR_PROF, 24)
    purlins = [0.0] + [math.radians(d) for d in (25, 55, 90, 125, 155)] + [math.pi]
    for a in purlins[1:-1]:
        run(a, PURLIN_PROF, y_lo, BACK_Y)
    for a, b in zip(purlins, purlins[1:]):
        run((a + b) / 2, BAR_PROF, y_lo, BACK_Y)
    # the half-round glass over each end wall: rings, spokes (every other one from the first ring), a boss
    ends = ([] if GRAND_ROSE else [(BACK_Y, -1)]) + ([(HALL_FRONT, 1)] if FRONT_END else [])   # R563: a lunette there
    for ye, sg in ends:
        ya, yb = sorted((ye, ye + sg * .18))
        sec = lambda w: ((-w, ya), (w, ya), (w, yb), (-w, yb))
        for r, w in ((1.0, .16), (3.4, .09), (6.5, .09)):
            mb.loft([[((r + dw) * math.cos(t), yy, END_H + (r + dw) * math.sin(t)) for dw, yy in sec(w)]
                     for t in (math.pi * i / 40 for i in range(41))])
        for k in range(1, 16):
            t = math.pi * k / 16
            c, s = math.cos(t), math.sin(t)
            spoke = lambda r: [(r * c - w * s, yy, END_H + r * s + w * c) for w, yy in sec(.05)]
            mb.loft([spoke(1.0 if k % 2 else 3.4), spoke(R - .2)])
        mb.prism(ccw([(.85 * math.cos(math.pi * i / 16), END_H + .85 * math.sin(math.pi * i / 16)) for i in range(17)]),
                 *sorted((ye, ye + sg * .3)))
    finish(mb, "Vault Ribs", col, bevel=0)
    # R588 (승규 "유리 천장", reference.webp / reference_side_windows.webp: glass between the ribs): the glass itself,
    # one half cylinder just outside the ribs' and purlins' top flanges (v = -.06), its faces looking in and down
    # at the hall. Unity makes it pale blue, see-through and glossy, and lets the sun through (no shadow).
    gl = MB()
    r, n = R + VAULT_GLASS_OUT, 64
    ring = lambda y: [(r * math.cos(math.pi * i / n), y, END_H + r * math.sin(math.pi * i / n)) for i in range(n + 1)]
    a, b = ring(y_lo), ring(BACK_Y)
    gl.add(a + b, [(i, i + 1, n + 2 + i, n + 1 + i) for i in range(n)])
    o = finish(gl, "Vault Glass", col, bevel=0)
    f = o.data.polygons[n // 2]                 # the top: its normal must point down (finish recalculates them)
    if f.normal.z > 0:
        for f in o.data.polygons:
            f.flip()


def decor(col):
    # knight on its plinth, facing left like the reference's
    if KNIGHT:
        put_kit("KN Knight White", col, (0, BAL_FRONT + .58, LAND_Z + .82), rz=math.pi, s=1.8, label="Balcony Knight")
    for s in (-1, 1):
        sd = "L" if s < 0 else "R"
        put_kit("C1 Crown Pedestal", col, (s * 5.55, 7.85, 2 * RISE), s=1.5, label=f"Stair Pedestal {sd}")
        if not BANNERS:
            continue
        # banners: red with the crown and black on the end wall, red on the front columns
        put_kit("BN Banner 1", col, (s * 3.85, BACK_Y - .12, END_H - .7), s=1.65, hang=True, label=f"End Banner Red {sd}")
        put_kit("BN Banner 2", col, (s * 5.8, BACK_Y - .12, END_H - .6), s=1.5, hang=True, label=f"End Banner Black {sd}")
        put_kit("BN Banner 1", col, (s * FRONT_COL[0] + s * -.05, FRONT_COL[1] - .62, END_H - 1.2), s=1.75, hang=True, label=f"Column Banner {sd}")
    if CHANDELIER:
        put_kit("CH Chandelier", col, (0, 9.6, 8.1), s=2.0, label="Crown Chandelier")


def floor_and_stage(col):
    mb = MB()
    mb.box(-SIDE_X, SIDE_X, HALL_FRONT, BACK_Y, -.2, 0)
    finish(mb, "Floor", col, bevel=0)
    put_kit("BD Board Stage", col, (0, 0, 0), s=(1.85, 1.85, .53), label="Board Stand-in")   # Unity draws the real 0.4 m stage


def camera_a(sc):
    cam = bpy.data.objects.get("SE Cam Lobby A")
    if cam is None:
        cd = bpy.data.cameras.new("SE Cam Lobby A")
        cam = bpy.data.objects.new("SE Cam Lobby A", cd)
        se_col("Cameras").objects.link(cam)
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(36)
    cam.data.clip_end = 300
    at, look = Vector((0.4, -12.5, 3.2)), Vector((0.4, 10.0, 3.0))
    cam.location = at
    cam.rotation_euler = (look - at).to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    return cam


def build_stair_end():
    clear_se()
    LT_PLACED.clear()
    sc, root = se_scene()
    shell, stairs, rails, dec, stand = se_col("Shell"), se_col("Stairs"), se_col("Rails"), se_col("Decor"), se_col("Stand-ins")
    base_and_dais(stairs)
    for s in (-1, 1):
        flights(stairs, s)
        railing(rails, s)
        corner_block(shell, s)
    balcony(shell)
    curtains(dec)
    back_wall(shell)
    side_walls(shell)
    vault(shell)
    decor(dec)
    if FRONT_END:
        gate_end(se_col("Gate End"))
    lantern_glass(dec)
    floor_and_stage(stand)
    camera_a(sc)
    tris = 0
    for c in root.children:
        for o in c.objects:
            if o.type == "MESH":
                tris += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return tris


def lights(sc):
    """Sun through the glass onto the stair end, a weaker fill from the gate side, a pale sky."""
    col = se_col("Lights")
    for name, energy, rot in (("SE Sun", 3.2, (38, 0, 20)), ("SE Fill", 1.0, (50, 0, 200))):
        o = bpy.data.objects.get(name)
        if o is None:
            o = bpy.data.objects.new(name, bpy.data.lights.new(name, "SUN"))
            col.objects.link(o)
        o.data.energy = energy
        o.data.angle = math.radians(4)
        o.rotation_euler = tuple(math.radians(v) for v in rot)
    w = bpy.data.worlds.get("SE Sky") or bpy.data.worlds.new("SE Sky")
    sc.world = w
    w.color = (.62, .68, .75)
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (.75, .8, .88, 1)
    bg.inputs[1].default_value = .5
    rib = bpy.data.objects.get("Vault Ribs")
    if rib is not None:
        rib.visible_shadow = False


def render_eevee(out_png, cam_name="SE Cam Lobby A", w=1920, h=1080, samples=32):
    sc, root = se_scene()
    lights(sc)
    sc.camera = bpy.data.objects[cam_name]
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = w, h, 100
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    sc.eevee.taa_render_samples = samples
    sc.view_settings.view_transform = "AgX"
    try:
        sc.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass
    sc.render.filepath = out_png
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(write_still=True, scene=sc.name)
    return out_png


def render_view(out_png, cam_name="SE Cam Lobby A", w=1920, h=1080, engine="BLENDER_WORKBENCH"):
    sc, root = se_scene()
    sc.camera = bpy.data.objects[cam_name]
    sc.render.resolution_x, sc.render.resolution_y, sc.render.resolution_percentage = w, h, 100
    sc.render.engine = engine
    sh = sc.display.shading
    sh.light, sh.color_type, sh.single_color = "STUDIO", "SINGLE", (0.8, 0.78, 0.74)
    sh.show_shadows, sh.show_cavity, sh.cavity_type = True, True, "BOTH"
    # light from high and in front, soft shadow edge: window lattices and rails don't stripe the floor
    sc.display.light_direction = (-0.35, -0.45, 0.82)
    sc.display.shadow_shift, sc.display.shadow_focus = .1, .2
    if sc.world is None:
        sc.world = bpy.data.worlds.get("SE Sky") or bpy.data.worlds.new("SE Sky")
    sc.world.color = (.62, .68, .75)
    sc.render.filepath = out_png
    sc.render.image_settings.file_format = "PNG"
    rib = bpy.data.objects.get("Vault Ribs")     # Workbench shadows can't be switched off per object
    if rib is not None:
        rib.hide_render = True
    try:
        bpy.ops.render.render(write_still=True, scene=sc.name)
    finally:
        if rib is not None:
            rib.hide_render = False
    return out_png


def view_cam(name, at, look, fov=40.0):
    cam = bpy.data.objects.get(name)
    if cam is None:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        se_col("Cameras").objects.link(cam)
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle = math.radians(fov)
    cam.data.clip_end = 300
    cam.location = Vector(at)
    cam.rotation_euler = (Vector(look) - Vector(at)).to_track_quat("-Z", "Y").to_euler()
    return cam

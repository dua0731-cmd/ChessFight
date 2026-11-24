# The grand banquet hall as its grey box stands after R828 (R818's chandeliers; R825's waiters, cloche lifts and king's
# balcony with no champagne tower; R828's dumbwaiter frame over the lifts and a fountain climbed against its flow with no
# ladders), dressed as a concept picture in the look of the place's concept art (Docs/PawnRush/ConceptArt/R787_BanquetHall):
# cream stone and walnut, gold, white banners with chess emblems, olive runners, red carpets along the way, candles, crown
# chandeliers. Four renders (R833): the whole hall, the chandeliers over the dance floor, the cloche lifts in their
# dumbwaiter and the chocolate fountain under the loft's ladder (the last three written beside the first as
# <out>_chandeliers.png, <out>_cloche_lifts.png, <out>_fountain.png). R818's version of this script drew R818's layout
# (git history). A picture only: nothing here goes into Unity.
# The layout is GrandBanquetHallBuilder.cs's (section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y)),
# its numbers copied below; the chandeliers are BanquetChandelierShape.cs's, built the same way (turned profiles, rings,
# tubes). The machines are posed at one moment of the shared clock (T): the west cloche lift open at the table to board,
# the east one open at the dais to step out, the curtain shut, the chocolate's ripples, the couples where the clock has them.
#
#   blender --background --factory-startup --python banquet_graybox_concept.py -- <out.png> [samples] [width] [height]
#
# The materials and helpers are banquet_blockout.py's (and through it ../RoyalObservatory/observatory_dress_preview.py's):
# its head, up to its first geometry, is run here (it reads the arguments and empties the scene).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler, Quaternion

_here = os.path.dirname(os.path.abspath(__file__))
_blk = open(os.path.join(_here, "banquet_blockout.py"), encoding="utf-8").read()
exec(compile(_blk.split("# ---------------------------------------------------------------- the hall")[0], "banquet_blockout.py (head)", "exec"), globals())
random.seed(11)
# The clock's moment (R833): the west cloche lift is open at the table (1.5 s into its 12.4 s) and the east one, half a
# cycle on, open at the dais; the curtain is shut (its 15 s cycle opens from 6 s), so it hides nothing of the fountain.
T = 1.5

# ---------------------------------------------------------------- the grey box's numbers (GrandBanquetHallBuilder, R828)
TY, SY, DY, HY, LY, CEIL = 4.4, 7.4, 10.4, 14.8, 20.0, 34.0
HX0, HX1, HZ1, SCR, DZ = -32.0, 32.0, 72.0, 6.0, 56.0
TRAY_X, BODY_X, TRAY_Y, TRAY_R, STACK_R, STACK_TOP = 27.8, 29.8, 2.4, 1.5, 0.8, 5.0
WZ0, WLANE, WSPEED, NW = 0.5, 29.0, 2.2, 4                   # R825: four waiters, the pantry door 8 m along the table
COUNTER = (22.0, 26.0, 4.0, 12.0)
KITCHEN_Z1, PANTRY_Z0, PANTRY_TOP = 4.0, 26.0, 7.8
T_X0, T_X1, T_Z0, CLOTH_Z1, RUN_X = 14.0, 26.0, 18.0, 50.0, 20.0
PLATES = (23.0, 29.0, 35.0, 41.0, 47.0)
# R825 the cloche lifts (ClocheLift), R828 the dumbwaiter frame they hang from (posts, a beam, pulleys, cables, chains).
CLOCHE_X, CLOCHE_Z, CLOCHE_R, BAND, OPEN = (17.0, 23.0), DZ - 0.1 - 2.2, 2.2, 1.6, 2.0
PLATTER_TOP = TY + 0.12
RISE = DY - PLATTER_TOP
GANTRY_X, GANTRY_Y, CABLE_X = (T_X0 + 0.15, 20.0, T_X1 - 0.15), 16.6, 2.4
CURTAIN_Z, STAGE_X1, CURTAIN_X, RAIL = 40.05, -20.0, -24.5, SY + 10.6
STAGE_Z0, STAGE_Z1, OPEN_S, PLEAT_W = CURTAIN_Z - 11.0, CURTAIN_Z + 9.0, 16.0, 1.05
BASS_X, BASS_TOP, BASS_Z = -23.95, 9.6, (19.0, CURTAIN_Z - 13.0)
BUF = (-32.0, -23.5, 3.0, 17.0)
FX, FZ, BOWL_R, BOWL_TOP = -27.7, 9.2, 4.2, 7.9
# R828: every tier flows from rim to foot at 0.3 m/s, four ripples on each run down it (ChocolateRipples).
TIER_R, TIER_TOP, FLOW, RIPPLES = (3.3, 2.3, 1.3), (10.733, 13.567, 16.4), 0.3, 4
LANES = (-2.6, 2.6)
ROPE_L = (6.5, 6.5, 6.5, 8.0, 8.0)
ROPE_LOW = (LY + 0.55, LY + 0.05, LY - 0.45, 17.8 + 0.55, 17.8 + 0.05)
GREAT_Y, GREAT_R = 17.8, 4.2
_SW = math.sin(math.radians(40))
_E = [l * _SW for l in ROPE_L]
_CH = math.sqrt(GREAT_R ** 2 - 2.6 ** 2)
ALONG = [0.35 + _E[0]]
ALONG.append(ALONG[0] + _E[0] + 0.8 + _E[1])
ALONG.append(ALONG[1] + _E[1] + 0.6 + _E[2])
GREAT_Z = SCR + ALONG[2] + _E[2] + 0.3 + _CH
ALONG.append(GREAT_Z + _CH + 0.3 - SCR + _E[3])
ALONG.append(ALONG[3] + _E[3] + 0.8 + _E[4])
LAND_Z = SCR + ALONG[4] + _E[4]
# R825: no champagne tower; the last ropes end over the king's balcony, the head table's wings either side of it.
BAL_Y, BAL_X, BAL_Z0 = 16.0, 6.0, LAND_Z - 0.8
TABLES_Z0 = BAL_Z0 + 0.9
LOFT_X0, LOFT_X1, LADDER_OUT = -31.0, 4.6, 0.3
BOARD_X = -4.25
print("GREAT_Z %.3f LAND_Z %.3f BAL_Z0 %.3f" % (GREAT_Z, LAND_Z, BAL_Z0))


def cloche_heights(t, delay):
    # ClocheLift.Heights: the platter's height over the table and the cloche's over the platter (0 shut, OPEN lifted off).
    board, shut, up, top = 3.0, 0.6, 2.2, 2.6
    u = (t - delay) % (board + 3 * shut + 2 * up + top + shut)
    ease = lambda x: 0.5 * (1 - math.cos(math.pi * min(1.0, max(0.0, x))))
    for length, f in ((board, lambda k: (0.0, OPEN)), (shut, lambda k: (0.0, OPEN * (1 - ease(k)))), (up, lambda k: (RISE * ease(k), 0.0)),
                      (shut, lambda k: (RISE, OPEN * ease(k))), (top, lambda k: (RISE, OPEN)), (shut, lambda k: (RISE, OPEN * (1 - ease(k)))),
                      (up, lambda k: (RISE * (1 - ease(k)), 0.0))):
        if u < length:
            return f(u / length)
        u -= length
    return 0.0, OPEN * ease(u / shut)

# ---------------------------------------------------------------- the concept art's colours
M_CREAM = marble("MAT_CreamStone", (0.66, 0.57, 0.44), (0.60, 0.51, 0.39), 0.28)
M_FLOORM = marble("MAT_FloorMarble", (0.52, 0.45, 0.35), (0.42, 0.36, 0.28), 0.5)
M_DAISM = marble("MAT_DaisMarbleC", (0.86, 0.82, 0.74), (0.76, 0.70, 0.60), 0.4)
M_CARPET = principled("MAT_RedCarpet", (0.40, 0.025, 0.04), 0.9)
M_OLIVE2 = principled("MAT_OliveCarpet", (0.16, 0.24, 0.07), 0.85)
M_BANNER = principled("MAT_Banner", (0.93, 0.90, 0.82), 0.8)
M_WINDOWL = principled("MAT_WindowLight", (1, 0.80, 0.52), 0.5, 0.0, (1.0, 0.70, 0.38), 1.6)
M_DOORL = principled("MAT_DoorLight", (1, 0.86, 0.62), 0.5, 0.0, (1.0, 0.74, 0.42), 3.0)
M_FLOWER = principled("MAT_Flower", (0.95, 0.93, 0.86), 0.6)
M_PINK = principled("MAT_FlowerPink", (0.92, 0.62, 0.62), 0.6)
M_GOLDB = principled("MAT_GoldBright", (1.0, 0.72, 0.26), 0.22, 1.0)
M_CRYSTALC = principled("MAT_CrystalClear", (0.92, 0.90, 0.84), 0.04, 0.0, (1.0, 0.86, 0.6), 0.04)
try:
    M_CRYSTALC.node_tree.nodes["Principled BSDF"].inputs["Transmission Weight"].default_value = 0.85
except Exception:
    pass
# The blockout's parquet, darker and less glossy here (it mirrored every candle).
M_PARQUET = principled("MAT_ParquetC", (0.19, 0.095, 0.04), 0.62)
M_PARQUET2 = principled("MAT_ParquetPaleC", (0.40, 0.27, 0.14), 0.62)
M_BALUSTER = principled("MAT_Baluster", (0.88, 0.84, 0.76), 0.35)
# R828: the chocolate's ripples, a little lighter and glossier than the sheet they run down.
M_CHOCWAVE = principled("MAT_ChocolateWave", (0.27, 0.12, 0.05), 0.16)
M_BLACK = principled("MAT_PawnBlack", (0.06, 0.055, 0.06), 0.45)


# ---------------------------------------------------------------- turned shapes, rings and tubes (BanquetChandelierShape's)
def _ring(bm, r, h, sides, m):
    if r < 1e-4:
        return [bm.verts.new(m @ Vector((0, 0, h)))]
    return [bm.verts.new(m @ Vector((r * math.cos(2 * math.pi * j / sides), r * math.sin(2 * math.pi * j / sides), h))) for j in range(sides)]


def _join(bm, a, b):
    n = max(len(a), len(b))
    if len(a) == 1 and len(b) == 1:
        return
    for j in range(n):
        if len(a) == 1:
            bm.faces.new((a[0], b[j], b[(j + 1) % n]))
        elif len(b) == 1:
            bm.faces.new((a[j], a[(j + 1) % n], b[0]))
        else:
            bm.faces.new((a[j], a[(j + 1) % n], b[(j + 1) % n], b[j]))


def lathe(bm, profile, m=Matrix(), sides=20):
    # (radius, height) pairs turned round the local Z (the section's y).
    prev = None
    for r, h in profile:
        ring = _ring(bm, r, h, sides, m)
        if prev is not None:
            _join(bm, prev, ring)
        prev = ring


def ring_bm(bm, R, r, m=Matrix(), seg=36, sides=8):
    rows = []
    for u in range(seg):
        a = 2 * math.pi * u / seg
        rad = Vector((math.cos(a), math.sin(a), 0))
        rows.append([bm.verts.new(m @ (rad * R + (rad * math.cos(2 * math.pi * v / sides) + Vector((0, 0, 1)) * math.sin(2 * math.pi * v / sides)) * r))
                     for v in range(sides)])
    for u in range(seg):
        a, b = rows[u], rows[(u + 1) % seg]
        for v in range(sides):
            bm.faces.new((a[v], b[v], b[(v + 1) % sides], a[(v + 1) % sides]))


def tube_bm(bm, pts, r, sides=8):
    pts = [Vector(p) for p in pts]
    along = (pts[1] - pts[0]).normalized()
    side = along.cross(Vector((0, 0, 1)) if abs(along.z) < 0.9 else Vector((1, 0, 0))).normalized()
    rows = []
    for i, p in enumerate(pts):
        nxt = (pts[i + 1] - p) if i + 1 < len(pts) else (p - pts[i - 1])
        prv = (p - pts[i - 1]) if i > 0 else nxt
        t = (nxt.normalized() + prv.normalized()).normalized()
        side = t.cross(side.cross(t)).normalized()
        up = t.cross(side)
        rows.append([bm.verts.new(p + (side * math.cos(2 * math.pi * s / sides) + up * math.sin(2 * math.pi * s / sides)) * r) for s in range(sides)])
    for i in range(len(rows) - 1):
        a, b = rows[i], rows[i + 1]
        for s in range(sides):
            bm.faces.new((a[s], b[s], b[(s + 1) % sides], a[(s + 1) % sides]))


def P(*xy):
    return [(xy[2 * i], xy[2 * i + 1]) for i in range(len(xy) // 2)]


def T3(x, y, z):
    return Matrix.Translation(Vector((x, y, z)))


def DIR(deg):
    a = math.radians(deg)
    return Vector((math.cos(a), math.sin(a), 0))


def to_mesh(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(mat)
    return me


def candle_cup(parts, at, size):
    s = size
    lathe(parts["brass"], P(.09 * s, .06 * s, .11 * s, .05 * s, .1 * s, .01 * s, .04 * s, -.04 * s, 0, -.05 * s), T3(*at), 12)
    lathe(parts["wax"], P(0, .365 * s, .03 * s, .36 * s, .045 * s, .34 * s, .045 * s, .05 * s), T3(*at), 10)
    lathe(parts["flame"], P(0, .58 * s, .03 * s, .5 * s, .045 * s, .43 * s, .02 * s, .38 * s, 0, .375 * s), T3(*at), 8)


def new_parts():
    return {k: bmesh.new() for k in ("brass", "wax", "flame", "crystal")}


PART_MATS = {"brass": M_GOLDB, "wax": M_WAX, "flame": M_FLAME, "crystal": M_CRYSTALC}


def finish(name, parts):
    return [(to_mesh(name + " " + k, bm, PART_MATS[k]), k) for k, bm in parts.items()]


def hanging_meshes(length):
    # BanquetChandelierShape.Hanging: origin at the pivot, the rope down -Z, the hoop at -length.
    p = new_parts()
    top = -(length - 2.5)
    br = p["brass"]
    ring_bm(br, .12, .03, T3(0, 0, -.1) @ Matrix.Rotation(math.pi / 2, 4, "X"), 16, 6)
    y = -.26
    k = 0
    while y > top + .05:
        m = T3(0, 0, y) @ Matrix.Rotation(k * math.pi / 2, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X") @ Matrix.Diagonal((1, 1, 1.6, 1))
        ring_bm(br, .07, .022, m, 10, 5)
        y -= .17
        k += 1
    lathe(br, P(0, .1, .12, .08, .22, 0, .18, -.08, .06, -.13), T3(0, 0, top), 20)
    ring, ry = 1.2, -1.25
    for i in range(6):
        d = DIR(i * 60 + 30)
        tube_bm(br, [Vector((0, 0, top - .1)) + d * .12, Vector((0, 0, top + ry + .04)) + d * (ring - .03)], .022, 6)
    lathe(br, P(.06, 0, .06, -.22, .14, -.32, .06, -.44, .05, -.6, .17, -.78, .24, -.98, .2, -1.14, .09, -1.26, .07, -1.48, .16, -1.62, .2, -1.78,
                .13, -1.93, .05, -2.03, 0, -2.08), T3(0, 0, top - .12), 20)
    ring_bm(br, .62, .035, T3(0, 0, top - .55), 36, 6)
    for i in range(6):
        tube_bm(br, [Vector((0, 0, top - .58)) + DIR(i * 60) * .08, Vector((0, 0, top - .58)) + DIR(i * 60) * .6], .02, 5)
        candle_cup(p, Vector((0, 0, top - .55)) + DIR(i * 60) * .62, .8)
    y0 = top + ry
    ring_bm(br, ring, .06, T3(0, 0, y0), 48, 8)
    ring_bm(br, ring - .05, .035, T3(0, 0, y0 - .14), 48, 6)
    for i in range(4):
        d = DIR(i * 90 + 45)
        tube_bm(br, [Vector((0, 0, y0 - .07)) + d * .2, Vector((0, 0, y0 - .07)) + d * (ring - .05)], .03, 6)
    for i in range(10):
        candle_cup(p, Vector((0, 0, y0 + .04)) + DIR(i * 36) * ring, 1.0)
        pt = Vector((0, 0, y0 + .05)) + DIR(i * 36 + 18) * ring
        lathe(br, P(0, .26, .03, .18, .045, .03, .03, 0), Matrix.Translation(pt), 8)
        lathe(p["crystal"], P(0, 0, .035, -.05, .055, -.13, 0, -.24), Matrix.Translation(pt + Vector((0, 0, -.2))), 8)
    lathe(br, P(.05, 0, .11, -.08, .08, -.16, .03, -.26), T3(0, 0, top - 2.2), 16)
    ring_bm(br, .14, .035, T3(0, 0, -length + .175) @ Matrix.Rotation(math.pi / 2, 4, "X"), 18, 6)
    return finish("Chandelier %.1f" % length, p)


def great_meshes(chain_up):
    # BanquetChandelierShape.Great: origin at the floor's middle.
    p = new_parts()
    br = p["brass"]
    r = GREAT_R
    lathe(br, P(0, 0, r - .05, 0, r, .14, r + .13, .16, r + .18, .05, r + .1, -.08, r - .3, -.22, 3, -.45, 1.6, -.78, .5, -1.05, 0, -1.12), Matrix(), 64)
    for i in range(24):
        at = DIR(i * 15 + 7.5) * (r + .12)
        candle_cup(p, at + Vector((0, 0, .17)), 1.1)
        lathe(p["crystal"], P(0, 0, .05, -.07, .08, -.19, 0, -.36), Matrix.Translation(at + Vector((0, 0, -.12))), 8)
    lathe(p["crystal"], P(0, -1.05, .5, -1.3, .32, -1.62, .16, -2, .26, -2.3, .1, -2.62, 0, -2.85), Matrix(), 24)
    lathe(br, P(0, 4.6, .08, 4.48, .16, 4.1, .3, 3.8, .18, 3.6, .2, 3.02, .36, 2.8, .22, 2.6, .26, 2.0, .44, 1.72, .56, 1.4, .46, 1.1, .32, .8,
                .38, .32, .6, .15, .55, 0), Matrix(), 32)
    for i in range(8):
        d = DIR(i * 45 + 22.5)
        arm = []
        for s in range(13):
            u = s / 12
            arm.append(d * (.3 + 1.6 * u) + Vector((0, 0, 1.95 - .45 * math.sin(math.pi * u) + .75 * u * u)))
        tube_bm(br, arm, .05, 8)
        candle_cup(p, arm[-1] + Vector((0, 0, .02)), 1.3)
        lathe(p["crystal"], P(0, 0, .045, -.06, .07, -.16, 0, -.3), Matrix.Translation(d * 1.15 + Vector((0, 0, 1.42))), 8)
    ring_bm(br, .8, .06, T3(0, 0, 3.9), 40, 8)
    for i in range(8):
        at = DIR(i * 45) * .8 + Vector((0, 0, 3.92))
        lathe(br, P(0, .5, .05, .38, .08, .06, .05, 0), Matrix.Translation(at), 10)
        lathe(br, P(0, .62, .06, .56, 0, .5), Matrix.Translation(at), 8)
    for i in range(4):
        tube_bm(br, [DIR(i * 90) * .75 + Vector((0, 0, 3.95)), DIR(i * 90) * (r + .05) + Vector((0, 0, .2))], .035, 6)
    y, k = 4.7, 0
    while y < 4.6 + chain_up:
        m = T3(0, 0, y) @ Matrix.Rotation(k * math.pi / 2, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X") @ Matrix.Diagonal((1, 1, 1.6, 1))
        ring_bm(br, .11, .035, m, 10, 5)
        y += .26
        k += 1
    return finish("Great chandelier", p)


def place_meshes(meshes, name, loc):
    g = bpy.data.objects.new(name, None)
    col.objects.link(g)
    g.location = loc
    g.rotation_mode = "QUATERNION"
    for me, k in meshes:
        o = bpy.data.objects.new(name + " " + k, me)
        col.objects.link(o)
        o.parent = g
    return g


def many(name, profile, places, mat, sides=12):
    # One mesh of the same turned shape at many places (section points): balusters, coupes.
    bm = bmesh.new()
    for x, y, z in places:
        lathe(bm, profile, T3(x, z, y), sides)
    o = bpy.data.objects.new(name, to_mesh(name, bm, mat))
    col.objects.link(o)
    return o


def carpet(points, y, width=1.6, mat=None):
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        a, b = U(x0, y + 0.012, z0), U(x1, y + 0.012, z1)
        d = b - a
        o = inst(ME_CUBE, "Carpet", mat or M_CARPET, (a + b) / 2, (width, d.length + width, 0.024))
        o.rotation_euler = (0, 0, math.atan2(-d.x, d.y))
        g = inst(ME_CUBE, "Carpet edge", M_GOLD, (a + b) / 2 + Vector((0, 0, -0.004)), (width + 0.18, d.length + width + 0.18, 0.02))
        g.rotation_euler = o.rotation_euler


def bouquet(x, y, z, s=1.0):
    cyl("Vase", x, y, y + 0.55 * s, z, 0.42 * s, M_GOLD, ME_CYL8)
    for i in range(7):
        a = i * 2.4
        ball("Bloom", x + math.cos(a) * 0.22 * s, y + 0.72 * s + (i % 3) * 0.08 * s, z + math.sin(a) * 0.22 * s, 0.3 * s, M_PINK if i % 3 == 0 else M_FLOWER)
    ball("Leaves", x, y + 0.62 * s, z, 0.7 * s, M_LEAF)


def candelabra(x, y, z, s=1.0):
    cyl("Candelabra foot", x, y, y + 0.08 * s, z, 0.5 * s, M_GOLD, ME_CYL8)
    cyl("Candelabra stem", x, y, y + 1.0 * s, z, 0.1 * s, M_GOLD, ME_CYL8)
    bar("Candelabra arm", (x - 0.4 * s, y + 0.9 * s, z), (x + 0.4 * s, y + 0.9 * s, z), 0.07 * s, M_GOLD)
    for dx in (-0.4, 0.0, 0.4):
        candle(x + dx * s, y + (0.95 if dx else 1.05) * s, z, 0.5 * s)


def banner(x, z, y0, y1, facing):
    # A white banner with gold edges and a gold king, hung on a wall facing `facing` (+1 east, -1 west, 0 south).
    if facing == 0:
        box("Banner", x - 1.6, x + 1.6, y0, y1, z - 0.12, z, M_BANNER)
        box("Banner hem", x - 1.7, x + 1.7, y0 - 0.3, y0 + 0.15, z - 0.14, z - 0.02, M_GOLD)
        cyl("Banner king", x, (y0 + y1) / 2 - 1.2, (y0 + y1) / 2 + 0.6, z - 0.16, 0.8, M_GOLD, ME_CYL8)
        ball("Banner king head", x, (y0 + y1) / 2 + 1.0, z - 0.16, 0.7, M_GOLD)
    else:
        xf = x + facing * 0.12
        box("Banner", min(x, xf), max(x, xf), y0, y1, z - 1.6, z + 1.6, M_BANNER)
        box("Banner hem", min(x, xf) - 0.02, max(x, xf) + 0.02, y0 - 0.3, y0 + 0.15, z - 1.7, z + 1.7, M_GOLD)
        cyl("Banner king", xf + facing * 0.04, (y0 + y1) / 2 - 1.2, (y0 + y1) / 2 + 0.6, z, 0.8, M_GOLD, ME_CYL8)
        ball("Banner king head", xf + facing * 0.04, (y0 + y1) / 2 + 1.0, z, 0.7, M_GOLD)


def sconce(x, y, z):
    ball("Sconce", x, y, z, 0.3, M_GOLD)
    candle(x, y + 0.1, z, 0.6)
    lamps.append((x, y + 0.9, z))


def player(x, y, z, team, s=0.85):
    piece(x, y, z, team, None, s)


# ---------------------------------------------------------------- the hall
box("Floor", HX0 - 1, HX1 + 1, -0.5, 0, -1.5, HZ1 + 1, M_FLOORM)
box("Wall W", HX0 - 1.2, HX0, 0, 30, 0, HZ1, M_CREAM)
box("Wall N w", HX0, -6.0, 0, 30, HZ1, HZ1 + 1.2, M_CREAM)
box("Wall N e", 6.0, HX1, 0, 30, HZ1, HZ1 + 1.2, M_CREAM)
box("Wall N low", -6.0, 6.0, 0, BAL_Y - 0.6, HZ1, HZ1 + 1.2, M_CREAM)
box("Wall N high", -6.0, 6.0, BAL_Y + 8.0, 30, HZ1, HZ1 + 1.2, M_CREAM)
box("Exit arch light", -3.5, 3.5, BAL_Y, BAL_Y + 7.6, HZ1 + 0.4, HZ1 + 0.6, M_DOORL)
box("Wall E low", HX1, HX1 + 1.2, 0, 9.0, 0, HZ1, M_CREAM)        # cut away over 9 m: the camera looks in from the east
box("Wall S low", HX0, HX1, 0, 8.0, -1.2, 0, M_CREAM)             # and from the south
# The walls' upper parts, for the views from inside the hall only (R833: through the cut the fountain's view saw the night).
CLOSE_ONLY = [box("Wall S high", HX0, HX1, 8.0, 30, -1.2, 0, M_CREAM), box("Wall E high", HX1, HX1 + 1.2, 9.0, 30, 0, HZ1, M_CREAM)]
box("Cornice W", HX0, HX0 + 0.5, 29.4, 30.2, 0, HZ1, M_GOLD)
box("Cornice N", HX0, HX1, 29.4, 30.2, HZ1 - 0.5, HZ1, M_GOLD)
for z in range(4, 70, 8):
    box("Pilaster W", HX0, HX0 + 0.6, 0, 29.4, z - 0.6, z + 0.6, M_WALNUT)
for x in range(-28, 30, 8):
    if abs(x) > 6:
        box("Pilaster N", x - 0.6, x + 0.6, 0, 29.4, HZ1 - 0.6, HZ1, M_WALNUT)
# Walnut wainscot under the windows, as in the concept art.
box("Wainscot W", HX0, HX0 + 0.25, 0, 9.0, 0, HZ1, M_WALNUT)
box("Wainscot W rail", HX0, HX0 + 0.35, 9.0, 9.4, 0, HZ1, M_GOLD)
box("Wainscot N", HX0, HX1, 0, 9.0, HZ1 - 0.25, HZ1, M_WALNUT)
for z in range(8, 66, 16):
    box("Window W frame", HX0, HX0 + 0.05, 11.6, 24.0, z - 0.4, z + 5.4, M_WALNUT)
    inst(ME_CYL, "Window W arch frame", M_WALNUT, U(HX0 + 0.03, 24.0, z + 2.5), (5.8, 5.8, 0.05), (0, math.pi / 2, 0))
    box("Window W mullion", HX0, HX0 + 0.2, 11.6, 26.0, z + 2.38, z + 2.62, M_WALNUT)
    box("Window W transom", HX0, HX0 + 0.2, 17.9, 18.1, z, z + 5.0, M_WALNUT)
    box("Window W", HX0 + 0.05, HX0 + 0.12, 12, 24, z, z + 5.0, M_WINDOWL)
    inst(ME_CYL, "Window W arch", M_WINDOWL, U(HX0 + 0.085, 24.0, z + 2.5), (5.0, 5.0, 0.07), (0, math.pi / 2, 0))
    box("Window W sill", HX0, HX0 + 0.4, 11.6, 12.0, z - 0.3, z + 5.3, M_GOLD)
    banner(HX0 + 0.1, z + 10.5, 12, 24, 1)
    sconce(HX0 + 0.5, 8.0, z + 2.5)
for x in (-26.0, -16.0, 11.0, 21.0):
    box("Window N frame", x - 0.4, x + 5.4, 12.6, 25.0, HZ1 - 0.05, HZ1, M_WALNUT)
    inst(ME_CYL, "Window N arch frame", M_WALNUT, U(x + 2.5, 25.0, HZ1 - 0.03), (5.8, 5.8, 0.05), (math.pi / 2, 0, 0))
    box("Window N mullion", x + 2.38, x + 2.62, 12.6, 27.0, HZ1 - 0.2, HZ1, M_WALNUT)
    box("Window N transom", x, x + 5.0, 18.9, 19.1, HZ1 - 0.2, HZ1, M_WALNUT)
    box("Window N", x, x + 5.0, 13, 25, HZ1 - 0.12, HZ1 - 0.05, M_WINDOWL)
    inst(ME_CYL, "Window N arch", M_WINDOWL, U(x + 2.5, 25.0, HZ1 - 0.085), (5.0, 5.0, 0.07), (math.pi / 2, 0, 0))
banner(-9.5, HZ1 - 0.02, 13, 27, 0)
banner(9.5, HZ1 - 0.02, 13, 27, 0)

# the screens, the passage's end, the loft over the passage
for (x0, x1, z0, z1) in ((BUF[1], 13.2, SCR - 0.2, SCR + 0.2), (12.8, 13.2, SCR + 0.2, T_Z0)):
    box("Screen", x0, x1, 0, 6.0, z0, z1, M_WALNUT)
    box("Screen cap", x0 - 0.1, x1 + 0.1, 6.0, 6.3, z0 - 0.1, z1 + 0.1, M_GOLD)
for x in range(-22, 13, 5):
    box("Screen panel", x, x + 3.6, 1.2, 5.0, SCR - 0.26, SCR - 0.2, M_OLIVE)
box("Passage end", -6.4, -6.0, 0, 6.0, 0, SCR - 0.2, M_WALNUT)
box("Loft", LOFT_X0, LOFT_X1, LY - 0.6, LY, 0, SCR - 0.4, M_CREAM)
box("Loft face", LOFT_X0, LOFT_X1, LY - 4.0, LY, SCR - 0.4, SCR, M_CREAM)
box("Loft cornice", LOFT_X0, LOFT_X1, LY - 4.2, LY - 3.9, SCR - 0.5, SCR + 0.1, M_GOLD)
box("Loft end", LOFT_X1, LOFT_X1 + 0.3, LY - 4.0, LY + 3.0, 0, SCR, M_WALNUT)
for x in range(-28, 4, 6):
    box("Loft corbel", x - 0.3, x + 0.3, 8.0, LY - 4.0, SCR - 0.5, SCR - 0.2, M_WALNUT)
for (x0, x1) in ((-24.0, -3.6), (-1.6, 1.6), (3.6, LOFT_X1)):
    many("Loft balusters", P(0.06, 0.9, 0.1, 0.8, 0.07, 0.55, 0.11, 0.25, 0.07, 0.05, 0.1, 0), [(x, LY, SCR - 0.12) for x in [x0 + 0.25 + 0.4 * i for i in range(int((x1 - x0 - 0.3) / 0.4) + 1)]], M_BALUSTER)
    box("Loft rail", x0, x1, LY + 0.9, LY + 1.05, SCR - 0.25, SCR, M_GOLD)
carpet([(FX, SCR - 1.2), (LANES[0], SCR - 1.2), (LANES[1], SCR - 1.2)], LY)

# ---------------------------------------------------------------- the servery, the kitchen, the pantry, the waiters
box("Counter", COUNTER[0], COUNTER[1], 0, TRAY_Y - 0.1, COUNTER[2], COUNTER[3], M_WALNUT)
box("Counter top", COUNTER[0] - 0.1, COUNTER[1] + 0.05, TRAY_Y - 0.1, TRAY_Y, COUNTER[2] - 0.1, COUNTER[3] + 0.1, M_LINEN)
ramp = inst(ME_CUBE, "Counter ramp", M_WALNUT, U((15 + COUNTER[0]) / 2, TRAY_Y / 2 - 0.3, (COUNTER[2] + COUNTER[3]) / 2), (math.hypot(7, TRAY_Y), COUNTER[3] - COUNTER[2] - 2, 0.6))
ramp.rotation_euler = (0, -math.atan2(TRAY_Y, 7.0), 0)
for i in range(3):
    cyl("Spare plates", 23 + i * 1.1, TRAY_Y, TRAY_Y + 0.8, 5.2, 1.0, M_PLATE)
box("Kitchen wall", 25.7, 26.0, 0, 7.8, 0, KITCHEN_Z1, M_CREAM)
box("Kitchen roof", 25.7, HX1, 7.5, 7.8, 0, KITCHEN_Z1, M_WALNUT)
box("Kitchen light", 26.2, HX1 - 0.2, 0, 7.2, 0.05, 0.2, M_DOORL)
box("Pantry lintel", 26.0, HX1, 7.2, PANTRY_TOP, PANTRY_Z0, PANTRY_Z0 + 0.3, M_WALNUT)
box("Pantry post", 31.7, HX1, 0, 7.5, PANTRY_Z0, PANTRY_Z0 + 0.3, M_WALNUT)
box("Pantry wall W", 25.7, 26.0, TY, PANTRY_TOP, PANTRY_Z0, DZ, M_CREAM)
box("Pantry roof", 25.7, HX1, 7.5, PANTRY_TOP, PANTRY_Z0, DZ, M_WALNUT)
box("Pantry cornice", 25.6, 26.1, PANTRY_TOP - 0.25, PANTRY_TOP + 0.05, PANTRY_Z0, DZ, M_GOLD)
box("Pantry light", 26.2, HX1 - 0.2, 0, 7.0, PANTRY_Z0 + 3.0, PANTRY_Z0 + 3.2, M_DOORL)
carpet([(0, 1.2), (0, 3.0), (15, 3.0), (15, 8.0)], 0.0)
carpet([(COUNTER[0] + 0.3, 8.0), (COUNTER[1] - 0.6, 8.0)], TRAY_Y)
waiter_body = frustum("ME_WaiterCoatC", 1.0, 0.55, 5.2)
for k in range(NW):
    z = WZ0 + (WSPEED * T + k * WLANE / NW) % WLANE
    if z > PANTRY_Z0 + 0.5:
        continue
    off = BODY_X - TRAY_X
    inst(waiter_body, "Waiter coat", M_COAT, U(BODY_X, 0, z), (1, 1, 1))
    torus("Waiter collar", BODY_X, 5.25, z, 0.5, 0.11, M_SHIRT)
    ball("Waiter head", BODY_X, 5.85, z, 1.15, M_IVORY)
    bar("Waiter arm", (BODY_X - 0.55, 4.7, z), (TRAY_X + 0.3, TRAY_Y - 0.1, z), 0.34, M_COAT)
    ball("Waiter glove", TRAY_X + 0.4, TRAY_Y - 0.15, z, 0.5, M_GLOVE)
    cyl("Tray", TRAY_X, TRAY_Y - 0.15, TRAY_Y, z, TRAY_R * 2, M_SILVER)
    cyl("Plate stack", TRAY_X, TRAY_Y, STACK_TOP, z, STACK_R * 2, M_PLATE)
    for r in range(10):
        torus("Plate rim", TRAY_X, TRAY_Y + 0.07 + r * 0.26, z, STACK_R, 0.035, M_GOLD)
    if k == 1:
        player(TRAY_X, STACK_TOP, z, 0)

# ---------------------------------------------------------------- the long table, the guests, the magician, the cloches
box("Long table", T_X0, T_X1, TY - 0.5, TY, T_Z0, DZ, M_WALNUT)
box("Long table skirt", T_X0 - 0.06, T_X1 + 0.06, 0.0, TY - 0.5, T_Z0 - 0.06, DZ, M_CLOTH2)
box("Tablecloth", T_X0 - 0.15, T_X1 + 0.15, TY, TY + 0.05, T_Z0 - 0.15, CLOTH_Z1, M_LINEN)
box("Tablecloth hem", T_X0 - 0.15, T_X1 + 0.15, TY - 1.2, TY + 0.05, T_Z0 - 0.2, T_Z0 - 0.15, M_LINEN)
box("Runner", RUN_X - 2.5, RUN_X + 2.5, TY + 0.05, TY + 0.07, T_Z0 + 1.0, CLOTH_Z1 - 1.0, M_OLIVE)
box("Runner edge", RUN_X - 2.6, RUN_X + 2.6, TY + 0.049, TY + 0.065, T_Z0 + 0.9, CLOTH_Z1 - 0.9, M_GOLD)
for z in PLATES:
    cyl("Plate", RUN_X, TY + 0.05, TY + 0.2, z, 2.4, M_PLATE)
    torus("Plate rim", RUN_X, TY + 0.21, z, 1.15, 0.05, M_GOLD)
    candelabra(RUN_X + 3.8, TY + 0.05, z + 3.0, 1.2)
    bouquet(RUN_X - 3.8, TY + 0.05, z + 3.0, 1.1)
    cyl("Goblet stem", RUN_X - 3.4, TY + 0.05, TY + 0.9, z + 1.0, 0.18, M_GLASS, ME_CYL8)
    cyl("Goblet bowl", RUN_X - 3.4, TY + 0.9, TY + 1.5, z + 1.0, 0.8, M_GLASS)
player(RUN_X, TY + 0.2, PLATES[2], 1)
player(RUN_X + 0.6, TY + 0.05, PLATES[3] + 2.5, 0)
guest_me = frustum("ME_GuestC", 1.05, 0.55, 4.4)
for i in range(9):
    z = 21.0 + 4.0 * i
    box("Chair back", 11.6, 12.0, 0.0, 5.6, z - 1.1, z + 1.1, M_WALNUT)
    box("Chair seat", 11.6, 14.0, 1.8, 2.2, z - 1.2, z + 1.2, M_OLIVE)
    inst(guest_me, "Guest", M_GUEST if i % 2 else M_CAPE, U(12.8, 0.0, z), (1, 1, 1))
    ball("Guest head", 12.8, 5.1, z, 1.15, M_IVORY if i % 3 else M_EBONY)
    cyl("Guest crown", 12.8, 5.6, 6.0, z, 0.55, M_GOLD, ME_CYL8)
mag = (RUN_X, 0.0, 15.2)
inst(frustum("ME_MagicianRobeC", 1.4, 0.85, 6.2), "Magician robe", M_ROBE, U(*mag), (1, 1, 1))
ball("Magician head", RUN_X, 6.9, 15.2, 1.35, M_IVORY)
cyl("Magician hat brim", RUN_X, 7.45, 7.6, 15.2, 2.0, M_EBONY)
cyl("Magician hat", RUN_X, 7.6, 9.1, 15.2, 1.25, M_EBONY)
for s in (-1, 1):
    bar("Magician arm", (RUN_X + s * 0.9, 5.6, 15.5), (RUN_X + s * 2.5, TY + 0.2, T_Z0 - 0.1), 0.42, M_ROBE)
# The cloche lifts (R825) in their dumbwaiter (R828): each platter hangs on two cables from gold lugs outside its
# cloche's band, each cloche on its own chain from its knob, over gold pulleys under a beam on three posts (slate in the
# grey box: they are not climbed; here walnut with gold bases and capitals, the east one on the pantry's wall). Nobody
# holds anything. The west lift is open at the table to board, the east one, half a cycle on, open at the dais.
def band_mesh():
    # The cloche's band: an open ring, so the platter shows under a lifted cloche.
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=False, cap_tris=False, segments=64, radius1=CLOCHE_R, radius2=CLOCHE_R, depth=BAND)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, BAND / 2))
    me = bpy.data.meshes.new("ME_ClocheBand")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    me.materials.append(None)
    return me


def chain(name, x, y0, y1, z, link=0.2, r=0.03, mat=None):
    # Links turned a quarter to each other from y0 up to y1 (section), as the chandeliers' chains.
    bm = bmesh.new()
    y, k = y0 + link * 0.5, 0
    while y < y1 - link * 0.3:
        m = T3(x, z, y) @ Matrix.Rotation(k * math.pi / 2, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "X") @ Matrix.Diagonal((1, 1.6, 1, 1))
        ring_bm(bm, link * 0.42, r, m, 12, 5)
        y += link * 0.82
        k += 1
    o = bpy.data.objects.new(name, to_mesh(name, bm, mat or M_GOLDB))
    col.objects.link(o)
    return o


band_me = band_mesh()
for i, x in enumerate(CLOCHE_X):
    up, lifted = cloche_heights(T, i * 6.2)
    py = PLATTER_TOP + up                            # the platter's top
    cy = py + lifted                                 # the cloche's foot
    cyl("Platter", x, py - 0.12, py, CLOCHE_Z, CLOCHE_R * 2, M_SILVER)
    torus("Platter rim", x, py - 0.02, CLOCHE_Z, CLOCHE_R, 0.05, M_GOLD)
    for s in (-1, 1):
        box("Platter lug", x + s * CLOCHE_R, x + s * (CLOCHE_R + 0.36), py - 0.08, py + 0.06, CLOCHE_Z - 0.15, CLOCHE_Z + 0.15, M_GOLDB)
        torus("Lug eye", x + s * CABLE_X, py + 0.16, CLOCHE_Z, 0.1, 0.03, M_GOLDB, (math.pi / 2, 0, 0))
        bar("Cable", (x + s * CABLE_X, py + 0.24, CLOCHE_Z), (x + s * CABLE_X, GANTRY_Y - 0.2, CLOCHE_Z), 0.07, M_IRON)
    inst(band_me, "Cloche band", M_CLOCHE, U(x, cy, CLOCHE_Z), (1, 1, 1))
    torus("Cloche foot", x, cy + 0.04, CLOCHE_Z, CLOCHE_R, 0.06, M_GOLD)
    torus("Cloche rim", x, cy + BAND, CLOCHE_Z, CLOCHE_R + 0.02, 0.07, M_GOLD)
    inst(ME_SPH, "Cloche dome", M_CLOCHE, U(x, cy + BAND + 0.2, CLOCHE_Z), (CLOCHE_R * 2, CLOCHE_R * 2, 1.6))
    ball("Cloche knob", x, cy + BAND + 1.05, CLOCHE_Z, 0.6, M_GOLD)
    torus("Knob eye", x, cy + BAND + 1.42, CLOCHE_Z, 0.12, 0.035, M_GOLDB, (math.pi / 2, 0, 0))
    chain("Cloche chain", x, cy + BAND + 1.5, GANTRY_Y - 0.2, CLOCHE_Z)
for gx in GANTRY_X:
    foot = PANTRY_TOP if gx > T_X1 - 1.0 else TY
    box("Dumbwaiter post", gx - 0.15, gx + 0.15, foot, GANTRY_Y, CLOCHE_Z - 0.15, CLOCHE_Z + 0.15, M_WALNUT)
    box("Dumbwaiter post base", gx - 0.24, gx + 0.24, foot, foot + 0.35, CLOCHE_Z - 0.24, CLOCHE_Z + 0.24, M_GOLD)
    box("Dumbwaiter post capital", gx - 0.24, gx + 0.24, GANTRY_Y - 0.3, GANTRY_Y, CLOCHE_Z - 0.24, CLOCHE_Z + 0.24, M_GOLD)
    ball("Dumbwaiter finial", gx, GANTRY_Y + 0.62, CLOCHE_Z, 0.36, M_GOLDB)
box("Dumbwaiter beam", T_X0, T_X1, GANTRY_Y, GANTRY_Y + 0.35, CLOCHE_Z - 0.25, CLOCHE_Z + 0.25, M_WALNUT)
box("Dumbwaiter beam trim", T_X0 - 0.05, T_X1 + 0.05, GANTRY_Y + 0.35, GANTRY_Y + 0.45, CLOCHE_Z - 0.3, CLOCHE_Z + 0.3, M_GOLD)
for x in CLOCHE_X:
    for s in (-1, 0, 1):
        inst(ME_CYL, "Pulley", M_GOLDB, U(x + s * CABLE_X, GANTRY_Y - 0.2, CLOCHE_Z), (0.42, 0.42, 0.12), (math.pi / 2, 0, 0))
        box("Pulley hanger", x + s * CABLE_X - 0.03, x + s * CABLE_X + 0.03, GANTRY_Y - 0.25, GANTRY_Y, CLOCHE_Z - 0.09, CLOCHE_Z + 0.09, M_GOLD)
player(CLOCHE_X[0] + 0.7, PLATTER_TOP, CLOCHE_Z - 0.5, 1)          # boarding the west lift
player(CLOCHE_X[0] - 1.2, TY + 0.05, CLOCHE_Z - 3.2, 0)            # on the way to it
player(CLOCHE_X[1] - 0.6, DY, DZ + 0.8, 0)                         # out of the east lift onto the dais

# ---------------------------------------------------------------- the royal dais, the gallery, the head table, the king's balcony
box("Dais", HX0, HX1, 0, DY, DZ, HZ1, M_DAISM)
box("Dais edge", HX0, HX1, DY - 0.45, DY + 0.04, DZ - 0.15, DZ + 0.05, M_GOLD)
for x in range(-30, 31, 4):
    box("Dais panel", x - 1.6, x + 1.6, 1.0, DY - 1.2, DZ - 0.08, DZ - 0.02, M_CREAM)
opening = 1.35
spans = ((HX0, BOARD_X - opening), (BOARD_X + opening, CLOCHE_X[0] - CLOCHE_R - 0.4), (CLOCHE_X[1] + CLOCHE_R + 0.4, HX1))
bal = []
for x0, x1 in spans:
    box("Balustrade base", x0, x1, DY, DY + 0.12, DZ, DZ + 0.3, M_BALUSTER)
    box("Balustrade rail", x0, x1, DY + 0.98, DY + 1.1, DZ - 0.02, DZ + 0.32, M_GOLD)
    n = int((x1 - x0) / 0.42)
    bal += [(x0 + 0.21 + 0.42 * i, DY + 0.12, DZ + 0.15) for i in range(n)]
many("Balusters", P(0.07, 0.86, 0.11, 0.78, 0.07, 0.6, 0.13, 0.3, 0.08, 0.06, 0.11, 0), bal, M_BALUSTER)
for s in (-1, 1):
    x = BOARD_X + s * (opening + 0.15)
    cyl("Boarding post", x, DY, DY + 1.8, DZ + 0.15, 0.3, M_GOLD, ME_CYL8)
    ball("Boarding lantern", x, DY + 2.1, DZ + 0.15, 0.6, M_LAMP)
    lamps.append((x, DY + 2.3, DZ + 0.15))
tables = TABLES_Z0
box("Gallery end W", BOARD_X - opening - 1.7, BOARD_X - opening - 1.4, DY, DY + 2.6, DZ + 0.3, tables, M_WALNUT)
box("Gallery end E", 26.0, 26.3, DY, DY + 2.6, DZ + 0.3, tables + 0.3, M_WALNUT)
box("Gallery end NE", 24.0, 26.0, DY, DY + 2.6, tables, tables + 0.3, M_WALNUT)
carpet([((CLOCHE_X[0] + CLOCHE_X[1]) / 2, DZ + 1.3), (BOARD_X, DZ + 1.3), (BOARD_X, DZ + 0.9)], DY)
# R825: the head table's two wings stand either side of the king's balcony.
for x0, x1 in ((-24.0, -BAL_X), (BAL_X, 24.0)):
    box("Head table", x0, x1, HY - 0.5, HY, tables, tables + 6.0, M_LINEN)
    box("Head table skirt", x0, x1, DY, HY - 0.5, tables - 0.06, tables + 6.0, M_CLOTH)
    box("Head table hem", x0 - 0.04, x1 + 0.04, HY - 0.75, HY - 0.45, tables - 0.1, tables + 6.0, M_GOLD)
    box("Head table runner", x0 + 0.5, x1 - 0.5, HY, HY + 0.02, tables + 2.4, tables + 3.6, M_OLIVE)
    for x in (x0 + 3, (x0 + x1) / 2, x1 - 3):
        candelabra(x, HY, tables + 1.2, 1.3)
        bouquet(x + 2.2, HY, tables + 3.0, 1.0)
# The king's balcony (R825, the end): a sheer marble front 16 m up, the last ropes' ends 0.8 m over its front, the exit
# at its back; gold rails along its sides, the two thrones and the canopy.
box("Balcony", -BAL_X, BAL_X, DY, BAL_Y, BAL_Z0, HZ1, M_DAISM)
box("Balcony fascia", -BAL_X - 0.1, BAL_X + 0.1, BAL_Y - 0.45, BAL_Y + 0.04, BAL_Z0 - 0.15, HZ1, M_GOLD)
box("Balcony plinth", -BAL_X - 0.1, BAL_X + 0.1, DY, DY + 0.5, BAL_Z0 - 0.12, HZ1, M_GOLD)
for x in (-3.9, 0.0, 3.9):
    box("Balcony panel", x - 1.5, x + 1.5, DY + 1.0, BAL_Y - 1.0, BAL_Z0 - 0.08, BAL_Z0 - 0.02, M_CREAM)
    box("Balcony panel frame", x - 1.62, x + 1.62, DY + 0.88, BAL_Y - 0.88, BAL_Z0 - 0.06, BAL_Z0 - 0.01, M_GOLD)
banner(0.0, BAL_Z0 - 0.1, DY + 1.6, BAL_Y - 1.6, 0)
for x in (-BAL_X - 0.3, BAL_X):
    box("Balcony rail", x, x + 0.3, BAL_Y + 0.95, BAL_Y + 1.1, BAL_Z0, HZ1, M_GOLD)
    many("Balcony balusters", P(0.07, 0.95, 0.11, 0.85, 0.07, 0.6, 0.13, 0.3, 0.08, 0.06, 0.11, 0),
         [(x + 0.15, BAL_Y, z) for z in [BAL_Z0 + 0.3 + 0.42 * i for i in range(int((HZ1 - BAL_Z0 - 0.3) / 0.42))]], M_BALUSTER)
carpet([(0, BAL_Z0 + 1.6), (0, HZ1 - 0.3)], BAL_Y)
for s in (-1, 1):
    box("Throne", s * 4.2 - 0.9, s * 4.2 + 0.9, BAL_Y, BAL_Y + 3.6, HZ1 - 2.6, HZ1 - 1.2, M_GOLD)
    box("Throne seat", s * 4.2 - 0.9, s * 4.2 + 0.9, BAL_Y, BAL_Y + 1.3, HZ1 - 3.6, HZ1 - 1.2, M_VELVET)
box("Canopy", -5.0, 5.0, BAL_Y + 7.8, BAL_Y + 8.6, HZ1 - 3.0, HZ1, M_VELVET)
box("Canopy fringe", -5.1, 5.1, BAL_Y + 7.5, BAL_Y + 7.8, HZ1 - 3.1, HZ1 - 2.9, M_GOLD)
player(-1.4, BAL_Y, BAL_Z0 + 1.6, 1)                                # arrived

# ---------------------------------------------------------------- the dance floor and the couples
for i in range(11):
    for j in range(17):
        x0, z0 = -20.0 + 3.0 * i, SCR + 0.2 + 3.0 * j
        if x0 < 11.6 and z0 < DZ:
            box("Dance square", x0, min(x0 + 3.0, 11.6), -0.02, 0.02, z0, min(z0 + 3.0, DZ), M_PARQUET if (i + j) % 2 else M_PARQUET2)
box("Dance border", -20.4, 11.8, -0.06, 0.005, SCR, DZ, M_GOLD)   # under the squares: only its edge shows
gown = frustum("ME_GownC", 1.8, 1.3, SY - 0.25)
cape = frustum("ME_CapeC", 1.6, 1.2, SY - 0.25)
for i in range(10):
    mid, q, kg, turn = L["couple"](i, T)
    for who, (x, z), me, mat in (("Queen", q, gown, M_GOWN), ("King", kg, cape, M_CAPE)):
        inst(me, who + " gown", mat, U(x, 0.25, z), (1, 1, 1))
        cyl(who + " hem", x, 0, 0.25, z, 3.8, M_GOLD)
        cyl(who + " shoulders", x, SY - 0.12, SY, z, 2.7, M_GOLD)
        ball(who + " head", x, SY + 0.7, z, 1.4, M_IVORY)
        cyl(who + " crown", x, SY + 1.25, SY + (2.3 if who == "King" else 1.75), z, 0.75, M_GOLD, ME_CYL8)
    bar("Joined arms", (q[0], SY - 0.35, q[1]), (kg[0], SY - 0.35, kg[1]), 0.45, M_IVORY)
    if i == 1:
        player(q[0] + 0.9, SY, q[1], 1)

# ---------------------------------------------------------------- the stage, the curtain, the orchestra, the pit
box("Stage", HX0, STAGE_X1, 0, SY - 0.1, STAGE_Z0, STAGE_Z1, M_WALNUT)
box("Stage floor", HX0, STAGE_X1, SY - 0.1, SY, STAGE_Z0, STAGE_Z1, M_PARQUET2)
box("Stage front trim", STAGE_X1 - 0.1, STAGE_X1 + 0.06, SY - 0.7, SY, STAGE_Z0, STAGE_Z1, M_GOLD)
for z in range(int(STAGE_Z0) + 2, int(STAGE_Z1), 3):
    box("Stage front panel", STAGE_X1, STAGE_X1 + 0.05, 1.0, SY - 1.2, z - 1.0, z + 1.0, M_CLOTH2)
# The rail and its pelmet run on 3.5 m past the curtain's open end, over the fountain's north-east: the fountain's own
# view (R833) leaves them out, a cutaway, or they would cover the top of the fountain and the loft's ladder.
PELMET = [bar("Curtain rail", (CURTAIN_X, RAIL, OPEN_S - 3.5), (CURTAIN_X, RAIL, STAGE_Z1 + 2.0), 0.3, M_GOLD),
          box("Pelmet", CURTAIN_X - 0.5, CURTAIN_X + 0.5, RAIL + 0.3, RAIL + 1.9, OPEN_S - 3.5, STAGE_Z1 + 2.0, M_VELVET2),
          box("Pelmet fringe", CURTAIN_X + 0.5, CURTAIN_X + 0.6, RAIL + 0.1, RAIL + 0.5, OPEN_S - 3.5, STAGE_Z1 + 2.0, M_GOLD)]
cyc = (T % 15.0)
op = 0.0 if cyc < 6 else (lambda k: k * k * (3 - 2 * k))(min(1.0, (cyc - 6) / 5.0)) if cyc < 11 else 1.0 if cyc < 13 else 1.0 - (lambda k: k * k * (3 - 2 * k))((cyc - 13) / 2.0)
for side in (-1, 1):
    for k in range(10):
        shut = CURTAIN_Z + side * (0.55 + PLEAT_W * k)
        opened = (STAGE_Z1 + 0.5 - 0.3 * k) if side > 0 else (OPEN_S - 0.3 * k)
        z = shut + (opened - shut) * op
        x = CURTAIN_X + (0.1 if k % 2 else -0.1)
        box("Pleat", x - 0.12, x + 0.12, SY + 0.3, RAIL, z - 0.55, z + 0.55, M_VELVET if k % 2 else M_VELVET2)
        box("Pleat fringe", x - 0.14, x + 0.14, SY + 0.15, SY + 0.35, z - 0.55, z + 0.55, M_GOLD)
        if side < 0 and k == 0:
            player(CURTAIN_X + 0.45, BASS_TOP + 0.6, z - 0.1, 0)
for (x, z) in ((-29.0, STAGE_Z0 + 4.0), (-29.0, STAGE_Z0 + 9.0), (-27.0, STAGE_Z0 + 14.0), (-29.0, STAGE_Z1 - 3.0)):
    figure("Musician", x, SY, z, 4.2, M_COAT, M_IVORY, 0.8)
    bar("Music stand", (x + 1.4, SY, z), (x + 1.4, SY + 2.6, z), 0.1, M_IRON)
cyl("Kettle drum", -30.0, SY, SY + 1.6, STAGE_Z1 - 6.5, 2.4, M_GOLD)
carpet([(STAGE_X1 - 0.9, CURTAIN_Z), (CURTAIN_X + 1.2, CURTAIN_Z - 0.5)], SY)
for s in (-1, 1):
    cyl("Alighting post", STAGE_X1 - 0.2, SY, SY + 1.8, CURTAIN_Z + s * 1.7, 0.3, M_GOLD, ME_CYL8)
    ball("Alighting lantern", STAGE_X1 - 0.2, SY + 2.1, CURTAIN_Z + s * 1.7, 0.6, M_LAMP)
box("Pit floor", HX0, -20.0, 0.0, 0.05, BUF[3], STAGE_Z0, M_PARQUET)
for z in BASS_Z:
    box("Bass body", BASS_X - 0.35, BASS_X + 0.35, 0, 7.0, z - 0.8, z + 0.8, M_WALNUT)
    bar("Bass neck", (BASS_X, 7.0, z), (BASS_X, BASS_TOP - 0.3, z), 0.28, M_EBONY)
    ball("Bass scroll", BASS_X, BASS_TOP - 0.2, z, 0.45, M_EBONY)
    figure("Bassist", -21.4, 0.0, z, 4.6, M_COAT, M_EBONY, 0.8)

# ---------------------------------------------------------------- the dessert buffet and the chocolate fountain
box("Buffet", BUF[0], BUF[1], SY - 0.5, SY, BUF[2], BUF[3], M_LINEN)
box("Buffet skirt", BUF[0], BUF[1] + 0.08, 0, SY - 0.5, BUF[2], BUF[3] + 0.08, M_CLOTH)
box("Buffet hem", BUF[0], BUF[1] + 0.12, SY - 0.75, SY - 0.45, BUF[2] - 0.04, BUF[3] + 0.12, M_GOLD)
cyl("Fountain bowl", FX, SY, BOWL_TOP, FZ, BOWL_R * 2, M_SILVER)
torus("Fountain bowl rim", FX, BOWL_TOP - 0.02, FZ, BOWL_R, 0.08, M_GOLD)
cyl("Chocolate pool", FX, BOWL_TOP - 0.05, BOWL_TOP + 0.01, FZ, BOWL_R * 2 - 0.3, M_CHOC)
# R828: the chocolate runs over each tier's rim and down the whole of its side - one sheet, no pale band, no ladders -
# with its ripples (ChocolateRipples) where the clock has them, here wavy as a liquid's; a lip where it curls over the
# rim; glossy streaks running down from the lip with a drop at the end of each, so a still picture shows it pouring.
def wavy_ring(name, x, y, z, R, r, amp, waves, phase, mat, seg=120, sides=8):
    bm = bmesh.new()
    rows = []
    for u in range(seg):
        a = 2 * math.pi * u / seg
        c = Vector((x + R * math.cos(a), z + R * math.sin(a), y + amp * math.sin(waves * a + phase)))
        rad = Vector((math.cos(a), math.sin(a), 0))
        rows.append([bm.verts.new(c + (rad * math.cos(2 * math.pi * v / sides) + Vector((0, 0, 1)) * math.sin(2 * math.pi * v / sides)) * r)
                     for v in range(sides)])
    for u in range(seg):
        a_, b_ = rows[u], rows[(u + 1) % seg]
        for v in range(sides):
            bm.faces.new((a_[v], b_[v], b_[(v + 1) % sides], a_[(v + 1) % sides]))
    o = bpy.data.objects.new(name, to_mesh(name, bm, mat))
    col.objects.link(o)
    return o


below = BOWL_TOP
for k in range(3):
    h, R = TIER_TOP[k] - below, TIER_R[k]
    cyl("Chocolate tier", FX, below, TIER_TOP[k], FZ, R * 2, M_CHOC)
    wavy_ring("Chocolate lip", FX, TIER_TOP[k] - 0.05, FZ, R + 0.02, 0.09, 0.025, 9, k, M_CHOC)
    for i in range(RIPPLES):
        under = (FLOW * T + i * h / RIPPLES) % h
        if 0.2 < under < h - 0.12:
            wavy_ring("Chocolate ripple", FX, TIER_TOP[k] - 0.08 - under, FZ, R + 0.02, 0.05, 0.05, 6 + k, 1.7 * i + k, M_CHOCWAVE)
    n = int(2 * math.pi * R / 0.42)
    for j in range(n):
        a = 2 * math.pi * (j + 0.37 * k + 0.3 * random.random()) / n
        sx, sz = FX + (R + 0.015) * math.cos(a), FZ + (R + 0.015) * math.sin(a)
        end = TIER_TOP[k] - 0.1 - h * (0.5 + 0.48 * random.random())
        w = 0.06 + 0.05 * random.random()
        bar("Chocolate streak", (sx, TIER_TOP[k] - 0.06, sz), (sx, end, sz), w, M_CHOCWAVE)
        ball("Chocolate drop", sx, end, sz, w + 0.06, M_CHOCWAVE)
    below = TIER_TOP[k]
cyl("Fountain column", FX, TIER_TOP[2], TIER_TOP[2] + 1.2, FZ, 0.7, M_SILVER)
inst(ME_CONE, "Fountain crown", M_GOLD, U(FX, TIER_TOP[2] + 1.7, FZ), (1.2, 1.2, 1.0))
for i in range(14):
    a = math.radians(i * 26 + 7)
    x, z = FX + 5.0 * math.sin(a), FZ + 5.0 * math.cos(a)
    if BUF[0] + 0.6 < x < BUF[1] - 0.6 and BUF[2] + 0.6 < z < BUF[3] - 0.6:
        ball("Strawberry", x, SY + 0.4, z, 0.8, M_BERRY)
        inst(ME_CONE, "Leaf", M_LEAF, U(x, SY + 0.75, z), (0.5, 0.5, 0.3))
for (x, z) in ((-30.6, 15.6), (-26.0, 15.8)):
    cyl("Cake stand", x, SY, SY + 0.9, z, 0.2, M_GOLD, ME_CYL8)
    for t2, r2 in ((0.9, 1.5), (1.4, 1.1), (1.85, 0.75)):
        cyl("Cake", x, SY + t2, SY + t2 + 0.45, z, r2, M_BERRY if t2 > 1.5 else M_PLATE)
carpet([(CURTAIN_X + 0.3, OPEN_S - 0.3), (FX, BUF[3] - 1.4), (FX, FZ + BOWL_R + 0.9)], SY)
# The lamplighters' ladder (R825; the hall's only ladder since R828): hung from the loft's edge on two hooks, in front of
# the fountain, its foot a metre under the fountain's top, 1.6 m from its south edge.
lz = SCR + LADDER_OUT + 0.05
for s in (-1, 1):
    bar("Ladder rail", (FX + s * 0.74, TIER_TOP[2] - 1.0, lz), (FX + s * 0.74, LY + 0.1, lz), 0.1, M_GOLDB)
    # A hook round the loft's edge (its face is 0.4 m deep): a ring from the rail's top back over the edge.
    torus("Ladder hook", FX + s * 0.74, LY + 0.1, (lz + SCR - 0.4) / 2, (lz - SCR + 0.4) / 2, 0.045, M_GOLDB, (0, math.pi / 2, 0))
y = TIER_TOP[2] - 0.7
while y < LY - 0.1:
    bar("Ladder rung", (FX - 0.74, y, lz), (FX + 0.74, y, lz), 0.07, M_GOLDB)
    y += 0.4
player(FX + 0.6, BOWL_TOP, FZ + TIER_R[0] + 0.45, 0)                # at the foot of the first tier
player(FX + 0.3, TIER_TOP[0] + 0.9, FZ + TIER_R[1] + 0.32, 0)       # climbing the second against the flow
player(FX - 0.9, TIER_TOP[0], FZ + TIER_R[1] + 0.6, 1)              # resting on the first tier's ring
player(FX + 0.1, 17.4, lz + 0.35, 1)                                # up the loft's ladder

# ---------------------------------------------------------------- the chandeliers
hanging = {l: hanging_meshes(l) for l in set(ROPE_L)}
for lane, x in enumerate(LANES):
    for k, l in enumerate(ROPE_L):
        z = SCR + ALONG[k]
        y = ROPE_LOW[k] + l * math.cos(math.radians(40))
        g = place_meshes(hanging[l], "Chandelier %d %d" % (lane, k), U(x, y, z))
        ang = math.radians(40 * math.sin(2 * math.pi * (T + ((k + lane) % 2) * 2.5) / 5.0))
        g.rotation_quaternion = Quaternion(Vector((1, 0, 0)), ang)
        cyl("Chandelier rod", x, y, CEIL, z, 0.09, M_GOLDB, ME_CYL8)
        ball("Chandelier swivel", x, y + 0.02, z, 0.24, M_GOLDB)
        end = Vector((0, l * math.sin(ang), -l * math.cos(ang)))
        mid = g.location + Vector((0, (l - 1.5) * math.sin(ang), -(l - 1.5) * math.cos(ang)))
        lamps.append((mid.x, mid.z, mid.y))
        if lane == 0 and k in (1, 4):
            hand = g.location + end
            player(hand.x, hand.z - 1.05, hand.y, 0 if k == 1 else 1)
great = place_meshes(great_meshes(CEIL - GREAT_Y - 4.6), "Great chandelier", U(0, GREAT_Y, GREAT_Z))
cyl("Great chandelier carpet", 0, GREAT_Y + 0.004, GREAT_Y + 0.02, GREAT_Z, GREAT_R * 2 - 0.5, M_OLIVE2)
for a in (0, 120, 240):
    lamps.append((2.0 * math.cos(math.radians(a)), GREAT_Y + 2.8, GREAT_Z + 2.0 * math.sin(math.radians(a))))
lamps.append((0, GREAT_Y - 0.6, GREAT_Z))
player(1.4, GREAT_Y, GREAT_Z - 1.2, 1)
player(-1.6, GREAT_Y, GREAT_Z + 0.8, 0)


# ---------------------------------------------------------------- light, camera, render
def light(name, kind, loc, energy, color, **kw):
    d = bpy.data.lights.new(name, kind)
    d.energy = energy
    d.color = color
    for k, v in kw.items():
        setattr(d, k, v)
    o = bpy.data.objects.new(name, d)
    col.objects.link(o)
    o.location = loc
    return o


def aim(o, target):
    o.rotation_euler = (Vector(target) - o.location).to_track_quat("-Z", "Y").to_euler()


world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.007, 0.004, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
key = light("LGT_Key", "SUN", U(70, 90, -30), 0.7, (1.0, 0.80, 0.56), angle=math.radians(12))
aim(key, U(0, 4, 36))
fill = light("LGT_Fill", "AREA", U(10, 60, 30), 22000, (1.0, 0.78, 0.55), size=70)
aim(fill, U(0, 0, 36))
for z in range(10, 66, 16):
    w = light("LGT_Window", "AREA", U(HX0 + 1.0, 18, z + 2.5), 5000, (1.0, 0.72, 0.42), size=6)
    aim(w, U(0, 4, z + 2.5))
for (x, y, z) in lamps:
    light("LGT_Candle", "POINT", U(x, y, z), 3200, (1.0, 0.56, 0.24), shadow_soft_size=0.5)
cam_d = bpy.data.cameras.new("CAM_Banquet")
cam_d.clip_end = 900
cam = bpy.data.objects.new("CAM_Banquet", cam_d)
col.objects.link(cam)
scene.camera = cam
scene.render.resolution_x = WIDTH
scene.render.resolution_y = HEIGHT
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
    try:
        scene.view_settings.look = look
        break
    except Exception:
        pass
scene.render.engine = "CYCLES"
prefs = bpy.context.preferences.addons["cycles"].preferences
prefs.compute_device_type = "OPTIX"
prefs.get_devices()
for d in prefs.devices:
    d.use = d.type in ("OPTIX",)
scene.cycles.device = "GPU"
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.max_bounces = 6
scene.view_settings.exposure = -0.2
scene.render.image_settings.file_format = "PNG"
print("OBJECTS", len(col.objects), "LAMPS", len(lamps))

# R818's two views, and R833's two of what R825 - R828 changed: the cloche lifts in their dumbwaiter from over the long
# table, and the chocolate fountain with the loft's ladder from over the dance floor's south-west corner.
shots = (("", 26, U(66, 60, -12), U(-6, 4, 36)),
         ("_chandeliers", 22, U(13, 25.5, 6.5), U(-2, 15, 44)),
         ("_cloche_lifts", 24, U(31.0, 15.6, 36.5), U(19.0, 11.2, 55.0)),
         ("_fountain", 20, U(-12.5, 19.0, 17.5), U(-28.4, 12.6, 6.6)))
ONLY = os.environ.get("BANQUET_SHOTS")   # e.g. "_cloche_lifts,_fountain": render only these
if ONLY:
    shots = tuple(s for s in shots if (s[0] or "hall") in ONLY.split(","))
for suffix, lens, eye, target in shots:
    for o in CLOSE_ONLY:
        o.hide_render = suffix not in ("_cloche_lifts", "_fountain")
    for o in PELMET:
        o.hide_render = suffix == "_fountain"
    cam_d.lens = lens
    cam.location = eye
    aim(cam, target)
    root, ext = os.path.splitext(OUT)
    scene.render.filepath = root + suffix + (ext or ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)

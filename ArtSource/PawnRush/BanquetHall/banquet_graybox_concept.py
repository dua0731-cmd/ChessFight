# The grand banquet hall as its grey box stands after R817 (the user's first run) and R818 (the chandeliers), dressed as
# a concept picture in the look of the place's concept art (Docs/PawnRush/ConceptArt/R787_BanquetHall): cream stone and
# walnut, gold, white banners with chess emblems, olive runners, red carpets along the way, candles, crown chandeliers.
# Two renders: the whole hall, and the chandeliers over the dance floor from near (written beside the first as
# <out>_chandeliers.png). A picture only: nothing here goes into Unity.
# The layout is GrandBanquetHallBuilder.cs's (section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y)),
# its numbers copied below; the chandeliers are BanquetChandelierShape.cs's, built the same way (turned profiles, rings,
# tubes). The machines are posed at one moment of the shared clock (T): the cloches up, the curtain opening, the top of
# the champagne tower running over, the couples where the clock has them.
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
T = 13.4

# ---------------------------------------------------------------- the grey box's numbers (GrandBanquetHallBuilder, R818)
TY, SY, DY, HY, LY, CEIL = 4.4, 7.4, 10.4, 14.8, 20.0, 34.0
HX0, HX1, HZ1, SCR, DZ = -32.0, 32.0, 72.0, 6.0, 56.0
TRAY_X, BODY_X, TRAY_Y, TRAY_R, STACK_R, STACK_TOP = 27.8, 29.8, 2.4, 1.5, 0.8, 5.0
WZ0, WLANE, WSPEED, NW = 0.5, 49.0, 2.2, 5
COUNTER = (22.0, 26.0, 4.0, 12.0)
KITCHEN_Z1, PANTRY_Z0 = 4.0, 43.0
T_X0, T_X1, T_Z0, CLOTH_Z1, RUN_X = 14.0, 26.0, 18.0, 50.0, 20.0
PLATES = (23.0, 29.0, 35.0, 41.0, 47.0)
CLOCHE_X, CLOCHE_Z, CLOCHE_R, BAND, DOME_R, LIFT = (17.0, 23.0), 52.9, 2.2, 1.4, 1.4, 5.6
CURTAIN_Z, STAGE_X1, CURTAIN_X, RAIL = 40.05, -20.0, -24.5, SY + 10.6
STAGE_Z0, STAGE_Z1, OPEN_S, PLEAT_W = CURTAIN_Z - 11.0, CURTAIN_Z + 9.0, 16.0, 1.05
BASS_X, BASS_TOP, BASS_Z = -23.95, 9.6, (19.0, CURTAIN_Z - 13.0)
BUF = (-32.0, -23.5, 3.0, 17.0)
FX, FZ, BOWL_R, BOWL_TOP = -27.7, 9.2, 4.2, 7.9
TIER_R, TIER_TOP, STILL = (3.3, 2.3, 1.3), (10.733, 13.567, 16.4), 1.3
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
TOWER_Z = LAND_Z + 0.6 + 4 * 1.9 * 0.5
LEVELS, PITCH, LEVEL_H = 4, 1.9, 1.5
BAL_Y = HY + LEVELS * LEVEL_H
LOFT_X0, LOFT_X1 = -31.0, 4.6
BOARD_X = -4.25
print("GREAT_Z %.3f LAND_Z %.3f TOWER_Z %.3f" % (GREAT_Z, LAND_Z, TOWER_Z))

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
M_COUPE = principled("MAT_Coupe", (0.95, 0.86, 0.55), 0.08, 0.0, (1.0, 0.82, 0.42), 0.25)
M_BALUSTER = principled("MAT_Baluster", (0.88, 0.84, 0.76), 0.35)
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
box("Pantry lintel", 26.0, HX1, 7.2, 7.8, PANTRY_Z0, PANTRY_Z0 + 0.3, M_WALNUT)
box("Pantry wall W", 25.7, 26.0, TY, 7.8, PANTRY_Z0, DZ, M_CREAM)
box("Pantry roof", 25.7, HX1, 7.5, 7.8, PANTRY_Z0, DZ, M_WALNUT)
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
lift = LIFT  # the clock's T has both cloches up
for x in CLOCHE_X:
    cyl("Platter", x, TY, TY + 0.06, CLOCHE_Z, CLOCHE_R * 2 + 0.2, M_SILVER)
    inst(ME_SPH, "Roast", M_ROAST, U(x, TY + 0.7, CLOCHE_Z), (2.6, 1.6, 1.4))
    y0 = TY + 0.06 + lift
    cyl("Cloche band", x, y0, y0 + BAND, CLOCHE_Z, CLOCHE_R * 2, M_CLOCHE)
    torus("Cloche rim", x, y0 + BAND, CLOCHE_Z, CLOCHE_R - 0.05, 0.06, M_GOLD)
    inst(ME_SPH, "Cloche dome", M_CLOCHE, U(x, y0 + BAND, CLOCHE_Z), (DOME_R * 2, DOME_R * 2, 3.2))
    ball("Cloche knob", x, y0 + BAND + 1.75, CLOCHE_Z, 0.5, M_GOLD)
    ball("Glove", x, y0 + BAND + 2.1, CLOCHE_Z, 0.95, M_GLOVE)
    bar("Sleeve", (x, y0 + BAND + 2.4, CLOCHE_Z), (x, y0 + BAND + 9.0, CLOCHE_Z - 4.0), 0.8, M_COAT)
player(CLOCHE_X[0] + 1.75, TY + 0.06 + lift + BAND, CLOCHE_Z + 0.4, 1)

# ---------------------------------------------------------------- the royal dais, the gallery, the head table, the tower, the balcony
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
tables = TOWER_Z - 3.5
box("Gallery end W", BOARD_X - opening - 1.7, BOARD_X - opening - 1.4, DY, DY + 2.6, DZ + 0.3, tables, M_WALNUT)
box("Gallery end E", 26.0, 26.3, DY, DY + 2.6, DZ + 0.3, tables + 0.3, M_WALNUT)
carpet([((CLOCHE_X[0] + CLOCHE_X[1]) / 2, DZ + 1.3), (BOARD_X, DZ + 1.3), (BOARD_X, DZ + 0.9)], DY)
for x0, x1 in ((-24.0, -6.0), (6.0, 24.0)):
    box("Head table", x0, x1, HY - 0.5, HY, tables, TOWER_Z + 2.5, M_LINEN)
    box("Head table skirt", x0, x1, DY, HY - 0.5, tables - 0.06, TOWER_Z + 2.5, M_CLOTH)
    box("Head table runner", x0 + 0.5, x1 - 0.5, HY, HY + 0.02, tables + 2.4, tables + 3.6, M_OLIVE)
    for x in (x0 + 3, (x0 + x1) / 2, x1 - 3):
        candelabra(x, HY, tables + 1.3, 1.3)
        bouquet(x + 2.2, HY, tables + 3.0, 1.0)
box("Pedestal", -5.0, 5.0, HY - 0.5, HY, TOWER_Z - 5.0, TOWER_Z + 5.0, M_LINEN)
box("Pedestal skirt", -5.0, 5.0, DY, HY - 0.5, TOWER_Z - 5.06, TOWER_Z + 5.0, M_CLOTH)
box("Pedestal hem", -5.1, 5.1, HY - 0.7, HY - 0.45, TOWER_Z - 5.1, TOWER_Z + 5.1, M_GOLD)
coupes = {}
u = (T - 1.0) % 4.0
for k in range(LEVELS):
    n, base = LEVELS - k, HY + LEVEL_H * k
    spill = 0.2 + 0.5 * (LEVELS - 1 - k) <= u < 0.2 + 0.5 * (LEVELS - 1 - k) + 1.6
    places = [((i - (n - 1) / 2) * PITCH, base, TOWER_Z + (j - (n - 1) / 2) * PITCH) for i in range(n) for j in range(n)]
    many("Coupe feet", P(0.55, 0.06, 0.55, 0.0), places, M_GLASS, 24)
    many("Coupe stems", P(0.06, 0.62, 0.07, 0.06), places, M_GLASS, 10)
    many("Coupes", P(0.98, 1.5, 0.92, 1.42, 0.6, 1.08, 0.2, 0.7, 0.07, 0.62), places, M_CHAMP if spill else M_COUPE, 24)
    if spill:
        half = n * PITCH / 2
        for (x0, x1, z0, z1) in ((-half, half, -half - 0.08, -half - 0.03), (-half, half, half + 0.03, half + 0.08), (-half - 0.08, -half - 0.03, -half, half), (half + 0.03, half + 0.08, -half, half)):
            box("Champagne sheet", x0, x1, base - 0.1, base + LEVEL_H, TOWER_Z + z0, TOWER_Z + z1, M_CHAMP)
bar("Bottle", (0, BAL_Y + 1.6, TOWER_Z + 2.6), (0, BAL_Y + 0.9, TOWER_Z + 0.6), 1.3, M_BOTTLE)
bar("Bottle label", (0, BAL_Y + 1.45, TOWER_Z + 2.15), (0, BAL_Y + 1.2, TOWER_Z + 1.4), 1.36, M_GOLD)
bar("Pour", (0, BAL_Y + 0.85, TOWER_Z + 0.5), (0, BAL_Y + 0.1, TOWER_Z + 0.1), 0.2, M_CHAMP)
bar("Sommelier sleeve", (0, BAL_Y + 1.5, TOWER_Z + 2.0), (0, 29.0, TOWER_Z + 6.0), 0.8, M_COAT)
ball("Sommelier glove", 0, BAL_Y + 1.5, TOWER_Z + 2.0, 0.9, M_GLOVE)
player(-1.2, HY + LEVEL_H * 2, TOWER_Z - 1.9 - 0.45, 0)
player(-2.4, HY, LAND_Z, 1)
box("Balcony", -6.0, 6.0, BAL_Y - 0.6, BAL_Y, TOWER_Z + 0.95, HZ1, M_DAISM)
box("Balcony fascia", -6.1, 6.1, BAL_Y - 0.9, BAL_Y - 0.5, TOWER_Z + 0.85, HZ1, M_GOLD)
for x in (-6.0, 5.7):
    box("Balcony rail", x, x + 0.3, BAL_Y + 0.95, BAL_Y + 1.1, TOWER_Z + 0.95, HZ1, M_GOLD)
    many("Balcony balusters", P(0.07, 0.95, 0.11, 0.85, 0.07, 0.6, 0.13, 0.3, 0.08, 0.06, 0.11, 0),
         [(x + 0.15, BAL_Y, z) for z in [TOWER_Z + 1.2 + 0.42 * i for i in range(int((HZ1 - TOWER_Z - 1.2) / 0.42))]], M_BALUSTER)
carpet([(0, TOWER_Z + 1.6), (0, HZ1 - 0.3)], BAL_Y)
for s in (-1, 1):
    box("Throne", s * 3.9 - 0.9, s * 3.9 + 0.9, BAL_Y, BAL_Y + 3.6, HZ1 - 2.4, HZ1 - 1.2, M_GOLD)
    box("Throne seat", s * 3.9 - 0.9, s * 3.9 + 0.9, BAL_Y, BAL_Y + 1.3, HZ1 - 3.4, HZ1 - 1.2, M_VELVET)
box("Canopy", -5.0, 5.0, BAL_Y + 7.8, BAL_Y + 8.6, HZ1 - 3.0, HZ1, M_VELVET)
box("Canopy fringe", -5.1, 5.1, BAL_Y + 7.5, BAL_Y + 7.8, HZ1 - 3.1, HZ1 - 2.9, M_GOLD)

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
bar("Curtain rail", (CURTAIN_X, RAIL, OPEN_S - 3.5), (CURTAIN_X, RAIL, STAGE_Z1 + 2.0), 0.3, M_GOLD)
box("Pelmet", CURTAIN_X - 0.5, CURTAIN_X + 0.5, RAIL + 0.3, RAIL + 1.9, OPEN_S - 3.5, STAGE_Z1 + 2.0, M_VELVET2)
box("Pelmet fringe", CURTAIN_X + 0.5, CURTAIN_X + 0.6, RAIL + 0.1, RAIL + 0.5, OPEN_S - 3.5, STAGE_Z1 + 2.0, M_GOLD)
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
cyl("Chocolate pool", FX, BOWL_TOP - 0.05, BOWL_TOP + 0.01, FZ, BOWL_R * 2 - 0.3, M_CHOC)
below = BOWL_TOP
for k in range(3):
    flow_top = TIER_TOP[k] - STILL
    cyl("Chocolate falling", FX, below, flow_top, FZ, TIER_R[k] * 2, M_CHOC)
    cyl("Chocolate still", FX, flow_top, TIER_TOP[k], FZ, TIER_R[k] * 2, M_CHOC2)
    torus("Tier rim", FX, TIER_TOP[k], FZ, TIER_R[k], 0.09, M_SILVER)
    for n in range(3):
        torus("Drip", FX, below + (flow_top - below) * (0.2 + 0.3 * n), FZ, TIER_R[k] + 0.03, 0.06, M_CHOC2)
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
player(FX - 1.0, TIER_TOP[0], FZ + TIER_R[0] - 0.4, 1)
player(FX + 0.3, BOWL_TOP, FZ + TIER_R[0] + 0.5, 0)

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
        if lane == 0 and k == 1:
            hand = g.location + end
            player(hand.x, hand.z - 1.05, hand.y, 0)
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

shots = (("", 26, U(66, 60, -12), U(-6, 4, 36)),
         ("_chandeliers", 22, U(13, 25.5, 6.5), U(-2, 15, 44)))
for suffix, lens, eye, target in shots:
    cam_d.lens = lens
    cam.location = eye
    aim(cam, target)
    root, ext = os.path.splitext(OUT)
    scene.render.filepath = root + suffix + (ext or ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)

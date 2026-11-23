# The grand banquet hall's draft (level design v0.3), stacked up from plain shapes: one hall with the musicians'
# gallery over the entry, a U of banquet tables round a chessboard dance floor (knights hopping, kings and queens
# waltzing), two lines of chandeliers down the middle, the cake on the head table and the royal balcony over it.
# Everything that moves is posed by pose(t), from banquet_layout.py's own functions, so the still below and the film
# (banquet_flow_video.py) show the same machine.
# A picture only: nothing here goes into Unity, and it is a blockout, not the concept art.
# Section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y). The numbers are banquet_layout.py's.
#
#   blender --background --factory-startup --python banquet_blockout.py -- <out.png> [samples] [width] [height]
#
# The box / cylinder / bar helpers are ../RoyalObservatory/observatory_dress_preview.py's: its head, up to its
# first geometry, is run here (it also reads the arguments and empties the scene).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler, Quaternion

_here = os.path.dirname(os.path.abspath(__file__))
_head = open(os.path.join(_here, "..", "RoyalObservatory", "observatory_dress_preview.py"), encoding="utf-8").read()
exec(compile(_head.split("# entry, ring court")[0], "observatory_dress_preview.py (head)", "exec"), globals())
L = {"__name__": "banquet_layout"}
exec(compile(open(os.path.join(_here, "banquet_layout.py"), encoding="utf-8").read(), "banquet_layout.py", "exec"), L)
D = math.radians
random.seed(7)

# ---------------------------------------------------------------- the banquet's colours
M_CLOTH = principled("MAT_Tablecloth", (0.74, 0.66, 0.48), 0.8)
M_CLOTH2 = principled("MAT_TableclothShade", (0.58, 0.48, 0.30), 0.8)
M_OLIVE = principled("MAT_Runner", (0.10, 0.19, 0.045), 0.75)
M_HEDGE = principled("MAT_Hedge", (0.07, 0.16, 0.05), 0.8)
M_WALNUT = principled("MAT_Walnut", (0.20, 0.10, 0.045), 0.45)
M_WARMSTONE = principled("MAT_WarmStone", (0.42, 0.33, 0.23), 0.7)
M_TILE = principled("MAT_KitchenTile", (0.045, 0.035, 0.03), 0.55)
M_PARQUET = principled("MAT_Parquet", (0.30, 0.17, 0.08), 0.35)
M_PARQUET2 = principled("MAT_ParquetPale", (0.52, 0.38, 0.22), 0.35)
M_IRON = principled("MAT_Iron", (0.05, 0.05, 0.055), 0.4, 0.8)
M_FIRE = principled("MAT_Fire", (0.62, 0.11, 0.02), 0.35, 0.0, (1.0, 0.16, 0.02), 0.55)
M_WAX = principled("MAT_Wax", (0.84, 0.78, 0.62), 0.45)
M_DRIP = principled("MAT_WaxDrip", (0.95, 0.90, 0.72), 0.3, 0.0, (1.0, 0.75, 0.4), 0.8)
M_FLAME = principled("MAT_Flame", (1, 0.8, 0.4), 0.3, 0.0, (1.0, 0.62, 0.22), 30.0)
M_SPONGE = principled("MAT_Cake", (0.86, 0.74, 0.60), 0.6)
M_CREAMSTRIPE = principled("MAT_Cream", (0.86, 0.30, 0.10), 0.5)
M_FONDANT = principled("MAT_Fondant", (0.42, 0.56, 0.68), 0.25)
M_CRYSTAL = principled("MAT_Crystal", (1.0, 0.86, 0.52), 0.12, 0.6, (1.0, 0.72, 0.36), 0.6)
M_WINE = principled("MAT_Goblet", (0.34, 0.20, 0.52), 0.1, 0.3)
M_SILVER = principled("MAT_Silver", (0.80, 0.82, 0.84), 0.18, 1.0)
M_PLATE = principled("MAT_Plate", (0.78, 0.76, 0.70), 0.25)
M_GOWN = principled("MAT_QueenGown", (0.80, 0.36, 0.16), 0.6)
M_CAPE = principled("MAT_KingCape", (0.30, 0.34, 0.42), 0.6)
M_IVORY = principled("MAT_Ivory", (0.80, 0.74, 0.60), 0.35)
M_EBONY = principled("MAT_Ebony", (0.07, 0.06, 0.06), 0.3)
M_DRAPE = principled("MAT_KnightDrape", (0.78, 0.40, 0.14), 0.6)
M_STRING = principled("MAT_HarpString", (0.95, 0.70, 0.25), 0.3, 0.6, (1.0, 0.6, 0.2), 0.6)
M_WARN = principled("MAT_Warning", (0.55, 0.30, 0.85), 0.4, 0.0, (0.6, 0.3, 1.0), 2.5)
M_HIT = principled("MAT_Hit", (0.9, 0.12, 0.08), 0.4, 0.0, (1.0, 0.15, 0.08), 4.0)
M_MARK_UP = principled("MAT_ClothUp", (0.95, 0.62, 0.18), 0.4, 0.0, (1.0, 0.6, 0.15), 1.2)
M_MARK_DOWN = principled("MAT_ClothDown", (0.25, 0.30, 0.36), 0.6)
M_HERO = principled("MAT_HeroMark", (1.0, 0.9, 0.2), 0.4, 0.0, (1.0, 0.85, 0.1), 6.0)

TABLE, STAGE_Y, LOFT_Y = L["TABLE"], L["STAGE_Y"], L["LOFT_Y"]
HALL, STAGE, LOFT, FLOOR = L["HALL"], L["STAGE"], L["LOFT"], L["FLOOR"]
AI, AO, AZ0, AX = L["ARM_IN"], L["ARM_OUT"], L["ARM_Z0"], L["ARM_X"]
HZ0, HZ1, HEADZ = L["HEAD_Z0"], L["HEAD_Z1"], L["HEAD_Z"]
CK, TIERS = L["CAKE"], L["TIERS"]
WALL_N = L["WALL_N"]
HEAD = L["heading"](L["ROPE_HEAD"])


def frustum(name, r0, r1, h, segments=36):
    # A cone with its point cut off, standing on z = 0.
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=r0, radius2=r1, depth=h)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, h / 2))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = abs(p.normal.z) < 0.9
    me.materials.append(None)
    return me


def group(name, at=(0.0, 0.0, 0.0)):
    e = bpy.data.objects.new(name, None)
    col.objects.link(e)
    e.location = U(*at)
    return e


def into(parent, *made):
    # What was just made, at its place in the parent's own frame.
    for o in made:
        o.parent = parent
    return parent


def panel(name, c, r, az, y0, y1, width, mat, depth=0.08):
    # A flat piece standing on a circle round c, facing out at heading az.
    x, z = c[0] + r * math.sin(D(az)), c[1] + r * math.cos(D(az))
    return inst(ME_CUBE, name, mat, U(x, (y0 + y1) / 2, z), (width, depth, y1 - y0), (0, 0, -D(az)))


def flame(x, y, z, size=0.5, lit=True):
    o = ball("Flame", x, y + size * 0.6, z, size, M_FLAME)
    if lit:
        lamps.append((x, y + size * 1.4, z))
    return o


def piece(x, y, z, team, parent=None, scale=1.0):
    # A pawn (the players): returned as its parts, so it can ride something.
    parts = [cyl("Pawn skirt", x, y, y + 0.55 * scale, z, 0.62 * scale, M_WHITE),
             inst(ME_CONE, "Pawn body", M_WHITE, U(x, y + 0.6 * scale, z), (0.6 * scale, 0.6 * scale, 0.7 * scale)),
             ball("Pawn head", x, y + 0.92 * scale, z, 0.42 * scale, M_WHITE),
             cyl("Pawn scarf", x, y + 0.66 * scale, y + 0.8 * scale, z, 0.4 * scale, M_RED if team else M_BLUE)]
    if parent is not None:
        into(parent, *parts)
    return parts


# ---------------------------------------------------------------- the hall: its west and north walls standing, the others kerbs
box("Hall floor", HALL[0] - 2, HALL[1] + 2, -0.6, 0, -4, WALL_N + 2, M_TILE)
box("Hall wall W", HALL[0] - 1.2, HALL[0], 0, 30, 0, WALL_N, M_WARMSTONE)
box("Hall wall N w", HALL[0], L["BALCONY"][0], 0, 30, WALL_N, WALL_N + 1.2, M_WARMSTONE)
box("Hall wall N e", L["BALCONY"][1], HALL[1], 0, 30, WALL_N, WALL_N + 1.2, M_WARMSTONE)
box("Hall wall N low", L["BALCONY"][0], L["BALCONY"][1], 0, L["BALCONY_Y"] - 0.8, WALL_N, WALL_N + 1.2, M_WARMSTONE)
box("Hall kerb E", HALL[1], HALL[1] + 0.8, 0, 1.0, 0, WALL_N, M_WARMSTONE)
box("Hall kerb S w", HALL[0], -8, 0, 1.0, -0.8, 0, M_WARMSTONE)
box("Hall kerb S e", 8, HALL[1], 0, 1.0, -0.8, 0, M_WARMSTONE)
for z in range(6, 66, 11):
    box("Window W", HALL[0], HALL[0] + 0.1, 10, 24, z, z + 4.5, M_DOOR)
    box("Banner W", HALL[0] + 0.1, HALL[0] + 0.25, 11, 23, z + 6.0, z + 8.8, M_OLIVE)
    box("Banner W mark", HALL[0] + 0.25, HALL[0] + 0.32, 16, 19.5, z + 6.8, z + 8.0, M_GOLD)
for x in (-27, -17, 12.5, 22.5):
    box("Window N", x, x + 4.5, 10, 24, WALL_N - 0.1, WALL_N, M_DOOR)
for x in (-21.5, 18.2):
    box("Banner N", x, x + 3.2, 11, 24, WALL_N - 0.25, WALL_N - 0.1, M_OLIVE)
for z in (22, 44):
    box("Fireplace", HALL[0], HALL[0] + 1.6, 0, 6.5, z, z + 7, M_WARMSTONE)
    box("Fireplace fire", HALL[0] + 1.55, HALL[0] + 1.7, 0.4, 3.6, z + 1.4, z + 5.6, M_FIRE)

# ---------------------------------------------------------------- the gallery over the entry: the servery under it, the stage, the loft
box("Stage slab", STAGE[0], STAGE[1], L["GALLERY_UNDER"], STAGE_Y, LOFT[2], STAGE[3], M_WARMSTONE)
box("Stage carpet", STAGE[0] + 1, STAGE[1] - 1, STAGE_Y, STAGE_Y + 0.04, STAGE[2] + 0.4, STAGE[3] - 0.5, M_OLIVE)
box("Stage front", STAGE[0], STAGE[1], STAGE_Y - 0.6, STAGE_Y + 0.02, STAGE[3], STAGE[3] + 0.12, M_GOLD)
box("Servery front wall", STAGE[0], STAGE[1], 0, L["GALLERY_UNDER"], STAGE[3] - L["SERVERY_BACK"] - 0.5, STAGE[3] - L["SERVERY_BACK"], M_WARMSTONE)
box("Loft", LOFT[0], LOFT[1], STAGE_Y, LOFT_Y, LOFT[2], LOFT[3], M_WALNUT)
box("Loft floor", LOFT[0], LOFT[1], LOFT_Y - 0.1, LOFT_Y, LOFT[2], LOFT[3], M_CLOTH)
for x in (-11, -4.5, 4.5, 11):
    for z in (3.0, 11.0):
        box("Servery pillar", x - 0.5, x + 0.5, 0, L["GALLERY_UNDER"], z - 0.5, z + 0.5, M_WALNUT)
for (x, z) in ((-10, 13.6), (-3, 14.0), (5.5, 13.4), (10.5, 6.0), (-9.5, 6.4)):
    box("Cart", x, x + 3, 0, 1.1, z, z + 1.8, M_WALNUT)
    for k in range(3):
        ball("Loaf", x + 0.6 + k * 0.9, 1.35, z + 0.9, 0.8, M_WARMSTONE)
box("Entry carpet", -2, 2, 0, 0.05, -3, 3, M_OLIVE)
HZ, STRINGS = L["HARP_Z"], L["STRINGS"]
STRING = []
for i, (x, top) in enumerate(STRINGS):
    STRING.append((bar("Harp string", (x, STAGE_Y + 0.3, HZ), (x, top, HZ), 0.12, M_STRING), i))
n0 = (STRINGS[0][0] - 0.9, STRINGS[0][1] - 0.22 + L["NECK"] / 2, (LOFT[3] + HZ) / 2)
n1 = (STRINGS[8][0] + 0.9, LOFT_Y - L["NECK"] / 2, (LOFT[3] + HZ) / 2)
inst(ME_CUBE, "Harp neck", M_GOLD, U((n0[0] + n1[0]) / 2, (n0[1] + n1[1]) / 2, n0[2]), (math.hypot(n1[0] - n0[0], n1[1] - n0[1]), HZ - LOFT[3], L["NECK"]),
     (0.0, -math.atan2(n1[1] - n0[1], n1[0] - n0[0]), 0.0))
box("Harp sound box", STRINGS[0][0] - 1.0, STRINGS[8][0] + 1.0, STAGE_Y, STAGE_Y + 0.35, HZ - 0.5, HZ + 0.5, M_WALNUT)
bar("Harp back", (STRINGS[0][0] - 0.9, STAGE_Y, HZ - 0.6), (n0[0], n0[1], HZ - 0.6), 0.7, M_GOLD)
cyl("Harpist", STRINGS[0][0] - 2.2, STAGE_Y, STAGE_Y + 3.4, HZ + 1.6, 1.7, M_EBONY)
ball("Harpist head", STRINGS[0][0] - 2.2, STAGE_Y + 4.0, HZ + 1.6, 1.3, M_EBONY)
for x in (6.0, 9.5):
    cyl("Musician", x, STAGE_Y, STAGE_Y + 2.6, 13.2, 1.5, M_IVORY)
    ball("Musician head", x, STAGE_Y + 3.1, 13.2, 1.1, M_IVORY)
WAX = []
for ci, c in enumerate(L["CANDLES"]):
    cyl("Candle dish", c[0], LOFT_Y, LOFT_Y + 0.2, c[1], L["DISH_R"] * 2, M_GOLD)
    cyl("Candle", c[0], LOFT_Y + 0.2, L["CANDLE_TOP"], c[1], L["CANDLE_R"] * 2, M_WAX)
    flame(c[0], L["CANDLE_TOP"] + 0.9, c[1], 0.6)
    cyl("Candle wick stand", c[0], L["CANDLE_TOP"], L["CANDLE_TOP"] + 0.9, c[1], 0.12, M_IRON, ME_CYL8)
    WAX.append((ci, c, [inst(ME_CUBE, "Wax drip", M_DRIP, U(0, -40, 0), (0.75, 0.24, 0.6)) for _ in range(3)]))

# ---------------------------------------------------------------- 1, 2: the U of tables, the cloth lanes, the runner, the goblets
CLOTH, GOBLETS = [], []
for side in (-1, 1):
    x0, x1 = sorted((side * AI, side * AO))
    box("Table arm", x0, x1, TABLE - 0.5, TABLE, AZ0, HZ1, M_CLOTH)
    box("Table arm skirt in", side * AI - 0.1, side * AI + 0.1, 0, TABLE - 0.5, STAGE[3], HZ0, M_CLOTH2)
    for z in (AZ0 + 2, 18, 32, 46):
        box("Table leg", side * (AO - 1.6) - 0.5, side * (AO - 1.6) + 0.5, 0, TABLE - 0.5, z - 0.5, z + 0.5, M_WALNUT)
    box("Hedge", side * AI - 0.25, side * AI + 0.25, TABLE, TABLE + L["FENCE"], AZ0, HZ0, M_HEDGE)
    xh0, xh1 = sorted((side * AI, side * 11.4))
    box("Hedge head", xh0, xh1, TABLE, TABLE + L["FENCE"], HZ0 - 0.25, HZ0 + 0.25, M_HEDGE)
    xr0, xr1 = sorted((side * (AX - L["RUNNER"]), side * (AX + L["RUNNER"])))
    box("Runner", xr0, xr1, TABLE, TABLE + 0.04, AZ0 + 1.5, HEADZ + L["RUNNER"], M_OLIVE)
    xr0, xr1 = sorted((side * (AX - L["RUNNER"]), side * (L["RUN_END"] - 0.6)))
    box("Runner head", xr0, xr1, TABLE, TABLE + 0.04, HEADZ - L["RUNNER"], HEADZ + L["RUNNER"], M_OLIVE)
    a = L["KNIGHT_SQUARES"][0]
    box("Knight square on the table", -side * a[0] - 1.5, -side * a[0] + 1.5, TABLE, TABLE + 0.05, a[2] - 1.5, a[2] + 1.5, M_WARN)
    for i in range(L["CLOTH_LANES"]):
        xa, xb = sorted((side * AI + side * i * L["LANE_WIDTH"], side * AI + side * (i + 1) * L["LANE_WIDTH"]))
        box("Cloth lane", xa + 0.05, xb - 0.05, 0.15, TABLE, AZ0 - 0.14, AZ0, M_CLOTH if i % 2 == 0 else M_CLOTH2)
        g = group("Cloth marks", ((xa + xb) / 2, 0.0, AZ0 - 0.155))
        marks = [box("Cloth lane mark", -0.5, 0.5, 0.5 + k * 0.95, 0.66 + k * 0.95, -0.02, 0.02, M_MARK_UP) for k in range(4)]
        into(g, *marks)
        CLOTH.append((g, i, marks, (xa + xb) / 2))
    GT, GR = L["GOBLET_TALL"], L["GOBLET_R"]
    for pair, (mid, head) in enumerate(L["goblet_spots"](side)):
        pg = group("Goblet pair", (mid[0], TABLE, mid[1]))
        pg.rotation_euler = (0.0, 0.0, -D(head))
        for s in (-1, 1):
            g = group("Goblet", (s * L["GOBLET_X"], 0.0, 0.0))
            g.parent = pg
            into(g, cyl("Goblet foot", 0, 0, 0.15, 0, 1.5, M_WINE), cyl("Goblet stem", 0, 0.15, GT - GR * 0.8, 0, 0.32, M_WINE, ME_CYL8),
                 ball("Goblet bowl", 0, GT, 0, GR * 2, M_WINE))
            GOBLETS.append((g, pair, s))
        for d in (-3.2, 3.2):
            for s in (-1, 1):
                into(pg, cyl("Plate", s * 4.6, 0, 0.08, d, 1.9, M_PLATE), cyl("Plate rim", s * 4.6, 0.08, 0.1, d, 1.3, M_GOLD))
box("Head table", -AO, AO, TABLE - 0.5, TABLE, HZ0, HZ1, M_CLOTH)
box("Head table skirt", -AI, AI, 0, TABLE - 0.5, HZ0 - 0.1, HZ0 + 0.1, M_CLOTH2)
for x in (-22, 22):
    box("Head table leg", x - 0.5, x + 0.5, 0, TABLE - 0.5, HZ1 - 2.0, HZ1 - 1.0, M_WALNUT)

# ---------------------------------------------------------------- 3, 4: the dance floor, the knights, the dais, the couples
n = int(round((FLOOR[1] - FLOOR[0]) / L["SQUARE"]))
wide = (FLOOR[1] - FLOOR[0]) / n
for i in range(n):
    for j in range(int((FLOOR[3] - FLOOR[2]) / L["SQUARE"]) + 1):
        z0 = FLOOR[2] + j * L["SQUARE"]
        box("Dance square", FLOOR[0] + i * wide, FLOOR[0] + (i + 1) * wide, 0, 0.05, z0, min(z0 + 3.0, FLOOR[3]), M_PARQUET if (i + j) % 2 else M_PARQUET2)
ISL = L["ISLAND"]
box("Dais", ISL[0], ISL[1], TABLE - 0.5, TABLE, ISL[2], ISL[3], M_CLOTH)
box("Dais edge", ISL[0] - 0.1, ISL[1] + 0.1, TABLE - 0.75, TABLE - 0.5, ISL[2] - 0.1, ISL[3] + 0.1, M_GOLD)
box("Dais post", -4.0, 4.0, 0, TABLE - 0.75, ISL[2] + 1.5, ISL[3] - 1.5, M_WALNUT)
for sq in L["KNIGHT_SQUARES"]:
    for s in (-1, 1):
        box("Knight square", sq[0] * s - 1.5, sq[0] * s + 1.5, sq[1] - (0.4 if sq[1] < 1 else 0.0), sq[1] + 0.04, sq[2] - 1.5, sq[2] + 1.5, M_WARN)
KR, KD, KT = L["KNIGHT_R"], L["KNIGHT_DRAPE"], L["KNIGHT_TALL"]
ME_KBODY = frustum("ME_KnightBody", KR + 0.05, KR - 0.25, KD)
KNIGHTS = []
for k in range(4):
    g = group("Knight %d" % k)
    dark = k % 2 == 1
    into(g, cyl("Knight base", 0, 0, 0.35, 0, (KR + 0.35) * 2, M_EBONY if dark else M_IVORY),
         inst(ME_KBODY, "Knight drape", M_DRAPE, U(0, 0.3, 0), (1, 1, 1)),
         torus("Knight hem", 0, 0.5, 0, KR + 0.06, 0.1, M_GOLD), torus("Knight collar", 0, KD + 0.25, 0, KR - 0.2, 0.14, M_GOLD),
         inst(ME_CUBE, "Knight neck", M_EBONY if dark else M_IVORY, U(0, KD + 0.9, 0.15), (1.1, 1.3, 1.9), (D(-18), 0, 0)),
         inst(ME_CUBE, "Knight head", M_EBONY if dark else M_IVORY, U(0, KT - 0.5, 0.75), (0.95, 2.0, 0.95), (D(24), 0, 0)),
         inst(ME_CUBE, "Knight mane", M_GOLD, U(0, KD + 1.2, -0.52), (0.3, 0.3, 2.0), (D(-18), 0, 0)))
    if k != 0:
        piece(0.0, 1.2, -KR - 0.34, k % 2, g)
    KNIGHTS.append(g)
GF, GS, SY, PAIR = L["GOWN_FOOT"], L["GOWN_SHOULDER"], L["SHOULDER_Y"], L["PAIR"]
ME_GOWN = frustum("ME_Gown", GF, GS, SY - 0.25)
COUPLES = []
for i in range(L["COUPLES"]):
    g = group("Couple %d" % i)
    for who, z, mat in (("Queen", -PAIR, M_GOWN), ("King", PAIR, M_CAPE)):
        into(g, inst(ME_GOWN, who + " gown", mat, U(0, 0.25, z), (1, 1, 1)), cyl(who + " foot", 0, 0, 0.25, z, GF * 2 + 0.2, M_GOLD),
             cyl(who + " shoulders", 0, SY - 0.12, SY, z, GS * 2 + 0.1, M_GOLD), ball(who + " head", 0, SY + 0.7, z, L["HEAD_R"] * 2 + 0.3, M_IVORY),
             cyl(who + " crown", 0, SY + 1.25, SY + (2.3 if who == "King" else 1.75), z, 0.75, M_GOLD, ME_CYL8))
    into(g, bar("Joined arms", (0, SY - 0.35, -PAIR + GS), (0, SY - 0.35, PAIR - GS), 0.45, M_IVORY))
    if i != 0:
        piece(0.0, SY, -PAIR - 0.85, i % 2, g)
    COUPLES.append(g)

# ---------------------------------------------------------------- 7: the two lines of chandeliers down the middle of the hall
RL, WAT, WR = L["ROPE_L"], L["WHEEL_AT"], L["WHEEL_R"]
ROPE_AXIS = Vector((HEAD[1], -HEAD[0], 0.0))          # the swing's axle, level, square to the way the lines lead
ROPES = []
for lane in (0, 1):
    for k in range(5):
        px, py, pz = L["rope_pivot"](lane, k)
        bar("Ceiling chain", (px, py, pz), (px, L["CEILING"], pz), 0.14, M_BRASS)
        g = group("Chandelier %d %d" % (lane, k), (px, py, pz))
        g.rotation_mode = "QUATERNION"
        into(g, bar("Chandelier line", (0, 0, 0), (0, -RL, 0), 0.16, M_BRASS), cyl("Chandelier wheel", 0, -WAT - 0.12, -WAT + 0.12, 0, WR * 2, M_GOLD),
             torus("Chandelier rim", 0, -WAT, 0, WR, 0.12, M_CRYSTAL), bar("Chandelier tassel", (0, -WAT, 0), (0, -RL, 0), 0.3, M_CRYSTAL),
             ball("Chandelier end", 0, -RL, 0, 0.55, M_GOLD))
        for a in range(0, 360, 60):
            into(g, flame((WR - 0.2) * math.sin(D(a)), -WAT + 0.12, (WR - 0.2) * math.cos(D(a)), 0.26, lit=False))
        if lane == 0 and k % 2 == 0:
            piece(0.3, -RL - 0.5, 0.0, k // 2 % 2, g)
        ROPES.append((g, lane, k))
        if lane == 0:
            lamps.append((0.0, py - WAT - 1.0, pz))

# ---------------------------------------------------------------- 8: the cake on the head table, every tier on its own turntable
cyl("Cake table", CK[0], TABLE - 0.5, TABLE, CK[1], L["CAKE_TABLE_R"] * 2, M_CLOTH)
cyl("Cake table skirt", CK[0], 0, TABLE - 0.5, CK[1], L["CAKE_TABLE_R"] * 2 - 0.3, M_CLOTH2)
cyl("Cake stand", CK[0], TABLE, TABLE + 0.25, CK[1], TIERS[0][0] * 2 + 1.0, M_GOLD)
TIER = []
bottom = TABLE + 0.25
for i, (r, top, speed) in enumerate(TIERS):
    g = group("Cake tier %d" % (i + 1), (CK[0], 0.0, CK[1]))
    into(g, cyl("Cake tier", 0, bottom, top, 0, r * 2, M_SPONGE), torus("Tier piping", 0, top, 0, r - 0.05, 0.11, M_PLATE))
    count, degrees = L["CREAM"][i]
    step = 10
    for a in range(0, 360, step):
        cream = count > 0 and ((a + step / 2 - 17.0 * i) % (360.0 / count)) < degrees
        into(g, panel("Cream" if cream else "Fondant", (0.0, 0.0), r + 0.03, a + step / 2, bottom + 0.12, top - 0.12, 2 * r * math.tan(D(step / 2)) + 0.03,
                      M_CREAMSTRIPE if cream else M_FONDANT))
    if i == 0:
        into(g, torus("Ledge rail", 0, top + 1.0, 0, r - 0.15, 0.07, M_GOLD))
        for a in range(0, 360, 20):
            into(g, cyl("Ledge rail post", (r - 0.15) * math.sin(D(a)), top, top + 1.0, (r - 0.15) * math.cos(D(a)), 0.1, M_GOLD, ME_CYL8))
    if 0 < i < 4:
        piece((r + TIERS[i + 1][0]) / 2 * math.sin(D(200 + 70 * i)), top, (r + TIERS[i + 1][0]) / 2 * math.cos(D(200 + 70 * i)), i % 2, g)
    TIER.append(g)
    bottom = top
cyl("Cake crown", CK[0] - 0.9, bottom, L["TOPPER"][1] - 0.9, CK[1] - 0.6, L["TOPPER"][0] * 2, M_GOLD)
flame(CK[0] - 0.9, L["TOPPER"][1] - 0.9, CK[1] - 0.6, 0.6)

# ---------------------------------------------------------------- 9: the cake knife and the royal balcony over the head of the hall
K0, K1 = L["KNIFE"]
BY, BAL, EXIT = L["BALCONY_Y"], L["BALCONY"], L["EXIT"]
box("Knife back", K0[0] - L["KNIFE_WIDTH"] / 2, K0[0] + L["KNIFE_WIDTH"] / 2, TIERS[4][1] - 0.15, TIERS[4][1] + 0.06, CK[1] - 1.2, K1[1] - 0.6, M_SILVER)
box("Knife blade", K0[0] - 0.12, K0[0] + 0.12, TIERS[3][1] + 0.1, TIERS[4][1] - 0.1, CK[1] - 1.0, CK[1] + 7.0, M_SILVER)
bar("Knife handle", (K0[0], TIERS[4][1] - 0.05, K1[1] - 0.8), (K0[0], BY + 0.3, K1[1] + 2.6), 1.0, M_WALNUT)
box("Balcony", BAL[0], BAL[1], BY - 0.8, BY, BAL[2], BAL[3], M_WARMSTONE)
box("Balcony carpet", EXIT[0] - 2, EXIT[0] + 2, BY, BY + 0.04, BAL[2] + 1, BAL[3], M_OLIVE)
for x in (BAL[0], BAL[1] - 0.3):
    box("Balcony rail", x, x + 0.3, BY, BY + 1.1, BAL[2], BAL[3], M_WALNUT)
box("Balcony floor under", BAL[0], BAL[1], 0, BY - 0.8, BAL[2] + 1.2, BAL[3], M_WARMSTONE)
box("Exit arch", EXIT[0] - 4, EXIT[0] + 4, BY, BY + 7, BAL[3] - 0.1, BAL[3] + 0.2, M_DOOR)
box("Exit wall", BAL[0], BAL[1], BY + 7, 30, BAL[3], BAL[3] + 1.2, M_WARMSTONE)
box("Canopy", EXIT[0] - 5, EXIT[0] + 5, BY + 7.2, BY + 8.2, BAL[3] - 3.5, BAL[3], M_OLIVE)
for s in (-1, 1):
    box("Throne", EXIT[0] + s * 4.0 - 0.8, EXIT[0] + s * 4.0 + 0.8, BY, BY + 3.4, BAL[2] + 2.0, BAL[2] + 3.2, M_GOLD)

# ---------------------------------------------------------------- the pieces standing about
A0 = L["KNIGHT_SQUARES"][0]
for i, (x, y, z) in enumerate(((0.5, 0, 1.5), (-6, 0, 2.2), (9, 0, 2.6), (-17, 0, 2.4), (23, 0, 2.8), (-21, TABLE, 9), (-19.2, TABLE, 16), (-20.6, TABLE, 24), (-19.4, TABLE, 33),
                               (-20.4, TABLE, 41), (20.5, TABLE, 8), (19.3, TABLE, 15), (20.8, TABLE, 23.5), (19.6, TABLE, 30), (20.4, TABLE, 39), (-17, TABLE, HEADZ + 0.6),
                               (15.5, TABLE, HEADZ - 0.5), (11.0, TABLE, HEADZ + 0.4), (2.0, TABLE, ISL[2] + 2.4), (-1.5, TABLE, ISL[2] + 4.2), (5.0, STAGE_Y, STAGE[3] - 1.4),
                               (-3.0, STAGE_Y, STAGE[3] - 2.6), (6.5, LOFT_Y, 4.0), (-9.0, LOFT_Y, 6.5), (EXIT[0] - 1.0, BY, BAL[2] + 4.0), (EXIT[0] + 1.4, BY, BAL[2] + 8.5),
                               (K0[0], TIERS[4][1] + 0.06, CK[1] + 6.0), (-8.5, 0, 28), (9.0, 0, 31), (-11.5, 0, 43))):
    piece(x, y, z, i % 2)


# ---------------------------------------------------------------- where everything is at time t
def cloth_shift(lane, t):
    # How far that lane's cloth has run since t = 0, metres up.
    up, hold, down, hold2 = L["CLOTH_CYCLE"]
    whole = sum(L["CLOTH_CYCLE"])
    v = t + lane * whole / 3.0
    n, u = math.floor(v / whole), v % whole
    net = up * L["CLOTH_UP"] - down * L["CLOTH_DOWN"]
    if u < up:
        d = u * L["CLOTH_UP"]
    elif u < up + hold:
        d = up * L["CLOTH_UP"]
    elif u < up + hold + down:
        d = up * L["CLOTH_UP"] - (u - up - hold) * L["CLOTH_DOWN"]
    else:
        d = net
    return n * net + d


def pose(t):
    for g, lane, marks, x in CLOTH:
        g.location = U(x, cloth_shift(lane, t) % 0.95, AZ0 - 0.155)
        speed = L["cloth_speed"](lane, t)
        for m in marks:
            m.material_slots[0].material = M_MARK_UP if speed > 0 else M_MARK_DOWN if speed < 0 else M_CLOTH
    for g, pair, s in GOBLETS:
        g.rotation_euler = (0.0, -s * D(L["GOBLET_LEAN"]) * L["goblet_lean"](pair, t), 0.0)
    for k, g in enumerate(KNIGHTS):
        x, y, z, yaw, air = L["knight"](k, t)
        g.location = U(x, y, z)
        g.rotation_euler = (0.0, 0.0, -D(yaw))
    for i, g in enumerate(COUPLES):
        mid, q, king, turn = L["couple"](i, t)
        g.location = U(mid[0], 0.0, mid[1])
        g.rotation_euler = (0.0, 0.0, D(turn))
    for o, i in STRING:
        u = L["pluck"](i, t)
        hit, warn = u < L["PLUCK_DEAD"], u > L["PLUCK_EVERY"] - L["PLUCK_WARN"]
        o.material_slots[0].material = M_HIT if hit else M_WARN if warn else M_STRING
        o.scale.x = o.scale.y = 0.3 if hit else 0.2 if warn else 0.12
    order = (0, 3, 1, 4, 2, 5)
    for ci, c, drips in WAX:
        n0 = int(math.floor(t / L["WAX_EVERY"]))
        for o in drips:
            o.location = U(0, -40, 0)
        for n in (n0, n0 - 1, n0 - 2):
            age = t - n * L["WAX_EVERY"]
            if n < 0 or age > L["WAX_WARN"] + 3.2 / L["WAX_SPEED"]:
                continue
            az = order[n % 6] * 60.0 + ci * 30.0
            y = L["CANDLE_TOP"] - 0.3 - max(0.0, age - L["WAX_WARN"]) * L["WAX_SPEED"]
            o = drips[n % 3]
            o.location = U(c[0] + (L["CANDLE_R"] + 0.08) * math.sin(D(az)), y, c[1] + (L["CANDLE_R"] + 0.08) * math.cos(D(az)))
            o.rotation_euler = (0.0, 0.0, -D(az))
            o.material_slots[0].material = M_WARN if age < L["WAX_WARN"] else M_DRIP
    for g, lane, k in ROPES:
        g.rotation_quaternion = Quaternion(ROPE_AXIS, D(L["rope_angle"](lane, k, t)))
    for i, g in enumerate(TIER):
        g.rotation_euler = (0.0, 0.0, -D(L["tier_turn"](i, t)))


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

# ==== still: everything above is also the film's set (banquet_flow_video.py runs it and stops here)
pose(2.3)
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.008, 0.005, 1)
key = light("LGT_Key", "SUN", U(70, 90, -30), 1.5, (1.0, 0.86, 0.66), angle=D(14))
aim(key, U(0, 4, 36))
fill = light("LGT_Fill", "AREA", U(50, 50, -20), 200000, (0.70, 0.78, 1.0), size=80)
aim(fill, U(0, 4, 36))
for (x, y, z) in lamps:
    light("LGT_Candle", "POINT", U(x, y, z), 2600, (1.0, 0.58, 0.24), shadow_soft_size=0.4)
cam_d.lens = 34
cam.location = U(50, 86, -24)
aim(cam, U(-1, 4.0, 37))
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
scene.view_settings.exposure = -0.9
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = OUT
print("OBJECTS", len(col.objects), "LAMPS", len(lamps))
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)

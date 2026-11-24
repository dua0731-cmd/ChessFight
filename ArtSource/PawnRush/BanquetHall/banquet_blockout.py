# The grand banquet hall's draft (level design v0.4), stacked up from plain shapes: the screens passage and the
# servery, the waiters' lane and the long table with its guests, the magician and the cloches, the royal dais with
# the head table, the champagne tower and the balcony, the dance floor with ten waltzing couples, the stage with its
# curtain and the orchestra, the dessert buffet with the chocolate fountain, the loft and two lines of chandeliers.
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
HIDE = (0.0, -80.0, 0.0)

# ---------------------------------------------------------------- the banquet's colours
M_CLOTH = principled("MAT_Tablecloth", (0.80, 0.74, 0.60), 0.8)
M_CLOTH2 = principled("MAT_TableclothShade", (0.62, 0.52, 0.36), 0.8)
M_LINEN = principled("MAT_Linen", (0.93, 0.91, 0.86), 0.85)
M_OLIVE = principled("MAT_Runner", (0.10, 0.19, 0.045), 0.75)
M_WALNUT = principled("MAT_Walnut", (0.20, 0.10, 0.045), 0.45)
M_WARMSTONE = principled("MAT_WarmStone", (0.42, 0.33, 0.23), 0.7)
M_MARBLE2 = principled("MAT_DaisMarble", (0.70, 0.64, 0.55), 0.25)
M_TILE = principled("MAT_KitchenTile", (0.045, 0.035, 0.03), 0.55)
M_PARQUET = principled("MAT_Parquet", (0.30, 0.17, 0.08), 0.35)
M_PARQUET2 = principled("MAT_ParquetPale", (0.52, 0.38, 0.22), 0.35)
M_IRON = principled("MAT_Iron", (0.05, 0.05, 0.055), 0.4, 0.8)
M_FLAME = principled("MAT_Flame", (1, 0.8, 0.4), 0.3, 0.0, (1.0, 0.62, 0.22), 30.0)
M_CRYSTAL = principled("MAT_Crystal", (1.0, 0.86, 0.52), 0.12, 0.6, (1.0, 0.72, 0.36), 0.6)
M_GLASS = principled("MAT_Glass", (0.70, 0.80, 0.84), 0.05, 0.2)
M_FULL = principled("MAT_ChampagneStill", (0.86, 0.72, 0.36), 0.15, 0.0, (1.0, 0.8, 0.4), 0.3)
M_CHAMP = principled("MAT_ChampagneOverflow", (1.0, 0.84, 0.40), 0.15, 0.0, (1.0, 0.80, 0.30), 6.0)
M_BOTTLE = principled("MAT_Bottle", (0.06, 0.20, 0.09), 0.2, 0.2)
M_CHOC = principled("MAT_Chocolate", (0.16, 0.07, 0.03), 0.25)
M_CHOC2 = principled("MAT_ChocolateLight", (0.40, 0.20, 0.09), 0.3)
M_BERRY = principled("MAT_Strawberry", (0.75, 0.06, 0.05), 0.4)
M_LEAF = principled("MAT_Leaf", (0.10, 0.35, 0.08), 0.6)
M_SILVER = principled("MAT_Silver", (0.80, 0.82, 0.84), 0.18, 1.0)
M_CLOCHE = principled("MAT_Cloche", (0.86, 0.87, 0.89), 0.3, 0.45)
M_PLATE = principled("MAT_Plate", (0.85, 0.83, 0.78), 0.25)
M_VELVET = principled("MAT_Curtain", (0.50, 0.03, 0.05), 0.85)
M_VELVET2 = principled("MAT_CurtainShade", (0.34, 0.02, 0.04), 0.85)
M_GOWN = principled("MAT_QueenGown", (0.80, 0.36, 0.16), 0.6)
M_CAPE = principled("MAT_KingCape", (0.30, 0.34, 0.42), 0.6)
M_IVORY = principled("MAT_Ivory", (0.80, 0.74, 0.60), 0.35)
M_EBONY = principled("MAT_Ebony", (0.07, 0.06, 0.06), 0.3)
M_COAT = principled("MAT_Coat", (0.10, 0.10, 0.12), 0.5)
M_SHIRT = principled("MAT_Shirt", (0.90, 0.90, 0.88), 0.5)
M_GLOVE = principled("MAT_Glove", (0.96, 0.96, 0.94), 0.5)
M_ROBE = principled("MAT_MagicianRobe", (0.24, 0.07, 0.38), 0.6)
M_GUEST = principled("MAT_GuestRobe", (0.36, 0.30, 0.46), 0.6)
M_ROAST = principled("MAT_Roast", (0.45, 0.20, 0.07), 0.5)
M_STEAM = principled("MAT_Steam", (1, 1, 1), 0.5, 0.0, (1, 1, 1), 0.8)
M_WAX = principled("MAT_Wax", (0.84, 0.78, 0.62), 0.45)
M_HERO = principled("MAT_HeroMark", (1.0, 0.9, 0.2), 0.4, 0.0, (1.0, 0.85, 0.1), 6.0)

for name in ("HALL", "SCREEN_Z", "SCREENS", "LOFT", "LOFT_Y", "DAIS_Z", "DAIS_Y", "HEAD_TOP", "TABLE", "STAGE_Y", "CEILING", "LANE", "TRAY_X", "WAITER_X",
             "TRAY_R", "TRAY_Y", "STACK_R", "STACK_TOP", "COUNTER", "TABLE_X0", "TABLE_X1", "TABLE_Z0", "TABLE_Z1", "RUNNER_X", "RUNNER", "PLATES",
             "PLATE_R", "GUESTS", "MAGICIAN", "CLOCHES", "CLOCHE_R", "STAGE_X0", "STAGE_X1", "STAGE_Z0", "STAGE_Z1", "CURTAIN_X", "RAIL_Y", "PLEATS",
             "BUFFET", "BUFFET_TOP", "BUFFET_HEDGE", "PIT", "BASSES", "BASS_TOP", "CURTAIN_OPEN_S", "FOUNTAIN", "BOWL_R", "BOWL_TOP", "TIER_R",
             "TIER_Y", "TIER_BASE", "FLOW", "LINES_X", "ROPE_L", "WHEEL_AT", "WHEEL_R", "CHANDELIERS", "LEVELS", "GLASS_PITCH", "LEVEL_H", "TOWER_Z",
             "PEDESTAL", "HEAD_TABLES", "HEAD_Z", "TOWER_TOP", "BALCONY", "BALCONY_Y", "EXIT", "COUPLES", "GOWN_FOOT", "GOWN_SHOULDER", "SHOULDER_Y",
             "PAIR", "HEAD_R", "WAITERS"):
    globals()[name] = L[name]


def frustum(name, r0, r1, h, segments=36):
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
    for o in made:
        o.parent = parent
    return parent


def panel(name, c, r, az, y0, y1, width, mat, depth=0.08):
    x, z = c[0] + r * math.sin(D(az)), c[1] + r * math.cos(D(az))
    return inst(ME_CUBE, name, mat, U(x, (y0 + y1) / 2, z), (width, depth, y1 - y0), (0, 0, -D(az)))


def flame(x, y, z, size=0.5, lit=True):
    o = ball("Flame", x, y + size * 0.6, z, size, M_FLAME)
    if lit:
        lamps.append((x, y + size * 1.4, z))
    return o


def candle(x, y, z, h=1.4):
    cyl("Candle", x, y, y + h, z, 0.34, M_WAX, ME_CYL8)
    flame(x, y + h, z, 0.28, lit=False)


def piece(x, y, z, team, parent=None, scale=1.0):
    parts = [cyl("Pawn skirt", x, y, y + 0.55 * scale, z, 0.62 * scale, M_WHITE),
             inst(ME_CONE, "Pawn body", M_WHITE, U(x, y + 0.6 * scale, z), (0.6 * scale, 0.6 * scale, 0.7 * scale)),
             ball("Pawn head", x, y + 0.92 * scale, z, 0.42 * scale, M_WHITE),
             cyl("Pawn scarf", x, y + 0.66 * scale, y + 0.8 * scale, z, 0.4 * scale, M_RED if team else M_BLUE)]
    if parent is not None:
        into(parent, *parts)
    return parts


def pawn(name, team, marker=False):
    g = group(name)
    piece(0.0, 0.0, 0.0, team, g, 1.25)
    if marker:
        into(g, inst(ME_CONE, "Hero marker", M_HERO, U(0, 2.5, 0), (0.55, 0.55, 0.9), (math.pi, 0, 0)))
    g.location = U(*HIDE)
    return g


def place(g, p, tumble=0.0):
    if p is None:
        g.location = U(*HIDE)
        return
    g.location = U(*p)
    g.rotation_euler = (D(tumble), 0.0, 0.0)


def set_bar(o, a, b):
    A, B = U(*a), U(*b)
    d = B - A
    o.location = (A + B) / 2
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    o.scale.z = max(1e-3, d.length)


def figure(name, x, y, z, height, mat, head=M_IVORY, radius=1.0):
    me = frustum("ME_" + name, radius, radius * 0.55, height * 0.82)
    return [inst(me, name + " robe", mat, U(x, y, z), (1, 1, 1)),
            torus(name + " collar", x, y + height * 0.82, z, radius * 0.5, 0.1, M_GOLD),
            ball(name + " head", x, y + height * 0.9, z, radius * 1.0, head)]


# ---------------------------------------------------------------- the hall
X0, X1, Z0, Z1 = HALL
box("Hall floor", X0 - 1, X1 + 1, -0.5, 0, Z0 - 2, Z1 + 1, M_TILE)
box("Hall wall W", X0 - 1.2, X0, 0, 30, Z0, Z1, M_WARMSTONE)
box("Hall wall N w", X0, BALCONY[0], 0, 30, Z1, Z1 + 1.2, M_WARMSTONE)
box("Hall wall N e", BALCONY[1], X1, 0, 30, Z1, Z1 + 1.2, M_WARMSTONE)
box("Hall wall N low", BALCONY[0], BALCONY[1], 0, BALCONY_Y - 0.6, Z1, Z1 + 1.2, M_WARMSTONE)
box("Hall wall N high", BALCONY[0], BALCONY[1], BALCONY_Y + 8.0, 30, Z1, Z1 + 1.2, M_WARMSTONE)
box("Exit arch", EXIT[0] - 3.5, EXIT[0] + 3.5, BALCONY_Y, BALCONY_Y + 7.6, Z1 + 0.4, Z1 + 0.6, M_DOOR)
box("Hall kerb E", X1, X1 + 1.0, 0, 1.2, Z0, Z1, M_WARMSTONE)
box("Hall kerb S", X0, X1, 0, 1.2, Z0 - 1.0, Z0, M_WARMSTONE)
for z in range(10, 66, 11):
    box("Window W", X0, X0 + 0.1, 11, 26, z, z + 4.5, M_DOOR)
    box("Banner W", X0 + 0.1, X0 + 0.25, 12, 25, z + 6.0, z + 8.8, M_OLIVE)
    box("Banner W mark", X0 + 0.25, X0 + 0.32, 17.5, 21, z + 6.8, z + 8.0, M_GOLD)
for x in (-27.0, -18.0, 11.0, 20.0):
    box("Window N", x, x + 4.5, 12, 27, Z1 - 0.1, Z1, M_DOOR)
box("Banner N w", -12.5, -8.5, 13, 27, Z1 - 0.25, Z1 - 0.1, M_OLIVE)
box("Banner N e", 8.5, 12.5, 13, 27, Z1 - 0.25, Z1 - 0.1, M_OLIVE)
# the screens and the loft over the passage
for a, b in zip(SCREENS, SCREENS[1:]):
    x0, x1 = sorted((a[0], b[0]))
    z0, z1 = sorted((a[1], b[1]))
    box("Screen", x0 - 0.2, x1 + 0.2, 0, 6.0, z0 - 0.2, z1 + 0.2, M_WALNUT)
    box("Screen cap", x0 - 0.3, x1 + 0.3, 6.0, 6.3, z0 - 0.3, z1 + 0.3, M_GOLD)
box("Loft", LOFT[0], LOFT[1], LOFT_Y - 0.6, LOFT_Y, LOFT[2] + 2.0, LOFT[3], M_WARMSTONE)
box("Loft face", LOFT[0], LOFT[1], LOFT_Y - 3.0, LOFT_Y - 0.6, LOFT[3] - 0.5, LOFT[3], M_WARMSTONE)
for x in range(-28, 29, 8):
    box("Loft pier", x - 0.5, x + 0.5, 0, LOFT_Y - 3.0, LOFT[3] - 1.0, LOFT[3] - 0.2, M_WARMSTONE)
for x0, x1 in ((LOFT[0], -28.0), (-25.0, -3.6), (-1.6, 1.6), (3.6, LOFT[1])):
    box("Loft rail", x0, x1, LOFT_Y, LOFT_Y + 1.1, LOFT[3] - 0.25, LOFT[3], M_GOLD)

# ---------------------------------------------------------------- the servery, the counter and the waiters
box("Counter", COUNTER[0], COUNTER[1], 0, TRAY_Y - 0.1, COUNTER[2], COUNTER[3], M_WALNUT)
box("Counter top", COUNTER[0] - 0.1, COUNTER[1] + 0.05, TRAY_Y - 0.1, TRAY_Y, COUNTER[2] - 0.1, COUNTER[3] + 0.1, M_CLOTH)
for i, h in enumerate((0.8, 1.6)):
    box("Counter step", COUNTER[0] - 1.6 + i * 0.8, COUNTER[0] - 0.8 + i * 0.8, 0, h, COUNTER[2] + 1.0, COUNTER[3] - 1.0, M_WALNUT)
for x in (22.8, 23.8):
    cyl("Spare plates", x, TRAY_Y, TRAY_Y + 0.8, 5.0, 1.0, M_PLATE)
box("Kitchen door", LANE[0] + 0.5, LANE[1] - 0.5, 0, 7.0, Z0 - 0.3, Z0 - 0.1, M_DOOR)
box("Pantry door", X1 - 0.3, X1 - 0.1, 0, 7.0, L["WAITER_Z1"] - 1.0, L["WAITER_Z1"] + 5.0, M_DOOR)
box("Pantry frame", X1 - 0.2, X1 + 1.0, 7.0, 9.0, L["WAITER_Z1"] - 1.5, L["WAITER_Z1"] + 5.5, M_WARMSTONE)
waiter_body = frustum("ME_WaiterCoat", 1.15, 0.6, 5.4)
WAITER_GROUPS = []
for k in range(WAITERS):
    g = group("Waiter")
    off = WAITER_X - TRAY_X
    into(g, inst(waiter_body, "Waiter coat", M_COAT, U(off, 0, 0), (1, 1, 1)),
         torus("Waiter collar", off, 5.45, 0, 0.55, 0.12, M_SHIRT),
         ball("Waiter head", off, 6.05, 0, 1.2, M_IVORY), ball("Waiter top", off, 6.75, 0, 0.35, M_IVORY),
         bar("Waiter arm", (off - 0.6, 4.9, 0), (0.2, 2.3, 0), 0.36, M_COAT), ball("Waiter glove", 0.2, 2.2, 0, 0.55, M_GLOVE),
         cyl("Tray", 0, TRAY_Y - 0.15, TRAY_Y, 0, TRAY_R * 2, M_SILVER), cyl("Plate stack", 0, TRAY_Y, STACK_TOP, 0, STACK_R * 2, M_PLATE))
    for r in range(10):
        into(g, torus("Plate rim", 0, TRAY_Y + 0.07 + r * 0.26, 0, STACK_R, 0.035, M_GOLD))
    WAITER_GROUPS.append((g, k))

# ---------------------------------------------------------------- the long table, its guests, the magician, the cloches
box("Long table", TABLE_X0, TABLE_X1, TABLE - 0.5, TABLE, TABLE_Z0, TABLE_Z1, M_CLOTH)
box("Long table skirt W", TABLE_X0 - 0.06, TABLE_X0, 0.0, TABLE, TABLE_Z0, TABLE_Z1, M_CLOTH2)
box("Long table skirt S", TABLE_X0, TABLE_X1, 0.0, TABLE, TABLE_Z0 - 0.06, TABLE_Z0, M_CLOTH2)
box("Long table skirt E", TABLE_X1, TABLE_X1 + 0.06, 0.0, TABLE, TABLE_Z0, TABLE_Z1, M_CLOTH2)
for z in L["PLATES"]:
    cyl("Plate", RUNNER_X, TABLE + 0.05, TABLE + 0.35, z, PLATE_R * 2, M_PLATE)
    torus("Plate rim", RUNNER_X, TABLE + 0.36, z, PLATE_R - 0.05, 0.05, M_GOLD)
    candle(RUNNER_X + 3.6, TABLE + 0.05, z + 2.0)
    cyl("Goblet stem", RUNNER_X - 3.4, TABLE + 0.05, TABLE + 0.9, z + 1.0, 0.18, M_GLASS, ME_CYL8)
    cyl("Goblet bowl", RUNNER_X - 3.4, TABLE + 0.9, TABLE + 1.5, z + 1.0, 0.8, M_GLASS)
guest_me = frustum("ME_Guest", 1.1, 0.6, 4.6)
for i, z in enumerate(GUESTS):
    x = TABLE_X0 - 1.2
    box("Chair back", x - 1.2, x - 0.9, 0.0, 6.0, z - 1.1, z + 1.1, M_WALNUT)
    inst(guest_me, "Guest", M_GUEST if i % 2 else M_CAPE, U(x, 0.0, z), (1, 1, 1))
    ball("Guest head", x, 5.3, z, 1.2, M_IVORY)
    cyl("Guest crown", x, 5.8, 6.3, z, 0.6, M_GOLD, ME_CYL8)
CLOTH_GROUP = group("Tablecloth")
into(CLOTH_GROUP, box("Cloth top", TABLE_X0 - 0.15, TABLE_X1 + 0.15, TABLE, TABLE + 0.05, TABLE_Z0 - 0.15, TABLE_Z1 - 0.2, M_LINEN),
     box("Cloth runner", RUNNER_X - RUNNER, RUNNER_X + RUNNER, TABLE + 0.05, TABLE + 0.07, TABLE_Z0 + 1.0, L["PLATES"][-1] + 2.0, M_OLIVE),
     box("Cloth hem S", TABLE_X0 - 0.15, TABLE_X1 + 0.15, TABLE - 1.2, TABLE + 0.05, TABLE_Z0 - 0.2, TABLE_Z0 - 0.15, M_LINEN))
mag = group("Magician", MAGICIAN)
robe = frustum("ME_MagicianRobe", 1.5, 0.9, 6.4)
into(mag, inst(robe, "Robe", M_ROBE, U(0, 0, 0), (1, 1, 1)), torus("Collar", 0, 6.4, 0, 0.8, 0.14, M_GOLD), ball("Head", 0, 7.1, 0, 1.4, M_IVORY),
     cyl("Hat brim", 0, 7.65, 7.8, 0, 2.1, M_EBONY), cyl("Hat", 0, 7.8, 9.3, 0, 1.3, M_EBONY), cyl("Hat band", 0, 7.85, 8.1, 0, 1.33, M_VELVET))
for s in (-1, 1):
    into(mag, bar("Arm", (s * 0.9, 5.8, 0.3), (s * 2.6, TABLE + 0.3, TABLE_Z0 - MAGICIAN[2] - 0.1), 0.45, M_ROBE),
         ball("Hand", s * 2.6, TABLE + 0.25, TABLE_Z0 - MAGICIAN[2] - 0.1, 0.65, M_GLOVE))
BUNDLE = inst(ME_SPH, "Cloth in the air", M_LINEN, U(*HIDE), (5.0, 1.6, 2.8))
CLOCHE_GROUPS = []
for i, (x, z) in enumerate(CLOCHES):
    cyl("Dish", x, TABLE, TABLE + 0.15, z, 4.2, M_PLATE)
    inst(ME_SPH, "Roast", M_ROAST, U(x, TABLE + 0.75, z), (2.6, 1.6, 1.5))
    g = group("Cloche", (x, TABLE, z))
    into(g, cyl("Cloche band", 0, 0.0, 1.4, 0, CLOCHE_R * 2, M_CLOCHE), inst(ME_SPH, "Cloche dome", M_CLOCHE, U(0, 1.4, 0), (4.4, 4.4, 3.0)),
         cyl("Cloche stem", 0, 2.85, 3.05, 0, 0.3, M_CLOCHE), torus("Cloche handle", 0, 3.42, 0, 0.42, 0.1, M_CLOCHE, (math.pi / 2, 0, 0)),
         ball("Glove", 0, 3.5, 0.0, 1.05, M_GLOVE), bar("Sleeve", (0, 3.8, -0.2), (0, 12.0, -4.5), 0.8, M_COAT))
    steam = [ball("Steam", 0, -80, 0, 0.9, M_STEAM) for _ in range(3)]
    CLOCHE_GROUPS.append((g, x, z, steam))

# ---------------------------------------------------------------- the royal dais, the head table, the tower, the balcony
box("Dais", X0, X1, 0, DAIS_Y, DAIS_Z, Z1, M_MARBLE2)
box("Dais edge", X0, X1, DAIS_Y - 0.4, DAIS_Y + 0.05, DAIS_Z - 0.15, DAIS_Z + 0.2, M_GOLD)
box("Dais carpet", -2.0, 2.0, DAIS_Y, DAIS_Y + 0.04, DAIS_Z + 0.5, PEDESTAL[2], M_VELVET)
for x0, x1 in HEAD_TABLES:
    box("Head table", x0, x1, HEAD_TOP - 0.5, HEAD_TOP, HEAD_Z[0], HEAD_Z[1], M_CLOTH)
    box("Head table skirt", x0, x1, DAIS_Y, HEAD_TOP - 0.5, HEAD_Z[0], HEAD_Z[0] + 0.1, M_CLOTH2)
    for x in (x0 + 3, (x0 + x1) / 2, x1 - 3):
        candle(x, HEAD_TOP, HEAD_Z[0] + 1.2, 1.8)
        cyl("Head table plate", x + 1.5, HEAD_TOP, HEAD_TOP + 0.08, HEAD_Z[0] + 2.4, 1.6, M_PLATE)
box("Pedestal", PEDESTAL[0], PEDESTAL[1], HEAD_TOP - 0.5, HEAD_TOP, PEDESTAL[2], PEDESTAL[3], M_LINEN)
box("Pedestal skirt", PEDESTAL[0], PEDESTAL[1], DAIS_Y, HEAD_TOP - 0.5, PEDESTAL[2], PEDESTAL[2] + 0.1, M_LINEN)
for x in (PEDESTAL[0], PEDESTAL[1] - 0.1):
    box("Pedestal skirt side", x, x + 0.1, DAIS_Y, HEAD_TOP - 0.5, PEDESTAL[2], PEDESTAL[3], M_LINEN)
for x0, x1 in ((PEDESTAL[0], PEDESTAL[0] + 0.15), (PEDESTAL[1] - 0.15, PEDESTAL[1])):
    box("Pedestal hem", x0, x1, HEAD_TOP - 0.7, HEAD_TOP - 0.5, PEDESTAL[2], PEDESTAL[3], M_GOLD)
bowl_me = frustum("ME_Coupe", 0.55, 0.95, 0.9)
TOWER_LEVELS = {}
for k in range(LEVELS):
    n, base = LEVELS - k, HEAD_TOP + LEVEL_H * k
    for i in range(n):
        for j in range(n):
            x, z = (i - (n - 1) / 2.0) * GLASS_PITCH, TOWER_Z + (j - (n - 1) / 2.0) * GLASS_PITCH
            cyl("Glass foot", x, base, base + 0.06, z, 1.1, M_GLASS)
            cyl("Glass stem", x, base + 0.06, base + 0.6, z, 0.22, M_GLASS, ME_CYL8)
            TOWER_LEVELS.setdefault(k, []).append(inst(bowl_me, "Glass bowl", M_FULL, U(x, base + 0.6, z), (1, 1, 1)))
BOTTLE = [bar("Bottle", (0, 0, 0), (0, 1, 0), 1.5, M_BOTTLE), bar("Bottle neck", (0, 0, 0), (0, 1, 0), 0.5, M_BOTTLE),
          bar("Bottle label", (0, 0, 0), (0, 1, 0), 1.56, M_GOLD), bar("Sommelier sleeve", (0, 0, 0), (0, 1, 0), 0.8, M_COAT),
          bar("Pour", (0, 0, 0), (0, 1, 0), 0.22, M_CHAMP)]
BOTTLE_GLOVE = ball("Sommelier glove", 0, -80, 0, 1.0, M_GLOVE)
box("Balcony", BALCONY[0], BALCONY[1], BALCONY_Y - 0.6, BALCONY_Y, BALCONY[2], BALCONY[3], M_WARMSTONE)
box("Balcony carpet", -1.8, 1.8, BALCONY_Y, BALCONY_Y + 0.04, BALCONY[2] + 0.4, BALCONY[3], M_VELVET)
for x in (BALCONY[0], BALCONY[1] - 0.3):
    box("Balcony rail", x, x + 0.3, BALCONY_Y, BALCONY_Y + 1.1, BALCONY[2], BALCONY[3], M_GOLD)
for s in (-1, 1):
    box("Throne", s * 3.6 - 0.9, s * 3.6 + 0.9, BALCONY_Y, BALCONY_Y + 3.6, BALCONY[3] - 2.4, BALCONY[3] - 1.2, M_GOLD)
    box("Throne seat", s * 3.6 - 0.9, s * 3.6 + 0.9, BALCONY_Y, BALCONY_Y + 1.3, BALCONY[3] - 3.4, BALCONY[3] - 1.2, M_VELVET)
box("Canopy", -5.0, 5.0, BALCONY_Y + 7.8, BALCONY_Y + 8.6, BALCONY[3] - 3.0, BALCONY[3], M_VELVET)

# ---------------------------------------------------------------- the dance floor and the couples
for i in range(int((TABLE_X0 - 2.4 - STAGE_X1) // 3.0) + 1):
    for j in range(int((DAIS_Z - SCREEN_Z) // 3.0) + 1):
        x0, z0 = STAGE_X1 + i * 3.0, SCREEN_Z + j * 3.0
        box("Dance square", x0, min(x0 + 3.0, TABLE_X0 - 2.4), 0, 0.05, z0, min(z0 + 3.0, DAIS_Z), M_PARQUET if (i + j) % 2 else M_PARQUET2)
gown_me = frustum("ME_Gown", GOWN_FOOT, GOWN_SHOULDER, SHOULDER_Y - 0.25)
COUPLE_GROUPS = []
for i in range(COUPLES):
    g = group("Couple %d" % i)
    for who, z, mat in (("Queen", 0.0, M_GOWN), ("King", 0.0, M_CAPE)):
        pass
    for who, xz, mat in (("Queen", (0.0, -PAIR), M_GOWN), ("King", (0.0, PAIR), M_CAPE)):
        x, z = xz
        into(g, inst(gown_me, who + " gown", mat, U(x, 0.25, z), (1, 1, 1)), cyl(who + " foot", x, 0, 0.25, z, GOWN_FOOT * 2 + 0.2, M_GOLD),
             cyl(who + " shoulders", x, SHOULDER_Y - 0.12, SHOULDER_Y, z, GOWN_SHOULDER * 2 + 0.1, M_GOLD),
             ball(who + " head", x, SHOULDER_Y + 0.7, z, HEAD_R * 2 + 0.3, M_IVORY),
             cyl(who + " crown", x, SHOULDER_Y + 1.25, SHOULDER_Y + (2.3 if who == "King" else 1.75), z, 0.75, M_GOLD, ME_CYL8))
    into(g, bar("Joined arms", (0, SHOULDER_Y - 0.35, -PAIR + GOWN_SHOULDER), (0, SHOULDER_Y - 0.35, PAIR - GOWN_SHOULDER), 0.45, M_IVORY))
    COUPLE_GROUPS.append(g)

# ---------------------------------------------------------------- the stage, the curtain, the orchestra, the pit
box("Stage", STAGE_X0, STAGE_X1, 0, STAGE_Y, STAGE_Z0, STAGE_Z1, M_WALNUT)
box("Stage floor", STAGE_X0, STAGE_X1, STAGE_Y - 0.1, STAGE_Y, STAGE_Z0, STAGE_Z1, M_PARQUET2)
box("Stage front trim", STAGE_X1 - 0.1, STAGE_X1 + 0.05, STAGE_Y - 0.7, STAGE_Y, STAGE_Z0, STAGE_Z1, M_GOLD)
bar("Curtain rail", (CURTAIN_X, RAIL_Y, CURTAIN_OPEN_S - 3.5), (CURTAIN_X, RAIL_Y, STAGE_Z1 + 1.0), 0.3, M_GOLD)
box("Pelmet", CURTAIN_X - 0.5, CURTAIN_X + 0.5, RAIL_Y - 0.2, RAIL_Y + 1.6, CURTAIN_OPEN_S - 3.5, STAGE_Z1 + 1.0, M_VELVET2)
box("Pelmet fringe", CURTAIN_X + 0.5, CURTAIN_X + 0.6, RAIL_Y - 0.4, RAIL_Y - 0.1, CURTAIN_OPEN_S - 3.5, STAGE_Z1 + 1.0, M_GOLD)
PLEAT_GROUPS = []
for side in (-1, 1):
    for k in range(PLEATS):
        g = group("Pleat")
        x = CURTAIN_X + (0.12 if k % 2 else -0.12)
        into(g, box("Pleat cloth", x - 0.12, x + 0.12, STAGE_Y + 0.3, RAIL_Y, -0.55, 0.55, M_VELVET if k % 2 else M_VELVET2),
             box("Pleat fringe", x - 0.14, x + 0.14, STAGE_Y + 0.15, STAGE_Y + 0.35, -0.55, 0.55, M_GOLD))
        PLEAT_GROUPS.append((g, side, k))
for x, z in ((-29.0, STAGE_Z0 + 4.0), (-29.0, STAGE_Z0 + 9.0), (-26.5, STAGE_Z0 + 13.0), (-29.0, STAGE_Z1 - 3.0)):
    figure("Musician", x, STAGE_Y, z, 4.4, M_COAT, M_IVORY, 0.8)
    bar("Music stand", (x + 1.4, STAGE_Y, z), (x + 1.4, STAGE_Y + 2.6, z), 0.1, M_IRON)
cyl("Grand drum", -30.0, STAGE_Y, STAGE_Y + 1.8, STAGE_Z1 - 6.5, 2.6, M_VELVET)
box("Pit floor", PIT[0], PIT[1], 0.0, 0.08, PIT[2], PIT[3], M_PARQUET2)
for b in BASSES:
    g = group("Double bass", (b[0], 0.0, b[1]))
    into(g, box("Bass body", -0.35, 0.35, 0.0, BASS_TOP - 3.0, -0.8, 0.8, M_WALNUT), bar("Bass neck", (0, BASS_TOP - 3.0, 0), (0, BASS_TOP - 0.3, 0), 0.3, M_EBONY),
         ball("Bass scroll", 0, BASS_TOP - 0.2, 0, 0.5, M_EBONY))
    figure("Bassist", b[0] - 1.6, 0.0, b[1], 4.8, M_COAT, M_EBONY, 0.8)

# ---------------------------------------------------------------- the dessert buffet and the chocolate fountain
box("Buffet", BUFFET[0], BUFFET[1], BUFFET_TOP - 0.5, BUFFET_TOP, BUFFET[2], BUFFET[3], M_CLOTH)
box("Buffet skirt E", BUFFET[1], BUFFET[1] + 0.08, 0, BUFFET_TOP, BUFFET[2], BUFFET[3], M_CLOTH2)
box("Buffet skirt N", BUFFET[0], BUFFET[1], 0, BUFFET_TOP, BUFFET[3], BUFFET[3] + 0.08, M_CLOTH2)
for z in range(int(BUFFET[2]) + 1, int(BUFFET[3]) - 1, 2):
    cyl("Cake stand", BUFFET[1] - 0.7, BUFFET_TOP, BUFFET_TOP + BUFFET_HEDGE - 0.6, z + 0.5, 0.25, M_GOLD, ME_CYL8)
    cyl("Stand cake", BUFFET[1] - 0.7, BUFFET_TOP + BUFFET_HEDGE - 0.6, BUFFET_TOP + BUFFET_HEDGE, z + 0.5, 1.2, M_BERRY if z % 4 else M_PLATE)
F = FOUNTAIN
cyl("Bowl", F[0], BUFFET_TOP, BOWL_TOP, F[1], BOWL_R * 2, M_SILVER)
cyl("Pool", F[0], BOWL_TOP - 0.05, BOWL_TOP + 0.02, F[1], BOWL_R * 2 - 0.4, M_CHOC)
dome_me = [frustum("ME_Tier%d" % i, r, 0.55, 0.6) for i, r in enumerate(TIER_R)]
FLOW_RINGS = []
for k in range(4):
    cyl("Choc curtain", F[0], TIER_BASE[k], TIER_Y[k], F[1], TIER_R[k] * 2, M_CHOC)
    inst(dome_me[k], "Tier", M_CHOC, U(F[0], TIER_Y[k], F[1]), (1, 1, 1))
    torus("Tier rim", F[0], TIER_Y[k], F[1], TIER_R[k], 0.08, M_SILVER)
    for n in range(4):
        FLOW_RINGS.append((torus("Flow", F[0], -80, F[1], TIER_R[k] + 0.03, 0.07, M_CHOC2), k, n))
cyl("Fountain column", F[0], TIER_Y[3], TIER_Y[3] + 1.2, F[1], 0.7, M_SILVER)
inst(ME_CONE, "Fountain crown", M_SILVER, U(F[0], TIER_Y[3] + 1.2, F[1]), (1.4, 1.4, 1.0))
for i in range(10):
    a = D(i * 36 + 15)
    x, z = F[0] + 5.8 * math.sin(a), F[1] + 5.8 * math.cos(a)
    if BUFFET[0] + 0.5 < x < BUFFET[1] - 1.2 and BUFFET[2] + 0.5 < z < BUFFET[3] - 0.5:
        ball("Strawberry", x, BUFFET_TOP + 0.45, z, 0.9, M_BERRY)
        inst(ME_CONE, "Leaf", M_LEAF, U(x, BUFFET_TOP + 0.8, z), (0.6, 0.6, 0.3))

# ---------------------------------------------------------------- the chandeliers
RL, WAT, WR = ROPE_L, WHEEL_AT, WHEEL_R
ROPES = []
for lane in (0, 1):
    for k in range(CHANDELIERS):
        px, py, pz = L["rope_pivot"](lane, k)
        bar("Ceiling chain", (px, py, pz), (px, CEILING, pz), 0.14, M_BRASS)
        g = group("Chandelier %d %d" % (lane, k), (px, py, pz))
        g.rotation_mode = "QUATERNION"
        into(g, bar("Chandelier line", (0, 0, 0), (0, -RL, 0), 0.16, M_BRASS), cyl("Chandelier wheel", 0, -WAT - 0.12, -WAT + 0.12, 0, WR * 2, M_GOLD),
             torus("Chandelier rim", 0, -WAT, 0, WR, 0.12, M_CRYSTAL), bar("Chandelier tassel", (0, -WAT, 0), (0, -RL, 0), 0.3, M_CRYSTAL),
             ball("Chandelier end", 0, -RL, 0, 0.55, M_GOLD))
        for a in range(0, 360, 60):
            into(g, flame((WR - 0.2) * math.sin(D(a)), -WAT + 0.12, (WR - 0.2) * math.cos(D(a)), 0.26, lit=False))
        ROPES.append((g, lane, k))
        if lane == 0 and k % 2 == 0:
            lamps.append((0.0, py - WAT - 1.0, pz))
box("Ceiling", X0, X1, CEILING, CEILING + 0.4, Z0, Z1, M_WARMSTONE) if False else None
lamps.extend([(22.0, 7.0, 30.0), (-26.0, 12.0, 38.0), (-26.0, 12.0, 10.0), (0.0, 14.0, 62.0), (24.0, 4.0, 6.0)])


# ---------------------------------------------------------------- where everything is at time t
def pose(t):
    for g, k in WAITER_GROUPS:
        z = L["waiter_z"](k, t)
        g.location = U(TRAY_X, 0.06 * abs(math.sin(t * 5.5 + k)), z) if z < L["WAITER_Z1"] - 0.5 else U(*HIDE)
    cz = L["cloth_shift"](t)
    CLOTH_GROUP.location = U(*HIDE) if cz is None else U(0.0, 0.0, -cz)
    roll = L["drum_roll"](t)
    lean = 6.0 if roll else 0.0
    mag.rotation_euler = (D(-lean), 0.0, 0.0)
    BUNDLE.location = U(MAGICIAN[0], 10.6 + 0.3 * math.sin(t * 6.0), MAGICIAN[2]) if cz is None else U(*HIDE)
    h, jig = L["cloche_lift"](t)
    for i, (g, x, z, steam) in enumerate(CLOCHE_GROUPS):
        g.location = U(x, TABLE + h, z)
        g.rotation_euler = (jig * (1 if i % 2 else -1), 0.0, 0.0)
        for n, s in enumerate(steam):
            if h > 0.6:
                rise = (t * 0.9 + n * 0.33) % 1.0
                s.location = U(x + (n - 1) * 0.7, TABLE + 1.4 + rise * 3.4, z + (n - 1) * 0.4)
                s.scale = (0.6 + rise,) * 3
            else:
                s.location = U(*HIDE)
    for i, g in enumerate(COUPLE_GROUPS):
        mid, q, king, turn = L["couple"](i, t)
        g.location = U(mid[0], 0.0, mid[1])
        # the queen sits at local (0, -PAIR) (south); turn the couple so she is at angle `turn` (degrees, from +x anticlockwise)
        g.rotation_euler = (0.0, 0.0, D(turn + 90.0))
    for g, side, k in PLEAT_GROUPS:
        g.location = U(0.0, 0.0, L["pleat_z"](side, k, t))
    for o, k, n in FLOW_RINGS:
        hgt = TIER_Y[k] - TIER_BASE[k]
        o.location = U(FOUNTAIN[0], TIER_Y[k] - ((t * FLOW + n * hgt / 4.0) % hgt), FOUNTAIN[1])
    for g, lane, k in ROPES:
        g.rotation_quaternion = Quaternion(Vector((1.0, 0.0, 0.0)), -D(L["rope_angle"](lane, k, t)))
    pouring = False
    u = (t - 1.0) % L["POURS_EVERY"]
    a = 144.0 if 0.0 <= u < L["POUR_FOR"] else (144.0 * (1.0 - min(1.0, (u - L["POUR_FOR"]) / 0.4)) if u < L["POUR_FOR"] + 0.4 else
                                                (144.0 * min(1.0, (u - L["POURS_EVERY"] + 0.5) / 0.5) if u > L["POURS_EVERY"] - 0.5 else 0.0))
    pouring = 0.0 <= u < L["POUR_FOR"]
    Hn = (0.0, TOWER_TOP + 1.6, TOWER_Z + 1.2)
    uu = (math.cos(D(a)), -math.sin(D(a)))
    mouth = (0.0, Hn[1] + 0.9 * uu[0], Hn[2] + 0.9 * uu[1])
    base = (0.0, Hn[1] - 3.4 * uu[0], Hn[2] - 3.4 * uu[1])
    set_bar(BOTTLE[0], base, Hn)
    set_bar(BOTTLE[1], Hn, mouth)
    set_bar(BOTTLE[2], tuple(p + (q - p) * 0.35 for p, q in zip(base, Hn)), tuple(p + (q - p) * 0.65 for p, q in zip(base, Hn)))
    mid = tuple((p + q) / 2.0 for p, q in zip(base, Hn))
    BOTTLE_GLOVE.location = U(mid[0], mid[1] + 0.2, mid[2] + 0.6)
    set_bar(BOTTLE[3], (mid[0], mid[1] + 0.2, mid[2] + 0.6), (0.0, 30.0, TOWER_Z + 6.0))
    if pouring:
        set_bar(BOTTLE[4], mouth, (0.0, TOWER_TOP - 0.1, TOWER_Z))
    else:
        BOTTLE[4].location = U(*HIDE)
    for k, bowls in TOWER_LEVELS.items():
        m = M_CHAMP if L["overflow"](k, t) else M_FULL
        for b in bowls:
            b.material_slots[0].material = m


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
pose(3.0)
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.008, 0.005, 1)
key = light("LGT_Key", "SUN", U(70, 90, -30), 1.5, (1.0, 0.86, 0.66), angle=D(14))
aim(key, U(0, 4, 36))
fill = light("LGT_Fill", "AREA", U(50, 50, -20), 200000, (0.70, 0.78, 1.0), size=80)
aim(fill, U(0, 4, 36))
for (x, y, z) in lamps:
    light("LGT_Candle", "POINT", U(x, y, z), 2600, (1.0, 0.58, 0.24), shadow_soft_size=0.4)
cam_d.lens = 28
cam.location = U(74, 64, 4)
aim(cam, U(-7, 3.0, 33))
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

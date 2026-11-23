# The grand banquet hall's draft (level design v0.1), stacked up from plain shapes for a look at its layers: the
# kitchen floor, the long table, the candelabra over the soup, the two chandeliers, the cake and the royal balcony.
# A picture only: nothing here goes into Unity, and it is a blockout, not the concept art.
# Section frame: x east, y up, z north; in Blender (X, Y, Z) = (x, z, y). The numbers are banquet_layout.py's.
#
#   blender --background --factory-startup --python banquet_blockout.py -- <out.png> [samples] [width] [height]
#
# The box / cylinder / bar / pawn helpers are ../RoyalObservatory/observatory_dress_preview.py's: its head, up to its
# first geometry, is run here (it also reads the arguments and empties the scene).
import bpy, bmesh, math, os, random
from mathutils import Vector, Matrix, Euler

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
M_WALNUT = principled("MAT_Walnut", (0.20, 0.10, 0.045), 0.45)
M_WARMSTONE = principled("MAT_WarmStone", (0.42, 0.33, 0.23), 0.7)
M_TILE = principled("MAT_KitchenTile", (0.045, 0.035, 0.03), 0.55)
M_IRON = principled("MAT_Iron", (0.05, 0.05, 0.055), 0.4, 0.8)
M_SOUP = principled("MAT_Soup", (0.62, 0.11, 0.02), 0.35, 0.0, (1.0, 0.16, 0.02), 0.55)
M_WAX = principled("MAT_Wax", (0.84, 0.78, 0.62), 0.45)
M_FLAME = principled("MAT_Flame", (1, 0.8, 0.4), 0.3, 0.0, (1.0, 0.62, 0.22), 30.0)
M_SPONGE = principled("MAT_Cake", (0.86, 0.74, 0.60), 0.6)
M_CREAMSTRIPE = principled("MAT_Cream", (0.86, 0.30, 0.10), 0.5)
M_FONDANT = principled("MAT_Fondant", (0.42, 0.56, 0.68), 0.25)
M_CRYSTAL = principled("MAT_Crystal", (1.0, 0.86, 0.52), 0.12, 0.6, (1.0, 0.72, 0.36), 0.6)
M_WINE = principled("MAT_Goblet", (0.34, 0.20, 0.52), 0.1, 0.3)
M_SILVER = principled("MAT_Silver", (0.80, 0.82, 0.84), 0.18, 1.0)
M_PLATE = principled("MAT_Plate", (0.78, 0.76, 0.70), 0.25)

TABLE, TX, Z0, Z1 = L["TABLE"], L["TABLE_X"], L["TABLE_Z0"], L["TABLE_Z1"]
HALL = L["HALL"]


def panel(name, c, r, az, y0, y1, width, mat, depth=0.08):
    # A flat piece standing on a circle round c, facing out at heading az.
    x, z = c[0] + r * math.sin(D(az)), c[1] + r * math.cos(D(az))
    return inst(ME_CUBE, name, mat, U(x, (y0 + y1) / 2, z), (width, depth, y1 - y0), (0, 0, -D(az)))


def flame(x, y, z, size=0.5):
    ball("Flame", x, y + size * 0.6, z, size, M_FLAME)
    lamps.append((x, y + size * 1.4, z))


# ---------------------------------------------------------------- the hall: the kitchen floor and two walls
box("Kitchen floor", HALL[0] - 4, HALL[1] + 4, -0.6, 0, HALL[2] - 6, HALL[3] + 4, M_TILE)
box("Hall wall N", HALL[0], HALL[1], 0, 30, HALL[3], HALL[3] + 1.2, M_WARMSTONE)
box("Hall wall W", HALL[0] - 1.2, HALL[0], 0, 30, HALL[2], HALL[3], M_WARMSTONE)
box("Hall kerb E", HALL[1], HALL[1] + 1.2, 0, 1.0, HALL[2], HALL[3], M_WARMSTONE)
for z in range(8, 100, 12):
    box("Window W", HALL[0], HALL[0] + 0.1, 8, 21, z, z + 4.5, M_DOOR)
    box("Banner W", HALL[0] + 0.1, HALL[0] + 0.25, 9.5, 20, z + 6.2, z + 9.2, M_OLIVE)
    box("Banner W mark", HALL[0] + 0.25, HALL[0] + 0.32, 14, 17, z + 7.1, z + 8.3, M_GOLD)
for x in range(-12, 20, 11):
    box("Window N", x, x + 4.5, 8, 21, HALL[3] - 0.1, HALL[3], M_DOOR)
    box("Banner N", x + 6, x + 9, 9.5, 20, HALL[3] - 0.25, HALL[3] - 0.1, M_OLIVE)
    box("Banner N mark", x + 6.9, x + 8.1, 14, 17, HALL[3] - 0.32, HALL[3] - 0.25, M_GOLD)
# the kitchen: ovens along the west, bread carts, shelves
for z in (10, 26, 42):
    box("Oven", HALL[0] + 0.2, HALL[0] + 4, 0, 2.6, z, z + 6, M_WARMSTONE)
    box("Oven fire", HALL[0] + 3.9, HALL[0] + 4.05, 0.5, 1.8, z + 1.5, z + 4.5, M_SOUP)
for (x, z) in ((-11, 20), (10.5, 12), (12, 30), (-10.5, 50), (24, 40), (26, 62), (30, 20)):
    box("Cart", x, x + 3, 0, 1.1, z, z + 1.8, M_WALNUT)
    for k in range(3):
        ball("Loaf", x + 0.6 + k * 0.9, 1.35, z + 0.9, 0.8, M_WARMSTONE)

# ---------------------------------------------------------------- 0, 1: the kitchen passage and the flowing tablecloth
box("Passage floor", -8, 8, 0, 0.06, 0, Z0, M_WARMSTONE)
for s in (-1, 1):
    box("Passage post", s * 8 - 0.5, s * 8 + 0.5, 0, 5.5, 0, 1.2, M_WALNUT)
box("Passage lintel", -8.5, 8.5, 5.5, 6.5, 0, 1.2, M_WALNUT)
for i in range(L["CLOTH_LANES"]):
    x0 = -TX + i * L["LANE_WIDTH"]
    box("Cloth lane", x0 + 0.05, x0 + L["LANE_WIDTH"] - 0.05, 0.15, TABLE, Z0 - 0.14, Z0, M_CLOTH if i % 2 == 0 else M_CLOTH2)
    up = i % 3 != 1
    for k in range(4):
        y = 0.9 + k * 0.95 + (0.3 if up else 0.0)
        box("Cloth lane mark", x0 + 0.5, x0 + L["LANE_WIDTH"] - 0.5, y, y + 0.16, Z0 - 0.17, Z0 - 0.14, M_GOLD if up else M_OLIVE)

# ---------------------------------------------------------------- 2: the long table, its runner and the goblets
box("Long table", -TX, TX, TABLE - 0.5, TABLE, Z0, Z1 + 1.2, M_CLOTH)
box("Long table edge", -TX - 0.1, TX + 0.1, TABLE - 0.75, TABLE - 0.5, Z0 - 0.1, Z1 + 1.3, M_WALNUT)
for z in (Z0 + 2, 30, 42, Z1 - 1):
    for s in (-1, 1):
        box("Table leg", s * (TX - 1.4) - 0.5, s * (TX - 1.4) + 0.5, 0, TABLE - 0.75, z - 0.5, z + 0.5, M_WALNUT)
box("Runner", -L["RUNNER"], L["RUNNER"], TABLE, TABLE + 0.04, Z0 + 1.5, Z1 + 1.0, M_OLIVE)
for s in (-1, 1):
    box("Runner hem", s * L["RUNNER"] - 0.12, s * L["RUNNER"] + 0.12, TABLE + 0.04, TABLE + 0.06, Z0 + 1.5, Z1 + 1.0, M_GOLD)
for z in (21.0, 29.0, 37.0, 45.0):
    for s in (-1, 1):
        box("Side cloth", s * TX if s > 0 else -TX - 0.12, TX + 0.12 if s > 0 else -TX, 0.2, TABLE - 0.5, z, z + 2.0, M_CLOTH2)
for z in L["GOBLETS"]:
    for s in (-1, 1):
        cyl("Goblet foot", s * 4.0, TABLE, TABLE + 0.15, z, 1.5, M_WINE)
        cyl("Goblet stem", s * 4.0, TABLE + 0.15, TABLE + 1.9, z, 0.32, M_WINE, ME_CYL8)
        ball("Goblet bowl", s * 4.0, TABLE + 3.0, z, 2.6, M_WINE)
        box("Bottles", s * 4.9 if s > 0 else -TX + 0.2, TX - 0.2 if s > 0 else -4.9, TABLE, TABLE + 2.4, z - 0.45, z + 0.45, M_WALNUT)
    for s in (-1, 1):
        for dz in (-3.4, 3.4):
            cyl("Plate", s * 4.3, TABLE, TABLE + 0.08, z + dz, 2.2, M_PLATE)
            cyl("Plate rim", s * 4.3, TABLE + 0.08, TABLE + 0.1, z + dz, 1.5, M_GOLD)

# ---------------------------------------------------------------- 3: the candelabra over the soup
SOUP, SR = L["SOUP"], L["SOUP_R"]
cyl("Cauldron", SOUP[0], 0, L["SOUP_Y"] + 0.9, SOUP[1], SR * 2 + 1.0, M_IRON)
cyl("Soup", SOUP[0], L["SOUP_Y"] + 0.9, L["SOUP_Y"] + 0.96, SOUP[1], SR * 2, M_SOUP)
lamps.append((SOUP[0], 4.5, SOUP[1]))
DR, DRUM_R, DRUM_TOP = L["DRUM"], L["DRUM_R"], L["DRUM_TOP"]
cyl("Candelabra table", DR[0], TABLE - 0.5, TABLE, DR[1], (DRUM_R + 1.0) * 2, M_CLOTH)
cyl("Candelabra post", DR[0], 0, TABLE - 0.5, DR[1], 2.4, M_WALNUT)
cyl("Candelabra drum", DR[0], TABLE, DRUM_TOP, DR[1], DRUM_R * 2, M_GOLD)
for h in (TABLE + 0.5, DRUM_TOP - 0.4):
    torus("Drum band", DR[0], h, DR[1], DRUM_R + 0.05, 0.14, M_BRASS)
for c in L["CANDLES"]:
    a, b = L["toward"](DR, c, DRUM_R - 0.4), L["toward"](c, DR, L["DISH_R"] - 0.3)
    bar("Candelabra arm", (a[0], DRUM_TOP - 0.25, a[1]), (b[0], DRUM_TOP - 0.25, b[1]), 1.0, M_GOLD)
    cyl("Candle dish", c[0], DRUM_TOP - 0.3, DRUM_TOP, c[1], L["DISH_R"] * 2, M_GOLD)
    cyl("Candle", c[0], DRUM_TOP, L["CANDLE_TOP"], c[1], L["CANDLE_R"] * 2, M_WAX)
    flame(c[0], L["CANDLE_TOP"], c[1], 0.7)
    for k in range(2):
        az = random.uniform(0, 360)
        y = random.uniform(DRUM_TOP + 0.6, L["CANDLE_TOP"] - 0.3)
        panel("Wax drip", c, L["CANDLE_R"] + 0.05, az, y, y + 0.55, 0.7, M_CLOTH, 0.22)

# ---------------------------------------------------------------- 4: the two chandeliers
for c, top, skirt in ((L["C1"], L["C1_TOP"], L["C1_SKIRT"]), (L["C2"], L["C2_TOP"], L["C2_SKIRT"])):
    R = L["RING_R"]
    cyl("Chandelier deck", c[0], top - 0.35, top, c[1], R * 2, M_GOLD)
    cyl("Chandelier skirt", c[0], top - skirt, top - 0.35, c[1], R * 2 - 0.1, M_CRYSTAL)
    for k in range(18):
        panel("Crystal strand", c, R, k * 20, top - skirt - 0.5, top - skirt, 0.18, M_CRYSTAL, 0.18)
    bar("Chandelier chain", (c[0], top, c[1]), (c[0], 30, c[1]), 0.22, M_BRASS)
    cyl("Chandelier hub", c[0], top, top + 0.9, c[1], 0.8, M_GOLD)
    for k in range(8):
        az = k * 45 + 22.5
        flame(c[0] + (R - 0.35) * math.sin(D(az)), top, c[1] + (R - 0.35) * math.cos(D(az)), 0.28)

# ---------------------------------------------------------------- 5: the cake on its table
CK = L["CAKE"]
cyl("Cake table", CK[0], TABLE - 0.5, TABLE, CK[1], L["CAKE_TABLE_R"] * 2, M_CLOTH)
cyl("Cake table skirt", CK[0], 1.2, TABLE - 0.5, CK[1], L["CAKE_TABLE_R"] * 2 - 0.3, M_CLOTH2)
cyl("Cake table post", CK[0], 0, 1.2, CK[1], 4.0, M_WALNUT)
cyl("Cake stand", CK[0], TABLE, TABLE + 0.25, CK[1], L["TIERS"][0][0] * 2 + 1.2, M_GOLD)
bottom = TABLE + 0.25
for i, (r, top, speed) in enumerate(L["TIERS"]):
    cyl("Cake tier", CK[0], bottom, top, CK[1], r * 2, M_SPONGE)
    count, degrees = L["CREAM"][i]
    step = 10
    for k in range(0, 360, step):
        cream = ((k + step / 2 - 20.0 * i) % (360.0 / count)) < degrees
        panel("Cream" if cream else "Fondant", CK, r + 0.03, k + step / 2, bottom + 0.12, top - 0.12, 2 * r * math.tan(D(step / 2)) + 0.03,
              M_CREAMSTRIPE if cream else M_FONDANT)
    torus("Tier piping", CK[0], top, CK[1], r - 0.05, 0.11, M_PLATE)
    bottom = top
cyl("Cake crown", CK[0], bottom, L["TOPPER"][1] - 0.5, CK[1], L["TOPPER"][0] * 2, M_GOLD)
for k in range(6):
    az = k * 60
    bar("Crown point", (CK[0] + 1.0 * math.sin(D(az)), L["TOPPER"][1] - 0.5, CK[1] + 1.0 * math.cos(D(az))),
        (CK[0] + 1.15 * math.sin(D(az)), L["TOPPER"][1] + 0.4, CK[1] + 1.15 * math.cos(D(az))), 0.3, M_GOLD)
flame(CK[0], L["TOPPER"][1] - 0.4, CK[1], 0.6)

# ---------------------------------------------------------------- 6: the cake knife and the royal balcony
K0, K1 = L["KNIFE"]
BY, BAL = L["BALCONY_Y"], L["BALCONY"]
bar("Knife blade", (K0[0], L["TIERS"][3][1] - 0.06, K0[1]), (K1[0] + 0.6, BY - 0.12, K1[1] + 0.5), 2.0, M_SILVER)
bar("Knife handle", (K1[0] + 0.6, BY + 0.25, K1[1] + 0.5), (K1[0] + 4.2, BY + 0.25, K1[1] + 3.5), 0.9, M_WALNUT)
box("Balcony", BAL[0], BAL[1], BY - 0.8, BY, BAL[2], BAL[3], M_WARMSTONE)
box("Balcony carpet", BAL[0] + 4, BAL[1] - 4, BY, BY + 0.04, BAL[2] + 1, BAL[3], M_OLIVE)
for x in (BAL[0] + 1.2, BAL[1] - 1.2):
    for z in (BAL[2] + 1.2, BAL[3] - 1.2):
        box("Balcony pier", x - 0.9, x + 0.9, 0, BY - 0.8, z - 0.9, z + 0.9, M_WARMSTONE)
box("Balcony rail S", BAL[0] + 5.0, BAL[1], BY, BY + 1.1, BAL[2], BAL[2] + 0.3, M_WALNUT)
box("Balcony rail W", BAL[0], BAL[0] + 0.3, BY, BY + 1.1, BAL[2] + 5.0, BAL[3], M_WALNUT)
box("Exit arch", L["EXIT"][0] - 5, L["EXIT"][0] + 5, BY, BY + 7, HALL[3] - 0.2, HALL[3] + 0.1, M_DOOR)
box("Canopy", L["EXIT"][0] - 6, L["EXIT"][0] + 6, BY + 7.2, BY + 8.2, HALL[3] - 3.5, HALL[3], M_OLIVE)
box("Canopy fringe", L["EXIT"][0] - 6, L["EXIT"][0] + 6, BY + 6.9, BY + 7.2, HALL[3] - 3.6, HALL[3] - 3.4, M_GOLD)

# ---------------------------------------------------------------- the pieces
C1, C2 = L["C1"], L["C2"]
t3 = L["toward"](CK, C2, L["TIERS"][2][0] - 0.7)
t2 = L["toward"](CK, C2, L["TIERS"][1][0] - 0.7)
PAWNS = [(0.5, 0, 4), (-2, 0, 9), (3, 0, 14.6), (-3.2, 0, 14.8), (1, TABLE, 20), (-1.2, TABLE, 28.5), (0.8, TABLE, 36.5), (-0.6, TABLE, 45), (1.4, TABLE, 52),
         (DR[0] - 1.2, DRUM_TOP, DR[1] + 0.6), (DR[0] + 1.0, DRUM_TOP, DR[1] - 0.8), (L["CANDLES"][1][0], L["CANDLE_TOP"], L["CANDLES"][1][1] - 0.4),
         (C1[0] - 0.8, L["C1_TOP"], C1[1] + 0.5), (C2[0] + 0.6, L["C2_TOP"], C2[1] - 0.4), (t2[0], L["TIERS"][1][1], t2[1]), (t3[0], L["TIERS"][2][1], t3[1]),
         (CK[0] - 1.6, L["TIERS"][3][1], CK[1] + 1.0), ((K0[0] + K1[0]) / 2, (L["TIERS"][3][1] + BY) / 2 + 0.05, (K0[1] + K1[1]) / 2),
         (28, BY, 93), (31, BY, 97.5), (-9.5, 0, 31), (9.0, 0, 44)]
for i, (x, y, z) in enumerate(PAWNS):
    pawn(x, y, z, i % 2)

# ---------------------------------------------------------------- light, camera, render
world = bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.008, 0.005, 1)


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


key = light("LGT_Key", "SUN", U(60, 80, 20), 1.5, (1.0, 0.86, 0.66), angle=D(14))
aim(key, U(4, 4, 60))
fill = light("LGT_Fill", "AREA", U(50, 40, 90), 60000, (0.70, 0.78, 1.0), size=70)
aim(fill, U(8, 4, 60))
for (x, y, z) in lamps:
    light("LGT_Candle", "POINT", U(x, y, z), 2600, (1.0, 0.58, 0.24), shadow_soft_size=0.4)

cam_d = bpy.data.cameras.new("CAM_BanquetBlockout")
cam_d.lens = 33
cam_d.clip_end = 900
cam = bpy.data.objects.new("CAM_BanquetBlockout", cam_d)
col.objects.link(cam)
cam.location = U(60, 44, -6)
aim(cam, U(5, 6.0, 57))
scene.camera = cam

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
scene.view_settings.exposure = -0.9
scene.render.image_settings.file_format = "PNG"
scene.render.filepath = OUT
print("OBJECTS", len(col.objects), "LAMPS", len(lamps))
bpy.ops.render.render(write_still=True)
print("RENDERED", OUT)

# Royal water garden - concept-art dressing for the last two graybox spots (2026-11-22): the island in the north-west
# lake and the east lock's water.
#
# 승규 11-22 (Unity pictures of the grey island with its glass column and of the lock's white arrows): "여기만 하면 돼",
# "전부 블렌더에서 하고 코덱스에서 이미지 받아와서 정밀 모델링 한다 블렌더에서 한 후 모델링 나한테 확인 받아". Reference
# pictures: Docs/PawnRush/ConceptArt/GardenFinish (Codex). As before every position and collider stays the v31
# graybox's (RoyalWaterGardenBuilder.cs Shell: "Garden island" r 5, top 0.6, and "Island fountain"; .Beats.cs EastLock:
# "Lock water", "Current arrow"); this only draws over them:
#   Island              a low round island: rounded cream blocks round its edge down into the lake (a moss band at the
#                       water), a gold band under the coping, a ring of kit tiles, a lawn with bushes, flowers and three
#                       slim cypresses, a two-tier fountain in the middle (its basins' water drawn; the jets are code's)
#   Lock water          a few centimetres of water over the lock's bed (x 27..35, z 10..24), running south: foam where
#                       it comes in under the dyke, in a band below each sluice gate and along both walls, and where it
#                       drops through a gold drain grate at the lock's mouth. Its vertex colours and UVs carry the flow
#                       for Unity: UV = metres (u across, v downstream), r foam, g across 0..1, b a seed
# All faces outward and no two faces in one plane (PITFALLS 38, 39).
#
# Run:  blender -b --python island_lock_sample_v1.py
# Out:  IslandLockSample_v1.blend, render_islandlock_v1_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Finish dressing"
os.environ["GC_SEED"] = "77"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403
import water_tiers as wt  # noqa: E402

LAKE = wt.MIDDLE                  # -0.55
UPPER = 3.8
ISLAND = Vector((-18.0, 16.0, 0))
R_ISLAND, ISLAND_TOP = 5.0, .6
GATES = (13.0, 17.5, 22.0)
LOCK_X0, LOCK_X1, LOCK_Z0, LOCK_Z1 = 27.0, 35.0, 10.0, 24.0

# Shared names with the other samples (one Unity material each).
M["moss"] = textured("GC | moss band", STONE_TEX, (.42, .62, .30), .75, .5)
M["channel"] = textured("GC | channel stone", STONE_TEX, (.55, .80, .84), .6, .5)
M["channel_dark"] = material("GC | channel joint", (.16, .36, .40), .8)
M["basin_water"] = material("GC | basin water", (.55, .85, .95), .05)
M["mouth"] = material("GC | nozzle dark", (.04, .05, .06), .5)
M["bronze"] = material("GC | bronze", (.62, .42, .2), .35, .85)
M["mosaic_teal"] = material("GC | mosaic teal", (.2, .62, .7), .4)
M["mosaic_deep"] = material("GC | mosaic deep", (.1, .38, .55), .4)
M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)
# New here.
M["lawn"] = textured("GC | lawn", STONE_TEX, (.40, .70, .22), .85, .5)
M["cypress"] = material("GC | cypress", (.16, .40, .16), .75)
M["cypress2"] = material("GC | cypress light", (.24, .52, .20), .75)


def blended(m, alpha):
    m.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = alpha
    for attr, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
        try:
            setattr(m, attr, value)
        except Exception:
            pass


blended(M["basin_water"], .45)

# ------------------------------------------------------------------ the context (other beats, for the renders only)
for blend, coll_name in (("PlatformsSample_v3.blend", "Platforms dressing"), ("LockDykeSample_v1.blend", "Lock dressing"),
                         ("CanalSample_v2.blend", "Canal dressing"), ("BoatsSample_v2.blend", "Boats dressing")):
    path = os.path.join(HERE, blend)
    with bpy.data.libraries.load(path) as (src, dst):
        dst.collections = [c for c in src.collections if c == coll_name]
    for c in dst.collections:
        c.name = "Context | " + coll_name
        sc.collection.children.link(c)
# The old shortcut (gone in Unity since R653) out of the platforms' context.
for ob in list(bpy.data.objects):
    if ob.name.startswith("Shortcut") or ob.name.startswith("Platform piers") or ob.name.startswith("Context lock water"):
        bpy.data.objects.remove(ob, do_unlink=True)

lake_mat = bpy.data.materials.new("Context | lake")
lake_mat.use_nodes = True
_l = lake_mat.node_tree.nodes["Principled BSDF"]
_l.inputs["Base Color"].default_value = (*srgb((.02, .55, .82)), 1)
_l.inputs["Roughness"].default_value = .06
ctx = Mesh("Context lake", CTX)
ctx.box((-46, -15, LAKE - .05), (46, 33, LAKE), lake_mat)
ctx.done()

# ================================================================== the island (GF_01_island.png)
isl = Mesh("Island stonework")
green = Mesh("Island planting")
fountain = Mesh("Island fountain")
cx, cy = ISLAND.x, ISLAND.y
M["mosaic_light"] = material("GC | mosaic light", (.6, .85, .88), .4)
M["island_tile"] = material("GC | island tile", (.97, .62, .50), .65)       # the picture's coral-peach tiles
M["island_tile2"] = material("GC | island tile warm", (.95, .56, .45), .65)

# The edge (the picture): a footing course down into the lake with a moss band at the water, a gold band over it, then
# one deep course of big rounded cream blocks to the top, a thin gold line under their rounded top edge. Outer face
# r 5.02, just proud of the collider's r 5.
isl.lathe([(0, LAKE - 1.4), (4.9, LAKE - 1.4), (4.9, ISLAND_TOP - .25), (0, ISLAND_TOP - .25)], Matrix.Translation(ISLAND), M["grout"], 64, False)
ring_blocks(isl, cx, cy, 4.3, 5.0, LAKE - 1.0, LAKE + .1, 1.7, [M["moss"]], .04)
ring_blocks(isl, cx, cy, 4.3, 5.03, LAKE + .12, -.14, 1.7, [M["stone2"], M["stone"]], .05, .5)
isl.lathe([(5.04, -.13), (5.1, -.115), (5.1, -.035), (5.04, -.02)], Matrix.Translation(ISLAND), GOLD, 96, True, True)
isl.lathe([(5.035, -.02), (5.06, -.012), (5.06, .0), (5.035, .008)], Matrix.Translation(ISLAND), GOLD, 96, True, True)
# The big blocks: 1.35 m along, 0.8 deep (r 4.22 .. 5.02), their tops rounded hard (a fat bevel), 0.6 high.
ring_blocks(isl, cx, cy, 4.22, 5.02, .01, ISLAND_TOP, 1.35, STONES, .12)
# The tile ring (r 3.45 .. 4.2): one row of separate rounded coral-peach tiles, about 0.45 m square, on a grout bed.
isl.lathe([(3.4, ISLAND_TOP - .06), (4.24, ISLAND_TOP - .06), (4.24, ISLAND_TOP - .025), (3.4, ISLAND_TOP - .025)],
          Matrix.Translation(ISLAND), M["grout"], 96, True, True)
n_t = round(2 * math.pi * 3.83 / .45)
for k in range(n_t):
    a0 = 360 * k / n_t
    isl.arc(3.47, 4.18, a0 + .45, a0 + 360 / n_t - .45, ISLAND_TOP - .06, ISLAND_TOP - .005,
            random.choice([M["island_tile"], M["island_tile2"]]), center=(cx, cy), bevel=.02, segs=2)

# The lawn: flat, just under the tiles' top (r 0 .. 3.47), the edge tucked under the tiles.
LAWN = ISLAND_TOP - .02
green.lathe([(0, LAWN - .08), (3.5, LAWN - .08), (3.5, LAWN), (0, LAWN)], Matrix.Translation(ISLAND), M["lawn"], 64, False)


# Two slim cypresses at the back (north, behind the fountain as the course sees it), as the picture's.
def cypress(m, at, height, radius):
    base = Vector(at)
    m.lathe([(0, 0), (.08, 0), (.07, .3), (0, .3)], Matrix.Translation(base), M["wood"], 8, True)
    prof = []
    for k in range(14):
        t = k / 13
        r = radius * math.sin(math.pi * (.12 + .88 * t)) ** .7 * (1 - .35 * t) if k < 13 else 0
        prof.append((max(r, .0), .22 + height * t))
    m.lathe([(0, .22)] + prof[1:], Matrix.Translation(base), M["cypress"], 14, True)
    golden = math.pi * (3 - math.sqrt(5))
    n = int(90 * height / 2.6)
    for i in range(n):
        t = (i + .5) / n
        r = radius * math.sin(math.pi * (.12 + .88 * t)) ** .7 * (1 - .35 * t) * .85
        th = golden * i
        out = Vector((math.cos(th), math.sin(th), 0))
        p = base + out * r + Vector((0, 0, .26 + height * .95 * t))
        direction = (Vector((0, 0, 1)) * .9 + out * .4).normalized()
        L = radius * random.uniform(.5, .62)
        m.leaf(p, direction, L, L * .72, random.choice([M["cypress"], M["cypress2"]]), up=out)


for ang in (122, 58):
    a = math.radians(ang)
    cypress(green, (cx + 2.75 * math.cos(a), cy + 2.75 * math.sin(a), LAWN - .02), random.uniform(2.5, 2.8), .36)

def round_bush(m, c, r):
    """The picture's clipped ball: a full green core and many small leaves laid flat over it, tips pointing down."""
    m.sphere(c, r * .97, M["leaf"], (1, 1, .9), 2)
    golden = math.pi * (3 - math.sqrt(5))
    n = 150
    for k in range(n):
        zf = 1 - (k + .5) / n * 1.75
        rr = math.sqrt(max(0.0, 1 - zf * zf))
        th = golden * k
        out = Vector((rr * math.cos(th), rr * math.sin(th), zf * .9)).normalized()
        base = c + Vector((out.x * r * .9, out.y * r * .9, out.z * r * .81))
        side = Vector((0, 0, 1)).cross(out)
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
        down = side.cross(out).normalized()
        d = (down * .8 + out * .35 + side.normalized() * random.uniform(-.3, .3)).normalized()
        L = r * random.uniform(.26, .32)
        m.leaf(base, d, L, L * .9, random.choice(LEAVES), up=out)


# Six round bushes and six flower clusters round the fountain (the picture's top view).
for k in range(6):
    a = math.radians(180 + 60 * k)
    if k in (0, 3):
        r = 2.55
    else:
        r = 2.45
    c = Vector((cx + r * math.cos(a), cy + r * math.sin(a), LAWN + .3))
    round_bush(green, c, .5)
for k in range(6):
    a = math.radians(210 + 60 * k)
    if 80 < (210 + 60 * k) % 360 < 100:
        continue        # the gap at the north between the cypresses
    r = 2.55
    c = Vector((cx + r * math.cos(a), cy + r * math.sin(a), LAWN))
    for j in range(4):
        lp = c + Vector((random.uniform(-.25, .25), random.uniform(-.25, .25), .02))
        for q in range(3):
            t = random.uniform(0, 2 * math.pi)
            green.leaf(lp, Vector((math.cos(t), math.sin(t), .35)), .2, .13, random.choice(LEAVES[:2]))
    for j in range(9):
        p = c + Vector((random.uniform(-.3, .3), random.uniform(-.3, .3), random.uniform(.1, .16)))
        green.flower(p, random.uniform(.13, .16), random.choice(FLOWERS[:4]), Vector((random.uniform(-.25, .25), random.uniform(-.25, .25), 1)))

# The fountain (the picture): a wide bowl (r 1.6) on a short round foot, a turned stem to a small upper bowl with a
# thick gold ring round its rim, a stone knob and a gold ball. The bowls' water is drawn; the jet is the game's.
F = Matrix.Translation(Vector((cx, cy, LAWN)))
fountain.lathe([(0, 0), (1.0, 0), (1.0, .06), (.92, .1), (.75, .16), (.58, .3), (.55, .36), (0, .36)], F, M["stone2"], 48, True)   # foot
fountain.lathe([(.56, .34), (.6, .38), (.6, .42), (.56, .44)], F, GOLD, 48, True, True)
fountain.lathe([(0, .42), (.7, .42), (1.05, .5), (1.4, .66), (1.62, .86), (1.66, .98), (1.6, 1.04), (1.52, 1.0),
                (1.45, .9), (1.1, .78), (0, .76)], F, M["stone"], 64, True)                                                         # lower bowl
fountain.lathe([(0, .93), (1.47, .93), (1.47, .931), (0, .931)], F, M["basin_water"], 64, False)
fountain.lathe([(0, .76), (.3, .76), (.26, .95), (.2, 1.2), (.24, 1.36), (.32, 1.42), (.26, 1.48), (0, 1.48)], F, M["stone3"], 32, True)  # stem
fountain.lathe([(0, 1.46), (.25, 1.48), (.5, 1.56), (.68, 1.7), (.72, 1.78), (.66, 1.8), (.56, 1.74), (0, 1.7)], F, M["stone"], 48, True)  # upper bowl
fountain.lathe([(0, 1.76), (.64, 1.76), (.64, 1.761), (0, 1.761)], F, M["basin_water"], 48, False)
fountain.lathe([(.7, 1.76), (.76, 1.78), (.79, 1.83), (.76, 1.88), (.7, 1.9), (.67, 1.86), (.69, 1.81)], F, GOLD, 64, True, True)  # gold ring
fountain.lathe([(0, 1.7), (.13, 1.7), (.1, 1.95), (.17, 2.02), (.2, 2.12), (.14, 2.2), (0, 2.2)], F, M["stone3"], 24, True)        # knob
fountain.sphere(F @ Vector((0, 0, 2.38)), .2, GOLD, subdiv=3)
fountain.lathe([(0, 2.55), (.04, 2.55), (.04, 2.585), (0, 2.585)], F, M["mouth"], 10, False)

# Lily pads on the lake round the island (the platforms' look).
lilies = Mesh("Island lilies")
for k in range(7):
    a = math.radians(random.uniform(0, 360))
    r = random.uniform(5.6, 7.2)
    rad = random.uniform(.35, .6)
    c = (cx + r * math.cos(a), cy + r * math.sin(a))
    notch = random.uniform(0, 2 * math.pi)
    pts = []
    for j in range(25):
        t = notch + .35 + (2 * math.pi - .7) * j / 24
        pts.append((rad * math.cos(t), rad * math.sin(t)))
    pts.append((0, 0))
    lilies.slab(pts, Matrix.Translation((c[0], c[1], LAKE + .01)), 0, .02, M["lily"])
    if k % 2 == 0:
        lilies.flower((c[0] + rad * .2, c[1], LAKE + .05), .09, M["pink"])

isl.done()
green.done()
fountain.done()
lilies.done()

# ================================================================== the lock's water
# A sheet 2.5 cm over the floor (the graybox floor's top is UPPER; the bed's slabs are 3 cm under it), 0.25 m quads,
# lifted a little where it boils up below a gate. Foam: r of the vertex colour (the Unity shader streaks it along v).
CELL = .25
nx, nz = int((LOCK_X1 - LOCK_X0) / CELL), int((LOCK_Z1 - LOCK_Z0) / CELL)
SURF = UPPER + .025


def foam_at(x, z):
    f = 0.0
    # Coming in under the dyke at the north end.
    f = max(f, math.exp(-(LOCK_Z1 - z) / .7) * .95)
    # Below (south of) each gate: a boil right under it, fading downstream over ~2 m.
    for g in GATES:
        d = (g - .3) - z
        if d > -.05:
            f = max(f, .9 * math.exp(-max(d, 0) / 1.1) * (1 if d >= 0 else .5))
        # A little piled up on the gate's north face.
        d2 = z - (g + .3)
        if 0 <= d2 < .8:
            f = max(f, .45 * (1 - d2 / .8))
    # Along the walls.
    w = min(x - LOCK_X0, LOCK_X1 - x)
    f = max(f, .6 * math.exp(-w / .22))
    # Into the drain at the mouth.
    f = max(f, .8 * math.exp(-(z - (LOCK_Z0 + .55)) / .5) if z > LOCK_Z0 + .55 else .85)
    return min(1.0, f)


def lift_at(x, z):
    h = 0.0
    for g in GATES:
        d = (g - .3) - z
        if 0 <= d < 2.2:
            h = max(h, .018 * math.sin(math.pi * min(1, d / 1.2)) * (1 - d / 2.2))
    return h


bm = bmesh.new()
uvl = bm.loops.layers.uv.new("UVMap")
col = bm.loops.layers.float_color.new("Col")
grid = []
for j in range(nz + 1):
    z = LOCK_Z0 + .6 + (LOCK_Z1 - .02 - LOCK_Z0 - .6) * j / nz      # from the drain grate's lip to the dyke's face
    row = []
    for i in range(nx + 1):
        x = LOCK_X0 + .005 + (LOCK_X1 - LOCK_X0 - .01) * i / nx
        row.append((bm.verts.new((x, z, SURF + lift_at(x, z))), x, z))
    grid.append(row)
seed = random.Random(5)
for j in range(nz):
    for i in range(nx):
        quad = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
        face = bm.faces.new([q[0] for q in quad])
        face.smooth = True
        for loop, (v, x, z) in zip(face.loops, quad):
            loop[uvl].uv = (x - LOCK_X0, LOCK_Z1 - z)                      # v downstream (south), metres
            loop[col] = (foam_at(x, z), (x - LOCK_X0) / (LOCK_X1 - LOCK_X0), seed.random() * 0 + .37, 1.0)
me = bpy.data.meshes.new("Lock water")
bm.to_mesh(me)
bm.free()


def flow_material(name):
    """A preview of the lock water's look in Unity: clear turquoise over the bed, foam (vertex r) broken into streaks
    stretched along the flow (UV v), thin bright streaks over all of it."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = .04
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Col"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
    uv = nt.nodes.new("ShaderNodeTexCoord")

    def noise(sx, sy, detail):
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (sx, sy, 1)
        nt.links.new(uv.outputs["UV"], mp.inputs["Vector"])
        tex = nt.nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = 1.0
        tex.inputs["Detail"].default_value = detail
        nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        return tex.outputs["Fac"]

    def mr(val, a, b):
        n = nt.nodes.new("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHSTEP"
        n.inputs["From Min"].default_value = a
        n.inputs["From Max"].default_value = b
        nt.links.new(val, n.inputs["Value"])
        return n.outputs["Result"]

    def op(o, a, b):
        n = nt.nodes.new("ShaderNodeMath")
        n.operation = o
        n.use_clamp = True
        for k, val in enumerate((a, b)):
            if isinstance(val, (int, float)):
                n.inputs[k].default_value = val
            else:
                nt.links.new(val, n.inputs[k])
        return n.outputs[0]

    streak = mr(noise(2.4, .16, 3), .62, .66)          # long streaks along v
    fine = mr(noise(7.0, .5, 2), .64, .67)
    foam = op("MULTIPLY", sep.outputs["Red"], op("ADD", op("MULTIPLY", mr(noise(4.0, .6, 4), .35, .6), 1.0), .15))
    white = op("MAXIMUM", foam, op("ADD", op("MULTIPLY", streak, .35), op("MULTIPLY", fine, .15)))
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["A"].default_value = (*srgb((.28, .78, .86)), 1)
    mix.inputs["B"].default_value = (*srgb((.97, .99, 1.0)), 1)
    nt.links.new(white, mix.inputs["Factor"])
    nt.links.new(mix.outputs["Result"], p.inputs["Base Color"])
    nt.links.new(op("ADD", op("MULTIPLY", white, .8), .2), p.inputs["Alpha"])
    for attr_name, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
        try:
            setattr(m, attr_name, value)
        except Exception:
            pass
    return m


me.materials.append(flow_material("GC | lock water"))
ob = bpy.data.objects.new("Lock water", me)
DRESS.objects.link(ob)

# Where it comes in: a stone sill against the dyke's face with a gold lip, a dark slot under it (z 23.82 .. 24).
lock = Mesh("Lock water works")
lock.box((LOCK_X0 + .02, 23.8, UPPER - .02), (LOCK_X1 - .02, 24.0, UPPER + .03), M["stone3"], .02)
lock.box((LOCK_X0 + .04, 23.77, UPPER + .028), (LOCK_X1 - .04, 23.82, UPPER + .036), GOLD, .008)
# Where it leaves: a gold drain grate across the mouth (z 10 .. 10.6), bars along the flow over a dark slot, flush.
lock.box((LOCK_X0 + .02, LOCK_Z0, UPPER - .1), (LOCK_X1 - .02, LOCK_Z0 + .6, UPPER - .03), M["mouth"])
lock.box((LOCK_X0 + .02, LOCK_Z0 + .58, UPPER - .03), (LOCK_X1 - .02, LOCK_Z0 + .64, UPPER + .02), GOLD, .01)
lock.box((LOCK_X0 + .02, LOCK_Z0 - .02, UPPER - .03), (LOCK_X1 - .02, LOCK_Z0 + .04, UPPER + .02), GOLD, .01)
n_bars = 26
for k in range(n_bars):
    x = LOCK_X0 + .2 + (LOCK_X1 - LOCK_X0 - .4) * k / (n_bars - 1)
    lock.box((x - .04, LOCK_Z0 + .04, UPPER - .03), (x + .04, LOCK_Z0 + .58, UPPER + .012), GOLD, .012)
lock.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((31, 15.2, UPPER), (-28.5, 3.5, 1.8), (30.5, 5, UPPER)):
    pawn(scale, px, py, pz)
scale.done()
# For the renders: the sluice gates up (open, as they are most of a cycle), so the water shows.
for ob in bpy.data.objects:
    if ob.name.startswith("Sluice gate"):
        ob.location.z += 2.8
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "IslandLockSample_v1.blend"))
shoot("islandlock_v1", "island", (-11.5, 7.5, 4.2), (-18, 16, .6), 30)
shoot("islandlock_v1", "island_platforms", (-27.5, 1.8, 4.2), (-18, 16, .8), 26)
shoot("islandlock_v1", "island_close", (-15.2, 11.8, 2.2), (-18, 16, 1.2), 32)
shoot("islandlock_v1", "island_top", (-18, 16.01, 22), (-18, 16, 0), 40)
shoot("islandlock_v1", "lock", (31, 6.5, 6.2), (31, 17, 3.8), 26)
shoot("islandlock_v1", "lock_low", (29.5, 11.5, 4.6), (31.5, 18.5, 3.8), 30)
shoot("islandlock_v1", "lock_gate", (33.5, 15.3, 5.2), (30.5, 17.5, 3.8), 30)
print("ISLAND LOCK SAMPLE DONE")

# Royal water garden - concept-art dressing for the last two graybox spots, v2 (2026-11-22): the island in the
# north-west lake and the east lock's water.
#
# 승규 11-22 (Unity pictures of the grey island with its glass column and of the lock's white arrows): "여기만 하면 돼",
# "전부 블렌더에서 하고 코덱스에서 이미지 받아와서 정밀 모델링 한다 블렌더에서 한 후 모델링 나한테 확인 받아"; on v1's
# renders: "나무 그래픽과 물 그래픽 더 좋게 해봐". Reference pictures: Docs/PawnRush/ConceptArt/GardenFinish (Codex). As
# before every position and collider stays the v31 graybox's (RoyalWaterGardenBuilder.cs Shell: "Garden island" r 5,
# top 0.6, and "Island fountain"; .Beats.cs EastLock: "Lock water", "Current arrow"); this only draws over them:
#   Island              a low round island: rounded cream blocks round its edge down into the lake (a moss band at the
#                       water), gold bands, a ring of coral-peach tiles, a lawn with six clipped bushes, flower clusters
#                       and two cypresses, a two-tier fountain in the middle
#     v2 trees          the cypresses as the garden's trees are drawn (tree_asset.py, 승규's Tripo trees): a column of
#                       rounded tufts, each under broad rounded leaves, sunlit tops lighter; a bark trunk with mossy roots
#     v2 bushes         the same rounded leaves, laid as scales over a full core, lighter on top
#     v2 fountain       water: a bell of water from the finial over the gold ball into the upper bowl, four streams
#                       from lips under its gold ring into the lower bowl, whitewater where they land. Sheets and
#                       streams carry the fall shader's data (PR_GardenFallLower via "GC | fall lower", as the weirs)
#   Lock water          a few centimetres of water over the lock's bed (x 27..35, z 10..24), running south
#     v2                its surface shaped as the water would stand: ripples stretched along the flow, a roller and a
#                       train of standing waves below each gate, water piled against each gate's north face, shock
#                       lines running in and down off the gates' slots, a hump where it comes in, a dip into the drain;
#                       whitewater (a crest of foam lumps) on each roller, along the walls and at the inflow
#                       Vertex colours and UVs carry the flow for Unity: UV = metres (u across, v downstream), r foam,
#                       g across 0..1, b a seed
# All faces outward and no two faces in one plane (PITFALLS 38, 39). The water meshes are named "Water ..." and
# "Fall ..." so handmade_pass.py leaves their vertex colours (their shaders' data) alone and Unity casts no shadow
# from them (RoyalWaterGardenBuilder.Concept.cs).
#
# Run:  blender -b --python island_lock_sample_v2.py
#       blender -b --python handmade_pass.py -- IslandLockSample_v2.blend --step 3     (no step 2: already varied)
#       blender -b --python handmade_pass.py -- IslandLockSample_v2.blend --step 4
#       GC_NEW_MATERIALS_ONLY=1 blender -b --python export_concept.py -- IslandLockSample_v2.blend "Finish dressing" GardenFinish
# Out:  IslandLockSample_v2.blend, render_islandlock_v2_*.png (next to this file); Unity: GardenFinish.fbx (R765, 2026-11-22)

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
M["mosaic_light"] = material("GC | mosaic light", (.6, .85, .88), .4)
M["crown_blue"] = material("GC | crown blue", (.3, .43, .8), .45)
M["bark"] = material("GC | bark", (.36, .17, .06), .8)                   # as tree_asset.py's
# New here.
M["lawn"] = textured("GC | lawn", STONE_TEX, (.40, .70, .22), .85, .5)
M["island_tile"] = material("GC | island tile", (.97, .62, .50), .65)       # the picture's coral-peach tiles
M["island_tile2"] = material("GC | island tile warm", (.95, .56, .45), .65)
M["cypress"] = material("GC | cypress", (.16, .40, .16), .75)              # v2: the tufts' shade
M["cypress2"] = material("GC | cypress light", (.24, .52, .20), .75)       # v2: the leaves
M["cypress3"] = material("GC | cypress sun", (.42, .68, .26), .72)         # v2: sunlit tops
M["bush_sun"] = material("GC | bush sun", (.55, .80, .26), .72)            # v2: the bushes' sunlit tops
M["whitewater"] = material("GC | whitewater", (.93, .97, 1.0), .35)        # v2: foam lumps (opaque, no glow)


def blended(m, alpha):
    m.node_tree.nodes["Principled BSDF"].inputs["Alpha"].default_value = alpha
    for attr, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
        try:
            setattr(m, attr, value)
        except Exception:
            pass


blended(M["basin_water"], .45)


def fall_material(name):
    """water_sample_v2.py's preview of PR_GardenFallSheet (the same data and sums): streaks laid along the flow (UV v =
    V_REF x travel time), air taken in as it falls (vertex g), clear at the brink."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = .1
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

    def math_node(op, a, b=None, clamp=False):
        n = nt.nodes.new("ShaderNodeMath")
        n.operation = op
        n.use_clamp = clamp
        for k, val in enumerate((a, b)):
            if val is None:
                continue
            if isinstance(val, (int, float)):
                n.inputs[k].default_value = val
            else:
                nt.links.new(val, n.inputs[k])
        return n.outputs[0]

    n1 = noise(3.5, .45, 2)
    n2 = noise(9.0, 1.2, 3)
    g = sep.outputs["Green"]
    air = math_node("MULTIPLY", math_node("ADD", math_node("SUBTRACT", math_node("MULTIPLY", g, 1.1), .12),
                                          math_node("MULTIPLY", math_node("SUBTRACT", n1, .5), .6), True), .6)
    streak = nt.nodes.new("ShaderNodeMapRange")
    streak.interpolation_type = "SMOOTHSTEP"
    streak.inputs["From Min"].default_value = .5
    streak.inputs["From Max"].default_value = .62
    nt.links.new(n2, streak.inputs["Value"])
    white = math_node("MULTIPLY", air, math_node("ADD", math_node("MULTIPLY", streak.outputs["Result"], .7), .45), True)
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.inputs["A"].default_value = (*srgb((.70, .90, 1.0)), 1)
    mix.inputs["B"].default_value = (*srgb((.96, .99, 1.0)), 1)
    nt.links.new(white, mix.inputs["Factor"])
    nt.links.new(mix.outputs["Result"], p.inputs["Base Color"])
    nt.links.new(math_node("ADD", math_node("MULTIPLY", white, .6), .3, True), p.inputs["Alpha"])
    for attr_name, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
        try:
            setattr(m, attr_name, value)
        except Exception:
            pass
    return m


M["fall_lower"] = fall_material("GC | fall lower")       # Unity: PR_GardenFallLower (as the weirs)

# ------------------------------------------------------------------ the context (other beats, for the renders only)
for blend, coll_name in (("PlatformsSample_v3.blend", "Platforms dressing"), ("LockDykeSample_v1.blend", "Lock dressing"),
                         ("CanalSample_v2.blend", "Canal dressing"), ("BoatsSample_v2.blend", "Boats dressing")):
    path = os.path.join(HERE, blend)
    with bpy.data.libraries.load(path) as (src, dst):
        dst.collections = [c for c in src.collections if c == coll_name]
    for c in dst.collections:
        c.name = "Context | " + coll_name
        sc.collection.children.link(c)
# The old shortcut (gone in Unity since R653) out of the platforms' context, and the lock's graybox water.
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


# ------------------------------------------------------------------ v2 shared pieces
def lobe(m, base, direction, length, width, mat, up):
    """A broad rounded leaf (the garden trees' look): round-tipped, cupped along its midrib, its face toward `up`."""
    d = Vector(direction).normalized()
    side = d.cross(Vector(up))
    if side.length < 1e-4:
        side = d.orthogonal()
    side.normalize()
    normal = side.cross(d).normalized()
    b = Vector(base)
    ts = (0., .22, .5, .78, 1.)
    cup = width * .14
    spine = [m.bm.verts.new(b + d * length * t + normal * cup * math.sin(math.pi * t)) for t in ts]
    left, right = {}, {}
    for i, t in enumerate(ts[1:-1], 1):
        w = width * .5 * math.sin(math.pi * t ** .85) ** .55
        mid = b + d * length * t - normal * cup * .35
        left[i] = m.bm.verts.new(mid - side * w)
        right[i] = m.bm.verts.new(mid + side * w)
    faces = []
    for edge in (left, right):
        faces.append((spine[0], edge[1], spine[1]))
        for i in (1, 2):
            faces.append((spine[i], edge[i], edge[i + 1], spine[i + 1]))
        faces.append((spine[3], edge[3], spine[4]))
    made = list(spine) + list(left.values()) + list(right.values())
    i_mat = m.mi(mat)
    for vs in faces:
        f = m.bm.faces.new(vs)
        f.normal_update()
        if f.normal.dot(normal) < 0:
            f.normal_flip()
        f.material_index = i_mat
        f.smooth = True
        m.no_recalc.add(f)
    return made


def scale_frame(out):
    """Directions laid as scales over a round surface facing `out`: (down along it, across it)."""
    side = Vector((0, 0, 1)).cross(out)
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    return side.cross(out).normalized(), side


def sunlit(out, light, mid, shade, lit_from=.35, shade_below=-.35):
    """The garden trees' painting: tops facing the sky lighter, undersides darker."""
    if out.z > lit_from + random.uniform(-.15, .15):
        return light
    if out.z < shade_below:
        return shade
    return mid


def puff(m, c, r, mat, flat=.55, subdiv=1):
    """A foam lump: a squashed, slightly uneven ball."""
    m.sphere(c, r, mat, (random.uniform(1.0, 1.5), random.uniform(.8, 1.1), flat), subdiv)


# ================================================================== the island (GF_01_island.png)
isl = Mesh("Island stonework")
green = Mesh("Island planting")
fountain = Mesh("Island fountain")
cx, cy = ISLAND.x, ISLAND.y

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


# v2: the cypress as the garden's trees: a column of rounded tufts, each under broad rounded leaves.
def cypress(m, at, height, radius):
    base = Vector(at)
    # The trunk: a bark lathe with a root flare, moss on the roots' tops (as tree_asset.py paints its roots).
    m.lathe([(0, 0), (.2, 0), (.17, .03), (.11, .09), (.085, .2), (.07, .55), (0, .55)], Matrix.Translation(base), M["bark"], 10, True)
    for k in range(5):
        a = 2 * math.pi * (k + random.random() * .4) / 5
        m.sphere(base + Vector((.13 * math.cos(a), .13 * math.sin(a), .03)), .055, M["moss"], (1.6, .9, .55), 1)
    z0 = .38

    def R(t):
        """The crown's radius a fraction t up it: full low down, tapering to a rounded point."""
        return radius * math.sin(math.pi * (.06 + .94 * min(t, 1))) ** .5 * (1 - .5 * t) + .02

    # A dark core so nothing shows through between the tufts.
    prof = [(R(k / 12) * .7, z0 + height * k / 12) for k in range(13)]
    prof[-1] = (0, z0 + height)
    m.lathe([(0, z0 - .02)] + prof, Matrix.Translation(base), M["cypress"], 12, True)
    golden = math.pi * (3 - math.sqrt(5))
    rings = max(6, int(height / .21))
    k_all = 0
    for ri in range(rings):
        t = (ri + .5) / rings
        z = z0 + height * t
        rr = R(t)
        n = max(2, round(2 * math.pi * rr * .72 / .26))
        for j in range(n):
            th = 2 * math.pi * (j + (ri % 2) * .5) / n + random.uniform(-.15, .15)
            out = Vector((math.cos(th), math.sin(th), 0))
            tr = rr * random.uniform(.36, .44) + .03
            c = base + out * rr * .66 + Vector((0, 0, z + random.uniform(-.04, .04)))
            m.sphere(c, tr, M["cypress2"], (1, 1, .85), 1)
            # Six sprays over the tuft's outer half, pointing up as a cypress's do (a flame-like texture), sunlit ones
            # lighter.
            for q in range(6):
                k_all += 1
                phi = golden * k_all
                zf = random.uniform(-.35, .8)
                lat = math.sqrt(max(0., 1 - zf * zf))
                o = (out * .8 + Vector((-out.y, out.x, 0)) * math.sin(phi) * .7).normalized() * lat + Vector((0, 0, zf))
                o.normalize()
                p = c + o * tr * .7
                down, side = scale_frame(o)
                d = (Vector((0, 0, 1)) * .85 + o * .5 + side * random.uniform(-.3, .3)).normalized()
                L = tr * random.uniform(.62, .78)
                lobe(m, p, d, L, L * .7, sunlit(o, M["cypress3"], M["cypress2"], M["cypress"]), o)
    # The tip: a little upright tuft.
    tip = base + Vector((0, 0, z0 + height + .02))
    m.sphere(tip - Vector((0, 0, .1)), .09, M["cypress2"], (1, 1, 1.4), 1)
    for q in range(4):
        a = 2 * math.pi * q / 4 + .4
        o = Vector((math.cos(a), math.sin(a), .9)).normalized()
        lobe(m, tip - Vector((0, 0, .16)) + o * .05, Vector((o.x * .3, o.y * .3, 1)), .2, .14, M["cypress3"], o)


for ang in (122, 58):
    a = math.radians(ang)
    cypress(green, (cx + 2.75 * math.cos(a), cy + 2.75 * math.sin(a), LAWN - .02), random.uniform(2.55, 2.8), .4)


# v2: the clipped ball under the same broad rounded leaves, lighter on top.
def round_bush(m, c, r):
    m.sphere(c, r * .95, M["leaf"], (1, 1, .9), 2)
    golden = math.pi * (3 - math.sqrt(5))
    n = 95
    for k in range(n):
        zf = 1 - (k + .5) / n * 1.7
        rr = math.sqrt(max(0.0, 1 - zf * zf))
        th = golden * k
        out = Vector((rr * math.cos(th), rr * math.sin(th), zf * .9)).normalized()
        base = c + Vector((out.x * r * .88, out.y * r * .88, out.z * r * .8))
        down, side = scale_frame(out)
        d = (down * .8 + out * .3 + side * random.uniform(-.3, .3)).normalized()
        L = r * random.uniform(.34, .4)
        lobe(m, base, d, L, L * .95, sunlit(out, M["bush_sun"], M["leaf2"], M["leaf"], .45, -.2), out)


# Six round bushes and six flower clusters round the fountain (the picture's top view).
for k in range(6):
    a = math.radians(180 + 60 * k)
    r = 2.55 if k in (0, 3) else 2.45
    c = Vector((cx + r * math.cos(a), cy + r * math.sin(a), LAWN + .3))
    round_bush(green, c, .5)
for k in range(6):
    a = math.radians(210 + 60 * k)
    if 80 < (210 + 60 * k) % 360 < 100:
        continue        # the gap at the north between the cypresses
    r = 2.55
    c = Vector((cx + r * math.cos(a), cy + r * math.sin(a), LAWN))
    for j in range(5):
        lp = c + Vector((random.uniform(-.25, .25), random.uniform(-.25, .25), .02))
        for q in range(3):
            t = random.uniform(0, 2 * math.pi)
            lobe(green, lp, Vector((math.cos(t), math.sin(t), .3)), .2, .16, random.choice(LEAVES[:2]), Vector((0, 0, 1)))
    for j in range(9):
        p = c + Vector((random.uniform(-.3, .3), random.uniform(-.3, .3), random.uniform(.1, .16)))
        green.flower(p, random.uniform(.13, .16), random.choice(FLOWERS[:4]), Vector((random.uniform(-.25, .25), random.uniform(-.25, .25), 1)))

# The fountain (the picture): a wide bowl (r 1.6) on a short round foot, a turned stem to a small upper bowl with a
# thick gold ring round its rim, a stone knob and a gold ball.
F = Matrix.Translation(Vector((cx, cy, LAWN)))
fountain.lathe([(0, 0), (1.0, 0), (1.0, .06), (.92, .1), (.75, .16), (.58, .3), (.55, .36), (0, .36)], F, M["stone2"], 48, True)   # foot
fountain.lathe([(.56, .34), (.6, .38), (.6, .42), (.56, .44)], F, GOLD, 48, True, True)
fountain.lathe([(0, .42), (.7, .42), (1.05, .5), (1.4, .66), (1.62, .86), (1.66, .98), (1.6, 1.04), (1.52, 1.0),
                (1.45, .9), (1.1, .78), (0, .76)], F, M["stone"], 64, True)                                                         # lower bowl
LOWER_W, UPPER_W = .93, 1.76
fountain.lathe([(0, LOWER_W), (1.47, LOWER_W), (1.47, LOWER_W + .001), (0, LOWER_W + .001)], F, M["basin_water"], 64, False)
fountain.lathe([(0, .76), (.3, .76), (.26, .95), (.2, 1.2), (.24, 1.36), (.32, 1.42), (.26, 1.48), (0, 1.48)], F, M["stone3"], 32, True)  # stem
fountain.lathe([(0, 1.46), (.25, 1.48), (.5, 1.56), (.68, 1.7), (.72, 1.78), (.66, 1.8), (.56, 1.74), (0, 1.7)], F, M["stone"], 48, True)  # upper bowl
fountain.lathe([(0, UPPER_W), (.64, UPPER_W), (.64, UPPER_W + .001), (0, UPPER_W + .001)], F, M["basin_water"], 48, False)
fountain.lathe([(.7, 1.76), (.76, 1.78), (.79, 1.83), (.76, 1.88), (.7, 1.9), (.67, 1.86), (.69, 1.81)], F, GOLD, 64, True, True)  # gold ring
fountain.lathe([(0, 1.7), (.13, 1.7), (.1, 1.95), (.17, 2.02), (.2, 2.12), (.14, 2.2), (0, 2.2)], F, M["stone3"], 24, True)        # knob
fountain.sphere(F @ Vector((0, 0, 2.38)), .2, GOLD, subdiv=3)
NOZZLE = 2.585
fountain.lathe([(0, NOZZLE - .035), (.045, NOZZLE - .035), (.045, NOZZLE), (0, NOZZLE)], F, M["mouth"], 10, False)
# v2: four stone lips under the gold ring (45, 135, 225, 315 degrees) the upper bowl spills through, gold-tipped.
SPOUTS = (45, 135, 225, 315)
LIP_IN, LIP_OUT, LIP_FLOOR = .6, .9, 1.7
for deg in SPOUTS:
    rot = F @ Matrix.Rotation(math.radians(deg), 4, "Z")
    for vs in (fountain.box((LIP_IN, -.1, LIP_FLOOR - .07), (LIP_OUT, .1, LIP_FLOOR), M["stone3"], .02),
               fountain.box((LIP_IN, -.1, LIP_FLOOR), (LIP_OUT, -.075, LIP_FLOOR + .06), M["stone3"], .01),
               fountain.box((LIP_IN, .075, LIP_FLOOR), (LIP_OUT, .1, LIP_FLOOR + .06), M["stone3"], .01),
               fountain.box((LIP_OUT - .03, -.105, LIP_FLOOR - .075), (LIP_OUT + .01, .105, LIP_FLOOR + .065), GOLD, .01)):
        for v in vs:
            v.co = rot @ v.co


# ------------------------------------------------------------------ v2: the fountain's water (the fall shader's sheets)
def water_sheet(name, rows, mat, seed, loop=False):
    """A falling sheet or stream from rows of (position, metres across, across 0..1, seconds of travel, progress 0..1),
    a = 0 for a sheet, 1 for a round stream (PR_GardenFallSheet's data, as water_sample_v2.fall_sheet)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    col = bm.loops.layers.float_color.new("Col")
    grid = [[(bm.verts.new(p), u, a, t, g) for p, u, a, t, g in row] for row in rows]
    stream = 1.0 if loop else 0.0
    for r0, r1 in zip(grid, grid[1:]):
        for j in range(len(r0) - 1):
            quad = (r0[j], r1[j], r1[j + 1], r0[j + 1])
            face = bm.faces.new([q[0] for q in quad])
            face.smooth = True
            for lp, q in zip(face.loops, quad):
                lp[uv].uv = (q[1], wt.V_REF * q[3])
                lp[col] = (q[2], q[4], seed, stream)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    DRESS.objects.link(ob)
    return ob


G = 9.81
# The bell: water leaves the nozzle 20 degrees up at 1.2 m/s, all round, over the gold ball into the upper bowl.
V0, A0 = 1.2, math.radians(20)
vr, vz = V0 * math.cos(A0), V0 * math.sin(A0)
t_land = (vz + math.sqrt(vz * vz + 2 * G * (NOZZLE - UPPER_W + .005))) / G
SEG = 56
rows = []
for k in range(15):
    t = t_land * k / 14
    r = .045 + vr * t
    z = NOZZLE + vz * t - G * t * t / 2
    s = k / 14
    row = []
    for j in range(SEG + 1):
        a = 2 * math.pi * j / SEG
        # A real bell is not a perfect dome: the sheet gathers into soft ribs as it spreads, more as it falls, and its
        # foot breaks unevenly where it meets the water.
        rib = 1 + .07 * s ** 1.3 * math.sin(16 * a + 2.5 * math.sin(5 * a)) + .03 * s * math.sin(7 * a + 1.3)
        dz = -.025 * s ** 3 * (.5 + .5 * math.sin(11 * a + 2.0)) if k < 14 else 0
        row.append((F @ Vector((r * rib * math.cos(a), r * rib * math.sin(a), z + dz)), j / SEG * 2 * math.pi * .3, .5, t, s))    # r .5: no frayed sides on a ring (PR_GardenFallSheet frays r 0 and 1)
    rows.append(row)
water_sheet("Fall fountain bell", rows, M["fall_lower"], .31)
BELL_R = .045 + vr * t_land

# The four streams: out of the lips at 0.55 m/s, falling into the lower bowl; round, 4.5 x 3 cm.
VS = .55
t_s = math.sqrt(2 * (LIP_FLOOR + .03 - LOWER_W) / G)
for i, deg in enumerate(SPOUTS):
    rot = F @ Matrix.Rotation(math.radians(deg), 4, "Z")
    rows = []
    for k in range(13):
        t = t_s * k / 12
        x = LIP_OUT + .01 + VS * t
        z = LIP_FLOOR + .03 - G * t * t / 2
        vel = Vector((VS, 0, -G * t)).normalized()
        nrm = Vector((-vel.z, 0, vel.x))         # in the stream's plane, square to it
        w = .045 * (1 - .25 * k / 12)          # it thins as it speeds up
        h = .03 * (1 - .2 * k / 12)
        row = []
        for j in range(9):
            a = 2 * math.pi * j / 8
            p = Vector((x, 0, z)) + Vector((0, 1, 0)) * w * math.cos(a) + nrm * h * math.sin(a)
            row.append((rot @ p, j / 8 * .2, j / 8, t, k / 12))
        rows.append(row)
    water_sheet("Fall fountain stream %d" % (i + 1), rows, M["fall_lower"], .17 + .19 * i, loop=True)

# Whitewater where they land: a ring of small lumps round the bell's foot, a few round each stream's.
foam = Mesh("Fountain whitewater")
for k in range(40):
    a = 2 * math.pi * (k + random.random() * .6) / 40
    rr = BELL_R + random.uniform(-.02, .04)
    puff(foam, F @ Vector((rr * math.cos(a), rr * math.sin(a), UPPER_W + .005)), random.uniform(.022, .04), M["whitewater"], .6, 2)
for deg in SPOUTS:
    rot = F @ Matrix.Rotation(math.radians(deg), 4, "Z")
    land = LIP_OUT + .01 + VS * t_s
    for k in range(14):
        a = random.uniform(0, 2 * math.pi)
        rr = random.uniform(0, .07) ** .8
        size = .045 - .25 * rr
        puff(foam, rot @ Vector((land + rr * math.cos(a), rr * math.sin(a), LOWER_W + .004)), size * random.uniform(.6, 1.0), M["whitewater"], .6, 2)

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
foam.done()
lilies.done()

# ================================================================== the lock's water
# A sheet 2.5 cm over the floor (the graybox floor's top is UPPER; the bed's slabs are 3 cm under it), 0.2 m quads,
# shaped as the running water stands (surf_h). Foam: r of the vertex colour (the Unity shader streaks it along v).
CELL = .2
nx, nz = int((LOCK_X1 - LOCK_X0) / CELL), int((LOCK_Z1 - LOCK_Z0) / CELL)
SURF = UPPER + .025
SHOCK = math.tan(math.radians(22))       # the shock lines off the gates' slots run in at 22 degrees


def surf_h(x, z):
    """The surface's height over SURF where the water runs south at 1.5 m/s."""
    h = .004 * math.sin(2 * math.pi * (z / .55 + .25 * math.sin(x * 1.3 + z * .4)))
    h += .003 * math.sin(2 * math.pi * (z / .31 - x * .21) + 1.7)
    h += .03 * math.exp(-(LOCK_Z1 - z) / .35)                       # the hump where it comes in
    for g in GATES:
        d = (g - .32) - z                                          # downstream of the gate's south face
        if d >= 0:
            h += .05 * math.exp(-((d - .35) / .22) ** 2)           # the roller
            if d > .6:
                h += .012 * math.cos(2 * math.pi * (d - .6) / .85) * math.exp(-(d - .6) / 1.4)   # standing waves
            for wx, sgn in ((LOCK_X0, 1), (LOCK_X1, -1)):
                off = sgn * (x - wx) - d * SHOCK
                h += .012 * math.exp(-(off / .09) ** 2) * math.exp(-d / 2.5)
        u = z - (g + .32)
        if u >= 0:
            h += .018 * math.exp(-u / .45)                         # piled against the gate's north face
    if z < LOCK_Z0 + 1.2:
        h -= .02 * (1 - (z - LOCK_Z0 - .6) / .6)                     # drawn down into the drain
    return h


def foam_at(x, z):
    f = .95 * math.exp(-(LOCK_Z1 - z) / .6)
    for g in GATES:
        d = (g - .32) - z
        if d >= 0:
            f = max(f, math.exp(-((d - .3) / .35) ** 2), .75 * math.exp(-d / 1.3))
            for wx, sgn in ((LOCK_X0, 1), (LOCK_X1, -1)):
                off = sgn * (x - wx) - d * SHOCK
                f = max(f, .7 * math.exp(-(off / .12) ** 2) * math.exp(-d / 3))
        u = z - (g + .32)
        if u >= 0:
            f = max(f, .5 * math.exp(-u / .35))
    w = min(x - LOCK_X0, LOCK_X1 - x)
    f = max(f, .65 * math.exp(-w / .18))
    if z < LOCK_Z0 + 1.3:
        f = max(f, .9 * (1 - (z - LOCK_Z0 - .6) / .7))
    return min(1.0, f)


bm = bmesh.new()
uvl = bm.loops.layers.uv.new("UVMap")
col = bm.loops.layers.float_color.new("Col")
grid = []
for j in range(nz + 1):
    z = LOCK_Z0 + .6 + (LOCK_Z1 - .02 - LOCK_Z0 - .6) * j / nz      # from the drain grate's lip to the dyke's face
    row = []
    for i in range(nx + 1):
        x = LOCK_X0 + .005 + (LOCK_X1 - LOCK_X0 - .01) * i / nx
        row.append((bm.verts.new((x, z, SURF + surf_h(x, z))), x, z))
    grid.append(row)
for j in range(nz):
    for i in range(nx):
        quad = (grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])
        face = bm.faces.new([q[0] for q in quad])
        face.smooth = True
        for loop, (v, x, z) in zip(face.loops, quad):
            loop[uvl].uv = (x - LOCK_X0, LOCK_Z1 - z)                      # v downstream (south), metres
            loop[col] = (foam_at(x, z), (x - LOCK_X0) / (LOCK_X1 - LOCK_X0), .37, 1.0)
me = bpy.data.meshes.new("Water lock")
bm.to_mesh(me)
bm.free()


def flow_material(name):
    """A preview of the lock water's look in Unity: clear turquoise the bed shows through, sun caustics dancing on it,
    crisp foam (vertex r broken by noise), thin bright streaks stretched along the flow (UV v), a brighter edge where
    it is seen flat (fresnel)."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = .03
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_name = "Col"
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(attr.outputs["Color"], sep.inputs["Color"])
    uv = nt.nodes.new("ShaderNodeTexCoord")

    def mapped(sx, sy):
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (sx, sy, 1)
        nt.links.new(uv.outputs["UV"], mp.inputs["Vector"])
        return mp.outputs["Vector"]

    def noise(sx, sy, detail):
        tex = nt.nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = 1.0
        tex.inputs["Detail"].default_value = detail
        nt.links.new(mapped(sx, sy), tex.inputs["Vector"])
        return tex.outputs["Fac"]

    def mr(val, a, b):
        n = nt.nodes.new("ShaderNodeMapRange")
        n.interpolation_type = "SMOOTHSTEP"
        n.inputs["From Min"].default_value = a
        n.inputs["From Max"].default_value = b
        if isinstance(val, (int, float)):
            n.inputs["Value"].default_value = val
        else:
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

    # Caustics: the edges of a Voronoi pattern (about 0.45 m cells), wobbled by noise, stretched a little with the flow.
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    try:
        vor.feature = "DISTANCE_TO_EDGE"
    except Exception:
        pass
    vor.inputs["Scale"].default_value = 1.0
    warp = nt.nodes.new("ShaderNodeVectorMath")
    warp.operation = "ADD"
    nt.links.new(mapped(3.4, 2.5), warp.inputs[0])
    wn = nt.nodes.new("ShaderNodeTexNoise")
    wn.inputs["Scale"].default_value = 1.0
    nt.links.new(mapped(1.1, .8), wn.inputs["Vector"])
    wsc = nt.nodes.new("ShaderNodeVectorMath")
    wsc.operation = "SCALE"
    nt.links.new(wn.outputs["Color"], wsc.inputs[0])
    wsc.inputs["Scale"].default_value = .35
    nt.links.new(wsc.outputs["Vector"], warp.inputs[1])
    nt.links.new(warp.outputs["Vector"], vor.inputs["Vector"])
    caustic = op("SUBTRACT", 1.0, mr(vor.outputs["Distance"], .0, .045))
    # Foam: the vertex foam broken into crisp patches; streaks: thin lines along the flow.
    patches = mr(op("ADD", op("MULTIPLY", noise(3.2, 1.1, 4), .9), op("MULTIPLY", sep.outputs["Red"], .75)), 1.02, 1.08)
    streak = op("MULTIPLY", mr(noise(2.6, .14, 3), .63, .655), op("ADD", op("MULTIPLY", sep.outputs["Red"], .8), .35))
    white = op("MAXIMUM", patches, op("MULTIPLY", streak, .85))
    fres = nt.nodes.new("ShaderNodeLayerWeight")
    fres.inputs["Blend"].default_value = .25
    water = nt.nodes.new("ShaderNodeMix")
    water.data_type = "RGBA"
    water.inputs["A"].default_value = (*srgb((.16, .70, .80)), 1)
    water.inputs["B"].default_value = (*srgb((.62, .95, 1.0)), 1)
    nt.links.new(op("MAXIMUM", op("MULTIPLY", caustic, .5), op("MULTIPLY", fres.outputs["Facing"], .6)), water.inputs["Factor"])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    nt.links.new(water.outputs["Result"], mix.inputs["A"])
    mix.inputs["B"].default_value = (*srgb((.97, .99, 1.0)), 1)
    nt.links.new(white, mix.inputs["Factor"])
    nt.links.new(mix.outputs["Result"], p.inputs["Base Color"])
    alpha = op("MAXIMUM", op("ADD", op("ADD", .18, op("MULTIPLY", caustic, .18)), op("MULTIPLY", fres.outputs["Facing"], .35)),
               op("MULTIPLY", white, .95))
    nt.links.new(alpha, p.inputs["Alpha"])
    for attr_name, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
        try:
            setattr(m, attr_name, value)
        except Exception:
            pass
    return m


me.materials.append(flow_material("GC | lock water"))
ob = bpy.data.objects.new("Water lock", me)
DRESS.objects.link(ob)

# v2: whitewater - a crest of foam lumps on each gate's roller, a fringe along both walls, a boil at the inflow.
white = Mesh("Lock whitewater")


def froth(m, x0, x1, zc, spread, big, step=.12, per=2, gaps=.18):
    """A band of froth across the lock at zc: overlapping lumps, biggest on the band's line and smaller the further off
    it (a normal spread), the band broken in a few places so it does not read as a row of beads."""
    x = x0
    while x < x1:
        if random.random() < gaps:          # a gap in the froth
            x += random.uniform(.15, .4)
            continue
        for _ in range(per):
            off = random.gauss(0, spread)
            z = zc + off
            size = big * math.exp(-(off / (spread * 1.6)) ** 2) * random.uniform(.55, 1.0)
            if size < .018:
                continue
            xx = x + random.uniform(-.05, .05)
            puff(m, (xx, z, SURF + surf_h(xx, z) + size * .2), size, M["whitewater"], random.uniform(.35, .55))
        x += random.uniform(step * .7, step * 1.3)


for g in GATES:
    zc = g - .32 - .35
    froth(white, LOCK_X0 + .08, LOCK_X1 - .08, zc, .16, .1)          # the roller's crest
    froth(white, LOCK_X0 + .2, LOCK_X1 - .2, zc - .75, .25, .045, .3, 1, .45)   # broken froth trailing off
# Along both walls: lumps drawn out along the wall, where the water rubs it.
for wx, sgn in ((LOCK_X0, 1), (LOCK_X1, -1)):
    z = LOCK_Z0 + .8
    while z < LOCK_Z1 - .3:
        if all(abs(z - g) > .45 for g in GATES):
            x = wx + sgn * random.uniform(.04, .1)
            size = random.uniform(.03, .06)
            white.sphere((x, z, SURF + surf_h(x, z) + size * .15), size, M["whitewater"], (.7, random.uniform(1.6, 2.4), .4), 1)
        z += random.uniform(.12, .26)
froth(white, LOCK_X0 + .06, LOCK_X1 - .06, LOCK_Z1 - .2, .1, .085, .12, 2, .1)   # the boil where it comes in
white.done()

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
for o in bpy.data.objects:
    if o.name.startswith("Sluice gate"):
        o.location.z += 2.8
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "IslandLockSample_v2.blend"))
shoot("islandlock_v2", "island", (-11.5, 7.5, 4.2), (-18, 16, .6), 30)
shoot("islandlock_v2", "island_platforms", (-27.5, 1.8, 4.2), (-18, 16, .8), 26)
shoot("islandlock_v2", "fountain", (-15.6, 12.2, 2.6), (-18, 16, 1.6), 34)
shoot("islandlock_v2", "trees", (-15.4, 13.6, 2.0), (-18.6, 18.3, 1.9), 34)
shoot("islandlock_v2", "lock", (31, 6.5, 6.2), (31, 17, 3.8), 26)
shoot("islandlock_v2", "lock_low", (29.5, 11.0, 4.6), (31.5, 18.5, 3.8), 30)
shoot("islandlock_v2", "lock_gate", (33.6, 14.2, 5.0), (30.8, 16.6, 3.8), 30)
print("ISLAND LOCK SAMPLE DONE")

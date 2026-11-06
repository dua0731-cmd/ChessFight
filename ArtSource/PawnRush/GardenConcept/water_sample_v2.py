# Royal water garden - the water in three tiers and the falls between them (R439; v2 R441), from water_tiers.py.
#
# v2 (승규 11-05, "층이 더 확실히 보이게 좀 더 높게 설정해주고 발판이 물에 잠기지 않게 해"): the same build from R441's
# heights - the reservoir 3.0, the lake -0.55 (under P1's feet), the moat -1.9 - so the steps are 3.45 m and 1.31 m.
#
# 승규 11-05: "물을 초반 중반 후반 으로 층으로 나누어서 물 높이를 높여 층마다 높이를 나눈 후 물이 떨어지게 수학적으로
# 자연스럽게 계산해". The single water sheet (y -1.7 under the whole garden) becomes three level pools - the reservoir
# (1.95, as it was), the platform lake (-0.35) and the plaza moat (-1.2) - with the water stepping down between them:
#   the spillway   the reservoir's south wall west of the quay (x -26 .. -10.4): four bays between rusticated piers
#                  with gold finials, a stone crest 0.20 m under the reservoir; the water draws down over it and falls
#                  2.2 m into the lake
#   the weirs      on the canal walk's line (z -15.5), east of the walk to the garden's east wall (six bays) and west
#                  of the canal's tall wall (two bays); the lake runs over them 0.086 m deep and falls 0.81 m into the moat
# Every falling sheet is the water's top surface computed parcel by parcel (water_tiers.profile): the drawdown to
# critical depth on the crest, then a projectile from the brink until it is under the pool below. Its vertex colours
# and UVs are what the garden's fall shader reads (PR_GardenFallSheet: r across, g brink -> pool, b a seed per bay, a 0
# for a curtain; UV = metres across, V_REF x seconds of travel, so its streaks move with the water).
# The other beats' dressing is loaded round it for the renders (not exported from here).
#
# Run:  blender -b --python water_sample_v2.py
# Out:  WaterSample_v2.blend, render_water_v2_*.png (next to this file)

import os
import sys

os.environ["GC_DRESS"] = "Water dressing"
os.environ["GC_SEED"] = "66"
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gc_kit import *  # noqa: E402,F401,F403
import water_tiers as wt  # noqa: E402

Q, FALLS = wt.solve()
for name, f in FALLS.items():
    print("FALL %-9s crest %.3f brink %.3f drop %.2f throw %.2f q %.4f" % (name, f["crest"], f["brink"], f["H"], f["throw"], f["q"]))


def tier_material(name, rgb, rough):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*srgb(rgb), 1)
    p.inputs["Roughness"].default_value = rough
    m.diffuse_color = (*srgb(rgb), 1)
    return m


M["tier"] = tier_material("GC | tier water", (.03, .52, .80), .05)      # Unity: PR_GardenWater (the garden's water)


def fall_material(name):
    """A preview of PR_GardenFallSheet's look, from the same data and the same sums: noise streaks laid along the
    flow (UV v = V_REF x travel time, so they stretch as the water speeds up), air taken in as it falls
    (air = clamp(1.1 g - 0.12 + 0.6 (n1 - 0.5)) x 0.6, white = air (0.45 + 0.7 streak)), clear at the brink."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    p = nt.nodes["Principled BSDF"]
    p.inputs["Roughness"].default_value = .1
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
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


M["fall_upper"] = fall_material("GC | fall upper")       # Unity: PR_GardenFallUpper (the spillway's 2.2 m)
M["fall_lower"] = fall_material("GC | fall lower")       # Unity: PR_GardenFallLower (the weirs' 0.81 m)

# ------------------------------------------------------------------ the other beats, for the renders (not exported)
CONTEXT_SAMPLES = (("PlazaSample_v4.blend", "Plaza dressing"), ("CanalSample_v2.blend", "Canal dressing"),
                   ("PlatformsSample_v3.blend", "Platforms dressing"), ("LockDykeSample_v1.blend", "Lock dressing"),
                   ("BoatsSample_v1.blend", "Boats dressing"))
for blend, coll_name in CONTEXT_SAMPLES:
    path = os.path.join(HERE, blend)
    with bpy.data.libraries.load(path) as (src, dst):
        dst.collections = [c for c in src.collections if c == coll_name]
    for c in dst.collections:
        c.name = "Context | " + coll_name
        sc.collection.children.link(c)

ctx = Mesh("Context blockout", CTX)
ctx.box((-44, 33, DEEP), (-26, 48.6, 3.8), M["clay"])            # the terraces by the reservoir
ctx.box((26, 33, DEEP), (44, 48.6, 3.8), M["clay"])
ctx.box((-14, 48.6, DEEP), (14, 60.5, 3.8), M["clay"])           # the gate yard
ctx.box((-44, 48.6, DEEP), (-14.3, 59, 3.8), M["clay"])
ctx.box((14.3, 48.6, DEEP), (44, 59, 3.8), M["clay"])
ctx.box((-48.5, -63, DEEP), (-46.5, 62, 7), M["clay"])            # the garden's walls
# (the east wall left out: the side view looks in from there)
ctx.lathe([(0, DEEP), (5, DEEP), (5, .6), (0, .6)], Matrix.Translation((-18, 16, 0)), M["clay"], 40)   # the island
ctx.done()
res = Mesh("Context reservoir", CTX)
res.box((-26, 33, wt.LATE - .05), (26, 48.6, wt.LATE), M["tier"])
res.done()

# ------------------------------------------------------------------ the pools: level sheets on a ~1 m grid
# The garden's water shader moves the vertices in its swell, so the sheets keep a fine grid. Each ends at the crest's
# upstream edge (the falling sheet goes on from there) or under the weir's downstream face; the sheets are never in
# one plane with another surface (PITFALLS 39).


def lines(a, b, step, extra=()):
    out = set()
    n = max(1, int(round((b - a) / step)))
    for i in range(n + 1):
        out.add(round(a + (b - a) * i / n, 4))
    for e in extra:
        if a - 1e-6 <= e <= b + 1e-6:
            out.add(round(e, 4))
    return sorted(out)


def sheet(name, y, xs, zs, keep):
    m = Mesh(name)
    verts = {}

    def v(x, z):
        if (x, z) not in verts:
            verts[(x, z)] = m.bm.verts.new((x, z, y))
        return verts[(x, z)]

    i = m.mi(M["tier"])
    for x0, x1 in zip(xs, xs[1:]):
        for z0, z1 in zip(zs, zs[1:]):
            if not keep((x0 + x1) / 2, (z0 + z1) / 2):
                continue
            f = m.bm.faces.new((v(x0, z0), v(x1, z0), v(x1, z1), v(x0, z1)))     # counter-clockwise from above: up
            f.material_index = i
            m.no_recalc.add(f)
            m.fixed[f] = {vv: (vv.co.x / 10, vv.co.y / 10) for vv in f.verts}
    m.done()


sp, we, ww = FALLS["spillway"], FALLS["weir E"], FALLS["weir W"]
EDGE = .05                                   # the crest coping's overhang past each face
XS = lines(-46.5, 46.5, 1.0, (-44, -26, -10.4, 26, 44, we["x0"], ww["x1"]))
# The moat (early): the garden's south wall to under the weirs' downstream face.
sheet("Water early", wt.EARLY, XS, lines(-61.5, we["z_down"], 1.0), lambda x, z: True)
# The lake (middle): from the weirs' crest (upstream edge) north to the reservoir's spillway / the dyke and the quay
# (under them) and up the strips beside the terraces.
z_lake_top = sp["z_down"]


def lake(x, z):
    if z < we["z_up"] + EDGE:
        return False
    if z < z_lake_top:
        return True
    if z < 33.0:
        return x < -26 or x > 26
    return x < -44 or x > 44


sheet("Water middle", wt.MIDDLE, XS, lines(we["z_up"] + EDGE, 60.5, 1.0, (z_lake_top, 33.0)), lake)

# ------------------------------------------------------------------ the crests, piers and abutments
stone = Mesh("Weir stonework")


def pier(x0, x1, z0, z1, top, bottom=-2.25, ball=True):
    """A rusticated stone pier (a mortar core 5 cm inside its blocks), a cap and a gold ball."""
    stone.box((x0 + .05, z0 + .05, bottom), (x1 - .05, z1 - .05, top - .2), M["grout"])
    n = max(2, round((top - .16 - bottom) / .55))
    for ci in range(n):
        y0 = bottom + (top - .16 - bottom) * ci / n
        y1 = bottom + (top - .16 - bottom) * (ci + 1) / n
        inset = .035 if ci % 2 else 0
        stone.box((x0 + inset, z0 + inset, y0 + .015), (x1 - inset, z1 - inset, y1 - .015), random.choice(STONES[:2]), .04)
    stone.box((x0 - .06, z0 - .06, top - .16), (x1 + .06, z1 + .06, top), M["stone3"], .035)
    if ball:
        stone.lathe([(0, top), (.13, top), (.09, top + .06), (.05, top + .1), (0, top + .1)],
                    Matrix.Translation(((x0 + x1) / 2, (z0 + z1) / 2, 0)), GOLD, 20)
        stone.sphere(((x0 + x1) / 2, (z0 + z1) / 2, top + .24), .15, GOLD, subdiv=3)


def crest_works(f, pier_top):
    # The crest wall under the bays: courses from below the lower pool to the crest, its coping the crest itself.
    for a, b in f["spans"]:
        kit_wall_run(stone, "x", f["z_down"], a, b, 0, f["z_up"] - f["z_down"], f["crest"] - .012, -2.25)
    # Piers between the bays and abutments at the ends, standing out 0.25 m both sides of the wall.
    z0, z1 = f["z_down"] - .25, f["z_up"] + .25
    edges = [f["x0"]] + [x for span in f["spans"] for x in span] + [f["x1"]]
    for x0, x1 in zip(edges[0::2], edges[1::2]):
        pier(x0, x1, z0, z1, pier_top)


crest_works(sp, wt.LATE + .65)
crest_works(we, wt.MIDDLE + .8)
crest_works(ww, wt.MIDDLE + .8)
stone.done()

# ------------------------------------------------------------------ the falling water


def fall_sheet(name, f, mat, seed):
    """One bay's falling water: the profile swept across the bay, its vertex colours and UVs for the fall shader."""
    prof = wt.profile(f)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    col = bm.loops.layers.float_color.new("Col")
    up_edge = f["z_up"] + EDGE * (1 if f["way"] < 0 else -1)
    sheets = []
    for k, (a, b) in enumerate(f["spans"]):
        a, b = a + .015, b - .015                    # just clear of the piers' faces
        n_across = max(2, int(round((b - a) / .4)))
        grid = []
        for s, y, t, g in prof:
            z = up_edge + f["way"] * s
            row = []
            for j in range(n_across + 1):
                u = j / n_across
                row.append((bm.verts.new((a + (b - a) * u, z, y)), u * (b - a), u, t, g))
            grid.append(row)
        bay_seed = ((seed * 7 + k * 13) % 17) / 17.0
        for r0, r1 in zip(grid, grid[1:]):
            for j in range(n_across):
                quad = (r0[j], r1[j], r1[j + 1], r0[j + 1])          # along x across: its front faces out and up
                face = bm.faces.new([q[0] for q in quad])
                face.smooth = True
                for loop, q in zip(face.loops, quad):
                    loop[uv].uv = (q[1], wt.V_REF * q[3])
                    loop[col] = (q[2], q[4], bay_seed, 0.0)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    DRESS.objects.link(ob)
    return ob


fall_sheet("Fall spillway", sp, M["fall_upper"], 1)
fall_sheet("Fall weir E", we, M["fall_lower"], 2)
fall_sheet("Fall weir W", ww, M["fall_lower"], 3)

# Where each falls lands (for the water shader's churn lines in Unity): the landing line's ends and its pool. In the
# renders, a preview of that churn (Unity draws it in the water shader, so it is not exported): a lighter, fading band.
def churn_material(alpha):
    cm = bpy.data.materials.new("Context | churn %.2f" % alpha)
    cm.use_nodes = True
    c = cm.node_tree.nodes["Principled BSDF"]
    c.inputs["Base Color"].default_value = (*srgb((.82, .94, 1.0)), 1)
    c.inputs["Roughness"].default_value = .4
    c.inputs["Alpha"].default_value = alpha
    for attr_name, value in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
        try:
            setattr(cm, attr_name, value)
        except Exception:
            pass
    return cm


CHURN = [churn_material(a) for a in (.22, .14, .07)]
churn = Mesh("Context churn", CTX)
for name, f in FALLS.items():
    z_land = f["z_down"] - EDGE - f["throw"]
    print("LANDING %-9s x %.2f .. %.2f  z %.3f  pool %.2f" % (name, f["x0"], f["x1"], z_land, f["lower"]))
    reach = .5 + .6 * f["H"]                  # the churned water spreads further from a higher fall
    for a, b in f["spans"]:
        # Three overlapping bands, each wider and fainter, so it fades out instead of ending on a line.
        for k, mat in enumerate(CHURN):
            grow = (k + 1) / len(CHURN)
            churn.box((a + .05 - .15 * k, z_land - reach * grow, f["lower"] + .004 + .002 * k),
                      (b - .05 + .15 * k, f["z_down"] - .02, f["lower"] + .006 + .002 * k), mat)
churn.done()

# ------------------------------------------------------------------ renders
scale = Mesh("Scale pawns", SCALE)
for px, py, pz in ((-8, 29.5, 3.8), (5.5, -14.5, 0.0), (-28, 3.5, 1.8)):
    pawn(scale, px, py, pz)
scale.done()
render_setup()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, "WaterSample_v2.blend"))
shoot("water_v2", "overview", (58, -62, 48), (0, 2, -2), 24)
shoot("water_v2", "tiers_side", (60, -24, 9), (0, 12, -.5), 22)
shoot("water_v2", "spillway_lake", (-10.5, 21, 4.2), (-18.5, 32.4, 1.2), 28)
shoot("water_v2", "spillway", (-6.5, 28.6, 5.6), (-18, 32.4, 1.0), 28)
shoot("water_v2", "spillway_low", (-17, 25.5, .9), (-18, 33, 1.3), 26)
shoot("water_v2", "weir_east", (25, -22.5, .6), (15, -15.6, -1.2), 26)
shoot("water_v2", "weir_close", (14.5, -19.6, -.9), (14.5, -15.6, -1.0), 28)
# P1 at the bottom of its bob (0.35 down): its tub's feet over the lake.
for ob in bpy.data.objects:
    if ob.name.startswith("Bobbing platform P1") and ob.type == "MESH":
        ob.location.z -= wt.PLATFORM_BOB
shoot("water_v2", "p1_low", (-15.2, -2.2, .25), (-21, 2.5, -.35), 30)
print("WATER SAMPLE DONE")

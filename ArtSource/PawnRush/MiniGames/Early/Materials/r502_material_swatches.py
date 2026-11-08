# R502 phase-1 material swatches for the C drawbridge (and the rest of phase 1 later).
#
# The R501 model used one painted colour image per material. The R502 concept sheets
# (Docs/PawnRush/ConceptArt/R502_DrawbridgeSheets) show PBR materials: puffy rounded stone blocks with per-block colour
# shifts and worn edges, grooved planks, riveted dark plates and teal water with caustics. This script builds a real
# high-poly sample of each surface and renders it straight down into tileable maps:
#
#   PR_Early_StoneBrick  2 m tile  BaseColor · Normal · Roughness · AO · Height
#   PR_Early_WoodPlank   2 m tile  BaseColor · Normal · Roughness · AO · Height
#   PR_Early_MetalPlate  1 m tile  BaseColor · Normal · Roughness · AO · Height · Metallic
#   PR_Early_Water       2 m tile  Normal · Caustic (generated, periodic; the colour lives in the Unity water shader)
#
# Tiling: every sample object is placed with linked copies one period away on all eight sides, so geometry, AO and the
# pixel filter wrap across the tile edge. Shader noise uses object coordinates, which the copies share.
# Normal maps are OpenGL style (green = +V), what Unity expects. Base colour holds no light or shadow (R499 4.3).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r502_material_swatches.py -- [--bake] [--preview] [--res N]
#
#   --bake     render the maps into Textures/ (--res sets the 2 m tile size, default 2048; the 1 m metal tile is half)
#   --preview  render swatch_render.png from Textures/ and swatch_compare.png (concept sheet 3's swatch row on top)
import bpy, bmesh, math, os, sys, random, tempfile
import numpy as np
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
TEX = os.path.join(HERE, "Textures")
WORK = os.path.join(tempfile.gettempdir(), "r502_swatch_work")
CONCEPT = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R502_DrawbridgeSheets", "03_props_materials.webp")
FONT = "C:/Windows/Fonts/malgun.ttf"

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048
os.makedirs(TEX, exist_ok=True)
os.makedirs(WORK, exist_ok=True)


# ------------------------------------------------------------------ small helpers
def lin(h):
    """'#RRGGBB' (sRGB) -> linear RGBA."""
    c = [int(h[i:i + 2], 16) / 255. for i in (1, 3, 5)]
    return tuple(x / 12.92 if x <= .04045 else ((x + .055) / 1.055) ** 2.4 for x in c) + (1.,)


def to_srgb(a):
    a = np.clip(a, 0., 1.)
    return np.where(a <= .0031308, a * 12.92, 1.055 * np.power(a, 1 / 2.4) - .055)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def node(nt, kind, x=0, y=0, **inp):
    n = nt.nodes.new(kind)
    n.location = (x, y)
    for k, v in inp.items():
        n.inputs[k].default_value = v
    return n


def link(nt, a, b):
    nt.links.new(a, b)


def ramp(nt, fac, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    while len(els) > 1:
        els.remove(els[-1])
    for i, (p, c) in enumerate(stops):
        e = els[0] if i == 0 else els.new(p)
        e.position = p
        e.color = lin(c) if isinstance(c, str) else c
    link(nt, fac, r.inputs["Fac"])
    return r.outputs["Color"]


def mixc(nt, fac, a, b):
    """Colour mix through ShaderNodeMix (RGBA); a/b are sockets or RGBA tuples."""
    m = nt.nodes.new("ShaderNodeMix")
    m.data_type = "RGBA"
    s = {i.identifier: i for i in m.inputs}
    for key, val in (("A_Color", a), ("B_Color", b)):
        if isinstance(val, tuple):
            s[key].default_value = val
        else:
            link(nt, val, s[key])
    if isinstance(fac, float):
        s["Factor_Float"].default_value = fac
    else:
        link(nt, fac, s["Factor_Float"])
    return [o for o in m.outputs if o.identifier == "Result_Color"][0]


def math_(nt, op, a, b=None, c=None):
    m = nt.nodes.new("ShaderNodeMath")
    m.operation = op
    for i, v in enumerate((a, b, c)):
        if v is None:
            continue
        if isinstance(v, (int, float)):
            m.inputs[i].default_value = v
        else:
            link(nt, v, m.inputs[i])
    return m.outputs[0]


def vscale(nt, col, s):
    m = nt.nodes.new("ShaderNodeVectorMath")
    m.operation = "SCALE"
    link(nt, col, m.inputs[0])
    if isinstance(s, (int, float)):
        m.inputs["Scale"].default_value = s
    else:
        link(nt, s, m.inputs["Scale"])
    return m.outputs[0]


def vmul(nt, a, b):
    m = nt.nodes.new("ShaderNodeVectorMath")
    m.operation = "MULTIPLY"
    link(nt, a, m.inputs[0])
    link(nt, b, m.inputs[1])
    return m.outputs[0]


def maprange(nt, v, a0, a1, b0, b1):
    m = nt.nodes.new("ShaderNodeMapRange")
    link(nt, v, m.inputs["Value"])
    m.inputs["From Min"].default_value, m.inputs["From Max"].default_value = a0, a1
    m.inputs["To Min"].default_value, m.inputs["To Max"].default_value = b0, b1
    m.clamp = True
    return m.outputs["Result"]


def noise_tex(nt, vec, scale, detail=4., rough=.55, dist=0., w=None):
    n = nt.nodes.new("ShaderNodeTexNoise")
    if w is not None:
        n.noise_dimensions = "4D"
        n.inputs["W"].default_value = w
    link(nt, vec, n.inputs["Vector"])
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.inputs["Distortion"].default_value = dist
    return n.outputs["Fac"]


# ------------------------------------------------------------------ bake materials: every surface is emission-only and
# a "pass" rewires what it emits (albedo, roughness, metallic, encoded normal, height, AO).
BAKE_MATS = []


def bake_material(name, albedo, rough, metal=0., bump=None, bump_strength=.3, bump_dist=.003, ao_dist=.08):
    """albedo/rough/metal/bump are callables (nt, ctx) -> socket or a constant."""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    tc = node(nt, "ShaderNodeTexCoord")
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_type = "OBJECT"
    attr.attribute_name = "tint"
    ctx = {"obj": tc.outputs["Object"], "tint": attr.outputs["Fac"]}

    def sock(v, kind):
        if callable(v):
            return v(nt, ctx)
        if kind == "color":
            r = nt.nodes.new("ShaderNodeRGB")
            r.outputs[0].default_value = lin(v) if isinstance(v, str) else v
            return r.outputs[0]
        r = nt.nodes.new("ShaderNodeValue")
        r.outputs[0].default_value = v
        return r.outputs[0]

    s = {"albedo": sock(albedo, "color"), "rough": sock(rough, "f"), "metal": sock(metal, "f")}
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    bn = nt.nodes.new("ShaderNodeBump")
    bn.inputs["Strength"].default_value = bump_strength
    bn.inputs["Distance"].default_value = bump_dist
    link(nt, geo.outputs["Normal"], bn.inputs["Normal"])
    if bump is not None:
        link(nt, bump(nt, ctx), bn.inputs["Height"])
    enc = nt.nodes.new("ShaderNodeVectorMath")
    enc.operation = "MULTIPLY_ADD"
    link(nt, bn.outputs["Normal"], enc.inputs[0])
    enc.inputs[1].default_value = (.5, .5, .5)
    enc.inputs[2].default_value = (.5, .5, .5)
    s["normal"] = enc.outputs[0]
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    link(nt, geo.outputs["Position"], sep.inputs[0])
    s["height"] = sep.outputs["Z"]
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 32
    ao.inputs["Distance"].default_value = ao_dist
    link(nt, bn.outputs["Normal"], ao.inputs["Normal"])
    s["ao"] = ao.outputs["AO"]
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Strength"].default_value = 1.
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    link(nt, em.outputs[0], out.inputs["Surface"])
    BAKE_MATS.append((m, s, em))
    return m


def wire(pass_):
    for m, s, em in BAKE_MATS:
        nt = m.node_tree
        for l in list(em.inputs["Color"].links):
            nt.links.remove(l)
        link(m.node_tree, s[pass_], em.inputs["Color"])


# ------------------------------------------------------------------ geometry
def eval_mesh(ob):
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    old = ob.data
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(old)
    return me


def box(name, sx, sy, sz, cuts=0, bevel=.02, seg=3, subsurf=0, angle=30):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.)
    for v in bm.verts:
        v.co.x *= sx
        v.co.y *= sy
        v.co.z *= sz
    if cuts:
        bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=cuts, use_grid_fill=True)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    if bevel:
        b = ob.modifiers.new("bevel", "BEVEL")
        b.width = bevel
        b.segments = seg
        b.limit_method = "ANGLE"
        b.angle_limit = math.radians(angle)
    if subsurf:
        s = ob.modifiers.new("sub", "SUBSURF")
        s.levels = s.render_levels = subsurf
    return eval_mesh(ob)


def deform(me, fn):
    n = len(me.vertices)
    co = np.empty(n * 3, np.float32)
    nr = np.empty(n * 3, np.float32)
    me.vertices.foreach_get("co", co)
    me.vertices.foreach_get("normal", nr)
    co = co.reshape(-1, 3)
    nr = nr.reshape(-1, 3)
    for i in range(n):
        co[i] = fn(Vector(co[i]), Vector(nr[i]))
    me.vertices.foreach_set("co", co.ravel())
    me.update()


def smooth(me):
    me.polygons.foreach_set("use_smooth", [True] * len(me.polygons))
    me.update()


def place(me, mat, loc, P, rot=(0., 0., 0.), tint=0.):
    """One sample object plus its eight neighbours one period away (the tile wraps)."""
    if mat.name not in [m.name for m in me.materials if m]:
        me.materials.append(mat)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            ob = bpy.data.objects.new(me.name, me)
            ob.location = (loc[0] + dx * P[0], loc[1] + dy * P[1], loc[2])
            ob.rotation_euler = rot
            ob["tint"] = tint
            bpy.context.scene.collection.objects.link(ob)


def floor(mat, P, z):
    me = bpy.data.meshes.new("floor")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new("floor", me)
    ob.scale = (P[0] * 2, P[1] * 2, 1)
    ob.location = (P[0] / 2, P[1] / 2, z)
    ob["tint"] = .5
    bpy.context.scene.collection.objects.link(ob)


# ------------------------------------------------------------------ the three sampled surfaces
def stone(nt, c):
    # per-block colour shift (tint) x broad mottling x fine speckle; cream limestone, warm peach-cream as concept sheet 3 (R502)
    base = ramp(nt, c["tint"], [(0., "#DDB896"), (.3, "#E6C6A6"), (.6, "#EBD2B6"), (.85, "#E0C2A8"), (1., "#D6BCA2")])
    mot = maprange(nt, noise_tex(nt, c["obj"], 2.4, 5., .62), .3, .7, .8, 1.1)
    spk = maprange(nt, noise_tex(nt, c["obj"], 38., 3., .7), .35, .7, .93, 1.02)
    return vscale(nt, vscale(nt, base, mot), spk)


def stone_rough(nt, c):
    return maprange(nt, noise_tex(nt, c["obj"], 9., 4.), .3, .7, .78, .93)


def stone_bump(nt, c):
    a = noise_tex(nt, c["obj"], 16., 8., .62)
    b = noise_tex(nt, c["obj"], 55., 2., .5)
    return math_(nt, "MULTIPLY_ADD", b, .35, a)


def build_stone(P=2.):
    random.seed(5021)
    mat = bake_material("PR_Early_StoneBrick", stone, stone_rough, 0., stone_bump, .35, .004, .09)
    mortar = bake_material("mortar", "#A88B6E", .95, 0., lambda nt, c: noise_tex(nt, c["obj"], 30., 4.), .3, .002, .09)
    floor(mortar, (P, P), .075)
    rows, rh, gap = 5, P / 5, .024
    for r in range(rows):
        y = r * rh + rh / 2
        x = random.uniform(0, P)
        end = x + P
        while x < end - 1e-4:
            L = random.uniform(.38, .78)
            if end - x - L < .34:
                L = end - x if end - x < .8 else (end - x) / 2
            h = .16 + random.uniform(-.012, .012)
            me = box("stone", L - gap, rh - gap, h, cuts=10, bevel=.075, seg=5, subsurf=2)
            hx, hy, hz = (L - gap) / 2, (rh - gap) / 2, h / 2
            puff = random.uniform(.03, .045)
            seed = Vector((random.uniform(0, 99), random.uniform(0, 99), random.uniform(0, 99)))

            def fn(p, n, hx=hx, hy=hy, hz=hz, puff=puff, seed=seed):
                rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.)
                d = Vector()
                if n.z > .3:
                    d.z += puff * (math.cos(rx * math.pi / 2) ** .5) * (math.cos(ry * math.pi / 2) ** .5)
                q = p * 3.5 + seed
                d += n * (noise.noise(q) * .01 + noise.noise(q * 3.3) * .003)
                edge = max(0., (max(rx, ry) - .7) / .3)
                chip = min(0., noise.noise(p * 9. + seed * 2.) + .15) + .4 * min(0., noise.noise(p * 23. + seed))
                d += n * (chip * .035 * edge)
                return p + d
            deform(me, fn)
            smooth(me)
            rot = (random.uniform(-.008, .008), random.uniform(-.008, .008), random.uniform(-.012, .012))
            place(me, mat, (x + L / 2, y, hz), (P, P), rot, random.random())
            x += L
    return P


def wood(nt, c):
    mapn = nt.nodes.new("ShaderNodeMapping")
    link(nt, c["obj"], mapn.inputs["Vector"])
    off = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY", c["tint"], 13.7), off.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", c["tint"], 7.3), off.inputs["Z"])
    link(nt, off.outputs[0], mapn.inputs["Location"])
    mapn.inputs["Scale"].default_value = (.25, 3.2, 3.2)
    wv = nt.nodes.new("ShaderNodeTexWave")
    wv.wave_type = "BANDS"
    wv.bands_direction = "Y"
    link(nt, mapn.outputs[0], wv.inputs["Vector"])
    wv.inputs["Scale"].default_value = .9
    wv.inputs["Distortion"].default_value = 9.
    wv.inputs["Detail"].default_value = 4.
    wv.inputs["Detail Scale"].default_value = 1.4
    grain = ramp(nt, wv.outputs["Fac"], [(0., "#6A3417"), (.35, "#8C4A22"), (.7, "#A85F2E"), (1., "#B8703A")])
    tint = ramp(nt, c["tint"], [(0., (.86, .84, .82, 1.)), (.5, (1., 1., 1., 1.)), (1., (1.06, 1., .94, 1.))])
    streak = maprange(nt, noise_tex(nt, mapn.outputs[0], 3., 6., .6), .3, .7, .88, 1.06)
    w2 = nt.nodes.new("ShaderNodeTexWave")
    w2.wave_type = "BANDS"
    w2.bands_direction = "Y"
    link(nt, mapn.outputs[0], w2.inputs["Vector"])
    w2.inputs["Scale"].default_value = 7.
    w2.inputs["Distortion"].default_value = 5.
    w2.inputs["Detail"].default_value = 2.
    fine = maprange(nt, w2.outputs["Fac"], .55, 1., 1.03, .78)
    return vscale(nt, vscale(nt, vmul(nt, grain, tint), streak), fine)


def wood_rough(nt, c):
    return maprange(nt, noise_tex(nt, c["obj"], 6., 4.), .3, .7, .66, .8)


def wood_bump(nt, c):
    mapn = nt.nodes.new("ShaderNodeMapping")
    link(nt, c["obj"], mapn.inputs["Vector"])
    mapn.inputs["Scale"].default_value = (.25, 3.2, 3.2)
    wv = nt.nodes.new("ShaderNodeTexWave")
    wv.wave_type = "BANDS"
    wv.bands_direction = "Y"
    link(nt, mapn.outputs[0], wv.inputs["Vector"])
    wv.inputs["Scale"].default_value = 2.2
    wv.inputs["Distortion"].default_value = 7.
    wv.inputs["Detail"].default_value = 3.
    return math_(nt, "MULTIPLY_ADD", noise_tex(nt, c["obj"], 40., 3.), .3, wv.outputs["Fac"])


def build_wood(P=2.):
    random.seed(5022)
    mat = bake_material("PR_Early_WoodPlank", wood, wood_rough, 0., wood_bump, .25, .002, .05)
    under = bake_material("under", "#2B1A11", 1., 0., None, 0., .001, .05)
    floor(under, (P, P), -.06)
    rows, pitch, gap, endgap = 8, P / 8, .014, .01
    for r in range(rows):
        y = r * pitch + pitch / 2
        x = random.uniform(0, P)
        end = x + P
        while x < end - 1e-4:
            L = random.uniform(.8, 1.4)
            if end - x - L < .5:
                L = end - x
            t = .05
            me = box("plank", L - endgap, pitch - gap, t, cuts=8, bevel=.012, seg=3, subsurf=1)
            hx, hy = (L - endgap) / 2, (pitch - gap) / 2
            bow = random.uniform(-.004, .004)
            seed = Vector((random.uniform(0, 99), random.uniform(0, 99), 0.))

            def fn(p, n, hx=hx, hy=hy, bow=bow, seed=seed):
                u = p.x / hx
                d = Vector((0., 0., bow * (1 - u * u)))
                q = Vector((p.x * 1.2, p.y * 9., p.z)) + seed
                d += n * noise.noise(q) * .0018
                rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.)
                edge = max(0., (max(rx, ry) - .85) / .15)
                d += n * min(0., noise.noise(p * 11. + seed) + .15) * .008 * edge
                return p + d
            deform(me, fn)
            smooth(me)
            rot = (random.uniform(-.006, .006), random.uniform(-.004, .004), random.uniform(-.003, .003))
            place(me, mat, (x + L / 2, y, t / 2 - .012), (P, P), rot, random.random())
            x += L
    return P


def metal(nt, c):
    mot = noise_tex(nt, c["obj"], 3.2, 7., .62)
    base = ramp(nt, mot, [(.25, "#3A3633"), (.5, "#4D4844"), (.75, "#625C57")])
    rust = maprange(nt, noise_tex(nt, c["obj"], 7., 4., .6, w=3.), .6, .72, 0., .3)
    return mixc(nt, rust, base, lin("#5B4232"))


def metal_rough(nt, c):
    mot = noise_tex(nt, c["obj"], 5., 6.)
    rust = maprange(nt, noise_tex(nt, c["obj"], 7., 4., .6, w=3.), .6, .72, 0., .3)
    return math_(nt, "ADD", maprange(nt, mot, .3, .7, .45, .68), rust)


def metal_metal(nt, c):
    rust = maprange(nt, noise_tex(nt, c["obj"], 7., 4., .6, w=3.), .6, .72, 0., .8)
    return math_(nt, "SUBTRACT", .7, rust)


def metal_bump(nt, c):
    a = noise_tex(nt, c["obj"], 22., 8., .6)
    b = noise_tex(nt, c["obj"], 5., 3.)
    return math_(nt, "MULTIPLY_ADD", b, .8, a)


def build_metal(P=1.):
    random.seed(5023)
    mat = bake_material("PR_Early_MetalPlate", metal, metal_rough, metal_metal, metal_bump, .18, .002, .05)
    rivet = bake_material("rivet", "#5E5E5B", .38, .9, lambda nt, c: noise_tex(nt, c["obj"], 60., 2.), .1, .001, .05)
    back = bake_material("back", "#141518", .9, .5, None, 0., .001, .05)
    floor(back, (P, P), -.03)
    size, t = P - .036, .03
    me = box("plate", size, size, t, cuts=16, bevel=.012, seg=4, subsurf=1)
    h = size / 2

    def fn(p, n):
        d = Vector()
        if n.z > .3:
            d.z += noise.noise(p * 6.) * .0012 + noise.noise(p * 18.) * .0004
        r = max(abs(p.x), abs(p.y)) / h
        d += n * min(0., noise.noise(p * 14.) + .2) * .004 * max(0., (r - .9) / .1)
        return p + d
    deform(me, fn)
    smooth(me)
    place(me, mat, (P / 2, P / 2, 0.), (P, P), tint=.5)
    rme = bpy.data.meshes.new("rivet")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=12, radius=.036)
    for v in bm.verts:
        v.co.z *= .55
    bm.to_mesh(rme)
    bm.free()
    smooth(rme)
    inset = .085
    for ix, iy in ((0, 0), (1, 0), (0, 1), (1, 1)):
        px = P / 2 - h + inset + ix * (size - 2 * inset)
        py = P / 2 - h + inset + iy * (size - 2 * inset)
        place(rme, rivet, (px, py, t / 2 - .002), (P, P), tint=random.random())
    return P


# ------------------------------------------------------------------ rendering the maps
def bake_scene(P, res):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.use_denoising = False
    sc.cycles.filter_width = 1.0
    sc.cycles.max_bounces = 1
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.file_format = "OPEN_EXR"
    sc.render.image_settings.color_depth = "32"
    w = bpy.data.worlds.new("black")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.
    sc.world = w
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = P
    cam.clip_end = 50
    co = bpy.data.objects.new("cam", cam)
    co.location = (P / 2, P / 2, 5.)
    sc.collection.objects.link(co)
    sc.camera = co


def render_pass(name, pass_, samples):
    sc = bpy.context.scene
    wire(pass_)
    sc.cycles.samples = samples
    path = os.path.join(WORK, f"{name}_{pass_}.exr")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    bpy.data.images.remove(img)
    return a.reshape(h, w, 4)


def write_png(path, rgb, colorspace):
    h, w = rgb.shape[:2]
    if rgb.ndim == 2:
        rgb = np.repeat(rgb[:, :, None], 3, 2)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((h, w, 1), np.float32)], 2).astype(np.float32)
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    img.colorspace_settings.name = colorspace
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    print("wrote", path)


def blur_wrap(a, n=1):
    for _ in range(n):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.
    return a


def bake(name, build, res, metallic=False):
    reset()
    BAKE_MATS.clear()
    P = build()
    bake_scene(P, res)
    out = lambda s: os.path.join(TEX, f"{name}_{s}.png")
    write_png(out("BaseColor"), to_srgb(render_pass(name, "albedo", 16)[:, :, :3]), "sRGB")
    write_png(out("Roughness"), render_pass(name, "rough", 16)[:, :, 0], "Non-Color")
    if metallic:
        write_png(out("Metallic"), render_pass(name, "metal", 16)[:, :, 0], "Non-Color")
    n = render_pass(name, "normal", 16)[:, :, :3] * 2 - 1
    n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
    write_png(out("Normal"), n * .5 + .5, "Non-Color")
    z = render_pass(name, "height", 16)[:, :, 0]
    lo, hi = np.percentile(z, .5), np.percentile(z, 99.5)
    write_png(out("Height"), (z - lo) / max(hi - lo, 1e-6), "Non-Color")
    write_png(out("AO"), blur_wrap(render_pass(name, "ao", 64)[:, :, 0], 1), "Non-Color")


# ------------------------------------------------------------------ water: periodic maps made straight in numpy
def worley(N, k, seed, warp=.0):
    rng = np.random.default_rng(seed)
    pts = rng.random((k, 2)).astype(np.float32)
    u = (np.arange(N, dtype=np.float32) + .5) / N
    U, V = np.meshgrid(u, u)
    if warp:
        U = (U + warp * np.sin(2 * np.pi * (V * 2 + .3)) ) % 1.
        V = (V + warp * np.sin(2 * np.pi * (U * 3 + .7)) ) % 1.
    f1 = np.full((N, N), 9., np.float32)
    f2 = np.full((N, N), 9., np.float32)
    for px, py in pts:
        dx = np.abs(U - px)
        dx = np.minimum(dx, 1 - dx)
        dy = np.abs(V - py)
        dy = np.minimum(dy, 1 - dy)
        d = np.sqrt(dx * dx + dy * dy)
        f2 = np.where(d < f1, f1, np.minimum(f2, d))
        f1 = np.minimum(f1, d)
    return f1, f2



def bake_water(res):
    N = max(256, res // 2)
    def caustic(k, seed, warp, freq):
        n = N // freq
        f1, f2 = worley(n, k, seed, warp)
        c = 1 - np.clip((f2 - f1) / .032, 0, 1)
        c = c ** 2.6
        return np.tile(c, (freq, freq))
    c = caustic(70, 1, .03, 1) * .8 + caustic(56, 2, .025, 1) * .45
    u = (np.arange(N, dtype=np.float32) + .5) / N
    U, V = np.meshgrid(u, u)
    glow = .75 + .25 * np.sin(2 * np.pi * (U + 2 * V) + 1.) * np.sin(2 * np.pi * (2 * U - V) + 2.)
    c = np.clip(blur_wrap(c * glow, 2) * 1.15, 0, 1)
    rng = np.random.default_rng(7)
    u = (np.arange(N, dtype=np.float32) + .5) / N
    U, V = np.meshgrid(u, u)
    h = np.zeros((N, N), np.float32)
    for kx, ky, a in ((1, 2, .5), (3, -1, .35), (-2, 3, .3), (5, 2, .18), (-4, -5, .12), (7, -3, .08)):
        h += a * np.sin(2 * np.pi * (kx * U + ky * V) + rng.random() * 6.283)
    f1, _ = worley(N, 30, 3, .02)
    h += 1.2 * f1
    s = 3.5 / N
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) / 2 / s * .004
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) / 2 / s * .004
    n = np.stack([-gx, -gy, np.ones_like(h)], 2)
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    write_png(os.path.join(TEX, "PR_Early_Water_Normal.png"), n * .5 + .5, "Non-Color")
    write_png(os.path.join(TEX, "PR_Early_Water_Caustic.png"), c, "Non-Color")


# ------------------------------------------------------------------ preview: swatch tiles laid out like concept sheet 3
# Concept sheet 3 swatch row (fractions of the 798 x 590 view): crop x 28..790, y 428..582; one 1 m tile ~ 121 px.
CROP = (28 / 798, 790 / 798, 428 / 590, 582 / 590)
PX_M = 121.
SLOTS = [101, 254, 404, 558, 713]       # stone, wood, metal, (gold), water centres in view px
WIDTH_M = (790 - 28) / PX_M


def load_img(path, cs):
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = cs
    return img


def preview_material(name, kind, span):
    m = bpy.data.materials.new("prev " + name)
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = node(nt, "ShaderNodeOutputMaterial")
    bsdf = node(nt, "ShaderNodeBsdfPrincipled")
    link(nt, bsdf.outputs[0], out.inputs["Surface"])
    uv = node(nt, "ShaderNodeUVMap")
    mp = node(nt, "ShaderNodeMapping")
    link(nt, uv.outputs[0], mp.inputs["Vector"])
    mp.inputs["Scale"].default_value = (span, span, 1.)

    def tex(suffix, cs):
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = load_img(os.path.join(TEX, f"{name}_{suffix}.png"), cs)
        link(nt, mp.outputs[0], t.inputs["Vector"])
        return t.outputs["Color"]
    nm = nt.nodes.new("ShaderNodeNormalMap")
    if kind == "water":
        link(nt, tex("Normal", "Non-Color"), nm.inputs["Color"])
        nm.inputs["Strength"].default_value = .8
        c = tex("Caustic", "Non-Color")
        col = ramp(nt, c, [(0., "#0580AE"), (.25, "#0CA4CE"), (.6, "#3ACDE4"), (1., "#F2FFFF")])
        link(nt, col, bsdf.inputs["Base Color"])
        link(nt, col, bsdf.inputs["Emission Color"])
        link(nt, math_(nt, "MULTIPLY", c, .35), bsdf.inputs["Emission Strength"])
        bsdf.inputs["Roughness"].default_value = .06
    else:
        link(nt, tex("Normal", "Non-Color"), nm.inputs["Color"])
        ao = tex("AO", "Non-Color")
        aog = math_(nt, "POWER", ao, .7)
        link(nt, vscale(nt, tex("BaseColor", "sRGB"), aog), bsdf.inputs["Base Color"])
        link(nt, tex("Roughness", "Non-Color"), bsdf.inputs["Roughness"])
        if kind == "metal":
            link(nt, tex("Metallic", "Non-Color"), bsdf.inputs["Metallic"])
    link(nt, nm.outputs[0], bsdf.inputs["Normal"])
    return m


def plain(name, col, rough=.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(col)
    b.inputs["Roughness"].default_value = rough
    return m


def slab(mat, cx, cz, size, bevel, uvk, uvo=(0., 0.)):
    me = box("slab", size, .08, size, cuts=0, bevel=bevel, seg=8, angle=30)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((p.x / size + .5) * uvk + uvo[0], (p.z / size + .5) * uvk + uvo[1])
    me.materials.append(mat)
    smooth(me)
    ob = bpy.data.objects.new("slab", me)
    ob.location = (cx, 0., cz)
    bpy.context.scene.collection.objects.link(ob)


def text(body, x, z, size=.12, col="#5A4632"):
    cu = bpy.data.curves.new("t", "FONT")
    cu.body = body
    cu.font = bpy.data.fonts.load(FONT, check_existing=True)
    cu.size = size
    cu.align_x = "CENTER"
    cu.align_y = "CENTER"
    ob = bpy.data.objects.new("t", cu)
    ob.location = (x, -.06, z)
    ob.rotation_euler = (math.pi / 2, 0, 0)
    ob.data.materials.append(plain("text", col, .8))
    bpy.context.scene.collection.objects.link(ob)


def preview():
    sc = reset()
    H = 3.25
    xs = [(s - 28) / PX_M for s in SLOTS]
    entries = [("PR_Early_StoneBrick", "stone", .55, "크림 돌 벽돌 (2 m 타일)"),
               ("PR_Early_WoodPlank", "wood", .5, "나무 판자 (2 m 타일)"),
               ("PR_Early_MetalPlate", "metal", 1., "리벳 어두운 금속판 (1 m 타일)"),
               None,
               ("PR_Early_Water", "water", .5, "물 (2 m 타일)")]
    z1, z2 = 2.3, .92
    text("R502 견본 · 1줄: 사진과 같은 크기  ·  2줄: 2×2 반복(이음새 확인)", WIDTH_M / 2, 3.08, .13)
    for x, e in zip(xs, entries):
        if e is None:
            text("금 — 도개교에는 안 씀", x, z1, .1, "#9A8A74")
            continue
        name, kind, span, label = e
        slab(preview_material(name, kind, span), x, z1, 1., .07, 1.)
        slab(preview_material(name, kind, 2.), x, z2, 1., .012, 1.)
        text(label, x, z1 - .68, .1)
        text("2×2 반복", x, z2 - .64, .1)
    bd = bpy.data.meshes.new("bd")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.)
    bm.to_mesh(bd)
    bm.free()
    bd.materials.append(plain("backdrop", "#F5EDE0", .95))
    bo = bpy.data.objects.new("bd", bd)
    bo.rotation_euler = (math.pi / 2, 0, 0)
    bo.location = (WIDTH_M / 2, .12, H / 2)
    sc.collection.objects.link(bo)
    # warm, soft key from the upper left, cream sky fill (concept sheet lighting)
    la = bpy.data.lights.new("key", "AREA")
    la.energy = 950
    la.size = 5.
    la.color = (1., .93, .84)
    lo = bpy.data.objects.new("key", la)
    lo.location = (WIDTH_M / 2 - 4, -6, H + 3.5)
    lo.rotation_euler = (Vector((WIDTH_M / 2, 0, H / 2)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = lin("#FFF4E6")
    bg.inputs["Strength"].default_value = .45
    sc.world = w
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = WIDTH_M
    co = bpy.data.objects.new("cam", cam)
    co.location = (WIDTH_M / 2, -10, H / 2)
    co.rotation_euler = (math.pi / 2, 0, 0)
    sc.collection.objects.link(co)
    sc.camera = co
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1600
    sc.render.resolution_y = int(1600 * H / WIDTH_M)
    sc.render.image_settings.file_format = "PNG"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    sc.view_settings.exposure = 0.
    path = os.path.join(HERE, "swatch_render.png")
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    compare(path)


def pixels(img):
    w, h = img.size
    a = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def compare(render_path):
    r = bpy.data.images.load(render_path, check_existing=False)
    R = pixels(r)
    c = bpy.data.images.load(CONCEPT, check_existing=False)
    C = pixels(c)
    h, w = C.shape[:2]
    x0, x1 = int(CROP[0] * w), int(CROP[1] * w)
    y0, y1 = h - int(CROP[3] * h), h - int(CROP[2] * h)     # rows are stored bottom-up
    crop = C[y0:y1, x0:x1].copy()
    ci = bpy.data.images.new("crop", crop.shape[1], crop.shape[0], alpha=True)
    ci.pixels.foreach_set(crop.ravel())
    W = R.shape[1]
    ci.scale(W, int(crop.shape[0] * W / crop.shape[1]))
    top = pixels(ci)
    bar = np.ones((6, W, 4), np.float32) * np.array([.55, .45, .35, 1.], np.float32)
    sheet = np.concatenate([R, bar, top], 0)
    out = bpy.data.images.new("compare", W, sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = os.path.join(HERE, "swatch_compare.png")
    out.file_format = "PNG"
    out.save()
    print("wrote", out.filepath_raw)


if "--bake" in ARGS:
    bake("PR_Early_StoneBrick", build_stone, RES)
    bake("PR_Early_WoodPlank", build_wood, RES)
    bake("PR_Early_MetalPlate", build_metal, RES // 2, metallic=True)
    bake_water(RES)
if "--preview" in ARGS:
    preview()

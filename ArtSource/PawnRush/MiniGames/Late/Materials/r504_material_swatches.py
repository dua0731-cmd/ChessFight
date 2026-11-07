# R504 phase-3 (upper palace) material swatches for G Royal Procession (and H, I and the war room later).
#
# Same method as the phase-1 swatches (Early/Materials/r502_material_swatches.py): build a real high-poly sample of each
# surface, render it straight down into tileable PBR maps, then lay the maps out next to the swatch row of the R504
# concept sheet (Docs/PawnRush/ConceptArt/R504_RoyalProcession/G_03_props_materials.png).
#
#   PR_Late_MarbleCream   2 m tile  BaseColor · Normal · Roughness · AO · Height     1 m polished slabs, warm grey veins
#   PR_Late_MarbleNavy    2 m tile  BaseColor · Normal · Roughness · AO · Height     dais marble #29334A, white veins
#   PR_Late_Maple         2 m tile  BaseColor · Normal · Roughness · AO · Height     light path-cell boards
#   PR_Late_Walnut        2 m tile  BaseColor · Normal · Roughness · AO · Height     dark path-cell boards
#   PR_Late_Velvet        1 m tile  BaseColor · Normal · Roughness · AO              purple velvet #604B72 (runner, border)
#   PR_Late_Ermine        1 m tile  BaseColor · Normal · Roughness · AO · Height     white fur + black tails, G slow cells only
#   PR_Late_Brass         1 m tile  BaseColor · Normal · Roughness · Metallic        matte brushed brass #9C8354
#   PR_Late_DoorNavy      2 m tile  BaseColor · Normal · Roughness · AO · Height     navy painted door planks #28344D
#
# Functional gold (#E2B23C, emission 0.6) stays the game's own flat material and is only shown in the preview.
# Tiling: every sample object has linked copies one period away on all eight sides, so geometry, AO and the pixel filter
# wrap. Normal maps are OpenGL style (green = +V). Base colour holds no light or shadow (R499 4.3).
#
#   "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --python r504_material_swatches.py -- [--bake] [--preview] [--res N] [--only NAME]
import bpy, bmesh, math, os, sys, random, tempfile
import numpy as np
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "..", ".."))
TEX = os.path.join(HERE, "Textures")
WORK = os.path.join(tempfile.gettempdir(), "r504_swatch_work")
CONCEPT = os.path.join(ROOT, "Docs", "PawnRush", "ConceptArt", "R504_RoyalProcession", "G_03_props_materials.png")
FONT = "C:/Windows/Fonts/malgun.ttf"

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RES = int(ARGS[ARGS.index("--res") + 1]) if "--res" in ARGS else 2048
ONLY = ARGS[ARGS.index("--only") + 1] if "--only" in ARGS else None
os.makedirs(TEX, exist_ok=True)
os.makedirs(WORK, exist_ok=True)


# ------------------------------------------------------------------ small helpers (copied from r502 so the two stay independent)
def lin(h):
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


def tnoise(nt, c, P, fx, fy, detail=4., rough=.55, dist=0., seed=0.):
    """Noise that repeats every P metres in object X and Y: the plane is wrapped onto a torus in 4D noise space
    (cos/sin of X -> vector x,y; cos/sin of Y -> vector z and W). fx/fy = noise features per tile along X/Y."""
    sx = nt.nodes.new("ShaderNodeSeparateXYZ")
    link(nt, c["obj"], sx.inputs[0])
    k = 2 * math.pi / P
    ax = math_(nt, "MULTIPLY", sx.outputs["X"], k)
    ay = math_(nt, "MULTIPLY", sx.outputs["Y"], k)
    rx, ry = fx / (2 * math.pi), fy / (2 * math.pi)
    v = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY_ADD", math_(nt, "COSINE", ax), rx, seed), v.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ax), rx), v.inputs["Y"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "COSINE", ay), ry), v.inputs["Z"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    link(nt, v.outputs[0], n.inputs["Vector"])
    link(nt, math_(nt, "MULTIPLY", math_(nt, "SINE", ay), ry), n.inputs["W"])
    n.inputs["Scale"].default_value = 1.
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.inputs["Distortion"].default_value = dist
    return n.outputs["Fac"]


def psines(p, P, terms):
    """Periodic height (period P in X and Y) from (kx, ky, amplitude, phase) sine terms."""
    return sum(a * math.sin(2 * math.pi * (kx * p.x + ky * p.y) / P + ph) for kx, ky, a, ph in terms)


def sine_terms(seed, n, kmax, amp):
    rng = random.Random(seed)
    out = []
    for _ in range(n):
        kx, ky = rng.randint(-kmax, kmax), rng.randint(-kmax, kmax)
        if kx == 0 and ky == 0:
            kx = 1
        out.append((kx, ky, amp * rng.uniform(.4, 1.) / math.hypot(kx, ky) ** .5, rng.uniform(0, 6.283)))
    return out


def mapping(nt, vec, scale=(1., 1., 1.), loc=None):
    m = nt.nodes.new("ShaderNodeMapping")
    link(nt, vec, m.inputs["Vector"])
    m.inputs["Scale"].default_value = scale
    if loc is not None:
        link(nt, loc, m.inputs["Location"])
    return m.outputs[0]


def offset_by_tint(nt, c, kx=13.7, kz=7.3):
    off = nt.nodes.new("ShaderNodeCombineXYZ")
    link(nt, math_(nt, "MULTIPLY", c["tint"], kx), off.inputs["X"])
    link(nt, math_(nt, "MULTIPLY", c["tint"], kz), off.inputs["Z"])
    return off.outputs[0]


def wave(nt, vec, scale, dist, detail, dscale=1., direction="Y", profile="SIN"):
    w = nt.nodes.new("ShaderNodeTexWave")
    w.wave_type = "BANDS"
    w.bands_direction = direction
    w.wave_profile = profile
    link(nt, vec, w.inputs["Vector"])
    w.inputs["Scale"].default_value = scale
    w.inputs["Distortion"].default_value = dist
    w.inputs["Detail"].default_value = detail
    w.inputs["Detail Scale"].default_value = dscale
    return w.outputs["Fac"]


# ------------------------------------------------------------------ bake materials (emission only; a pass rewires what it emits)
BAKE_MATS = []


def bake_material(name, albedo, rough, metal=0., bump=None, bump_strength=.3, bump_dist=.003, ao_dist=.08):
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
        link(nt, s[pass_], em.inputs["Color"])


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


def wrap_noise(p, P, f):
    """Seamless noise on a P-periodic plane: blend of four shifted samples (bilinear in the tile position)."""
    u, v = (p.x % P) / P, (p.y % P) / P
    a = noise.noise(Vector((p.x * f, p.y * f, p.z * f)))
    b = noise.noise(Vector(((p.x - P) * f, p.y * f, p.z * f)))
    c = noise.noise(Vector((p.x * f, (p.y - P) * f, p.z * f)))
    d = noise.noise(Vector(((p.x - P) * f, (p.y - P) * f, p.z * f)))
    return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v


# ------------------------------------------------------------------ marble (cream walls/floor, navy dais)
def marble_albedo(base_stops, vein_col, vein2_col, vein_amt):
    def f(nt, c):
        o = mapping(nt, c["obj"], (1., 1., 1.), offset_by_tint(nt, c, 11.3, 5.9))
        base = ramp(nt, c["tint"], base_stops)
        cloud = maprange(nt, noise_tex(nt, o, 1.6, 6., .6), .3, .7, .9, 1.08)
        col = vscale(nt, base, cloud)
        # thin sharp veins: |wave| near zero, wave distorted by noise, diagonal
        v1 = wave(nt, mapping(nt, o, (1., .45, 1.)), 1.3, 14., 6., 1.6, "DIAGONAL")
        sharp = maprange(nt, math_(nt, "ABSOLUTE", math_(nt, "SUBTRACT", v1, .5)), 0., .05, vein_amt, 0.)
        v2 = wave(nt, mapping(nt, o, (.6, 1., 1.)), 2.7, 10., 5., 2.2, "DIAGONAL")
        fine = maprange(nt, math_(nt, "ABSOLUTE", math_(nt, "SUBTRACT", v2, .5)), 0., .025, vein_amt * .6, 0.)
        col = mixc(nt, sharp, col, lin(vein_col))
        return mixc(nt, fine, col, lin(vein2_col))
    return f


def marble_rough(nt, c):
    return maprange(nt, noise_tex(nt, c["obj"], 7., 4.), .3, .7, .14, .3)


def marble_bump(nt, c):
    return math_(nt, "MULTIPLY", noise_tex(nt, c["obj"], 30., 4.), .3)


def build_marble(name, base_stops, vein, vein2, vein_amt, grout, seed, P=2.):
    def build():
        random.seed(seed)
        mat = bake_material(name, marble_albedo(base_stops, vein, vein2, vein_amt), marble_rough, 0., marble_bump, .06, .001, .05)
        g = bake_material("grout", grout, .85, 0., None, 0., .001, .05)
        floor(g, (P, P), .02)
        n, gap = 2, .006
        s = P / n
        for i in range(n):
            for j in range(n):
                me = box("slab", s - gap, s - gap, .05, cuts=6, bevel=.006, seg=3)
                h = (s - gap) / 2

                def fn(p, nr, h=h):
                    r = max(abs(p.x), abs(p.y)) / h
                    edge = max(0., (r - .97) / .03)
                    return p + nr * min(0., noise.noise(p * 30.) + .3) * .0015 * edge
                deform(me, fn)
                smooth(me)
                place(me, mat, (i * s + s / 2, j * s + s / 2, .025), (P, P),
                      (0., 0., random.uniform(-.0015, .0015)), random.random())
        return P
    return build


# ------------------------------------------------------------------ wood boards (maple, walnut) for the path cells
def wood_albedo(stops):
    def f(nt, c):
        o = mapping(nt, c["obj"], (.22, 3.4, 3.4), offset_by_tint(nt, c))
        grain = ramp(nt, wave(nt, o, .9, 8., 4., 1.4), stops)
        tint = ramp(nt, c["tint"], [(0., (.88, .86, .84, 1.)), (.5, (1., 1., 1., 1.)), (1., (1.05, 1., .95, 1.))])
        streak = maprange(nt, noise_tex(nt, o, 3., 6., .6), .3, .7, .9, 1.05)
        fine = maprange(nt, wave(nt, o, 7., 5., 2.), .55, 1., 1.02, .82)
        return vscale(nt, vscale(nt, vmul(nt, grain, tint), streak), fine)
    return f


def wood_rough(lo, hi):
    return lambda nt, c: maprange(nt, noise_tex(nt, c["obj"], 6., 4.), .3, .7, lo, hi)


def wood_bump(nt, c):
    o = mapping(nt, c["obj"], (.22, 3.4, 3.4))
    return math_(nt, "MULTIPLY_ADD", noise_tex(nt, c["obj"], 40., 3.), .25, wave(nt, o, 2.2, 7., 3.))


def build_boards(name, stops, rough, seed, P=2.):
    def build():
        random.seed(seed)
        mat = bake_material(name, wood_albedo(stops), wood_rough(*rough), 0., wood_bump, .18, .0015, .04)
        under = bake_material("under", "#1E140E", 1., 0., None, 0., .001, .04)
        floor(under, (P, P), -.05)
        rows, pitch, gap, endgap = 10, P / 10, .006, .006
        for r in range(rows):
            y = r * pitch + pitch / 2
            x = random.uniform(0, P)
            end = x + P
            while x < end - 1e-4:
                L = random.uniform(.9, 1.5)
                if end - x - L < .5:
                    L = end - x
                t = .04
                me = box("board", L - endgap, pitch - gap, t, cuts=8, bevel=.006, seg=3, subsurf=1)
                hx, hy = (L - endgap) / 2, (pitch - gap) / 2
                seed_v = Vector((random.uniform(0, 99), random.uniform(0, 99), 0.))

                def fn(p, n, hx=hx, hy=hy, sv=seed_v):
                    d = n * noise.noise(Vector((p.x * 1.2, p.y * 9., p.z)) + sv) * .0008
                    rx, ry = min(abs(p.x) / hx, 1.), min(abs(p.y) / hy, 1.)
                    edge = max(0., (max(rx, ry) - .9) / .1)
                    return p + d + n * min(0., noise.noise(p * 11. + sv) + .2) * .003 * edge
                deform(me, fn)
                smooth(me)
                rot = (random.uniform(-.002, .002), random.uniform(-.002, .002), 0.)
                place(me, mat, (x + L / 2, y, t / 2 - .01), (P, P), rot, random.random())
                x += L
        return P
    return build


# ------------------------------------------------------------------ continuous surfaces (velvet, ermine, brass): one P x P
# plane per tile copy. All shader noise is tnoise and all displacement is psines, so both repeat exactly every P.
def sheet(name, mat, P, cuts, height_fn=None, z=0., tint=.5):
    # a flat grid, not a box: a smooth-shaded box averages the side faces into the top normals and bakes a dome
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=max(1, cuts), y_segments=max(1, cuts), size=P / 2)
    bm.to_mesh(me)
    bm.free()
    if height_fn is not None:
        def fn(p, n):
            if n.z < .3:
                return p
            w = Vector((p.x + P / 2, p.y + P / 2, 0.))      # tile space, so copies line up
            return p + Vector((0., 0., height_fn(w)))
        deform(me, fn)
    smooth(me)
    place(me, mat, (P / 2, P / 2, z), (P, P), tint=tint)


# velvet: soft pile, crushed patches carry the sheen in the colour
def velvet_albedo(P):
    def f(nt, c):
        crush = tnoise(nt, c, P, 3., 6., 5., .6, 1.5)
        col = ramp(nt, crush, [(.2, "#45245F"), (.5, "#5A3278"), (.8, "#6C4290")])
        pile = maprange(nt, tnoise(nt, c, P, 260., 260., 2., .5, 0., 7.), .3, .7, .93, 1.04)
        return vscale(nt, col, pile)
    return f


def velvet_bump(P):
    return lambda nt, c: math_(nt, "MULTIPLY_ADD", tnoise(nt, c, P, 260., 260., 2., .5, 0., 7.), .15,
                               tnoise(nt, c, P, 3., 6., 5., .6, 1.5))


def build_velvet(P=1.):
    mat = bake_material("PR_Late_Velvet", velvet_albedo(P), .92, 0., velvet_bump(P), .35, .004, .04)
    terms = sine_terms(5046, 10, 4, .004)
    sheet("cloth", mat, P, 60, lambda w: psines(w, P, terms))
    return P


# ermine: white fur with black heraldic tails (G slow cells only)
def fur_albedo(P):
    def f(nt, c):
        strands = maprange(nt, tnoise(nt, c, P, 90., 540., 3., .5), .3, .7, .86, 1.)
        clump = maprange(nt, tnoise(nt, c, P, 9., 9., 4., .55, 0., 3.), .3, .7, .92, 1.02)
        return vscale(nt, vscale(nt, ramp(nt, c["tint"], [(0., "#EFE9DF"), (1., "#F7F3EC")]), strands), clump)
    return f


def fur_bump(P):
    return lambda nt, c: math_(nt, "MULTIPLY_ADD", tnoise(nt, c, P, 9., 9., 4., .55, 0., 3.), .6,
                               tnoise(nt, c, P, 90., 540., 3., .5))


def build_ermine(P=1.):
    random.seed(5045)
    fur = bake_material("PR_Late_Ermine", fur_albedo(P), .95, 0., fur_bump(P), .6, .004, .03)
    tail = bake_material("tail", ramp_const("#1A1816"), .85, 0., None, 0., .001, .03)
    terms = sine_terms(5048, 14, 9, .003)
    sheet("fur", fur, P, 80, lambda w: psines(w, P, terms))
    # heraldic ermine spot: a tail that widens toward its foot, three small dots above. 4 x 4 staggered per metre.
    tail_me = bpy.data.meshes.new("tail")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=1.)
    for v in bm.verts:
        y = v.co.y
        v.co.x *= .016 * (1. + .9 * max(0., -y))
        v.co.y *= .07
        v.co.z *= .012
    bm.to_mesh(tail_me)
    bm.free()
    smooth(tail_me)
    dot_me = bpy.data.meshes.new("dot")
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=.015)
    for v in bm.verts:
        v.co.z *= .5
    bm.to_mesh(dot_me)
    bm.free()
    smooth(dot_me)
    n = 4
    for i in range(n):
        for j in range(n):
            x = (i + .5 + (.5 if j % 2 else 0.)) * P / n
            y = (j + .5) * P / n
            r = random.uniform(-.12, .12)
            place(tail_me, tail, (x, y - .02, .014), (P, P), (0., 0., r), random.random())
            for dx, dy in ((-.026, .062), (.026, .062), (0., .09)):
                place(dot_me, tail, (x + dx, y + dy, .014), (P, P), tint=random.random())
    return P


def ramp_const(h):
    return h


# matte brushed brass (decor metal; functional gold stays the game's flat colour)
def brass_albedo(P):
    def f(nt, c):
        brush = maprange(nt, tnoise(nt, c, P, 30., 1200., 3., .5), .3, .7, .82, 1.1)
        patina = maprange(nt, tnoise(nt, c, P, 4., 4., 5., .6, 0., 5.), .55, .75, 0., .35)
        return mixc(nt, patina, vscale(nt, ramp(nt, c["tint"], [(0., "#9C8354"), (1., "#A88E5E")]), brush), lin("#6E5E3C"))
    return f


def brass_rough(P):
    return lambda nt, c: math_(nt, "ADD", maprange(nt, tnoise(nt, c, P, 30., 1200., 3.), .3, .7, .5, .64),
                               maprange(nt, tnoise(nt, c, P, 4., 4., 5., .6, 0., 5.), .55, .75, 0., .2))


def build_brass(P=1.):
    mat = bake_material("PR_Late_Brass", brass_albedo(P), brass_rough(P), .95,
                        lambda nt, c: tnoise(nt, c, P, 30., 1200., 3., .5), .08, .0006, .02)
    sheet("sheet", mat, P, 0, None, 0., .4)
    return P


# ------------------------------------------------------------------ navy painted door planks (vertical boards, worn edges)
def door_albedo(P):
    def f(nt, c):
        paint = ramp(nt, c["tint"], [(0., "#242F46"), (.5, "#28344D"), (1., "#2F3C57")])
        brush = maprange(nt, tnoise(nt, c, P, 6., 160., 4., .6, 2., 11.), .25, .75, .74, 1.16)
        return vscale(nt, paint, brush)
    return f


def door_bump(P):
    return lambda nt, c: math_(nt, "MULTIPLY_ADD", tnoise(nt, c, P, 60., 60., 3., .5, 0., 2.), .25,
                               tnoise(nt, c, P, 6., 160., 3., .55, 0., 11.))


def build_door(P=2.):
    random.seed(5047)
    mat = bake_material("PR_Late_DoorNavy", door_albedo(P),
                        lambda nt, c: maprange(nt, tnoise(nt, c, P, 6., 6., 4.), .3, .7, .5, .62), 0., door_bump(P), .12, .0015, .05)
    under = bake_material("under", "#0E1220", 1., 0., None, 0., .001, .05)
    floor(under, (P, P), -.05)
    cols, pitch, gap = 9, P / 9, .012
    for r in range(cols):
        y = r * pitch + pitch / 2
        me = box("plank", P, pitch - gap, .045, cuts=10, bevel=.01, seg=3, subsurf=1)
        hy = (pitch - gap) / 2
        terms = sine_terms(5100 + r, 6, 6, .004)

        def fn(p, n, hy=hy, terms=terms):
            ry = min(abs(p.y) / hy, 1.)
            edge = max(0., (ry - .88) / .12)
            wear = min(0., psines(Vector((p.x + P / 2, 0., 0.)), P, terms) * 60. + .1)
            return p + n * wear * .004 * edge
        deform(me, fn)
        smooth(me)
        place(me, mat, (P / 2, y, .01), (P, P), tint=random.random())
    return P


# ------------------------------------------------------------------ rendering the maps
def bake_scene(P, res):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
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


def heal_band(a, bx, by):
    """Continuous sheets: the bump/AO near the outer pixels is computed across two touching objects and comes out
    wrong, and door plank end bevels leave a groove at the X wrap. Classic offset blend: mix the map with a copy rolled
    by half a tile, the copy weighted 1 at the wrap edge and 0 from bx/by (fraction of the tile) inward, so the
    copy's own seam (now in the middle) never shows."""
    for axis, f in ((1, bx), (0, by)):
        if f <= 0:
            continue
        n = a.shape[axis]
        d = np.minimum(np.arange(n) + .5, n - np.arange(n) - .5) / n        # 0 at the wrap edge, .5 in the middle
        w = np.clip(1. - d / f, 0., 1.)
        w = w * w * (3 - 2 * w)
        shape = [1] * a.ndim
        shape[axis] = n
        w = w.reshape(shape)
        a = a * (1 - w) + np.roll(a, n // 2, axis) * w
    return a


HEAL = {"PR_Late_Velvet": (.12, .12), "PR_Late_Ermine": (.06, .06), "PR_Late_Brass": (.12, .12), "PR_Late_DoorNavy": (.04, 0.)}
MAPS = ("BaseColor", "Roughness", "Metallic", "Normal", "Height", "AO")


def heal_files(name):
    """Apply heal_band to maps already on disk (raw values in, raw values out)."""
    bx, by = HEAL[name]
    for s in MAPS:
        p = os.path.join(TEX, f"{name}_{s}.png")
        if not os.path.exists(p):
            continue
        im = bpy.data.images.load(p, check_existing=False)
        im.colorspace_settings.name = "Non-Color"
        w, h = im.size
        a = np.empty(w * h * 4, np.float32)
        im.pixels.foreach_get(a)
        bpy.data.images.remove(im)
        a = heal_band(a.reshape(h, w, 4)[:, :, :3].copy(), bx, by)
        if s == "Normal":
            n = a * 2 - 1
            n /= np.maximum(np.linalg.norm(n, axis=2, keepdims=True), 1e-6)
            a = n * .5 + .5
        write_png(p, a, "Non-Color")


def bake(name, build, res, metallic=False, height=True, ao=True):
    if ONLY and ONLY != name:
        return
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
    if height:
        z = render_pass(name, "height", 16)[:, :, 0]
        lo, hi = np.percentile(z, .5), np.percentile(z, 99.5)
        write_png(out("Height"), (z - lo) / max(hi - lo, 1e-6), "Non-Color")
    if ao:
        write_png(out("AO"), blur_wrap(render_pass(name, "ao", 64)[:, :, 0], 1), "Non-Color")
    if name in HEAL:
        heal_files(name)


# ------------------------------------------------------------------ preview: swatches in the order of the R504 sheet's swatch row
# G_03_props_materials.png swatch row (fractions of the image): x 25..1510 / 1536, y 790..1010 / 1024. Nine cubes.
CROP = (25 / 1536, 1510 / 1536, 790 / 1024, 1010 / 1024)
ENTRIES = [("PR_Late_MarbleCream", "base", 1., "크림 대리석 (2 m)"),
           ("PR_Late_MarbleNavy", "base", 1., "남색 대리석 (2 m)"),
           ("PR_Late_Maple", "base", 1., "메이플 (2 m)"),
           ("PR_Late_Walnut", "base", 1., "월넛 (2 m)"),
           ("PR_Late_Velvet", "base", 1., "보라 벨벳 (1 m)"),
           ("PR_Late_Ermine", "base", 1., "담비털 (1 m)"),
           ("PR_Late_Brass", "metal", 1., "무광 황동 (1 m)"),
           ("GOLD", "gold", 1., "기능 금 (게임 색)"),
           ("PR_Late_DoorNavy", "base", 1., "남색 문 판자 (2 m)")]
TILE_M = {"PR_Late_MarbleCream": 2., "PR_Late_MarbleNavy": 2., "PR_Late_Maple": 2., "PR_Late_Walnut": 2.,
          "PR_Late_Velvet": 1., "PR_Late_Ermine": 1., "PR_Late_Brass": 1., "PR_Late_DoorNavy": 2.}


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
    if kind == "gold":
        bsdf.inputs["Base Color"].default_value = lin("#E2B23C")
        bsdf.inputs["Metallic"].default_value = 1.
        bsdf.inputs["Roughness"].default_value = .25
        bsdf.inputs["Emission Color"].default_value = lin("#E2B23C")
        bsdf.inputs["Emission Strength"].default_value = .6
        return m
    uv = node(nt, "ShaderNodeUVMap")
    mp = node(nt, "ShaderNodeMapping")
    link(nt, uv.outputs[0], mp.inputs["Vector"])
    mp.inputs["Scale"].default_value = (span, span, 1.)

    def tex(suffix, cs):
        p = os.path.join(TEX, f"{name}_{suffix}.png")
        if not os.path.exists(p):
            return None
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = load_img(p, cs)
        link(nt, mp.outputs[0], t.inputs["Vector"])
        return t.outputs["Color"]
    nm = nt.nodes.new("ShaderNodeNormalMap")
    link(nt, tex("Normal", "Non-Color"), nm.inputs["Color"])
    col = tex("BaseColor", "sRGB")
    ao = tex("AO", "Non-Color")
    if ao is not None:
        col = vscale(nt, col, math_(nt, "POWER", ao, .7))
    link(nt, col, bsdf.inputs["Base Color"])
    link(nt, tex("Roughness", "Non-Color"), bsdf.inputs["Roughness"])
    if kind == "metal":
        link(nt, tex("Metallic", "Non-Color"), bsdf.inputs["Metallic"])
    if name == "PR_Late_Velvet":
        bsdf.inputs["Sheen Weight"].default_value = .6
        bsdf.inputs["Sheen Tint"].default_value = lin("#B9A3CF")
    if name == "PR_Late_Ermine":
        bsdf.inputs["Sheen Weight"].default_value = .4
    link(nt, nm.outputs[0], bsdf.inputs["Normal"])
    return m


def plain(name, col, rough=.9):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(col)
    b.inputs["Roughness"].default_value = rough
    return m


def slab(mat, cx, cz, size, bevel, uvk):
    me = box("slab", size, .08, size, cuts=0, bevel=bevel, seg=8, angle=30)
    uvl = me.uv_layers.new(name="UVMap")
    for poly in me.polygons:
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            uvl.data[li].uv = ((p.x / size + .5) * uvk, (p.z / size + .5) * uvk)
    me.materials.append(mat)
    smooth(me)
    ob = bpy.data.objects.new("slab", me)
    ob.location = (cx, 0., cz)
    bpy.context.scene.collection.objects.link(ob)


def text(body, x, z, size=.12, col="#4A3C5A"):
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
    step, size = 1.25, 1.
    W = step * len(ENTRIES)
    H = 3.2
    z1, z2 = 2.15, .82
    text("R504 3단계(윗궁) 견본 · 1줄: 1 m 크기  ·  2줄: 2×2 반복(이음새 확인)", W / 2, 3.0, .13)
    for i, (name, kind, _, label) in enumerate(ENTRIES):
        x = step * (i + .5)
        tile = TILE_M.get(name, 1.)
        slab(preview_material(name, kind, size / tile), x, z1, size, .07, 1.)
        if kind != "gold":
            slab(preview_material(name, kind, 2.), x, z2, size, .012, 1.)
            text("2×2 반복", x, z2 - .62, .09)
        else:
            text("텍스처 없음", x, z2, .09, "#8A7C6A")
        text(label, x, z1 - .64, .09)
    bd = bpy.data.meshes.new("bd")
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.)
    bm.to_mesh(bd)
    bm.free()
    bd.materials.append(plain("backdrop", "#F5EDE0", .95))
    bo = bpy.data.objects.new("bd", bd)
    bo.rotation_euler = (math.pi / 2, 0, 0)
    bo.location = (W / 2, .12, H / 2)
    sc.collection.objects.link(bo)
    # soft cool-white key from above (skylight) plus a warm parchment fill, like the concept sheet
    la = bpy.data.lights.new("key", "AREA")
    la.energy = 1100
    la.size = 6.
    la.color = (1., .96, .9)
    lo = bpy.data.objects.new("key", la)
    lo.location = (W / 2 - 4, -6, H + 3.5)
    lo.rotation_euler = (Vector((W / 2, 0, H / 2)) - lo.location).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
    w = bpy.data.worlds.new("sky")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = lin("#FFF4E6")
    bg.inputs["Strength"].default_value = .5
    sc.world = w
    cam = bpy.data.cameras.new("cam")
    cam.type = "ORTHO"
    cam.ortho_scale = W
    co = bpy.data.objects.new("cam", cam)
    co.location = (W / 2, -10, H / 2)
    co.rotation_euler = (math.pi / 2, 0, 0)
    sc.collection.objects.link(co)
    sc.camera = co
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 96
    sc.cycles.use_denoising = True
    sc.render.resolution_x = 1800
    sc.render.resolution_y = int(1800 * H / W)
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "Standard"
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
    R = pixels(bpy.data.images.load(render_path, check_existing=False))
    C = pixels(bpy.data.images.load(CONCEPT, check_existing=False))
    h, w = C.shape[:2]
    x0, x1 = int(CROP[0] * w), int(CROP[1] * w)
    y0, y1 = h - int(CROP[3] * h), h - int(CROP[2] * h)     # rows are stored bottom-up
    crop = C[y0:y1, x0:x1].copy()
    ci = bpy.data.images.new("crop", crop.shape[1], crop.shape[0], alpha=True)
    ci.pixels.foreach_set(crop.ravel())
    W = R.shape[1]
    ci.scale(W, int(crop.shape[0] * W / crop.shape[1]))
    top = pixels(ci)
    bar = np.ones((6, W, 4), np.float32) * np.array([.4, .3, .45, 1.], np.float32)
    sheet = np.concatenate([R, bar, top], 0)
    out = bpy.data.images.new("compare", W, sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = os.path.join(HERE, "swatch_compare.png")
    out.file_format = "PNG"
    out.save()
    print("wrote", out.filepath_raw)


if "--bake" in ARGS:
    cream = [(0., "#E4DCCD"), (.5, "#EAE3D6"), (1., "#DED5C5")]
    navy = [(0., "#253046"), (.5, "#29334A"), (1., "#2E3952")]
    bake("PR_Late_MarbleCream", build_marble("PR_Late_MarbleCream", cream, "#B98E55", "#CDAE84", 1., "#BFB5A4", 5041), RES)
    bake("PR_Late_MarbleNavy", build_marble("PR_Late_MarbleNavy", navy, "#C9CED8", "#7D879C", .8, "#161C2A", 5042), RES)
    bake("PR_Late_Maple", build_boards("PR_Late_Maple", [(0., "#C08A4E"), (.4, "#D29F62"), (.75, "#DFB37A"), (1., "#E6BF88")], (.45, .58), 5043), RES)
    bake("PR_Late_Walnut", build_boards("PR_Late_Walnut", [(0., "#3A2216"), (.35, "#4E2E1C"), (.7, "#653D25"), (1., "#744830")], (.45, .58), 5044), RES)
    bake("PR_Late_Velvet", build_velvet, RES // 2, height=False)
    bake("PR_Late_Ermine", build_ermine, RES // 2)
    bake("PR_Late_Brass", build_brass, RES // 2, metallic=True, height=False, ao=False)
    bake("PR_Late_DoorNavy", build_door, RES)
if "--heal" in ARGS:          # fix the wrap edge of maps baked before heal_band existed (no re-bake)
    for k in HEAL:
        if not ONLY or ONLY == k:
            heal_files(k)
if "--preview" in ARGS:
    preview()
